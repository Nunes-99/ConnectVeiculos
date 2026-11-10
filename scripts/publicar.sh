#!/bin/bash
# ConnectVeiculos - publica em producao o que esta no master, com imagens
# prontas do GitHub Actions (.github/workflows/imagens.yml).
#
# Antes, a VM construia as imagens e o site ficava lento por ~13 min. Agora ela
# so baixa: o site fica fora do ar apenas os segundos de troca do container.
#
# Rodar no PC, a partir da raiz do repo, DEPOIS do push e com o workflow
# "Imagens" verde para o commit (gh run list --workflow=imagens.yml):
#   bash scripts/publicar.sh
#
# O que faz na VM, em segundo plano (a conexao SSH pode cair sem problema):
#   1. backup dos bancos em /home/ubuntu/backups/antes-publicar-<data>
#   2. git merge --ff-only origin/master
#   3. baixa as imagens do commit (tag = sha) e marca como :latest
#   4. recria backend-cliente e frontend-ssr (so os que mudaram)
#   5. apaga imagens antigas do sistema, cache de build e backups velhos
#      (disco da VM e pequeno: em 09/11/2026 encheu com 26 GB de cache de
#      build de quando a VM construia as imagens, e o deploy falhou)
# Log em /home/ubuntu/publicar.log; a ultima linha e "fim: ..." ou "ERRO: ...".
#
# Plano B, se o GitHub estiver fora: construir na VM como antes,
#   sudo docker compose up -d --build backend-cliente frontend-ssr

set -e

VM_IP="${VM_IP:-136.248.77.154}"
SSH_KEY="${SSH_KEY:-$HOME/.ssh/connectveiculos-oracle.key}"

ssh -i "$SSH_KEY" -o ServerAliveInterval=30 "ubuntu@$VM_IP" \
    'cat > /home/ubuntu/publicar-remoto.sh; nohup bash /home/ubuntu/publicar-remoto.sh > /home/ubuntu/publicar.log 2>&1 < /dev/null & echo "publicacao iniciada na VM (log: /home/ubuntu/publicar.log)"' << 'REMOTO'
set -e
falhou() { echo "ERRO: $1 ($(date -u))"; exit 1; }
cd /home/ubuntu/ConnectVeiculos

B=/home/ubuntu/backups/antes-publicar-$(date +%Y%m%d-%H%M)
mkdir -p "$B" && cp -a data "$B/" || falhou "backup"
echo "backup: $B"

git fetch -q origin && git merge --ff-only origin/master || falhou "merge"
SHA=$(git rev-parse HEAD)
git log --oneline -1

# Antes de baixar: cache de build nao serve para nada aqui (as imagens vem
# prontas) e sem espaco o pull falha no meio.
sudo docker builder prune -af > /dev/null 2>&1 || true
livre=$(df -BG --output=avail / | tail -1 | tr -dc '0-9')
echo "disco livre: ${livre}G"
[ "${livre:-0}" -ge 3 ] || falhou "disco quase cheio (${livre}G livres): rode 'sudo docker image prune -af' e 'df -h /'"

for nome in backend frontend; do
    img=ghcr.io/nunes-99/connectveiculos-$nome
    if ! sudo docker pull -q "$img:$SHA"; then
        df -h / | tail -1
        falhou "nao baixou a imagem $nome do commit $SHA (workflow Imagens terminou? disco cheio? veja df acima)"
    fi
    sudo docker tag "$img:$SHA" "$img:latest"
done

sudo docker compose up -d --no-build backend-cliente frontend-ssr || falhou "compose up"
sudo docker image prune -f > /dev/null
# Imagens de commits anteriores: o rollback baixa de novo do GitHub.
for nome in backend frontend; do
    img=ghcr.io/nunes-99/connectveiculos-$nome
    sudo docker images "$img" --format '{{.Repository}}:{{.Tag}}' \
        | grep -v -e ":$SHA\$" -e ':latest$' | xargs -r sudo docker rmi > /dev/null 2>&1 || true
done
# Backups antes de cada publicacao: fica com os 30 mais recentes.
ls -1dt /home/ubuntu/backups/antes-publicar-* 2>/dev/null | tail -n +31 | xargs -r rm -rf
df -h / | tail -1
sudo docker ps --format '{{.Names}} {{.Status}}'
echo "fim: $(date -u)"
REMOTO
