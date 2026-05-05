#!/bin/bash
# ConnectVeiculos - remove um tenant do sistema (operacao destrutiva,
# mas recuperavel — banco eh arquivado, nao deletado).
#
# Uso:
#   bash scripts/excluir-tenant.sh --slug acme [--yes]
#
# Sem --yes, pede confirmacao interativa.
#
# Variaveis sobrescriviveis (env):
#   API_URL       URL base da API (default: https://connectveiculos.dev.br)
#   ADMIN_TOKEN   token X-Admin-Token (default: lido do credenciais ou .env da VM)

set -e

SLUG=""
SKIP_CONFIRM=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --slug) SLUG="$2"; shift 2 ;;
        --yes|-y) SKIP_CONFIRM=true; shift ;;
        *) echo "Argumento desconhecido: $1" >&2; exit 1 ;;
    esac
done

if [ -z "$SLUG" ]; then
    echo "Uso: bash scripts/excluir-tenant.sh --slug X [--yes]" >&2
    exit 1
fi

if [ "$SLUG" = "default" ]; then
    echo "ERRO: tenant 'default' nao pode ser excluido (protecao)." >&2
    exit 1
fi

API_URL="${API_URL:-https://connectveiculos.dev.br}"

if [ -z "$ADMIN_TOKEN" ]; then
    if [ -f "$HOME/Documents/Vitor/ConnectVeiculos-CREDENCIAIS.txt" ]; then
        ADMIN_TOKEN=$(grep '^ADMIN_API_TOKEN=' "$HOME/Documents/Vitor/ConnectVeiculos-CREDENCIAIS.txt" 2>/dev/null | head -1 | cut -d= -f2-)
    fi
fi
if [ -z "$ADMIN_TOKEN" ] && [ -f /home/ubuntu/ConnectVeiculos/.env ]; then
    ADMIN_TOKEN=$(grep '^ADMIN_API_TOKEN=' /home/ubuntu/ConnectVeiculos/.env 2>/dev/null | head -1 | cut -d= -f2-)
fi

if [ -z "$ADMIN_TOKEN" ]; then
    echo "ERRO: ADMIN_TOKEN nao definido." >&2
    exit 1
fi

if [ "$SKIP_CONFIRM" = false ]; then
    echo
    echo "ATENCAO: voce esta prestes a excluir o tenant '$SLUG'."
    echo "Isso vai:"
    echo "  - Remover o registro do master (subdomain $SLUG.connectveiculos.dev.br para de funcionar)"
    echo "  - Arquivar o banco em /home/ubuntu/ConnectVeiculos/data/_arquivado_${SLUG}_YYYYMMDD-HHMMSS.db"
    echo "    (operacao recuperavel — basta renomear de volta + reinserir no master)"
    echo
    read -p "Tem certeza? Digite o slug exato '$SLUG' para confirmar: " input
    if [ "$input" != "$SLUG" ]; then
        echo "Confirmacao nao bate. Abortando."
        exit 1
    fi
fi

echo
echo ">>> DELETE $API_URL/api/admin/tenants/$SLUG"
HTTP_CODE=$(curl -sS -o /tmp/excluir-tenant.out -w '%{http_code}' \
    -X DELETE "$API_URL/api/admin/tenants/$SLUG" \
    -H "X-Admin-Token: $ADMIN_TOKEN")

echo ">>> HTTP $HTTP_CODE"
cat /tmp/excluir-tenant.out
echo
rm -f /tmp/excluir-tenant.out

if [ "$HTTP_CODE" != "200" ]; then
    echo
    echo "ERRO: exclusao falhou (HTTP $HTTP_CODE)." >&2
    exit 1
fi

echo
echo "================================================================"
echo "  Tenant '$SLUG' excluido com sucesso."
echo "================================================================"
echo
echo "Proximos passos manuais (opcional):"
echo
echo "  1) Reduzir o cert SSL Let's Encrypt removendo o subdomain do tenant"
echo "     excluido. Sem isso, o cert continua valido para $SLUG.connectveiculos.dev.br"
echo "     ate a proxima renovacao (90 dias). Para reduzir AGORA:"
echo
echo "     ssh -i ~/.ssh/connectveiculos-oracle.key ubuntu@136.248.77.154 \\"
echo "         \"sudo certbot certonly --webroot --webroot-path /var/www/certbot \\"
echo "             --email contato@connectveiculos.dev.br --agree-tos --no-eff-email \\"
echo "             --non-interactive --expand \\"
echo "             -d connectveiculos.dev.br -d www.connectveiculos.dev.br \\"
echo "             # listar AQUI todos os subdomains DOS TENANTS QUE FICAM (sem $SLUG)\""
echo
echo "  2) Remover o A record especifico do DNS se voce nao usa wildcard."
echo
echo "  3) Para deletar definitivamente o banco arquivado (apos verificar que"
echo "     nao precisa restaurar), na VM:"
echo "     sudo rm /home/ubuntu/ConnectVeiculos/data/_arquivado_${SLUG}_*.db"
