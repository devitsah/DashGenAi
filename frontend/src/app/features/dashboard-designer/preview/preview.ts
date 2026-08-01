import {
  Component, OnInit, OnDestroy, AfterViewInit,
  ElementRef, NgZone, ChangeDetectorRef
} from '@angular/core';
import { CommonModule } from '@angular/common';
import * as echarts from 'echarts';
import { GridStack, GridStackNode } from 'gridstack';
import { BuilderStateService } from '../builder-state.service';
import { ProcessPromptResult } from '../../../core/models/prompt-response.model';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { forkJoin, of, Subject } from 'rxjs';
import { catchError, debounceTime } from 'rxjs/operators';
import { WidgetService } from '../../../core/services/widget.service';

interface PreviewWidget {
  id: string;
  title: string;
  widgetType: string;
  widgetTypeLabel: string;
  rows: Record<string, unknown>[];
  columns: string[];
  result: ProcessPromptResult;
}

@Component({
  selector: 'app-preview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './preview.html',
  styleUrls: ['./preview.css']
})
export class Preview implements AfterViewInit, OnDestroy {
  private grid!: GridStack;
  private charts = new Map<string, echarts.ECharts>();

  // Widget currently being renamed in the preview grid (null = none in edit mode)
  editingWidgetId: string | null = null;

  // Widget whose color panel is currently open (Custom style only, null = none open)
  openColorPickerId: string | null = null;

  // NOTE: per-widget background colors are NOT stored locally on this component
  // anymore. They live on BuilderStateService (getWidgetColor/setWidgetColor/
  // clearWidgetColor) so a Back/Continue between wizard steps (which destroys
  // and recreates this component via *ngIf) doesn't wipe out colors the user
  // already picked. Only pushed to the server on Commit (see saveLayout).

  // Live layout (x/y/w/h) per widget — actually stored on BuilderStateService
  // (see getWidgetLayout/setWidgetLayout), because *ngIf destroys/recreates
  // this whole component when the user clicks Back/Continue between wizard
  // steps, which would otherwise reset every widget to its default size on
  // remount. Seeded from the widget's saved position (if any) the first
  // time it's rendered, then kept up to date by the grid 'change' listener.
layoutFor(w: PreviewWidget, index: number): { x: number; y: number; w: number; h: number } {
    const r: any = w.result;
    // If this widget has never been dragged/saved, don't default everyone to
    // the same (0,0) — that causes overlap the moment any ONE of them is
    // dragged, since GridStack doesn't auto-reflow items sitting at a stale
    // explicit position. Instead, stagger defaults: 2 widgets per row (w:6
    // each, 12-column grid), stacking downward.
    const fallbackX = (index % 2) * 6;
    const fallbackY = Math.floor(index / 2) * 4;
    return this.state.getWidgetLayout(w.id, {
      x: r.positionX ?? fallbackX,
      y: r.positionY ?? fallbackY,
      w: r.width ?? 6,
      h: r.height ?? 4
    });
  }
  // Auto-save: fires 600ms after the user stops dragging/resizing a widget,
  // so we don't send a PUT on every intermediate frame — only once, after
  // the layout settles. Keyed by GridStack node id so rapid changes to the
  // same widget collapse into a single save.
  private layoutChange$ = new Subject<GridStackNode>();
  autoSaveStatus: 'idle' | 'saving' | 'saved' | 'error' = 'idle';

  constructor(
    public state: BuilderStateService,
    private el: ElementRef,
    private zone: NgZone,
    private cdr: ChangeDetectorRef,
    private http: HttpClient,
    private widgetService: WidgetService
  ) {
    this.layoutChange$.pipe(debounceTime(600)).subscribe((node) => {
      const rawId = (node as any).el?.getAttribute('data-widget-id');
      if (!rawId) return;
      const id = Number(rawId.replace('w-', ''));
      if (!id) return;

      this.zone.run(() => (this.autoSaveStatus = 'saving'));
      this.http.put(
        `${environment.apiUrl}/widgets/${id}/position`,
        { positionX: node.x ?? 0, positionY: node.y ?? 0, width: node.w ?? 6, height: node.h ?? 4 }
      ).subscribe({
        next: () => this.zone.run(() => (this.autoSaveStatus = 'saved')),
        error: (err) => {
          console.error(`Failed to auto-save widget ${id} layout`, err);
          this.zone.run(() => (this.autoSaveStatus = 'error'));
        }
      });
    });
  }

