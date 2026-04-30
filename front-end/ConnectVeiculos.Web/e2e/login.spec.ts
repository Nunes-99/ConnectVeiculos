import { test, expect } from '@playwright/test';

test.describe('Login', () => {
  test('redireciona / nao autenticado para /login', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/.*login/);
  });

  test('exibe formulario de login', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByRole('textbox').first()).toBeVisible();
  });

  test('rejeita credenciais invalidas', async ({ page }) => {
    await page.goto('/login');
    const inputs = page.getByRole('textbox');
    await inputs.first().fill('errado@x.com');
    await page.locator('input[type=password]').fill('errado');
    await page.getByRole('button', { name: /entrar/i }).click();
    // Deve continuar em /login (nao avanca)
    await expect(page).toHaveURL(/.*login/);
  });

  test('POST /api/auth/login admin retorna token JWT', async ({ request }) => {
    const r = await request.post('http://localhost:5219/api/auth/login', {
      data: { email: 'admin@connectveiculos.com.br', senha: 'admin123' }
    });
    if (r.status() === 429) test.skip(true, 'Rate limited');
    expect(r.ok()).toBeTruthy();
    const body = await r.json();
    expect(body).toHaveProperty('token');
    expect(body.token).toMatch(/^eyJ/);
  });

  test('POST /api/auth/login com senha errada retorna 401/400/429', async ({ request }) => {
    const r = await request.post('http://localhost:5219/api/auth/login', {
      data: { email: 'admin@connectveiculos.com.br', senha: 'errado' }
    });
    expect([400, 401, 429]).toContain(r.status());
  });
});
