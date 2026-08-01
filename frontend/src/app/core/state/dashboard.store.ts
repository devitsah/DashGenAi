import { Injectable, computed } from '@angular/core';
import { DashboardService } from '../services/dashboard.service';

@Injectable({ providedIn: 'root' })
export class DashboardStore {
  readonly dashboards = computed(() => this.svc.dashboards());
  readonly selected = computed(() => this.svc.selectedDashboard());
  readonly count = computed(() => this.svc.dashboards().length);

  constructor(private svc: DashboardService) {}

  load() { return this.svc.loadList(); }
  loadById(id: number) { return this.svc.loadById(id); }
  delete(id: number) { return this.svc.delete(id); }
  rename(id: number, name: string, description?: string) {
    return this.svc.rename(id, name, description);
  }
}