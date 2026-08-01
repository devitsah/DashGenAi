import { AfterViewChecked, Component, ElementRef, QueryList, ViewChildren } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import * as echarts from 'echarts';
import { BuilderStateService, ChatMessage } from '../builder-state.service';

const WIDGET_TYPE_CHOICES = ['KPI Card', 'Table', 'Bar Chart', 'Pie Chart', 'Line Chart'];

@Component({
  selector: 'app-datasource',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './datasource.html',
  styleUrls: ['./datasource.css']
})
export class Datasource implements AfterViewChecked {
  @ViewChildren('chartCanvas') chartCanvases!: QueryList<ElementRef<HTMLDivElement>>;

  inputText = '';
  selectedWidgetType: string | null = null;
  readonly widgetTypeChoices = WIDGET_TYPE_CHOICES;

  // Resets per component instance — charts must be re-drawn when DOM is recreated
  private renderedChartIds = new Set<string>();

  constructor(public state: BuilderStateService) {}

  get messages(): ChatMessage[] { return this.state.chatMessages(); }
  get isBusy(): boolean { return this.state.loading(); }

  toggleWidgetType(label: string): void {
    this.selectedWidgetType = this.selectedWidgetType === label ? null : label;
  }

  send(overrideText?: string): void {
    const text = (overrideText ?? this.inputText).trim();
    if (!text || this.isBusy) return;
    this.state.sendMessage(text, overrideText ? null : this.selectedWidgetType);
    this.inputText = '';
    this.selectedWidgetType = null;
  }

  accept(msg: ChatMessage): void { this.state.acceptMessageWidget(msg); }
  reject(msg: ChatMessage): void { this.state.rejectMessageWidget(msg); }

  ngAfterViewChecked(): void {
    this.chartCanvases?.forEach(ref => {
      const el = ref.nativeElement;
      const id = el.getAttribute('data-chart-id');
      if (!id || this.renderedChartIds.has(id)) return;
      const msg = this.messages.find(m => m.chartId === id);
      if (!msg?.previewRows?.length) return;
      this.renderedChartIds.add(id);
      this.renderChart(el, msg);
    });
  }

  private renderChart(el: HTMLDivElement, msg: ChatMessage): void {
    const rows = msg.previewRows!;
    const keys = Object.keys(rows[0]);
    const labelCol = keys[0];
    const valueCol = keys.length > 1 ? keys[keys.length - 1] : keys[0];
    const labels = rows.map(r => String(r[labelCol] ?? ''));
    const values = rows.map(r => Number(r[valueCol] ?? 0));
    const t = msg.widgetType ?? 'bar';
    const chart = echarts.init(el);

    let option: object;
    if (t === 'pie') {
      option = {
        tooltip: { trigger: 'item' },
        legend: { orient: 'horizontal', bottom: 0, textStyle: { fontSize: 10 } },
        series: [{ type: 'pie', radius: ['35%', '65%'],
          data: rows.map(r => ({ name: String(r[labelCol]), value: Number(r[valueCol]) })),
          label: { fontSize: 10 } }]
      };
    } else if (t === 'line') {
      option = {
        tooltip: { trigger: 'axis' },
        grid: { left: 40, right: 10, top: 10, bottom: 30 },
        xAxis: { type: 'category', data: labels, axisLabel: { fontSize: 10, rotate: labels.length > 6 ? 30 : 0 } },
        yAxis: { type: 'value', axisLabel: { fontSize: 10 } },
        series: [{ type: 'line', smooth: true, data: values, color: '#00b96b', areaStyle: { opacity: 0.1 } }]
      };
    } else {
      option = {
        tooltip: { trigger: 'axis' },
        grid: { left: 40, right: 10, top: 10, bottom: 30 },
        xAxis: { type: 'category', data: labels, axisLabel: { fontSize: 10, rotate: labels.length > 6 ? 30 : 0 } },
        yAxis: { type: 'value', axisLabel: { fontSize: 10 } },
        series: [{ type: 'bar', data: values, color: '#1677ff', barMaxWidth: 40 }]
      };
    }
    chart.setOption(option as any);
    new ResizeObserver(() => chart.resize()).observe(el);
  }

  isChart(type: string | undefined): boolean {
    return !!type && (type === 'bar' || type === 'line' || type === 'pie');
  }
  isKpi(type: string | undefined): boolean { return type === 'kpi'; }
  isTable(type: string | undefined): boolean {
    if (!type) return true;
    return !this.isChart(type) && !this.isKpi(type);
  }
  kpiValue(rows: Record<string, unknown>[]): string {
    const vals = rows[0] ? Object.values(rows[0]) : [];
    return vals[vals.length - 1] == null ? '—' : String(vals[vals.length - 1]);
  }
  kpiLabel(rows: Record<string, unknown>[]): string {
    const keys = rows[0] ? Object.keys(rows[0]) : [];
    return keys[keys.length - 1] ?? '';
  }
}
