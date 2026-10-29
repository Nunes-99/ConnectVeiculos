# Deploy - ConnectVeiculos

O sistema roda **só em produção**: https://connectveiculos.dev.br, numa VM da
Oracle Cloud (Always Free, x86, 2 núcleos, 954 MB de RAM). Não há homologação.
Vercel, Render e Supabase não são mais usados.

Como a VM foi criada e configurada do zero: `DEPLOY_ORACLE.md`.

## Arquitetura

```
              ┌──────────────── VM Oracle (136.248.77.154) ────────────────┐
 internet ──► │ nginx (80/443) ──► frontend-ssr (Angular SSR, :4000)       │
              │        └─────────► backend-cliente (.NET 8, :8080)         │
              │                       └─ data/*.db (SQLite, 1 por loja)    │
              └────────────────────────────────────────────────────────────┘
```

- Repositório na VM: `/home/ubuntu/ConnectVeiculos` (branch `fix/auditoria-completa`,
  que só avança por fast-forward a partir do `master`).
- Configuração: `.env` na raiz do repositório na VM (fora do git).
- Dados: `data/` (bancos), `uploads/cliente/` (fotos), `dpkeys/` (chaves que
  cifram os tokens OAuth — **não apagar**, senão todas as integrações desconectam).

## Publicar uma versão

1. Commit e push no `master`.
2. O GitHub Actions roda o **CI** (build e testes) e o **Imagens**, que constrói
   as imagens Docker do back-end e do front-end e envia para o
   `ghcr.io/nunes-99/connectveiculos-*`. Conferir se as duas ficaram verdes:
   ```bash
   gh run list --limit 4
   ```
3. Publicar (no PC, na raiz do repositório):
   ```bash
   bash scripts/publicar.sh
   ```
   Na VM, em segundo plano: backup dos bancos em `/home/ubuntu/backups/antes-publicar-<data>`,
   `git merge --ff-only origin/master`, download das imagens do commit, troca dos
   containers e limpeza das imagens antigas. A conexão SSH pode cair que nada se perde.
4. Conferir o resultado:
   ```bash
   ssh -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154 tail -5 /home/ubuntu/publicar.log
   ```
   A última linha é `fim: ...` (deu certo) ou `ERRO: ...` (parou antes de trocar os containers).

O site fica fora do ar só os segundos da troca de container. Antes, a imagem era
construída na própria VM e o site ficava lento por uns 13 minutos.

**Publicar é sempre manual.** O workflow Imagens só deixa a imagem pronta, não mexe em produção.

### Plano B: construir na VM

Se o GitHub estiver fora ou a imagem não existir:

```bash
cd /home/ubuntu/ConnectVeiculos
sudo docker compose up -d --build backend-cliente     # ~12 min
sudo docker compose up -d --build frontend-ssr        # ~13 min, site lento enquanto isso
```

O nginx só muda quando `nginx/` muda: `sudo docker compose up -d --build nginx`.

## Voltar para a versão anterior

As imagens ficam marcadas com o commit. Para voltar o código e os containers:

```bash
cd /home/ubuntu/ConnectVeiculos
git log --oneline -5                          # escolher o commit
git reset --hard <commit>
for n in backend frontend; do
  sudo docker pull ghcr.io/nunes-99/connectveiculos-$n:<commit-completo>
  sudo docker tag  ghcr.io/nunes-99/connectveiculos-$n:<commit-completo> ghcr.io/nunes-99/connectveiculos-$n:latest
done
sudo docker compose up -d --no-build backend-cliente frontend-ssr
```

Se uma migração estragou dados, restaurar a pasta `data/` do backup feito pelo
`publicar.sh` (com o back-end parado).

## Variáveis do `.env`

As principais (lista completa e padrões em `docker-compose.yml`):

| Variável | Para quê |
|---|---|
| `JWT_SECRET_KEY_CLIENTE` | Assinatura do login |
| `ADMIN_API_TOKEN` | Rotas administrativas (criar/excluir loja) |
| `META_APP_ID`, `META_APP_SECRET`, `META_INSTAGRAM_ENABLED` | App da Meta (Facebook, Instagram, stories) |
| `ML_APP_ID`, `ML_CLIENT_SECRET` | Mercado Livre |
| `WHATSAPP_VERIFY_TOKEN` | Verificação do webhook do WhatsApp |
| `WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_PHONE_ID` | **Deixar vazias**: se preenchidas, valem para todas as lojas |
| `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY` | Notificações push |
| `GEMINI_API_KEY` | Leitura do CRLV por foto |
| `SEARCH_CONSOLE_CLIENT_ID`, `SEARCH_CONSOLE_CLIENT_SECRET`, `SEARCH_CONSOLE_REFRESH_TOKEN` | Reenvio automático do sitemap ao Google (opcional; vazio = desligado) |

Depois de mudar o `.env`: `sudo docker compose up -d backend-cliente` (recria o container com os valores novos).

## Rotina na VM

- Backup diário dos bancos às 03:30 (`scripts/backup-sqlite.sh`, cron do root).
- Renovação do certificado HTTPS pelo certbot (`scripts/check-cert-expiry.sh`).
- Domínios próprios das lojas a cada 5 min (`scripts/dominios-proprios.sh`).

Detalhes, logs e monitoramento: `DEPLOY_ORACLE.md`, seção "Monitoring / Status".
Lojas (criar, excluir, domínio próprio): `GUIA-OPERACAO-MULTI-TENANT.md` e `docs/dominio-proprio.md`.
