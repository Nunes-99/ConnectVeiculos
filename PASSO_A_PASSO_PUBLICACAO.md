# Passo a Passo - Publicar o ConnectVeiculos

> Repositorio ja esta no GitHub: https://github.com/Nunes-99/ConnectVeiculos (privado)

---

## ETAPA 1: Criar conta na Oracle Cloud (GRATIS)

1. Acesse https://cloud.oracle.com/free
2. Clique em **"Start for Free"**
3. Preencha seus dados (precisa de cartao, mas **NAO cobra nada**)
4. Regiao: escolha **"Brazil East (Sao Paulo)"** se disponivel
5. Aguarde a ativacao (pode levar ate 30 min)

---

## ETAPA 2: Criar a VM (maquina virtual)

1. No painel Oracle: **Compute > Instances > Create Instance**
2. Configure:
   - Name: `connectveiculos`
   - Image: **Ubuntu 22.04** (Canonical)
   - Shape: Clique em **"Change shape"** > aba **"Ampere"** > **VM.Standard.A1.Flex**
   - OCPUs: **1** | Memory: **6 GB** (tudo Always Free)
3. Em **"Add SSH keys"**:
   - Marque **"Generate a key pair for me"**
   - Clique em **"Save Private Key"** - GUARDE ESSE ARQUIVO!
4. Clique em **Create**
5. Aguarde status **"RUNNING"**
6. Copie o **Public IP** (ex: `132.145.xxx.xxx`)

---

## ETAPA 3: Liberar portas no Oracle Cloud

1. Na pagina da instancia, clique no link **"Subnet"**
2. Clique em **"Default Security List"**
3. Clique em **"Add Ingress Rules"** e adicione:

| Source CIDR   | Protocol | Port |
|---------------|----------|------|
| 0.0.0.0/0     | TCP      | 80   |
| 0.0.0.0/0     | TCP      | 443  |

4. Salve

---

## ETAPA 4: Conectar na VM via SSH

**Abra o PowerShell (ou Git Bash) e rode:**

```powershell
# Primeiro, ajuste a permissao da chave (so precisa 1 vez)
icacls "C:\caminho\da\sua-chave.key" /inheritance:r /grant:r "%USERNAME%:R"

# Conectar
ssh -i "C:\caminho\da\sua-chave.key" ubuntu@SEU_IP
```

> Substitua `C:\caminho\da\sua-chave.key` pelo caminho real do arquivo .key que voce baixou
> Substitua `SEU_IP` pelo IP publico da VM

---

## ETAPA 5: Instalar Docker na VM

Cole estes comandos um por um:

```bash
sudo apt update && sudo apt upgrade -y
```

```bash
curl -fsSL https://get.docker.com -o get-docker.sh && sudo sh get-docker.sh
```

```bash
sudo usermod -aG docker $USER && sudo apt install docker-compose-plugin git -y
```

```bash
# Liberar firewall
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo apt install iptables-persistent -y
sudo netfilter-persistent save
```

**IMPORTANTE: Saia e reconecte:**
```bash
exit
```

```powershell
ssh -i "C:\caminho\da\sua-chave.key" ubuntu@SEU_IP
```

---

## ETAPA 6: Baixar e configurar o projeto

```bash
# Clonar (vai pedir usuario e senha/token do GitHub)
git clone https://github.com/Nunes-99/ConnectVeiculos.git
cd ConnectVeiculos
```

```bash
# Criar pastas
mkdir -p data uploads/dev uploads/cliente
```

```bash
# Configurar variaveis de ambiente
cp .env.example .env
nano .env
```

**No arquivo .env, altere as chaves JWT para algo unico:**
```
JWT_SECRET_KEY=MinhaChaveDevSecreta2024!@#ConnectVeiculosDev
JWT_SECRET_KEY_CLIENTE=MinhaChaveClienteSecreta2024!@#ConnectVeiculosCliente
```

Salvar: **Ctrl+O > Enter > Ctrl+X**

---

## ETAPA 7: Subir o sistema

```bash
docker compose up -d --build
```

> Primeira vez demora **5-10 minutos**. Aguarde.

```bash
# Verificar se esta rodando (deve mostrar 3 servicos "Up")
docker compose ps
```

```bash
# Ver logs se algo der errado
docker compose logs -f
```

---

## ETAPA 8: Acessar o sistema

### Ambiente DEV (seu teste):
- **URL:** `http://SEU_IP`
- **Login:** admin@connectveiculos.com.br
- **Senha:** admin123

### Ambiente CLIENTE (teste do cliente):
Para o cliente acessar um ambiente separado, configure um subdominio:
- `cliente.seudominio.com` -> aponta para o mesmo IP

Ou acesse direto e o Nginx roteia automaticamente.

---

## COMO FUNCIONA A SEPARACAO

| Ambiente | Backend | Banco de dados | Uploads |
|----------|---------|----------------|---------|
| DEV (voce) | backend-dev | data/dev.db | uploads/dev/ |
| CLIENTE | backend-cliente | data/cliente.db | uploads/cliente/ |

Os dados sao **100% separados**. O que voce faz no DEV nao afeta o cliente e vice-versa.

---

## CONFIGURAR DOMINIO (opcional, recomendado)

1. Compre um dominio (Registro.br ~R$40/ano)
2. No painel DNS, adicione:
   - `seudominio.com` -> A -> SEU_IP (ambiente dev)
   - `cliente.seudominio.com` -> A -> SEU_IP (ambiente cliente)
   - ou `app.seudominio.com` -> A -> SEU_IP (ambiente cliente)

3. Instale SSL:
```bash
sudo apt install certbot -y
sudo certbot certonly --standalone -d seudominio.com -d cliente.seudominio.com
```

---

## COMANDOS DO DIA A DIA

```bash
# Ver status
docker compose ps

# Ver logs
docker compose logs -f

# Reiniciar tudo
docker compose restart

# Parar tudo
docker compose down

# Atualizar para nova versao
git pull && docker compose up -d --build

# Backup dos bancos
cp data/dev.db ~/backup-dev-$(date +%Y%m%d).db
cp data/cliente.db ~/backup-cliente-$(date +%Y%m%d).db
```

---

## ADICIONAR MAIS CLIENTES NO FUTURO

Para cada novo cliente, adicione um novo servico no `docker-compose.yml`:

```yaml
  backend-novocliente:
    build:
      context: ./back-end
      dockerfile: Dockerfile
    environment:
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/novocliente.db
      - JwtSettings__SecretKey=ChaveUnicaDoNovoCliente123!
      # ... demais configs
    volumes:
      - ./data:/app/data
      - ./uploads/novocliente:/app/wwwroot/uploads
```

E adicione um bloco `server` no `nginx.conf` para o subdominio dele.

---

## RESOLUCAO DE PROBLEMAS

**Nao consigo acessar pelo IP:**
```bash
docker compose ps          # Esta rodando?
docker compose logs -f     # Tem erro?
sudo iptables -L -n        # Porta 80 esta aberta?
```

**Erro de build:**
```bash
docker compose down
docker compose up -d --build --force-recreate
```

**Sem espaco:**
```bash
docker system prune -a     # Limpa imagens antigas
df -h                      # Ver espaco em disco
```

**Esqueci a senha:**
O banco recria o admin padrao na primeira execucao.
Para resetar, delete o arquivo .db e reinicie.
