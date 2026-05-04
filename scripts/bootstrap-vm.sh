#!/bin/bash
#
# ConnectVeiculos - Bootstrap inicial da VM Oracle (Ubuntu 22.04)
#
# Roda UMA VEZ no primeiro deploy. Faz:
#   1. Atualiza Ubuntu
#   2. Instala Docker + plugin compose + iptables-persistent
#   3. Libera portas 80 e 443 no firewall do Ubuntu
#   4. Adiciona usuario ao grupo docker
#   5. Clona o repo (precisa Personal Access Token do GitHub)
#   6. Mostra proximos passos
#
# Uso (na VM):
#   GITHUB_TOKEN=ghp_xxxxxxxxxxxxx bash bootstrap-vm.sh
#
# OU baixe e execute em uma linha:
#   GITHUB_TOKEN=ghp_xxxxxxxxxxxxx bash <(curl -s https://raw.githubusercontent.com/Nunes-99/ConnectVeiculos/master/scripts/bootstrap-vm.sh)

set -e

# --- Config (sobrescriva via env vars antes de rodar) ---
GITHUB_USER="${GITHUB_USER:-Nunes-99}"
GITHUB_REPO="${GITHUB_REPO:-ConnectVeiculos}"
GITHUB_TOKEN="${GITHUB_TOKEN:-}"
INSTALL_DIR="${INSTALL_DIR:-$HOME/$GITHUB_REPO}"

# --- Sanidade ---
if [ -z "$GITHUB_TOKEN" ]; then
    echo "❌ ERRO: variavel GITHUB_TOKEN nao definida."
    echo
    echo "Como gerar:"
    echo "  1. GitHub → Settings → Developer settings → Personal access tokens → Fine-grained"
    echo "  2. Generate new token → Repository: Nunes-99/ConnectVeiculos"
    echo "  3. Permissions: Contents (Read), Metadata (Read)"
    echo "  4. Rode novamente: GITHUB_TOKEN=ghp_xxx bash bootstrap-vm.sh"
    exit 1
fi

if ! command -v sudo &>/dev/null; then
    echo "❌ ERRO: sudo nao disponivel."
    exit 1
fi

echo "════════════════════════════════════════════════════════════"
echo "  ConnectVeiculos — Bootstrap VM"
echo "  Repo: $GITHUB_USER/$GITHUB_REPO"
echo "  Diretorio: $INSTALL_DIR"
echo "════════════════════════════════════════════════════════════"
echo

# --- 1. Atualizar sistema ---
echo "==> [1/6] Atualizando Ubuntu..."
# Retenta uma vez se mirror estiver em sync, e como ultimo recurso ignora
# arquivos de traducao (i18n) que sao a causa mais comum de "unexpected size".
# Esses arquivos nao sao usados na instalacao real de pacotes.
sudo apt-get update -qq \
    || sudo apt-get update -qq -o Acquire::Languages=none \
    || echo "  ⚠️  apt-get update parcial (mirror sync); continuando"
sudo DEBIAN_FRONTEND=noninteractive apt-get upgrade -y -qq -o Acquire::Languages=none

# --- 2. Instalar Docker se nao estiver presente ---
echo "==> [2/6] Instalando Docker..."
if ! command -v docker &>/dev/null; then
    curl -fsSL https://get.docker.com | sudo sh
    echo "  ✓ Docker instalado"
else
    echo "  ✓ Docker ja presente: $(docker --version)"
fi

# Pacotes complementares
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq \
    docker-compose-plugin \
    git \
    iptables-persistent \
    netfilter-persistent \
    curl

# --- 3. Liberar firewall do Ubuntu ---
echo "==> [3/6] Liberando portas 80, 443 e 22..."
sudo iptables -C INPUT -p tcp --dport 80 -j ACCEPT 2>/dev/null || \
    sudo iptables -I INPUT 6 -p tcp --dport 80 -j ACCEPT

sudo iptables -C INPUT -p tcp --dport 443 -j ACCEPT 2>/dev/null || \
    sudo iptables -I INPUT 6 -p tcp --dport 443 -j ACCEPT

sudo netfilter-persistent save >/dev/null
echo "  ✓ Regras iptables salvas"

echo "  ⚠️  IMPORTANTE: tambem libere portas 80 e 443 no Oracle Security List:"
echo "      Console Oracle → Networking → VCN → Security Lists → Default → Ingress Rules"
echo "      Source: 0.0.0.0/0  Protocol: TCP  Destination Port: 80"
echo "      Source: 0.0.0.0/0  Protocol: TCP  Destination Port: 443"

# --- 4. Adicionar usuario ao grupo docker ---
echo "==> [4/6] Configurando grupo docker..."
if ! id -nG "$USER" | grep -qw docker; then
    sudo usermod -aG docker "$USER"
    NEED_RELOGIN=1
fi

# --- 5. Clonar repo ---
echo "==> [5/6] Clonando $GITHUB_USER/$GITHUB_REPO..."
if [ -d "$INSTALL_DIR/.git" ]; then
    cd "$INSTALL_DIR"
    git pull origin master
    echo "  ✓ Repo atualizado em $INSTALL_DIR"
else
    git clone "https://${GITHUB_TOKEN}@github.com/${GITHUB_USER}/${GITHUB_REPO}.git" "$INSTALL_DIR"
    cd "$INSTALL_DIR"
    echo "  ✓ Repo clonado em $INSTALL_DIR"
fi

# Remove o token da config remota (segurança — nao deixa cached no disco)
git remote set-url origin "https://github.com/${GITHUB_USER}/${GITHUB_REPO}.git"

# --- 6. Verificar .env ---
echo "==> [6/6] Verificando .env..."
if [ ! -f .env ]; then
    cp .env.example .env 2>/dev/null || true
    chmod 600 .env
    NEED_ENV=1
fi

echo
echo "════════════════════════════════════════════════════════════"
echo "  ✅ Bootstrap concluido!"
echo "════════════════════════════════════════════════════════════"
echo

if [ "$NEED_RELOGIN" = "1" ]; then
    echo "⚠️  PROXIMO PASSO 1: faca logout e login de novo no SSH para aplicar grupo docker"
    echo "    exit"
    echo "    ssh -i sua-chave.key ubuntu@$(curl -s ifconfig.me)"
    echo
fi

if [ "$NEED_ENV" = "1" ]; then
    echo "⚠️  PROXIMO PASSO 2: edite .env com suas credenciais"
    echo "    cd $INSTALL_DIR"
    echo "    nano .env"
    echo "    (cole o bloco do arquivo CONNECTVEICULOS-CREDENCIAIS.txt no seu PC)"
    echo
fi

echo "PROXIMO PASSO 3: subir o stack"
echo "    cd $INSTALL_DIR"
echo "    sudo docker compose up -d --build"
echo "    sudo docker compose ps    # confirmar containers Up"
echo
echo "PROXIMO PASSO 4 (apos DNS connectveiculos.dev.br apontado e propagado):"
echo "    sudo bash scripts/setup-https.sh"
echo
echo "Verificar IP publico: $(curl -s ifconfig.me 2>/dev/null || echo 'erro detectando')"
echo "════════════════════════════════════════════════════════════"
