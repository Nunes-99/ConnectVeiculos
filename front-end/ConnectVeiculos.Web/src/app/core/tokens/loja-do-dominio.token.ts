import { DOCUMENT } from '@angular/common';
import { InjectionToken, inject } from '@angular/core';

/**
 * Slug da loja dona do dominio proprio desta requisicao (ex: a Diamante em
 * diamanteveiculossalto.com.br), ou '' no endereco da plataforma.
 *
 * No SSR vem do server.ts (providers do render). No navegador vem da meta
 * "cv-loja-dominio" que o server.ts injeta no HTML. Sem providedIn de
 * proposito: um token 'root' com factory seria resolvido no injector da
 * aplicacao e ignoraria o valor que o server.ts fornece na plataforma.
 */
export const LOJA_DO_DOMINIO = new InjectionToken<string>('LOJA_DO_DOMINIO');

export const META_LOJA_DO_DOMINIO = 'cv-loja-dominio';

/** Slug da loja do dominio proprio, ou null. Chamar em contexto de injecao. */
export function injectLojaDoDominio(): string | null {
  const doServidor = inject(LOJA_DO_DOMINIO, { optional: true });
  if (doServidor != null) return doServidor || null;
  const meta = inject(DOCUMENT).querySelector(`meta[name="${META_LOJA_DO_DOMINIO}"]`);
  return meta?.getAttribute('content') || null;
}

/**
 * Caminhos publicos do catalogo. No dominio da propria loja o catalogo fica na
 * raiz ("/" e "/veiculo/123"); no endereco da plataforma segue em
 * /catalogo/{slug}.
 */
export function caminhoCatalogo(tenantSlug: string | null, lojaDoDominio: string | null): string {
  if (!tenantSlug) return '/catalogo';
  if (tenantSlug === lojaDoDominio) return '/';
  return `/catalogo/${tenantSlug}`;
}

export function caminhoVeiculo(tenantSlug: string | null, lojaDoDominio: string | null, veiculoId: number): string {
  if (!tenantSlug) return `/catalogo?veiculo=${veiculoId}`;
  if (tenantSlug === lojaDoDominio) return `/veiculo/${veiculoId}`;
  return `/catalogo/${tenantSlug}/veiculo/${veiculoId}`;
}
