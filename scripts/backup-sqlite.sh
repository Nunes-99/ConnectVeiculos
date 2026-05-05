#!/bin/bash
# ConnectVeiculos - Backup automatico do SQLite (cliente.db)
#
# Roda diariamente via cron na VM. Usa o comando .backup do SQLite (consistente,
# nao trava o app), comprime, e remove backups com mais de RETENTION_DAYS dias.
#
# Tambem faz upload off-site para o bucket Object Storage Oracle (se oci CLI
# estiver instalado e configurado em /home/ubuntu/.oci/). Falhas no upload nao
# matam o backup local — apenas alertam via Discord webhook (se configurado).
# Retencao remota e gerenciada por lifecycle policy no bucket (delete >30 dias).
#
# Restore (LOCAL):
#   1) sudo docker compose stop backend-cliente
#   2) gunzip -c /home/ubuntu/backups/cliente-YYYYMMDD-HHMMSS.db.gz \
#        > /home/ubuntu/ConnectVeiculos/data/cliente.db
#   3) sudo docker compose start backend-cliente
#
# Restore (OFF-SITE, se a VM morreu):
#   1) Listar objetos no bucket:
#        oci os object list --bucket-name connectveiculos-backups
#   2) Baixar o backup desejado:
#        oci os object get --bucket-name connectveiculos-backups \
#            --name cliente-YYYYMMDD-HHMMSS.db.gz \
#            --file ./cliente.db.gz
#   3) gunzip cliente.db.gz, mover para /home/ubuntu/ConnectVeiculos/data/cliente.db
#   4) start backend
#
# Setup do cron (rodar uma vez, na VM):
#   chmod +x /home/ubuntu/ConnectVeiculos/scripts/backup-sqlite.sh
#   ( crontab -l 2>/dev/null | grep -v 'backup-sqlite';
#     echo '30 3 * * * /home/ubuntu/ConnectVeiculos/scripts/backup-sqlite.sh >> /home/ubuntu/backups/backup.log 2>&1'
#   ) | crontab -
#
# Variaveis sobrescriviveis (env):
#   DB_PATH         caminho do cliente.db (default: /home/ubuntu/ConnectVeiculos/data/cliente.db)
#   BACKUP_DIR      destino dos backups (default: /home/ubuntu/backups)
#   RETENTION_DAYS  quantos dias manter localmente (default: 7)
#   OCI_BUCKET      bucket Object Storage para off-site (default: connectveiculos-backups)
#   OCI_CLI         caminho do binario oci (default: /home/ubuntu/.local/bin/oci)
#                   se nao existir, upload off-site eh pulado silenciosamente

set -e

DB_PATH="${DB_PATH:-/home/ubuntu/ConnectVeiculos/data/cliente.db}"
BACKUP_DIR="${BACKUP_DIR:-/home/ubuntu/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"

DATE=$(date +%Y%m%d-%H%M%S)
TMP_BACKUP="$BACKUP_DIR/cliente-$DATE.db"
FINAL_BACKUP="$TMP_BACKUP.gz"

mkdir -p "$BACKUP_DIR"

if [ ! -f "$DB_PATH" ]; then
    echo "[$(date '+%F %T')] ERRO: banco nao encontrado em $DB_PATH" >&2
    exit 1
fi

# .backup gera uma copia consistente mesmo com o app escrevendo
sqlite3 "$DB_PATH" ".backup '$TMP_BACKUP'"
gzip "$TMP_BACKUP"

# Remove backups antigos
find "$BACKUP_DIR" -maxdepth 1 -name 'cliente-*.db.gz' -type f -mtime +"$RETENTION_DAYS" -delete

SIZE=$(du -h "$FINAL_BACKUP" | cut -f1)
COUNT=$(find "$BACKUP_DIR" -maxdepth 1 -name 'cliente-*.db.gz' -type f | wc -l)

echo "[$(date '+%F %T')] OK: $FINAL_BACKUP ($SIZE), $COUNT backups mantidos localmente."

