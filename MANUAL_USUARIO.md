# ConnectVeiculos - Manual do Usuario

## O que e o ConnectVeiculos?

O ConnectVeiculos e um sistema completo de gestao para **revendas e concessionarias de veiculos**. Ele permite gerenciar todo o ciclo de compra e venda de veiculos, desde o cadastro do veiculo ate o controle financeiro e comissoes de vendedores.

---

## Primeiros Passos

### 1. Acessando o Sistema

1. Abra o navegador e acesse o endereco do sistema
2. Na tela inicial, voce vera a pagina de **Login**

### 2. Fazendo Login

1. Digite seu email
2. Digite sua senha
3. Clique em **"Entrar"**

### 3. Recuperando Senha

1. Clique em **"Esqueci minha senha"** na tela de login
2. Digite seu email cadastrado
3. Voce recebera um link por email para redefinir a senha
4. Clique no link e defina uma nova senha

---

## Navegacao Principal

Apos fazer login, voce vera o menu lateral (sidebar) com as seguintes opcoes:

| Menu | Funcao |
|------|--------|
| Dashboard | Visao geral do negocio |
| Veiculos | Gerenciar veiculos do estoque |
| Vendas | Registrar e acompanhar vendas |
| Usuarios | Gerenciar usuarios do sistema |
| Lojas | Cadastrar filiais/lojas |
| Categorias | Tipos de veiculos |
| Relatorios | Relatorios e exportacao |
| Catalogo | Catalogo publico de veiculos |
| Acessos | Niveis de permissao |
| Logs | Historico de acoes |

---

## Funcionalidades Detalhadas

### Dashboard

O Dashboard e sua pagina inicial apos o login. Aqui voce encontra:

#### Cards de Resumo
- **Total de Veiculos**: Quantidade de veiculos no estoque
- **Veiculos Disponiveis**: Prontos para venda
- **Veiculos Vendidos**: Total de vendas realizadas
- **Veiculos Reservados**: Veiculos com reserva

#### Graficos
- **Veiculos por Categoria**: Distribuicao do estoque por tipo (sedan, hatch, SUV, etc.)
- **Veiculos por Loja**: Quantidade e valor por filial
- **Status dos Veiculos**: Pizza com disponibilidade do estoque
- **Vendas por Periodo**: Evolucao das vendas (grafico de linha)
- **Faturamento Mensal**: Receita e lucro por mes (grafico de barras)

#### Dashboard Avancado
- **Comparativo Mensal**: Compara mes atual com anterior
- **Top 10 Veiculos**: Modelos mais vendidos
- **Filtros de Periodo**: Selecione 7, 15, 30 ou 90 dias

#### Veiculos Recentes
Lista dos ultimos veiculos cadastrados no sistema.

---

### Veiculos

#### Visualizando Veiculos
1. Clique em **"Veiculos"** no menu
2. Voce vera uma tabela com todos os veiculos cadastrados
3. Use a barra de busca para filtrar por marca, modelo, placa ou chassi
4. Use os filtros para ver por loja, categoria ou status

#### Cadastrando um Novo Veiculo
1. Clique no botao **"+ Novo Veiculo"**
2. Preencha os dados:
   - **Loja**: Selecione a loja onde o veiculo esta
   - **Categoria**: Tipo do veiculo (sedan, hatch, SUV, etc.)
   - **Marca**: Fabricante (Honda, Toyota, VW, etc.)
   - **Modelo**: Nome do modelo (Civic, Corolla, Golf, etc.)
   - **Ano**: Ano de fabricacao
   - **Placa**: Placa do veiculo (opcional)
   - **Chassi**: Numero do chassi (opcional)
   - **Cor**: Cor do veiculo
   - **KM**: Quilometragem atual
   - **Preco de Venda**: Valor para o cliente
   - **Preco de Compra**: Valor pago pelo veiculo (para calculo de lucro)
   - **Status**: Disponivel, Vendido ou Reservado
3. Clique em **"Salvar"**

#### Status dos Veiculos
- **Disponivel** (D): Pronto para venda
- **Vendido** (V): Ja foi vendido
- **Reservado** (R): Cliente fez reserva

#### Gerenciando Imagens
1. Na lista de veiculos, clique no icone de **camera** do veiculo
2. Na janela de imagens:
   - Clique em **"Selecionar Imagens"** para adicionar fotos
   - Formatos aceitos: JPG, PNG, GIF, WEBP
   - Tamanho maximo: 5MB por imagem
   - Voce pode enviar varias imagens de uma vez
3. Clique em **"Enviar Todas"** para fazer upload
4. Para remover uma imagem, clique no icone de **lixeira**

