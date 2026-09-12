import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../../auth/services/auth.service';
import { ProblemDetails } from '../models/problem-details.model';
import { ToastService } from '../../../shared/components/ui/toast/toast.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const toastService = inject(ToastService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      // 1. Structured RFC 7807 ProblemDetails payload extraction
      let errorMessage = 'Ocorreu um erro inesperado. Por favor, tente novamente.';
      let errorTitle = 'Erro na Requisição';
      const fieldValidationErrors: string[] = [];

      if (error.error && typeof error.error === 'object') {
        const problem = error.error as ProblemDetails;

        if (problem.errors && typeof problem.errors === 'object') {
          for (const key of Object.keys(problem.errors)) {
            const msgs = problem.errors[key];
            if (Array.isArray(msgs) && msgs.length > 0) {
              fieldValidationErrors.push(...msgs);
            }
          }
        }

        if (fieldValidationErrors.length > 0) {
          errorMessage = fieldValidationErrors.join(' • ');
          errorTitle = problem.title || 'Falha de Validação';
        } else if (problem.detail) {
          errorMessage = problem.detail;
          errorTitle = problem.title || errorTitle;
        } else if (problem.title) {
          errorMessage = problem.title;
        }
      } else if (typeof error.error === 'string' && error.error.trim().length > 0) {
        errorMessage = error.error;
      } else if (error.message) {
        errorMessage = error.message;
      }

      // 2. Defensive HTTP status code handling
      if (error.status === 429) {
        const retryAfter = error.headers.get('Retry-After');
        const delayMsg = retryAfter ? ` Aguarde ${retryAfter} segundos.` : ' Aguarde um momento.';
        toastService.warning(
          `Limite de requisições atingido.${delayMsg}`,
          'Taxa Excedida (429)',
          8000
        );
      } else if (error.status === 401) {
        const isAuthRoute = req.url.includes('/auth/login') || req.url.includes('/auth/register');
        if (!isAuthRoute) {
          toastService.warning(
            'Sua sessão expirou ou foi invalidada. Por favor, faça login novamente.',
            'Sessão Expirada',
            6000
          );
          authService.logout();
        }
      } else if (error.status === 403) {
        toastService.error(
          'Você não possui privilégios suficientes para executar esta operação.',
          'Acesso Negado (403)',
          7000
        );
      } else if (error.status === 0) {
        toastService.error(
          'Não foi possível estabelecer conexão com o backend HookBridge. Verifique sua rede.',
          'Servidor Indisponível',
          8000
        );
      } else if (error.status >= 500) {
        toastService.error(
          errorMessage,
          `Falha no Servidor (${error.status})`,
          9000
        );
      }

      console.error(`[HTTP Error ${error.status}]: ${errorMessage}`, error);
      return throwError(() => error);
    })
  );
};
