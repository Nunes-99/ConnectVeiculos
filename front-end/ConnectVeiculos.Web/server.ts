import { APP_BASE_HREF } from '@angular/common';
import { CommonEngine } from '@angular/ssr';
import express from 'express';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import bootstrap from './src/main.server';
import { SITE_ORIGIN } from './src/app/core/tokens/site-origin.token';
import { LOJA_DO_DOMINIO, META_LOJA_DO_DOMINIO } from './src/app/core/tokens/loja-do-dominio.token';

// =====================================================================
// Verification meta tags (Google Search Console + Facebook Domain Verif)
// ---------------------------------------------------------------------
// Cada tenant cadastra seu proprio "content" via admin (Sistema > Integracoes).
// O SSR busca a lista agregada de TODOS os tenants ativos e injeta uma meta
// tag por codigo no <head> de QUALQUER URL servida (admin e catalogo).
// Cache de 5 min reduz pressao no backend; novos codigos aparecem na proxima
// requisicao apos esse intervalo.
// =====================================================================
const VERIFICATION_CACHE_TTL_MS = 5 * 60 * 1000;
// Tags de verificacao POR LOJA (slug -> tags). Cada pagina recebe so as da
// loja dona do host: antes todas as lojas saiam em todas as paginas, e qualquer
// loja cadastrada gratis se verificava como dona de connectveiculos.dev.br.
let verificationPorSlug = new Map<string, string>();
// Codigo do cadastro automatico no Search Console (SearchConsoleDominiosJob):
// so sai no dominio proprio da loja, nunca na raiz da plataforma.
let verificationAutoPorSlug = new Map<string, string>();
let verificationCacheExpiresAt = 0;

async function carregarVerificacoes(): Promise<void> {
  const now = Date.now();
  if (now < verificationCacheExpiresAt) return;

  try {
    const apiBase = process.env['API_BASE_URL'] || 'http://localhost:5219';
    const resp = await fetch(`${apiBase}/api/integracoes/verification-codes`);
    if (!resp.ok) {
      verificationCacheExpiresAt = now + 30_000; // backoff curto em caso de erro
      return;
    }
    const data = (await resp.json()) as {
      lojas?: Array<{ slug: string; google?: string | null; facebook?: string | null; googleAuto?: string | null }>;
    };
    const mapa = new Map<string, string>();
    const auto = new Map<string, string>();
    for (const loja of data.lojas || []) {
      if (loja.googleAuto && loja.googleAuto !== loja.google) auto.set(loja.slug.toLowerCase(), loja.googleAuto);
      const tags: string[] = [];
      if (loja.google) tags.push(`    <meta name="google-site-verification" content="${escapeAttr(loja.google)}" />`);
      if (loja.facebook) tags.push(`    <meta name="facebook-domain-verification" content="${escapeAttr(loja.facebook)}" />`);
      if (tags.length) mapa.set(loja.slug.toLowerCase(), tags.join('\n'));
    }
    verificationPorSlug = mapa;
    verificationAutoPorSlug = auto;
    verificationCacheExpiresAt = now + VERIFICATION_CACHE_TTL_MS;
  } catch {
    verificationCacheExpiresAt = now + 30_000;
  }
}

/**
 * Tags da loja dona deste host: dominio proprio -> a loja dele; subdominio
 * da plataforma (loja.connectveiculos.dev.br) -> essa loja; raiz da
 * plataforma -> so os tenants da propria plataforma (VERIFICACAO_RAIZ_SLUGS).
 */
// "empresa-teste" continua: a meta do Facebook dela pode ser a que verifica
// o dominio da plataforma na Meta.
const VERIFICACAO_RAIZ_SLUGS = (process.env['VERIFICACAO_RAIZ_SLUGS'] || 'default,empresa-teste')
  .split(',').map((s) => s.trim().toLowerCase()).filter(Boolean);

// Codigos Google fixos da raiz da plataforma. Em 29/10/2026 nao se sabia qual
// dos dois codigos (o da loja "default" ou o antigo da Diamante) verificou
// connectveiculos.dev.br no Search Console. Em 30/10 a Diamante trocou o dela
// pelo do proprio dominio (diamanteveiculossalto.com.br); o antigo fica aqui
// para a plataforma nao perder a verificacao. Codigo de verificacao e' publico
// (sai no HTML), nao e' segredo.
const VERIFICACAO_RAIZ_GOOGLE_FIXOS = (process.env['VERIFICACAO_RAIZ_GOOGLE'] || 'LlCIrMvkdbBYifIkgCBRtShWRyez1Kke9xDlx9IHkQI')
  .split(',').map((s) => s.trim()).filter(Boolean);

