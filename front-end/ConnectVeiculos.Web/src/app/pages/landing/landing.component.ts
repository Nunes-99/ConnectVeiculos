import { Component, OnInit, afterNextRender, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services';
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
  private seoService = inject(SeoService);
  private router = inject(Router);

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
      title: 'Marketplaces em 1 clique',
      desc: 'Publicação automática no Google Merchant (Vehicle Ads), Facebook Catalog e Mercado Livre. Edite uma vez, propaga em todos.'
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
      icon: 'request_quote',
      title: 'Financiamento integrado',
      desc: 'Simulações com BV e Banco Pan direto no catálogo, agilizando o fechamento sem o cliente precisar sair do anúncio.'
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
      desc: 'Catálogo no ar, leads chegando organizados e seus veículos anunciados no Google e Facebook automaticamente.'
    }
  ];

  planos = [
    {
      nome: 'Starter',
      preco: 'R$ 0',
      periodo: 'durante o teste',
      destaque: false,
      recursos: [
        'Até 10 veículos no catálogo',
        '1 loja, até 2 usuários',
        'Catálogo público com SEO',
        'Captura de leads e test drives',
        'Suporte por e-mail'
      ],
      cta: 'Começar grátis'
    },
    {
      nome: 'Pro',
      preco: 'R$ 197',
      periodo: '/mês',
      destaque: true,
      recursos: [
        'Veículos ilimitados',
        'Até 3 lojas, usuários ilimitados',
        'Google Merchant + Facebook Catalog',
        'Mercado Livre integrado',
        'Notificações automáticas (e-mail + WhatsApp)',
        'Relatórios avançados e exportação',
        'Simulação de financiamento (BV, Pan)',
        'Suporte prioritário'
      ],
      cta: 'Assinar Pro'
    },
    {
      nome: 'Enterprise',
      preco: 'Sob consulta',
      periodo: '',
      destaque: false,
      recursos: [
        'Tudo do plano Pro',
        'Lojas ilimitadas',
        'SLA dedicado com gerente de conta',
        'Onboarding e treinamento presencial',
        'Integrações sob demanda',
        'Domínio próprio no catálogo',
        'Suporte 24/7'
      ],
      cta: 'Falar com vendas'
    }
  ];

  faqs = [
    {
      pergunta: 'O que é o ConnectVeiculos?',
      resposta: 'É uma plataforma SaaS na nuvem feita para revendas de veículos. Em um só lugar você gerencia estoque, catálogo online, leads, test drives, vendas, financiamento e ainda publica automaticamente nos principais marketplaces (Google, Facebook, Mercado Livre).'
    },
    {
      pergunta: 'Preciso instalar algo no computador?',
      resposta: 'Não. Tudo funciona no navegador — Chrome, Edge, Safari ou Firefox — em qualquer computador, tablet ou celular. Sua equipe acessa de onde estiver, sem instalação e sem dor de cabeça com atualizações.'
    },
    {
      pergunta: 'Como funcionam as integrações com Google e Facebook?',
      resposta: 'Você autentica suas contas do Google Merchant Center e Facebook Catalog uma única vez nas configurações. A partir daí, cada veículo cadastrado, alterado ou vendido é sincronizado automaticamente nos catálogos — em tempo real, sem você fazer nada.'
    },
    {
      pergunta: 'Posso testar antes de assinar?',
      resposta: 'Sim. O plano Starter é gratuito e permite cadastrar até 10 veículos com todas as funcionalidades essenciais. Sem cartão de crédito, sem limite de tempo enquanto você estiver testando.'
    },
    {
      pergunta: 'Meus dados ficam seguros?',
      resposta: 'Sim. Cada revenda tem seu banco de dados isolado (arquitetura multi-tenant), comunicação criptografada (HTTPS), senhas protegidas com hash forte e backup automático diário. Em conformidade com a LGPD.'
    },
    {
      pergunta: 'Posso migrar meu estoque atual?',
      resposta: 'Sim. Suportamos importação em massa via planilha CSV — você nos passa o arquivo, a gente importa pra você no onboarding. Veículos e fotos chegam organizados, prontos para publicar.'
    }
  ];

  faqAberto: number | null = null;

  ngOnInit(): void {
    this.seoService.setLandingPage();
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
    this.router.navigate(['/dashboard']);
  }
}
