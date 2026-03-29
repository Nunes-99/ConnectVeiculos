# Melhorias Planejadas - ConnectVeiculos

Este documento lista todas as melhorias planejadas para o sistema, organizadas por categoria e prioridade.

---

## 1. AUTORIZACAO POR PAPEIS (RBAC)

**Prioridade:** Alta
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Implementar sistema de permissoes baseado em papeis (Role-Based Access Control).

### Papeis Planejados
- **Administrador**: Acesso total ao sistema
- **Gerente**: Gerencia lojas, usuarios, relatorios
- **Vendedor**: Cadastro de veiculos e vendas
- **Visualizador**: Apenas consulta (sem edicao)

### Tarefas
- [ ] Criar tabela de Papeis (Roles)
- [ ] Criar tabela de Permissoes por funcionalidade
- [ ] Adicionar `[Authorize(Roles = "Admin")]` nos controllers
- [ ] Criar middleware de verificacao de permissao
- [ ] Implementar tela de gerenciamento de papeis no front-end
- [ ] Adicionar verificacao de permissao nos componentes Angular
- [ ] Ocultar botoes/menus baseado nas permissoes do usuario

### Arquivos a Modificar
- Controllers (adicionar atributos de autorizacao)
- Program.cs (configurar policies)
- Front-end: guards, services, templates

---

## 2. DASHBOARD COM GRAFICOS

**Prioridade:** Alta
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Adicionar visualizacoes graficas no dashboard para melhor analise de dados.

### Graficos Planejados
- Vendas por periodo (linha/barra)
- Faturamento mensal (barra)
- Veiculos por categoria (pizza)
- Estoque por status (donut)
- Top 10 veiculos mais vendidos
- Comparativo mes atual vs anterior

### Tarefas
- [ ] Instalar biblioteca de graficos (ng2-charts ou ngx-charts)
- [ ] Criar endpoints para dados agregados
- [ ] Implementar componente de grafico de vendas
- [ ] Implementar componente de grafico de estoque
- [ ] Implementar componente de grafico financeiro
- [ ] Adicionar filtros de periodo nos graficos
- [ ] Implementar cache nos dados agregados

### Bibliotecas Sugeridas
- **Front-end:** ng2-charts (wrapper do Chart.js) ou ngx-charts
- **Back-end:** Cache com MemoryCache ou Redis

---

## 3. EXPORTAR RELATORIOS PDF/EXCEL

**Prioridade:** Alta
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Permitir download de relatorios em formatos PDF e Excel.

### Relatorios para Exportar
- Relatorio de Vendas
- Relatorio de Estoque
- Relatorio Financeiro
- Lista de Veiculos
- Historico de Auditoria

### Tarefas
- [ ] Adicionar pacote ClosedXML para Excel
- [ ] Adicionar pacote QuestPDF ou iTextSharp para PDF
- [ ] Criar servico de geracao de Excel
- [ ] Criar servico de geracao de PDF
- [ ] Adicionar endpoints de exportacao
- [ ] Criar templates de relatorio PDF
- [ ] Adicionar botoes de exportacao no front-end
- [ ] Implementar download de arquivos

### Pacotes NuGet
```
ClosedXML (Excel)
QuestPDF (PDF - gratuito para uso comercial)
```

---

## 4. NOTIFICACOES EM TEMPO REAL

**Prioridade:** Media
**Complexidade:** Alta
**Status:** [ ] Pendente

### Descricao
Sistema de notificacoes em tempo real usando SignalR.

### Tipos de Notificacoes
- Nova venda realizada
- Veiculo reservado
- Estoque baixo (menos de X veiculos)
- Novo usuario cadastrado
- Alteracao de preco

### Tarefas
- [ ] Configurar SignalR no back-end
- [ ] Criar Hub de notificacoes
- [ ] Implementar servico de notificacoes
- [ ] Criar componente de notificacoes no front-end
- [ ] Implementar badge de notificacoes nao lidas
- [ ] Criar tela de historico de notificacoes
- [ ] Adicionar preferencias de notificacao por usuario
- [ ] Implementar notificacoes por email (opcional)

### Pacotes
```
Microsoft.AspNetCore.SignalR
@microsoft/signalr (npm)
```

---

## 5. VALIDACAO DE CPF/CNPJ/PLACA

**Prioridade:** Media
**Complexidade:** Baixa
**Status:** [ ] Pendente

