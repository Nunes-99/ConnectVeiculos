import { APP_BASE_HREF } from '@angular/common';
import { CommonEngine } from '@angular/ssr';
import express from 'express';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';
import bootstrap from './src/main.server';

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
  server.get('**', (req, res) => {
    res.sendFile(join(browserDistFolder, 'index.csr.html'));
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
      .then((html) => res.send(html))
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
