#!/usr/bin/env bash
# =============================================================================
# Dominio proprio das lojas: certificado + nginx + ativacao, sem mao humana.
#
# Roda NA VM pelo cron, de 5 em 5 minutos:
#   */5 * * * * /home/ubuntu/ConnectVeiculos/scripts/dominios-proprios.sh >> /home/ubuntu/backups/dominios.log 2>&1
#
# Para cada dominio cadastrado na tela de Integracoes (API admin):
#   1. se o DNS ja aponta para esta VM e nao ha certificado, pede um ao
#      Let's Encrypt (webroot — o desafio cai no server da plataforma);
#   2. com o certificado, gera nginx/dominios/<dominio>.conf;
#   3. testa e recarrega o nginx so se algo mudou;
#   4. confirma o HTTPS e ativa o dominio na API — a partir dai os links
#      publicos (feeds, posts, sitemap) usam o dominio da loja.
# Dominio removido na tela: o .conf dele sai e o nginx deixa de servi-lo.
#
# Tambem recarrega o nginx quando qualquer certificado for renovado: o certbot
# renova sozinho, mas o nginx so le o arquivo novo num reload.
#
# Variaveis opcionais:
#   DOMINIO_PROPRIO_IP   IP publico desta VM (default 136.248.77.154)
#   APP_DIR              raiz do projeto (default /home/ubuntu/ConnectVeiculos)
# =============================================================================
set -euo pipefail

APP_DIR="${APP_DIR:-/home/ubuntu/ConnectVeiculos}"
IP="${DOMINIO_PROPRIO_IP:-136.248.77.154}"
CONF_DIR="$APP_DIR/nginx/dominios"
NGINX="connectveiculos-nginx-1"
WEBROOT=/var/www/certbot
CARIMBO="$CONF_DIR/.ultimo-reload"

log() { echo "[$(date '+%F %T')] $*"; }

# Uma execucao por vez (certbot pode demorar mais que 5 min num dia ruim).
exec 9>/tmp/dominios-proprios.lock
flock -n 9 || exit 0

mkdir -p "$CONF_DIR"

TOKEN=$(grep '^ADMIN_API_TOKEN=' "$APP_DIR/.env" | head -1 | cut -d= -f2-)
if [ -z "$TOKEN" ]; then
    log "ERRO: ADMIN_API_TOKEN ausente em $APP_DIR/.env"
    exit 1
fi

# A API e' chamada pelo nginx local (porta 80, /api/ nao redireciona).
api() {
    curl -fsS --max-time 20 -H "Host: connectveiculos.dev.br" -H "X-Admin-Token: $TOKEN" "$@"
}

aponta() {
    getent ahostsv4 "$1" 2>/dev/null | awk '{print $1}' | sort -u | grep -qx "$IP"
}

tem_no_certificado() {
    sudo openssl x509 -in "/etc/letsencrypt/live/$1/fullchain.pem" -noout -ext subjectAltName 2>/dev/null \
        | grep -qE "DNS:${2//./\.}(,|$)"
}

