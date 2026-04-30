import { Component, inject, OnInit, OnDestroy, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { Title } from '@angular/platform-browser';
import { CatalogoService, ImagemService, TestDriveService, LeadService, FavoritoService, ToastService } from '../../core/services';
import { SeoService } from '../../core/services/seo.service';
import { CurrencyMaskDirective } from '../../shared/directives';
import { CatalogoVeiculo, CatalogoFiltro, CatalogoLoja, CatalogoLojaResumo } from '../../core/models';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';

// Imagem placeholder SVG "sem foto" em base64
const NO_IMAGE_PLACEHOLDER = `data:image/svg+xml;base64,${btoa(`<svg xmlns="http://www.w3.org/2000/svg" width="600" height="400" viewBox="0 0 600 400">
  <rect width="600" height="400" fill="#e8e8e8"/>
  <g transform="translate(300,175)" fill="#bbb">
    <rect x="-60" y="-30" width="120" height="70" rx="8" fill="none" stroke="#bbb" stroke-width="3"/>
    <circle cx="0" cy="5" r="18" fill="none" stroke="#bbb" stroke-width="3"/>
    <circle cx="0" cy="5" r="7"/>
    <rect x="-18" y="-30" width="16" height="8" rx="2"/>
    <line x1="-45" y1="20" x2="-20" y2="-5" stroke="#bbb" stroke-width="2.5"/>
    <line x1="-20" y1="-5" x2="0" y2="10" stroke="#bbb" stroke-width="2.5"/>
    <line x1="0" y1="10" x2="20" y2="-2" stroke="#bbb" stroke-width="2.5"/>
    <line x1="20" y1="-2" x2="45" y2="20" stroke="#bbb" stroke-width="2.5"/>
  </g>
  <text x="300" y="260" text-anchor="middle" font-family="Arial,sans-serif" font-size="16" fill="#999">Sem foto disponivel</text>
</svg>`)}`;

@Component({
  selector: 'app-catalogo',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyMaskDirective],
  templateUrl: './catalogo.component.html',
  styleUrl: './catalogo.component.scss'
})
export class CatalogoComponent implements OnInit, OnDestroy {
  private catalogoService = inject(CatalogoService);
  private imagemService = inject(ImagemService);
  private testDriveService = inject(TestDriveService);
  private leadService = inject(LeadService);
  private favoritoService = inject(FavoritoService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private titleService = inject(Title);
  private toast = inject(ToastService);
  private seoService = inject(SeoService);
  private platformId = inject(PLATFORM_ID);

  veiculos: CatalogoVeiculo[] = [];
  filtros: CatalogoFiltro = {
    marcas: [],
    anoMin: 2000,
    anoMax: new Date().getFullYear(),
    precoMin: 0,
    precoMax: 500000
  };
  loja: CatalogoLoja | null = null;
  lojas: CatalogoLojaResumo[] = [];
  filtroLojaId: number | null = null;
  loading = false;
  total = 0;
  lojaId: number | null = null;
  lojaSlug: string | null = null;

  // Subscriptions
  private routeSubscription: Subscription | null = null;

  // Real-time
  private hubConnection: signalR.HubConnection | null = null;
  conectado = false;
  atualizacaoRecente = false;

  // Detalhes modal
  showDetalhes = false;
  veiculoSelecionado: CatalogoVeiculo | null = null;
  detalhesImagemIndex = 0;
  thumbsOffset = 0;
  thumbsVisiveis = 5;

  // Galeria fullscreen
  showGaleria = false;
  galeriaVeiculo: CatalogoVeiculo | null = null;
  galeriaIndex = 0;

  // Filtros selecionados
  marcaSelecionada = '';
  anoMinSelecionado: number | null = null;
  anoMaxSelecionado: number | null = null;
  precoMinSelecionado: number | null = null;
  precoMaxSelecionado: number | null = null;

  // Auto-open vehicle from route
  autoOpenVeiculoId: number | null = null;

  // Favorites (backed by API + localStorage for session)
  favoritos: Set<number> = new Set();
  showFavoritosOnly = false;
  buscaTexto = '';
  categoriaSelecionada = '';
  categorias: string[] = [];
  veiculosFiltrados: CatalogoVeiculo[] = [];
  favoritoEmail: string = '';
  favoritoNome: string = '';
  favoritoTelefone: string = '';
  showFavoritoCadastro = false;
  favoritoPendente: number | null = null; // veiculoId waiting for email registration
  favoritoLogado = false; // true when visitor has provided email

  // Compare
  comparando: CatalogoVeiculo[] = [];
  showComparador = false;

  // View mode
  viewMode: 'grid' | 'list' = 'grid';

  // Mobile filters toggle
  showFiltros = false;

  // Ordering
  ordenacao = '';

  // Finance calculator
  showFinanciamento = false;
  finVeiculo: CatalogoVeiculo | null = null;
  finEntrada = 0;
  finTaxa = 1.49;
  finParcelas = 48;
  finValorParcela = 0;

  // Solicitacao de credito (lead de financiamento)
  showSolicitacaoCredito = false;
  solicitacaoEnviada = false;
  solNome = '';
  solTelefone = '';
  solEmail = '';
  solCpf = '';
  solRenda = 0;
  solEntrada = 0;
  solParcelas: number | null = null;
  solObservacao = '';

  // Test drive form
  showTestDrive = false;
  testDriveVeiculo: CatalogoVeiculo | null = null;
  tdNome = '';
  tdTelefone = '';
  tdWhatsApp = '';
  tdEmail = '';
  tdData = '';
  tdHorario = '';
  tdObs = '';
  tdEnviado = false;
  tdMinData = '';
  datasDisponiveis: { valor: string; label: string }[] = [];
  todosHorarios = ['08:00', '09:00', '10:00', '11:00', '13:00', '14:00', '15:00', '16:00', '17:00'];
  horariosDisponiveis: string[] = [...this.todosHorarios];

  // Share
  showShare = false;
  shareVeiculo: CatalogoVeiculo | null = null;
  linkCopiado = false;

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.loadFavoritos();
    }
    this.routeSubscription = this.route.params.subscribe(params => {
      if (params['lojaId']) {
        const param = params['lojaId'];
        if (/^\d+$/.test(param)) {
          this.lojaId = Number(param);
          this.lojaSlug = null;
        } else {
          this.lojaSlug = param;
          this.lojaId = null;
        }
      }
      if (params['veiculoId']) {
        this.autoOpenVeiculoId = Number(params['veiculoId']);
      }
      this.loadCatalogo();
      if (isPlatformBrowser(this.platformId)) {
        this.iniciarSignalR();
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSubscription?.unsubscribe();
    this.pararSignalR();
  }

  private iniciarSignalR(): void {
    const hubUrl = `${environment.apiUrl.replace('/api', '')}/hubs/catalogo`;
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.hubConnection.on('CatalogoAtualizado', () => {
      this.atualizacaoRecente = true;
      this.loadCatalogo();
      setTimeout(() => this.atualizacaoRecente = false, 5000);
    });

    this.hubConnection.onreconnected(() => { this.conectado = true; this.loadCatalogo(); });
    this.hubConnection.onclose(() => this.conectado = false);

    this.hubConnection.start().then(() => {
      this.conectado = true;
      if (this.lojaId) {
        this.hubConnection!.invoke('AssinarLoja', this.lojaId);
      } else {
        this.hubConnection!.invoke('AssinarCatalogoGeral');
      }
    }).catch(() => this.conectado = false);
  }

  private pararSignalR(): void {
    this.hubConnection?.stop();
    this.hubConnection = null;
    this.conectado = false;
  }

  loadCatalogo(): void {
    this.loading = true;
    const request$ = this.lojaSlug
      ? this.catalogoService.getCatalogoBySlug(
          this.lojaSlug,
          this.marcaSelecionada || undefined,
          this.anoMinSelecionado || undefined,
          this.anoMaxSelecionado || undefined,
          this.precoMinSelecionado || undefined,
          this.precoMaxSelecionado || undefined
        )
      : this.catalogoService.getCatalogo(
          this.marcaSelecionada || undefined,
          this.anoMinSelecionado || undefined,
          this.anoMaxSelecionado || undefined,
          this.precoMinSelecionado || undefined,
          this.precoMaxSelecionado || undefined,
          this.filtroLojaId || this.lojaId || undefined
        );
    request$.subscribe({
      next: (resultado) => {
        this.veiculos = resultado.veiculos;
        this.filtros = resultado.filtros;
        this.total = resultado.total;
        this.loja = resultado.loja || null;
        this.lojas = resultado.lojas || [];
        // Set lojaId from API response when accessed via slug
        if (this.loja && !this.lojaId) {
          this.lojaId = this.loja.lojId;
        }
        this.categorias = [...new Set(this.veiculos.map(v => v.categoriaNome).filter(c => c))];
        this.filtrarPorTexto();
        this.loading = false;
        // SEO: meta tags e dados estruturados
        if (this.autoOpenVeiculoId) {
          const v = this.veiculos.find(v => v.veiId === this.autoOpenVeiculoId);
          if (v) {
            this.seoService.setVehiclePage(v);
            this.seoService.setVehicleJsonLd(v);
            if (isPlatformBrowser(this.platformId)) {
              this.abrirDetalhes(v);
            }
          }
          this.autoOpenVeiculoId = null;
        } else {
          this.seoService.setCatalogPage(this.loja);
          this.seoService.setCatalogJsonLd(this.veiculos);
        }
      },
      error: () => this.loading = false
    });
  }

  limparFiltros(): void {
    this.marcaSelecionada = '';
    this.anoMinSelecionado = null;
    this.anoMaxSelecionado = null;
    this.precoMinSelecionado = null;
    this.precoMaxSelecionado = null;
    this.filtroLojaId = null;
    this.loadCatalogo();
  }

  onLojaFiltroChange(lojaId: string): void {
    this.filtroLojaId = lojaId ? Number(lojaId) : null;
    this.loadCatalogo();
  }

  // Veiculos exibidos (com filtro de favoritos + busca + categoria)
  get veiculosExibidos(): CatalogoVeiculo[] {
    return this.veiculosFiltrados;
  }

  filtrarPorTexto(): void {
    let resultado = this.showFavoritosOnly
      ? this.veiculos.filter(v => this.favoritos.has(v.veiId))
      : [...this.veiculos];

    if (this.buscaTexto) {
      const termo = this.buscaTexto.toLowerCase();
      resultado = resultado.filter(v =>
        (v.veiMarca + ' ' + v.veiModelo).toLowerCase().includes(termo)
      );
    }

    if (this.categoriaSelecionada) {
      resultado = resultado.filter(v => v.categoriaNome === this.categoriaSelecionada);
    }

    // Sorting
    if (this.ordenacao) {
      resultado.sort((a, b) => {
        switch (this.ordenacao) {
          case 'preco_asc': return a.veiPreco - b.veiPreco;
          case 'preco_desc': return b.veiPreco - a.veiPreco;
          case 'ano_desc': return b.veiAno - a.veiAno;
          case 'ano_asc': return a.veiAno - b.veiAno;
          case 'km_asc': return a.veiKm - b.veiKm;
          default: return 0;
        }
      });
    }

    this.veiculosFiltrados = resultado;
  }

  getWhatsAppUrl(): string {
    if (!this.loja?.lojWhatsApp) return '#';
    const phone = this.loja.lojWhatsApp.replace(/\D/g, '');
    const fullPhone = phone.startsWith('55') ? phone : '55' + phone;
    return 'https://wa.me/' + fullPhone;
  }

  // Detalhes
  abrirDetalhes(veiculo: CatalogoVeiculo): void {
    this.veiculoSelecionado = veiculo;
    this.detalhesImagemIndex = 0;
    this.thumbsOffset = 0;
    this.showDetalhes = true;
    if (isPlatformBrowser(this.platformId)) {
      document.body.style.overflow = 'hidden';
    }
  }

  fecharDetalhes(): void {
    this.showDetalhes = false;
    this.veiculoSelecionado = null;
    if (isPlatformBrowser(this.platformId)) {
      document.body.style.overflow = '';
    }
  }

  detalhesAnterior(): void {
    if (this.veiculoSelecionado) {
      const total = this.getImagensVeiculo(this.veiculoSelecionado).length;
      this.detalhesImagemIndex = (this.detalhesImagemIndex - 1 + total) % total;
      this.ajustarThumbsOffset();
    }
  }

  detalhesProxima(): void {
    if (this.veiculoSelecionado) {
      const total = this.getImagensVeiculo(this.veiculoSelecionado).length;
      this.detalhesImagemIndex = (this.detalhesImagemIndex + 1) % total;
      this.ajustarThumbsOffset();
    }
  }

  getThumbsVisiveis(): { caminho: string; index: number }[] {
    if (!this.veiculoSelecionado) return [];
    const imagens = this.getImagensVeiculo(this.veiculoSelecionado);
    return imagens
      .slice(this.thumbsOffset, this.thumbsOffset + this.thumbsVisiveis)
      .map((caminho, i) => ({ caminho, index: this.thumbsOffset + i }));
  }

  thumbsAnterior(): void {
    this.thumbsOffset = Math.max(0, this.thumbsOffset - this.thumbsVisiveis);
  }

  thumbsProxima(): void {
    if (!this.veiculoSelecionado) return;
    const total = this.getImagensVeiculo(this.veiculoSelecionado).length;
    this.thumbsOffset = Math.min(total - this.thumbsVisiveis, this.thumbsOffset + this.thumbsVisiveis);
  }

  private ajustarThumbsOffset(): void {
    if (this.detalhesImagemIndex < this.thumbsOffset) {
      this.thumbsOffset = this.detalhesImagemIndex;
    } else if (this.detalhesImagemIndex >= this.thumbsOffset + this.thumbsVisiveis) {
      this.thumbsOffset = this.detalhesImagemIndex - this.thumbsVisiveis + 1;
    }
  }

  // Galeria fullscreen
  abrirGaleria(veiculo: CatalogoVeiculo, index: number = 0): void {
    this.galeriaVeiculo = veiculo;
    this.galeriaIndex = index;
    this.showGaleria = true;
  }

  fecharGaleria(): void {
    this.showGaleria = false;
    this.galeriaVeiculo = null;
  }

  galeriaAnterior(): void {
    if (this.galeriaVeiculo) {
      const total = this.getImagensVeiculo(this.galeriaVeiculo).length;
      this.galeriaIndex = (this.galeriaIndex - 1 + total) % total;
    }
  }

  galeriaProxima(): void {
    if (this.galeriaVeiculo) {
      const total = this.getImagensVeiculo(this.galeriaVeiculo).length;
      this.galeriaIndex = (this.galeriaIndex + 1) % total;
    }
  }

  // Imagens
  getImagensVeiculo(veiculo: CatalogoVeiculo): string[] {
    if (veiculo.imagens && veiculo.imagens.length > 0) {
      return veiculo.imagens;
    }
    return [];
  }

  getImagemPrincipal(veiculo: CatalogoVeiculo): string {
    if (veiculo.imagens && veiculo.imagens.length > 0) {
      return this.imagemService.getImageUrl(veiculo.imagens[0]);
    }
    return NO_IMAGE_PLACEHOLDER;
  }

  getPlaceholderImage(): string {
    return NO_IMAGE_PLACEHOLDER;
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img) img.src = NO_IMAGE_PLACEHOLDER;
  }

  getImageUrl(caminho: string): string {
    return this.imagemService.getImageUrl(caminho);
  }

  // WhatsApp
  abrirWhatsApp(veiculo?: CatalogoVeiculo): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const v = veiculo || this.veiculoSelecionado;
    if (!v) return;
    const telefone = v.lojaWhatsApp?.replace(/\D/g, '') || this.loja?.lojWhatsApp?.replace(/\D/g, '') || this.loja?.lojTel1?.replace(/\D/g, '') || '';
    if (!telefone) return;
    const mensagem = encodeURIComponent(
      `Ola! Tenho interesse no veiculo ${v.veiMarca} ${v.veiModelo} ${v.veiAno} - ${this.formatarPreco(v.veiPreco)}`
    );
    window.open(`https://wa.me/55${telefone}?text=${mensagem}`, '_blank');
    this.leadService.registrar({
      veiculoId: v?.veiId || null,
      lojaId: this.lojaId,
      origem: 'WHATSAPP_CATALOGO'
    }).subscribe();
  }

