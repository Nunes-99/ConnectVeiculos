# Deploy no Oracle Cloud Free Tier

## Pre-requisitos
- Conta Oracle Cloud (gratis: https://cloud.oracle.com/free)
- Git instalado

## Passo 1: Criar VM na Oracle Cloud
1. Acesse cloud.oracle.com > Compute > Instances > Create Instance
2. Escolha "Always Free Eligible" (ARM Ampere A1)
3. Shape: VM.Standard.A1.Flex (1 OCPU, 6GB RAM)
4. Image: Ubuntu 22.04
5. Gere e baixe a SSH key
6. Crie a instancia

## Passo 2: Configurar a VM
```bash
# Conectar via SSH
ssh -i sua-chave.key ubuntu@IP_DA_VM

# Instalar Docker
sudo apt update && sudo apt upgrade -y
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER
sudo apt install docker-compose-plugin -y

# Logout e login novamente para aplicar grupo docker
exit
ssh -i sua-chave.key ubuntu@IP_DA_VM
```

## Passo 3: Deploy da aplicacao
```bash
# Clonar o repositorio
git clone SEU_REPOSITORIO connectveiculos
cd connectveiculos

# Criar diretorio de dados
mkdir -p data uploads

# Configurar variaveis de ambiente
cp .env.example .env
nano .env  # Altere a JWT_SECRET_KEY

# Build e iniciar
docker compose up -d --build

# Verificar se esta rodando
docker compose ps
docker compose logs -f
```

## Passo 4: Abrir portas no Oracle Cloud
1. Acesse: Networking > Virtual Cloud Networks > sua VCN
2. Clique em "Security Lists" > "Default Security List"
3. Adicione Ingress Rules:
   - Source: 0.0.0.0/0, Protocol: TCP, Port: 80
   - Source: 0.0.0.0/0, Protocol: TCP, Port: 443

4. No Ubuntu, libere o firewall:
```bash
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save
```

## Passo 5: Configurar dominio (opcional)
1. Compre um dominio (ex: connectveiculos.com.br)
2. No painel DNS, adicione um registro A apontando para o IP da VM
3. Instale SSL com Certbot:
```bash
sudo apt install certbot python3-certbot-nginx -y
sudo certbot --nginx -d seudominio.com.br
```

## Criar novo cliente
```bash
./scripts/criar-cliente.sh "Auto Center SP" "admin@autocenter.com" "senha123"
```

## Comandos uteis
```bash
# Ver logs
docker compose logs -f

# Reiniciar
docker compose restart

# Atualizar
git pull
docker compose up -d --build

# Backup
cp data/*.db backups/
```
