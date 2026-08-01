import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './sidebar.html',
  styleUrls: ['./sidebar.css']
})
export class Sidebar implements OnInit {
  public name = 'System User';
  public email = '';
  public initials = 'SU';

  constructor(private authService: AuthService) {}

  public ngOnInit(): void {
    try {
      const storedUser = localStorage.getItem('user');
      const storedEmail = localStorage.getItem('email');
      this.name = storedUser ? this.sanitizeJsonString(storedUser) : 'System User';
      this.email = storedEmail ? this.sanitizeJsonString(storedEmail) : 'user@enterprise.internal';
      this.initials = this.generateInitials(this.name);
    } catch (error) {
      console.error('Failed to initialize session profile context metrics:', error);
    }
  }

  public logout(): void {
    this.authService.logout();
  }

  private sanitizeJsonString(input: string): string {
    try {
      const parsed = JSON.parse(input);
      return typeof parsed === 'string' ? parsed : input;
    } catch {
      return input;
    }
  }

  private generateInitials(fullName: string): string {
    const segments = fullName.trim().split(/\s+/);
    if (segments.length === 0 || !segments[0]) return '??';
    const firstInitial = segments[0].charAt(0);
    const lastInitial = segments.length > 1 ? segments[segments.length - 1].charAt(0) : '';
    return (firstInitial + lastInitial).toUpperCase();
  }
}
