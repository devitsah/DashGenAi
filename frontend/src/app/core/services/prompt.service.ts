import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ProcessPromptResult } from '../models/prompt-response.model';
import { environment } from '../../../environments/environment';
 
@Injectable({ providedIn: 'root' })
export class PromptService {
  constructor(private http: HttpClient) {}
 
  submit(promptText: string, dashboardId?: number, parentPromptId?: number | null) {
    return this.http.post<ProcessPromptResult>(`${environment.apiUrl}/prompts`, {
      promptText,
      dashboardId: dashboardId ?? null,
      parentPromptId: parentPromptId ?? null
    });
  }
 
  getExamples() {
    return this.http.get<string[]>(`${environment.apiUrl}/schema/examples`);
  }
}
 