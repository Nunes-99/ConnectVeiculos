export const environment = {
  production: true,
  apiUrl: '/api',
  // URL absoluta usada para montar og:image, og:url, canonical etc.
  // Necessario porque WhatsApp/Facebook ignoram URLs relativas em og:image
  // e document.location.origin nao e confiavel durante SSR (CommonEngine + domino).
  siteBaseUrl: 'https://connectveiculos.dev.br',
  // URL absoluta publica usada para feeds (FB Catalog, Google Merchant) que
  // precisam ser puxados por crawlers externos.
  publicApiBaseUrl: 'https://connectveiculos.dev.br/api'
};
