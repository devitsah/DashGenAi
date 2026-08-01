import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl:'./login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent {
  email = '';
  password = '';

  loading = signal(false);
  error = signal<string | null>(null);

  constructor(
    private auth: AuthService,
    private router: Router
  ) {}

  submit() {
    if (!this.email || !this.password) {
      this.error.set('Please enter both email and password.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.auth.login(this.email, this.password).subscribe({
      next: () => {
        this.loading.set(false);
        // '/homepage' renders whichever dashboard the user has set as
        // default (falls back to an empty state if they haven't set one).
        this.router.navigate(['/homepage']);
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Invalid email or password.');
      }
    });
  }
}