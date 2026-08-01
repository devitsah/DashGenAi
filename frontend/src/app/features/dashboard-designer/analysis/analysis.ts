import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { BuilderStateService } from '../builder-state.service';

@Component({
  selector: 'app-analysis',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './analysis.html',
  styleUrls: ['./analysis.css']
})
export class Analysis {
  constructor(public state: BuilderStateService) {}

  get hasResult(): boolean {
    const r = this.state.lastResult();
    return !!r && !r.needsClarification;
  }

  get rows(): { label: string; value: string; filled: boolean }[] {
    const r = this.state.lastResult();
    const s = r?.intentSummary;

    // Dashboard style: prefer what the backend confirmed, fall back to user's
    // selection from Step 1 (style signal), never show raw 'custom' default.
    const styleRaw = s?.dashboardStyle ?? this.state.style();
    const styleLabel = styleRaw === 'power_bi' ? 'Power BI'
      : styleRaw === 'grafana' ? 'Grafana'
      : styleRaw === 'powerbi' ? 'Power BI'
      : styleRaw === 'custom'  ? 'Custom'
      : styleRaw ?? 'Custom';

    const widgetType = s?.widgetType
      ? this.formatWidgetType(s.widgetType)
      : null;

    return [
      { label: 'Widget Type',     value: widgetType     ?? '—', filled: !!widgetType },
      { label: 'Metric',          value: s?.metric      ?? '—', filled: !!s?.metric },
      { label: 'Dimension',       value: s?.dimension   ?? '—', filled: !!s?.dimension },
      { label: 'Time Period',     value: s?.timePeriod  ?? '—', filled: !!s?.timePeriod },
      { label: 'Filters',         value: s?.filters?.length ? s.filters.join(', ') : '—', filled: !!s?.filters?.length },
      { label: 'Dashboard Style', value: styleLabel,             filled: true }
    ];
  }

  private formatWidgetType(raw: string): string {
    const key = raw.toLowerCase().replace(/[_\s]/g, '');
    const map: Record<string, string> = {
      kpicard:   'KPI Card',
      table:     'Table',
      barchart:  'Bar Chart',
      piechart:  'Pie Chart',
      linechart: 'Line Chart'
    };
    return map[key] ?? raw;
  }
}
