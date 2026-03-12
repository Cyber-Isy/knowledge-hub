import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { NotificationService } from '../services/notification.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const notificationService = inject(NotificationService);

  const token = authService.getToken();

  if (token) {
    req = req.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    });
  }

  return next(req).pipe(
    catchError((error) => {
      if (error.status === 0) {
        notificationService.error('Connection lost. Please check your network and try again.');
      } else if (error.status === 401) {
        authService.logout();
        router.navigate(['/login']);
      } else if (error.status === 403) {
        notificationService.warning('You do not have permission to perform this action.');
      } else if (error.status === 429) {
        notificationService.warning('Too many requests. Please try again later.');
      } else if (error.status >= 500) {
        notificationService.error('A server error occurred. Please try again later.');
      }
      return throwError(() => error);
    }),
  );
};
