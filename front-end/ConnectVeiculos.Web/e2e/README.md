# Testes E2E - ConnectVeiculos

Smoke tests com [Playwright](https://playwright.dev/).

## Setup (uma vez)

```bash
npm install
npm run test:e2e:install   # baixa browsers do Playwright
```

## Rodar

Pre-requisito: o backend `.NET` precisa estar rodando em `http://localhost:5219` com o admin
seeded (`admin@connectveiculos.com.br` / `admin123`).

```bash
# headless (CI)
npm run test:e2e

# UI interativa (debug)
npm run test:e2e:ui

# contra um ambiente publicado
E2E_BASE_URL=https://connectveiculos.dev.br npm run test:e2e
```

O `playwright.config.ts` sobe o `ng serve` automaticamente (porta 4200) se a base URL nao for setada.

## Cobertura atual

- Login: tela carrega, valida credenciais invalidas, aceita admin valido
- Catalogo publico: acessivel sem auth

Adicionar mais cenarios em `e2e/*.spec.ts`.
