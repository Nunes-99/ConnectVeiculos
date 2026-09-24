# Roteiro de apresentação e implantação — cliente operando

O cliente faz tudo no próprio computador/celular; você conduz. Ordem: **loja →
integrações → veículos → demais telas**. Tempo estimado: 1h30 a 2h.

---

## Antes da reunião

**Você**
- [ ] Nenhum deploy rodando nas 2h anteriores (a VM fica lenta e chega a dar 504)
- [ ] Adicionar o perfil do Facebook do cliente como **Testador** do app da Meta
      (developers.facebook.com → app *ConnectVeiculos* → Funções do app → Testadores)
      e pedir que ele **aceite o convite antes** — sem isso ele não consegue conectar
- [ ] Decidir com ele, antes, se os carros vão para o **Mercado Livre**: cada anúncio
      publicado cobra a taxa da categoria na conta ML dele (ficam em
      "aguardando pagamento" até pagar)

**Pedir ao cliente para trazer**
- CNPJ, endereço, telefone, WhatsApp, e-mail, horário de atendimento, Instagram e
  Facebook da loja
- **Logo** (PNG/JPG) e, se tiver, uma foto larga para o banner
- Login do **Facebook** (que administra a Página da loja) e do **Mercado Livre**
- Instagram da loja como **conta profissional vinculada à Página** do Facebook
- 1 ou 2 carros para cadastrar ao vivo: **fotos** (até 10), **CRLV** e preço
- Celular dele em mãos (para ver o catálogo e testar como cliente final)

---

## 1. Cadastro da loja (15 min)

**1.1 Criar a conta** — ele acessa **https://connectveiculos.dev.br/registro**
- Nome da empresa, e-mail, senha, nome do responsável
- O sistema cria o endereço do catálogo a partir do nome
  (ex.: `connectveiculos.dev.br/catalogo/diamante-veiculos`)
- ⚠️ **Você me avisa o e-mail usado**: a empresa nasce sem plano, e loja sem plano
  não entra no sitemap (fica fora do Google). Eu atribuo o plano na hora.

**1.2 Dados da loja** — menu **Lojas → editar a loja**
- Nome, CNPJ, IE, endereço completo (vira o mapa do rodapé e o local do test drive),
  telefone, WhatsApp, e-mail
- Instagram e Facebook (só o usuário, ex.: `diamanteveiculo`)
- Horário de atendimento (texto livre — aparece na faixa do topo do catálogo)

**1.3 Aparência do catálogo** — na mesma tela, seção de personalização
- Tema (claro/escuro), cor de fundo, cores de destaque
- Logo, banner, título e subtítulo do banner, texto "Sobre a loja"
- Link do "Vender meu carro" (se vazio, usa o WhatsApp da loja)
- Mostrar grade de marcas / mostrar mapa
- **Pré-visualizar** e abrir o catálogo no celular dele — é o primeiro "uau"

---

## 2. Integrações (30–40 min) — menu **Integrações**

Fazer nesta ordem. Cada cartão tem o botão **Testar conexão** depois de configurado.

### 2.1 Facebook + Instagram (cartão *Meta — Facebook + Instagram*)
Pré-requisito: convite de testador aceito (ver "Antes da reunião").
1. **Conectar** → login no Facebook → autorizar todas as permissões pedidas
2. Se a conta tiver uma Página só, o sistema escolhe sozinho; se tiver várias,
   escolher a da loja
3. Conferir que aparece "Page: *nome da página*" e "Instagram vinculado: @…"
4. **Testar conexão** nos dois (Posts orgânicos no Facebook / no Instagram)
5. Deixar os dois interruptores **Ativado**

Se aparecer "Nenhuma Page encontrada": a Página pertence a um Portfólio
Empresarial e faltou autorizar o acesso a ele — desconectar e conectar de novo,
marcando o portfólio.

### 2.2 Mercado Livre (se ele decidiu anunciar lá)
1. **Conectar** → login no Mercado Livre → autorizar
2. A conexão se renova sozinha; não precisa reconectar
3. Lembrar da taxa por anúncio (ver "Antes da reunião")

### 2.3 WhatsApp Business
- **Não dá para demonstrar ao vivo ainda.** O recebimento (mensagem do cliente
  vira lead na hora) foi validado, mas o app da Meta está em modo
  desenvolvimento e só repassa ao servidor webhooks de teste disparados pelo
  painel — mensagem real não chega. E o número de teste está ligado à
  `empresa-teste`, não à loja nova. Mostrar só o cartão e explicar
