import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services';

export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        authService.logout();
        router.navigate(['/login']);
      }

      let errorMessage = 'Ocorreu um erro inesperado';

      if (error.error instanceof ErrorEvent) {
        errorMessage = error.error.message;
      } else if (error.error?.type === 'ValidationError' && error.error?.errors) {
        const messages = Object.values(error.error.errors).flat();
        errorMessage = messages.join('\n');
      } else if (error.error?.message) {
        errorMessage = error.error.message;
      } else if (error.error?.title) {
        errorMessage = error.error.title;
      } else if (error.status === 404) {
        errorMessage = 'Recurso não encontrado';
      } else if (error.status === 500) {
        errorMessage = 'Erro interno do servidor';
      }

      console.error('HTTP Error:', errorMessage, error);

      // Mostrar feedback para o usuario (exceto 401 que ja faz logout)
      if (error.status !== 401) {
        alert(errorMessage);
      }

      return throwError(() => new Error(errorMessage));
    })
  );
};