### Descricao
Validar documentos brasileiros com algoritmos de digito verificador.

### Validacoes
- CPF (11 digitos + verificador)
- CNPJ (14 digitos + verificador)
- Placa (formato antigo XXX-0000 e Mercosul XXX0X00)
- Chassi (17 caracteres, padrao internacional)

### Tarefas
- [ ] Criar classe de validacao de CPF
- [ ] Criar classe de validacao de CNPJ
- [ ] Criar classe de validacao de Placa
- [ ] Criar classe de validacao de Chassi
- [ ] Adicionar validacoes nos InputModels
- [ ] Criar diretivas de mascara no Angular
- [ ] Adicionar validacao em tempo real nos forms
- [ ] Criar testes unitarios para validadores

---

## 6. RATE LIMITING

**Prioridade:** Alta
**Complexidade:** Baixa
**Status:** [ ] Pendente

### Descricao
Proteger API contra ataques de forca bruta e DDoS.

### Regras Planejadas
- Login: 5 tentativas por minuto por IP
- API geral: 100 requests por minuto por usuario
- Endpoints publicos: 30 requests por minuto por IP

### Tarefas
- [ ] Instalar pacote AspNetCoreRateLimit
- [ ] Configurar rate limiting no Program.cs
- [ ] Definir regras por endpoint
- [ ] Adicionar headers de rate limit nas respostas
- [ ] Implementar bloqueio temporario apos exceder limite
- [ ] Criar whitelist para IPs confiáveis

### Pacotes
```
AspNetCoreRateLimit
```

---

## 7. REFRESH TOKEN

**Prioridade:** Media
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Implementar renovacao automatica de tokens JWT.

### Funcionamento
1. Login retorna access_token (curta duracao) + refresh_token (longa duracao)
2. Quando access_token expira, front-end usa refresh_token para obter novo
3. Refresh_token e invalidado apos uso (rotacao)
4. Logout invalida todos os tokens

### Tarefas
- [ ] Criar tabela de RefreshTokens
- [ ] Modificar endpoint de login para retornar refresh_token
- [ ] Criar endpoint POST /api/auth/refresh
- [ ] Criar endpoint POST /api/auth/revoke
- [ ] Implementar interceptor no Angular para renovacao automatica
- [ ] Adicionar blacklist de tokens revogados
- [ ] Implementar limpeza periodica de tokens expirados

---

## 8. LOGS DE AUDITORIA AUTOMATICOS

**Prioridade:** Media
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Registrar automaticamente todas as operacoes CRUD no sistema.

### Informacoes a Registrar
- Usuario que fez a acao
- Data/hora
- IP de origem
- Entidade afetada
- Acao (Create, Update, Delete)
- Dados antes e depois (para Update)

### Tarefas
- [ ] Criar interceptor de Entity Framework para auditoria
- [ ] Modificar DbContext para capturar mudancas
- [ ] Registrar automaticamente em SaveChanges
- [ ] Criar tela de consulta de auditoria
- [ ] Adicionar filtros por usuario, data, entidade
- [ ] Implementar exportacao de logs
- [ ] Adicionar retencao de logs (ex: 90 dias)

---

## 9. CONSULTA TABELA FIPE

**Prioridade:** Media
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Integrar com API da tabela FIPE para consulta de precos de referencia.

### Funcionalidades
- Buscar marcas por tipo (carro, moto, caminhao)
- Buscar modelos por marca
- Buscar anos por modelo
- Obter preco FIPE
- Sugerir preco de venda baseado na FIPE

### Tarefas
- [ ] Criar servico de integracao com API FIPE
- [ ] Implementar cache de consultas (1 dia)
- [ ] Criar endpoint para busca de marcas
- [ ] Criar endpoint para busca de modelos
- [ ] Criar endpoint para busca de precos
- [ ] Adicionar campo de preco FIPE no cadastro de veiculo
- [ ] Criar componente de autocomplete no front-end
- [ ] Calcular percentual sobre FIPE no relatorio

### API
```
https://parallelum.com.br/fipe/api/v1
```

---

## 10. FOTOS COM COMPRESSAO

**Prioridade:** Baixa
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Otimizar upload e armazenamento de imagens de veiculos.

### Funcionalidades
- Compressao automatica de imagens
- Conversao para WebP (menor tamanho)
- Geracao de thumbnails
- Limite de tamanho por imagem
- Validacao de tipo MIME real