#### Editando um Veiculo
1. Clique no icone de **editar** (lapis) do veiculo
2. Faca as alteracoes necessarias
3. Clique em **"Salvar"**

#### Excluindo um Veiculo
1. Clique no icone de **lixeira** do veiculo
2. Confirme a exclusao na janela que aparecer

---

### Vendas

#### Visualizando Vendas
1. Clique em **"Vendas"** no menu
2. Veja a lista de todas as vendas realizadas
3. Informacoes exibidas: Veiculo, Vendedor, Comprador, Valor, Data, Status

#### Registrando uma Venda
1. Clique em **"+ Nova Venda"**
2. Preencha os dados:

   **Dados da Venda:**
   - **Veiculo**: Selecione o veiculo (apenas disponiveis)
   - **Vendedor**: Quem realizou a venda
   - **Data da Venda**: Data da transacao
   - **Valor**: Valor final (pre-preenchido com preco do veiculo)
   - **% Comissao**: Percentual de comissao do vendedor (padrao 5%)

   **Dados do Comprador:**
   - **Nome Completo**: Nome do comprador
   - **CPF**: CPF do comprador
   - **Telefone**: Telefone para contato
   - **Email**: Email do comprador
   - **Endereco**: Endereco completo

   **Pagamento:**
   - **Forma de Pagamento**: Dinheiro, PIX, Cartao, Financiamento, etc.
   - **Observacao**: Informacoes adicionais

3. O sistema calcula automaticamente a **comissao** do vendedor
4. Clique em **"Registrar Venda"**

#### Formas de Pagamento
- Dinheiro
- PIX
- Cartao de Credito
- Cartao de Debito
- Financiamento
- Consorcio
- Troca
- Misto

#### Status das Vendas
- **Ativa** (A): Venda valida
- **Estornada** (E): Venda cancelada/estornada

#### Estornando uma Venda
1. Encontre a venda na lista
2. Clique no botao **"Estornar"**
3. Confirme o estorno
4. O veiculo voltara para o status **Disponivel**

---

### Usuarios

#### Visualizando Usuarios
1. Clique em **"Usuarios"** no menu
2. Use a busca para encontrar por nome, email ou CPF
3. Use a paginacao para navegar entre os registros

#### Cadastrando um Usuario
1. Clique em **"+ Novo Usuario"**
2. Preencha os dados:
   - **Loja**: Loja onde o usuario trabalha
   - **Nivel de Acesso**: Permissoes do usuario
   - **Nome Completo**
   - **CPF**
   - **RG**
   - **Email**: Sera usado como login
   - **Senha**: Minimo 6 caracteres
   - **Status**: Ativo ou Inativo
3. Clique em **"Salvar"**

#### Niveis de Acesso (Funcoes)
- **Administrador**: Acesso total ao sistema
- **Gerente**: Acesso a todas as operacoes da loja
- **Vendedor**: Acesso limitado (veiculos e vendas)

#### Editando um Usuario
1. Clique no icone de **editar** (lapis)
2. Faca as alteracoes (a senha e opcional na edicao)
3. Clique em **"Salvar"**

---

### Lojas

#### Visualizando Lojas
1. Clique em **"Lojas"** no menu
2. Veja a lista de todas as lojas/filiais cadastradas

#### Cadastrando uma Loja
1. Clique em **"+ Nova Loja"**
2. Preencha os dados:
   - **Nome da Loja**
   - **Endereco**: Logradouro, numero, bairro, cidade, estado, CEP
   - **Contatos**: Email, telefones, WhatsApp
   - **CNPJ**
   - **Inscricao Estadual**
   - **Status**: Ativa ou Inativa
3. Clique em **"Salvar"**

---

### Categorias

Categorias sao os tipos de veiculos que sua loja trabalha.

#### Exemplos de Categorias
- Sedan
- Hatch
- SUV
- Picape
- Motocicleta
- Caminhao
- Utilitario

#### Cadastrando uma Categoria
1. Clique em **"Categorias"** no menu
2. Clique em **"+ Nova Categoria"**
3. Preencha:
   - **Nome**: Nome da categoria
   - **Descricao**: Descricao opcional
   - **Status**: Ativa ou Inativa
4. Clique em **"Salvar"**

---

### Relatorios

A pagina de relatorios oferece tres tipos de relatorios completos:

#### Relatorio de Vendas
Mostra o desempenho de vendas:
- **Resumo**: Total de vendas, valor total, comissoes, vendas estornadas
- **Vendas por Mes**: Evolucao mensal
- **Vendas por Vendedor**: Ranking de vendedores com comissoes
- **Vendas por Forma de Pagamento**: Distribuicao por tipo de pagamento

