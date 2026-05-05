# Deploy no Oracle Cloud Free Tier

## Pre-requisitos
- Conta Oracle Cloud (gratis: https://cloud.oracle.com/free)
- Git instalado

## Passo 1: Criar VM na Oracle Cloud
1. Acesse cloud.oracle.com > Compute > Instances > Create Instance
2. Escolha "Always Free Eligible" (ARM Ampere A1)
3. Shape: VM.Standard.A1.Flex (1 OCPU, 6GB RAM)
4. Image: Ubuntu 22.04
5. Gere e baixe a SSH key
6. Crie a instancia

## Passo 2: Configurar a VM
```bash
# Conectar via SSH
ssh -i sua-chave.key ubuntu@IP_DA_VM

# Instalar Docker
sudo apt update && sudo apt upgrade -y
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER
sudo apt install docker-compose-plugin -y

# Logout e login novamente para aplicar grupo docker
exit
ssh -i sua-chave.key ubuntu@IP_DA_VM
```

## Passo 3: Deploy da aplicacao
```bash
# Clonar o repositorio
git clone SEU_REPOSITORIO connectveiculos
cd connectveiculos

# Criar diretorio de dados
mkdir -p data uploads

# Configurar variaveis de ambiente
cp .env.example .env
nano .env  # Altere a JWT_SECRET_KEY

# Build e iniciar
docker compose up -d --build

# Verificar se esta rodando
docker compose ps
docker compose logs -f
```

## Passo 4: Abrir portas no Oracle Cloud
1. Acesse: Networking > Virtual Cloud Networks > sua VCN
2. Clique em "Security Lists" > "Default Security List"
3. Adicione Ingress Rules:
   - Source: 0.0.0.0/0, Protocol: TCP, Port: 80
   - Source: 0.0.0.0/0, Protocol: TCP, Port: 443

4. No Ubuntu, libere o firewall:
```bash
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

## Passo 5: Configurar dominio (opcional)
1. Compre um dominio (ex: connectveiculos.com.br)
2. No painel DNS, adicione um registro A apontando para o IP da VM
3. Instale SSL com Certbot:
```bash
sudo apt install certbot python3-certbot-nginx -y
sudo certbot --nginx -d seudominio.com.br
```

## Criar novo cliente
```bash
./scripts/criar-cliente.sh "Auto Center SP" "admin@autocenter.com" "senha123"
```

## Comandos uteis
```bash
# Ver logs
docker compose logs -f

# Reiniciar
docker compose restart

# Atualizar
git pull
docker compose up -d --build

# Backup manual (alem do cron diario)
sudo bash scripts/backup-sqlite.sh
```

## Monitoring / Status

### Health check publico
- `GET https://<seu-dominio>/health` → `{"status":"healthy"}` (200 OK)
- Apenas esse endpoint eh exposto. Os endpoints detalhados ficam internos:
  - `/health/ready` (todas as checks: memoria, FIPE, etc)
  - `/health/db`
  - `/health/external`

Pra acessar os internos via VM:
```bash
sudo docker exec <nginx-container> wget -qO- http://backend-cliente:8080/health/ready
```

### Cron jobs ativos na VM (root crontab)
| Schedule | Script | Acao |
|---|---|---|
| `30 3 * * *` | `scripts/backup-sqlite.sh` | Backup diario do SQLite (retencao 7 dias) |
| `0 8 * * *` | `scripts/check-cert-expiry.sh` | Alerta no Discord se cert SSL < 7 dias |

### Renovacao automatica do cert SSL
- `certbot.timer` (systemd) renova 2x ao dia automaticamente.
- Se a renovacao falhar silenciosamente, `check-cert-expiry.sh` avisa via Discord webhook
  pelo menos 7 dias antes do vencimento.
- DISCORD_WEBHOOK_URL fica no `.env` da VM (nao no repo).

### Monitoring externo (UptimeRobot)
- Plano free, 50 monitores, check 5/5min, alerta por email + Discord webhook.
- Configure um monitor HTTP apontando para `https://<seu-dominio>/health` com keyword
  `healthy` (protege contra falso positivo se backend cair mas frontend responder 200).
- Status page publica fica em `stats.uptimerobot.com/<id>` apos criada.

### Logs operacionais na VM
- `/home/ubuntu/backups/backup.log` → output do backup diario
- `/home/ubuntu/backups/cert-check.log` → output do check de cert
- `docker compose logs <servico>` → logs runtime dos containers

## Multi-tenant

O sistema serve N clientes na mesma VM, cada um com banco isolado.
Para detalhes arquiteturais ver [MULTI_TENANT.md](./MULTI_TENANT.md);
para operacao ver [GUIA-OPERACAO-MULTI-TENANT.md](./GUIA-OPERACAO-MULTI-TENANT.md).

### Pre-requisitos no provider DNS

Adicionar wildcard A record:
```
A    *    ->    <IP da VM>    TTL 300
```

Sem isso, cada tenant novo exige A record especifico.

### Pre-requisitos no .env da VM

```bash
ADMIN_API_TOKEN=<gerar com `openssl rand -base64 36`>
TENANTS_DATA_DIR=/app/data
DEFAULT_TENANT_DATABASE_FILE=cliente.db   # banco do tenant default
```

### Cert SSL para subdomains

Cada subdomain de tenant precisa estar coberto pelo cert. Apos criar
o tenant via script:
```bash
sudo certbot certonly --webroot --webroot-path /var/www/certbot \
    --email contato@<dominio> --agree-tos --no-eff-email \
    --non-interactive --expand \
    -d <dominio> -d www.<dominio> -d <slug>.<dominio>

sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml \
    exec nginx nginx -s reload
```

### Endpoints administrativos

| Metodo | Caminho | Auth | Acao |
|---|---|---|---|
| GET | /api/admin/tenants | X-Admin-Token | listar tenants |
| POST | /api/admin/tenants | X-Admin-Token | criar tenant + admin user |
