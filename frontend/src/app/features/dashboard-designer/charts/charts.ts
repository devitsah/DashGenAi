import {
  Component, OnDestroy, ElementRef, ViewChild,
  effect, AfterViewChecked, ChangeDetectorRef, NgZone
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as echarts from 'echarts';
import { BuilderStateService } from '../builder-state.service';

type Tab = 'preview' | 'query';
type ChartOverride = 'auto' | 'bar' | 'line' | 'pie' | 'table' | 'kpi';

@Component({
  selector: 'app-charts',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './charts.html',
  styleUrls: ['./charts.css']
})
export class Charts implements OnDestroy, AfterViewChecked {
  @ViewChild('chartEl') chartEl?: ElementRef<HTMLDivElement>;

  activeTab: Tab = 'preview';
  chartOverride: ChartOverride = 'auto';
  copyStatus = 'Copy';

  private chart?: echarts.ECharts;
  private needsRender = false;
  private resizeObserver?: ResizeObserver;

  readonly chartTypes: { label: string; value: ChartOverride }[] = [
    { label: 'Auto',  value: 'auto'  },
    { label: 'Bar',   value: 'bar'   },
    { label: 'Line',  value: 'line'  },
    { label: 'Pie',   value: 'pie'   },
    { label: 'Table', value: 'table' },
    { label: 'KPI',   value: 'kpi'   }
  ];

  constructor(
    public state: BuilderStateService,
    private cdr: ChangeDetectorRef,
    private zone: NgZone
  ) {
    // React to new results — reset overrides and schedule a render
    effect(() => {
      const r = this.state.lastResult();
      if (r && !r.needsClarification) {
        this.chartOverride = 'auto';
        this.activeTab = 'preview';
        this.scheduleRender();
      }
    });
  }

  // Called after every CD cycle — renders the chart once the *ngIf DOM is ready
  ngAfterViewChecked(): void {
    if (!this.needsRender) return;
    // For table/KPI the DOM is already rendered by *ngIf — just clear the flag.
    // For charts, wait until #chartEl exists in the DOM (created by *ngIf="isChartType").
    if (!this.isChartType) {
      this.needsRender = false;
      return;
    }
    if (this.chartEl?.nativeElement) {
      this.needsRender = false;
      this.zone.runOutsideAngular(() => this.renderChart());
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.chart?.dispose();
  }

  // ── computed helpers ──────────────────────────────────────────────────────

  get hasData(): boolean {
    const r = this.state.lastResult();
    return !!r && !r.needsClarification;
  }

  get sql(): string { return this.state.lastResult()?.generatedSql ?? ''; }

  get executionError(): string | null {
    return this.state.lastResult()?.widgetDataError ?? null;
  }

  get isEmptyResult(): boolean {
    const r = this.state.lastResult();
    return !!r && !r.needsClarification && !this.executionError
      && (r.widgetData?.length ?? 0) === 0;
  }

  // Normalise any backend widget type string → lowercase no-separator key
  // e.g. 'KpiCard' | 'kpi_card' | 'kpi' → 'kpicard'
  //      'BarChart' | 'bar_chart' | 'bar' → 'barchart'
  private normalizeType(raw: string): string {
    return raw.toLowerCase().replace(/[_\s]/g, '');
  }

  get resolvedType(): string {
    const raw = this.chartOverride !== 'auto'
      ? this.chartOverride
      : (this.state.lastResult()?.intentSummary?.widgetType ?? 'table');
    return this.normalizeType(raw);
  }

  get isChartType(): boolean {
    const t = this.resolvedType;
    return t.includes('bar') || t.includes('line') || t.includes('pie');
  }

  get isKpi(): boolean {
    return this.resolvedType.includes('kpi');
  }

  get isTable(): boolean {
    return !this.isChartType && !this.isKpi;
  }

  get tableColumns(): string[] {
    const rows = this.state.lastResult()?.widgetData;
    return rows?.[0] ? Object.keys(rows[0]) : [];
  }

  get tableRows(): Record<string, unknown>[] {
    return this.state.lastResult()?.widgetData ?? [];
  }

  get kpiValue(): string {
    const row = this.tableRows[0];
    if (!row) return '—';
    const values = Object.values(row);
    // Backend puts aggregate/numeric value LAST
    const val = values[values.length - 1];
    return val == null ? '—' : String(val);
  }

  get kpiLabel(): string {
    // Use the last column name as the label (matches kpiValue column)
    const cols = this.tableColumns;
    return cols[cols.length - 1] ?? '';
  }

  // ── interactions ──────────────────────────────────────────────────────────

  setTab(tab: Tab): void {
    this.activeTab = tab;
    if (tab === 'preview') this.scheduleRender();
  }

  setChartType(type: ChartOverride): void {
    this.chartOverride = type;
    if (this.activeTab === 'preview') this.scheduleRender();
  }

  copyQuery(): void {
    if (!this.sql) return;
    navigator.clipboard.writeText(this.sql).then(() => {
      this.copyStatus = 'Copied!';
      setTimeout(() => (this.copyStatus = 'Copy'), 2000);
    });
  }

  // ── rendering ─────────────────────────────────────────────────────────────

  // Mark that a render is needed and trigger a CD cycle so ngAfterViewChecked
  // runs and can find the *ngIf-gated #chartEl once it exists in the DOM.
  private scheduleRender(): void {
    this.needsRender = true;
    this.cdr.detectChanges();
  }

  private renderChart(): void {
    const el = this.chartEl?.nativeElement;
    if (!el) return;

    const rows = this.state.lastResult()?.widgetData;
    if (!rows?.length) return;

    // Dispose previous instance and disconnect old observer
    this.resizeObserver?.disconnect();
    this.chart?.dispose();
    this.chart = echarts.init(el);

    const cols = Object.keys(rows[0]);
    const labelCol = cols[0];
    const valueCol = cols[cols.length - 1];
    const labels = rows.map(r => String(r[labelCol] ?? ''));
    const values = rows.map(r => Number(r[valueCol] ?? 0));
    const t = this.resolvedType;

    let option: object;

    if (t.includes('pie')) {
      option = {
        tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
        legend: { orient: 'vertical', left: 'left', type: 'scroll' },
        series: [{
          type: 'pie',
          radius: ['40%', '70%'],
          data: rows.map(r => ({
            name: String(r[labelCol] ?? ''),
            value: Number(r[valueCol] ?? 0)
          }))
        }]
      };
    } else if (t.includes('line')) {
      option = {
        tooltip: { trigger: 'axis' },
        grid: { left: 50, right: 20, top: 20, bottom: 50 },
        xAxis: {
          type: 'category', data: labels,
          axisLabel: { rotate: labels.length > 8 ? 30 : 0, overflow: 'truncate', width: 80 }
        },
        yAxis: { type: 'value' },
        series: [{
          type: 'line', smooth: true, data: values,
          color: '#00b96b', areaStyle: { opacity: 0.1 }
        }]
      };
    } else {
      // bar (default)
      option = {
        tooltip: { trigger: 'axis' },
        grid: { left: 50, right: 20, top: 20, bottom: 50 },
        xAxis: {
          type: 'category', data: labels,
          axisLabel: { rotate: labels.length > 8 ? 30 : 0, overflow: 'truncate', width: 80 }
        },
        yAxis: { type: 'value' },
        series: [{ type: 'bar', data: values, color: '#1677ff', barMaxWidth: 60 }]
      };
    }

    this.chart.setOption(option as any);

    // Keep chart responsive
    this.resizeObserver = new ResizeObserver(() => {
      this.zone.runOutsideAngular(() => this.chart?.resize());
    });
    this.resizeObserver.observe(el);
  }
}
