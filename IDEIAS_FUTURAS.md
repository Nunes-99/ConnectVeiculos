# Ideias para Implementacoes Futuras

## 1. Integracao com Simulacoes e Financiamentos

### Fase 1 - Simulador Proprio (Prioridade Alta)
- Simulador de financiamento com tabela Price
- Taxas configuraveis por loja (cada loja define suas taxas)
- Campos: valor de entrada, numero de parcelas, taxa de juros
- Calculo automatico da parcela mensal
- Exibicao no catalogo publico junto ao veiculo
- Sem dependencia de APIs externas

### Fase 2 - Integracao com Agregador (Prioridade Media)
- Integrar com plataforma agregadora (ex: Fintera, Creditas)
- Uma unica API que consulta multiplos bancos
- Requisitos: cadastro da loja na plataforma + documentacao basica (CNPJ)
- Prazo estimado de liberacao: 1 a 2 semanas
- Custo: varia entre R$ 99-500/mes ou comissao por operacao

### Fase 3 - APIs Diretas dos Bancos (Prioridade Baixa)
- Integracao direta com bancos: BV Financeira, Santander, Itau, Bradesco
- Requisitos: credenciamento como correspondente bancario + homologacao
- Prazo: 1 a 3 meses por banco (processo burocratico)
- Custo da API: geralmente gratuito (banco lucra nos juros)
- Maior controle e menos intermediarios

### Estrutura Tecnica Necessaria
- **Backend:** Novo modulo `Financiamento` (Core/Application/Infrastructure)
  - Entidades: `SimulacaoFinanciamento`, `PropostaFinanciamento`
  - Service para chamadas HTTP as APIs externas
  - Endpoints REST na API
- **Frontend:** Tela de simulacao vinculada ao veiculo
  - Formulario: valor entrada, parcelas, CPF
  - Exibicao das propostas retornadas
- **Banco de dados:** Novas tabelas (criadas automaticamente pelo padrao existente)

---

## 2. Dono Atual do Veiculo
- Adicionar campos `VeiDonoAtual` (nome) e `VeiDonoCelular` (telefone) no cadastro do veiculo
- Informacao interna (nao aparece no catalogo publico)
- Util para contato direto com o proprietario em caso de documentacao, transferencia, etc.
- Mascara de telefone no frontend: (99) 99999-9999

---

## 3. Relatorio Financeiro / Dashboard de Lucro
- Calcular lucro por veiculo: preco de venda - preco de compra - despesas
- Dashboard com lucro total mensal, margem media, veiculos mais rentaveis
- Grafico de evolucao de lucro por periodo
- Exportacao de relatorio financeiro em Excel/PDF

---

## 4. Integracao com WhatsApp (API)
- Botao "Chamar no WhatsApp" no catalogo publico (ja existe link basico)
- Mensagem automatica pre-formatada com dados do veiculo
- Notificacao via WhatsApp quando um lead e cadastrado
- Integracao com WhatsApp Business API para envio automatico de mensagens

---

## 5. Historico de Negociacao por Veiculo
- Registrar propostas recebidas (valor, data, cliente)
- Acompanhar evolucao da negociacao ate o fechamento
- Historico completo de interacoes por veiculo

---

## 6. Controle de Documentacao do Veiculo
- Checklist de documentos: CRLV, laudo cautelar, transferencia, vistoria
- Status por documento: pendente, em andamento, concluido
- Alertas para documentos vencidos ou pendentes
- Upload de PDFs/fotos dos documentos

---

## 7. Agendamento de Visitas / Test Drive Online
- Permitir que visitantes do catalogo agendem visitas diretamente
- Calendario com horarios disponiveis por loja
- Confirmacao automatica por e-mail/WhatsApp
- Ja existe a entidade TestDrive - seria uma evolucao com calendario visual

---

## 8. Comparador de Veiculos no Catalogo Publico
- Permitir que o visitante selecione 2 ou 3 veiculos para comparar lado a lado
- Comparar: preco, ano, km, caracteristicas, etc.
- Aumenta o engajamento e tempo no site

---

## 9. Integracao com Tabela FIPE
- Consultar valor FIPE automaticamente ao cadastrar marca/modelo/ano
- Exibir badge "abaixo da FIPE" ou "acima da FIPE" no catalogo
- API gratuita disponivel: https://brasilapi.com.br (endpoint /fipe)
- Ajuda o cliente a perceber que o preco e justo

---

## 10. Notificacoes por E-mail para Clientes
- Cliente favorita um veiculo e recebe alerta se o preco baixar
- Alerta quando um veiculo similar ao interesse do cliente for cadastrado
- Newsletter semanal com novos veiculos da loja
- Ja existe infraestrutura de e-mail (SMTP) no projeto

---

## 11. SEO e Compartilhamento em Redes Sociais
- Meta tags dinamicas (Open Graph) por veiculo no catalogo publico
- Ao compartilhar link do veiculo no WhatsApp/Facebook, aparecer foto + preco + modelo
- Sitemap automatico para indexacao no Google
- URL amigavel: /catalogo/loja-nome/toyota-corolla-2024

---

## 12. App PWA com Notificacoes Push
- O projeto ja e PWA (Service Worker configurado)
- Adicionar notificacoes push para alertar vendedores sobre novos leads
- Notificar clientes sobre novos veiculos ou mudancas de preco
- Funciona sem precisar publicar em loja de apps