  // --- Custom color (staging preview) ---

  get isCustomStyle(): boolean {
    return this.state.style() === 'custom';
  }

  toggleColorPicker(w: PreviewWidget): void {
    this.openColorPickerId = this.openColorPickerId === w.id ? null : w.id;
  }

  closeColorPicker(): void {
    this.openColorPickerId = null;
  }

  widgetColor(w: PreviewWidget): string {
    return this.state.getWidgetColor(w.id) ?? '#ffffff';
  }

  hasCustomColor(w: PreviewWidget): boolean {
    return this.state.getWidgetColor(w.id) !== undefined;
  }

  onColorPicked(w: PreviewWidget, value: string): void {
    this.state.setWidgetColor(w.id, value);
  }

  resetColor(w: PreviewWidget): void {
    this.state.clearWidgetColor(w.id);
  }

  // Custom style only: whichever of dark/light text stays readable against
  // the chosen background. Bound directly onto the title/value/label/table
  // text elements in the template (inline style beats the class-based
  // colors in preview.css), so text stays visible no matter what color
  // the user picks. Returns null when no custom color is set, so the
  // normal CSS (indigo KPI accent etc.) applies unchanged.
  widgetTextColor(w: PreviewWidget): string | null {
    if (!this.isCustomStyle) return null;
    const color = this.state.getWidgetColor(w.id);
    if (!color) return null;
    return this.contrastText(color);
  }

  private contrastText(hex: string): string {
    const c = hex.replace('#', '');
    if (c.length !== 6) return '#111827';
    const r = parseInt(c.substring(0, 2), 16);
    const g = parseInt(c.substring(2, 4), 16);
    const b = parseInt(c.substring(4, 6), 16);
    const yiq = (r * 299 + g * 587 + b * 114) / 1000;
    return yiq >= 150 ? '#111827' : '#ffffff';
  }

  // Applied via [ngStyle] on .widget-card. Sets the chosen background PLUS a
  // --widget-border CSS variable that .data-table th/td (and the header's
  // border) read via var(--widget-border, <default>) — see preview.css.
  // This keeps the row/column lines visible no matter what color is picked,
  // instead of always using the same fixed light-grey line that only reads
  // well on a white background.
  widgetCardStyle(w: PreviewWidget): Record<string, string> {
    const color = this.state.getWidgetColor(w.id);
    if (!color) return {};
    return {
      background: color,
      '--widget-border': this.contrastBorder(color),
      '--widget-header-bg': this.shade(color, -12)
    };
  }

  // Picks a border color that stays visible against ANY chosen background:
  // light backgrounds get a dark, semi-transparent line; dark backgrounds get
  // a light, semi-transparent line — same idea as the grafana theme's
  // rgba(255,255,255,0.15) row dividers on its orange table.
  private contrastBorder(hex: string): string {
    const c = hex.replace('#', '');
    const r = parseInt(c.substring(0, 2), 16);
    const g = parseInt(c.substring(2, 4), 16);
    const b = parseInt(c.substring(4, 6), 16);
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
    return luminance > 0.55 ? 'rgba(0,0,0,0.18)' : 'rgba(255,255,255,0.28)';
  }

  // Darkens/lightens the chosen color a bit, used for the table header row
  // (--widget-header-bg) so the header reads as a distinct band instead of
  // blending flat into the body rows — negative percent = darker.
  private shade(hex: string, percent: number): string {
    const c = hex.replace('#', '');
    const num = parseInt(c, 16);
    const amt = Math.round(2.55 * percent);
    const r = Math.min(255, Math.max(0, (num >> 16) + amt));
    const g = Math.min(255, Math.max(0, ((num >> 8) & 0x00FF) + amt));
    const b = Math.min(255, Math.max(0, (num & 0x0000FF) + amt));
    return `rgb(${r}, ${g}, ${b})`;
  }

  get widgets(): PreviewWidget[] {
    return this.state.acceptedWidgets().map((r, i) => this.toWidget(r, i));
  }

  get hasWidgets(): boolean { return this.state.acceptedWidgets().length > 0; }

