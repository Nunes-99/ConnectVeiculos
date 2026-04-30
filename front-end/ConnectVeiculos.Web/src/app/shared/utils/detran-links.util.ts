/**
 * Mapa de URLs de consulta de debitos veiculares dos Detrans estaduais.
 *
 * Decisao de design: nenhum Detran oferece deep-link confiavel para preencher
 * a placa via querystring (cada um exige formulario manual). Por isso retornamos
 * a URL da pagina de consulta de debitos e o usuario cola a placa la.
 *
 * URLs verificadas em 2026 — se algum Detran reformular o site, basta atualizar aqui.
 */

export interface DetranLink {
  uf: string;
  nome: string;
  url: string;
  observacao?: string;
}

const LINKS: Record<string, DetranLink> = {
  AC: { uf: 'AC', nome: 'Detran-AC', url: 'https://www.detran.ac.gov.br' },
  AL: { uf: 'AL', nome: 'Detran-AL', url: 'https://www.detran.al.gov.br' },
  AM: { uf: 'AM', nome: 'Detran-AM', url: 'https://www.detran.am.gov.br' },
  AP: { uf: 'AP', nome: 'Detran-AP', url: 'https://www.detran.ap.gov.br' },
  BA: { uf: 'BA', nome: 'Detran-BA', url: 'https://www.detran.ba.gov.br' },
  CE: { uf: 'CE', nome: 'Detran-CE', url: 'https://www.detran.ce.gov.br' },
  DF: { uf: 'DF', nome: 'Detran-DF', url: 'https://www.detran.df.gov.br/index.php/menu-servicos/consulta-de-debitos' },
  ES: { uf: 'ES', nome: 'Detran-ES', url: 'https://detran.es.gov.br' },
  GO: { uf: 'GO', nome: 'Detran-GO', url: 'https://www.detran.go.gov.br' },
  MA: { uf: 'MA', nome: 'Detran-MA', url: 'https://www.detran.ma.gov.br' },
  MG: { uf: 'MG', nome: 'Detran-MG', url: 'https://www.detran.mg.gov.br/veiculos/situacao-do-veiculo' },
  MS: { uf: 'MS', nome: 'Detran-MS', url: 'https://www.detran.ms.gov.br' },
  MT: { uf: 'MT', nome: 'Detran-MT', url: 'https://www.detran.mt.gov.br' },
  PA: { uf: 'PA', nome: 'Detran-PA', url: 'https://www.detran.pa.gov.br' },
  PB: { uf: 'PB', nome: 'Detran-PB', url: 'https://www.detran.pb.gov.br' },
  PE: { uf: 'PE', nome: 'Detran-PE', url: 'https://www.detran.pe.gov.br' },
  PI: { uf: 'PI', nome: 'Detran-PI', url: 'https://www.detran.pi.gov.br' },
  PR: { uf: 'PR', nome: 'Detran-PR', url: 'https://www.detran.pr.gov.br/Pagina/Consulta-de-Veiculos' },
  RJ: { uf: 'RJ', nome: 'Detran-RJ', url: 'https://www.detran.rj.gov.br/_documento.veiculo/index.asp' },
  RN: { uf: 'RN', nome: 'Detran-RN', url: 'https://www.detran.rn.gov.br' },
  RO: { uf: 'RO', nome: 'Detran-RO', url: 'https://detran.ro.gov.br' },
  RR: { uf: 'RR', nome: 'Detran-RR', url: 'https://www.detran.rr.gov.br' },
  RS: { uf: 'RS', nome: 'Detran-RS', url: 'https://www.detran.rs.gov.br/sec/inicial?p=publico-veiculos-consulta-cobranca' },
  SC: { uf: 'SC', nome: 'Detran-SC', url: 'https://www.detran.sc.gov.br' },
  SE: { uf: 'SE', nome: 'Detran-SE', url: 'https://www.detran.se.gov.br' },
  SP: { uf: 'SP', nome: 'Detran-SP', url: 'https://www.detran.sp.gov.br/wps/portal/portaldetran/cidadao/veiculos/inf_servicos_veiculos/consulta_debitos' },
  TO: { uf: 'TO', nome: 'Detran-TO', url: 'https://www.detran.to.gov.br' }
};

const FALLBACK: DetranLink = {
  uf: '',
  nome: 'Sinesp Cidadao',
  url: 'https://www.gov.br/pt-br/servicos/consultar-veiculo-pelo-aplicativo-sinesp-cidadao',
  observacao: 'UF nao identificada — direcionado ao consulta federal Sinesp'
};

/** Retorna o link do Detran do estado informado, ou Sinesp como fallback. */
export function getDetranLink(uf?: string | null): DetranLink {
  if (!uf) return FALLBACK;
  const key = uf.trim().toUpperCase();
  return LINKS[key] ?? FALLBACK;
}

/** Lista todos os links (util para selector). */
export function getAllDetranLinks(): DetranLink[] {
  return Object.values(LINKS);
}
