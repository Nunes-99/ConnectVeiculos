# Templates WhatsApp — Guia de Submissão no Meta Business

Pra o sistema enviar notificações automáticas de test drive (confirmação, cancelamento, lembrete), você precisa **criar 3 templates no Meta Business Manager** e esperar Meta aprovar (~24h cada).

> Mesma orientação está disponível dentro do sistema em `/integracoes` → card "WhatsApp Business" → botão "Ver templates".

## Pré-requisitos

1. Conta gratuita em https://business.facebook.com
2. WhatsApp Business Account (WABA) vinculada a um número empresarial (não pode estar em uso no WhatsApp comum)
3. App Business no [developers.facebook.com](https://developers.facebook.com) com produto **WhatsApp** ativado
4. Acesso ao **WhatsApp Manager** dentro do Meta Business

## Passo a passo

1. Acesse https://business.facebook.com → menu lateral → **WhatsApp Manager**
2. Clique em **Templates de mensagem**
3. Clique em **Criar template**
4. Para cada um dos 3 templates abaixo:
   - **Categoria**: Utility (Utilitário) — não use Marketing, taxa é mais alta
   - **Nome**: copie EXATO como está em cada bloco (com underscore, minúsculo)
   - **Idioma**: Portuguese (BR)
   - **Body**: copie o texto da seção "Corpo da mensagem"
   - NÃO adicione header, footer, ou buttons — só body
5. Salve e submeta. Aprovação Meta: ~24h
6. Depois de aprovado, em ConnectVeículos `/integracoes` clique em **Configurar** no card "WhatsApp Business" e cole Access Token + Phone Number ID

---

## Template 1 — `testdrive_confirmado`

**Categoria:** Utility · **Idioma:** Portuguese (BR)

**Body:**
```
Olá {{1}}, tudo bem?

Seu test drive está CONFIRMADO! ✅

📅 Data: {{2}} às {{3}}
🚗 Veículo: {{4}}
📍 Local: {{5}}

Lembre-se de trazer um documento de identificação com foto (CNH ou RG).
Se precisar reagendar, é só responder esta mensagem.

Te esperamos!
_{{6}}_
```

**Variáveis** (na ordem que o sistema envia):
- `{{1}}` Nome do cliente
- `{{2}}` Data (dd/mm/yyyy)
- `{{3}}` Horário (HH:mm)
- `{{4}}` Veículo (marca + modelo + ano)
- `{{5}}` Endereço da loja
- `{{6}}` Nome da loja

---

## Template 2 — `testdrive_cancelado`

**Categoria:** Utility · **Idioma:** Portuguese (BR)

**Body:**
```
Olá {{1}},

Infelizmente precisamos CANCELAR seu test drive ❌

📅 Data: {{2}} às {{3}}
🚗 Veículo: {{4}}

Quer reagendar para outro horário? Responda esta mensagem ou nos chame que organizamos uma nova data.

Pedimos desculpas pelo transtorno.
_{{5}}_
```

**Variáveis:**
- `{{1}}` Nome do cliente
- `{{2}}` Data
- `{{3}}` Horário
- `{{4}}` Veículo
- `{{5}}` Nome da loja

---

## Template 3 — `testdrive_lembrete`

**Categoria:** Utility · **Idioma:** Portuguese (BR)

**Body:**
```
Olá {{1}}, tudo bem?

Passando pra LEMBRAR do seu test drive AMANHÃ! ⏰

📅 Data: {{2}} às {{3}}
🚗 Veículo: {{4}}
📍 Local: {{5}}

Não esqueça:
✅ Documento com foto (CNH ou RG)
✅ Chegue 10 minutos antes

Se precisar remarcar, é só responder esta mensagem.

Te esperamos!
_{{6}}_
```

**Variáveis:** mesmas do template 1 (incluem endereço da loja).

Disparado automaticamente pelo job Hangfire `lembrete-testdrive` (todo dia às 9h da manhã, busca test drives confirmados com data = amanhã).

---

## Custos

| Tipo | Custo aproximado |
|---|---|
| Conversas iniciadas pelo cliente (até 1.000/mês) | Grátis |
| Conversas iniciadas pelo cliente acima de 1.000/mês | ~R$ 0,06 |
| Templates Utility (esses 3) | ~R$ 0,06 cada |
| Templates Marketing | ~R$ 0,30 cada |

Pago direto ao Meta com o cartão da sua conta Business.

## Erros comuns

- **"Template não encontrado"** ao tentar enviar → ou o nome digitado ao criar não bate com `testdrive_confirmado/_cancelado/_lembrete`, ou o template ainda está em status "Em revisão"/"Rejeitado"
- **"Template rejeitado"** pelo Meta → geralmente é categoria errada. Confira se marcou Utility (não Marketing)
- **Cliente não recebe** apesar de retornar sucesso → número errado/desligado/bloqueou a empresa. Sistema mostra toast pro admin nesse caso

## O que acontece quando WhatsApp não está configurado

Os botões "Confirmar" e "Cancelar" no `/test-drives` continuam funcionando — só atualizam o status interno do agendamento. O sistema mostra um toast amarelo avisando "WhatsApp não integrado — só o status foi atualizado". Você precisa contatar o cliente manualmente nesse caso.
