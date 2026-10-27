import { InjectionToken } from '@angular/core';

/**
 * Origem publica da requisicao (ex: "https://diamanteveiculos.com.br"),
 * fornecida pelo server.ts no SSR. Sem ela o canonical/og:url de uma loja com
 * dominio proprio sairia com o environment.siteBaseUrl (connectveiculos.dev.br),
 * porque document.location.origin vem vazio no CommonEngine.
 */
export const SITE_ORIGIN = new InjectionToken<string>('SITE_ORIGIN');
