# Guia: Como fazer o ConnectVeiculos aparecer no Google

**Site:** connectveiculos.dev.br
**Objetivo:** Que pesquisas como "Fiat Argo 2007" tragam o catalogo do site nos resultados do Google.

---

## Passo 1: Deploy do SSR (Server-Side Rendering)

O site atualmente roda como SPA (Single Page Application). Para o Google indexar o conteudo dos veiculos, e necessario rodar o servidor Node.js que renderiza o HTML completo.

### 1.1 Build do projeto

```bash
cd front-end/ConnectVeiculos.Web
npm install
ng build
```

Isso gera duas pastas em `dist/connect-veiculos.web/`:
- `browser/` - arquivos estaticos (CSS, JS, imagens)
- `server/` - servidor Node.js com SSR

### 1.2 Configurar variaveis de ambiente

Crie ou configure as seguintes variaveis no servidor:

| Variavel | Valor | Descricao |
|----------|-------|-----------|
| `PORT` | `4000` (ou a porta desejada) | Porta do servidor Node |
| `API_BASE_URL` | `http://localhost:5219` (ou URL interna da API .NET) | URL que o servidor Node usa para chamar a API |
| `SITE_BASE_URL` | `https://connectveiculos.dev.br` | URL publica do site (usada no sitemap) |

### 1.3 Executar o servidor

```bash
node dist/connect-veiculos.web/server/server.mjs
```

### 1.4 Configurar proxy reverso (Nginx ou similar)

O Nginx deve apontar para o servidor Node na porta configurada:

```nginx
server {
    listen 80;
    server_name connectveiculos.dev.br;

    location / {
        proxy_pass http://localhost:4000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

> **Nota:** Se usa Oracle Cloud, configure o Nginx ou outro proxy na VM para apontar ao Node.js.

### 1.5 Verificar se o SSR esta funcionando

Acesse pelo terminal:

```bash
curl https://connectveiculos.dev.br/catalogo
```

O HTML retornado deve conter os dados dos veiculos (nomes, precos, etc.) diretamente no codigo-fonte, e nao apenas `<app-root></app-root>`.

---

## Passo 2: Cadastrar no Google Search Console

### 2.1 Acessar o Google Search Console

1. Acesse: https://search.google.com/search-console
2. Faca login com sua conta Google

### 2.2 Adicionar propriedade

1. Clique em **"Adicionar propriedade"**
2. Escolha **"Prefixo do URL"**
3. Digite: `https://connectveiculos.dev.br`
4. Clique em **"Continuar"**

### 2.3 Verificar propriedade

O Google oferece varias formas de verificacao. A mais simples:

**Opcao A - Tag HTML (recomendado):**
1. O Google vai fornecer uma meta tag tipo:
   ```html
   <meta name="google-site-verification" content="CODIGO_AQUI" />
   ```
2. Adicione essa tag no arquivo `src/index.html` dentro do `<head>`
3. Faca o build e deploy novamente
4. Volte ao Search Console e clique em **"Verificar"**

**Opcao B - Arquivo HTML:**
1. Baixe o arquivo HTML de verificacao fornecido pelo Google
2. Coloque na pasta `public/` do projeto Angular
3. Faca o build e deploy
4. Volte ao Search Console e clique em **"Verificar"**

### 2.4 Submeter o Sitemap

1. No menu lateral, clique em **"Sitemaps"**
2. No campo "Adicionar novo sitemap", digite: `sitemap.xml`
3. Clique em **"Enviar"**
4. O Google vai processar e mostrar quantas URLs foram encontradas

O sitemap e gerado automaticamente em `https://connectveiculos.dev.br/sitemap.xml` e inclui todas as lojas e veiculos disponiveis no catalogo.

---

## Passo 3: Solicitar indexacao das paginas principais

### 3.1 Inspecionar URLs

1. No Search Console, use a barra de pesquisa no topo
2. Digite a URL do catalogo: `https://connectveiculos.dev.br/catalogo`
3. Clique em **"Solicitar indexacao"**
4. Repita para URLs importantes como:
   - `https://connectveiculos.dev.br/catalogo/slug-da-loja`
   - URLs de veiculos especificos que voce quer indexar primeiro

### 3.2 Verificar renderizacao

1. Na inspecao de URL, clique em **"Testar URL ativa"**
2. Depois clique em **"Ver pagina testada"** > **"Captura de tela"**
3. Confirme que a pagina aparece com o conteudo dos veiculos renderizado

---

## Passo 4: Otimizacoes adicionais

### 4.1 HTTPS

- Certifique-se de que o site usa HTTPS (certificado SSL)
- O Google prioriza sites com HTTPS
- Se ainda nao tem, use o **Let's Encrypt** (gratuito):
  ```bash
  sudo certbot --nginx -d connectveiculos.dev.br
  ```

### 4.2 Velocidade do site

- Teste em: https://pagespeed.web.dev/
- Digite: `https://connectveiculos.dev.br/catalogo`
- O SSR ja melhora significativamente a pontuacao

### 4.3 Google Meu Negocio (opcional mas recomendado)

Se a loja tem endereco fisico:
1. Acesse: https://business.google.com/
2. Cadastre a empresa com endereco, telefone, horario
3. Adicione o link do catalogo no perfil
4. Isso ajuda a aparecer em pesquisas locais ("loja de carros em [cidade]")

---

## Passo 5: Acompanhar resultados

### 5.1 No Google Search Console

Acompanhe semanalmente:
- **Desempenho** - quantas vezes o site apareceu e quantos cliques recebeu
- **Cobertura** - quantas paginas foram indexadas
- **Experiencia na pagina** - metricas de velocidade e usabilidade

### 5.2 Pesquisar no Google

Apos 1-2 semanas, teste pesquisas como:
- `site:connectveiculos.dev.br` - mostra todas as paginas indexadas
- `site:connectveiculos.dev.br fiat argo` - busca especifica dentro do site
- `"connectveiculos" fiat argo 2007` - busca com nome do site

---

## Cronograma esperado

| Periodo | O que esperar |
|---------|--------------|
| Dia 1 | Deploy do SSR + cadastro no Search Console |
| 1-3 dias | Google descobre o sitemap e comeca a rastrear |
| 1-2 semanas | Primeiras paginas indexadas |
| 2-4 semanas | Maioria das paginas do catalogo indexadas |
| 1-3 meses | Posicionamento comeca a estabilizar |

---

## Dicas para melhorar o posicionamento

1. **Conteudo unico por veiculo** - Preencha o campo "Observacao" com descricoes detalhadas (opcionais, estado do veiculo, diferenciais)
2. **Imagens de qualidade** - Veiculos com boas fotos tem melhor engajamento
3. **Manter catalogo atualizado** - Remover veiculos vendidos e adicionar novos regularmente sinaliza ao Google que o site e ativo
4. **Compartilhar links** - Divulgar links do catalogo em redes sociais gera trafego e backlinks
5. **Nome da loja na URL** - O slug da loja ja esta configurado (ex: `/catalogo/auto-center-sp`)

---

## Resumo dos recursos de SEO implementados

| Recurso | Status |
|---------|--------|
| Server-Side Rendering (SSR) | Implementado |
| Meta tags dinamicas (title, description) | Implementado |
| Open Graph (compartilhamento redes sociais) | Implementado |
| Twitter Cards | Implementado |
| JSON-LD / Schema.org (dados estruturados) | Implementado |
| Sitemap XML dinamico | Implementado |
| robots.txt | Implementado |
| URLs amigaveis (slug por loja) | Implementado |