async function getVerificationMetaTags(req: express.Request): Promise<string> {
  await Promise.all([carregarVerificacoes(), carregarDominios()]);
  const host = hostNu(req);
  const raiz = new URL(PLATAFORMA_URL).hostname.toLowerCase().replace(/^www\./, '');
  const slugs: string[] = [];
  const doDominio = slugDoDominio(req);
  if (doDominio) slugs.push(doDominio);
  else if (host === raiz) slugs.push(...VERIFICACAO_RAIZ_SLUGS);
  else if (host.endsWith('.' + raiz)) slugs.push(host.slice(0, -(raiz.length + 1)));
  const tags = slugs.map((s) => verificationPorSlug.get(s)).filter(Boolean) as string[];
  const auto = doDominio ? verificationAutoPorSlug.get(doDominio) : undefined;
  if (auto) tags.push(`    <meta name="google-site-verification" content="${escapeAttr(auto)}" />`);
  if (host === raiz) {
    for (const codigo of VERIFICACAO_RAIZ_GOOGLE_FIXOS) {
      if (!tags.some((t) => t.includes(`"${codigo}"`))) {
        tags.push(`    <meta name="google-site-verification" content="${escapeAttr(codigo)}" />`);
      }
    }
  }
  return tags.join('\n');
}

function escapeAttr(value: string): string {
  return value.replace(/[&<>"']/g, (ch) => {
    switch (ch) {
      case '&': return '&amp;';
      case '<': return '&lt;';
      case '>': return '&gt;';
      case '"': return '&quot;';
      case "'": return '&#39;';
      default: return ch;
    }
  });
}

function injectVerificationTags(html: string, tags: string): string {
  if (!tags) return html;
  // Idempotente: se ja injetou nesta string, nao injeta de novo.
  if (html.includes('google-site-verification') || html.includes('facebook-domain-verification')) {
    // Pode haver tags estaticas no template; mesmo assim concatenamos as dinamicas.
  }
  return html.replace('</head>', `${tags}\n  </head>`);
}

// =====================================================================
// Dominio proprio por loja (ex: diamanteveiculos.com.br)
// ---------------------------------------------------------------------
// A API lista as lojas com dominio ATIVO (HTTPS ja confirmado pelo
// scripts/dominios-proprios.sh). Com isso:
//   - no dominio da loja, "/" vai para o catalogo dela e o sitemap/robots
//     listam so ela, com o proprio dominio;
//   - no endereco da plataforma, /catalogo/{slug} dessa loja redireciona
//     para o dominio, para o Google nao ver o mesmo catalogo em dois lugares.
// Loja sem dominio proprio segue exatamente como antes.
// =====================================================================
const PLATAFORMA_URL = (process.env['PLATFORM_URL'] || 'https://connectveiculos.dev.br').replace(/\/+$/, '');
const DOMINIOS_CACHE_TTL_MS = 60 * 1000;
let dominiosPorHost = new Map<string, string>();
let dominiosPorSlug = new Map<string, string>();
let dominiosExpiresAt = 0;

async function carregarDominios(): Promise<void> {
  const now = Date.now();
  if (now < dominiosExpiresAt) return;
  try {
    const apiBase = process.env['API_BASE_URL'] || 'http://localhost:5219';
    const resp = await fetch(`${apiBase}/api/catalogo/dominios`);
    if (!resp.ok) {
      dominiosExpiresAt = now + 15_000;
      return;
    }
    const lista = (await resp.json()) as Array<{ slug: string; dominio: string }>;
    dominiosPorHost = new Map(lista.map((d) => [d.dominio.toLowerCase(), d.slug.toLowerCase()]));
    dominiosPorSlug = new Map(lista.map((d) => [d.slug.toLowerCase(), d.dominio.toLowerCase()]));
    dominiosExpiresAt = now + DOMINIOS_CACHE_TTL_MS;
  } catch {
    // API fora: mantem o mapa anterior e tenta de novo em 15 s.
    dominiosExpiresAt = now + 15_000;
  }
}

/** Host da requisicao sem porta e sem "www.". */
function hostNu(req: express.Request): string {
  return (req.hostname || '').toLowerCase().replace(/^www\./, '');
}

/** Slug da loja dona do dominio desta requisicao, ou undefined. */
function slugDoDominio(req: express.Request): string | undefined {
  return dominiosPorHost.get(hostNu(req));
}

