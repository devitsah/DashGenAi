import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ProcessPromptResult } from '../models/prompt-response.model';
import { environment } from '../../../environments/environment';
 
@Injectable({ providedIn: 'root' })
export class ClarificationService {
  constructor(private http: HttpClient) {}
 
  answer(parentPromptId: number, answerText: string, dashboardId?: number) {
    return this.http.post<ProcessPromptResult>(`${environment.apiUrl}/prompts`, {
      promptText: answerText,
      dashboardId: dashboardId ?? null,
      parentPromptId
    });
  }
}
 