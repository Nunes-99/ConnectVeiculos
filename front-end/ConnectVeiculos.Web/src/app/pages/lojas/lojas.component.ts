import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { LojaService, ImagemService, AuthService, ToastService } from '../../core/services';
import { Loja } from '../../core/models';
import { MaskDirective } from '../../shared/directives';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ConfirmModalComponent } from '../../shared/components/confirm-modal/confirm-modal.component';
import { DocumentoValidator } from '../../shared/validators/documento.validator';
import { TelefonePipe } from '../../shared/pipes';

@Component({
  selector: 'app-lojas',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, MaskDirective, PaginationComponent, ConfirmModalComponent, TelefonePipe],
  templateUrl: './lojas.component.html',
  styleUrl: './lojas.component.scss'
})
export class LojasComponent implements OnInit {
  private lojaService = inject(LojaService);
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private imagemService = inject(ImagemService);
  private toast = inject(ToastService);
  private sanitizer = inject(DomSanitizer);
  private authService = inject(AuthService);

  urlCopiada = false;

  urlCatalogoPublico(): string {
    const slug = this.authService.getTenantSlug() || '';
    const origin = typeof window !== 'undefined' ? window.location.origin : 'https://connectveiculos.dev.br';
    return slug ? `${origin}/catalogo/${slug}` : `${origin}/catalogo`;
  }

