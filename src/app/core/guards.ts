import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn || inject(Router).parseUrl('/login');
};

export const roleGuard = (...roles: string[]): CanActivateFn => () => {
  const auth = inject(AuthService);
  return auth.hasRole(...roles) || inject(Router).parseUrl('/');
};
