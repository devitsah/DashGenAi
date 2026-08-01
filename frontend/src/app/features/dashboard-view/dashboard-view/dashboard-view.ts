import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterOutlet, RouterModule } from '@angular/router';
import { DashboardService } from '../../../core/services/dashboard.service';
import Swal from 'sweetalert2';

@Component({
  selector: 'app-dashboard-view',
  imports: [RouterOutlet, RouterModule],
  templateUrl: './dashboard-view.html',
  styleUrl: './dashboard-view.css',
})
export class DashboardView implements OnInit {
  dashboardId!: number;
  title = '';

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private dashboardService: DashboardService
  ) {}

  ngOnInit() {
    this.dashboardId = Number(this.route.snapshot.paramMap.get('id'));
    this.dashboardService.loadById(this.dashboardId).subscribe({
      next: d => (this.title = d.name),
      error: () => (this.title = 'Unknown dashboard')
    });
  }

  NavigateView(): void {
    this.router.navigate(['/homepage/DefaultView', this.dashboardId, 'view']);
  }

  NavigateDesign(): void {
    this.router.navigate(['/homepage/DefaultView', this.dashboardId, 'design']);
  }

  Delete_Dashboard(): void {
    this.dashboardService.delete(this.dashboardId).subscribe({
      next: () => {
        Swal.fire({
          icon: 'success', title: 'Dashboard deleted!', toast: true,
          position: 'top-end', showConfirmButton: false, timer: 2000,
          timerProgressBar: true, width: '320px'
        }).then(() => this.router.navigate(['/homepage/DashboardHome']));
      },
      error: () => {
        Swal.fire({
          icon: 'error', title: 'Error Found!', toast: true,
          position: 'top-end', showConfirmButton: false, timer: 2000,
          timerProgressBar: true, width: '320px'
        });
      }
    });
  }

  Clone_Dashboard(): void {
    this.dashboardService.clone(this.dashboardId).subscribe({
      next: () => {
        Swal.fire({
          icon: 'success', title: 'Dashboard cloned!', toast: true,
          position: 'top-end', showConfirmButton: false, timer: 2000,
          timerProgressBar: true, width: '320px'
        }).then(() => this.router.navigate(['/homepage/DashboardHome']));
      },
      error: (err) => {
        Swal.fire({
          icon: 'error', title: 'Could not clone dashboard',
          text: err?.error?.message ?? 'Unknown error — check backend logs.',
          toast: true, position: 'top-end', showConfirmButton: false, timer: 4000,
          timerProgressBar: true, width: '360px'
        });
      }
    });
  }

  Set_Default(): void {
    this.dashboardService.set_default_dashboard(this.dashboardId).subscribe({
      next: () => {
        Swal.fire({
          icon: 'success', title: 'Set as default dashboard', toast: true,
          position: 'top-end', showConfirmButton: false, timer: 2000,
          timerProgressBar: true, width: '320px'
        });
      },
      error: (err) => {
        Swal.fire({
          icon: 'error', title: 'Could not set default',
          text: err?.error?.message ?? 'Unknown error — check backend logs.',
          toast: true, position: 'top-end', showConfirmButton: false, timer: 4000,
          timerProgressBar: true, width: '360px'
        });
      }
    });
  }
}