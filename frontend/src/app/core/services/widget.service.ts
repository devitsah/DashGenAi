import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface CreateWidgetRequest {
  dashboardId: number;
  title: string;
  widgetType: string;
  table?: string | null;
  groupByColumn?: string | null;
  metricColumn?: string | null;
  aggregation?: string | null;
  timeColumn?: string | null;
  interval?: string | null;
  htmlContent?: string | null;
  displayOptions?: Record<string, unknown> | null;
}

export interface CreatedWidget {
  id: number;
  title: string;
  widgetType: string;
  width: number;
  height: number;
  positionX: number;
  positionY: number;
  configJson?: string;
}

@Injectable({ providedIn: 'root' })
export class WidgetService {
  constructor(private http: HttpClient) {}

  create(request: CreateWidgetRequest) {
    return this.http.post<CreatedWidget>(
      `${environment.apiUrl}/widgets`,
      request
    );
  }

  getData(widgetId: number) {
    return this.http.get<Record<string, unknown>[]>(
      `${environment.apiUrl}/widgets/${widgetId}/data`
    );
  }

  // Custom dashboard style only: persists the per-widget background color
  // picked in the Staging Preview color panel.
  updateColor(widgetId: number, backgroundColor: string) {
    return this.http.put<{ backgroundColor: string }>(
      `${environment.apiUrl}/widgets/${widgetId}/style`,
      { backgroundColor }
    );
  }
    updatePosition(widgetId: number, positionX: number, positionY: number, width: number, height: number) {
    return this.http.put<void>(
      `${environment.apiUrl}/widgets/${widgetId}/position`,
      { positionX, positionY, width, height }
    );
  }

  // Server responds 204 No Content on success, so this is typed <void>
  // rather than <{ title: string }> — there is no response body to read.
  rename(widgetId: number, title: string) {
    return this.http.put<void>(
      `${environment.apiUrl}/widgets/${widgetId}/title`,
      { title }
    );
  }

  delete(widgetId: number) {
    return this.http.delete<void>(
      `${environment.apiUrl}/widgets/${widgetId}`
    );
  }
}