# Passo a Passo - Publicar o ConnectVeiculos

## ETAPA 1: Subir o codigo no GitHub (repositorio privado)

### 1.1 Criar repositorio no GitHub
1. Acesse https://github.com/new
2. Nome: `ConnectVeiculos`
3. Marque: **Private** (privado)
4. NAO marque "Add a README" (ja temos os arquivos)
5. Clique em **Create repository**

### 1.2 Subir o codigo (rodar no terminal, na pasta do projeto)
```bash
cd C:/Users/vitor.nunes/Documents/Vitor/Projetos/ConnectVeiculos
git init
git add .
git commit -m "Versao inicial do ConnectVeiculos"
git branch -M main
git remote add origin https://github.com/SEU_USUARIO/ConnectVeiculos.git
git push -u origin main
```

> Se pedir login, use seu usuario e um Personal Access Token
> (GitHub > Settings > Developer Settings > Personal Access Tokens > Generate new token)

---

## ETAPA 2: Criar VM gratis na Oracle Cloud

### 2.1 Criar conta
1. Acesse https://cloud.oracle.com/free
2. Clique em "Start for Free"
3. Preencha os dados (precisa de cartao de credito, mas NAO cobra)
4. Escolha a regiao "Brazil East (Sao Paulo)" se disponivel

### 2.2 Criar a VM (maquina virtual)
1. No painel Oracle, va em: **Compute > Instances > Create Instance**
2. Configure:
   - Name: `connectveiculos`
   - Image: **Ubuntu 22.04** (Canonical)
   - Shape: Clique em "Change shape" > **Ampere** > **VM.Standard.A1.Flex**
   - OCPUs: 1, Memory: 6 GB (tudo Always Free)
3. Em "Add SSH keys":
   - Selecione "Generate a key pair for me"
   - **BAIXE a chave privada** (arquivo .key) - GUARDE BEM, voce vai precisar
4. Clique em **Create**
5. Aguarde o status mudar para "RUNNING"
6. Anote o **Public IP Address** (ex: 132.145.xxx.xxx)

### 2.3 Liberar portas no Oracle Cloud
1. Na pagina da instancia, clique em **"Subnet"** (link azul)
2. Clique em **"Default Security List"**
3. Clique em **"Add Ingress Rules"**
4. Adicione estas regras:
   - Source CIDR: `0.0.0.0/0` | Protocol: TCP | Destination Port: `80`
   - Source CIDR: `0.0.0.0/0` | Protocol: TCP | Destination Port: `443`
5. Salve

---

## ETAPA 3: Configurar a VM

### 3.1 Conectar via SSH
**No Windows (PowerShell ou Git Bash):**
```bash
ssh -i C:/caminho/da/sua-chave.key ubuntu@IP_DA_VM
```

**Se der erro de permissao da chave no Windows:**
```powershell
icacls "C:\caminho\da\sua-chave.key" /inheritance:r /grant:r "%USERNAME%:R"
```

### 3.2 Instalar Docker na VM
```bash
# Atualizar sistema
sudo apt update && sudo apt upgrade -y

# Instalar Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER
sudo apt install docker-compose-plugin -y

# Instalar ferramentas uteis
sudo apt install git -y

# IMPORTANTE: Sair e entrar novamente para aplicar grupo docker
exit
```

### 3.3 Reconectar e clonar o projeto
```bash
ssh -i C:/caminho/da/sua-chave.key ubuntu@IP_DA_VM

# Clonar o repositorio (privado - vai pedir login)
git clone https://github.com/SEU_USUARIO/ConnectVeiculos.git
cd ConnectVeiculos

# Criar pastas necessarias
mkdir -p data uploads

# Configurar variaveis de ambiente
cp .env.example .env
nano .env
```

**No arquivo .env, altere a JWT_SECRET_KEY para uma chave unica:**
```
JWT_SECRET_KEY=SuaChaveSuperSecretaUnicaAqui2024!@#ConnectVeiculos
```
Salve com: `Ctrl+O, Enter, Ctrl+X`

### 3.4 Liberar firewall do Ubuntu
```bash
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo apt install iptables-persistent -y
sudo netfilter-persistent save
```

---

## ETAPA 4: Subir o sistema

```bash
cd ~/ConnectVeiculos

# Build e iniciar (primeira vez demora ~5-10 minutos)
docker compose up -d --build

# Verificar se esta rodando
docker compose ps

# Ver logs (Ctrl+C para sair)
docker compose logs -f
```

**Pronto!** Acesse: `http://IP_DA_VM`

Login padrao:
- Email: `admin@connectveiculos.com.br`
- Senha: `admin123`

---

## ETAPA 5: Configurar dominio (opcional mas recomendado)

### 5.1 Comprar dominio
- Registro.br (~R$40/ano para .com.br)
- Ou Hostinger/GoDaddy para .com

### 5.2 Configurar DNS
No painel do registrador de dominio:
- Adicione um registro **A** apontando para o IP da VM
- Exemplo: `connectveiculos.com.br` -> `132.145.xxx.xxx`

### 5.3 Instalar SSL (HTTPS)
```bash
sudo apt install certbot -y
sudo certbot certonly --standalone -d seudominio.com.br
```

---

## COMANDOS UTEIS DO DIA A DIA

```bash
# Ver se esta rodando
docker compose ps

# Ver logs
docker compose logs -f

# Reiniciar
docker compose restart

# Parar
docker compose down

# Atualizar para nova versao
cd ~/ConnectVeiculos
git pull
docker compose up -d --build

# Backup do banco de dados
cp data/*.db ~/backups/

# Ver uso de disco/memoria
df -h
free -h
```

---

## CRIAR NOVO CLIENTE

```bash
cd ~/ConnectVeiculos
chmod +x scripts/criar-cliente.sh
./scripts/criar-cliente.sh "Nome da Empresa" "email@admin.com" "senha123"
```

O catalogo do cliente ficara em: `seudominio.com/catalogo/nome-da-empresa`

---

## RESOLUCAO DE PROBLEMAS

### Sistema nao abre
```bash
docker compose logs -f  # Ver erros
docker compose down && docker compose up -d --build  # Reiniciar do zero
```

### Porta bloqueada
```bash
sudo iptables -L -n  # Ver regras
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
```

### Sem espaco em disco
```bash
docker system prune -a  # Limpar imagens antigas do Docker
```

### Esqueceu a senha admin
Acesse o banco SQLite e altere manualmente, ou delete o banco para recriar.
