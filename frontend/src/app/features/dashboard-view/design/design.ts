import {
  AfterViewInit,
  Component,
  EmbeddedViewRef,
  OnInit,
  signal,
  TemplateRef,
  ViewChild,
  ViewContainerRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { GridStack } from 'gridstack';
import * as echarts from 'echarts';
import { Subject } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import Swal from 'sweetalert2';
import { DashboardService } from '../../../core/services/dashboard.service';
import { WidgetService, CreatedWidget } from '../../../core/services/widget.service';
import { Widget } from '../../../core/models/dashboard.model';
import { AddWidgetDialog } from './add-widget-dialog/add-widget-dialog';

interface WidgetLayoutStructure {
  id: string;
  title: string;
  type: string;
  x: number;
  y: number;
  w: number;
  h: number;
  rows: Record<string, unknown>[];
  traceability?: { prompt: string; sql: string };
  backgroundColor?: string;
  html?: string;
  options?: Record<string, any>;
}

@Component({
  selector: 'app-design',
  standalone: true,
  imports: [CommonModule, FormsModule, AddWidgetDialog],
  templateUrl: './design.html',
  styleUrls: ['./design.css']
})
export class Design implements OnInit, AfterViewInit {
  @ViewChild('widgetTemplate') widgetTemplate!: TemplateRef<any>;

  grid!: GridStack;
  dashboardTitle = signal<string>('');
  dashboardDescription = signal<string>('');
  currentPreset = signal<'custom' | 'grafana' | 'powerbi'>('custom');
  widgetCollection: WidgetLayoutStructure[] = [];

  private gridReady = false;
  dashboardId!: number;

  // Tracks each widget's grid DOM element so Delete_Widget can remove it
  // from the GridStack instance directly, without a full re-render.
  private widgetElements = new Map<string, HTMLElement>();

  // Tracks each widget's embedded view so we can force a manual
  // detectChanges() after async updates (rename, etc.) as a safety net,
  // and so we can properly destroy() it when a widget is deleted.
  private widgetViews = new Map<string, EmbeddedViewRef<any>>();

  // Auto-save: layout changes are pushed in here and only actually saved
  // 600ms after the user stops dragging/resizing, so we don't fire a PUT
  // on every intermediate frame of a drag.
  private layoutChange$ = new Subject<WidgetLayoutStructure>();
  autoSaveStatus = signal<'idle' | 'saving' | 'saved' | 'error'>('idle');

  addWidgetOpen = signal(false);

  constructor(
    private vcRef: ViewContainerRef,
    private dashboardService: DashboardService,
    private widgetService: WidgetService,
    private route: ActivatedRoute
  ) {
    this.layoutChange$.pipe(debounceTime(600)).subscribe((widget) => {
      this.autoSaveStatus.set('saving');
      this.widgetService
        .updatePosition(Number(widget.id), widget.x, widget.y, widget.w, widget.h)
        .subscribe({
          next: () => this.autoSaveStatus.set('saved'),
          error: (err) => {
            console.error(`Failed to auto-save widget ${widget.id} layout`, err);
            this.autoSaveStatus.set('error');
          }
        });
    });
  }

  ngOnInit(): void {
    this.dashboardId = Number(this.route.parent?.snapshot.paramMap.get('id'));
    this.dashboardService.loadById(this.dashboardId).subscribe({
      next: (dashboard) => {
        this.dashboardTitle.set(dashboard.name);
        this.dashboardDescription.set(dashboard.description ? dashboard.description : 'No description available');
        this.currentPreset.set(this.mapStyle(dashboard.dashboardStyle));
        this.loadWidgets(dashboard.widgets);
      },
      error: (err) => console.error('Failed to load dashboard', err)
    });
  }

  ngAfterViewInit(): void {
    this.grid = GridStack.init({
      column: 12,
      margin: 6,
      cellHeight: 90,
      float: true,
      draggable: { handle: '.widget-header', cancel: '.no-drag' },
      resizable: { handles: 'all' }
    });

    this.grid.on('change', (_event, items) => this.syncLayout(items));
    this.gridReady = true;

    if (this.widgetCollection.length) {
      const pending = [...this.widgetCollection];
      this.widgetCollection = [];
      pending.forEach((w) => this.mountWidget(w));
    }
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
      case 'AreaChart': return 'area';
      case 'Gauge': return 'gauge';
      case 'TimeSeries': return 'timeseries';
      case 'HtmlWidget': return 'html';
      default: return 'table';
    }
  }

  private loadWidgets(widgets: Widget[]): void {
    this.widgetCollection = [];
    widgets.forEach((w) => {
      this.widgetService.getData(w.id).subscribe({
        next: (rows) => this.addWidget(w, rows),
        error: (err) => {
          console.error(`Failed to load data for widget ${w.id}`, err);
          this.addWidget(w, []);
        }
      });
    });
  }

  // Widget.ConfigJson is a free-form jsonb bucket - html content (HTML widgets)
  // and per-widget display options (legend/gridlines/gauge range/etc, set from
  // the Add Widget dialog) both live there so no extra columns were needed.
  private parseConfig(configJson: string | undefined): { html?: string; options?: Record<string, any> } {
    if (!configJson) return {};
    try {
      const parsed = JSON.parse(configJson);
      return { html: parsed.html, options: parsed.displayOptions };
    } catch {
      return {};
    }
  }

  private addWidget(w: Widget, rows: Record<string, unknown>[]): void {
    const { html, options } = this.parseConfig(w.configJson);
    const widget: WidgetLayoutStructure = {
      id: String(w.id),
      title: w.title,
      type: this.mapWidgetType(w.widgetType),
      x: w.positionX,
      y: w.positionY,
      w: w.width,
      h: w.height,
      rows,
      traceability: w.generatedSql ? { prompt: '', sql: w.generatedSql } : undefined,
      backgroundColor: w.backgroundColor,
      html,
      options
    };

    if (!this.gridReady) {
      this.widgetCollection.push(widget);
      return;
    }
    this.mountWidget(widget);
  }

  // ---- Add Widget dialog ----
  openAddWidget(): void {
    this.addWidgetOpen.set(true);
  }

  onWidgetDialogClosed(): void {
    this.addWidgetOpen.set(false);
  }

  onWidgetCreated(event: { widget: CreatedWidget; rows: Record<string, unknown>[] }): void {
    this.addWidgetOpen.set(false);
    const { widget, rows } = event;
    const { html, options } = this.parseConfig(widget.configJson);

    this.mountWidget({
      id: String(widget.id),
      title: widget.title,
      type: this.mapWidgetType(widget.widgetType),
      x: widget.positionX,
      y: widget.positionY,
      w: widget.width,
      h: widget.height,
      rows,
      html,
      options
    });

    Swal.fire({
      icon: 'success', title: 'Widget added', toast: true,
      position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px'
    });
  }

  private mountWidget(widget: WidgetLayoutStructure): void {
    this.widgetCollection.push(widget);

    const gridItem = this.grid.addWidget({
      x: widget.x,
      y: widget.y,
      w: widget.w,
      h: widget.h,
      id: widget.id,
      content: ''
    });
    this.widgetElements.set(widget.id, gridItem);

    const view = this.vcRef.createEmbeddedView(this.widgetTemplate, { $implicit: widget });

    // NOTE: ViewContainerRef.createEmbeddedView() already attaches this view
    // (unlike TemplateRef.createEmbeddedView()). We only track it here so we
    // can force a manual detectChanges() after async updates (rename, etc.)
    // as a safety net, and so we can destroy() it cleanly on delete.
    this.widgetViews.set(widget.id, view);

    view.detectChanges();

    const element = view.rootNodes.find((n) => n.nodeType === Node.ELEMENT_NODE) as HTMLElement;
    const container = gridItem.querySelector('.grid-stack-item-content');
    if (container && element) {
      container.innerHTML = '';
      container.appendChild(element);
    }

    setTimeout(() => this.renderChart(widget));
  }

  kpiValue(widget: WidgetLayoutStructure): string {
    const row = widget.rows[0];
    if (!row) return '—';
    const values = Object.values(row);
    // Backend SELECT rule puts label/name columns FIRST, the aggregate value LAST.
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
  customHeaderBg(widget: WidgetLayoutStructure): string | null {
    if (this.currentPreset() !== 'custom' || !widget.backgroundColor) return null;
    return this.shade(widget.backgroundColor, -12);
  }

  private shade(hex: string, percent: number): string {
    const c = hex.replace('#', '');
    const num = parseInt(c, 16);
    const amt = Math.round(2.55 * percent);
    const r = Math.min(255, Math.max(0, (num >> 16) + amt));
    const g = Math.min(255, Math.max(0, ((num >> 8) & 0x00ff) + amt));
    const b = Math.min(255, Math.max(0, (num & 0x0000ff) + amt));
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

  syncLayout(items: any[]): void {
    items.forEach((item) => {
      const widget = this.widgetCollection.find((x) => x.id === item.id);
      if (!widget) return;
      widget.x = item.x;
      widget.y = item.y;
      widget.w = item.w;
      widget.h = item.h;

      // Queue this widget's new layout for auto-save (debounced).
      this.layoutChange$.next(widget);
    });
  }

  // Wires the Enter key inside a SweetAlert2 text-input modal directly to
  // its confirm button. We don't rely on SweetAlert2's own document-level
  // Enter handler, since something in this app's event flow (GridStack's
  // touch/mouse simulation lives on document too) can end up swallowing
  // the keydown before SweetAlert2 ever sees it. Binding directly to the
  // input element inside didOpen guarantees Enter always submits, and
  // Shift+Enter/other modifier combos are left alone.
  private bindEnterToConfirm(): void {
    const input = Swal.getInput();
    if (!input) return;
    input.addEventListener('keydown', (e: KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        e.stopPropagation();
        if (!Swal.isVisible()) return;
        const confirmBtn = Swal.getConfirmButton();
        if (confirmBtn && !confirmBtn.disabled) {
          Swal.clickConfirm();
        }
      }
    });
  }

  // Design-only: rename the dashboard itself via the pencil icon next to the title.
  Rename_Dashboard(): void {
    if (Swal.isVisible()) return;
    Swal.fire({
      title: 'Rename dashboard',
      input: 'text',
      inputValue: this.dashboardTitle(),
      inputPlaceholder: 'Dashboard name',
      showCancelButton: true,
      confirmButtonText: 'Save',
      width: '340px',
      customClass: { popup: 'swal-compact' },
      inputValidator: v => !v.trim() ? 'Name cannot be empty' : null,
      didOpen: () => this.bindEnterToConfirm()
    }).then(result => {
      if (!result.isConfirmed || !result.value.trim()) return;
      const newName = result.value.trim();
      this.dashboardService.rename(this.dashboardId, newName).subscribe({
        next: () => {
          this.dashboardTitle.set(newName);
          Swal.fire({
            icon: 'success', title: 'Dashboard renamed', toast: true,
            position: 'top-end', showConfirmButton: false, timer: 2000,
            timerProgressBar: true, width: '320px'
          });
        },
        error: () => Swal.fire({
          icon: 'error', title: 'Rename failed', toast: true,
          position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px'
        })
      });
    });
  }

  // Design-only: rename a single widget via the pencil icon on its header.
  Rename_Widget(widget: WidgetLayoutStructure): void {
    if (Swal.isVisible()) return;
    Swal.fire({
      title: 'Rename widget',
      input: 'text',
      inputValue: widget.title,
      inputPlaceholder: 'Widget name',
      showCancelButton: true,
      confirmButtonText: 'Save',
      width: '340px',
      customClass: { popup: 'swal-compact' },
      inputValidator: v => !v.trim() ? 'Name cannot be empty' : null,
      didOpen: () => this.bindEnterToConfirm()
    }).then(result => {
      if (!result.isConfirmed || !result.value.trim()) return;
      const newTitle = result.value.trim();
      this.widgetService.rename(Number(widget.id), newTitle).subscribe({
        next: () => {
          widget.title = newTitle;
          this.widgetViews.get(widget.id)?.detectChanges();
        },
        error: () => Swal.fire({
          icon: 'error', title: 'Rename failed', toast: true,
          position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px'
        })
      });
    });
  }

  // Design-only: remove a widget from the dashboard entirely.
  Delete_Widget(widget: WidgetLayoutStructure): void {
    if (Swal.isVisible()) return;
    Swal.fire({
      title: 'Delete widget?',
      text: `"${widget.title}"`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Delete',
      confirmButtonColor: '#ef4444',
      width: '300px',
      customClass: { popup: 'swal-compact' }
    }).then(result => {
      if (!result.isConfirmed) return;
      this.widgetService.delete(Number(widget.id)).subscribe({
        next: () => {
          const el = this.widgetElements.get(widget.id);
          if (el) {
            this.grid.removeWidget(el);
            this.widgetElements.delete(widget.id);
          }
          const view = this.widgetViews.get(widget.id);
          if (view) {
            view.destroy();
            this.widgetViews.delete(widget.id);
          }
          this.widgetCollection = this.widgetCollection.filter(w => w.id !== widget.id);
          Swal.fire({
            icon: 'success', title: 'Widget deleted', toast: true,
            position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px'
          });
        },
        error: () => Swal.fire({
          icon: 'error', title: 'Delete failed', toast: true,
          position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px'
        })
      });
    });
  }

  private static readonly PALETTES: Record<string, string[]> = {
    Default: ['#f2495c', '#ff9830', '#5794f2', '#73bf69', '#b877d9'],
    Vivid: ['#ef4444', '#f97316', '#3b82f6', '#22c55e', '#a855f7'],
    Pastel: ['#fca5a5', '#fdba74', '#93c5fd', '#86efac', '#d8b4fe'],
    Monochrome: ['#1e293b', '#334155', '#475569', '#64748b', '#94a3b8']
  };

  private renderChart(widget: WidgetLayoutStructure): void {
    if (widget.type === 'gauge') { this.renderGauge(widget); return; }

    const chartTypes = ['line', 'pie', 'bar', 'area', 'timeseries'];
    if (!chartTypes.includes(widget.type)) return;
    if (widget.rows.length === 0) return;

    const el = document.getElementById(`${widget.type}-${widget.id}`);
    if (!el) return;

    const opts = widget.options ?? {};
    const colors = Design.PALETTES[opts['colorTheme'] as string] ?? Design.PALETTES['Default'];
    const showLegend = opts['legend'] !== false;
    const showLabels = !!opts['dataLabels'];
    const showGrid = opts['gridLines'] !== false;
    const stacked = !!opts['stacked'];
    const legendPos = ((opts['legendPosition'] as string) ?? 'Bottom').toLowerCase();

    const cols = Object.keys(widget.rows[0]);
    const labelCol = cols[0];
    const valueCol = cols.length > 1 ? cols[cols.length - 1] : cols[0];
    const labels = widget.rows.map((r) => String(r[labelCol] ?? ''));
    const values = widget.rows.map((r) => Number(r[valueCol] ?? 0));

    const chart = echarts.init(el);
    const axisLabel = { rotate: labels.length > 8 ? 30 : 0, overflow: 'truncate', width: 80 };
    const isVerticalLegend = legendPos === 'left' || legendPos === 'right';
    const legend = showLegend
      ? {
          show: true,
          orient: isVerticalLegend ? 'vertical' : 'horizontal',
          left: legendPos === 'left' ? 'left' : undefined,
          right: legendPos === 'right' ? 'right' : undefined,
          top: legendPos === 'top' ? 'top' : undefined,
          bottom: legendPos === 'bottom' ? 'bottom' : undefined
        }
      : { show: false };

    const optionMap: Record<string, object> = {
      line: {
        tooltip: { trigger: 'axis' },
        legend,
        grid: { left: 20, right: 20, top: showLegend ? 60 : 30, bottom: 50, show: showGrid, borderWidth: 0 },
        xAxis: { type: 'category', data: labels, axisLabel, splitLine: { show: showGrid } },
        yAxis: { type: 'value', splitLine: { show: showGrid } },
        series: [{ type: 'line', smooth: true, data: values, color: colors[0], label: { show: showLabels, position: 'top' } }]
      },
      timeseries: {
        tooltip: { trigger: 'axis' },
        legend,
        grid: { left: 20, right: 20, top: showLegend ? 60 : 30, bottom: 50, show: showGrid, borderWidth: 0 },
        xAxis: { type: 'category', data: labels, axisLabel, splitLine: { show: showGrid } },
        yAxis: { type: 'value', splitLine: { show: showGrid } },
        series: [{ type: 'line', smooth: true, data: values, color: colors[0], areaStyle: { opacity: 0.15 }, label: { show: showLabels, position: 'top' } }]
      },
      area: {
        tooltip: { trigger: 'axis' },
        legend,
        grid: { left: 20, right: 20, top: showLegend ? 60 : 30, bottom: 50, show: showGrid, borderWidth: 0 },
        xAxis: { type: 'category', data: labels, axisLabel, splitLine: { show: showGrid } },
        yAxis: { type: 'value', splitLine: { show: showGrid } },
        series: [{
          type: 'line', smooth: true, data: values, color: colors[0],
          areaStyle: { opacity: stacked ? 0.6 : 0.3 }, stack: stacked ? 'total' : undefined,
          label: { show: showLabels, position: 'top' }
        }]
      },
      bar: {
        tooltip: { trigger: 'axis' },
        legend,
        grid: { left: 50, right: 20, top: showLegend ? 60 : 30, bottom: 50, show: showGrid, borderWidth: 0 },
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
        legend: showLegend ? { ...legend, type: 'scroll' } : { show: false },
        color: colors,
        series: [{
          type: 'pie',
          radius: ['40%', '70%'],
          label: { show: showLabels, formatter: '{b}: {d}%' },
          data: widget.rows.map((r) => ({ name: String(r[labelCol] ?? ''), value: Number(r[valueCol] ?? 0) }))
        }]
      }
    };

    chart.setOption(optionMap[widget.type] as echarts.EChartsOption);
    new ResizeObserver(() => chart.resize()).observe(el);
  }

  private renderGauge(widget: WidgetLayoutStructure): void {
    const el = document.getElementById(`gauge-${widget.id}`);
    if (!el) return;

    const opts = widget.options ?? {};
    const min = Number(opts['minValue'] ?? 0);
    const max = Number(opts['maxValue'] ?? 100);
    const colors = Design.PALETTES[opts['colorTheme'] as string] ?? Design.PALETTES['Default'];

    const row = widget.rows[0];
    const value = row ? Number(Object.values(row)[Object.values(row).length - 1] ?? 0) : 0;

    const chart = echarts.init(el);
    chart.setOption({
      series: [{
        type: 'gauge',
        min, max,
        progress: { show: true, width: 14, itemStyle: { color: colors[0] } },
        axisLine: { lineStyle: { width: 14 } },
        pointer: { show: false },
        axisTick: { show: false },
        splitLine: { length: 8 },
        axisLabel: { fontSize: 10, distance: 12 },
        detail: { valueAnimation: true, fontSize: 22, offsetCenter: [0, '30%'], color: colors[0] },
        data: [{ value }]
      }]
    });
    new ResizeObserver(() => chart.resize()).observe(el);
  }
}