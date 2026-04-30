import { test, expect } from '@playwright/test';

const API = 'http://localhost:5219';

test.describe('API publica (sem auth)', () => {
  test('GET /api/catalogo retorna estrutura esperada', async ({ request }) => {
    const r = await request.get(`${API}/api/catalogo`);
    expect(r.ok()).toBeTruthy();
    const body = await r.json();
    expect(body).toHaveProperty('veiculos');
    expect(body).toHaveProperty('lojas');
    expect(body).toHaveProperty('total');
  });

  test('GET /api/feed/google retorna feed XML', async ({ request }) => {
    const r = await request.get(`${API}/api/feed/google`);
    expect(r.ok()).toBeTruthy();
    const text = await r.text();
    expect(text).toMatch(/<\?xml|<rss|<feed/);
  });

  test('GET /api/feed/facebook retorna 200', async ({ request }) => {
    const r = await request.get(`${API}/api/feed/facebook`);
    expect(r.ok()).toBeTruthy();
  });
});

test.describe('API leads (cadastro)', () => {
  test('POST /api/leads aceita ou rejeita com erro tratado', async ({ request }) => {
    const r = await request.post(`${API}/api/leads`, {
      data: {
        veiculoId: null,
        lojaId: null,
        nomeCliente: 'Teste E2E ' + Date.now(),
        telefone: '11999999999',
        email: `teste-${Date.now()}@e2e.local`,
        origem: 'WHATSAPP_CATALOGO',
        observacao: 'Lead criado pelo teste E2E'
      }
    });
    expect([200, 201, 401, 400]).toContain(r.status());
  });
});
