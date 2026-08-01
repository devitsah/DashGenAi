import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { DashboardService } from '../../../core/services/dashboard.service';

@Component({
  selector: 'app-home-navbar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './home-navbar.html',
  styleUrl: './home-navbar.css',
})
export class HomeNavbar implements OnInit, OnDestroy {
  public count = 0;
  private readonly destroy$ = new Subject<void>();

  constructor(private readonly dashboardService: DashboardService) {}

  public ngOnInit(): void {
    this.dashboardService.getDashboards()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: list => { this.count = list.length; },
        error: () => {}
      });
  }

  public ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }
}