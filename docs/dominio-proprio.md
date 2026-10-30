# Domínio próprio da loja

A loja que tiver um domínio (ex.: `diamanteveiculos.com.br`) pode ter o catálogo nele.
É **opcional**: sem domínio, o catálogo segue em `connectveiculos.dev.br/catalogo/<loja>`.

Tudo continua no mesmo sistema e no mesmo banco. Veículos, fotos, favoritos, test drives,
leads e WhatsApp aparecem igual nos dois endereços. O painel administrativo continua em
`connectveiculos.dev.br`; só o catálogo público ganha o endereço da loja.

## Para cada loja nova (sem programar)

1. **Primeiro o DNS.** No registro do domínio (Registro.br, GoDaddy, Hostinger, Cloudflare…), criar:
   - registro **A** de `dominio.com.br` → `136.248.77.154`;
   - registro **A** de `www.dominio.com.br` → `136.248.77.154` (opcional, recomendado).
   - No Cloudflare, deixar a nuvem **cinza** ("DNS only").
2. **Depois o cadastro:** painel da loja → Integrações → Domínio próprio → Cadastrar.
   O sistema só aceita domínio com formato válido, fora da plataforma e que **já aponta
   para o servidor** — isso prova que o domínio existe, que vai responder e que quem
   cadastra controla o domínio. Se ainda não aponta, a tela diz o que falta (a mudança
   de DNS pode levar algumas horas).
3. **Esperar.** Em até 10 minutos o servidor gera o certificado HTTPS, configura o nginx
   e o card passa para **Ativo**.
4. **Google:** cadastrar `dominio.com.br` no Search Console, escolher "Tag HTML", colar o
   código em Integrações → Marketing no Google (a tag sai em qualquer endereço da loja)
   e enviar `https://dominio.com.br/sitemap.xml`. Criar ou atualizar o **Perfil da Empresa no Google**
   com o site.

**Depois de cadastrado, o domínio não se edita.** Para usar outro: **Remover domínio**
(pede confirmação; o catálogo volta para `connectveiculos.dev.br`) e cadastrar o novo,
que passa pela mesma validação. A API também recusa a troca direta.

## O que muda quando fica Ativo

- `dominio.com.br/` é o catálogo da loja e `dominio.com.br/veiculo/123` o veículo, sem
  `/catalogo/<loja>` no endereço; `www.` redireciona para o endereço sem `www`.
- `connectveiculos.dev.br/catalogo/<loja>/...` redireciona (302) para o domínio, já no
  caminho novo (o "Pré-visualizar" da tela de Lojas não redireciona). No próprio domínio,
  `/catalogo/<loja>/...` redireciona (301) para a raiz: links antigos continuam valendo.
- No navegador, o Angular sabe de qual loja é o domínio pela meta `cv-loja-dominio` que o
  `server.ts` injeta (no SSR, pelo token `LOJA_DO_DOMINIO`).
- Links dos posts do Facebook, feeds de catálogo (Facebook/Google), Google Merchant e
  e-mails passam a usar o domínio (`Loja.LojUrlCatalogo`).
- Sitemap, `robots.txt`, canonical e og:url com o domínio. O sitemap da plataforma
  deixa de listar essa loja (o Google não aceita URL de outro host no sitemap).

Mudanças aparecem no site em até 2 minutos (cache de 60 s na API e 60 s no SSR).

## Como funciona (técnico)

| Peça | Onde |
|---|---|
| Domínio e data de ativação do tenant | `Tenants.TenDominioProprio` / `TenDominioAtivoEm` (master) |
| Normalização e validação | `Core/Catalogo/DominioProprio.cs` |
| Tela da loja | `api/integracoes/dominio-proprio` (`DominioProprioController`) |
| Host → tenant | `TenantResolutionMiddleware` (domínio vence a regra de subdomínio) |
| Lista para o servidor e ativação | `api/admin/tenants/dominios` e `.../{slug}/dominio/ativar` (`X-Admin-Token`) |
| Lista pública para o SSR | `api/catalogo/dominios` (só domínios ativos) |
| Redirecionamentos, sitemap, robots | `front-end/ConnectVeiculos.Web/server.ts` |
| Certificado + nginx | `scripts/dominios-proprios.sh` → `nginx/dominios/<dominio>.conf` |

O script roda na VM pelo cron, de 5 em 5 minutos:

```
*/5 * * * * /home/ubuntu/ConnectVeiculos/scripts/dominios-proprios.sh >> /home/ubuntu/backups/dominios.log 2>&1
```

Ele também recarrega o nginx quando o certbot renova **qualquer** certificado (inclusive
o da plataforma). Antes disso não havia hook: o nginx seguia com o certificado antigo
em memória até alguém reiniciá-lo.

Se `nginx -t` falhar depois de uma mudança, o script desfaz exatamente o que alterou
naquele ciclo, para um arquivo quebrado nunca ficar na pasta (derrubaria o nginx inteiro
no próximo restart). Log em `/home/ubuntu/backups/dominios.log`.

**Domínio removido na tela:** o `.conf` sai do nginx e os links voltam para
`connectveiculos.dev.br`. O certificado fica em `/etc/letsencrypt`; para parar de tentar
renová-lo: `sudo certbot delete --cert-name dominio.com.br`.
