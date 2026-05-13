import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { LojaService, ImagemService, AuthService } from '../../core/services';
import { Loja } from '../../core/models';
import { MaskDirective } from '../../shared/directives';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ConfirmModalComponent } from '../../shared/components/confirm-modal/confirm-modal.component';
import { DocumentoValidator } from '../../shared/validators/documento.validator';

@Component({
  selector: 'app-lojas',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, MaskDirective, PaginationComponent, ConfirmModalComponent],
  templateUrl: './lojas.component.html',
  styleUrl: './lojas.component.scss'
})
export class LojasComponent implements OnInit {
  private lojaService = inject(LojaService);
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private imagemService = inject(ImagemService);
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
    lojPadraoCatalogo: [false]
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
        lojPadraoCatalogo: loja.lojPadraoCatalogo ?? false
      });
      this.logoPreview = loja.lojImg ? (loja.lojImg.startsWith('data:') ? loja.lojImg : this.imagemService.getImageUrl(loja.lojImg)) : null;
      this.logoFile = null;
    } else {
      this.editId = null;
      // Herdar URL do catálogo da primeira loja existente
      const urlCatalogo = this.lojas.find(l => l.lojUrlCatalogo)?.lojUrlCatalogo || '';
      this.form.reset({ lojSts: true, lojSlug: '', lojCorPrimaria: '#1a237e', lojCorSecundaria: '#25d366', lojImg: '', lojInstagram: '', lojFacebook: '', lojUrlCatalogo: urlCatalogo, lojPadraoCatalogo: false });
      this.logoPreview = null;
      this.logoFile = null;
    }
    this.cepPreenchido = false;
    this.toggleCamposEndereco(false);
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.form.reset({ lojSts: true, lojSlug: '', lojCorPrimaria: '#1a237e', lojCorSecundaria: '#25d366', lojImg: '', lojInstagram: '', lojFacebook: '', lojUrlCatalogo: '', lojPadraoCatalogo: false });
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

  save(): void {
    this.validarCnpj();

    if (this.form.invalid || this.cnpjInvalido) {
      this.form.markAllAsTouched();
      return;
    }

    if (this.logoPreview && this.logoFile) {
      this.form.patchValue({ lojImg: this.logoPreview });
    }
    const data = this.form.getRawValue();

    if (this.editMode && this.editId) {
      this.lojaService.update(this.editId, data).subscribe({
        next: () => {
          this.loadData();
          this.closeModal();
        }
      });
    } else {
      this.lojaService.create(data).subscribe({
        next: () => {
          this.loadData();
          this.closeModal();
        }
      });
    }
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
}
