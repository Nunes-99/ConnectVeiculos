import { HttpInterceptorFn } from '@angular/common/http';
import { isPlatformServer } from '@angular/common';
import { inject, PLATFORM_ID } from '@angular/core';

export const serverUrlInterceptor: HttpInterceptorFn = (req, next) => {
  const platformId = inject(PLATFORM_ID);

  if (isPlatformServer(platformId) && req.url.startsWith('/api')) {
    const serverApiBase = (typeof process !== 'undefined' && process.env?.['API_BASE_URL']) || 'http://localhost:5219';
    const clonedReq = req.clone({
      url: `${serverApiBase}${req.url}`
    });
    return next(clonedReq);
  }

  return next(req);
};
