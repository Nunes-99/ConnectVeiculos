import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { VeiculoService, LojaService, CategoriaService, ImagemService, VeiculoImagem, ToastService, FipeService, FipeMarca, FipeModelo } from '../../core/services';
import { Veiculo, Loja, Categoria } from '../../core/models';
import { MaskDirective, CurrencyMaskDirective } from '../../shared/directives';
import { PaginationComponent, ConfirmModalComponent } from '../../shared/components';
import { ScannerDocumentoComponent, CrlvExtraido } from '../../shared/components/scanner-documento/scanner-documento.component';
import { getDetranLink, DetranLink } from '../../shared/utils/detran-links.util';
import * as XLSX from 'xlsx';

@Component({
  selector: 'app-veiculos',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MaskDirective, CurrencyMaskDirective, PaginationComponent, ConfirmModalComponent, ScannerDocumentoComponent],
  templateUrl: './veiculos.component.html',
  styleUrl: './veiculos.component.scss'
})
export class VeiculosComponent implements OnInit {
  private veiculoService = inject(VeiculoService);
  private lojaService = inject(LojaService);
  private categoriaService = inject(CategoriaService);
  private imagemService = inject(ImagemService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);
  private fipeService = inject(FipeService);

  veiculos: Veiculo[] = [];
  veiculosPaginados: Veiculo[] = [];
  lojas: Loja[] = [];
  categorias: Categoria[] = [];
  imagens: VeiculoImagem[] = [];
  loading = false;
  showModal = false;
  showImagensModal = false;
  editMode = false;
  editId: number | null = null;
  selectedVeiculo: Veiculo | null = null;
  uploadingImage = false;
  uploadProgress = 0;
  uploadTotal = 0;
  uploadCurrent = 0;
  imagensPreview: { file: File; preview: string }[] = [];

  // Modal de confirmacao
  showConfirmModal = false;
  veiculoParaExcluir: number | null = null;

  // Scanner CRLV
  showScanner = false;

  abrirScanner(): void { this.showScanner = true; }
  fecharScanner(): void { this.showScanner = false; }

  aplicarCrlvExtraido(dados: CrlvExtraido): void {
    const patch: any = {};
    if (dados.placa) patch.veiPlaca = dados.placa.replace(/[^A-Z0-9]/gi, '').toUpperCase();
    if (dados.chassi) patch.veiChassi = dados.chassi.toUpperCase();
    if (dados.marca) patch.veiMarca = dados.marca;
    if (dados.modelo) patch.veiModelo = dados.modelo;
    if (dados.anoModelo || dados.anoFabricacao) patch.veiAno = dados.anoModelo || dados.anoFabricacao;
    if (dados.cor) patch.veiCor = dados.cor.charAt(0).toUpperCase() + dados.cor.slice(1).toLowerCase();
    if (dados.proprietarioNome) patch.veiDonoAtual = dados.proprietarioNome;
    this.form.patchValue(patch);
    if (dados.marca) this.buscaMarca = dados.marca;
    if (dados.modelo) this.buscaModelo = dados.modelo;
    this.toast.success('Dados do CRLV preenchidos. Revise e ajuste se necessário.');
    this.showScanner = false;

    // Tenta resolver codigos FIPE pra disparar busca de preço automaticamente.
    if (dados.marca) {
      this.resolverFipeAposCrlv(dados.marca, dados.modelo || '');
    }
  }

  /**
   * Resolve marca/modelo do CRLV via API FIPE (string → códigos) e dispara busca de preço.
   * Espera marcasFipe carregar se necessário (chamada async no ngOnInit pode não ter terminado).
   */
  private resolverFipeAposCrlv(marcaStr: string, modeloStr: string, tentativasRestantes = 10): void {
    // Aguarda marcasFipe ser populado (carrega async no ngOnInit). Retenta até 5s.
    if (this.marcasFipe.length === 0) {
      if (tentativasRestantes <= 0) {
        console.warn('FIPE: marcas não carregaram após 5s, abortando resolução automática');
        return;
      }
      setTimeout(() => this.resolverFipeAposCrlv(marcaStr, modeloStr, tentativasRestantes - 1), 500);
      return;
    }

    const marcaNorm = marcaStr.trim().toLowerCase();
    const marcaFipe = this.marcasFipe.find(m => {
      const n = m.nome.toLowerCase();
      return n === marcaNorm || n.includes(marcaNorm) || marcaNorm.includes(n);
    });
    if (!marcaFipe) {
      this.toast.warning(`FIPE: marca "${marcaStr}" não encontrada na tabela. Preço FIPE não preenchido.`);
      return;
    }

    this.marcaCodigoSelecionado = marcaFipe.codigo;
    this.outroMarca = false;
    this.buscaMarca = marcaFipe.nome;

    this.fipeService.getModelos(marcaFipe.codigo).subscribe({
      next: (modelos) => {
        this.modelosFipe = modelos;
        if (!modeloStr) { this.buscarPrecoFipe(); return; }

        // Match por palavras em comum — pega o que tem mais tokens em comum.
        const tokensCrlv = modeloStr.toLowerCase().split(/[\s\-/.()]+/).filter(t => t.length > 1);
        let melhor: { modelo: FipeModelo; score: number } | null = null;
        for (const m of modelos) {
          const nome = m.nome.toLowerCase();
          const score = tokensCrlv.filter(t => nome.includes(t)).length;
          if (score > 0 && (!melhor || score > melhor.score)) {
            melhor = { modelo: m, score };
          }
        }

        if (melhor) {
          this.modeloCodigoSelecionado = melhor.modelo.codigo;
          this.outroModelo = false;
          this.buscaModelo = melhor.modelo.nome;
          this.form.patchValue({ veiModelo: melhor.modelo.nome });
          this.buscarPrecoFipe();
        } else {
          this.toast.warning(`FIPE: modelo "${modeloStr}" não bateu com nenhum da FIPE. Escolha manualmente no dropdown se quiser preço FIPE.`);
        }
      },
      error: () => {
        this.toast.warning('FIPE indisponível no momento (API externa fora do ar). Tente preencher manualmente.');
      }
    });
  }

