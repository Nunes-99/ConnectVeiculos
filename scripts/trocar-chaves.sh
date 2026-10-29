#!/bin/bash
# Troca as chaves secretas do .env da VM sem que elas passem pelo chat nem
# fiquem na linha de comando. Rodar no Git Bash do PC (precisa de terminal
# interativo para colar as chaves):
#
#   bash scripts/trocar-chaves.sh
#
# - JWT_SECRET_KEY_CLIENTE e ADMIN_API_TOKEN: gerados aqui (opcional).
# - META_APP_SECRET, ML_CLIENT_SECRET, GEMINI_API_KEY: cole a nova (gerada no
#   painel de cada um) ou deixe vazio para manter a atual.
# Faz backup do .env na VM, atualiza o arquivo de credenciais do PC e reinicia
# so o backend-cliente.
set -euo pipefail

VM=ubuntu@136.248.77.154
CHAVE_SSH="$HOME/.ssh/connectveiculos-oracle.key"
CRED="$HOME/Documents/Vitor/ConnectVeiculos-CREDENCIAIS.txt"

if [ ! -t 0 ]; then
  echo "Rode num terminal Git Bash (precisa colar as chaves): bash scripts/trocar-chaves.sh"
  exit 1
fi

declare -A NOVAS

read -r -p "Gerar nova JWT_SECRET_KEY_CLIENTE? Todo mundo precisa entrar de novo. [s/N] " r
# O openssl do Git Bash termina as linhas com \r\n: sem tirar o \r, a chave
# virava duas linhas no .env e o docker compose recusava o arquivo.
if [[ "$r" =~ ^[sS] ]]; then NOVAS[JWT_SECRET_KEY_CLIENTE]=$(openssl rand -base64 64 | tr -d '\r\n'); fi

read -r -p "Gerar novo ADMIN_API_TOKEN? [s/N] " r
if [[ "$r" =~ ^[sS] ]]; then NOVAS[ADMIN_API_TOKEN]=$(openssl rand -hex 32 | tr -d '\r\n'); fi

for k in META_APP_SECRET ML_CLIENT_SECRET GEMINI_API_KEY; do
  read -r -s -p "Novo $k (Enter vazio = manter o atual): " v; echo
  v=$(printf '%s' "$v" | tr -d '\r' | sed -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//')
  # No PowerShell o botao direito ja cola; somado ao Ctrl+V a chave vinha dobrada.
  h=$(( ${#v} / 2 ))
  if [ -n "$v" ] && [ $(( ${#v} % 2 )) -eq 0 ] && [ "${v:0:$h}" = "${v:$h}" ]; then
    v=${v:0:$h}; echo "  (a chave veio colada 2 vezes; usando uma so)"
  fi
  if [ -n "$v" ] && [ "$k" = META_APP_SECRET ] && ! [[ "$v" =~ ^[0-9a-f]{32}$ ]]; then
    echo "  META_APP_SECRET tem 32 caracteres de 0-9 e a-f; o que foi colado (${#v} caracteres) nao e' a chave. Nada foi alterado."
    exit 1
  fi
  if [ -n "$v" ]; then NOVAS[$k]=$v; fi
done

for k in "${!NOVAS[@]}"; do
  [[ "${NOVAS[$k]}" =~ ^[^[:space:]]+$ ]] || { echo "$k ficou com espaco ou quebra de linha; nada foi alterado."; exit 1; }
done

if [ ${#NOVAS[@]} -eq 0 ]; then echo "Nada para trocar."; exit 0; fi
echo "Vai trocar: ${!NOVAS[*]}"
read -r -p "Confirma? [s/N] " r
[[ "$r" =~ ^[sS] ]] || { echo "Cancelado."; exit 0; }

PAYLOAD=""
for k in "${!NOVAS[@]}"; do PAYLOAD+="$k=${NOVAS[$k]}"$'\n'; done

# 1) Manda as chaves pela entrada padrao (nao aparecem em ps nem no historico).
printf '%s' "$PAYLOAD" | ssh -i "$CHAVE_SSH" "$VM" 'umask 077; cat > ~/.novas-chaves'

# 2) Backup e troca no .env; o arquivo temporario e' apagado na hora.
ssh -i "$CHAVE_SSH" "$VM" 'cd ~/ConnectVeiculos && cp -p .env ".env.bak-$(date +%Y%m%d-%H%M%S)" && python3 -c "
import os
novas = dict(l.split(\"=\", 1) for l in open(os.path.expanduser(\"~/.novas-chaves\")).read().splitlines() if \"=\" in l)
os.remove(os.path.expanduser(\"~/.novas-chaves\"))
linhas = open(\".env\").read().splitlines()
feitas = set()
for i, l in enumerate(linhas):
    k = l.split(\"=\", 1)[0].strip()
    if k in novas and not l.lstrip().startswith(\"#\"):
        linhas[i] = k + \"=\" + novas[k]; feitas.add(k)
for k in novas:
    if k not in feitas: linhas.append(k + \"=\" + novas[k])
open(\".env\", \"w\").write(chr(10).join(linhas) + chr(10))
print(\"env atualizado:\", \", \".join(sorted(novas)))
"'

# 3) Arquivo de credenciais do PC (excluir-tenant.sh le o ADMIN_API_TOKEN dele).
if [ -f "$CRED" ]; then
  cp -p "$CRED" "$CRED.bak"
  for k in "${!NOVAS[@]}"; do
    if grep -q "^$k=" "$CRED"; then
      K="$k" V="${NOVAS[$k]}" awk 'BEGIN{k=ENVIRON["K"]; v=ENVIRON["V"]} index($0, k"=")==1 {print k"="v; next} {print}' "$CRED" > "$CRED.tmp" && mv "$CRED.tmp" "$CRED"
    else
      printf '%s=%s\n' "$k" "${NOVAS[$k]}" >> "$CRED"
    fi
  done
  rm -f "$CRED.bak"
  echo "credenciais do PC atualizadas"
fi

# 4) Reinicia so o backend e confere que subiu.
ssh -i "$CHAVE_SSH" "$VM" 'cd ~/ConnectVeiculos && sudo docker compose up -d backend-cliente 2>&1 | tail -2; sleep 20; sudo docker ps --filter name=backend-cliente --format "{{.Names}} {{.Status}}"; sudo docker logs --since 1m connectveiculos-backend-cliente-1 2>&1 | grep -iE "fatal|unhandled|JwtSettings" | tail -5'
echo "status do site: $(curl -s -o /dev/null -w '%{http_code}' https://connectveiculos.dev.br/api/dashboard) (401 = backend no ar)"