# --- Upload off-site (Object Storage Oracle) ---
# Falhas aqui nao quebram o backup local; apenas alertam via Discord se webhook estiver configurado.
# Quando o script roda via sudo, HOME=/root e o oci CLI procura /root/.oci/config — entao
# precisamos passar --config-file explicito apontando para a config do user ubuntu.
OCI_BUCKET="${OCI_BUCKET:-connectveiculos-backups}"
OCI_CLI="${OCI_CLI:-/home/ubuntu/.local/bin/oci}"
OCI_CONFIG="${OCI_CONFIG:-/home/ubuntu/.oci/config}"
OCI_REMOTE_RETENTION_DAYS="${OCI_REMOTE_RETENTION_DAYS:-30}"
OBJ_NAME="cliente-$DATE.db.gz"

if [ -x "$OCI_CLI" ] && [ -f "$OCI_CONFIG" ]; then
    if "$OCI_CLI" --config-file "$OCI_CONFIG" os object put \
            --bucket-name "$OCI_BUCKET" \
            --name "$OBJ_NAME" \
            --file "$FINAL_BACKUP" \
            --force >/dev/null 2>&1; then
        echo "[$(date '+%F %T')] OFF-SITE OK: $OBJ_NAME -> bucket $OCI_BUCKET"

        # Rotacao manual: deleta objetos com mais de OCI_REMOTE_RETENTION_DAYS dias.
        # (Em vez de lifecycle policy do bucket, que exigiria IAM permitindo o service
        # principal — mais simples manter retencao no script.)
        cutoff_iso=$(date -u -d "$OCI_REMOTE_RETENTION_DAYS days ago" +%Y-%m-%dT%H:%M:%SZ)
        deleted=0
        # Lista objetos cliente-*.db.gz e encontra os mais antigos que cutoff
        "$OCI_CLI" --config-file "$OCI_CONFIG" os object list \
                --bucket-name "$OCI_BUCKET" \
                --prefix "cliente-" \
                --query "data[?\"time-created\" < '$cutoff_iso'].name" \
                --raw-output 2>/dev/null \
            | tr -d '[]"' | tr ',' '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' | grep -v '^$' \
            | while read -r old_obj; do
                if "$OCI_CLI" --config-file "$OCI_CONFIG" os object delete \
                        --bucket-name "$OCI_BUCKET" --name "$old_obj" --force >/dev/null 2>&1; then
                    deleted=$((deleted+1))
                    echo "  deletado off-site: $old_obj"
                fi
            done
    else
        echo "[$(date '+%F %T')] OFF-SITE ERRO: upload de $OBJ_NAME para $OCI_BUCKET falhou" >&2

        # Tenta avisar no Discord (best effort — sem matar o cron)
        if [ -z "$DISCORD_WEBHOOK_URL" ] && [ -f /home/ubuntu/ConnectVeiculos/.env ]; then
            DISCORD_WEBHOOK_URL=$(grep '^DISCORD_WEBHOOK_URL=' /home/ubuntu/ConnectVeiculos/.env | head -1 | cut -d= -f2-)
        fi
        if [ -n "$DISCORD_WEBHOOK_URL" ]; then
            curl -fsSL -X POST -H 'Content-Type: application/json' \
                -d '{"username":"ConnectVeiculos","embeds":[{"title":"⚠️ Backup off-site falhou","description":"Backup LOCAL OK, mas upload pro Object Storage falhou. Backup local em '"$BACKUP_DIR"'/'"$OBJ_NAME"'. Verifique credenciais oci e logs em '"$BACKUP_DIR"'/backup.log","color":16753920,"fields":[{"name":"Bucket","value":"'"$OCI_BUCKET"'","inline":true},{"name":"Objeto","value":"'"$OBJ_NAME"'","inline":true}]}]}' \
                "$DISCORD_WEBHOOK_URL" -o /dev/null 2>/dev/null || true
        fi
    fi
else
    echo "[$(date '+%F %T')] OFF-SITE PULADO: oci CLI ou config nao encontrado (backup so local)"
fi
