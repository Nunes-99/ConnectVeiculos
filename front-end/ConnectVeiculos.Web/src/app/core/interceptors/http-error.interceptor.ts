import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService, ToastService } from '../services';

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);
  const toast = inject(ToastService);
  const platformId = inject(PLATFORM_ID);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (isPlatformBrowser(platformId) && error.status === 401) {
        // Tenta refresh transparente uma unica vez antes de deslogar.
        // Evita loop: nao retenta se a propria request era /auth/refresh ou /auth/login.
        const url = req.url || '';
        const isAuthCall = url.includes('/auth/refresh') || url.includes('/auth/login');
        const refreshToken = authService.getRefreshToken();
        if (!isAuthCall && refreshToken) {
          return authService.refreshSession().pipe(
            switchMap(() => {
              // Re-emite a request original com o novo JWT (o auth.interceptor pega via getToken).
              const newToken = authService.getToken();
              const retry = newToken
                ? req.clone({ setHeaders: { Authorization: `Bearer ${newToken}` } })
                : req;
              return next(retry);
            }),
            catchError(() => {
              authService.logout();
              router.navigate(['/login']);
              return throwError(() => new Error('Sessao expirada. Faca login novamente.'));
            })
          );
        }

        authService.logout();
        router.navigate(['/login']);
      }

      let errorMessage = 'Ocorreu um erro inesperado';

      if (error.error instanceof ErrorEvent) {
        errorMessage = error.error.message;
      } else if (error.error?.type === 'ValidationError' && error.error?.errors) {
        const messages = Object.values(error.error.errors).flat();
        errorMessage = messages.join('. ');
      } else if (error.error?.error) {
        errorMessage = error.error.error;
      } else if (error.error?.message) {
        errorMessage = error.error.message;
      } else if (error.error?.title) {
        errorMessage = error.error.title;
      } else if (typeof error.error === 'string') {
        errorMessage = error.error;
      } else if (error.status === 404) {
        errorMessage = 'Recurso não encontrado';
      } else if (error.status === 500) {
        errorMessage = 'Erro interno do servidor';
      }

      if (isPlatformBrowser(platformId) && error.status !== 401) {
        toast.error(errorMessage);
      }

      return throwError(() => new Error(errorMessage));
    })
  );
};