  // Importacao
  importando = false;
  importResult: { sucesso: number; erros: number; detalhes: string[] } | null = null;
  showImportResult = false;

  // Consulta Detran
  showDetranModal = false;
  detranVeiculo: Veiculo | null = null;
  detranLink: DetranLink = { uf: '', nome: '', url: '' };

  // Paginacao
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  showModeloImport = false;

  // Marcas e Modelos (FIPE)
  marcasFipe: FipeMarca[] = [];
  modelosFipe: FipeModelo[] = [];
  marcaSelecionada = '';
  marcaCodigoSelecionado = '';
  carregandoModelos = false;
  outroMarca = false;
  outroModelo = false;
  buscaMarca = '';
  buscaModelo = '';
  showDropdownMarca = false;
  showDropdownModelo = false;

  get marcasFiltradas(): FipeMarca[] {
    if (!this.buscaMarca) return this.marcasFipe;
    const termo = this.buscaMarca.toLowerCase();
    return this.marcasFipe.filter(m => m.nome.toLowerCase().includes(termo));
  }

  get modelosFiltrados(): FipeModelo[] {
    if (!this.buscaModelo) return this.modelosFipe;
    const termo = this.buscaModelo.toLowerCase();
    return this.modelosFipe.filter(m => m.nome.toLowerCase().includes(termo));
  }

  // Filtros
  filtroTexto = '';
  filtroLoja: number | null = null;
  filtroCategoria: number | null = null;
  filtroStatus = '';

  form: FormGroup = this.fb.group({
    r_LojId: [0, [Validators.required, Validators.min(1)]],
    r_CatId: [0, [Validators.required, Validators.min(1)]],
    veiMarca: ['', [Validators.required, Validators.minLength(2)]],
    veiModelo: ['', [Validators.required, Validators.minLength(2)]],
    veiAno: [null, [Validators.required, Validators.min(1900), Validators.max(2100)]],
    veiPlaca: ['', Validators.required],
    veiChassi: [''],
    veiCor: ['', Validators.required],
    veiKm: [null, Validators.min(0)],
    veiPreco: [0, [Validators.required, Validators.min(0)]],
    veiSts: ['D', Validators.required],
    veiPrecoCompra: [0, Validators.min(0)],
    veiPrecoFipe: [null as number | null, Validators.min(0)],
    veiOpcionais: [''],
    veiObservacao: [''],
    veiDonoAtual: [''],
    veiDonoCelular: ['']
  });

  opcionaisDisponiveis = [
    'Ar-condicionado', 'Direção hidráulica', 'Direção elétrica', 'Vidros elétricos',
    'Travas elétricas', 'Alarme', 'Airbag', 'ABS', 'Câmbio automático', 'Câmbio CVT',
    'Bancos de couro', 'Sensor de estacionamento', 'Câmera de ré', 'Central multimídia',
    'Bluetooth', 'GPS', 'Teto solar', 'Rodas de liga leve', 'Farol de milha',
    'Piloto automático', 'Controle de tração', 'Start/Stop', 'Chave presencial',
    'Retrovisor elétrico', 'Sensor de chuva', 'Farol de LED'
  ];
  opcionaisSelecionados: Set<string> = new Set();

  toggleOpcional(opcional: string): void {
    if (this.opcionaisSelecionados.has(opcional)) {
      this.opcionaisSelecionados.delete(opcional);
    } else {
      this.opcionaisSelecionados.add(opcional);
    }
    this.form.patchValue({ veiOpcionais: Array.from(this.opcionaisSelecionados).join(',') });
  }

