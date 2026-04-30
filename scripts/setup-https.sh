#!/bin/bash
# ConnectVeiculos - setup HTTPS com Let's Encrypt
# Rode UMA UNICA VEZ apos o primeiro deploy estar de pe e DNS apontado.
#
# Pre-requisitos:
#   - DNS de connectveiculos.dev.br aponta para o IP da VM
#   - docker compose esta de pe (porta 80 acessivel)
#   - rodar como root ou com sudo

set -e

DOMAIN="${DOMAIN:-connectveiculos.dev.br}"
EMAIL="${EMAIL:-contato@connectveiculos.dev.br}"

echo ">>> Setup HTTPS para $DOMAIN"

# 1. Instalar certbot se nao existir
if ! command -v certbot &> /dev/null; then
    echo ">>> Instalando certbot"
    apt-get update
    apt-get install -y certbot
fi

# 2. Criar diretorio do webroot challenge
mkdir -p /var/www/certbot

# 3. Gerar certificado em modo webroot (nginx ja roda dentro do compose)
echo ">>> Gerando certificado para $DOMAIN e www.$DOMAIN"
certbot certonly \
    --webroot \
    --webroot-path /var/www/certbot \
    --email "$EMAIL" \
    --agree-tos \
    --no-eff-email \
    --non-interactive \
    -d "$DOMAIN" \
    -d "www.$DOMAIN"

# 4. Habilitar bloco HTTPS no nginx.conf
echo ">>> Habilitando HTTPS no nginx.conf"
NGINX_CONF="$(dirname "$0")/../nginx/nginx.conf"
# Descomenta bloco HTTPS (remove '# ' do inicio das linhas)
sed -i '/^# server {$/,/^# }$/{ s/^# // }' "$NGINX_CONF"
# Habilita redirect HTTP->HTTPS
sed -i 's|# return 301 https://\$host\$request_uri;|return 301 https://$host$request_uri;|' "$NGINX_CONF"

# 5. Recarregar nginx
echo ">>> Recarregando nginx"
docker compose -f "$(dirname "$0")/../docker-compose.yml" exec nginx nginx -s reload || \
    docker compose -f "$(dirname "$0")/../docker-compose.yml" restart nginx

# 6. Setup auto-renovacao via cron (Let's Encrypt expira em 90 dias)
CRON_JOB="0 3 * * * certbot renew --quiet --post-hook 'docker compose -f $(realpath $(dirname "$0")/../docker-compose.yml) exec nginx nginx -s reload'"
( crontab -l 2>/dev/null | grep -v "certbot renew"; echo "$CRON_JOB" ) | crontab -

echo ""
echo ">>> HTTPS configurado!"
echo ">>> Verifique em: https://$DOMAIN"
echo ">>> Certificado renova automaticamente todo dia 3h da manha"
