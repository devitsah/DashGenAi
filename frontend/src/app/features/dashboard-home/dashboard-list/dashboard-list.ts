import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { DashboardService } from '../../../core/services/dashboard.service';
import { DashboardCard } from '../../../core/models/dashboard.model';
import { BehaviorSubject, combineLatest, Observable } from 'rxjs';
import { map, shareReplay } from 'rxjs/operators';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-dashboard-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './dashboard-list.html',
  styleUrl: './dashboard-list.css'
})
export class DashboardList implements OnInit {
  private allCards$ = new BehaviorSubject<DashboardCard[]>([]);
  private search$ = new BehaviorSubject<string>('');

  public searchText = '';
  public loading = true;

  public filteredCards$: Observable<DashboardCard[]> = combineLatest([
    this.allCards$,
    this.search$
  ]).pipe(
    map(([cards, q]) => {
      const term = q.toLowerCase().trim();
      return term ? cards.filter(c => c.title.toLowerCase().includes(term)) : cards;
    }),
    shareReplay(1)
  );

  constructor(
    private readonly dashboardService: DashboardService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.dashboardService.getDashboards().subscribe({
      next: cards => { this.allCards$.next(cards); this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  onSearch(): void {
    this.search$.next(this.searchText);
  }

  preview(id: number): void {
    this.router.navigate(['/homepage/DefaultView', id, 'view']);
  }

  rename(card: DashboardCard): void {
    Swal.fire({
      title: 'Rename',
      input: 'text',
      inputValue: card.title,
      inputPlaceholder: 'Dashboard name',
      showCancelButton: true,
      confirmButtonText: 'Save',
      width: '340px',
      customClass: { popup: 'swal-compact' },
      inputValidator: v => !v.trim() ? 'Name cannot be empty' : null
    }).then(result => {
      if (!result.isConfirmed || !result.value.trim()) return;
      this.dashboardService.rename(card.dashboardId, result.value.trim()).subscribe({
        next: () => {
          const updated = this.allCards$.value.map(c =>
            c.dashboardId === card.dashboardId ? { ...c, title: result.value.trim() } : c
          );
          this.allCards$.next(updated);
        },
        error: () => Swal.fire({ icon: 'error', title: 'Rename failed', toast: true, position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px' })
      });
    });
  }

  delete(card: DashboardCard): void {
    Swal.fire({
      title: 'Delete?',
      text: `"${card.title}"`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Delete',
      confirmButtonColor: '#ef4444',
      width: '300px',
      customClass: { popup: 'swal-compact' }
    }).then(result => {
      if (!result.isConfirmed) return;
      this.dashboardService.delete(card.dashboardId).subscribe({
        next: () => {
          this.allCards$.next(this.allCards$.value.filter(c => c.dashboardId !== card.dashboardId));
          Swal.fire({ icon: 'success', title: 'Deleted', toast: true, position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px' });
        },
        error: () => Swal.fire({ icon: 'error', title: 'Delete failed', toast: true, position: 'top-end', showConfirmButton: false, timer: 2000, width: '280px' })
      });
    });
  }
}
