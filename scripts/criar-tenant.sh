#!/bin/bash
# ConnectVeiculos - provisionamento de novo tenant (cliente operador).
#
# Cria registro no banco master, banco SQLite isolado para o tenant,
# e o usuario admin do tenant. Apos rodar:
#   - Tenant fica acessivel em https://{slug}.connectveiculos.dev.br
#     (depois que o cert SSL for emitido — vide passo final do script).
#
# Uso:
#   bash scripts/criar-tenant.sh \
#     --slug acme \
#     --nome "Acme Veiculos LTDA" \
#     --admin-email joao@acme.com.br \
#     --admin-senha TempXyz123 \
#     [--admin-nome "Joao Silva"]
#
# Variaveis sobrescriviveis (env):
#   API_URL       URL base da API (default: https://connectveiculos.dev.br)
#   ADMIN_TOKEN   token do header X-Admin-Token (default: lido do .env da VM
#                 ou do CONNECTVEICULOS-CREDENCIAIS.txt local)

set -e

SLUG=""
NOME=""
ADMIN_EMAIL=""
ADMIN_SENHA=""
ADMIN_NOME=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --slug) SLUG="$2"; shift 2 ;;
        --nome) NOME="$2"; shift 2 ;;
        --admin-email) ADMIN_EMAIL="$2"; shift 2 ;;
        --admin-senha) ADMIN_SENHA="$2"; shift 2 ;;
        --admin-nome) ADMIN_NOME="$2"; shift 2 ;;
        *) echo "Argumento desconhecido: $1" >&2; exit 1 ;;
    esac
done

if [ -z "$SLUG" ] || [ -z "$NOME" ] || [ -z "$ADMIN_EMAIL" ] || [ -z "$ADMIN_SENHA" ]; then
    echo "Uso: bash scripts/criar-tenant.sh --slug X --nome 'Y' --admin-email z --admin-senha w [--admin-nome 'N']" >&2
    exit 1
fi

API_URL="${API_URL:-https://connectveiculos.dev.br}"

# Resolver ADMIN_TOKEN se nao foi passado
if [ -z "$ADMIN_TOKEN" ]; then
    if [ -f "$HOME/Documents/Vitor/ConnectVeiculos-CREDENCIAIS.txt" ]; then
        ADMIN_TOKEN=$(grep '^ADMIN_API_TOKEN=' "$HOME/Documents/Vitor/ConnectVeiculos-CREDENCIAIS.txt" 2>/dev/null | head -1 | cut -d= -f2-)
    fi
fi
if [ -z "$ADMIN_TOKEN" ] && [ -f /home/ubuntu/ConnectVeiculos/.env ]; then
    ADMIN_TOKEN=$(grep '^ADMIN_API_TOKEN=' /home/ubuntu/ConnectVeiculos/.env 2>/dev/null | head -1 | cut -d= -f2-)
fi

if [ -z "$ADMIN_TOKEN" ]; then
    echo "ERRO: ADMIN_TOKEN nao definido. Defina ADMIN_API_TOKEN no .env da VM e em CONNECTVEICULOS-CREDENCIAIS.txt." >&2
    exit 1
fi

# Monta JSON com escape basico (suficiente para os campos esperados)
escape_json() { printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'; }

PAYLOAD=$(cat <<JSON
{
  "slug": "$(escape_json "$SLUG")",
  "nome": "$(escape_json "$NOME")",
  "adminEmail": "$(escape_json "$ADMIN_EMAIL")",
  "adminSenha": "$(escape_json "$ADMIN_SENHA")"$( [ -n "$ADMIN_NOME" ] && echo "," ),
  $( [ -n "$ADMIN_NOME" ] && echo "\"adminNome\": \"$(escape_json "$ADMIN_NOME")\"" )
}
JSON
)
# Remove linhas em branco do payload (cosmetico)
PAYLOAD=$(echo "$PAYLOAD" | grep -v '^$' || echo "$PAYLOAD")

echo ">>> POST $API_URL/api/admin/tenants (slug=$SLUG)"
HTTP_CODE=$(curl -sS -o /tmp/criar-tenant.out -w '%{http_code}' \
    -X POST "$API_URL/api/admin/tenants" \
    -H "X-Admin-Token: $ADMIN_TOKEN" \
    -H 'Content-Type: application/json' \
    -d "$PAYLOAD")

echo ">>> HTTP $HTTP_CODE"
cat /tmp/criar-tenant.out
echo
rm -f /tmp/criar-tenant.out

if [ "$HTTP_CODE" != "201" ]; then
    echo
    echo "ERRO: criacao do tenant falhou (HTTP $HTTP_CODE)." >&2
    exit 1
fi

echo
echo "================================================================"
echo "  Tenant '$SLUG' criado com sucesso."
echo "================================================================"
echo
echo "Proximos passos manuais:"
echo
echo "  1) Configurar DNS A record (ou wildcard) para:"
echo "     $SLUG.connectveiculos.dev.br -> IP da VM (136.248.77.154)"
echo
echo "  2) Expandir cert SSL Let's Encrypt na VM para incluir o subdomain:"
echo
echo "     sudo certbot certonly --webroot --webroot-path /var/www/certbot \\"
echo "       --email contato@connectveiculos.dev.br --agree-tos --no-eff-email \\"
echo "       --non-interactive --expand \\"
echo "       -d connectveiculos.dev.br -d www.connectveiculos.dev.br \\"
echo "       -d $SLUG.connectveiculos.dev.br"
echo
echo "  3) Reload nginx para aplicar o cert novo:"
echo "     sudo docker compose -f /home/ubuntu/ConnectVeiculos/docker-compose.yml \\"
echo "       exec nginx nginx -s reload"
echo
echo "  4) Acessar https://$SLUG.connectveiculos.dev.br e logar com $ADMIN_EMAIL"
