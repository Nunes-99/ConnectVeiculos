#!/bin/bash
# Pega uma chave de acesso da Credere para o teste real (CredereRealTests),
# sem precisar rodar o sistema inteiro no PC. Rodar no Git Bash:
#
#   bash scripts/credere-token.sh
#
# 1. Le CREDERE_CLIENT_ID/CREDERE_CLIENT_SECRET do arquivo de credenciais do
#    PC (o trocar-chaves.sh grava la) ou pede para colar (nao aparece na tela).
# 2. Mostra o link de login da Credere: abra no navegador e entre com a conta
#    da loja. A Credere volta para o endereco de retorno com ?code=... na
#    barra (a pagina pode dar erro, tudo bem): copie o endereco inteiro.
# 3. Troca o codigo pela chave (vale 5 minutos, uso unico) e grava em
#    ~/.credere-token.json, so legivel por voce. A chave nunca e' impressa.
set -euo pipefail

CRED="$HOME/Documents/Vitor/ConnectVeiculos-CREDENCIAIS.txt"
SAIDA="$HOME/.credere-token.json"
BASE="${CREDERE_BASE_URL:-https://app.meucredere.com.br/api/v1}"
REDIRECT="${CREDERE_REDIRECT_URI:-http://localhost:4200/integracoes/credere}"

if [ ! -t 0 ]; then
  echo "Rode num terminal Git Bash: bash scripts/credere-token.sh"
  exit 1
fi

ler() { [ -f "$CRED" ] && grep -E "^$1=" "$CRED" | tail -1 | cut -d= -f2- | tr -d '\r' || true; }
CLIENT_ID=$(ler CREDERE_CLIENT_ID)
CLIENT_SECRET=$(ler CREDERE_CLIENT_SECRET)
[ -n "$CLIENT_ID" ] || read -r -p "CREDERE_CLIENT_ID: " CLIENT_ID
[ -n "$CLIENT_SECRET" ] || { read -r -s -p "CREDERE_CLIENT_SECRET (nao aparece): " CLIENT_SECRET; echo; }
CLIENT_ID=$(printf '%s' "$CLIENT_ID" | tr -d '\r' | xargs)
CLIENT_SECRET=$(printf '%s' "$CLIENT_SECRET" | tr -d '\r' | xargs)

URL_REDIRECT=$(python -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1],safe=""))' "$REDIRECT")
echo
echo "Abra no navegador e entre com a conta da loja na Credere:"
echo
echo "  $BASE/authorize?response_type=code&scope=simulator+proposals&client_id=$CLIENT_ID&redirect_uri=$URL_REDIRECT"
echo
read -r -p "Cole aqui o endereco para onde a Credere voltou (ou so o codigo): " VOLTA
CODE=$(python -c 'import sys,urllib.parse as u
v=sys.argv[1].strip()
q=u.parse_qs(u.urlparse(v).query)
print(q["code"][0] if "code" in q else v)' "$VOLTA")
[ -n "$CODE" ] || { echo "Sem codigo."; exit 1; }

# O corpo vai pela entrada padrao: o segredo nao aparece na linha de comando.
RESPOSTA=$(CODE="$CODE" CID="$CLIENT_ID" CSEC="$CLIENT_SECRET" RED="$REDIRECT" python -c 'import json,os
print(json.dumps({"code":os.environ["CODE"],"grant_type":"authorization_code","scope":"simulator+proposals",
"redirect_uri":os.environ["RED"],"client_id":os.environ["CID"],"client_secret":os.environ["CSEC"]}))' \
  | curl -sS -X POST "$BASE/token" -H "Content-Type: application/json" --data @- -w '\n%{http_code}')
STATUS=$(printf '%s' "$RESPOSTA" | tail -1)
CORPO=$(printf '%s' "$RESPOSTA" | sed '$d')

if [ "$STATUS" != "200" ] && [ "$STATUS" != "201" ]; then
  echo "A Credere recusou ($STATUS):"
  printf '%s' "$CORPO" | python -c 'import json,sys
try:
  d=json.load(sys.stdin); e=d.get("error"); print(e.get("message") if isinstance(e,dict) else (d.get("error_description") or e))
except Exception: print("(resposta sem JSON)")'
  exit 1
fi

umask 077
printf '%s' "$CORPO" > "$SAIDA"
printf '%s' "$CORPO" | python -c 'import json,sys,datetime
d=json.load(sys.stdin); u=d.get("user") or {}; c=u.get("account") or {}
exp=datetime.datetime.fromtimestamp(d.get("created_at",0)+d.get("expires_in",0))
print("Chave salva em ~/.credere-token.json")
print("Conta:", c.get("name"), "| usuario:", u.get("name"), "| escopos:", d.get("scope"))
print("Vale ate:", exp.strftime("%d/%m/%Y"))'
echo
echo "Agora rode o teste real (no Git Bash, na pasta back-end):"
echo "  CREDERE_TESTE_REAL=1 dotnet test ConnectVeiculos.Tests --filter CredereRealTests --logger \"console;verbosity=detailed\""
