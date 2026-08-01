import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DashboardService } from '../../../core/services/dashboard.service';
import { BuilderStateService } from '../builder-state.service';
import { Dashboard } from '../../../core/models/dashboard.model';

@Component({
  selector: 'app-style',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './style.html',
  styleUrls: ['./style.css']
})
export class Style implements OnInit {
  mode: 'create' | 'existing' = 'create';

  // Create mode
  dashboardName = '';
  dashboardDescription = '';
  dashboardStyle = 'powerbi';
  creating = false;
  createError: string | null = null;

  // Existing mode
  existingDashboards: Dashboard[] = [];
  selectedExistingId: number | null = null;
  loadingExisting = false;
  searchQuery = '';

  readonly styleOptions = [
    { value: 'powerbi', label: 'Power BI' },
    { value: 'grafana', label: 'Grafana' },
    { value: 'custom',  label: 'Custom'  }
  ];

  constructor(private dashboardService: DashboardService, public state: BuilderStateService) {}

  ngOnInit(): void {
    this.loadingExisting = true;
    this.dashboardService.loadList().subscribe({
      next: (list) => { this.existingDashboards = list; this.loadingExisting = false; },
      error: () => { this.loadingExisting = false; }
    });
  }

  get dashboardReady(): boolean { return !!this.state.dashboardId(); }

  get selectedExistingName(): string {
    return this.existingDashboards.find(d => d.id === this.selectedExistingId)?.name ?? '';
  }

  get filteredDashboards(): Dashboard[] {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return this.existingDashboards;
    return this.existingDashboards.filter(d =>
      d.name.toLowerCase().includes(q) ||
      (d.description ?? '').toLowerCase().includes(q)
    );
  }

  setMode(m: 'create' | 'existing'): void {
    if (this.dashboardReady) return;
    this.mode = m;
    this.selectedExistingId = null;
    this.searchQuery = '';
    this.createError = null;
  }

  styleLabel(raw: string): string {
    const map: Record<string, string> = {
      'PowerBi': 'Power BI', 'powerbi': 'Power BI',
      'Grafana': 'Grafana',  'grafana': 'Grafana',
      'Custom':  'Custom',   'custom':  'Custom'
    };
    return map[raw] ?? raw;
  }

  createDashboard(): void {
    if (!this.dashboardName.trim() || this.creating || this.dashboardReady) return;
    this.creating = true;
    this.createError = null;
    this.dashboardService.createAndReturnId(
      this.dashboardName.trim(),
      this.dashboardDescription.trim() || undefined,
      this.dashboardStyle
    ).subscribe({
      next: (id) => {
        this.state.setDashboardId(id);
        this.state.setStyle(this.dashboardStyle);
        this.state.selectedStyle.set(this.dashboardStyle);
        this.state.createdDashboardName.set(this.dashboardName.trim());
        this.creating = false;
      },
      error: (err) => {
        this.createError = err?.error?.message ?? 'Failed to create dashboard.';
        this.creating = false;
      }
    });
  }

  useExisting(): void {
    if (!this.selectedExistingId || this.dashboardReady) return;
    const dash = this.existingDashboards.find(d => d.id === this.selectedExistingId);
    if (!dash) return;
    this.state.setDashboardId(dash.id);
    const styleMap: Record<string, string> = {
      'PowerBi': 'powerbi', 'Grafana': 'grafana', 'Custom': 'custom'
    };
    const mappedStyle = styleMap[dash.dashboardStyle] ?? 'powerbi';
    this.state.selectedStyle.set(mappedStyle);
    this.state.setStyle(mappedStyle);
  }
}
