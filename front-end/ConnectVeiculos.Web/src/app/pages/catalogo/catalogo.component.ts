import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Title } from '@angular/platform-browser';
import { CatalogoService, ImagemService, TestDriveService, LeadService, FavoritoService } from '../../core/services';
import { CurrencyMaskDirective } from '../../shared/directives';
import { CatalogoVeiculo, CatalogoFiltro, CatalogoLoja } from '../../core/models';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';

// Imagens placeholder por marca (URLs publicas gratuitas)
const PLACEHOLDER_IMAGES: Record<string, string> = {
  'toyota': 'https://images.unsplash.com/photo-1621007947382-bb3c3994e3fb?w=600&h=400&fit=crop',
  'volkswagen': 'https://images.unsplash.com/photo-1622653902334-23c8e tried&w=600&h=400&fit=crop',
  'jeep': 'https://images.unsplash.com/photo-1533473359331-0135ef1b58bf?w=600&h=400&fit=crop',
  'chevrolet': 'https://images.unsplash.com/photo-1552519507-da3b142c6e3d?w=600&h=400&fit=crop',
  'honda': 'https://images.unsplash.com/photo-1606611013016-969c19ba27b5?w=600&h=400&fit=crop',
  'hyundai': 'https://images.unsplash.com/photo-1629897048514-3dd7414fe72a?w=600&h=400&fit=crop',
  'fiat': 'https://images.unsplash.com/photo-1583121274602-3e2820c69888?w=600&h=400&fit=crop',
  'bmw': 'https://images.unsplash.com/photo-1555215695-3004980ad54e?w=600&h=400&fit=crop',
  'mercedes': 'https://images.unsplash.com/photo-1618843479313-40f8afb4b4d8?w=600&h=400&fit=crop',
  'ford': 'https://images.unsplash.com/photo-1551830820-330a71b99659?w=600&h=400&fit=crop',
  'default': 'https://images.unsplash.com/photo-1494976388531-d1058494cdd8?w=600&h=400&fit=crop'
};

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

  veiculos: CatalogoVeiculo[] = [];
  filtros: CatalogoFiltro = {
    marcas: [],
    anoMin: 2000,
    anoMax: new Date().getFullYear(),
    precoMin: 0,
    precoMax: 500000
  };
  loja: CatalogoLoja | null = null;
  loading = false;
  total = 0;
  lojaId: number | null = null;
  lojaSlug: string | null = null;

  // Real-time
  private hubConnection: signalR.HubConnection | null = null;
  conectado = false;
  atualizacaoRecente = false;

  // Detalhes modal
  showDetalhes = false;
  veiculoSelecionado: CatalogoVeiculo | null = null;
  detalhesImagemIndex = 0;

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
  todosHorarios = ['08:00', '09:00', '10:00', '11:00', '13:00', '14:00', '15:00', '16:00', '17:00'];
  horariosDisponiveis: string[] = [...this.todosHorarios];

  // Share
  showShare = false;
  shareVeiculo: CatalogoVeiculo | null = null;
  linkCopiado = false;

  ngOnInit(): void {
    this.loadFavoritos();
    this.route.params.subscribe(params => {
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
      this.iniciarSignalR();
    });
  }

  ngOnDestroy(): void {
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
          this.lojaId || undefined
        );
    request$.subscribe({
      next: (resultado) => {
        this.veiculos = resultado.veiculos;
        this.filtros = resultado.filtros;
        this.total = resultado.total;
        this.loja = resultado.loja || null;
        // Set lojaId from API response when accessed via slug
        if (this.loja && !this.lojaId) {
          this.lojaId = this.loja.lojId;
        }
        this.categorias = [...new Set(this.veiculos.map(v => v.categoriaNome).filter(c => c))];
        this.filtrarPorTexto();
        this.loading = false;
        // Atualizar titulo da pagina com nome da empresa
        if (this.loja) {
          this.titleService.setTitle(`${this.loja.lojNome} - Catalogo de Veiculos`);
        } else {
          this.titleService.setTitle('Catalogo de Veiculos');
        }
        // Auto-open vehicle if navigated via direct URL
        if (this.autoOpenVeiculoId) {
          const v = this.veiculos.find(v => v.veiId === this.autoOpenVeiculoId);
          if (v) this.abrirDetalhes(v);
          this.autoOpenVeiculoId = null;
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
    this.showDetalhes = true;
    document.body.style.overflow = 'hidden';
  }

  fecharDetalhes(): void {
    this.showDetalhes = false;
    this.veiculoSelecionado = null;
    document.body.style.overflow = '';
  }

  detalhesAnterior(): void {
    if (this.veiculoSelecionado) {
      const total = this.getImagensVeiculo(this.veiculoSelecionado).length;
      this.detalhesImagemIndex = (this.detalhesImagemIndex - 1 + total) % total;
    }
  }

  detalhesProxima(): void {
    if (this.veiculoSelecionado) {
      const total = this.getImagensVeiculo(this.veiculoSelecionado).length;
      this.detalhesImagemIndex = (this.detalhesImagemIndex + 1) % total;
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
    return this.getPlaceholderImage(veiculo.veiMarca);
  }

  getPlaceholderImage(marca: string): string {
    const key = marca?.toLowerCase() || 'default';
    return PLACEHOLDER_IMAGES[key] || PLACEHOLDER_IMAGES['default'];
  }

  onImageError(event: Event, marca: string): void {
    const img = event.target as HTMLImageElement;
    if (img) img.src = this.getPlaceholderImage(marca);
  }

  getImageUrl(caminho: string): string {
    return this.imagemService.getImageUrl(caminho);
  }

  // WhatsApp
  abrirWhatsApp(veiculo?: CatalogoVeiculo): void {
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
    if (!this.favoritoEmail) return;
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
  // TEST DRIVE
  // ==========================================
  abrirTestDrive(veiculo: CatalogoVeiculo): void {
    this.testDriveVeiculo = veiculo;
    this.tdNome = ''; this.tdTelefone = ''; this.tdWhatsApp = ''; this.tdEmail = ''; this.tdData = ''; this.tdHorario = ''; this.tdObs = '';
    this.tdEnviado = false;
    const hoje = new Date();
    this.tdMinData = hoje.toISOString().split('T')[0];
    this.horariosDisponiveis = [...this.todosHorarios];
    this.showTestDrive = true;
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
      error: () => { alert('Erro ao agendar. Tente novamente.'); }
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
    const base = window.location.origin;
    const lojaParam = this.lojaSlug || this.loja?.lojSlug || this.lojaId;
    return lojaParam ? `${base}/catalogo/${lojaParam}/veiculo/${veiculo.veiId}` : `${base}/catalogo?veiculo=${veiculo.veiId}`;
  }

  copiarLink(): void {
    if (!this.shareVeiculo) return;
    navigator.clipboard.writeText(this.getShareUrl(this.shareVeiculo)).then(() => {
      this.linkCopiado = true;
      setTimeout(() => this.linkCopiado = false, 2000);
    });
  }

  compartilharWhatsApp(): void {
    if (!this.shareVeiculo) return;
    const v = this.shareVeiculo;
    const texto = encodeURIComponent(`Confira: ${v.veiMarca} ${v.veiModelo} ${v.veiAno} - ${this.formatarPreco(v.veiPreco)}\n${this.getShareUrl(v)}`);
    window.open(`https://wa.me/?text=${texto}`, '_blank');
  }
}
