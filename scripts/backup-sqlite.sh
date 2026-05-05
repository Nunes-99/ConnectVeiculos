#!/bin/bash
# ConnectVeiculos - Backup automatico dos bancos SQLite (multi-tenant).
#
# Roda diariamente via cron na VM. Para cada arquivo .db em DATA_DIR:
#   - usa o comando .backup do SQLite (consistente, nao trava o app)
#   - comprime com gzip
#   - sobe para o Object Storage Oracle (se oci CLI configurado)
#   - aplica retencao local (RETENTION_DAYS, default 7)
#   - aplica retencao remota (OCI_REMOTE_RETENTION_DAYS, default 30)
#
# Nomenclatura: cada backup vira "{base}-YYYYMMDD-HHMMSS.db.gz" onde
# {base} eh o nome do arquivo .db sem extensao (cliente.db -> "cliente",
# acme.db -> "acme", _master.db -> "_master"). Rotacao isolada por prefixo
# — backup de um tenant nao afeta o de outro.
#
# Falhas no upload nao matam o backup local — alertam via Discord webhook
# (se DISCORD_WEBHOOK_URL configurado em .env ou env var).
#
# Restore (LOCAL):
#   1) sudo docker compose stop backend-cliente
#   2) gunzip -c /home/ubuntu/backups/{base}-YYYYMMDD-HHMMSS.db.gz \
#        > /home/ubuntu/ConnectVeiculos/data/{base}.db
#   3) sudo docker compose start backend-cliente
#
# Restore (OFF-SITE — VM nova):
#   1) oci os object list --bucket-name connectveiculos-backups \
#        --prefix "{base}-" --query "data[-1].name" --raw-output
#   2) oci os object get --bucket-name connectveiculos-backups \
#        --name {base}-YYYYMMDD-HHMMSS.db.gz --file ./{base}.db.gz
#   3) gunzip {base}.db.gz, mover para /home/ubuntu/ConnectVeiculos/data/{base}.db
#   4) Repetir para o _master.db (registry de tenants) e cada {tenant-slug}.db
#   5) start backend
#
# Setup do cron (rodar uma vez, na VM):
#   chmod +x /home/ubuntu/ConnectVeiculos/scripts/backup-sqlite.sh
#   ( crontab -l 2>/dev/null | grep -v 'backup-sqlite';
#     echo '30 3 * * * /home/ubuntu/ConnectVeiculos/scripts/backup-sqlite.sh >> /home/ubuntu/backups/backup.log 2>&1'
#   ) | crontab -
#
# Variaveis sobrescriviveis (env):
#   DATA_DIR                       diretorio dos .db (default: /home/ubuntu/ConnectVeiculos/data)
#   BACKUP_DIR                     destino dos backups (default: /home/ubuntu/backups)
#   RETENTION_DAYS                 dias de retencao local (default: 7)
#   OCI_BUCKET                     bucket off-site (default: connectveiculos-backups)
#   OCI_CLI                        caminho do oci (default: /home/ubuntu/.local/bin/oci)
#   OCI_CONFIG                     caminho do config oci (default: /home/ubuntu/.oci/config)
#   OCI_REMOTE_RETENTION_DAYS      dias de retencao no bucket (default: 30)

set -e

DATA_DIR="${DATA_DIR:-/home/ubuntu/ConnectVeiculos/data}"
BACKUP_DIR="${BACKUP_DIR:-/home/ubuntu/backups}"
RETENTION_DAYS="${RETENTION_DAYS:-7}"
OCI_BUCKET="${OCI_BUCKET:-connectveiculos-backups}"
OCI_CLI="${OCI_CLI:-/home/ubuntu/.local/bin/oci}"
OCI_CONFIG="${OCI_CONFIG:-/home/ubuntu/.oci/config}"
OCI_REMOTE_RETENTION_DAYS="${OCI_REMOTE_RETENTION_DAYS:-30}"

DATE=$(date +%Y%m%d-%H%M%S)

mkdir -p "$BACKUP_DIR"

# Resolver DISCORD_WEBHOOK_URL para alertas de falha (best effort)
if [ -z "$DISCORD_WEBHOOK_URL" ] && [ -f /home/ubuntu/ConnectVeiculos/.env ]; then
    DISCORD_WEBHOOK_URL=$(grep '^DISCORD_WEBHOOK_URL=' /home/ubuntu/ConnectVeiculos/.env 2>/dev/null | head -1 | cut -d= -f2-)
fi

