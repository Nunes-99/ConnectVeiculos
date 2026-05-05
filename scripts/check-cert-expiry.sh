#!/bin/bash
# ConnectVeiculos - alerta de cert SSL proximo de expirar
#
# Roda diariamente via cron na VM. Faz handshake TLS contra o dominio,
# le notAfter do cert, calcula dias restantes. Se < THRESHOLD_DAYS, posta
# alerta no Discord webhook configurado em DISCORD_WEBHOOK_URL.
#
# Sem alerta = cert OK (silencio eh sucesso).
#
# Uso manual (teste):
#   THRESHOLD_DAYS=999 bash scripts/check-cert-expiry.sh   # forca alerta
#
# Setup do cron (na VM, uma vez):
#   ( sudo crontab -l 2>/dev/null | grep -v 'check-cert-expiry';
#     echo '0 8 * * * /home/ubuntu/ConnectVeiculos/scripts/check-cert-expiry.sh >> /home/ubuntu/backups/cert-check.log 2>&1'
#   ) | sudo crontab -
#
# Variaveis sobrescriviveis (env):
#   DOMAIN              dominio a checar (default: connectveiculos.dev.br)
#   THRESHOLD_DAYS      dias antes de comecar a alertar (default: 7)
#   DISCORD_WEBHOOK_URL URL do webhook (obrigatorio; lido do ENV_FILE se ausente)
#   ENV_FILE            de onde ler DISCORD_WEBHOOK_URL se nao no env do shell
#                       (default: /home/ubuntu/ConnectVeiculos/.env)

set -e

DOMAIN="${DOMAIN:-connectveiculos.dev.br}"
THRESHOLD_DAYS="${THRESHOLD_DAYS:-7}"
ENV_FILE="${ENV_FILE:-/home/ubuntu/ConnectVeiculos/.env}"

if [ -z "$DISCORD_WEBHOOK_URL" ] && [ -f "$ENV_FILE" ]; then
    DISCORD_WEBHOOK_URL=$(grep '^DISCORD_WEBHOOK_URL=' "$ENV_FILE" | head -1 | cut -d= -f2-)
fi

if [ -z "$DISCORD_WEBHOOK_URL" ]; then
    echo "[$(date '+%F %T')] ERRO: DISCORD_WEBHOOK_URL nao configurado." >&2
    exit 1
fi

post_alert() {
    # $1 = title, $2 = description, $3 = color (decimal), $4 = days_left, $5 = not_after
    local payload
    payload=$(cat <<JSON
{
  "username": "ConnectVeiculos",
  "embeds": [{
    "title": "$1",
    "description": "$2",
    "color": $3,
    "fields": [
      {"name": "Dominio", "value": "$DOMAIN", "inline": true},
      {"name": "Dias restantes", "value": "$4", "inline": true},
      {"name": "Expira em", "value": "$5", "inline": false},
      {"name": "Como investigar", "value": "\`sudo systemctl status certbot.timer\`\n\`sudo journalctl -u certbot.service -n 50\`\n\`sudo certbot certificates\`", "inline": false}
    ],
    "footer": {"text": "Cron diario as 08:00 (host) - $(hostname)"}
  }]
}
JSON
)
    curl -fsSL -X POST -H 'Content-Type: application/json' -d "$payload" "$DISCORD_WEBHOOK_URL" -o /dev/null
}

# Tenta ler o cert via TLS handshake
NOT_AFTER=$(echo | openssl s_client -servername "$DOMAIN" -connect "$DOMAIN:443" 2>/dev/null \
    | openssl x509 -noout -enddate 2>/dev/null \
    | cut -d= -f2)

if [ -z "$NOT_AFTER" ]; then
    echo "[$(date '+%F %T')] ALERTA: handshake TLS contra $DOMAIN falhou."
    post_alert \
        "❌ Cert SSL inacessivel" \
        "Nao foi possivel ler o cert de $DOMAIN via TLS handshake. Site pode estar fora do ar ou com cert quebrado." \
        15158332 \
        "?" \
        "?"
    exit 1
fi

EXPIRY_TS=$(date -d "$NOT_AFTER" +%s)
NOW_TS=$(date +%s)
DAYS_LEFT=$(( (EXPIRY_TS - NOW_TS) / 86400 ))

if [ "$DAYS_LEFT" -lt 0 ]; then
    echo "[$(date '+%F %T')] CRITICO: cert $DOMAIN ja expirou ($DAYS_LEFT dias)."
    post_alert \
        "🔴 Cert SSL EXPIROU" \
        "O cert ja venceu. HTTPS provavelmente quebrado. Renove urgente." \
        15158332 \
        "$DAYS_LEFT" \
        "$NOT_AFTER"
elif [ "$DAYS_LEFT" -lt "$THRESHOLD_DAYS" ]; then
    echo "[$(date '+%F %T')] AVISO: cert $DOMAIN expira em $DAYS_LEFT dias."
    post_alert \
        "🟠 Cert SSL proximo de expirar" \
        "A renovacao automatica do Let's Encrypt aparentemente nao executou (cert deveria ter sido renovado quando faltavam 30 dias). Investigue antes que vire outage." \
        16753920 \
        "$DAYS_LEFT" \
        "$NOT_AFTER"
else
    echo "[$(date '+%F %T')] OK: $DOMAIN cert valido por mais $DAYS_LEFT dias (expira $NOT_AFTER)."
fi