  copiarUrlCatalogo(): void {
    const url = this.urlCatalogoPublico();
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(url).then(() => {
        this.urlCopiada = true;
        setTimeout(() => this.urlCopiada = false, 2500);
      });
    }
  }

  lojas: Loja[] = [];
  loading = false;
  showModal = false;
  editMode = false;
  editId: number | null = null;

  cnpjInvalido = false;
  logoPreview: string | null = null;
  logoFile: File | null = null;
  bannerPreview: string | null = null;
  bannerFile: File | null = null;
  faviconPreview: string | null = null;
  faviconFile: File | null = null;
  salvando = false;
  showPreview = false;

  // Modal de confirmacao
  showConfirmModal = false;
  lojaParaExcluir: number | null = null;

  // Paginação
  page = 1;
  pageSize = 10;
  totalItems = 0;
  totalPages = 0;
  searchTerm = '';
  cepPreenchido = false;

  form: FormGroup = this.fb.group({
    lojNome: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(150)]],
    lojSlug: ['', Validators.maxLength(100)],
    lojLogradouro: ['', Validators.maxLength(200)],
    lojNumero: ['', Validators.maxLength(10)],
    lojBairro: ['', Validators.maxLength(100)],
    lojCidade: ['', [Validators.required, Validators.maxLength(100)]],
    lojEstado: ['', [Validators.required, Validators.maxLength(2)]],
    lojCEP: [''],
    lojComplemento: ['', Validators.maxLength(100)],
    lojEmail: ['', [Validators.email, Validators.maxLength(150)]],
    lojTel1: [''],
    lojTel2: [''],
    lojWhatsApp: [''],
    lojCNPJ: [''],
    lojIE: ['', Validators.maxLength(14)],
    lojSts: [true],
    lojCorPrimaria: ['#1a237e'],
    lojCorSecundaria: ['#25d366'],
    lojImg: [''],
    lojInstagram: [''],
    lojFacebook: [''],
    lojUrlCatalogo: ['', Validators.maxLength(500)],
    lojPadraoCatalogo: [false],

    // Personalizacao do catalogo publico
    lojTema: ['escuro'],
    lojCorFundo: ['#171717'],
    lojBannerImg: [''],
    lojBannerTitulo: ['', Validators.maxLength(80)],
    lojBannerSubtitulo: ['', Validators.maxLength(160)],
    lojFavicon: [''],
    lojHorario: ['', Validators.maxLength(120)],
    lojSobre: ['', Validators.maxLength(1000)],
    lojLinkVenderCarro: ['', Validators.maxLength(500)],
    lojMostrarMarcas: [true],
    lojMostrarMapa: [true]
  });

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;
    this.lojaService.getAllPaged(this.page, this.pageSize, this.searchTerm).subscribe({
      next: (result) => {
        this.lojas = result.items;
        this.totalItems = result.totalItems;
        this.totalPages = result.totalPages;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onSearch(): void {
    this.page = 1;
    this.loadData();
  }

  onPageChange(page: number): void {
    this.page = page;
    this.loadData();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.page = 1;
    this.loadData();
  }

  openModal(loja?: Loja): void {
    this.editMode = !!loja;
    this.cnpjInvalido = false;
    if (loja) {
      this.editId = loja.lojId;
      this.form.patchValue(loja);
      this.form.patchValue({
        lojSlug: loja.lojSlug || '',
        lojCorPrimaria: loja.lojCorPrimaria || '#1a237e',
        lojCorSecundaria: loja.lojCorSecundaria || '#25d366',
        lojImg: loja.lojImg || '',
        lojInstagram: loja.lojInstagram || '',
        lojFacebook: loja.lojFacebook || '',
        lojUrlCatalogo: loja.lojUrlCatalogo || '',
        lojPadraoCatalogo: loja.lojPadraoCatalogo ?? false,
        lojTema: loja.lojTema || 'escuro',
        lojCorFundo: loja.lojCorFundo || '#171717',
        lojBannerImg: loja.lojBannerImg || '',
        lojBannerTitulo: loja.lojBannerTitulo || '',
        lojBannerSubtitulo: loja.lojBannerSubtitulo || '',
        lojFavicon: loja.lojFavicon || '',
        lojHorario: loja.lojHorario || '',
        lojSobre: loja.lojSobre || '',
        lojLinkVenderCarro: loja.lojLinkVenderCarro || '',
        lojMostrarMarcas: loja.lojMostrarMarcas ?? true,
        lojMostrarMapa: loja.lojMostrarMapa ?? true
      });
      this.bannerPreview = loja.lojBannerImg ? this.imagemService.getImageUrl(loja.lojBannerImg) : null;
      this.faviconPreview = loja.lojFavicon ? this.imagemService.getImageUrl(loja.lojFavicon) : null;
      this.bannerFile = null;
      this.faviconFile = null;
      this.logoPreview = loja.lojImg ? (loja.lojImg.startsWith('data:') ? loja.lojImg : this.imagemService.getImageUrl(loja.lojImg)) : null;
      this.logoFile = null;
    } else {
      this.editId = null;
      // Herdar URL do catálogo da primeira loja existente
      const urlCatalogo = this.lojas.find(l => l.lojUrlCatalogo)?.lojUrlCatalogo || '';
      this.form.reset({ lojSts: true, lojSlug: '', lojCorPrimaria: '#1a237e', lojCorSecundaria: '#25d366', lojImg: '', lojInstagram: '', lojFacebook: '', lojUrlCatalogo: urlCatalogo, lojPadraoCatalogo: false, lojTema: 'escuro', lojCorFundo: '#171717', lojBannerImg: '', lojBannerTitulo: '', lojBannerSubtitulo: '', lojFavicon: '', lojHorario: '', lojSobre: '', lojLinkVenderCarro: '', lojMostrarMarcas: true, lojMostrarMapa: true });
      this.bannerPreview = null; this.faviconPreview = null; this.bannerFile = null; this.faviconFile = null;
      this.logoPreview = null;
      this.logoFile = null;
    }
    this.cepPreenchido = false;
    this.toggleCamposEndereco(false);
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.form.reset({ lojSts: true, lojSlug: '', lojCorPrimaria: '#1a237e', lojCorSecundaria: '#25d366', lojImg: '', lojInstagram: '', lojFacebook: '', lojUrlCatalogo: '', lojPadraoCatalogo: false, lojTema: 'escuro', lojCorFundo: '#171717', lojBannerImg: '', lojBannerTitulo: '', lojBannerSubtitulo: '', lojFavicon: '', lojHorario: '', lojSobre: '', lojLinkVenderCarro: '', lojMostrarMarcas: true, lojMostrarMapa: true });
    this.editId = null;
    this.logoPreview = null;
    this.logoFile = null;
    this.showPreview = false;
  }

  validarCnpj(): void {
    const cnpj = this.form.get('lojCNPJ')?.value;
    this.cnpjInvalido = !!cnpj && !DocumentoValidator.isValidCNPJ(cnpj);
  }

  onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      this.logoFile = input.files[0];
      const reader = new FileReader();
      reader.onload = (e) => {
        this.logoPreview = e.target?.result as string;
      };
      reader.readAsDataURL(this.logoFile);
    }
  }

  onBannerSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      this.bannerFile = input.files[0];
      const reader = new FileReader();
      reader.onload = (e) => { this.bannerPreview = e.target?.result as string; };
      reader.readAsDataURL(this.bannerFile);
    }
  }

  removerBanner(): void {
    this.bannerPreview = null;
    this.bannerFile = null;
    this.form.patchValue({ lojBannerImg: '' });
  }

  onFaviconSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      this.faviconFile = input.files[0];
      const reader = new FileReader();
      reader.onload = (e) => { this.faviconPreview = e.target?.result as string; };
      reader.readAsDataURL(this.faviconFile);
    }
  }

  removerFavicon(): void {
    this.faviconPreview = null;
    this.faviconFile = null;
    this.form.patchValue({ lojFavicon: '' });
  }

  removerLogo(): void {
    this.logoPreview = null;
    this.logoFile = null;
    this.form.patchValue({ lojImg: '' });
  }

  abrirPreview(): void {
    this.showPreview = true;
  }

  abrirPreVisualizacao(): void {
    // Catalogo publico agora e path-based por TENANT (nao mais por loja).
    const tenant = this.authService.getTenantSlug() || 'default';
    const urlCatalogo = this.form.get('lojUrlCatalogo')?.value;
    if (urlCatalogo) {
      const baseUrl = urlCatalogo.endsWith('/') ? urlCatalogo.slice(0, -1) : urlCatalogo;
      window.open(`${baseUrl}/catalogo/${tenant}`, '_blank');
    } else {
      window.open(`/catalogo/${tenant}`, '_blank');
    }
  }

  private _previewUrlCache: { tenant: string; url: SafeResourceUrl } | null = null;

  getPreviewUrl(): SafeResourceUrl {
    // Catalogo publico agora e path-based por TENANT (nao mais por loja).
    // IMPORTANTE: memoizado — sanitizer.bypassSecurityTrustResourceUrl retorna
    // novo objeto a cada chamada, e Angular compara [src] por referencia → sem
    // memoize, o iframe recarrega a cada change detection cycle.
    const tenant = this.authService.getTenantSlug() || 'default';
    if (!this._previewUrlCache || this._previewUrlCache.tenant !== tenant) {
      this._previewUrlCache = {
        tenant,
        url: this.sanitizer.bypassSecurityTrustResourceUrl(`/catalogo/${tenant}`)
      };
    }
    return this._previewUrlCache.url;
  }

  /** Rotulos amigaveis dos campos, pra dizer na mensagem o que falta. */
  private readonly rotulosCampos: Record<string, string> = {
    lojNome: 'Nome', lojSlug: 'Identificador', lojLogradouro: 'Endereço',
    lojNumero: 'Número', lojBairro: 'Bairro', lojCidade: 'Cidade',
    lojEstado: 'Estado', lojCEP: 'CEP', lojComplemento: 'Complemento',
    lojEmail: 'E-mail', lojTel1: 'Telefone', lojTel2: 'Telefone 2',
    lojWhatsApp: 'WhatsApp', lojCNPJ: 'CNPJ', lojIE: 'Inscrição Estadual',
    lojUrlCatalogo: 'URL do catálogo', lojBannerTitulo: 'Título do banner',
    lojBannerSubtitulo: 'Subtítulo do banner', lojHorario: 'Horário de atendimento',
    lojSobre: 'Sobre a loja', lojLinkVenderCarro: 'Link "Venda seu carro"'
  };

  save(): void {
    this.validarCnpj();

    if (this.form.invalid || this.cnpjInvalido) {
      this.form.markAllAsTouched();
      this.avisarCamposPendentes();
      return;
    }

    if (this.logoPreview && this.logoFile) {
      this.form.patchValue({ lojImg: this.logoPreview });
    }
    const data = this.form.getRawValue();
    this.salvando = true;

    const salvo$ = this.editMode && this.editId
      ? this.lojaService.update(this.editId, data)
      : this.lojaService.create(data);

    salvo$.subscribe({
      next: (loja) => {
        const id = loja?.lojId ?? this.editId;

        // Banner e favicon so podem subir depois que a loja tem id, e o caminho
        // devolvido precisa voltar pro registro — por isso o segundo update.
        if (id && (this.bannerFile || this.faviconFile)) {
          this.enviarImagensDaLoja(id, data);
          return;
        }

        this.salvando = false;
        this.loadData();
        this.closeModal();
      },
      error: () => { this.salvando = false; }
    });
  }

  private enviarImagensDaLoja(lojaId: number, data: Record<string, unknown>): void {
    const envios: Promise<void>[] = [];

    if (this.bannerFile) {
      envios.push(new Promise<void>((resolve) => {
        this.imagemService.uploadLoja(lojaId, this.bannerFile as File, 'banner').subscribe({
          next: (r) => { data['lojBannerImg'] = r.caminho; resolve(); },
          error: () => resolve()
        });
      }));
    }

    if (this.faviconFile) {
      envios.push(new Promise<void>((resolve) => {
        this.imagemService.uploadLoja(lojaId, this.faviconFile as File, 'favicon').subscribe({
          next: (r) => { data['lojFavicon'] = r.caminho; resolve(); },
          error: () => resolve()
        });
      }));
    }

    Promise.all(envios).then(() => {
      this.lojaService.update(lojaId, data as never).subscribe({
        next: () => {
          this.salvando = false;
          this.loadData();
          this.closeModal();
        },
        error: () => { this.salvando = false; }
      });
    });
  }

  remove(id: number): void {
    this.lojaParaExcluir = id;
    this.showConfirmModal = true;
  }

  confirmarExclusao(): void {
    if (this.lojaParaExcluir) {
      this.lojaService.remove(this.lojaParaExcluir).subscribe({
        next: () => {
          this.loadData();
          this.cancelarExclusao();
        },
        error: () => this.cancelarExclusao()
      });
    }
  }

  cancelarExclusao(): void {
    this.showConfirmModal = false;
    this.lojaParaExcluir = null;
  }

  buscarCep(): void {
    const cep = this.form.get('lojCEP')?.value?.replace(/\D/g, '');
    if (!cep || cep.length !== 8) return;

    this.http.get<any>(`https://viacep.com.br/ws/${cep}/json/`).subscribe({
      next: (data) => {
        if (data.erro) {
          this.cepPreenchido = false;
          this.toggleCamposEndereco(false);
          return;
        }
        this.form.patchValue({
          lojLogradouro: data.logradouro || '',
          lojBairro: data.bairro || '',
          lojCidade: data.localidade || '',
          lojEstado: data.uf || '',
          lojComplemento: data.complemento || ''
        });
        this.cepPreenchido = true;
        this.toggleCamposEndereco(true);
      },
      error: () => {
        this.cepPreenchido = false;
        this.toggleCamposEndereco(false);
      }
    });
  }

  private toggleCamposEndereco(desabilitar: boolean): void {
    const campos = ['lojLogradouro', 'lojBairro', 'lojCidade', 'lojEstado'];
    campos.forEach(campo => {
      if (desabilitar) {
        this.form.get(campo)?.disable();
      } else {
        this.form.get(campo)?.enable();
      }
    });
  }

  hasError(field: string, error: string): boolean {
    const control = this.form.get(field);
    return control ? control.hasError(error) && control.touched : false;
  }

  /**
   * Diz o que impede o salvamento e leva ate o campo.
   *
   * Antes este caminho so fazia markAllAsTouched e um return: o clique em
   * Salvar nao mandava requisicao nenhuma e nada aparecia na tela, entao a
   * impressao era de botao quebrado. Foi o que aconteceu com o CNPJ invalido
   * de uma loja — o unico sinal era o campo marcado, fora da area visivel do
   * modal.
   */
  private avisarCamposPendentes(): void {
    const pendentes = Object.keys(this.form.controls)
      .filter(nome => this.form.get(nome)?.invalid)
      .map(nome => this.rotulosCampos[nome] || nome);

    if (this.cnpjInvalido) pendentes.unshift('CNPJ (dígitos não conferem)');

    this.toast.error(
      pendentes.length
        ? `Revise antes de salvar: ${pendentes.join(', ')}.`
        : 'Há campos inválidos no formulário.'
    );

    const primeiro = this.cnpjInvalido
      ? 'lojCNPJ'
      : Object.keys(this.form.controls).find(nome => this.form.get(nome)?.invalid);

    if (!primeiro) return;
    const el = document.querySelector<HTMLElement>(`[formcontrolname="${primeiro}"]`);
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    el?.focus({ preventScroll: true });
  }
}
