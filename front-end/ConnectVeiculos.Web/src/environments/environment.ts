export const environment = {
  production: false,
  apiUrl: 'http://localhost:5219/api',
  siteBaseUrl: 'http://localhost:4000',
  // URL absoluta publica usada para feeds (FB Catalog, Google Merchant) que
  // precisam ser puxados por crawlers externos — nunca pode ser localhost
  // mesmo em dev, senao Facebook/Google nao conseguem buscar o XML.
  publicApiBaseUrl: 'https://connectveiculos.dev.br/api'
};