  ngOnInit(): void {
    this.loadData();
    this.fipeService.getMarcas().subscribe({
      next: (marcas) => this.marcasFipe = marcas
    });
  }

  private loadData(): void {
    this.loading = true;
    this.veiculoService.getAll().subscribe({
      next: (data) => {
        this.veiculos = data;
        this.totalItems = data.length;
        this.updatePaginatedVeiculos();
        this.loading = false;
      },
      error: () => this.loading = false
    });

    this.lojaService.getAll().subscribe({
      next: (data) => this.lojas = data
    });

    this.categoriaService.getAll().subscribe({
      next: (data) => this.categorias = data
    });
  }

  openModal(veiculo?: Veiculo): void {
    this.editMode = !!veiculo;
    this.imagensPreview = [];
    this.imagens = [];
    if (veiculo) {
      this.editId = veiculo.veiId;
      this.form.patchValue({
        r_LojId: veiculo.r_LojId,
        r_CatId: veiculo.r_CatId,
        veiMarca: veiculo.veiMarca,
        veiModelo: veiculo.veiModelo,
        veiAno: veiculo.veiAno,
        veiPlaca: veiculo.veiPlaca,
        veiChassi: veiculo.veiChassi,
        veiCor: veiculo.veiCor,
        veiKm: veiculo.veiKm,
        veiPreco: veiculo.veiPreco,
        veiSts: veiculo.veiSts,
        veiPrecoCompra: veiculo.veiPrecoCompra,
        veiPrecoFipe: veiculo.veiPrecoFipe ?? null,
        veiOpcionais: veiculo.veiOpcionais || '',
        veiObservacao: veiculo.veiObservacao || '',
        veiDonoAtual: veiculo.veiDonoAtual || '',
        veiDonoCelular: veiculo.veiDonoCelular || ''
      });
      this.opcionaisSelecionados = new Set(
        veiculo.veiOpcionais ? veiculo.veiOpcionais.split(',').filter(o => o) : []
      );
      this.loadImagens(veiculo.veiId);
      // Verificar se a marca está na FIPE
      const marcaFipe = this.marcasFipe.find(m => m.nome.toLowerCase() === veiculo.veiMarca.toLowerCase());
      if (marcaFipe) {
        this.marcaSelecionada = marcaFipe.nome;
        this.marcaCodigoSelecionado = marcaFipe.codigo;
        this.buscaMarca = marcaFipe.nome;
        this.outroMarca = false;
        this.carregandoModelos = true;
        this.fipeService.getModelos(marcaFipe.codigo).subscribe({
          next: (modelos) => {
            this.modelosFipe = modelos;
            this.carregandoModelos = false;
            const modeloMatch = modelos.find(m => m.nome.toLowerCase() === veiculo.veiModelo.toLowerCase());
            this.outroModelo = !modeloMatch;
            this.buscaModelo = veiculo.veiModelo;
            if (modeloMatch) this.modeloCodigoSelecionado = modeloMatch.codigo;
          },
          error: () => { this.carregandoModelos = false; this.outroModelo = true; this.buscaModelo = veiculo.veiModelo; }
        });
      } else {
        this.marcaSelecionada = '';
        this.marcaCodigoSelecionado = '';
        this.buscaMarca = veiculo.veiMarca;
        this.buscaModelo = veiculo.veiModelo;
        this.modelosFipe = [];
        this.outroMarca = true;
        this.outroModelo = true;
      }
    } else {
      this.editId = null;
      this.marcaSelecionada = '';
      this.marcaCodigoSelecionado = '';
      this.modelosFipe = [];
      this.outroMarca = false;
      this.outroModelo = false;
      this.carregandoModelos = false;
      this.buscaMarca = '';
      this.buscaModelo = '';
      this.opcionaisSelecionados = new Set();
      this.form.reset({
        r_LojId: 0,
        r_CatId: 0,
        veiAno: null,
        veiKm: null,
        veiPreco: 0,
        veiSts: 'D',
        veiPrecoCompra: 0,
        veiPrecoFipe: null,
        veiObservacao: '',
        veiDonoAtual: '',
        veiDonoCelular: ''
      });
    }
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.editId = null;
    this.form.reset();
    this.imagensPreview = [];
    this.imagens = [];
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.value;
    const kmLimpo = String(raw.veiKm || '0').replace(/\./g, '').replace(/,/g, '');
    const data = {
      ...raw,
      r_LojId: Number(raw.r_LojId),
      r_CatId: Number(raw.r_CatId),
      veiAno: Number(raw.veiAno),
      veiKm: Number(kmLimpo) || 0,
      veiPreco: Number(raw.veiPreco) || 0,
      veiPrecoCompra: Number(raw.veiPrecoCompra) || 0,
      veiPrecoFipe: raw.veiPrecoFipe ? Number(raw.veiPrecoFipe) : null,
      veiOpcionais: raw.veiOpcionais || '',
      veiSitSts: raw.veiSitSts || '',
      veiObservacao: raw.veiObservacao || '',
      veiDonoAtual: raw.veiDonoAtual || '',
      veiDonoCelular: raw.veiDonoCelular || ''
    };

    if (this.editMode && this.editId) {
      this.veiculoService.update(this.editId, data).subscribe({
        next: () => {
          this.uploadPendingImages(this.editId!).then(() => {
            this.loadData();
            this.closeModal();
          });
        }
      });
    } else {
      this.veiculoService.create(data).subscribe({
        next: (result: any) => {
          const veiculoId = result?.id || result?.veiId;
          if (veiculoId && this.imagensPreview.length > 0) {
            this.uploadPendingImages(veiculoId).then(() => {
              this.loadData();
              this.closeModal();
            });
          } else {
            this.loadData();
            this.closeModal();
          }
        }
      });
    }
  }