post_alert_discord() {
    local title="$1"
    local description="$2"
    local color="${3:-16753920}"  # default: laranja
    [ -z "$DISCORD_WEBHOOK_URL" ] && return 0
    curl -fsSL -X POST -H 'Content-Type: application/json' \
        -d "{\"username\":\"ConnectVeiculos\",\"embeds\":[{\"title\":\"$title\",\"description\":\"$description\",\"color\":$color}]}" \
        "$DISCORD_WEBHOOK_URL" -o /dev/null 2>/dev/null || true
}

backup_one_db() {
    local db_path="$1"
    local base
    base=$(basename "$db_path" .db)

    local tmp_backup="$BACKUP_DIR/${base}-${DATE}.db"
    local final_backup="${tmp_backup}.gz"

    if [ ! -f "$db_path" ]; then
        echo "[$(date '+%F %T')] AVISO: $db_path nao encontrado, pulando."
        return 0
    fi

    # .backup gera copia consistente mesmo com app escrevendo
    if ! sqlite3 "$db_path" ".backup '$tmp_backup'" 2>/dev/null; then
        echo "[$(date '+%F %T')] ERRO local: backup de $db_path falhou" >&2
        post_alert_discord "🔴 Backup local falhou" "sqlite3 .backup retornou erro para $db_path" 15158332
        return 1
    fi
    gzip "$tmp_backup"

    # Retencao local por prefixo (cada base tem seu proprio ciclo)
    find "$BACKUP_DIR" -maxdepth 1 -name "${base}-*.db.gz" -type f -mtime +"$RETENTION_DAYS" -delete

    local size count
    size=$(du -h "$final_backup" | cut -f1)
    count=$(find "$BACKUP_DIR" -maxdepth 1 -name "${base}-*.db.gz" -type f | wc -l)
    echo "[$(date '+%F %T')] OK [$base]: $final_backup ($size), $count backups locais."

    # Upload off-site (best effort)
    if [ -x "$OCI_CLI" ] && [ -f "$OCI_CONFIG" ]; then
        local obj_name="${base}-${DATE}.db.gz"
        if "$OCI_CLI" --config-file "$OCI_CONFIG" os object put \
                --bucket-name "$OCI_BUCKET" \
                --name "$obj_name" \
                --file "$final_backup" \
                --force >/dev/null 2>&1; then
            echo "[$(date '+%F %T')] OFF-SITE OK [$base]: $obj_name -> $OCI_BUCKET"

            # Rotacao remota por prefixo
            local cutoff_iso
            cutoff_iso=$(date -u -d "$OCI_REMOTE_RETENTION_DAYS days ago" +%Y-%m-%dT%H:%M:%SZ)
            "$OCI_CLI" --config-file "$OCI_CONFIG" os object list \
                    --bucket-name "$OCI_BUCKET" \
                    --prefix "${base}-" \
                    --query "data[?\"time-created\" < '$cutoff_iso'].name" \
                    --raw-output 2>/dev/null \
                | tr -d '[]"' | tr ',' '\n' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//' | grep -v '^$' \
                | while read -r old_obj; do
                    if "$OCI_CLI" --config-file "$OCI_CONFIG" os object delete \
                            --bucket-name "$OCI_BUCKET" --name "$old_obj" --force >/dev/null 2>&1; then
                        echo "  deletado off-site: $old_obj"
                    fi
                done
        else
            echo "[$(date '+%F %T')] OFF-SITE ERRO [$base]: upload de $obj_name falhou" >&2
            post_alert_discord \
                "⚠️ Backup off-site falhou ($base)" \
                "Backup LOCAL OK, mas upload pro Object Storage falhou. Backup local em $BACKUP_DIR/$obj_name. Verifique credenciais oci."
        fi
    fi
}

# --- Loop sobre todos os .db do diretorio data ---
shopt -s nullglob
dbs=("$DATA_DIR"/*.db)
shopt -u nullglob

if [ ${#dbs[@]} -eq 0 ]; then
    echo "[$(date '+%F %T')] AVISO: nenhum .db encontrado em $DATA_DIR"
    exit 0
fi

if [ ! -x "$OCI_CLI" ] || [ ! -f "$OCI_CONFIG" ]; then
    echo "[$(date '+%F %T')] OFF-SITE: oci CLI ou config nao encontrado — backups serao apenas locais."
fi

failed=0
for db in "${dbs[@]}"; do
    if ! backup_one_db "$db"; then
        failed=$((failed+1))
    fi
done

if [ "$failed" -gt 0 ]; then
    echo "[$(date '+%F %T')] $failed banco(s) falharam no backup local." >&2
    exit 1
fi
