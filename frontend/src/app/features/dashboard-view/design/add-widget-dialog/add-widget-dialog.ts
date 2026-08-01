import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SchemaService, SchemaTable } from '../../../../core/services/schema.service';
import { WidgetService, CreateWidgetRequest, CreatedWidget } from '../../../../core/services/widget.service';

export interface ChartTypeOption {
  value: string;
  label: string;
  icon: string;
  description: string;
}

// Every chart type this dialog can produce, and which sections of the form
// apply to it. Table-driven so adding a 10th widget type later is a one-line
// change instead of touching every *ngIf in the template.
const CHART_TYPES: ChartTypeOption[] = [
  { value: 'KpiCard', label: 'KPI Card', icon: 'fa-solid fa-gauge-high', description: 'A single headline number' },
  { value: 'BarChart', label: 'Bar', icon: 'fa-solid fa-chart-column', description: 'Compare values across categories' },
  { value: 'LineChart', label: 'Line', icon: 'fa-solid fa-chart-line', description: 'Trend across a dimension' },
  { value: 'AreaChart', label: 'Area', icon: 'fa-solid fa-chart-area', description: 'Trend with filled magnitude' },
  { value: 'PieChart', label: 'Pie', icon: 'fa-solid fa-chart-pie', description: 'Share of a whole' },
  { value: 'Table', label: 'Table', icon: 'fa-solid fa-table', description: 'Raw rows in a grid' },
  { value: 'Gauge', label: 'Gauge', icon: 'fa-solid fa-gauge', description: 'A value against a range' },
  { value: 'TimeSeries', label: 'Time Series', icon: 'fa-solid fa-clock-rotate-left', description: 'Metric over time' },
  { value: 'HtmlWidget', label: 'HTML', icon: 'fa-solid fa-code', description: 'Custom markup, no data source' }
];

const AGGREGATIONS = ['COUNT', 'SUM', 'AVG', 'MIN', 'MAX'];
const INTERVALS = ['Day', 'Week', 'Month'];
const LEGEND_POSITIONS = ['Bottom', 'Top', 'Left', 'Right'];
const COLOR_THEMES = ['Default', 'Vivid', 'Pastel', 'Monochrome'];

@Component({
  selector: 'app-add-widget-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './add-widget-dialog.html',
  styleUrls: ['./add-widget-dialog.css']
})
export class AddWidgetDialog implements OnChanges {
  @Input() open = false;
  @Input() dashboardId!: number;
  @Output() closed = new EventEmitter<void>();
  @Output() created = new EventEmitter<{ widget: CreatedWidget; rows: Record<string, unknown>[] }>();

  readonly chartTypes = CHART_TYPES;
  readonly aggregations = AGGREGATIONS;
  readonly intervals = INTERVALS;
  readonly legendPositions = LEGEND_POSITIONS;
  readonly colorThemes = COLOR_THEMES;

  tables = signal<SchemaTable[]>([]);
  saving = signal(false);
  error = signal<string | null>(null);

  // ---- form state ----
  widgetName = '';
  chartType = 'BarChart';
  table = '';
  groupByColumn = '';
  metricColumn = '';
  aggregation = 'COUNT';
  timeColumn = '';
  interval = 'Day';
  htmlContent = '<div style="padding:12px">Custom HTML widget</div>';

  // Display options - not every field is shown for every chart type, see
  // the *chartFamily* getters below.
  gridLines = true;
  legend = true;
  dataLabels = false;
  stacked = false;
  legendPosition = 'Bottom';
  colorTheme = 'Default';
  minValue = 0;
  maxValue = 100;
  showRowNumbers = false;
  stripedRows = true;
  pageSize = 10;
  valuePrefix = '';
  valueSuffix = '';