  // IMPORTANT: without this, *ngFor re-creates the DOM node for every widget on
  // every change-detection cycle (since `widgets` getter returns brand-new object
  // literals each time), which desyncs GridStack's internally-tracked nodes from
  // Angular's DOM and produces duplicate-looking widget cards. trackBy keeps the
  // same DOM node for the same widget id, so GridStack's positioning stays intact.
  trackByWidgetId(_index: number, w: PreviewWidget): string {
    return w.id;
  }

ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      this.grid = GridStack.init({
        column: 12,
        margin: 6,
        cellHeight: 90,
        float: true,
        minRow: 1,
        animate: true,
        resizable: { handles: 'se' }
      }, this.el.nativeElement.querySelector('.grid-stack'));
 
      this.grid.on('resizestop', (_e: Event, el: GridStackNode) => {
        const id = (el as any).el?.getAttribute('data-widget-id');
        if (id && this.charts.has(id)) {
          this.charts.get(id)!.resize();
        }
      });
 
      // Fires after drag or resize settles — update shared layout state and
      // queue for debounced auto-save.
      this.grid.on('change', (_e: Event, items: GridStackNode[]) => {
        items.forEach(item => {
          const rawId = (item as any).el?.getAttribute('data-widget-id');
          if (rawId) {
            this.state.setWidgetLayout(rawId, {
              x: item.x ?? 0, y: item.y ?? 0, w: item.w ?? 6, h: item.h ?? 4
            });
          }
          this.layoutChange$.next(item);
        });
      });
    });
 
    setTimeout(() => this.renderAllCharts(), 100);
  }
  // --- Rename (staging preview) ---

  startEdit(w: PreviewWidget): void {
    this.editingWidgetId = w.id;
  }

  saveEdit(w: PreviewWidget, newTitle: string): void {
    this.editingWidgetId = null;

    const trimmed = newTitle.trim();
    if (!trimmed || trimmed === w.title) return; // no-op: empty or unchanged

    if (trimmed.length > 100) {
      this.state.error.set('Title must be 100 characters or fewer.');
      return;
    }

    const widgetId = w.result.widgetId;
    if (widgetId != null) {
      this.state.renameWidget(widgetId, trimmed);
    }
  }

  cancelEdit(): void {
    this.editingWidgetId = null;
  }

  // --- Delete (staging preview) ---

  deleteWidget(w: PreviewWidget): void {
    const confirmed = window.confirm(`Delete "${w.title}"? This can't be undone.`);
    if (!confirmed) return;

    this.state.clearWidgetColor(w.id);

    // Dispose its chart instance (if any) so we don't leak echarts instances
    // pointing at a DOM node that's about to be removed.
    const chart = this.charts.get(w.id);
    if (chart) {
      chart.dispose();
      this.charts.delete(w.id);
    }
    if (this.openColorPickerId === w.id) this.openColorPickerId = null;

    this.state.removeAcceptedWidget(w.result);
  }

  // --- Layout persistence ---

  saveLayout(): Promise<void> {
    const nodes = this.grid?.getGridItems() ?? [];
    const positionCalls = nodes.map(el => {
      const widgetId = el.getAttribute('data-widget-id');
      if (!widgetId) return of(null);
      const node = (el as any).gridstackNode;
      const id = Number(widgetId.replace('w-', ''));
      return this.http.put(
        `${environment.apiUrl}/widgets/${id}/position`,
        { positionX: node?.x ?? 0, positionY: node?.y ?? 0, width: node?.w ?? 6, height: node?.h ?? 4 }
      ).pipe(catchError(() => of(null)));
    });

    // Custom style only: whatever colors the user picked in the staging preview
    // color panels get saved to the widget's ConfigJson right when they hit Commit.
    const colorCalls = this.isCustomStyle
      ? this.state.widgetColorEntries.map(([widgetId, color]) => {
          const id = Number(widgetId.replace('w-', ''));
          return this.widgetService.updateColor(id, color).pipe(catchError(() => of(null)));
        })
      : [];

    const calls = [...positionCalls, ...colorCalls];
    return calls.length ? forkJoin(calls).toPromise().then(() => {}) : Promise.resolve();
  }

  ngOnDestroy(): void {
    this.charts.forEach(c => c.dispose());
    this.grid?.destroy(false);
  }

  private renderAllCharts(): void {
    this.widgets.forEach(w => {
      if (this.isChart(w.widgetType)) {
        const el = this.el.nativeElement.querySelector(`[data-chart-id="${w.id}"]`);
        if (el && !this.charts.has(w.id)) {
          this.zone.runOutsideAngular(() => this.renderChart(el, w));
        }
      }
    });
  }

  private renderChart(el: HTMLDivElement, w: PreviewWidget): void {
    const cols = w.columns;
    const labelCol = cols[0];
    const valueCol = cols.length > 1 ? cols[cols.length - 1] : cols[0];
    const labels = w.rows.map(r => String(r[labelCol] ?? ''));
    const values = w.rows.map(r => Number(r[valueCol] ?? 0));
    const chart = echarts.init(el);
    this.charts.set(w.id, chart);

    let option: object;
    if (w.widgetType === 'pie') {
      option = {
        tooltip: { trigger: 'item' },
        legend: { orient: 'horizontal', bottom: 0, type: 'scroll', textStyle: { fontSize: 10 } },
        series: [{ type: 'pie', radius: ['35%', '65%'],
          data: w.rows.map(r => ({ name: String(r[labelCol] ?? ''), value: Number(r[valueCol] ?? 0) })) }]
      };
    } else if (w.widgetType === 'line') {
      option = {
        tooltip: { trigger: 'axis' },
        grid: { left: 45, right: 15, top: 15, bottom: 40 },
        xAxis: { type: 'category', data: labels, axisLabel: { fontSize: 10, rotate: labels.length > 6 ? 30 : 0 } },
        yAxis: { type: 'value', axisLabel: { fontSize: 10 } },
        series: [{ type: 'line', smooth: true, data: values, color: '#00b96b', areaStyle: { opacity: 0.1 } }]
      };
    } else {
      option = {
        tooltip: { trigger: 'axis' },
        grid: { left: 45, right: 15, top: 15, bottom: 40 },
        xAxis: { type: 'category', data: labels, axisLabel: { fontSize: 10, rotate: labels.length > 6 ? 30 : 0 } },
        yAxis: { type: 'value', axisLabel: { fontSize: 10 } },
        series: [{
          type: 'bar',
          data: values,
          barMaxWidth: 40,
          itemStyle: {
            color: (params: { dataIndex: number }) => {
              const palette = ['#f2495c', '#ff9830', '#5794f2', '#73bf69'];
              return palette[params.dataIndex % palette.length];
            }
          }
        }]
      };
    }
    chart.setOption(option as any);
    new ResizeObserver(() => this.zone.runOutsideAngular(() => chart.resize())).observe(el);
  }

  isKpi(type: string): boolean { return type === 'kpi'; }
  isChart(type: string): boolean { return type === 'bar' || type === 'line' || type === 'pie'; }
  isTable(type: string): boolean { return !this.isKpi(type) && !this.isChart(type); }

  kpiValue(w: PreviewWidget): string {
    const values = w.rows[0] ? Object.values(w.rows[0]) : [];
    const val = values[values.length - 1];
    return val == null ? '—' : String(val);
  }

  kpiLabel(w: PreviewWidget): string {
    const keys = w.rows[0] ? Object.keys(w.rows[0]) : [];
    return keys[keys.length - 1] ?? '';
  }

  private normalizeType(raw: string): string {
    const key = raw.toLowerCase().replace(/[_\s]/g, '');
    const map: Record<string, string> = { kpicard: 'kpi', table: 'table', barchart: 'bar', linechart: 'line', piechart: 'pie' };
    return map[key] ?? 'table';
  }

  private formatTypeLabel(raw: string): string {
    const map: Record<string, string> = { kpi: 'KPI Card', table: 'Table', bar: 'Bar Chart', line: 'Line Chart', pie: 'Pie Chart' };
    return map[this.normalizeType(raw)] ?? raw;
  }

  private toWidget(r: ProcessPromptResult, index: number): PreviewWidget {
    const s = r.intentSummary;
    const typeNorm = this.normalizeType(s?.widgetType ?? 'table');
    const rows = r.widgetData ?? [];
    const columns = rows[0] ? Object.keys(rows[0]) : [];
    return {
      id: `w-${r.widgetId ?? index}`,
      title: r.widgetTitle ?? `Widget ${index + 1}`,
      widgetType: typeNorm,
      widgetTypeLabel: this.formatTypeLabel(s?.widgetType ?? 'table'),
      rows,
      columns,
      result: r
    };
  }
}