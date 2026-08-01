import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import Swal from 'sweetalert2';
import { DashboardService } from '../../../core/services/dashboard.service';

@Component({
  selector: 'app-save',
  templateUrl: './save.html',
  styleUrls: ['./save.css'],
  imports: [FormsModule]
})
export class Save {
  dashboard = { name: '', description: '' };
  saving = false;

  constructor(private router: Router, private dashboardService: DashboardService) {}

  saveDashboard(): void {
    if (!this.dashboard.name.trim()) {
      Swal.fire({ icon: 'warning', title: 'Name required', toast: true, position: 'top-end', showConfirmButton: false, timer: 2000, width: '320px' });
      return;
    }
    this.saving = true;
    this.dashboardService.create(this.dashboard.name.trim(), this.dashboard.description.trim() || undefined).subscribe({
      next: () => {
        Swal.fire({ title: 'Saved!', text: 'Dashboard saved successfully', icon: 'success', confirmButtonText: 'OK' })
          .then(() => this.router.navigate(['/homepage/DashboardHome']));
      },
      error: () => {
        this.saving = false;
        Swal.fire({ icon: 'error', title: 'Save failed', toast: true, position: 'top-end', showConfirmButton: false, timer: 2500, width: '320px' });
      }
    });
  }
}