### Tarefas
- [ ] Adicionar pacote ImageSharp
- [ ] Criar servico de processamento de imagens
- [ ] Implementar compressao automatica no upload
- [ ] Gerar thumbnail (300x200) para listagens
- [ ] Gerar versao media (800x600) para detalhes
- [ ] Manter original para download
- [ ] Converter para WebP com fallback JPG
- [ ] Adicionar lazy loading no front-end

### Pacotes
```
SixLabors.ImageSharp
```

---

## 11. HISTORICO DE PRECOS

**Prioridade:** Baixa
**Complexidade:** Baixa
**Status:** [ ] Pendente

### Descricao
Registrar historico de alteracoes de preco dos veiculos.

### Funcionalidades
- Registrar preco anterior e novo
- Data da alteracao
- Usuario que alterou
- Motivo (opcional)
- Grafico de evolucao de preco

### Tarefas
- [ ] Criar tabela HistoricoPreco
- [ ] Criar entidade e repository
- [ ] Modificar update de veiculo para registrar historico
- [ ] Criar endpoint de consulta de historico
- [ ] Criar componente de timeline no front-end
- [ ] Adicionar grafico de evolucao de preco

---

## 12. CALCULADORA DE FINANCIAMENTO

**Prioridade:** Baixa
**Complexidade:** Baixa
**Status:** [ ] Pendente

### Descricao
Simular parcelas de financiamento para clientes.

### Funcionalidades
- Entrada + parcelas
- Diferentes taxas de juros
- Tabela Price e SAC
- Impressao da simulacao

### Tarefas
- [ ] Criar servico de calculo financeiro
- [ ] Implementar calculo Tabela Price
- [ ] Implementar calculo SAC
- [ ] Criar endpoint de simulacao
- [ ] Criar componente de calculadora no front-end
- [ ] Adicionar botao "Simular Financiamento" no detalhe do veiculo
- [ ] Gerar PDF da simulacao

---

## 13. QR CODE DO VEICULO

**Prioridade:** Baixa
**Complexidade:** Baixa
**Status:** [ ] Pendente

### Descricao
Gerar QR Code para acesso rapido ao catalogo do veiculo.

### Funcionalidades
- QR Code com link para pagina publica do veiculo
- Impressao de etiqueta com QR
- QR Code no PDF de detalhes

### Tarefas
- [ ] Adicionar pacote QRCoder
- [ ] Criar endpoint de geracao de QR Code
- [ ] Adicionar QR na pagina de detalhes do veiculo
- [ ] Criar botao de impressao de etiqueta
- [ ] Incluir QR no PDF de ficha do veiculo

### Pacotes
```
QRCoder
```

---

## 14. APP MOBILE (PWA)

**Prioridade:** Baixa
**Complexidade:** Alta
**Status:** [X] Concluido

### Descricao
Transformar o sistema em Progressive Web App para uso mobile.

### Funcionalidades
- Instalavel no celular
- Funciona offline (dados em cache)
- Push notifications
- Camera para fotos de veiculos
- Responsividade completa

### Tarefas
- [ ] Configurar service worker no Angular
- [ ] Criar manifest.json
- [ ] Implementar cache de dados offline
- [ ] Otimizar layout para mobile
- [ ] Implementar captura de foto pela camera
- [ ] Configurar push notifications
- [ ] Testar em dispositivos iOS e Android
- [ ] Publicar na Google Play (TWA)

---

## 15. TESTES AUTOMATIZADOS

**Prioridade:** Media
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Aumentar cobertura de testes do sistema (atual ~5%).

### Meta
- Cobertura minima de 70%
- Testes unitarios para todos os UseCases
- Testes de integracao para repositories
- Testes E2E para fluxos criticos

### Tarefas
- [ ] Criar testes para todos os UseCases
- [ ] Criar testes para validadores
- [ ] Criar testes de integracao com banco em memoria
- [ ] Configurar Cypress para testes E2E
- [ ] Criar testes E2E para login
- [ ] Criar testes E2E para cadastro de veiculo
- [ ] Criar testes E2E para venda
- [ ] Configurar CI/CD para rodar testes automaticamente

### Ferramentas
```
xUnit (back-end)
Moq (mocks)
Cypress ou Playwright (E2E)
```

---

## 16. CACHE REDIS

**Prioridade:** Baixa
**Complexidade:** Media
**Status:** [ ] Pendente

