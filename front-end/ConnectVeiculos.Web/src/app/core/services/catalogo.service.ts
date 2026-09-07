import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CatalogoResultado } from '../models';

@Injectable({
  providedIn: 'root'
})
export class CatalogoService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiUrl}/catalogo`;

  getCatalogo(
    marca?: string,
    anoMin?: number,
    anoMax?: number,
    precoMin?: number,
    precoMax?: number,
    lojaId?: number,
    tenantSlug?: string,
    ignorarCache = false
  ): Observable<CatalogoResultado> {
    let params = new HttpParams();

    if (marca) params = params.set('marca', marca);
    if (anoMin) params = params.set('anoMin', anoMin.toString());
    if (anoMax) params = params.set('anoMax', anoMax.toString());
    if (precoMin) params = params.set('precoMin', precoMin.toString());
    if (precoMax) params = params.set('precoMax', precoMax.toString());
    if (lojaId) params = params.set('lojaId', lojaId.toString());
    if (tenantSlug) params = params.set('tenant', tenantSlug);

    // O endpoint manda Cache-Control: max-age=30, otimo pra navegacao normal.
    // Mas quando o SignalR avisa que o catalogo mudou nos SABEMOS que a copia do
    // navegador esta velha — o parametro faz a URL diferir e forca ida ao
    // servidor. Nao e' lido pelo controller.
    if (ignorarCache) params = params.set('_ts', Date.now().toString());

    return this.http.get<CatalogoResultado>(this.baseUrl, { params });
  }

  getCatalogoBySlug(
    slug: string,
    marca?: string,
    anoMin?: number,
    anoMax?: number,
    precoMin?: number,
    precoMax?: number,
    tenantSlug?: string,
    ignorarCache = false
  ): Observable<CatalogoResultado> {
    let params = new HttpParams();

    if (marca) params = params.set('marca', marca);
    if (anoMin) params = params.set('anoMin', anoMin.toString());
    if (anoMax) params = params.set('anoMax', anoMax.toString());
    if (precoMin) params = params.set('precoMin', precoMin.toString());
    if (precoMax) params = params.set('precoMax', precoMax.toString());
    if (tenantSlug) params = params.set('tenant', tenantSlug);

    if (ignorarCache) params = params.set('_ts', Date.now().toString());

    return this.http.get<CatalogoResultado>(`${this.baseUrl}/slug/${slug}`, { params });
  }

  getVeiculo(veiculoId: number, tenantSlug?: string): Observable<any> {
    let params = new HttpParams();
    if (tenantSlug) params = params.set('tenant', tenantSlug);
    return this.http.get(`${this.baseUrl}/veiculo/${veiculoId}`, { params });
  }
}