gerar_conf() {
    local d="$1" nomes="$1"
    if tem_no_certificado "$d" "www.$d"; then nomes="$d www.$d"; fi
    cat <<EOF
# Gerado por scripts/dominios-proprios.sh — nao editar (e' sobrescrito).
server {
    listen 80;
    server_name $nomes;
    location /.well-known/acme-challenge/ { root $WEBROOT; }
    location / { return 301 https://$d\$request_uri; }
}

server {
    listen 443 ssl;
    http2 on;
    server_name $nomes;
    ssl_certificate /etc/letsencrypt/live/$d/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/$d/privkey.pem;
    include /etc/nginx/snippets/dominio-proprio.conf;
}
EOF
}

LISTA=$(api "http://127.0.0.1/api/admin/tenants/dominios") || { log "ERRO: API nao respondeu"; exit 1; }

mudou=0
declare -A ESPERADOS=()
PENDENTES_ATIVACAO=()

while IFS=$'\t' read -r slug dominio ativo; do
    if [ -z "${dominio:-}" ]; then continue; fi
    # Defesa: o valor vai para o nome de arquivo e para a config do nginx.
    if ! [[ "$dominio" =~ ^[a-z0-9]([a-z0-9.-]*[a-z0-9])?$ ]] || ! [[ "$slug" =~ ^[a-z0-9-]+$ ]]; then
        log "ignorado: valor inesperado '$slug' / '$dominio'"
        continue
    fi

    if ! sudo test -f "/etc/letsencrypt/live/$dominio/fullchain.pem"; then
        if ! aponta "$dominio"; then
            log "$dominio ($slug): DNS ainda nao aponta para $IP"
            continue
        fi
        nomes=(-d "$dominio")
        if aponta "www.$dominio"; then nomes+=(-d "www.$dominio"); fi
        log "$dominio ($slug): DNS ok, pedindo certificado (${nomes[*]})"
        if ! sudo certbot certonly --webroot -w "$WEBROOT" --cert-name "$dominio" "${nomes[@]}" \
                --non-interactive --agree-tos --keep-until-expiring -q; then
            log "$dominio: certbot falhou (tenta de novo no proximo ciclo)"
            continue
        fi
    elif aponta "www.$dominio" && ! tem_no_certificado "$dominio" "www.$dominio"; then
        # O www passou a apontar depois do certificado: inclui ele.
        log "$dominio: incluindo www no certificado"
        sudo certbot certonly --webroot -w "$WEBROOT" --cert-name "$dominio" -d "$dominio" -d "www.$dominio" \
            --non-interactive --agree-tos --expand -q || log "$dominio: falhou ao incluir o www"
    fi

    ESPERADOS["$dominio.conf"]=1
    novo=$(gerar_conf "$dominio")
    if [ ! -f "$CONF_DIR/$dominio.conf" ] || [ "$(cat "$CONF_DIR/$dominio.conf")" != "$novo" ]; then
        printf '%s\n' "$novo" > "$CONF_DIR/$dominio.conf.novo"
        mudou=1
    fi
    if [ "$ativo" = "false" ]; then PENDENTES_ATIVACAO+=("$slug $dominio"); fi
done < <(printf '%s' "$LISTA" | jq -r '.[] | [.slug, .dominio, (.ativo | tostring)] | @tsv')

# Dominios que sairam da tela: o .conf sai do nginx (o certificado fica).
for f in "$CONF_DIR"/*.conf; do
    [ -e "$f" ] || continue
    nome=$(basename "$f")
    if [ -z "${ESPERADOS[$nome]:-}" ]; then
        log "removendo $nome (dominio nao esta mais cadastrado)"
        mv "$f" "$f.removido"
        mudou=1
    fi
done

# Certificado renovado pelo certbot desde o ultimo reload tambem pede reload.
if [ -f "$CARIMBO" ] && sudo find /etc/letsencrypt/live/ -name fullchain.pem -newer "$CARIMBO" | grep -q .; then
    log "certificado renovado desde o ultimo reload"
    mudou=1
fi
if [ ! -f "$CARIMBO" ]; then mudou=1; fi

if [ "$mudou" = 1 ]; then
    NOVOS=()
    for f in "$CONF_DIR"/*.conf.novo; do
        [ -e "$f" ] || continue
        alvo="${f%.novo}"
        if [ -f "$alvo" ]; then cp "$alvo" "$alvo.anterior"; else NOVOS+=("$alvo"); fi
        mv "$f" "$alvo"
    done
    if docker exec "$NGINX" nginx -t -q; then
        docker exec "$NGINX" nginx -s reload
        rm -f "$CONF_DIR"/*.anterior "$CONF_DIR"/*.removido
        touch "$CARIMBO"
        log "nginx recarregado"
    else
        # Volta exatamente ao estado anterior. Um .conf quebrado na pasta
        # derrubaria o nginx inteiro (plataforma junto) no proximo restart.
        log "ERRO: nginx -t falhou; voltando a configuracao anterior"
        for f in "${NOVOS[@]}"; do rm -f "$f"; done
        for f in "$CONF_DIR"/*.conf.anterior; do if [ -e "$f" ]; then mv "$f" "${f%.anterior}"; fi; done
        for f in "$CONF_DIR"/*.conf.removido; do if [ -e "$f" ]; then mv "$f" "${f%.removido}"; fi; done
        exit 1
    fi
fi

# Ativacao: so depois que o HTTPS do dominio respondeu de verdade.
for item in "${PENDENTES_ATIVACAO[@]}"; do
    slug=${item%% *}; dominio=${item#* }
    if [ ! -f "$CONF_DIR/$dominio.conf" ]; then continue; fi
    if curl -fsS --max-time 10 --resolve "$dominio:443:127.0.0.1" "https://$dominio/health" > /dev/null; then
        api -X POST "http://127.0.0.1/api/admin/tenants/$slug/dominio/ativar" > /dev/null \
            && log "$dominio ($slug): ATIVO" \
            || log "$dominio: HTTPS ok, mas a API recusou a ativacao"
    else
        log "$dominio: HTTPS ainda nao responde"
    fi
done
