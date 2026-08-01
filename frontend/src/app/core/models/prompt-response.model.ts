export interface ProcessPromptResult {
  promptId: number;
  needsClarification: boolean;
  clarificationQuestion: string | null;
  clarificationOptions: string[] | null;
  dashboardId: number | null;
  widgetId: number | null;
  widgetTitle: string | null;
  generatedSql: string | null;
  widgetData: Record<string, unknown>[] | null;
  widgetDataError?: string | null;
  intentSummary: IntentSummary | null;
  suggestions: string[] | null;
  blocked?: boolean;
}

export interface IntentSummary {
  dashboardStyle: string | null;
  widgetType: string | null;
  metric: string | null;
  dimension: string | null;
  filters: string[];
  timePeriod: string | null;
}