- **Envio automático** (confirmação, lembrete e cancelamento de test drive): os 3
  modelos já estão aprovados pela Meta, mas falta o **número do WhatsApp da loja**.
  Explicar as opções e sair da reunião com a decisão:
  - chip novo só para o sistema (recomendado), ou
  - o número atual da loja (ela deixa de usar o aplicativo nesse número)
  - o número precisa receber SMS/ligação uma vez e ter um nome de exibição
    (ex.: "Diamante Veículos"), que a Meta aprova em 1–2 dias
- Custo: receber é grátis; cada mensagem enviada pela loja ≈ R$ 0,06

### 2.4 E-mail (SMTP)
- Hoje sai de um endereço provisório. Anotar qual e-mail da loja ele quer como
  remetente (e a senha de app, se for Gmail) — configuramos depois

### 2.5 Google
- Nada a configurar: o catálogo entra no Google sozinho (sitemap + dados
  estruturados). Leva alguns dias para as páginas aparecerem na busca
- Google Vehicle Ads não existe no Brasil; Merchant fica para depois

---

## 3. Veículos (20–30 min) — menu **Veículos**

**3.1 Cadastrar o primeiro carro, ao vivo**
1. **Novo veículo**
2. **Scanner do CRLV**: foto do documento → preenche placa, chassi, marca, modelo,
   ano, cor. Conferir os campos
3. Preço de venda, preço de compra (só ele vê — alimenta o lucro), km, categoria,
   opcionais (separados por vírgula), observações
4. **Salvar** e em seguida **subir as fotos** (a primeira é a capa)
5. Esperar 1–2 minutos: o sistema espera as fotos terminarem e **publica sozinho**
   no Facebook, no Instagram (carrossel) e no Mercado Livre
6. Abrir o Instagram e o Facebook da loja no celular dele e mostrar o post

**3.2 O que acontece sozinho depois**
- Mudou o preço → a legenda do post do Facebook é reescrita
- Reservou → carimbo "🔒 RESERVADO"; vendeu → "✅ VENDIDO"; excluiu →
  "⛔ INDISPONÍVEL". O carro sai do catálogo
- Editar não posta de novo (para não repetir post a cada correção). Para publicar
  de novo manualmente: menu **⋮** da linha do veículo
- O Instagram não permite editar legenda nem apagar post pelo sistema

**3.3 Estoque grande**: **Importar planilha** (CSV). Carro importado sem foto não
publica; publicar pelo menu ⋮ depois de subir as fotos.

---

## 4. Demais telas — ele no papel de cliente final (20 min)

**4.1 Catálogo no celular dele** (`/catalogo/<slug-da-loja>`)
- Busca, filtros, detalhe do carro, compartilhar, favoritar, comparar
- **Simular financiamento** → **Solicitar análise de crédito** (preencher com dados
  reais dele ou fictícios)
- **Agendar test drive** (dias úteis 09h–17h, sábado até 11h, domingo fechado)
- **Tenho interesse** → abre o WhatsApp da loja com o carro na mensagem

**4.2 De volta ao painel**
- **Captação de Clientes (Leads)**: os pedidos aparecem sozinhos, sem recarregar —
  com o veículo, e no crédito com renda, entrada, parcelas e CPF. Mudar o status
  (Em contato → Negociando → Convertido)
- **Test Drives**: confirmar o agendamento feito no celular; remarcar
- **Negociações**, **Vendas** (registrar a venda do carro de teste → mostrar o
  carimbo VENDIDO no Facebook → estornar para voltar)
- **Documentos** do veículo (vencimentos), **Favoritos** (quem favoritou recebe
  e-mail quando o preço cai ou chega carro parecido)
- **Painel Geral** e **Relatórios** (estoque, vendas, lucro)
- **Usuários**: cadastrar um vendedor → ele recebe senha temporária por e-mail e
  troca no primeiro acesso

---

## Ao final — anotar e combinar

- [ ] Número do WhatsApp escolhido
- [ ] E-mail remetente
- [ ] Carros restantes: ele cadastra, ou manda planilha
- [ ] Me avisar o e-mail da conta criada (plano + checagem do sitemap)
- [ ] Carro de teste da reunião: vender/excluir se não for real