  private async uploadPendingImages(veiculoId: number): Promise<void> {
    if (this.imagensPreview.length === 0) return;

    this.uploadingImage = true;
    this.uploadTotal = this.imagensPreview.length;
    this.uploadCurrent = 0;

    for (const item of this.imagensPreview) {
      try {
        await this.imagemService.upload(veiculoId, item.file).toPromise();
        this.uploadCurrent++;
      } catch {
        // Continua com as proximas imagens mesmo se uma falhar
      }
    }

    this.imagensPreview = [];
    this.uploadingImage = false;
    this.uploadTotal = 0;
    this.uploadCurrent = 0;
  }

  remove(id: number): void {
    this.veiculoParaExcluir = id;
    this.showConfirmModal = true;
  }

  confirmarExclusao(): void {
    if (this.veiculoParaExcluir) {
      this.veiculoService.remove(this.veiculoParaExcluir).subscribe({
        next: () => {
          this.loadData();
          this.cancelarExclusao();
        }
      });
    }
  }

  cancelarExclusao(): void {
    this.showConfirmModal = false;
    this.veiculoParaExcluir = null;
  }

  // Imagens
  openImagensModal(veiculo: Veiculo): void {
    this.selectedVeiculo = veiculo;
    this.loadImagens(veiculo.veiId);
    this.showImagensModal = true;
  }

  closeImagensModal(): void {
    this.showImagensModal = false;
    this.selectedVeiculo = null;
    this.imagens = [];
    this.imagensPreview = [];
    this.uploadProgress = 0;
    this.uploadTotal = 0;
    this.uploadCurrent = 0;
  }

  private loadImagens(veiculoId: number): void {
    this.imagemService.getByVeiculo(veiculoId).subscribe({
      next: (data) => this.imagens = data
    });
  }

  readonly MAX_IMAGENS = 20;

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
      const maxSize = 5 * 1024 * 1024; // 5MB
      const jaExistentes = (this.imagens?.length || 0) + this.imagensPreview.length;
      const disponivel = this.MAX_IMAGENS - jaExistentes;

      if (disponivel <= 0) {
        this.toast.warning(`Limite de ${this.MAX_IMAGENS} imagens por veículo atingido. Remova alguma antes de adicionar mais.`);
        input.value = '';
        return;
      }

