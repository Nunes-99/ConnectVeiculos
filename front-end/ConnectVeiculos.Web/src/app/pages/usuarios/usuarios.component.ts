import { Component, inject, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, NgForm } from '@angular/forms';
import { UsuarioService, LojaService, AcessoService } from '../../core/services';
import { Usuario, UsuarioInput, Loja, Acesso, PagedResult } from '../../core/models';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ConfirmModalComponent } from '../../shared/components/confirm-modal/confirm-modal.component';
import { MaskDirective } from '../../shared/directives';
import { DocumentoValidator } from '../../shared/validators/documento.validator';

@Component({
  selector: 'app-usuarios',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginationComponent, ConfirmModalComponent, MaskDirective],
  templateUrl: './usuarios.component.html',
  styleUrl: './usuarios.component.scss'
})
export class UsuariosComponent implements OnInit {
  private usuarioService = inject(UsuarioService);
  private lojaService = inject(LojaService);
  private acessoService = inject(AcessoService);

  @ViewChild('usuarioForm') usuarioForm!: NgForm;

  usuarios: Usuario[] = [];
  lojas: Loja[] = [];
  acessos: Acesso[] = [];
  loading = false;
  showModal = false;
  editMode = false;
  formSubmitted = false;

  cpfInvalido = false;

  // Modal de confirmacao
  showConfirmModal = false;
  usuarioParaExcluir: number | null = null;

  // Paginacao
  page = 1;
  pageSize = 10;
  totalItems = 0;
  totalPages = 0;
  searchTerm = '';

  lojasSelecionadas: Set<number> = new Set();

  formData: UsuarioInput = {
    r_LojId: 0,
    lojasIds: [],
    r_AcsId: 0,
    usuNome: '',
    usuCPF: '',
    usuRG: '',
    usuEmail: '',
    usuSenha: '',
    usuFuncao: '',
    usuSts: true
  };

  toggleLoja(lojaId: number): void {
    if (this.lojasSelecionadas.has(lojaId)) {
      this.lojasSelecionadas.delete(lojaId);
    } else {
      this.lojasSelecionadas.add(lojaId);
    }
    this.formData.lojasIds = Array.from(this.lojasSelecionadas);
    this.formData.r_LojId = this.lojasSelecionadas.size > 0 ? Array.from(this.lojasSelecionadas)[0] : 0;
  }

  selecionarTodasLojas(): void {
    if (this.lojasSelecionadas.size === this.lojas.length) {
      this.lojasSelecionadas.clear();
    } else {
      this.lojas.forEach(l => this.lojasSelecionadas.add(l.lojId));
    }
    this.formData.lojasIds = Array.from(this.lojasSelecionadas);
    this.formData.r_LojId = this.lojasSelecionadas.size > 0 ? Array.from(this.lojasSelecionadas)[0] : 0;
  }

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;
    this.usuarioService.getAllPaged(this.page, this.pageSize, this.searchTerm || undefined).subscribe({
      next: (result) => {
        this.usuarios = result.items;
        this.totalItems = result.totalItems;
        this.totalPages = result.totalPages;
        this.loading = false;
      },
      error: () => this.loading = false
    });

    this.lojaService.getAll().subscribe({
      next: (data) => this.lojas = data
    });