#### Relatorio de Estoque
Mostra a situacao do estoque:
- **Resumo**: Total de veiculos, disponiveis, valor total, valor medio
- **Estoque por Loja**: Quantidade e valor por filial
- **Estoque por Categoria**: Quantidade e valor por tipo
- **Estoque por Marca**: Quantidade e valor por fabricante

#### Relatorio Financeiro
Mostra os resultados financeiros:
- **Receita Bruta**: Total de vendas
- **Custo Total**: Valor de compra dos veiculos
- **Lucro Bruto**: Receita menos custo
- **Lucro Liquido**: Lucro menos comissoes
- **Margem de Lucro**: Percentual de lucro
- **Ticket Medio**: Valor medio por venda
- **Financeiro por Mes**: Evolucao mensal
- **Financeiro por Loja**: Resultado por filial

#### Filtros
- **Data Inicio**: Data inicial do periodo
- **Data Fim**: Data final do periodo
- **Loja**: Filtrar por loja especifica
- **Categoria**: Filtrar por categoria (estoque)

#### Exportando Relatorios
1. Selecione o relatorio desejado
2. Aplique os filtros
3. Clique em **"Exportar PDF"** ou **"Exportar Excel"**
4. O arquivo sera baixado automaticamente

---

### Catalogo

O Catalogo e uma pagina publica para exibir os veiculos disponiveis para clientes.

#### Funcionalidades do Catalogo
- **Visualizacao em Cards**: Fotos e informacoes principais
- **Filtros**: Marca, ano, preco
- **WhatsApp**: Botao para contato direto com a loja

#### Como Funciona
1. Clientes acessam o catalogo pelo link publico
2. Navegam pelos veiculos disponiveis
3. Clicam em **"WhatsApp"** para entrar em contato
4. A mensagem e pre-preenchida com os dados do veiculo

---

### Acessos

A pagina de Acessos permite configurar os niveis de permissao do sistema.

#### Niveis Padrao
- **Administrador**: Acesso total
- **Gerente**: Acesso gerencial
- **Vendedor**: Acesso operacional

#### Cadastrando um Nivel de Acesso
1. Clique em **"+ Novo Acesso"**
2. Defina o nome do nivel
3. Selecione as permissoes
4. Clique em **"Salvar"**

---

### Logs

A pagina de Logs mostra o historico de acoes realizadas no sistema.

#### Informacoes Registradas
- **Usuario**: Quem realizou a acao
- **Acao**: O que foi feito (criar, editar, excluir)
- **Entidade**: Onde foi feito (veiculo, venda, usuario)
- **Data/Hora**: Quando foi feito
- **Detalhes**: Informacoes adicionais

---

## Notificacoes em Tempo Real

O sistema possui notificacoes em tempo real usando SignalR:

- **Icone de Sino**: No topo da tela
- **Badge**: Mostra quantidade de notificacoes nao lidas
- **Tipos de Notificacoes**:
  - Nova venda registrada
  - Novo veiculo cadastrado
  - Veiculo reservado

---

## Controle de Acesso (RBAC)

O sistema possui controle de acesso por nivel de usuario:

| Funcionalidade | Vendedor | Gerente | Administrador |
|----------------|----------|---------|---------------|
| Dashboard | Sim | Sim | Sim |
| Veiculos (visualizar) | Sim | Sim | Sim |
| Veiculos (criar/editar) | Nao | Sim | Sim |
| Vendas | Sim | Sim | Sim |
| Usuarios | Nao | Nao | Sim |
| Lojas | Nao | Sim | Sim |
| Categorias | Nao | Sim | Sim |
| Relatorios | Nao | Sim | Sim |
| Acessos | Nao | Nao | Sim |
| Logs | Nao | Nao | Sim |

---

## Dicas e Boas Praticas

1. **Cadastre todas as lojas** antes de adicionar veiculos e usuarios
2. **Crie as categorias** que sua loja trabalha
3. **Adicione fotos de qualidade** aos veiculos para o catalogo
4. **Mantenha o preco de compra** atualizado para relatorios precisos
5. **Verifique o Dashboard diariamente** para acompanhar vendas
6. **Exporte relatorios mensalmente** para controle financeiro
7. **Use o catalogo publico** para divulgar nas redes sociais

---

## Suporte

Se precisar de ajuda:
1. Verifique este manual
2. Consulte os tooltips (icones de interrogacao) no sistema
3. Entre em contato com o administrador do sistema

---

*Manual atualizado em: 02/01/2026*
*Versao do sistema: 1.0*
