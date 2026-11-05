# Como atualizar as chaves do servidor

Chaves que valem para o sistema todo (não são de uma loja só) ficam no `.env`
do servidor. Elas **nunca passam pelo chat**: quem cola é você, no seu PC, e o
script leva até o servidor.

Chaves que o script conhece: `CREDERE_CLIENT_ID`, `CREDERE_CLIENT_SECRET`,
`CREDERE_BASE_URL`, `META_APP_SECRET`, `ML_CLIENT_SECRET`, `GEMINI_API_KEY`,
`MERCADOPAGO_ACCESS_TOKEN`, `MERCADOPAGO_WEBHOOK_SECRET` (e, se pedir, gera
`JWT_SECRET_KEY_CLIENTE` e `ADMIN_API_TOKEN`).

## Passo a passo

1. Abra o **Git Bash** (não o PowerShell: o script precisa de terminal para colar).
2. Vá para a pasta do projeto:

       cd ~/Documents/Vitor/ConnectVeiculos

3. Rode:

       bash scripts/trocar-chaves.sh

4. Responda às perguntas, uma por vez:
   - `Gerar nova JWT_SECRET_KEY_CLIENTE?` → **N** (se responder S, todo mundo
     precisa entrar de novo no sistema).
   - `Gerar novo ADMIN_API_TOKEN?` → **N**.
   - Para cada chave (`Novo META_APP_SECRET`, `Novo ML_CLIENT_SECRET`...):
     **Enter vazio mantém a atual**. Só cole nas que quer trocar.
     O que você cola **não aparece na tela**, é normal. Cole uma vez só
     (botão direito **ou** Ctrl+V); se colar duas vezes o script percebe e corrige.
5. Ele mostra `Vai trocar: ...` com os nomes das chaves. Confira e responda **s**.
6. Pronto. O script sozinho:
   - faz backup do `.env` no servidor (`.env.bak-AAAAMMDD-HHMMSS`);
   - grava as chaves novas;
   - atualiza o seu arquivo `ConnectVeiculos-CREDENCIAIS.txt`;
   - reinicia só o backend (o site fica uns 20 segundos sem a API).
7. No fim ele mostra `status do site: 401 (401 = backend no ar)`. **401 é o certo.**
   Se aparecer outro número, me mande a saída.

## Exemplo: chaves da Credere

No passo 4, Enter em tudo, menos:

| Pergunta | O que colar |
|---|---|
| `Novo CREDERE_CLIENT_ID` | o client_id que a Credere mandou |
| `Novo CREDERE_CLIENT_SECRET` | o client_secret |
| `Novo CREDERE_BASE_URL` | o endereço do **sandbox**, se for testar nele. Para produção: Enter (fica `https://app.meucredere.com.br/api/v1`) |

Depois de testar no sandbox, **volte para produção** antes de uma loja real
usar: rode de novo e, em `CREDERE_BASE_URL`, cole
`https://app.meucredere.com.br/api/v1`.

Em seguida: **Integrações → Credere → Conectar conta da Credere** (passo a
passo pela tela em `docs/credere-roteiro.md`, seção 4).

## Se algo der errado

- `Rode num terminal Git Bash`: abriu no lugar errado; use o Git Bash.
- `... nao e' a chave. Nada foi alterado.`: o que foi colado não tem o formato
  daquela chave; confira e rode de novo.
- Para voltar o `.env` anterior: o backup fica em
  `~/ConnectVeiculos/.env.bak-...` no servidor (peça para o Claude restaurar).
