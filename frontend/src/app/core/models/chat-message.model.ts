import { IntentSummary } from './prompt-response.model';

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  options?: string[];
  sql?: string;
  data?: Record<string, unknown>[];
  isFinal?: boolean;
  intentSummary?: IntentSummary | null;
  suggestions?: string[] | null;
  dashboardId?: number | null;
}
