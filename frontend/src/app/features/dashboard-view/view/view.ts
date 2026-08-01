import {
  AfterViewInit, Component, OnInit, signal, TemplateRef, ViewChild, ViewContainerRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { GridStack } from 'gridstack';
import * as echarts from 'echarts';
import { DashboardService } from '../../../core/services/dashboard.service';
import { WidgetService } from '../../../core/services/widget.service';
import { Widget } from '../../../core/models/dashboard.model';

interface WidgetLayoutStructure {
  id: string;
  title: string;
  type: string;
  x: number; y: number; w: number; h: number;
  rows: Record<string, unknown>[];
  traceability?: { prompt: string; sql: string };
  backgroundColor?: string;   // ⬅ add
  options?: Record<string, any>;
}

@Component({
  selector: 'app-dashboard-view',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './view.html',
  styleUrls: ['./view.css']
})
export class View implements OnInit, AfterViewInit {
  @ViewChild('widgetTemplate') widgetTemplate!: TemplateRef<any>;

  grid!: GridStack;
  dashboardTitle = signal<string>('');
  currentPreset = signal<'custom' | 'grafana' | 'powerbi'>('custom');
  dashboardDescription = signal<string>('');
  widgetCollection: WidgetLayoutStructure[] = [];
  dashboardStyle= 'Custom';
  private gridReady = false;
  private pendingWidgets: Array<{ w: Widget; rows: Record<string, unknown>[] }> = [];

  constructor(
    private vcRef: ViewContainerRef,
    private dashboardService: DashboardService,
    private widgetService: WidgetService,
    private route: ActivatedRoute
  ) {}

 ngOnInit(): void {
  const id = Number(this.route.snapshot.paramMap.get('id'));
  if (!id) {
    console.error('No dashboard id found in route');
    return;
  }
  this.dashboardService.loadById(id).subscribe({
    next: dashboard => {
      this.dashboardTitle.set(dashboard.name);
      this.dashboardDescription.set(dashboard.description ? dashboard.description : "No description available");
      this.dashboardStyle = dashboard.dashboardStyle;
      this.currentPreset.set(this.mapStyle(dashboard.dashboardStyle));
      this.loadWidgets(dashboard.widgets);
    },
    error: err => console.error('Failed to load dashboard', err)
  });
}

  ngAfterViewInit(): void {
    this.grid = GridStack.init({
      column: 12, margin: 6, cellHeight: 90, float: true,
      disableDrag: true, disableResize: true
    });
    this.gridReady = true;
    // Flush any widgets that arrived before the grid was ready
    this.pendingWidgets.forEach(({ w, rows }) => this.addWidget(w, rows));
    this.pendingWidgets = [];
  }

  private mapStyle(style: string): 'custom' | 'grafana' | 'powerbi' {
    if (style === 'Grafana') return 'grafana';
    if (style === 'PowerBi') return 'powerbi';
    return 'custom';
  }

  private mapWidgetType(t: string): string {
    switch (t) {
      case 'KpiCard': return 'kpi';
      case 'Table': return 'table';
      case 'PieChart': return 'pie';
      case 'BarChart': return 'bar';
      case 'LineChart': return 'line';
      default: return 'table';
    }
  }

  private loadWidgets(widgets: Widget[]): void {
    this.widgetCollection = [];
    widgets.forEach(w => {
      this.widgetService.getData(w.id).subscribe({
        next: rows => this.addWidget(w, rows),
        error: err => {
          console.error(`Failed to load data for widget ${w.id}`, err);
          this.addWidget(w, []);
        }
      });
    });
  }

  // Widget.ConfigJson is a free-form jsonb bucket - the Add Widget dialog's
  // Display Options (gridLines, legend, dataLabels, stacked, legendPosition,
  // colorTheme) are stored under configJson.displayOptions. Mirrors
  // design.ts's parseConfig so the View tab renders the same as Design.
  private parseDisplayOptions(configJson: string | undefined): Record<string, any> | undefined {
    if (!configJson) return undefined;
    try {
      const parsed = JSON.parse(configJson);
      return parsed.displayOptions;
    } catch {
      return undefined;
    }
  }

  private addWidget(w: Widget, rows: Record<string, unknown>[]): void {
    const widget: WidgetLayoutStructure = {
      id: String(w.id),
      title: w.title,
      type: this.mapWidgetType(w.widgetType),
      x: w.positionX, y: w.positionY, w: w.width, h: w.height,
      rows,
      traceability: w.generatedSql ? { prompt: '', sql: w.generatedSql } : undefined,
      backgroundColor: w.backgroundColor,
      options: this.parseDisplayOptions(w.configJson)
    };

    // Grid not ready yet — queue and flush in ngAfterViewInit
    if (!this.gridReady) {
      this.pendingWidgets.push({ w, rows });
      return;
    }

    this.widgetCollection.push(widget);

    const gridItem = this.grid.addWidget({
      x: widget.x, y: widget.y, w: widget.w, h: widget.h, id: widget.id, content: ''
    });

    const view = this.vcRef.createEmbeddedView(this.widgetTemplate, { $implicit: widget });
    view.detectChanges();

    const element = view.rootNodes.find(n => n.nodeType === Node.ELEMENT_NODE) as HTMLElement;
    const container = gridItem.querySelector('.grid-stack-item-content');
    if (container && element) {
      container.innerHTML = '';
      container.appendChild(element);
    }

    setTimeout(() => this.renderChart(widget), 100);
  }


 kpiValue(widget: WidgetLayoutStructure): string {
    const row = widget.rows[0];
    if (!row) return '—';
    const values = Object.values(row);
    const val = values[values.length - 1];
    return val === null || val === undefined ? '—' : String(val);
  }

  kpiLabel(widget: WidgetLayoutStructure): string {
    const row = widget.rows[0];
    if (!row) return '';
    const keys = Object.keys(row);
    return keys[keys.length - 1] ?? '';
  }

  tableColumns(widget: WidgetLayoutStructure): string[] {
    return widget.rows[0] ? Object.keys(widget.rows[0]) : [];
  }

  // Custom style only: when the user has picked a per-widget background
  // color, the text needs to switch to whichever of dark/light stays
  // readable against it. Returns null everywhere else so the normal CSS
  // (indigo KPI accent etc.) applies unchanged.
  customTextColor(widget: WidgetLayoutStructure): string | null {
    if (this.currentPreset() !== 'custom' || !widget.backgroundColor) return null;
    return this.contrastColor(widget.backgroundColor);
  }

  // Custom style only: the header row (and table <th>) must not stay on the
  // hardcoded light CSS background once a custom widget color is picked —
  // otherwise the white text chosen above becomes invisible against it.
  // Shades the widget's own color instead, so the header reads as a
  // distinct band and the text picked by customTextColor() stays visible.
  customHeaderBg(widget: WidgetLayoutStructure): string | null {
    if (this.currentPreset() !== 'custom' || !widget.backgroundColor) return null;
    return this.shade(widget.backgroundColor, -12);
  }

  private shade(hex: string, percent: number): string {
    const c = hex.replace('#', '');
    const num = parseInt(c, 16);
    const amt = Math.round(2.55 * percent);
    const r = Math.min(255, Math.max(0, (num >> 16) + amt));
    const g = Math.min(255, Math.max(0, ((num >> 8) & 0x00FF) + amt));
    const b = Math.min(255, Math.max(0, (num & 0x0000FF) + amt));
    return `rgb(${r}, ${g}, ${b})`;
  }

  private contrastColor(hex: string): string {
    const c = hex.replace('#', '');
    if (c.length !== 6) return '#111827';
    const r = parseInt(c.substring(0, 2), 16);
    const g = parseInt(c.substring(2, 4), 16);
    const b = parseInt(c.substring(4, 6), 16);
    const yiq = (r * 299 + g * 587 + b * 114) / 1000;
    return yiq >= 150 ? '#111827' : '#ffffff';
  }

  private renderChart(widget: WidgetLayoutStructure): void {
    if (widget.type !== 'line' && widget.type !== 'pie' && widget.type !== 'bar') return;
    if (widget.rows.length === 0) return;

    const el = document.getElementById(`${widget.type}-${widget.id}`);
    if (!el) return;

    const cols = Object.keys(widget.rows[0]);
    const labelCol = cols[0];
    const valueCol = cols.length > 1 ? cols[cols.length - 1] : cols[0];
    const labels = widget.rows.map(r => String(r[labelCol] ?? ''));
    const values = widget.rows.map(r => Number(r[valueCol] ?? 0));

    const chart = echarts.init(el);

    const palettes: Record<string, string[]> = {
      Default: ['#f2495c', '#ff9830', '#5794f2', '#73bf69', '#b877d9'],
      Vivid: ['#ef4444', '#f97316', '#3b82f6', '#22c55e', '#a855f7'],
      Pastel: ['#fca5a5', '#fdba74', '#93c5fd', '#86efac', '#d8b4fe'],
      Monochrome: ['#1e293b', '#334155', '#475569', '#64748b', '#94a3b8']
    };
    const opts = widget.options ?? {};
    const colors = palettes[opts['colorTheme'] as string] ?? palettes['Default'];
    const showLabels = !!opts['dataLabels'];
    const showLegend = opts['legend'] !== false;
    const showGrid = opts['gridLines'] !== false;
    const stacked = !!opts['stacked'];
    const axisLabel = { rotate: labels.length > 8 ? 30 : 0, overflow: 'truncate', width: 80 };

    const optionMap: Record<string, object> = {
      line: {
        tooltip: { trigger: 'axis' },
        grid: { left: 20, right: 20, top: 50, bottom: 50  },
        xAxis: { type: 'category', data: labels, axisLabel, splitLine: { show: false } },
        yAxis: { type: 'value', splitLine: { show: showGrid } },
        series: [{ type: 'line', smooth: true, data: values, color: colors[0], areaStyle: { opacity: 0.1 }, label: { show: showLabels, position: 'top' } }]
      },
      bar: {
        tooltip: { trigger: 'axis' },
        grid: { left: 50, right: 20, top: 50, bottom: 50  },
        xAxis: { type: 'category', data: labels, axisLabel, splitLine: { show: false } },
        yAxis: { type: 'value', splitLine: { show: showGrid } },
        series: [{
          type: 'bar',
          data: values,
          barMaxWidth: 60,
          stack: stacked ? 'total' : undefined,
          label: { show: showLabels, position: 'top' },
          itemStyle: {
            color: (params: { dataIndex: number }) => colors[params.dataIndex % colors.length]
          }
        }]
      },
      pie: {
        tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
        legend: showLegend ? { orient: 'vertical', left: 'left', type: 'scroll' } : { show: false },
        color: colors,
        series: [{
          type: 'pie', radius: ['40%', '70%'],
          label: { show: showLabels, formatter: '{b}: {d}%' },
          data: widget.rows.map(r => ({ name: String(r[labelCol] ?? ''), value: Number(r[valueCol] ?? 0) }))
        }]
      }
    };

    chart.setOption(optionMap[widget.type] as echarts.EChartsOption);
    new ResizeObserver(() => chart.resize()).observe(el);
  }
}