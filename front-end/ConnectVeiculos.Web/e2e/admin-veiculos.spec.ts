import { test, expect, request as pwRequest } from '@playwright/test';

const API = 'http://localhost:5219';

let token = '';

test.beforeAll(async () => {
  // O retry abaixo espera 60s quando toma 429, mas o timeout padrao do hook e
  // 30s — sem isso o hook estoura antes de terminar a espera e a suite inteira
  // falha em vez de cair no test.skip('Sem token').
  test.setTimeout(150_000);
  const ctx = await pwRequest.newContext();
  for (let attempt = 0; attempt < 3; attempt++) {
    const r = await ctx.post(`${API}/api/auth/login`, {
      data: { email: 'admin@connectveiculos.com.br', senha: 'admin123' }
    });
    if (r.ok()) { token = (await r.json()).token; break; }
    if (r.status() === 429) await new Promise(res => setTimeout(res, 60_000));
  }
  await ctx.dispose();
});

test.describe('Admin (autenticado via API)', () => {
  test('GET /api/dashboard/lucro retorna estrutura esperada', async ({ request }) => {
    test.skip(!token, 'Sem token (rate limited)');
    const r = await request.get(`${API}/api/dashboard/lucro`, {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(r.ok()).toBeTruthy();
    const body = await r.json();
    expect(body).toHaveProperty('receita');
    expect(body).toHaveProperty('lucroLiquido');
    expect(body).toHaveProperty('topVeiculosRentaveis');
    expect(body).toHaveProperty('lucroPorMes');
  });

  test('GET /api/lojas retorna lista', async ({ request }) => {
    test.skip(!token, 'Sem token');
    const r = await request.get(`${API}/api/lojas`, { headers: { Authorization: `Bearer ${token}` } });
    expect(r.ok()).toBeTruthy();
  });

  test('GET /api/veiculos-documentos/vencendo retorna 200', async ({ request }) => {
    test.skip(!token, 'Sem token');
    const r = await request.get(`${API}/api/veiculos-documentos/vencendo?diasAFrente=30`, {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(r.ok()).toBeTruthy();
  });

  test('GET /api/integracoes/whatsapp/status', async ({ request }) => {
    test.skip(!token, 'Sem token');
    const r = await request.get(`${API}/api/integracoes/whatsapp/status`, {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(r.ok()).toBeTruthy();
    const body = await r.json();
    expect(body).toHaveProperty('configurado');
  });

  // Removido: /api/detran/status nao existe mais. A consulta ao Detran virou
  // redirect pro site oficial do estado, resolvido no front
  // (shared/utils/detran-links.util.ts) — nao ha endpoint pra testar aqui.

  test('GET /api/integracoes/mercadolivre/status', async ({ request }) => {
    test.skip(!token, 'Sem token');
    const r = await request.get(`${API}/api/integracoes/mercadolivre/status`, {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(r.ok()).toBeTruthy();
    const body = await r.json();
    expect(body).toHaveProperty('conectado');
  });
});
