import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services';
import { environment } from '../../../environments/environment';

// So intercepta requisicoes para a API propria. Requisicoes externas (ViaCEP,
// CDNs, etc) passam intactas para nao quebrar com CORS preflight em headers
// custom como X-Tenant-Slug.
function isLocalApi(url: string): boolean {
  if (url.startsWith('/')) return true;
  try {
    const target = new URL(url);
    const apiBase = new URL(environment.apiUrl, window.location.origin);
    return target.host === apiBase.host;
  } catch {
    return false;
  }
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!isLocalApi(req.url)) {
    return next(req);
  }

  const authService = inject(AuthService);
  const token = authService.getToken();
  const tenantSlug = authService.getTenantSlug();

  const headers: Record<string, string> = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (tenantSlug) headers['X-Tenant-Slug'] = tenantSlug;

  if (Object.keys(headers).length > 0) {
    return next(req.clone({ setHeaders: headers }));
  }
  return next(req);
};
