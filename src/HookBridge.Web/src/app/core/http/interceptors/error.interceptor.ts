import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';
import { ProblemDetails } from '../models/problem-details.model';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // 401 Unauthorized -> clear state and redirect to login
      if (error.status === 401 && !req.url.includes('/auth/login') && !req.url.includes('/auth/register')) {
        authService.logout();
      }

      // Parse RFC 7807 ProblemDetails payload if present
      let errorMessage = 'An unexpected error occurred. Please try again.';
      if (error.error && typeof error.error === 'object') {
        const problem = error.error as ProblemDetails;
        if (problem.detail) {
          errorMessage = problem.detail;
        } else if (problem.title) {
          errorMessage = problem.title;
        }
      } else if (error.message) {
        errorMessage = error.message;
      }

      console.error(`[HTTP Error ${error.status}]: ${errorMessage}`, error);
      return throwError(() => error);
    })
  );
};
