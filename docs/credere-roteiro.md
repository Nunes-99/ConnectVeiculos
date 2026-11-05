# Credere: do contrato ao ar

Integração de financiamento multibanco. A loja **assina a Credere** e conecta a
conta dela no ConnectVeiculos (OAuth); a ficha de financiamento simula nos
bancos em que a loja já é credenciada. Documentação da API:
https://docs.meucredere.com.br/api/

O código está pronto (simulação). Este roteiro é o que falta para ligar.

## 1. Na reunião com a Credere

Sair com:

- [ ] Preço para o ConnectVeiculos como plataforma parceira (ou "sem custo").
- [ ] Se as simulações saem da mensalidade da loja ou têm cobrança extra.
- [ ] `client_id` e `client_secret` do ConnectVeiculos.
- [ ] As duas `redirect_uri` cadastradas:
  - `https://connectveiculos.dev.br/integracoes/credere` (produção)
  - `http://localhost:4200/integracoes/credere` (testes no PC)
- [ ] Sandbox: endereço da API de testes e um CPF de teste. Sem sandbox, a
      simulação vai aos bancos de verdade e faz pré-análise do CPF.
- [ ] Limite de 300 simulações/hora e 1.000/dia **por IP**: vale para todas as
      lojas somadas (tudo sai do nosso servidor). Pedir aumento se preciso.
- [ ] Se cada vendedor precisa de usuário na Credere (o sistema procura o
      vendedor pelo e-mail; sem achar, usa quem conectou a conta).
- [ ] Loja que usa a Credere pelo WCheck (caso da Diamante): pode ter conta
      própria aproveitando os acessos dos bancos?
- [ ] Proposta por escrito, por e-mail.

## 2. Guardar as chaves (no PC, Git Bash)

    bash scripts/trocar-chaves.sh

Cole `CREDERE_CLIENT_ID` e `CREDERE_CLIENT_SECRET` quando pedir (as outras
chaves: Enter para manter). Elas vão para o `.env` da VM e para o arquivo de
credenciais do PC. Nada passa pelo chat.

## 3. Testar a integração no PC (antes de publicar)

1. Pegar uma chave de acesso com a conta da loja (ou a conta de testes):

       bash scripts/credere-token.sh

   Abra o link que aparece, entre na Credere e cole o endereço para onde ela
   voltou (a página `localhost:4200` pode dar erro, tudo bem). Se a Credere
   passou um endereço de sandbox, rode antes `export CREDERE_BASE_URL=...`.

2. Leitura (conta, lojas, bancos e versões de veículo), sem simular:

       cd back-end
       CREDERE_TESTE_REAL=1 dotnet test ConnectVeiculos.Tests --filter CredereRealTests --logger "console;verbosity=detailed"

3. Simulação (vai aos bancos; usar o CPF de teste da Credere):

       CREDERE_TESTE_REAL=1 CREDERE_TESTE_SIMULAR=1 CREDERE_CPF_TESTE=<cpf de teste> \
         CREDERE_VENDEDOR_EMAIL=<e-mail do vendedor na Credere> \
         dotnet test ConnectVeiculos.Tests --filter CredereRealTests --logger "console;verbosity=detailed"

   Mande a saída para o Claude: ele confere contra o que a documentação diz e
   ajusta o que vier diferente.

Pontos que a documentação deixou dúbios e o teste confirma: PATCH x PUT no
lead, formato da data de nascimento, `users` x `user` na lista de vendedores,
status 200 x 201 na troca do código.

## 4. Publicar

1. Juntar a branch `feature/renave-nfe` no master (CI verde).
2. `bash scripts/publicar.sh`.
3. Em **Integrações → Credere**: Conectar conta, escolher a loja, conferir os
   bancos (acesso válido / inválido).
4. Em **Financiamento**: abrir uma ficha, buscar a versão do veículo e
   "Simular na Credere".

## Onde está o código

- Backend: `Infrastructure/Services/Credere/CredereService.cs`,
  `API/Controllers/CredereController.cs` (`/api/integracoes/credere`).
- Telas: cartão em Integrações (`shared/components/credere`), retorno do login
  (`pages/integracoes/credere-retorno.component.ts`) e a seção na ficha
  (`pages/financiamentos`).
- Testes: `CredereServiceTests` (Credere simulada) e `CredereRealTests` (real, opcional).