  abrirWhatsAppGeral(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const telefone = this.loja?.lojWhatsApp?.replace(/\D/g, '') || this.loja?.lojTel1?.replace(/\D/g, '') || '';
    if (!telefone && this.veiculos.length > 0) {
      const tel = this.veiculos[0].lojaWhatsApp?.replace(/\D/g, '') || '';
      if (!tel) return;
      window.open(`https://wa.me/55${tel}?text=${encodeURIComponent('Ola! Gostaria de saber mais sobre os veiculos disponiveis.')}`, '_blank');
      this.leadService.registrar({
        veiculoId: null,
        lojaId: this.lojaId,
        origem: 'WHATSAPP_CATALOGO'
      }).subscribe();
      return;
    }
    if (!telefone) return;
    window.open(`https://wa.me/55${telefone}?text=${encodeURIComponent('Ola! Gostaria de saber mais sobre os veiculos disponiveis.')}`, '_blank');
    this.leadService.registrar({
      veiculoId: null,
      lojaId: this.lojaId,
      origem: 'WHATSAPP_CATALOGO'
    }).subscribe();
  }

  formatarPreco(valor: number): string {
    return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  formatarKm(km: number): string {
    return km.toLocaleString('pt-BR') + ' km';
  }

  // ==========================================
  // FAVORITES (com e-mail)
  // ==========================================
  toggleFavorito(veiId: number): void {
    // Se ja logado com e-mail, toggle direto
    if (this.favoritoLogado && this.favoritoEmail) {
      if (this.favoritos.has(veiId)) {
        this.favoritos.delete(veiId);
        this.saveFavoritosLocal();
        this.favoritoService.desfavoritar(this.favoritoEmail, veiId).subscribe();
      } else {
        this.favoritos.add(veiId);
        this.saveFavoritosLocal();
        this.favoritoService.favoritar(veiId, this.favoritoEmail, this.favoritoNome, this.favoritoTelefone).subscribe();
      }
    } else {
      // Precisa pedir e-mail primeiro
      this.favoritoPendente = veiId;
      this.showFavoritoCadastro = true;
    }
  }

  confirmarFavoritoCadastro(): void {
    if (!this.favoritoEmail || !isPlatformBrowser(this.platformId)) return;
    // Salvar dados do visitante localmente
    this.favoritoLogado = true;
    localStorage.setItem('catalogo_fav_email', this.favoritoEmail);
    localStorage.setItem('catalogo_fav_nome', this.favoritoNome);
    localStorage.setItem('catalogo_fav_telefone', this.favoritoTelefone);
    this.showFavoritoCadastro = false;

    // Carregar favoritos do servidor
    this.favoritoService.meusFavoritos(this.favoritoEmail).subscribe({
      next: (ids) => {
        this.favoritos = new Set(ids);
        // Adicionar o pendente
        if (this.favoritoPendente) {
          this.favoritos.add(this.favoritoPendente);
          this.saveFavoritosLocal();
          this.favoritoService.favoritar(this.favoritoPendente, this.favoritoEmail, this.favoritoNome, this.favoritoTelefone).subscribe();
          this.favoritoPendente = null;
        }
      }
    });
  }

  fecharFavoritoCadastro(): void {
    this.showFavoritoCadastro = false;
    this.favoritoPendente = null;
  }

  isFavorito(veiId: number): boolean {
    return this.favoritos.has(veiId);
  }

  private loadFavoritos(): void {
    // Verificar se ja tem e-mail salvo
    const email = localStorage.getItem('catalogo_fav_email');
    if (email) {
      this.favoritoEmail = email;
      this.favoritoNome = localStorage.getItem('catalogo_fav_nome') || '';
      this.favoritoTelefone = localStorage.getItem('catalogo_fav_telefone') || '';
      this.favoritoLogado = true;
      // Carregar do servidor
      this.favoritoService.meusFavoritos(email).subscribe({
        next: (ids) => { this.favoritos = new Set(ids); }
      });
    } else {
      const stored = localStorage.getItem('catalogo_favoritos');
      if (stored) this.favoritos = new Set(JSON.parse(stored));
    }
  }

  private saveFavoritosLocal(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    localStorage.setItem('catalogo_favoritos', JSON.stringify([...this.favoritos]));
  }

  getVeiculosFavoritos(): CatalogoVeiculo[] {
    return this.veiculos.filter(v => this.favoritos.has(v.veiId));
  }

  // ==========================================
  // COMPARE
  // ==========================================
  toggleComparar(veiculo: CatalogoVeiculo, event: Event): void {
    event.stopPropagation();
    const idx = this.comparando.findIndex(v => v.veiId === veiculo.veiId);
    if (idx >= 0) {
      this.comparando.splice(idx, 1);
    } else if (this.comparando.length < 3) {
      this.comparando.push(veiculo);
    }
  }

  isComparando(veiId: number): boolean {
    return this.comparando.some(v => v.veiId === veiId);
  }

  abrirComparador(): void {
    if (this.comparando.length >= 2) this.showComparador = true;
  }

  fecharComparador(): void {
    this.showComparador = false;
  }

  removerComparacao(veiId: number): void {
    this.comparando = this.comparando.filter(v => v.veiId !== veiId);
    if (this.comparando.length < 2) this.showComparador = false;
  }

  // ==========================================
  // FINANCE CALCULATOR
  // ==========================================
  abrirFinanciamento(veiculo: CatalogoVeiculo): void {
    this.finVeiculo = veiculo;
    this.finEntrada = Math.round(veiculo.veiPreco * 0.2);
    this.showFinanciamento = true;
    this.calcularFinanciamento();
  }

  fecharFinanciamento(): void {
    this.showFinanciamento = false;
    this.finVeiculo = null;
  }

  calcularFinanciamento(): void {
    if (!this.finVeiculo) return;
    const valorFinanciado = this.finVeiculo.veiPreco - this.finEntrada;
    const taxaMensal = this.finTaxa / 100;
    if (taxaMensal > 0) {
      this.finValorParcela = valorFinanciado * (taxaMensal * Math.pow(1 + taxaMensal, this.finParcelas)) / (Math.pow(1 + taxaMensal, this.finParcelas) - 1);
    } else {
      this.finValorParcela = valorFinanciado / this.finParcelas;
    }
  }

  // ==========================================
  // SOLICITACAO DE CREDITO (LEAD DE FINANCIAMENTO)
  // ==========================================
  abrirSolicitacaoCredito(): void {
    this.solicitacaoEnviada = false;
    this.solNome = '';
    this.solTelefone = '';
    this.solEmail = '';
    this.solCpf = '';
    this.solRenda = 0;
    this.solEntrada = this.finEntrada || 0;
    this.solParcelas = this.finParcelas || null;
    this.solObservacao = '';
    this.showSolicitacaoCredito = true;
  }

  fecharSolicitacaoCredito(): void {
    this.showSolicitacaoCredito = false;
    this.solicitacaoEnviada = false;
  }

  formatarSolTelefone(): void {
    let v = this.solTelefone.replace(/\D/g, '');
    if (v.length > 11) v = v.substring(0, 11);
    if (v.length > 6) {
      this.solTelefone = `(${v.substring(0, 2)}) ${v.substring(2, 7)}-${v.substring(7)}`;
    } else if (v.length > 2) {
      this.solTelefone = `(${v.substring(0, 2)}) ${v.substring(2)}`;
    } else if (v.length > 0) {
      this.solTelefone = `(${v}`;
    }
  }

  formatarSolCpf(): void {
    let v = this.solCpf.replace(/\D/g, '');
    if (v.length > 11) v = v.substring(0, 11);
    if (v.length > 9) {
      this.solCpf = `${v.substring(0, 3)}.${v.substring(3, 6)}.${v.substring(6, 9)}-${v.substring(9)}`;
    } else if (v.length > 6) {
      this.solCpf = `${v.substring(0, 3)}.${v.substring(3, 6)}.${v.substring(6)}`;
    } else if (v.length > 3) {
      this.solCpf = `${v.substring(0, 3)}.${v.substring(3)}`;
    } else {
      this.solCpf = v;
    }
  }

  enviarSolicitacaoCredito(): void {
    if (!this.finVeiculo || !this.solNome || !this.solTelefone || !this.solCpf || !this.solRenda) {
      return;
    }

    const data = {
      veiculoId: this.finVeiculo.veiId,
      lojaId: this.lojaId,
      nomeCliente: this.solNome,
      telefone: this.solTelefone,
      email: this.solEmail || null,
      origem: 'FINANCIAMENTO',
      observacao: this.solObservacao || null,
      cpf: this.solCpf,
      renda: this.solRenda,
      entrada: this.solEntrada || null,
      parcelas: this.solParcelas
    };

    this.leadService.registrar(data).subscribe({
      next: () => {
        this.solicitacaoEnviada = true;
      },
      error: () => {
        this.toast.error('Erro ao enviar solicitacao. Tente novamente.');
      }
    });
  }

  // ==========================================
  // TEST DRIVE
  // ==========================================
  abrirTestDrive(veiculo: CatalogoVeiculo): void {
    this.testDriveVeiculo = veiculo;
    const hoje = new Date();
    this.tdMinData = hoje.toISOString().split('T')[0];
    this.tdNome = ''; this.tdTelefone = ''; this.tdWhatsApp = ''; this.tdEmail = ''; this.tdData = this.tdMinData; this.tdHorario = ''; this.tdObs = '';
    this.tdEnviado = false;
    this.datasDisponiveis = this.gerarProximasDatas(30);
    this.horariosDisponiveis = [...this.todosHorarios];
    this.onTdDataChange();
    this.showTestDrive = true;
  }

  private gerarProximasDatas(dias: number): { valor: string; label: string }[] {
    const datas: { valor: string; label: string }[] = [];
    const hoje = new Date();
    const diasSemana = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado'];
    for (let i = 0; i < dias; i++) {
      const d = new Date(hoje);
      d.setDate(d.getDate() + i);
      const valor = d.toISOString().split('T')[0];
      const dia = String(d.getDate()).padStart(2, '0');
      const mes = String(d.getMonth() + 1).padStart(2, '0');
      const label = i === 0 ? `Hoje (${dia}/${mes})` : i === 1 ? `Amanhã (${dia}/${mes})` : `${diasSemana[d.getDay()]} ${dia}/${mes}`;
      datas.push({ valor, label });
    }
    return datas;
  }

  onTdDataChange(): void {
    if (!this.tdData) {
      this.horariosDisponiveis = [...this.todosHorarios];
      return;
    }

    this.testDriveService.listar(this.lojaId || undefined).subscribe({
      next: (tds) => {
        const ocupados = tds
          .filter(td => td.tdrStatus !== 'X' && td.tdrDataAgendamento?.split('T')[0] === this.tdData)
          .map(td => td.tdrHorario);
        let disponiveis = this.todosHorarios.filter(h => !ocupados.includes(h));

        // Filtra horários já passados quando a data selecionada é hoje
        const hoje = new Date();
        const hojeStr = hoje.toISOString().split('T')[0];
        if (this.tdData === hojeStr) {
          const horaAtual = hoje.getHours();
          const minutoAtual = hoje.getMinutes();
          disponiveis = disponiveis.filter(h => {
            const [hora, minuto] = h.split(':').map(Number);
            return hora > horaAtual || (hora === horaAtual && minuto > minutoAtual);
          });
        }

        this.horariosDisponiveis = disponiveis;
        if (!this.horariosDisponiveis.includes(this.tdHorario)) {
          this.tdHorario = '';
        }
      }
    });
  }

  formatarTdTelefone(): void {
    let v = this.tdTelefone.replace(/\D/g, '');
    if (v.length > 11) v = v.substring(0, 11);
    if (v.length > 6) {
      this.tdTelefone = `(${v.substring(0, 2)}) ${v.substring(2, 7)}-${v.substring(7)}`;
    } else if (v.length > 2) {
      this.tdTelefone = `(${v.substring(0, 2)}) ${v.substring(2)}`;
    } else if (v.length > 0) {
      this.tdTelefone = `(${v}`;
    }
  }

  formatarTdWhatsApp(): void {
    let v = this.tdWhatsApp.replace(/\D/g, '');
    if (v.length > 11) v = v.substring(0, 11);
    if (v.length > 6) {
      this.tdWhatsApp = `(${v.substring(0, 2)}) ${v.substring(2, 7)}-${v.substring(7)}`;
    } else if (v.length > 2) {
      this.tdWhatsApp = `(${v.substring(0, 2)}) ${v.substring(2)}`;
    } else if (v.length > 0) {
      this.tdWhatsApp = `(${v}`;
    }
  }

  fecharTestDrive(): void {
    this.showTestDrive = false;
    this.testDriveVeiculo = null;
  }

  enviarTestDrive(): void {
    if (!this.testDriveVeiculo || !this.tdNome || !this.tdTelefone || !this.tdData) return;
    this.testDriveService.agendar({
      veiculoId: this.testDriveVeiculo.veiId,
      lojaId: this.lojaId,
      nomeCliente: this.tdNome,
      telefone: this.tdTelefone,
      whatsApp: this.tdWhatsApp || this.tdTelefone,
      email: this.tdEmail,
      dataAgendamento: this.tdData,
      horario: this.tdHorario,
      observacao: this.tdObs
    }).subscribe({
      next: () => { this.tdEnviado = true; },
      error: () => { this.toast.error('Erro ao agendar. Tente novamente.'); }
    });
  }

  // ==========================================
  // SHARE
  // ==========================================
  abrirShare(veiculo: CatalogoVeiculo): void {
    this.shareVeiculo = veiculo;
    this.linkCopiado = false;
    this.showShare = true;
  }

  fecharShare(): void {
    this.showShare = false;
    this.shareVeiculo = null;
  }

  getShareUrl(veiculo: CatalogoVeiculo): string {
    const urlCatalogo = this.loja?.lojUrlCatalogo;
    const base = urlCatalogo ? urlCatalogo.replace(/\/$/, '') : (isPlatformBrowser(this.platformId) ? window.location.origin : '');
    const lojaParam = this.lojaSlug || this.loja?.lojSlug || this.lojaId;
    return lojaParam ? `${base}/catalogo/${lojaParam}/veiculo/${veiculo.veiId}` : `${base}/catalogo?veiculo=${veiculo.veiId}`;
  }

  copiarLink(): void {
    if (!this.shareVeiculo || !isPlatformBrowser(this.platformId)) return;
    navigator.clipboard.writeText(this.getShareUrl(this.shareVeiculo)).then(() => {
      this.linkCopiado = true;
      setTimeout(() => this.linkCopiado = false, 2000);
    });
  }

  compartilharWhatsApp(): void {
    if (!this.shareVeiculo || !isPlatformBrowser(this.platformId)) return;
    const v = this.shareVeiculo;
    const texto = encodeURIComponent(`Confira: ${v.veiMarca} ${v.veiModelo} ${v.veiAno} - ${this.formatarPreco(v.veiPreco)}\n${this.getShareUrl(v)}`);
    window.open(`https://wa.me/?text=${texto}`, '_blank');
  }
}
