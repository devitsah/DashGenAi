import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { PromptService } from '../../core/services/prompt.service';
import { ProcessPromptResult } from '../../core/models/prompt-response.model';
import { environment } from '../../../environments/environment';
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  options?: string[];
  widgetType?: string;
  previewRows?: Record<string, unknown>[];
  previewColumns?: string[];
  sql?: string;
  chartId?: string;
  result?: ProcessPromptResult;
  accepted?: boolean;
  rejected?: boolean;
}

@Injectable({ providedIn: 'root' })
export class BuilderStateService {
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly awaitingClarification = signal(false);
  readonly widgetCount = signal(0);
  readonly dashboardId = signal<number | null>(null);
  readonly lastResult = signal<ProcessPromptResult | null>(null);
  readonly style = signal<string>('powerbi');
  readonly allWidgets = signal<ProcessPromptResult[]>([]);
  readonly acceptedWidgets = signal<ProcessPromptResult[]>([]);
  readonly selectedStyle = signal<string | null>(null);
  readonly createdDashboardName = signal<string>('');
  readonly chatMessages = signal<ChatMessage[]>([
    {
      role: 'assistant',
      text: 'Hi! Tell me what you want to visualize. '
    }
  ]);

  private lastPromptId: number | null = null;

  // Live per-widget grid position/size, keyed by preview widget id (e.g. "w-42").
  // Lives here (not on the Preview component) because *ngIf destroys and
  // recreates Preview every time the user navigates Back/Continue between
  // wizard steps — a field on the component would be wiped each time.
  // This service is providedIn: 'root', so it survives for the whole
  // session until the page is reloaded.
  private widgetLayout = new Map<string, { x: number; y: number; w: number; h: number }>();

  getWidgetLayout(id: string, fallback: { x: number; y: number; w: number; h: number }) {
    return this.widgetLayout.get(id) ?? fallback;
  }

  setWidgetLayout(id: string, layout: { x: number; y: number; w: number; h: number }): void {
    this.widgetLayout.set(id, layout);
  }

  // Per-widget background colors chosen in the Staging Preview color panel
  // (Custom style only), keyed by preview widget id (e.g. "w-42"). Same reason
  // as widgetLayout above - lives here instead of on Preview so a Back/Continue
  // between wizard steps doesn't reset colors the user already picked.
  private widgetColors = new Map<string, string>();

  getWidgetColor(id: string): string | undefined {
    return this.widgetColors.get(id);
  }

  setWidgetColor(id: string, color: string): void {
    this.widgetColors.set(id, color);
  }

  clearWidgetColor(id: string): void {
    this.widgetColors.delete(id);
  }

  get widgetColorEntries(): [string, string][] {
    return Array.from(this.widgetColors.entries());
  }

  constructor(private promptService: PromptService, private http: HttpClient) {}

  setStyle(s: string): void { this.style.set(s); }
  setDashboardId(id: number): void { this.dashboardId.set(id); }

  acceptWidget(result: ProcessPromptResult): void {
    this.acceptedWidgets.update(list => [...list, result]);
  }

  rejectWidget(result: ProcessPromptResult): void {
    // Remove it from local state immediately so the UI feels responsive...
    this.allWidgets.update(list => list.filter(w => w !== result));
    this.widgetCount.update(n => Math.max(0, n - 1));

    // ...but the widget (and its query) was already persisted to the dashboard the
    // moment the AI generated it, so it has to be deleted server-side too - otherwise
    // it still shows up when the dashboard is opened, even though it was "rejected".
    if (result.widgetId != null) {
      this.http.delete(`${environment.apiUrl}/widgets/${result.widgetId}`).pipe(
        catchError(() => {
          this.error.set('Could not remove the rejected widget. Please try again.');
          return of(null);
        })
      ).subscribe();
    }
  }
 renameWidget(widgetId: number, newTitle: string): void {
  const trimmed = newTitle.trim();
  if (!trimmed) return;

  this.acceptedWidgets.update(list =>
    list.map(w => w.widgetId === widgetId ? { ...w, widgetTitle: trimmed } : w)
  );

  this.http.put(`${environment.apiUrl}/widgets/${widgetId}/title`, { title: trimmed })
    .pipe(catchError(() => {
      this.error.set('Could not rename widget. Please try again.');
      return of(null);
    }))
    .subscribe();
}

  removeAcceptedWidget(result: ProcessPromptResult): void {
    // Remove from local state immediately so the UI feels responsive...
    this.acceptedWidgets.update(list => list.filter(w => w !== result));

    // ...but the widget was already persisted server-side when it was accepted,
    // so it has to be deleted there too, or it reappears when the dashboard reloads.
    if (result.widgetId != null) {
      this.http.delete(`${environment.apiUrl}/widgets/${result.widgetId}`).pipe(
        catchError(() => {
          this.error.set('Could not delete widget. Please try again.');
          return of(null);
        })
      ).subscribe();
    }
  }
  

