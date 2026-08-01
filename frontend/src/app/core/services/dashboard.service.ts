import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { switchMap, tap, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { Dashboard, DashboardCard } from '../models/dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {

  // Signals
  dashboards = signal<Dashboard[]>([]);
  selectedDashboard = signal<Dashboard | null>(null);

  constructor(private http: HttpClient) {}

  // ==========================
  // Create Dashboard
  // ==========================
  create(name: string, description?: string): Observable<Dashboard[]> {
    return this.http
      .post(`${environment.apiUrl}/dashboards`, { name, description })
      .pipe(switchMap(() => this.loadList()));
  }

  createAndReturnId(name: string, description?: string, style?: string): Observable<number> {
    return this.http
      .post<{ id: number }>(`${environment.apiUrl}/dashboards`, { name, description, style })
      .pipe(map(res => res.id));
  }

  // ==========================
  // Load All Dashboards
  // ==========================
  loadList(): Observable<Dashboard[]> {
    return this.http
      .get<Dashboard[]>(`${environment.apiUrl}/dashboards`)
      .pipe(
        tap(list => this.dashboards.set(list))
      );
  }

  // Alias used by old components
getDashboards(): Observable<DashboardCard[]> {
  return this.http
    .get<Dashboard[]>(`${environment.apiUrl}/dashboards`)
    .pipe(
      tap(list => this.dashboards.set(list)),
      map(list =>
        list.map(d => ({
          dashboardId: d.id,
          title: d.name,
          description: d.description ?? '',
          platform: d.dashboardStyle ?? 'Custom',
          updated: String(d.versionNo ?? 1),
          widgets: d.widgets?.length ?? 0
        }))
      )
    );
}

  // ==========================
  // Load Single Dashboard
  // ==========================
  loadById(id: number): Observable<Dashboard> {
    return this.http
      .get<Dashboard>(`${environment.apiUrl}/dashboards/${id}`)
      .pipe(
        tap(d => this.selectedDashboard.set(d))
      );
  }

  // ==========================
  // Rename Dashboard
  // ==========================
  rename(
    id: number,
    name: string,
    description?: string
  ): Observable<any> {
    return this.http
      .put(`${environment.apiUrl}/dashboards/${id}`, {
        name,
        description
      })
      .pipe(
        tap(() => {
          this.dashboards.update(list =>
            list.map(d =>
              d.id === id
                ? {
                    ...d,
                    name,
                    description: description ?? d.description
                  }
                : d
            )
          );
        })
      );
  }
   clone(id: number): Observable<{ id: number }> {
    return this.http.post<{ id: number }>(
      `${environment.apiUrl}/dashboards/${id}/clone`,
      {}
    );
  }

  // ==========================
  // Delete Dashboard
  // ==========================
  delete(id: number): Observable<any> {
    return this.http
      .delete(`${environment.apiUrl}/dashboards/${id}`)
      .pipe(
        tap(() => {
          this.dashboards.update(list =>
            list.filter(d => d.id !== id)
          );

          if (this.selectedDashboard()?.id === id) {
            this.selectedDashboard.set(null);
          }
        })
      );
  }

  // ==========================
  // Dashboard Count
  // ==========================
  getDashCounts(): Observable<number> {
    return this.http.get<number>(
      `${environment.apiUrl}/dashboards/count`
    );
  }

  // ==========================
  // Get Default Dashboard
  // ==========================
  get_default_dashboard(): Observable<Dashboard> {
    return this.http.get<Dashboard>(
      `${environment.apiUrl}/dashboards/default`
    );
  }

  // ==========================
  // Set Default Dashboard
  // ==========================
  set_default_dashboard(id: number): Observable<any> {
    return this.http.post(
      `${environment.apiUrl}/dashboards/default/${id}`,
      {}
    );
  }
  updateBackgroundColor(id: number, backgroundColor: string): Observable<{ backgroundColor: string }> {
    return this.http.put<{ backgroundColor: string }>(
      `${environment.apiUrl}/dashboards/${id}/style`,
      { backgroundColor }
    );
  }
}