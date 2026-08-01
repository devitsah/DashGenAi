import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

const TOKEN_KEY = 'ai_dashboard_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private tokenSignal = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  isAuthenticated = computed(() => !!this.tokenSignal());

  constructor(private http: HttpClient, private router: Router) {
    // On app start, if a token already exists (e.g. from a previous session),
    // make sure the display name/email derived from it are populated too.
    const existingToken = this.tokenSignal();
    if (existingToken) {
      this.storeUserInfoFromToken(existingToken);
    }
  }

  register(name: string, email: string, password: string) {
    return this.http
      .post<{ token: string }>(`${environment.apiUrl}/auth/register`, { name, email, password })
      .pipe(tap(res => this.setToken(res.token)));
  }

  login(email: string, password: string) {
    return this.http
      .post<{ token: string }>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap(res => this.setToken(res.token)));
  }

  logout() {
    this.http.post(`${environment.apiUrl}/auth/logout`, {}).subscribe({
      complete: () => this.clearSession(),
      error: () => this.clearSession() // clear locally even if the server call fails
    });
  }

  getToken(): string | null {
    return this.tokenSignal();
  }

  private setToken(token: string) {
    localStorage.setItem(TOKEN_KEY, token);
    this.tokenSignal.set(token);
    this.storeUserInfoFromToken(token);
  }

  /**
   * The JWT already carries "name" and "email" claims (see JwtTokenGenerator on the backend).
   * Decode them client-side so the sidebar can show the real logged-in user instead of
   * always falling back to "System User".
   */
  private storeUserInfoFromToken(token: string): void {
    try {
      const payloadSegment = token.split('.')[1];
      if (!payloadSegment) return;

      const base64 = payloadSegment.replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(base64.length + (4 - (base64.length % 4)) % 4, '=');
      const payload = JSON.parse(atob(padded));

      if (payload.name) localStorage.setItem('user', payload.name);
      if (payload.email) localStorage.setItem('email', payload.email);
    } catch {
      // Malformed/unexpected token shape — leave localStorage untouched,
      // sidebar will fall back to its defaults.
    }
  }

  private clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem('user');
    localStorage.removeItem('email');
    this.tokenSignal.set(null);
    this.router.navigate(['/login']);
  }
}
