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

  server.set('view engine', 'html');
  server.set('views', browserDistFolder);

  // Sitemap dinâmico
  server.get('/sitemap.xml', async (req, res) => {
    try {
      const apiBase = process.env['API_BASE_URL'] || 'http://localhost:5219';
      const siteBase = process.env['SITE_BASE_URL'] || `${req.protocol}://${req.get('host')}`;

      const response = await fetch(`${apiBase}/api/catalogo`);
      const data = await response.json();

      let xml = '<?xml version="1.0" encoding="UTF-8"?>\n';
      xml += '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n';

      xml += `  <url><loc>${siteBase}/catalogo</loc><changefreq>daily</changefreq><priority>0.8</priority></url>\n`;

      for (const loja of data.lojas || []) {
        const slug = loja.lojSlug || loja.lojId;
        xml += `  <url><loc>${siteBase}/catalogo/${slug}</loc><changefreq>daily</changefreq><priority>0.7</priority></url>\n`;
      }

      for (const v of data.veiculos || []) {
        xml += `  <url><loc>${siteBase}/catalogo/veiculo/${v.veiId}</loc><changefreq>weekly</changefreq><priority>0.9</priority></url>\n`;
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

  // Rotas do catalogo: SSR (renderiza no servidor com dados)
  server.get('/catalogo', ssrHandler(commonEngine, indexHtml, browserDistFolder));
  server.get('/catalogo/:lojaId', ssrHandler(commonEngine, indexHtml, browserDistFolder));
  server.get('/catalogo/:lojaId/veiculo/:veiculoId', ssrHandler(commonEngine, indexHtml, browserDistFolder));

  // Demais rotas: servir o index.html estático (client-side only)
  server.get('**', (req, res) => {
    res.sendFile(join(browserDistFolder, 'index.html'));
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