    this.acessoService.getAll().subscribe({
      next: (data) => this.acessos = data
    });
  }

  onPageChange(newPage: number): void {
    this.page = newPage;
    this.loadData();
  }

  onPageSizeChange(newSize: number): void {
    this.pageSize = newSize;
    this.page = 1;
    this.loadData();
  }

  onSearch(): void {
    this.page = 1;
    this.loadData();
  }

  openModal(usuario?: Usuario): void {
    this.editMode = !!usuario;
    this.formSubmitted = false;
    this.cpfInvalido = false;
    if (usuario) {
      this.formData = {
        usuId: usuario.usuId,
        r_LojId: usuario.r_LojId,
        lojasIds: [],
        r_AcsId: usuario.r_AcsId,
        usuNome: usuario.usuNome,
        usuCPF: usuario.usuCPF || '',
        usuRG: usuario.usuRG || '',
        usuEmail: usuario.usuEmail,
        usuSenha: '',
        usuFuncao: usuario.usuFuncao || '',
        usuSts: usuario.usuSts
      };
      // Carregar lojas associadas (por enquanto usa r_LojId existente)
      this.lojasSelecionadas = new Set(usuario.r_LojId ? [usuario.r_LojId] : []);
      this.formData.lojasIds = Array.from(this.lojasSelecionadas);
    } else {
      this.lojasSelecionadas = new Set();
      this.resetForm();
    }
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.formSubmitted = false;
    this.resetForm();
  }

  private resetForm(): void {
    this.lojasSelecionadas = new Set();
    this.formData = {
      r_LojId: 0,
      lojasIds: [],
      r_AcsId: 0,
      usuNome: '',
      usuCPF: '',
      usuRG: '',
      usuEmail: '',
      usuSenha: '',
      usuFuncao: '',
      usuSts: true
    };
  }

  validarCpf(): void {
    this.cpfInvalido = !!this.formData.usuCPF && !DocumentoValidator.isValidCPF(this.formData.usuCPF);
  }

  save(): void {
    this.formSubmitted = true;
    this.validarCpf();

    // Verifica se o formulario e valido
    if (this.usuarioForm?.invalid) {
      return;
    }

    // Validacao de CPF
    if (this.cpfInvalido) {
      return;
    }

    // Validacao adicional de senha para novo usuario
    if (!this.editMode && (!this.formData.usuSenha || this.formData.usuSenha.length < 6)) {
      return;
    }

    // Validacao de lojas
    if (this.lojasSelecionadas.size === 0) {
      return;
    }

    if (!this.formData.r_AcsId || this.formData.r_AcsId === 0) {
      return;
    }

    // Derivar funcao do acesso selecionado
    const acessoSelecionado = this.acessos.find(a => a.acsId == this.formData.r_AcsId);
    if (acessoSelecionado) {
      this.formData.usuFuncao = acessoSelecionado.acsNome;
    }

    if (this.editMode && this.formData.usuId) {
      this.usuarioService.update(this.formData.usuId, this.formData).subscribe({
        next: () => {
          this.loadData();
          this.closeModal();
        }
      });
    } else {
      this.usuarioService.create(this.formData).subscribe({
        next: () => {
          this.loadData();
          this.closeModal();
        }
      });
    }
  }

  remove(id: number): void {
    this.usuarioParaExcluir = id;
    this.showConfirmModal = true;
  }

  confirmarExclusao(): void {
    if (this.usuarioParaExcluir) {
      this.usuarioService.remove(this.usuarioParaExcluir).subscribe({
        next: () => {
          this.loadData();
          this.cancelarExclusao();
        }
      });
    }
  }

  cancelarExclusao(): void {
    this.showConfirmModal = false;
    this.usuarioParaExcluir = null;
  }

  isFieldInvalid(fieldName: string): boolean {
    const field = this.usuarioForm?.controls[fieldName];
    return !!(field?.invalid && (field?.touched || this.formSubmitted));
  }

  isSelectInvalid(value: number): boolean {
    return this.formSubmitted && (!value || value === 0);
  }

  isSenhaInvalid(): boolean {
    if (this.editMode) return false;
    return this.formSubmitted && (!this.formData.usuSenha || this.formData.usuSenha.length < 6);
  }

  isFormValid(): boolean {
    const basicValid = this.usuarioForm?.valid ?? false;
    const lojaValid = this.formData.r_LojId > 0;
    const acessoValid = this.formData.r_AcsId > 0;
    const senhaValid = this.editMode || (this.formData.usuSenha?.length >= 6);
    const cpfValid = !this.formData.usuCPF || DocumentoValidator.isValidCPF(this.formData.usuCPF);
    return basicValid && lojaValid && acessoValid && senhaValid && cpfValid;
  }
}