  sendMessage(rawText: string, selectedWidgetType: string | null = null): void {
    const raw = rawText.trim();
    if (!raw || this.loading()) return;

    const applyTypeHint = !this.awaitingClarification() && selectedWidgetType;
    const prompt = applyTypeHint ? `${raw} as a ${selectedWidgetType}` : raw;

    this.chatMessages.update(msgs => [...msgs, { role: 'user', text: prompt }]);

    const styleHint = this.style() !== 'powerbi'
      ? `Use ${this.style()} dashboard style. `
      : 'Use power_bi dashboard style. ';
    const promptWithStyle = this.awaitingClarification() ? prompt : `${styleHint}${prompt}`;

    const obs$ = this.awaitingClarification() && this.lastPromptId != null
      ? this.http.post<ProcessPromptResult>(
          `${environment.apiUrl}/clarifications/${this.lastPromptId}/answer`,
          { answerText: prompt, dashboardId: this.dashboardId() ?? null }
        )
      : this.promptService.submit(promptWithStyle, this.dashboardId() ?? undefined);

    this.loading.set(true);
    this.error.set(null);

    obs$.subscribe({
      next: (result) => {
        this.loading.set(false);
        this.lastResult.set(result);
        this.lastPromptId = result.promptId;

        if (result.blocked) {
          this.chatMessages.update(msgs => [...msgs, {
            role: 'assistant',
            text: result.clarificationQuestion ?? 'That request cannot be processed.'
          }]);
        } else if (result.needsClarification) {
          this.awaitingClarification.set(true);
          this.chatMessages.update(msgs => [...msgs, {
            role: 'assistant',
            text: result.clarificationQuestion ?? 'Could you clarify that?',
            options: result.clarificationOptions ?? undefined
          }]);
        } else {
          this.awaitingClarification.set(false);
          if (result.dashboardId) this.dashboardId.set(result.dashboardId);
          this.widgetCount.update(n => n + 1);
          this.allWidgets.update(list => [...list, result]);
          this.pushSuccessMessage(result);
        }
      },
      error: (err: unknown) => {
        this.loading.set(false);
        const message = err instanceof HttpErrorResponse
          ? (err.error?.message ?? err.message ?? 'Request failed')
          : err instanceof Error ? err.message : 'Request failed';
        this.error.set(message);
        this.chatMessages.update(msgs => [...msgs, { role: 'assistant', text: message }]);
      }
    });
  }

  acceptMessageWidget(msg: ChatMessage): void {
    if (!msg.result || msg.accepted || msg.rejected) return;
    this.chatMessages.update(msgs => msgs.map(m => m === msg ? { ...m, accepted: true } : m));
    this.acceptWidget(msg.result);
  }

  rejectMessageWidget(msg: ChatMessage): void {
    if (!msg.result || msg.accepted || msg.rejected) return;
    this.chatMessages.update(msgs => msgs.map(m => m === msg ? { ...m, rejected: true } : m));
    this.rejectWidget(msg.result);
  }

  private pushSuccessMessage(result: ProcessPromptResult): void {
    const s = result.intentSummary;
    const typeNorm = this.normalizeType(s?.widgetType ?? 'table');
    const label = this.formatWidgetLabel(typeNorm);
    const metric = s?.metric ? ` for "${s.metric}"` : '';
    const dimension = s?.dimension ? ` by ${s.dimension}` : '';
    const timePeriod = s?.timePeriod ? ` (${s.timePeriod})` : '';
    const filters = s?.filters?.length ? ` | filters: ${s.filters.join(', ')}` : '';
    const rows = result.widgetData ?? [];
    const columns = rows[0] ? Object.keys(rows[0]) : [];
    const previewRows = rows.slice(0, 100);
    const isChart = typeNorm === 'bar' || typeNorm === 'line' || typeNorm === 'pie';
    const chartId = isChart ? `chart-${Date.now()}` : undefined;

    this.chatMessages.update(msgs => [...msgs, {
      role: 'assistant',
      text: `✓ ${label} created${metric}${dimension}${timePeriod}${filters}.`,
      widgetType: typeNorm,
      previewRows: previewRows.length ? previewRows : undefined,
      previewColumns: columns.length ? columns : undefined,
      sql: result.generatedSql ?? undefined,
      chartId,
      result
    }]);

    if (result.suggestions?.length) {
      this.chatMessages.update(msgs => [...msgs, {
        role: 'assistant', text: 'You could also add:', options: result.suggestions!
      }]);
    }
  }

  private normalizeType(raw: string): string {
    const key = raw.toLowerCase().replace(/[_\s]/g, '');
    const map: Record<string, string> = { kpicard: 'kpi', table: 'table', barchart: 'bar', linechart: 'line', piechart: 'pie' };
    return map[key] ?? 'table';
  }

  private formatWidgetLabel(normalized: string): string {
    const map: Record<string, string> = { kpi: 'KPI Card', table: 'Table', bar: 'Bar Chart', line: 'Line Chart', pie: 'Pie Chart' };
    return map[normalized] ?? normalized;
  }
}