/**
 * No dominio da propria loja o catalogo fica na raiz: /catalogo/{slug}/veiculo/5?x=1
 * vira /veiculo/5?x=1 e /catalogo/{slug} vira /. A query e' mantida.
 */
function caminhoNoDominio(originalUrl: string, slugNaUrl: string): string {
  const resto = originalUrl.slice(`/catalogo/${slugNaUrl}`.length);
  if (resto === '' || resto === '/') return '/';
  if (resto.startsWith('?')) return `/${resto}`;
  return resto;
}

/** Meta que diz ao Angular no navegador de qual loja e' este dominio. */
function metaLojaDoDominio(slug: string | undefined): string {
  return slug ? `    <meta name="${META_LOJA_DO_DOMINIO}" content="${escapeAttr(slug)}" />` : '';
}

export function app(): express.Express {
  const server = express();
  const serverDistFolder = dirname(fileURLToPath(import.meta.url));
  const browserDistFolder = resolve(serverDistFolder, '../browser');
  const indexHtml = join(serverDistFolder, 'index.server.html');

  const commonEngine = new CommonEngine();

  // Hardening: esconde o header "X-Powered-By: Express" das responses.
  server.disable('x-powered-by');

  // Protecoes que nao dependem de listar cada script/fonte do site (uma CSP
  // completa quebraria Google Fonts, mapa e pixel): ninguem embute o site num
  // iframe de outro dominio, nada de <object>/<embed>, e um <base> injetado nao
  // consegue redirecionar os links relativos para outro host.
  server.use((_req, res, next) => {
    res.set('Content-Security-Policy', "frame-ancestors 'self'; object-src 'none'; base-uri 'self'");
    next();
  });

  // Express atras de nginx — confiar nos headers X-Forwarded-* pra que
  // req.protocol retorne "https" (do X-Forwarded-Proto do nginx) ao
  // inves de "http". Sem isso, sitemap.xml gera <loc>http://...</loc>
  // e o Search Console rejeita por mismatch de protocolo com a
  // propriedade cadastrada (https://).
  server.set('trust proxy', true);

  // Sitemap dinâmico — multi-tenant: itera todos os tenants ativos e
  // gera URLs no formato /catalogo/{tenantSlug} e /catalogo/{tenantSlug}/veiculo/{id}.
  /** Data em yyyy-MM-dd (formato W3C aceito no sitemap), ou null se invalida. */
  const dataSitemap = (valor: unknown): string | null => {
    if (!valor) return null;
    const d = new Date(String(valor));
    return isNaN(d.getTime()) ? null : d.toISOString().slice(0, 10);
  };

  // Redirecionamentos do dominio proprio. Roda antes de tudo (sitemap,
  // estaticos, SSR) para valer em qualquer caminho do catalogo.
  server.use(async (req, res, next) => {
    if (req.method !== 'GET' && req.method !== 'HEAD') return next();
    await carregarDominios();

    const slugHost = slugDoDominio(req);
    const catalogo = req.path.match(/^\/catalogo\/([^/]+)/);

    if (slugHost) {
      // www.loja.com.br -> loja.com.br: um endereco so para o Google.
      if ((req.hostname || '').toLowerCase().startsWith('www.')) {
        return res.redirect(301, `https://${hostNu(req)}${req.originalUrl}`);
      }
      if (catalogo) {
        // Catalogo de OUTRA loja no dominio desta: manda para a plataforma.
        if (catalogo[1].toLowerCase() !== slugHost) {
          return res.redirect(302, `${PLATAFORMA_URL}${req.originalUrl}`);
        }
        // Endereco antigo (/catalogo/{slug}/...) da propria loja: vai para a raiz.
        return res.redirect(301, caminhoNoDominio(req.originalUrl, catalogo[1]));
      }
      return next();
    }

    // Endereco da plataforma, loja que tem dominio ativo: vai para o dominio.
    // Menos dentro de iframe — o "Pre-visualizar" da tela de Lojas enquadra o
    // catalogo na mesma origem e o X-Frame-Options barraria outro dominio.
    if (catalogo && req.get('sec-fetch-dest') !== 'iframe') {
      const dominio = dominiosPorSlug.get(catalogo[1].toLowerCase());
      // 302: se a loja tirar o dominio, o navegador nao pode ter guardado o desvio.
      if (dominio) return res.redirect(302, `https://${dominio}${caminhoNoDominio(req.originalUrl, catalogo[1])}`);
    }
    next();
  });

  // robots.txt com o sitemap do proprio host (plataforma ou dominio da loja).
  let robotsCache: string | null = null;
  server.get('/robots.txt', async (req, res, next) => {
    try {
      if (robotsCache === null) robotsCache = await readFile(join(browserDistFolder, 'robots.txt'), 'utf-8');
      res.set('Content-Type', 'text/plain; charset=utf-8');
      res.set('Cache-Control', 'public, max-age=3600');
      res.send(robotsCache.replace(/^Sitemap:.*$/m, `Sitemap: ${req.protocol}://${req.get('host')}/sitemap.xml`));
    } catch (err) {
      next(err);
    }
  });

  // Cada geracao do sitemap faz 1 + N chamadas a API (uma por loja). Sem cache,
  // repetir GET /sitemap.xml num loop era um jeito barato de ocupar o servidor.
  const sitemapCache = new Map<string, { xml: string; expira: number }>();
  const SITEMAP_TTL_MS = 10 * 60 * 1000;

  server.get('/sitemap.xml', async (req, res) => {
    try {
      const apiBase = process.env['API_BASE_URL'] || 'http://localhost:5219';
      const slugHost = slugDoDominio(req);
      const siteBase = slugHost
        ? `https://${hostNu(req)}`
        : process.env['SITE_BASE_URL'] || `${req.protocol}://${req.get('host')}`;

      const emCache = sitemapCache.get(siteBase);
      if (emCache && emCache.expira > Date.now()) {
        res.set('Content-Type', 'application/xml');
        res.set('Cache-Control', 'public, max-age=3600');
        res.send(emCache.xml);
        return;
      }

      // 1) Lista de tenants: no dominio proprio, so a loja dele; na plataforma,
      //    os tenants publicos menos os que ja tem dominio (entram no sitemap
      //    do proprio dominio — o protocolo nao aceita URL de outro host).
      let tenants: Array<{ slug: string }>;
      if (slugHost) {
        tenants = [{ slug: slugHost }];
      } else {
        const tenantsResp = await fetch(`${apiBase}/api/catalogo/public-tenants`);
        const publicos: Array<{ slug: string; nome: string }> = tenantsResp.ok ? await tenantsResp.json() : [];
        tenants = publicos.filter((t) => !dominiosPorSlug.has(t.slug.toLowerCase()));
      }

      let xml = '<?xml version="1.0" encoding="UTF-8"?>\n';
      xml += '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n';

      // Landing institucional - alvo principal pra busca por "connectveiculos".
      // No dominio da loja, "/" e' o catalogo dela: entra logo abaixo.
      if (!slugHost) {
        xml += `  <url><loc>${siteBase}/</loc><changefreq>weekly</changefreq><priority>1.0</priority></url>\n`;
      }

      // 2) Para cada tenant, busca seu catalogo e gera URLs especificas
      for (const tenant of tenants) {
        try {
          const catResp = await fetch(`${apiBase}/api/catalogo?tenant=${encodeURIComponent(tenant.slug)}`);
          if (!catResp.ok) continue;
          const data = await catResp.json();

          // Catalogo vazio nao entra: uma pagina "nenhum veiculo encontrado"
          // nao serve a quem chega pela busca, e ainda consome rastreamento
          // que deveria ir para as lojas com estoque.
          const veiculos = data.veiculos || [];
          if (veiculos.length === 0) continue;

          // lastmod e' o que o Google usa para decidir o que revisitar;
          // changefreq e priority ele ignora. A listagem muda quando qualquer
          // veiculo muda, entao vale a data mais recente entre eles.
          const datas = veiculos.map((v: any) => dataSitemap(v.veiDtAtualizacao)).filter(Boolean).sort();
          const lastmodLoja = datas.length ? `<lastmod>${datas[datas.length - 1]}</lastmod>` : '';
          // No dominio da loja o catalogo fica na raiz (/ e /veiculo/{id}).
          const prefixo = slugHost ? '' : `/catalogo/${tenant.slug}`;
          xml += `  <url><loc>${siteBase}${prefixo || '/'}</loc>${lastmodLoja}<changefreq>daily</changefreq><priority>0.8</priority></url>\n`;

          for (const v of veiculos) {
            const dt = dataSitemap(v.veiDtAtualizacao);
            const lastmod = dt ? `<lastmod>${dt}</lastmod>` : '';
            xml += `  <url><loc>${siteBase}${prefixo}/veiculo/${v.veiId}</loc>${lastmod}<changefreq>weekly</changefreq><priority>0.9</priority></url>\n`;
          }
        } catch {
          // tenant individual falhou — continua os outros
        }
      }

      xml += '</urlset>';

      // Host inventado vira chave nova: limita o tamanho do mapa.
      if (sitemapCache.size > 200) sitemapCache.clear();
      sitemapCache.set(siteBase, { xml, expira: Date.now() + SITEMAP_TTL_MS });

      res.set('Content-Type', 'application/xml');
      res.set('Cache-Control', 'public, max-age=3600');
      res.send(xml);
    } catch {
      res.status(500).send('Erro ao gerar sitemap');
    }
  });

  // Servir arquivos estaticos
  server.get('**', express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
  }));

  // A landing tambem vai por SSR. Ela estava no ramo estatico e o HTML servido
  // era um <app-root> vazio de 16 KB: aparecia no Google so porque o Googlebot
  // executa JavaScript, e os links do rodape para os catalogos — a unica coisa
  // que aponta para eles no site inteiro — nao existiam para quem nao executa.
  // E a pagina mais importante do dominio; vale o custo de renderizar.
  server.get('/', ssrHandler(commonEngine, indexHtml, browserDistFolder));

  // Rotas do catalogo: SSR (renderiza no servidor com dados).
  // Path-based multi-tenancy: /catalogo/:tenantSlug e variantes.
  server.get('/catalogo', ssrHandler(commonEngine, indexHtml, browserDistFolder));
  server.get('/catalogo/:tenantSlug', ssrHandler(commonEngine, indexHtml, browserDistFolder));
  server.get('/catalogo/:tenantSlug/veiculo/:veiculoId', ssrHandler(commonEngine, indexHtml, browserDistFolder));
  // Veiculo no dominio proprio da loja (o catalogo dela fica na raiz).
  const ssrVeiculoNoDominio = ssrHandler(commonEngine, indexHtml, browserDistFolder);
  server.get('/veiculo/:veiculoId', (req, res, next) =>
    slugDoDominio(req) ? ssrVeiculoNoDominio(req, res, next) : next());

  // Demais rotas: servir o index estático (client-side only).
  // Angular 17+ gera o template CSR como `index.csr.html` (em vez de `index.html`).
  // Lemos o arquivo, injetamos meta tags de verificacao e enviamos a string.
  // Cache do conteudo do arquivo em memoria (template nao muda em runtime).
  let csrTemplateCache: string | null = null;
  server.get('**', async (req, res, next) => {
    try {
      if (csrTemplateCache === null) {
        csrTemplateCache = await readFile(join(browserDistFolder, 'index.csr.html'), 'utf-8');
      }
      const tags = await getVerificationMetaTags(req);
      res.set('Content-Type', 'text/html; charset=utf-8');
      res.send(injectVerificationTags(csrTemplateCache, [tags, metaLojaDoDominio(slugDoDominio(req))].filter(Boolean).join('\n')));
    } catch (err) {
      next(err);
    }
  });

  return server;
}

