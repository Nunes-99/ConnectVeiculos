import { APP_BASE_HREF } from '@angular/common';
import { CommonEngine } from '@angular/ssr';
import express from 'express';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import bootstrap from './src/main.server';

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
let verificationCacheTags = '';
let verificationCacheExpiresAt = 0;

async function getVerificationMetaTags(): Promise<string> {
  const now = Date.now();
  if (now < verificationCacheExpiresAt) return verificationCacheTags;

  try {
    const apiBase = process.env['API_BASE_URL'] || 'http://localhost:5219';
    const resp = await fetch(`${apiBase}/api/integracoes/verification-codes`);
    if (!resp.ok) {
      verificationCacheExpiresAt = now + 30_000; // backoff curto em caso de erro
      return verificationCacheTags;
    }
    const data = (await resp.json()) as { google?: string[]; facebook?: string[] };
    const tags: string[] = [];
    for (const code of data.google || []) {
      tags.push(`    <meta name="google-site-verification" content="${escapeAttr(code)}" />`);
    }
    for (const code of data.facebook || []) {
      tags.push(`    <meta name="facebook-domain-verification" content="${escapeAttr(code)}" />`);
    }
    verificationCacheTags = tags.join('\n');
    verificationCacheExpiresAt = now + VERIFICATION_CACHE_TTL_MS;
    return verificationCacheTags;
  } catch {
    verificationCacheExpiresAt = now + 30_000;
    return verificationCacheTags;
  }
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

export function app(): express.Express {
  const server = express();
  const serverDistFolder = dirname(fileURLToPath(import.meta.url));
  const browserDistFolder = resolve(serverDistFolder, '../browser');
  const indexHtml = join(serverDistFolder, 'index.server.html');

  const commonEngine = new CommonEngine();

  // Hardening: esconde o header "X-Powered-By: Express" das responses.
  server.disable('x-powered-by');

  // Sitemap dinâmico — multi-tenant: itera todos os tenants ativos e
  // gera URLs no formato /catalogo/{tenantSlug} e /catalogo/{tenantSlug}/veiculo/{id}.
  server.get('/sitemap.xml', async (req, res) => {
    try {
      const apiBase = process.env['API_BASE_URL'] || 'http://localhost:5219';
      const siteBase = process.env['SITE_BASE_URL'] || `${req.protocol}://${req.get('host')}`;

      // 1) Lista de tenants publicos
      const tenantsResp = await fetch(`${apiBase}/api/catalogo/public-tenants`);
      const tenants: Array<{ slug: string; nome: string }> = tenantsResp.ok ? await tenantsResp.json() : [];

      let xml = '<?xml version="1.0" encoding="UTF-8"?>\n';
      xml += '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n';

      // 2) Para cada tenant, busca seu catalogo e gera URLs especificas
      for (const tenant of tenants) {
        try {
          const catResp = await fetch(`${apiBase}/api/catalogo?tenant=${encodeURIComponent(tenant.slug)}`);
          if (!catResp.ok) continue;
          const data = await catResp.json();

          xml += `  <url><loc>${siteBase}/catalogo/${tenant.slug}</loc><changefreq>daily</changefreq><priority>0.8</priority></url>\n`;

          for (const v of data.veiculos || []) {
            xml += `  <url><loc>${siteBase}/catalogo/${tenant.slug}/veiculo/${v.veiId}</loc><changefreq>weekly</changefreq><priority>0.9</priority></url>\n`;
          }
        } catch {
          // tenant individual falhou — continua os outros
        }
      }

      xml += '</urlset>';

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

  // Rotas do catalogo: SSR (renderiza no servidor com dados).
  // Path-based multi-tenancy: /catalogo/:tenantSlug e variantes.
  server.get('/catalogo', ssrHandler(commonEngine, indexHtml, browserDistFolder));
  server.get('/catalogo/:tenantSlug', ssrHandler(commonEngine, indexHtml, browserDistFolder));
  server.get('/catalogo/:tenantSlug/veiculo/:veiculoId', ssrHandler(commonEngine, indexHtml, browserDistFolder));

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
      const tags = await getVerificationMetaTags();
      res.set('Content-Type', 'text/html; charset=utf-8');
      res.send(injectVerificationTags(csrTemplateCache, tags));
    } catch (err) {
      next(err);
    }
  });

  return server;
}

function ssrHandler(commonEngine: CommonEngine, indexHtml: string, browserDistFolder: string) {
  return (req: express.Request, res: express.Response, next: express.NextFunction) => {
    const { protocol, originalUrl, baseUrl, headers } = req;

    commonEngine
      .render({
        bootstrap,
        documentFilePath: indexHtml,
        url: `${protocol}://${headers.host}${originalUrl}`,
        publicPath: browserDistFolder,
        providers: [{ provide: APP_BASE_HREF, useValue: baseUrl }],
      })
      .then(async (html) => {
        const tags = await getVerificationMetaTags();
        res.send(injectVerificationTags(html, tags));
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
