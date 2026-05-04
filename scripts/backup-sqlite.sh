#!/bin/bash
# ConnectVeiculos - Backup automatico do SQLite (cliente.db)
#
# Roda diariamente via cron na VM. Usa o comando .backup do SQLite (consistente,
# nao trava o app), comprime, e remove backups com mais de RETENTION_DAYS dias.
#
# Restore:
#   1) sudo docker compose stop backend-cliente
#   2) gunzip -c /home/ubuntu/backups/cliente-YYYYMMDD-HHMMSS.db.gz \
#        > /home/ubuntu/ConnectVeiculos/data/cliente.db
#   3) sudo docker compose start backend-cliente
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
#   RETENTION_DAYS  quantos dias manter (default: 7)

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

echo "[$(date '+%F %T')] OK: $FINAL_BACKUP ($SIZE), $COUNT backups mantidos."
