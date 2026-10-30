import { Component, OnInit, afterNextRender, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AuthService, CatalogoService } from '../../core/services';
import { SeoService } from '../../core/services/seo.service';

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss'
})
export class LandingComponent implements OnInit {
  private authService = inject(AuthService);
  private catalogoService = inject(CatalogoService);
  private seoService = inject(SeoService);
  private router = inject(Router);

  /**
   * Lojas publicas, linkadas no rodape.
   *
   * Nao e vitrine: e o unico caminho de rastreio ate os catalogos. Ate
   * 2026-09-15 nenhuma pagina do site apontava para /catalogo — o Search
   * Console mostrava as URLs como "Detetada, atualmente nao indexada", com
   * "Pagina de referencia: nada detetado" e nenhum rastreio. O sitemap diz ao
   * Google que a URL existe; o link e o que diz que ela importa.
   *
   * A chamada acontece no ngOnInit para o SSR incluir os links no HTML: um
   * link que so aparece depois do JavaScript nao cumpre esse papel.
   */
  lojasPublicas = signal<{ slug: string; nome: string }[]>([]);

  // Durante SSR e a primeira renderizacao do client (hidratacao),
  // o template sempre renderiza como anonimo. So depois que o Angular
  // confirma o render via afterNextRender é que liberamos o estado
  // real de auth. Sem isso, o AuthService restaura o token do localStorage
  // antes do hydration concluir e causa DOM mismatch (NG0500).
  private hydrated = signal(false);
  isAuthenticated = computed(() => this.hydrated() && this.authService.isAuthenticated());

  ano = new Date().getFullYear();

  constructor() {
    afterNextRender(() => this.hydrated.set(true));
  }

  features = [
    {
      icon: 'inventory_2',
      title: 'Estoque sob controle',
      desc: 'Cadastre, atualize e acompanhe cada veículo com fotos, documentos, histórico de preços e status — entrada, disponível, reservado, vendido.'
    },
    {
      icon: 'storefront',
      title: 'Catálogo online 24h',
      desc: 'Página pública por loja, otimizada para Google, com fotos em alta, detalhes técnicos, filtros e contato direto via WhatsApp.'
    },
    {
      icon: 'share',
      title: 'Mercado Livre + Facebook em 1 clique',
      desc: 'Publicação automática no Mercado Livre e posts e stories no Facebook e no Instagram. Cadastra o veículo uma vez e ele propaga.'
    },
    {
      icon: 'group',
      title: 'Leads e test drives',
      desc: 'Capture interessados pelo catálogo, agende test drives, distribua para vendedores e acompanhe a conversão em tempo real.'
    },
    {
      icon: 'analytics',
      title: 'Relatórios que decidem',
      desc: 'Vendas por período, performance por vendedor, giro de estoque, origem de leads, comparativo entre lojas. Exporte para Excel ou PDF.'
    },
    {
      icon: 'apartment',
      title: 'Multi-loja, multi-usuário',
      desc: 'Várias unidades, cada uma com seu estoque e equipe. Permissões granulares por cargo: Administrador, Gerente, Vendedor.'
    },
    {
      icon: 'sms',
      title: 'Notificações automáticas',
      desc: 'Avise clientes que favoritaram quando o preço cair, quando um similar chegar e quando a venda for confirmada — por e-mail e WhatsApp.'
    },
    {
      icon: 'document_scanner',
      title: 'OCR de documentos',
      desc: 'Tire foto do CRLV e o sistema extrai chassi, RENAVAM, placa, ano e modelo automaticamente. Cadastro em segundos.'
    }
  ];

  passos = [
    {
      numero: '01',
      titulo: 'Cadastre sua revenda',
      desc: 'Conta criada em menos de 2 minutos. Sem cartão de crédito, sem instalação, sem letras miúdas.'
    },
    {
      numero: '02',
      titulo: 'Configure em 10 minutos',
      desc: 'Adicione suas lojas, importe veículos via CSV ou cadastre na hora, defina usuários e conecte suas integrações.'
    },
    {
      numero: '03',
      titulo: 'Venda mais e melhor',
      desc: 'Catálogo no ar, leads chegando organizados e seus veículos publicados no Mercado Livre e no catálogo do Facebook/Instagram automaticamente.'
    }
  ];