### Descricao
Implementar cache distribuido com Redis para producao.

### Beneficios
- Cache compartilhado entre instancias
- Melhor performance em escala
- Sessoes distribuidas

### Tarefas
- [ ] Configurar Redis (local ou Azure Cache)
- [ ] Adicionar pacote StackExchange.Redis
- [ ] Criar servico de cache com Redis
- [ ] Migrar cache do dashboard para Redis
- [ ] Implementar cache de sessoes
- [ ] Configurar expiracao de cache por tipo de dado

### Pacotes
```
Microsoft.Extensions.Caching.StackExchangeRedis
```

---

## 17. LOGGING COM SERILOG

**Prioridade:** Baixa
**Complexidade:** Baixa
**Status:** [ ] Pendente

### Descricao
Implementar logging estruturado para melhor debug em producao.

### Funcionalidades
- Logs estruturados em JSON
- Niveis de log (Debug, Info, Warning, Error)
- Contexto de requisicao (usuario, IP, request ID)
- Sink para arquivo e console
- Integracao com Seq ou Elasticsearch (opcional)

### Tarefas
- [ ] Adicionar pacotes Serilog
- [ ] Configurar Serilog no Program.cs
- [ ] Adicionar enrichers de contexto
- [ ] Configurar sink para arquivo rotativo
- [ ] Adicionar logs nos pontos criticos
- [ ] Configurar nivel de log por ambiente

### Pacotes
```
Serilog.AspNetCore
Serilog.Sinks.File
Serilog.Sinks.Console
Serilog.Enrichers.Environment
```

---

## ORDEM DE IMPLEMENTACAO SUGERIDA

### Fase 1 - Seguranca e Qualidade
1. Rate Limiting (#6)
2. Validacao CPF/CNPJ/Placa (#5)
3. Logging com Serilog (#17)

### Fase 2 - Funcionalidades Core
4. Autorizacao por Papeis (#1)
5. Logs de Auditoria Automaticos (#8)
6. Refresh Token (#7)

### Fase 3 - Experiencia do Usuario
7. Dashboard com Graficos (#2)
8. Exportar Relatorios PDF/Excel (#3)
9. Historico de Precos (#11)

### Fase 4 - Integracoes
10. Consulta Tabela FIPE (#9)
11. Fotos com Compressao (#10)
12. QR Code do Veiculo (#13)

### Fase 5 - Avancado
13. Calculadora de Financiamento (#12)
14. Notificacoes em Tempo Real (#4)
15. Testes Automatizados (#15)

### Fase 6 - Escala
16. Cache Redis (#16)
17. App Mobile PWA (#14)

---

## ACOMPANHAMENTO

| # | Melhoria | Status | Data Inicio | Data Fim |
|---|----------|--------|-------------|----------|
| 1 | Autorizacao por Papeis | OK | 26/12/2024 | 26/12/2024 |
| 2 | Dashboard com Graficos | OK (MELHORIAS.md) | - | - |
| 3 | Exportar PDF/Excel | OK (MELHORIAS.md) | - | - |
| 4 | Notificacoes Tempo Real | OK | 26/12/2024 | 26/12/2024 |
| 5 | Validacao CPF/CNPJ/Placa | OK | 26/12/2024 | 26/12/2024 |
| 6 | Rate Limiting | OK | 26/12/2024 | 26/12/2024 |
| 7 | Refresh Token | OK | 26/12/2024 | 26/12/2024 |
| 8 | Logs Auditoria Auto | OK (MELHORIAS.md) | - | - |
| 9 | Consulta FIPE | OK | 26/12/2024 | 26/12/2024 |
| 10 | Fotos Compressao | OK | 26/12/2024 | 26/12/2024 |
| 11 | Historico Precos | OK | 26/12/2024 | 26/12/2024 |
| 12 | Calc. Financiamento | OK | 26/12/2024 | 26/12/2024 |
| 13 | QR Code Veiculo | OK | 26/12/2024 | 26/12/2024 |
| 14 | App Mobile PWA | OK | 26/12/2024 | 26/12/2024 |
| 15 | Testes Automatizados | OK | 26/12/2024 | 26/12/2024 |
| 16 | Cache Redis | OK | 26/12/2024 | 26/12/2024 |
| 17 | Logging Serilog | OK | 26/12/2024 | 26/12/2024 |

---

*Documento criado em: 15/12/2024*
*Ultima atualizacao: 26/12/2024*
