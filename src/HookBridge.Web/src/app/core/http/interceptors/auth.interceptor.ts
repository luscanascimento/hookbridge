import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Cookies are sent automatically by the browser when withCredentials is true.
  // No need to manually set Authorization header for browser-based auth.
  const authReq = req.clone({ withCredentials: true });
  return next(authReq);
};

