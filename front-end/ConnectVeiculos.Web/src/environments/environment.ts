export const environment = {
  production: false,
  apiUrl: 'http://localhost:5219/api',
  siteBaseUrl: 'http://localhost:4000',
  // URL absoluta publica usada para feeds (FB Catalog, Google Merchant) que
  // precisam ser puxados por crawlers externos — nunca pode ser localhost
  // mesmo em dev, senao Facebook/Google nao conseguem buscar o XML.
  publicApiBaseUrl: 'https://connectveiculos.dev.br/api',
  // WhatsApp do suporte, em E.164 sem o '+' (formato que o wa.me espera).
  // Fica aqui pra trocar sem mexer em codigo — antes estava fixo no
  // componente do plano, e era um numero ficticio em producao.
  suporteWhatsApp: '5511953179948'
};