      let adicionadas = 0;
      for (let i = 0; i < input.files.length; i++) {
        const file = input.files[i];

        if (adicionadas >= disponivel) {
          this.toast.warning(`Limite de ${this.MAX_IMAGENS} imagens atingido. As demais não foram adicionadas.`);
          break;
        }

        // Validar tipo de arquivo
        if (!allowedTypes.includes(file.type)) {
          this.toast.warning(`Arquivo "${file.name}" não permitido. Use JPG, PNG, GIF ou WEBP.`);
          continue;
        }

        // Validar tamanho (max 5MB)
        if (file.size > maxSize) {
          this.toast.warning(`Arquivo "${file.name}" muito grande. Máximo 5MB.`);
          continue;
        }

        adicionadas++;

        // Criar preview
        const reader = new FileReader();
        reader.onload = (e) => {
          this.imagensPreview.push({
            file: file,
            preview: e.target?.result as string
          });
        };
        reader.readAsDataURL(file);
      }
      input.value = '';
    }
  }

  removerPreview(index: number): void {
    this.imagensPreview.splice(index, 1);
  }

  async uploadAllImages(): Promise<void> {
    if (this.imagensPreview.length === 0 || !this.selectedVeiculo) return;

    this.uploadingImage = true;
    this.uploadTotal = this.imagensPreview.length;
    this.uploadCurrent = 0;
    this.uploadProgress = 0;

    const veiculoId = this.selectedVeiculo.veiId;

    for (const item of this.imagensPreview) {
      try {
        await this.imagemService.upload(veiculoId, item.file).toPromise();
        this.uploadCurrent++;
        this.uploadProgress = Math.round((this.uploadCurrent / this.uploadTotal) * 100);
      } catch {
        this.toast.error(`Erro ao enviar imagem: ${item.file.name}`);
      }
    }

    this.imagensPreview = [];
    this.uploadingImage = false;
    this.uploadProgress = 0;
    this.uploadTotal = 0;
    this.uploadCurrent = 0;
    this.loadImagens(veiculoId);
  }

  removeImagem(imagem: VeiculoImagem): void {
    this.imagemService.delete(imagem.imgId).subscribe({
      next: () => {
        const veiculoId = this.selectedVeiculo?.veiId || this.editId;
        if (veiculoId) {
          this.loadImagens(veiculoId);
        }
      }
    });
  }

  definirPrincipal(imagem: VeiculoImagem): void {
    this.imagemService.definirPrincipal(imagem.imgId).subscribe({
      next: () => {
        const veiculoId = this.selectedVeiculo?.veiId || this.editId;
        if (veiculoId) {
          this.loadImagens(veiculoId);
        }
        this.toast.success('Imagem definida como principal!');
      },
      error: () => {
        this.toast.error('Erro ao definir imagem principal.');
      }
    });
  }

  getImageUrl(caminho: string): string {
    return this.imagemService.getImageUrl(caminho);
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'D': 'Disponivel',
      'V': 'Vendido',
      'R': 'Reservado'
    };
    return labels[status] || status;
  }

  hasError(field: string, error: string): boolean {
    const control = this.form.get(field);
    return control ? control.hasError(error) && control.touched : false;
  }

  // Paginacao e Filtros
  updatePaginatedVeiculos(): void {
    let veiculosFiltrados = [...this.veiculos];

    // Aplicar filtro de texto (marca, modelo, placa, chassi)
    if (this.filtroTexto) {
      const texto = this.filtroTexto.toLowerCase();
      veiculosFiltrados = veiculosFiltrados.filter(v =>
        v.veiMarca?.toLowerCase().includes(texto) ||
        v.veiModelo?.toLowerCase().includes(texto) ||
        v.veiPlaca?.toLowerCase().includes(texto) ||
        v.veiChassi?.toLowerCase().includes(texto)
      );
    }

    // Filtro por loja
    if (this.filtroLoja) {
      veiculosFiltrados = veiculosFiltrados.filter(v => v.r_LojId === this.filtroLoja);
    }

    // Filtro por categoria
    if (this.filtroCategoria) {
      veiculosFiltrados = veiculosFiltrados.filter(v => v.r_CatId === this.filtroCategoria);
    }

    // Filtro por status
    if (this.filtroStatus) {
      veiculosFiltrados = veiculosFiltrados.filter(v => v.veiSts === this.filtroStatus);
    }

    this.totalItems = veiculosFiltrados.length;
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.veiculosPaginados = veiculosFiltrados.slice(startIndex, endIndex);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.updatePaginatedVeiculos();
  }

  aplicarFiltros(): void {
    this.currentPage = 1;
    this.updatePaginatedVeiculos();
  }

  limparFiltros(): void {
    this.filtroTexto = '';
    this.filtroLoja = null;
    this.filtroCategoria = null;
    this.filtroStatus = '';
    this.currentPage = 1;
    this.updatePaginatedVeiculos();
  }

  // Excel - Exportar
  exportarExcel(): void {
    const wb = XLSX.utils.book_new();

    const data = [
      ['Marca', 'Modelo', 'Ano', 'Placa', 'Chassi', 'Cor', 'KM', 'Preco Compra', 'Preco Venda', 'Status', 'Loja', 'Categoria', 'Data Entrada'],
      ...this.veiculos.map(v => [
        v.veiMarca,
        v.veiModelo,
        v.veiAno,
        v.veiPlaca || '',
        v.veiChassi || '',
        v.veiCor || '',
        v.veiKm || 0,
        v.veiPrecoCompra || 0,
        v.veiPreco,
        this.getStatusLabel(v.veiSts),
        v.lojaNome || '',
        v.categoriaNome || '',
        v.veiDtEntrada ? new Date(v.veiDtEntrada).toLocaleDateString('pt-BR') : ''
      ])
    ];

    const ws = XLSX.utils.aoa_to_sheet(data);

    // Ajustar largura das colunas
    ws['!cols'] = [
      { wch: 15 }, { wch: 25 }, { wch: 8 }, { wch: 12 }, { wch: 20 },
      { wch: 12 }, { wch: 10 }, { wch: 15 }, { wch: 15 }, { wch: 12 },
      { wch: 20 }, { wch: 18 }, { wch: 14 }
    ];

    XLSX.utils.book_append_sheet(wb, ws, 'Veiculos');

    const dataAtual = new Date().toISOString().split('T')[0];
    XLSX.writeFile(wb, `veiculos_${dataAtual}.xlsx`);
  }

  baixarModeloImport(): void {
    const wb = XLSX.utils.book_new();
    const data = [
      ['Marca', 'Modelo', 'Ano', 'Placa', 'Chassi', 'Cor', 'KM', 'Preco Compra', 'Preco Venda', 'Status', 'Loja', 'Categoria'],
      ['Honda', 'Civic', 2023, 'ABC-1D23', '9BWHE21JX24052050', 'Preto', 15000, 95000, 115000, 'Disponivel', 'Minha Loja', 'Sedan'],
      ['Toyota', 'Corolla', 2024, 'XYZ-9A87', '93HGGV63PNZ123456', 'Branco', 8000, 120000, 145000, 'Disponivel', 'Minha Loja', 'Sedan'],
      ['Hyundai', 'HB20', 2022, 'DEF-5B67', '9BWSU19F08B234567', 'Prata', 32000, 55000, 68000, 'Disponivel', 'Minha Loja', 'Hatch'],
    ];
    const ws = XLSX.utils.aoa_to_sheet(data);
    ws['!cols'] = [
      { wch: 12 }, { wch: 15 }, { wch: 6 }, { wch: 10 }, { wch: 22 },
      { wch: 10 }, { wch: 8 }, { wch: 14 }, { wch: 14 }, { wch: 12 },
      { wch: 15 }, { wch: 12 }
    ];
    XLSX.utils.book_append_sheet(wb, ws, 'Modelo');
    XLSX.writeFile(wb, 'modelo_importacao_veiculos.xlsx');
  }

  // Marcas e Modelos (FIPE)
  selecionarMarca(marca: FipeMarca): void {
    this.outroMarca = false;
    this.outroModelo = false;
    this.marcaSelecionada = marca.nome;
    this.marcaCodigoSelecionado = marca.codigo;
    this.modeloCodigoSelecionado = null;
    this.buscaMarca = marca.nome;
    this.buscaModelo = '';
    this.showDropdownMarca = false;
    this.form.patchValue({ veiMarca: marca.nome, veiModelo: '' });
    this.carregandoModelos = true;
    this.modelosFipe = [];
    this.fipeService.getModelos(marca.codigo).subscribe({
      next: (modelos) => {
        this.modelosFipe = modelos;
        this.carregandoModelos = false;
      },
      error: () => this.carregandoModelos = false
    });
  }

  selecionarOutroMarca(): void {
    this.outroMarca = true;
    this.outroModelo = true;
    this.modelosFipe = [];
    this.marcaSelecionada = '';
    this.marcaCodigoSelecionado = '';
    this.buscaMarca = '';
    this.buscaModelo = '';
    this.showDropdownMarca = false;
    this.form.patchValue({ veiMarca: '', veiModelo: '' });
  }

  modeloCodigoSelecionado: number | null = null;
  buscandoFipe = false;

  selecionarModelo(modelo: FipeModelo): void {
    this.outroModelo = false;
    this.buscaModelo = modelo.nome;
    this.showDropdownModelo = false;
    this.modeloCodigoSelecionado = modelo.codigo;
    this.form.patchValue({ veiModelo: modelo.nome });
    this.buscarPrecoFipe();
  }

  /**
   * Busca o preço FIPE quando temos marca + modelo + ano selecionados.
   *
   * Por padrao (forcar=false) NAO sobrescreve um valor ja preenchido — isso
   * evita que mexer no campo Ano (blur) destrua o preco FIPE salvo no banco
   * quando o usuario esta apenas editando outros campos do veiculo. Para
   * recalculo explicito (botao "Buscar FIPE"), passe forcar=true.
   *
   * Best-effort — falha silencioso se nao achar.
   */
  buscarPrecoFipe(forcar: boolean = false): void {
    const marcaCod = this.marcaCodigoSelecionado;
    const modeloCod = this.modeloCodigoSelecionado;
    const ano = this.form.get('veiAno')?.value;
    if (!marcaCod || !modeloCod || !ano) return;

    // Nao sobrescrever valor existente a menos que o usuario tenha pedido explicitamente.
    const valorAtual = this.form.get('veiPrecoFipe')?.value;
    if (!forcar && valorAtual !== null && valorAtual !== undefined && valorAtual !== '' && Number(valorAtual) > 0) {
      return;
    }

    this.buscandoFipe = true;
    this.fipeService.getAnos(marcaCod, modeloCod).subscribe({
      next: (anos) => {
        const anoMatch = anos.find(a => a.codigo.startsWith(`${ano}-`) || a.nome.startsWith(`${ano} `));
        if (!anoMatch) {
          this.buscandoFipe = false;
          this.toast.warning(`FIPE: ano ${ano} não disponível para esse modelo na tabela FIPE.`);
          return;
        }

        this.fipeService.getPreco(marcaCod, modeloCod, anoMatch.codigo).subscribe({
          next: (preco) => {
            const valorNumerico = this.fipeService.parseValorFipe(preco.valor);
            if (valorNumerico) {
              this.form.patchValue({ veiPrecoFipe: valorNumerico });
              this.toast.success(`FIPE: ${preco.valor} (${preco.mesReferencia})`);
            }
            this.buscandoFipe = false;
          },
          error: () => {
            this.buscandoFipe = false;
            this.toast.warning('FIPE: falha ao buscar preço (API externa indisponível).');
          }
        });
      },
      error: () => {
        this.buscandoFipe = false;
        this.toast.warning('FIPE: falha ao buscar anos disponíveis (API externa indisponível).');
      }
    });
  }

  selecionarOutroModelo(): void {
    this.outroModelo = true;
    this.buscaModelo = '';
    this.showDropdownModelo = false;
    this.form.patchValue({ veiModelo: '' });
  }

  fecharDropdowns(): void {
    setTimeout(() => {
      this.showDropdownMarca = false;
      this.showDropdownModelo = false;
    }, 200);
  }

  // Excel - Importar
  onImportarExcel(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const file = input.files[0];
    input.value = '';

    this.importando = true;
    this.importResult = null;

    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const data = new Uint8Array(e.target?.result as ArrayBuffer);
        const workbook = XLSX.read(data, { type: 'array' });
        const sheetName = workbook.SheetNames[0];
        const sheet = workbook.Sheets[sheetName];
        const rows: any[][] = XLSX.utils.sheet_to_json(sheet, { header: 1 });

        if (rows.length < 2) {
          this.toast.warning('O arquivo está vazio ou não possui dados além do cabeçalho.');
          this.importando = false;
          return;
        }

        this.processarImportacao(rows);
      } catch {
        this.toast.error('Erro ao ler o arquivo Excel.');
        this.importando = false;
      }
    };
    reader.readAsArrayBuffer(file);
  }

  private processarImportacao(rows: any[][]): void {
    const header = rows[0].map((h: any) => String(h).trim().toLowerCase());
    const dataRows = rows.slice(1).filter(row => row.some(cell => cell !== null && cell !== undefined && cell !== ''));

    const colMap: Record<string, number> = {};
    const aliases: Record<string, string[]> = {
      marca: ['marca'],
      modelo: ['modelo'],
      ano: ['ano'],
      placa: ['placa'],
      chassi: ['chassi'],
      cor: ['cor'],
      km: ['km', 'quilometragem'],
      precoCompra: ['preco compra', 'precocompra', 'preco_compra', 'custo', 'valor compra'],
      preco: ['preco venda', 'precovenda', 'preco_venda', 'preco', 'valor', 'valor venda'],
      status: ['status', 'sts', 'situacao'],
      loja: ['loja', 'lojanome', 'loja_nome'],
      categoria: ['categoria', 'categorianome', 'categoria_nome'],
    };

    for (const [key, names] of Object.entries(aliases)) {
      const idx = header.findIndex(h => names.some(n => h.includes(n)));
      if (idx !== -1) colMap[key] = idx;
    }

    const resultado = { sucesso: 0, erros: 0, detalhes: [] as string[] };
    let pendentes = dataRows.length;

    if (pendentes === 0) {
      this.importando = false;
      this.toast.warning('Nenhuma linha de dados encontrada.');
      return;
    }

    const defaultLoja = this.lojas.length > 0 ? this.lojas[0].lojId : 1;
    const defaultCategoria = this.categorias.length > 0 ? this.categorias[0].catId : 1;

    // Criar set de veiculos existentes para checagem de duplicidade (placa + cor + modelo)
    const veiculosExistentes = new Set(
      this.veiculos.map(v =>
        `${(v.veiPlaca || '').trim().toLowerCase()}|${(v.veiCor || '').trim().toLowerCase()}|${(v.veiModelo || '').trim().toLowerCase()}`
      )
    );

    for (let i = 0; i < dataRows.length; i++) {
      const row = dataRows[i];
      const lineNum = i + 2;

      const getString = (key: string, valorPadrao: string = 'XXXXX'): string => {
        if (colMap[key] !== undefined && row[colMap[key]] !== null && row[colMap[key]] !== undefined && String(row[colMap[key]]).trim() !== '') {
          return String(row[colMap[key]]).trim();
        }
        return valorPadrao;
      };

      const getNumber = (key: string, valorPadrao: number = 99999): number => {
        if (colMap[key] !== undefined && row[colMap[key]] !== null && row[colMap[key]] !== undefined) {
          const val = Number(row[colMap[key]]);
          if (!isNaN(val)) return val;
        }
        return valorPadrao;
      };

      // Resolver loja por nome
      let lojaId = defaultLoja;
      if (colMap['loja'] !== undefined) {
        const lojaNome = String(row[colMap['loja']] || '').trim().toLowerCase();
        const lojaEncontrada = this.lojas.find(l => l.lojNome.toLowerCase() === lojaNome);
        if (lojaEncontrada) lojaId = lojaEncontrada.lojId;
      }

      // Resolver categoria por nome
      let catId = defaultCategoria;
      if (colMap['categoria'] !== undefined) {
        const catNome = String(row[colMap['categoria']] || '').trim().toLowerCase();
        const catEncontrada = this.categorias.find(c => c.catNome.toLowerCase() === catNome);
        if (catEncontrada) catId = catEncontrada.catId;
      }

      // Resolver status
      let status = 'D';
      if (colMap['status'] !== undefined) {
        const statusVal = String(row[colMap['status']] || '').trim().toUpperCase();
        if (['D', 'V', 'R'].includes(statusVal)) {
          status = statusVal;
        } else if (statusVal.includes('VENDIDO')) {
          status = 'V';
        } else if (statusVal.includes('RESERV')) {
          status = 'R';
        }
      }

      const veiculo = {
        r_LojId: lojaId,
        r_CatId: catId,
        veiMarca: getString('marca'),
        veiModelo: getString('modelo'),
        veiAno: getNumber('ano', 2024),
        veiPlaca: getString('placa', ''),
        veiChassi: getString('chassi', ''),
        veiCor: getString('cor', ''),
        veiKm: getNumber('km', 0),
        veiPreco: getNumber('preco'),
        veiPrecoCompra: getNumber('precoCompra', 0),
        veiSts: status
      };

      const descricao = `${veiculo.veiMarca} ${veiculo.veiModelo}`.trim() || `Linha ${lineNum}`;

      // Verificar duplicidade por placa + cor + modelo
      const chave = `${veiculo.veiPlaca.trim().toLowerCase()}|${veiculo.veiCor.trim().toLowerCase()}|${veiculo.veiModelo.trim().toLowerCase()}`;
      if (veiculosExistentes.has(chave)) {
        pendentes--;
        if (pendentes <= 0) {
          this.importando = false;
          this.importResult = resultado;
          this.showImportResult = true;
          this.loadData();
        }
        continue;
      }
      veiculosExistentes.add(chave);

      this.veiculoService.create(veiculo).subscribe({
        next: () => {
          resultado.sucesso++;
          this.finalizarImportacao(resultado, --pendentes);
        },
        error: (err) => {
          resultado.erros++;
          let msgErro = '';
          if (err.error) {
            if (typeof err.error === 'string') {
              msgErro = err.error;
            } else if (err.error.errors) {
              const erros = err.error.errors;
              msgErro = Object.values(erros).flat().join('; ');
            } else if (err.error.title) {
              msgErro = err.error.title;
            } else if (err.error.message) {
              msgErro = err.error.message;
            } else {
              msgErro = JSON.stringify(err.error);
            }
          } else {
            msgErro = err.message || 'Erro desconhecido';
          }
          resultado.detalhes.push(`Linha ${lineNum} (${descricao}): ${msgErro}`);
          this.finalizarImportacao(resultado, --pendentes);
        }
      });
    }
  }

  private finalizarImportacao(resultado: { sucesso: number; erros: number; detalhes: string[] }, pendentes: number): void {
    if (pendentes <= 0) {
      this.importando = false;
      this.importResult = resultado;
      this.showImportResult = true;
      this.loadData();
    }
  }

  fecharResultadoImportacao(): void {
    this.showImportResult = false;
    this.importResult = null;
  }

  toggleSocialStatus(veiculoId: number, rede: string, currentValue: boolean): void {
    this.veiculoService.atualizarStatusSocial(veiculoId, rede, !currentValue).subscribe({
      next: () => this.loadData()
    });
  }

  // ============================================================
  // Consultar Detran (redireciona pro site oficial do estado)
  // ============================================================
  abrirConsultaDetran(veiculo: Veiculo): void {
    if (!veiculo.veiPlaca) {
      this.toast.warning('Veículo sem placa cadastrada.');
      return;
    }
    this.detranVeiculo = veiculo;
    const loja = this.lojas.find(l => l.lojId === veiculo.r_LojId);
    this.detranLink = getDetranLink(loja?.lojEstado);
    this.showDetranModal = true;
  }

  fecharConsultaDetran(): void {
    this.showDetranModal = false;
    this.detranVeiculo = null;
  }

  copiarPlaca(): void {
    if (!this.detranVeiculo?.veiPlaca) return;
    navigator.clipboard.writeText(this.detranVeiculo.veiPlaca).then(() => {
      this.toast.success(`Placa ${this.detranVeiculo!.veiPlaca} copiada.`);
    });
  }
}
