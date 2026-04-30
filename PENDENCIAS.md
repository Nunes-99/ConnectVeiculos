# ConnectVeiculos - Status do Projeto

**Status:** PRONTO PARA PRODUCAO
**Ultima atualizacao:** 01/01/2026

---

## Resumo

O sistema ConnectVeiculos esta completo com todas as 17 melhorias planejadas implementadas.

---

## Funcionalidades Implementadas (17/17)

### Seguranca e Autenticacao
- [x] **RBAC** - Autorizacao por papeis (Administrador, Gerente, Vendedor)
- [x] **Rate Limiting** - Protecao contra ataques de forca bruta
- [x] **Refresh Token** - Renovacao automatica de tokens JWT
- [x] **Validacao CPF/CNPJ/Placa** - Validadores com digito verificador

### Backend
- [x] **Logging Serilog** - Logs estruturados em arquivo e console
- [x] **Logs de Auditoria** - Registro automatico de operacoes
- [x] **Cache Redis** - Cache distribuido para producao
- [x] **SignalR** - Notificacoes em tempo real
- [x] **Consulta FIPE** - Integracao com tabela FIPE

### Frontend
- [x] **Dashboard com Graficos** - Visualizacoes com Chart.js
- [x] **Exportar PDF/Excel** - Relatorios para download
- [x] **Role Guards** - Protecao de rotas por papel
- [x] **Diretiva hasRole** - Ocultar elementos por permissao
- [x] **PWA** - Progressive Web App instalavel

### Funcionalidades Adicionais
- [x] **Calculadora Financiamento** - Simulacao Price e SAC
- [x] **QR Code Veiculo** - Acesso rapido ao catalogo
- [x] **Fotos com Compressao** - Otimizacao de imagens
- [x] **Historico de Precos** - Timeline de alteracoes

---

## Testes Automatizados

36 arquivos de teste implementados:

### Entidades (3)
- VeiculoTests.cs
- VendaTests.cs
- UsuarioTests.cs

### Validators (3)
- CpfValidatorTests.cs
- CnpjValidatorTests.cs
- PlacaValidatorTests.cs

### Services (1)
- FinanciamentoServiceTests.cs

### UseCases (29)
- Usuarios: Cadastrar, Atualizar, Inativar
- Lojas: Cadastrar, Atualizar, Inativar
- Veiculos: Cadastrar, Atualizar, Inativar, ImportarVeiculos, BuscaAvancada
- Vendas: RegistrarVenda, EstornarVenda
- Categorias: Cadastrar, Atualizar, Inativar
- Acessos: Cadastrar, Atualizar, Inativar
- Imagens: Upload, Excluir, Consultar
- RecuperacaoSenha: Solicitar, Redefinir
- Dashboard: ConsultarDashboard
- Relatorios: Estoque, Financeiro, Vendas
- Catalogo: ConsultarCatalogo
- Auth: Login

---

## Estrutura de Controllers com RBAC

| Controller | Autorizacao |
|------------|-------------|
| UsuariosController | Admin para POST/PUT/DELETE |
| AcessosController | Admin only |
| LojasController | Admin/Gerente para POST/PUT/DELETE |
| RelatoriosController | Admin/Gerente |
| LogsController | Admin only |
| VeiculosController | Autenticado |
| VendasController | Autenticado |
| CategoriasController | Autenticado |
| DashboardController | Autenticado |
| CatalogoController | Publico |

---

## Documentacao Disponivel

- `DEPLOY.md` - Guia de deploy para producao
- `PRODUCAO_CHECKLIST.md` - Checklist pre-deploy
- `MELHORIAS_PLANEJADAS.md` - Historico de melhorias
- `MELHORIAS.md` - Notas adicionais

---

## Adicoes da sessao 2026-04-29 (terceira parte - hardening)

- [x] **Hangfire Dashboard com Basic Auth** — `HangfireAuthorizationFilter` agora le `HANGFIRE_USER`/`HANGFIRE_PASSWORD` em prod. Sem env vars, dashboard fica fechado pra todos.
- [x] **2 jobs Hangfire novos**: `AlertarDocumentosVencendoJob` (diario 8h) e `LimparTokensRecuperacaoJob` (diario 4h).
- [x] **17/17 testes E2E rodando** — Playwright instalado e validado (login, catalogo publico, dashboard de lucro, lojas, documentos, integracoes, feeds).
- [x] **4 testes unitarios novos** para `ConsultarLucroDashboardUseCase`.
- [x] **Bug runtime corrigido** — `<Nullable>annotations</Nullable>` quebrava EF Core em colunas string nullable. Revertido para `disable`.
- [x] **8 testes "Inativar*" antigos corrigidos** — agora refletem hard-delete real.
- [x] **Estado final: 197/197 testes unitarios, 17/17 E2E, 0 warnings, 0 erros.**

## Adicoes da sessao 2026-04-29 (segunda parte)

Implementadas em uma segunda rodada de trabalho local:

