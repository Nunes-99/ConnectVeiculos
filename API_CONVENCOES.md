# Convenções de Nomenclatura — API ConnectVeículos

Resumo: a API tem duas convenções de naming, herdadas do histórico do projeto. Conhecer ambas evita confusão na hora de integrar.

## Grupo A — Entidades de domínio centrais

Usam **prefixo de 3 letras** + camelCase. Refletem nomes de colunas do banco.

| Entidade | Prefixo | Exemplos de campos |
|---|---|---|
| Usuario | `Usu*` | `usuNome`, `usuEmail`, `usuSenha`, `usuFuncao`, `usuSts` |
| Veiculo | `Vei*` | `veiPlaca`, `veiMarca`, `veiModelo`, `veiAno`, `veiPreco` |
| Venda | `Ven*` | `venValor`, `venDtVenda`, `venCompradorNome`, `venFormaPagamento` |
| Loja | `Loj*` | `lojNome`, `lojSlug`, `lojCNPJ`, `lojUrlCatalogo` |
| Categoria | `Cat*` | `catNome`, `catDesc`, `catSts` |
| Acesso | `Acs*` | `acsNome`, `acsDesc`, `acsSts` |

**FKs** usam prefixo `R_` (de "referência"): `r_LojId`, `r_CatId`, `r_VeiId`, `r_UsuId`, `r_AcsId`.

## Grupo B — Entidades operacionais

Usam **camelCase limpo** sem prefixo. Foram adicionadas depois.

| Entidade | Endpoints | Exemplos de campos |
|---|---|---|
| Lead | `POST /api/leads` | `nomeCliente`, `telefone`, `email`, `veiculoId`, `lojaId`, `origem` |
| TestDrive | `POST /api/testdrives` | `nomeCliente`, `telefone`, `whatsApp`, `dataAgendamento`, `horario` |
| Despesa | `POST /api/despesas` | `veiculoId`, `tipo`, `descricao`, `valor`, `dataDespesa` |
| Documento | `POST /api/veiculos-documentos` | `veiculoId`, `tipo`, `status`, `arquivo`, `observacao`, `dataVencimento` |
| Favorito | `POST /api/favoritos` | `veiculoId`, `email`, `nome`, `telefone` |
| Negociação | `POST /api/negociacoes` | `r_VeiId`, `negNomeCliente`, `negValorOferta`, `negStatus` *(híbrido — usa prefixo)* |

## Status (enums string)

| Entidade | Campo | Valores aceitos |
|---|---|---|
| Veiculo | `veiSts` | `D` (Disponível), `R` (Reservado), `V` (Vendido), `I` (Inativo) |
| Venda | `venFormaPagamento` | `DINHEIRO`, `PIX`, `CARTAO_CREDITO`, `CARTAO_DEBITO`, `FINANCIAMENTO`, `CONSORCIO`, `TROCA`, `MISTO` |
| Documento | `tipo` | `CRLV`, `IPVA`, `LAUDO`, `TRANSFERENCIA`, `SEGURO`, `MULTA`, `FINANCIAMENTO`, `OUTROS` |
| Documento | `status` | `PENDENTE`, `EM_DIA`, `VENCIDO`, `CONCLUIDO` |

## Multi-tenancy

Todas as requests autenticadas e públicas podem informar o tenant de 3 formas (precedência: query string > header > subdomínio):

1. **Query string** — `?tenant=inova-motor` (recomendado pra rotas públicas como `/api/catalogo`)
2. **Header HTTP** — `X-Tenant-Slug: inova-motor` (usado pelo frontend admin via interceptor automático)
3. **Subdomínio** — `inova-motor.connectveiculos.dev.br` (precisa de DNS wildcard, opcional)

## Validações por entidade

| Campo | Regra | Erro retornado |
|---|---|---|
| `veiPlaca` | Mercosul (`ABC1D23`) ou antiga (`ABC-1234`) | "Placa invalida. Formatos aceitos: ABC-1234 ou ABC1D23 (Mercosul)." |
| `veiChassi` | 17 caracteres VIN com dígito verificador válido | "Chassi invalido. Deve ter 17 caracteres alfanumericos." |
| `venCompradorCpf` | CPF (11 dígitos) ou CNPJ (14 dígitos) com dígito verificador | "CPF/CNPJ do comprador invalido." |
| `usuEmail` / `email` | Formato `local@dominio` + único globalmente (UserEmailMap no master) | "Este e-mail ja esta em uso em outra empresa do sistema." |
| `dataAgendamento` (TestDrive) | Data >= hoje | "Data de agendamento nao pode ser no passado." |
| `valor` (Despesa) | > 0 | "Valor da despesa deve ser maior que zero." |