function ssrHandler(commonEngine: CommonEngine, indexHtml: string, browserDistFolder: string) {
  return (req: express.Request, res: express.Response, next: express.NextFunction) => {
    const { protocol, originalUrl, baseUrl, headers } = req;
    const lojaDoDominio = slugDoDominio(req);

    commonEngine
      .render({
        bootstrap,
        documentFilePath: indexHtml,
        url: `${protocol}://${headers.host}${originalUrl}`,
        publicPath: browserDistFolder,
        providers: [
          { provide: APP_BASE_HREF, useValue: baseUrl },
          // Canonical/og:url com o host real: no dominio proprio da loja, o
          // dominio dela; na plataforma, connectveiculos.dev.br como antes.
          { provide: SITE_ORIGIN, useValue: `${protocol}://${headers.host}` },
          // Loja dona deste dominio ('' na plataforma): o catalogo dela na raiz.
          { provide: LOJA_DO_DOMINIO, useValue: lojaDoDominio ?? '' },
        ],
      })
      .then(async (html) => {
        const tags = await getVerificationMetaTags(req);
        res.send(injectVerificationTags(html, [tags, metaLojaDoDominio(lojaDoDominio)].filter(Boolean).join('\n')));
      })
      .catch((err) => next(err));
  };
}

function run(): void {
  const port = process.env['PORT'] || 4000;

  const server = app();
  server.listen(port, () => {
    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

run();
