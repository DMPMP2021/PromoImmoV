import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { UserInfo } from './models';

const KEY = 'promoimmo.user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  user: UserInfo | null = JSON.parse(localStorage.getItem(KEY) ?? 'null');

  get token() { return this.user?.token ?? ''; }
  get isLoggedIn() { return !!this.user; }
  hasRole(...roles: string[]) { return !!this.user && roles.some(r => this.user!.roles.includes(r)); }

  login(userName: string, password: string) {
    return this.http.post<UserInfo>(`${environment.apiUrl}/api/auth/login`, { userName, password })
      .pipe(tap(u => { this.user = u; localStorage.setItem(KEY, JSON.stringify(u)); }));
  }
  logout() {
    this.user = null;
    localStorage.removeItem(KEY);
    this.router.navigate(['/login']);
  }
}
