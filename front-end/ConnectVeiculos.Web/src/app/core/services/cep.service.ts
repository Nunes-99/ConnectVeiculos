import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';

export interface EnderecoCep {
  cep: string;
  logradouro: string;
  bairro: string;
  cidade: string;
  uf: string;
  /** Codigo IBGE do municipio (o que a NF-e pede). */
  codigoMunicipio: number;
}

/** Busca de endereco pelo CEP no ViaCEP. Devolve null se o CEP nao existir ou o servico falhar. */
@Injectable({ providedIn: 'root' })
export class CepService {
  private http = inject(HttpClient);

  buscar(cep: string | null | undefined): Observable<EnderecoCep | null> {
    const digitos = (cep || '').replace(/\D/g, '');
    if (digitos.length !== 8) return of(null);
    return this.http.get<any>(`https://viacep.com.br/ws/${digitos}/json/`).pipe(
      map(d => d?.erro ? null : {
        cep: digitos,
        logradouro: d.logradouro || '',
        bairro: d.bairro || '',
        cidade: d.localidade || '',
        uf: d.uf || '',
        codigoMunicipio: Number(d.ibge) || 0
      }),
      catchError(() => of(null))
    );
  }
}

/** Compara nomes de cidade sem acento e sem diferenca de maiusculas. */
export function mesmaCidade(a: string | null | undefined, b: string | null | undefined): boolean {
  const n = (s: string | null | undefined) => (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').trim().toUpperCase();
  return !!a && n(a) === n(b);
}