  constructor(private schemaService: SchemaService, private widgetService: WidgetService) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open'] && this.open) {
      this.resetForm();
      this.loadTables();
    }
  }

  private loadTables(): void {
    this.schemaService.getTables().subscribe({
      next: (tables) => {
        this.tables.set(tables);
        if (!this.table && tables.length) this.table = tables[0].table;
      },
      error: () => this.error.set('Could not load data sources.')
    });
  }

  private resetForm(): void {
    this.widgetName = '';
    this.chartType = 'BarChart';
    this.groupByColumn = '';
    this.metricColumn = '';
    this.aggregation = 'COUNT';
    this.timeColumn = '';
    this.interval = 'Day';
    this.error.set(null);
    this.saving.set(false);
    this.gridLines = true;
    this.legend = true;
    this.dataLabels = false;
    this.stacked = false;
    this.legendPosition = 'Bottom';
    this.colorTheme = 'Default';
    this.minValue = 0;
    this.maxValue = 100;
    this.showRowNumbers = false;
    this.stripedRows = true;
    this.pageSize = 10;
    this.valuePrefix = '';
    this.valueSuffix = '';
  }

  // ---- conditions per chart type ----
  get isHtml(): boolean { return this.chartType === 'HtmlWidget'; }
  get isTable(): boolean { return this.chartType === 'Table'; }
  get isTimeSeries(): boolean { return this.chartType === 'TimeSeries'; }
  get isKpiOrGauge(): boolean { return this.chartType === 'KpiCard' || this.chartType === 'Gauge'; }
  get isGauge(): boolean { return this.chartType === 'Gauge'; }
  get needsDataSource(): boolean { return !this.isHtml; }
  get needsGroupBy(): boolean { return ['BarChart', 'LineChart', 'AreaChart', 'PieChart'].includes(this.chartType); }
  get needsMetric(): boolean { return !this.isHtml && !this.isTable; }

  // Chart-family display options (grid/legend/stacked) only make sense for
  // things that actually render as an axis-based or sliced chart.
  get showChartFamilyOptions(): boolean {
    return ['BarChart', 'LineChart', 'AreaChart', 'TimeSeries'].includes(this.chartType);
  }
  get showPieOptions(): boolean { return this.chartType === 'PieChart'; }
  get showStackedOption(): boolean {
    return ['BarChart', 'AreaChart'].includes(this.chartType);
  }
  get showKpiOptions(): boolean { return this.chartType === 'KpiCard'; }
  get showGaugeOptions(): boolean { return this.chartType === 'Gauge'; }
  get showTableOptions(): boolean { return this.isTable; }

  get selectedTableColumns() {
    return this.tables().find((t) => t.table === this.table)?.columns ?? [];
  }
  get numericColumns() {
    return this.selectedTableColumns.filter((c) => c.numeric);
  }

  get canSave(): boolean {
    if (!this.widgetName.trim()) return false;
    if (this.isHtml) return true;
    if (!this.table) return false;
    if (this.needsGroupBy && !this.groupByColumn) return false;
    if (this.isTimeSeries && !this.timeColumn) return false;
    if (this.needsMetric && this.aggregation !== 'COUNT' && !this.metricColumn) return false;
    return true;
  }

  selectType(value: string): void {
    this.chartType = value;
  }

  close(): void {
    this.closed.emit();
  }

  save(): void {
    if (!this.canSave || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);

    const displayOptions = this.buildDisplayOptions();

    const request: CreateWidgetRequest = {
      dashboardId: this.dashboardId,
      title: this.widgetName.trim(),
      widgetType: this.chartType,
      table: this.isHtml ? null : this.table,
      groupByColumn: this.needsGroupBy ? this.groupByColumn : null,
      metricColumn: this.needsMetric ? this.metricColumn : null,
      aggregation: this.needsMetric ? this.aggregation : null,
      timeColumn: this.isTimeSeries ? this.timeColumn : null,
      interval: this.isTimeSeries ? this.interval : null,
      htmlContent: this.isHtml ? this.htmlContent : null,
      displayOptions
    };

    this.widgetService.create(request).subscribe({
      next: (widget) => {
        if (this.isHtml) {
          this.saving.set(false);
          this.created.emit({ widget, rows: [] });
          return;
        }
        this.widgetService.getData(widget.id).subscribe({
          next: (rows) => {
            this.saving.set(false);
            this.created.emit({ widget, rows });
          },
          error: () => {
            this.saving.set(false);
            this.created.emit({ widget, rows: [] });
          }
        });
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(err?.error?.message ?? 'Failed to create widget.');
      }
    });
  }

  private buildDisplayOptions(): Record<string, unknown> {
    if (this.isHtml) return {};
    if (this.showKpiOptions) {
      return { colorTheme: this.colorTheme, prefix: this.valuePrefix, suffix: this.valueSuffix };
    }
    if (this.showGaugeOptions) {
      return { colorTheme: this.colorTheme, minValue: this.minValue, maxValue: this.maxValue };
    }
    if (this.showTableOptions) {
      return { showRowNumbers: this.showRowNumbers, stripedRows: this.stripedRows, pageSize: this.pageSize };
    }
    if (this.showPieOptions) {
      return {
        legend: this.legend,
        dataLabels: this.dataLabels,
        legendPosition: this.legendPosition,
        colorTheme: this.colorTheme
      };
    }
    if (this.showChartFamilyOptions) {
      return {
        gridLines: this.gridLines,
        legend: this.legend,
        dataLabels: this.dataLabels,
        stacked: this.showStackedOption ? this.stacked : false,
        legendPosition: this.legendPosition,
        colorTheme: this.colorTheme
      };
    }
    return {};
  }
}