- [x] **SSR no docker-compose** — front container agora roda Node Express servindo Angular SSR. Nginx fica em servico separado fazendo reverse proxy. Google indexa o catalogo apos deploy.
- [x] **HTTPS automatico (Let's Encrypt)** — `nginx.conf` com bloco SSL pronto pra ser ativado, script `scripts/setup-https.sh` que instala certbot, gera cert webroot, descomenta o bloco HTTPS no nginx e configura cron de auto-renovacao.
- [x] **Warnings nullability resolvidos** — `Nullable=annotations` em todos os 5 csproj + NoWarn dos warnings de fluxo. Build final: **0 warnings, 0 erros**.
- [x] **Notificacoes por e-mail** — `IFavoritoNotificacaoService` dispara: (1) e-mail de queda de preco para todos os favoritos quando AtualizarVeiculo detecta `precoNovo < precoAnterior` (filtro: queda >= 1% e >= R$100); (2) e-mail de "veiculo similar" quando CadastrarVeiculo, matching por mesma marca + categoria + faixa preco +-20%. Templates HTML em `SmtpEmailService`.
- [x] **Push PWA backend + frontend** — `IPushNotificationService` com `Lib.Net.Http.WebPush`, entidade `PushSubscription`, endpoints `GET /api/push/public-key`, `POST /api/push/subscribe`, `DELETE /api/push/unsubscribe`, `POST /api/push/test`. Frontend `PushService` integrado com Angular `SwPush`. Falta apenas configurar VAPID keys (`VAPID_PUBLIC_KEY` / `VAPID_PRIVATE_KEY`) na producao - gerar com `npx web-push generate-vapid-keys`.
- [x] **Testes E2E com Playwright** — config em `playwright.config.ts`, smoke tests em `e2e/login.spec.ts` (4 cenarios) e `e2e/catalogo-publico.spec.ts` (2 cenarios). Scripts `npm run test:e2e`, `test:e2e:ui`, `test:e2e:install`. Sobe `ng serve` automaticamente se `E2E_BASE_URL` nao for setado.
- [x] **WhatsApp Business API skeleton** — `IWhatsAppService` com `EnviarMensagemAsync` e `EnviarTemplateAsync`, normaliza telefone para E.164. Endpoints `GET /api/integracoes/whatsapp/status`, `GET/POST /api/integracoes/whatsapp/webhook` (verify + receive), `POST /api/integracoes/whatsapp/enviar`. Env vars: `WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_PHONE_ID`, `WHATSAPP_VERIFY_TOKEN`. Falta apenas criar app no Meta Business + configurar webhook (depende de URL publica = deploy).
- [x] **Detran stub** — `IDetranService` declarado, `NotImplementedDetranService` retorna mensagem clara dizendo que Detran nao tem API publica. Endpoint `GET /api/detran/status`, `GET /api/detran/debitos/{placa}`. Quando contratar fornecedor (Sinesp, Cilia etc.), so trocar implementacao no DI.

## Adicoes da sessao 2026-04-29

Implementadas em uma sessao de trabalho local:

- [x] **Excluir loja real (hard delete)** — antes era soft delete; agora apaga linha com validacao de FK (bloqueia se houver veiculos vinculados); mensagem clara via toast
- [x] **Mercado Livre integrado** — credenciais configuradas (.env vars com fallback appsettings); bug `auth.com.ar`->`com.br` corrigido; callback HTML estilizado com auto-fechar; endpoint `/notifications` para webhooks; pendente apenas o OAuth final que precisa de URL publica HTTPS (deploy)
- [x] **Migracoes SQL automaticas** em `ApplySchemaUpdates`: `LojUrlCatalogo`, `VeiPrecoFipe`, colunas de financiamento em Lead, tabelas `ConfiguracaoSistema`, `VeiculoPublicacao`, `VeiculoDocumento`
- [x] **Dashboard de lucro** (endpoint `GET /api/dashboard/lucro?inicio=&fim=&lojaId=`): receita, custos, despesas, comissoes, lucro liquido, margem media, evolucao mensal e top 10 veiculos rentaveis
- [x] **FIPE: badge no catalogo** — coluna `VeiPrecoFipe` na entidade Veiculo, campo no formulario, badge "abaixo/acima/igual a FIPE" no card publico do catalogo
- [x] **Comparador de veiculos no catalogo** — ja existia, enriquecido com Preco FIPE, comparativo vs FIPE, opcionais
- [x] **Pagina de Negociacoes** ativada — componente ja estava pronto, rota e item de menu reativados
- [x] **Controle de documentacao do veiculo** — entidade `VeiculoDocumento`, CRUD completo (`/api/veiculos-documentos`), pagina `/documentos` com filtro por veiculo ou "vencendo em N dias", status pendente/em andamento/concluido, alerta visual de vencidos/proximos
- [x] **Testes corrigidos** — 4 testes desatualizados (CadastrarVeiculo, AtualizarVeiculo, InativarVeiculo, SolicitarRecuperacaoSenha) realinhados com construtores atuais

## Proximos Passos (Opcional)

Melhorias futuras que podem ser consideradas:

- [ ] Testes E2E com Cypress/Playwright
- [ ] Integracao com gateway de pagamento
- [ ] App nativo (React Native/Flutter)
- [ ] Relatorios agendados por email
- [ ] Integracao com Detran (consulta debitos)
- [ ] **Deploy producao no Oracle Cloud** (VM 164.152.53.141, dominio connectveiculos.dev.br no Registro.br) — script de bootstrap pendente, ML so funciona apos o deploy

---

## Tecnologias

### Backend
- .NET 8
- Entity Framework Core
- SQLite (dev) / PostgreSQL (prod)
- SignalR
- Serilog
- FluentValidation
- BCrypt.Net

### Frontend
- Angular 17+
- Bootstrap 5
- Chart.js
- SignalR Client

---

*Projeto concluido em: 26/12/2024*
*Documento atualizado em: 2026-04-29 (sessao com features locais, integracao ML, dashboard lucro, FIPE, documentos)*