   // Sincronizado com os planos do backend (TenantsMigrationsRunner.SeedPlanoSeNaoExistir).
   // So o que o sistema realmente diferencia por plano: os limites. Recursos
   // (integracoes, relatorios, importacao por planilha) valem para todos.
   planos = [
     {
       nome: 'Free',
       preco: 'R$ 0',
       periodo: '/mês',
       destaque: false,
       recursos: [
         '30 dias grátis com todos os limites liberados',
         'Até 5 veículos no catálogo',
         '1 loja, 1 usuário',
         '20 leads por mês',
         'Catálogo público com SEO',
         'Todas as integrações e recursos'
       ],
       cta: 'Começar grátis'
     },
     {
       nome: 'Basic',
       preco: 'R$ 49,99',
       periodo: '/mês',
       destaque: false,
       recursos: [
         'Até 50 veículos',
         '1 loja, até 3 usuários',
         '200 leads por mês',
         'Todas as integrações e recursos',
         'Cartão ou Pix, cancele quando quiser',
         '7 dias para desistir com reembolso total'
       ],
       cta: 'Assinar Basic'
     },
     {
       nome: 'Pro',
       preco: 'R$ 99,99',
       periodo: '/mês',
       destaque: true,
       recursos: [
         'Até 500 veículos',
         'Até 3 lojas, até 10 usuários',
         '2.000 leads por mês',
         'Todas as integrações e recursos',
         'Cartão ou Pix, cancele quando quiser',
         '7 dias para desistir com reembolso total'
       ],
       cta: 'Assinar Pro'
     },
     {
       nome: 'Enterprise',
       preco: 'R$ 199,99',
       periodo: '/mês',
       destaque: false,
       recursos: [
         'Veículos, lojas e usuários ilimitados',
         'Leads ilimitados',
         'Todas as integrações e recursos',
         'Cartão ou Pix, cancele quando quiser',
         '7 dias para desistir com reembolso total'
       ],
       cta: 'Assinar Enterprise'
     }
   ];

  faqs = [
    {
      pergunta: 'O que é o ConnectVeiculos?',
      resposta: 'É uma plataforma SaaS na nuvem feita para revendas de veículos. Em um só lugar você gerencia estoque, catálogo online, leads, test drives e vendas — e publica seus veículos automaticamente no Mercado Livre, no catálogo do Facebook e na vitrine do Instagram.'
    },
    {
      pergunta: 'Preciso instalar algo no computador?',
      resposta: 'Não. Tudo funciona no navegador — Chrome, Edge, Safari ou Firefox — em qualquer computador, tablet ou celular. Sua equipe acessa de onde estiver, sem instalação e sem dor de cabeça com atualizações.'
    },
    {
      pergunta: 'Como funcionam as integrações com Mercado Livre e Facebook?',
      resposta: 'Mercado Livre: você autentica sua conta uma vez e cada veículo cadastrado pode ser publicado com 1 clique — com fotos, descrição e localização. Facebook e Instagram: o sistema gera um feed XML que o Facebook Catalog lê automaticamente, alimentando o Marketplace e — se você criar uma Loja no Commerce Manager — a vitrine pública no perfil da Page e do Instagram.'
    },
    {
      pergunta: 'Posso testar antes de assinar?',
      resposta: 'Sim. Ao se cadastrar você ganha 30 dias de trial com TODOS os recursos liberados, independente do plano que escolher. Depois do trial, o plano Free continua gratuito (até 5 veículos, 1 loja, 1 usuário). Sem cartão de crédito.'
    },
    {
      pergunta: 'Como pago e como cancelo?',
      resposta: 'Pelo próprio painel, em "Meu plano", com pagamento processado pelo Mercado Pago. No cartão de crédito, a mensalidade é cobrada automaticamente todo mês; no Pix, cada pagamento libera o plano por 30 dias. Desistiu em até 7 dias do pagamento? Devolvemos 100%. Depois disso você cancela quando quiser, sem multa: o plano segue até o fim do período pago, a loja volta para o Free e nada é apagado.'
    },
    {
      pergunta: 'Meus dados ficam seguros?',
      resposta: 'Sim. Cada revenda tem seu banco de dados isolado (arquitetura multi-tenant), comunicação criptografada (HTTPS), senhas protegidas com hash forte e backup automático diário. Em conformidade com a LGPD.'
    },
    {
      pergunta: 'Posso migrar meu estoque atual?',
      resposta: 'Sim. Em qualquer plano você importa o estoque por planilha (CSV) — sobe o arquivo no painel e os veículos são cadastrados em lote. Fotos podem ser adicionadas depois pelo painel ou pelo celular.'
    }
  ];

  faqAberto: number | null = null;

  ngOnInit(): void {
    this.seoService.setLandingPage();

    this.catalogoService.listarLojasPublicas().subscribe({
      next: lojas => this.lojasPublicas.set(lojas),
      // Sem lojas a secao nao aparece. A landing nao pode quebrar porque a
      // listagem falhou.
      error: () => this.lojasPublicas.set([])
    });
  }

  toggleFaq(index: number): void {
    this.faqAberto = this.faqAberto === index ? null : index;
  }

  irParaRegistro(): void {
    this.router.navigate(['/registro']);
  }

  irParaLogin(): void {
    this.router.navigate(['/login']);
  }

  irParaDashboard(): void {
    this.router.navigate([this.authService.paginaInicial()]);
  }
}
