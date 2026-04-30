import { test, expect } from '@playwright/test';

test.describe('Catalogo publico', () => {
  test('abre /catalogo sem login', async ({ page }) => {
    await page.goto('/catalogo');
    // Deve mostrar a pagina de catalogo (nao redirect para login)
    await expect(page).not.toHaveURL(/login/);
  });

  test('renderiza pagina do catalogo (heading, card ou empty state)', async ({ page }) => {
    await page.goto('/catalogo');
    // Aguarda QUALQUER elemento que indique que a pagina carregou (catalogo pode estar vazio ou com cards)
    const algumElemento = page.locator('h1, h2, h3, .card, .veiculo-card, .empty-state, .catalogo-empty, app-catalogo, .filtros, [class*="catalogo"]');
    await expect(algumElemento.first()).toBeVisible({ timeout: 15000 });
  });
});
