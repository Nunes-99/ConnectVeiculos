import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NegociacaoService, Negociacao, VeiculoService, LojaService, ToastService } from '../../core/services';
import { Veiculo, Loja } from '../../core/models';
import { MaskDirective, CurrencyMaskDirective } from '../../shared/directives';
import { PaginationComponent, ConfirmModalComponent } from '../../shared/components';

@Component({
  selector: 'app-negociacoes',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, MaskDirective, CurrencyMaskDirective, PaginationComponent, ConfirmModalComponent],
  templateUrl: './negociacoes.component.html',
  styleUrl: './negociacoes.component.scss'
})
export class NegociacoesComponent implements OnInit {
  private negociacaoService = inject(NegociacaoService);
  private veiculoService = inject(VeiculoService);
  private lojaService = inject(LojaService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);

  negociacoes: Negociacao[] = [];
  negociacoesFiltradas: Negociacao[] = [];
  veiculos: Veiculo[] = [];
  lojas: Loja[] = [];
  loading = false;
  showModal = false;
  editMode = false;
  editId: number | null = null;

  // Modal de confirmação
  showConfirmModal = false;
  negociacaoParaExcluir: number | null = null;

  // Paginação
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  // Filtros
  filtroStatus = '';
  filtroVeiculo: number | null = null;

  form: FormGroup = this.fb.group({
    veiculoId: [0, [Validators.required, Validators.min(1)]],
    lojaId: [null],
    nomeCliente: ['', [Validators.required, Validators.minLength(2)]],
    telefone: [''],
    email: [''],
    valorProposta: [0, [Validators.required, Validators.min(1)]],
    status: ['PROPOSTA', Validators.required],
    observacao: ['']
  });

  ngOnInit(): void {
    this.loadData();
    this.veiculoService.getAll().subscribe({ next: (data) => this.veiculos = data });
    this.lojaService.getAll().subscribe({ next: (data) => this.lojas = data });
  }

  loadData(): void {
    this.loading = true;
    this.negociacaoService.listar(
      this.filtroVeiculo || undefined,
      undefined,
      this.filtroStatus || undefined
    ).subscribe({
      next: (data) => {
        this.negociacoes = data;
        this.aplicarFiltros();
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  aplicarFiltros(): void {
    this.currentPage = 1;
    this.negociacoesFiltradas = [...this.negociacoes];
    this.totalItems = this.negociacoesFiltradas.length;
  }

  get negociacoesPaginadas(): Negociacao[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.negociacoesFiltradas.slice(start, start + this.pageSize);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
  }

  openModal(negociacao?: Negociacao): void {
    this.editMode = !!negociacao;
    if (negociacao) {
      this.editId = negociacao.negId;
      this.form.patchValue({
        veiculoId: negociacao.r_VeiId,
        lojaId: negociacao.r_LojId || null,
        nomeCliente: negociacao.negNomeCliente,
        telefone: negociacao.negTelefone || '',
        email: negociacao.negEmail || '',
        valorProposta: negociacao.negValorProposta,
        status: negociacao.negStatus,
        observacao: negociacao.negObservacao || ''
      });
    } else {
      this.editId = null;
      this.form.reset({
        veiculoId: 0,
        lojaId: null,
        nomeCliente: '',
        telefone: '',
        email: '',
        valorProposta: 0,
        status: 'PROPOSTA',
        observacao: ''
      });
    }
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.editId = null;
    this.form.reset();
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.value;
    const data = {
      ...raw,
      veiculoId: Number(raw.veiculoId),
      lojaId: raw.lojaId ? Number(raw.lojaId) : null,
      valorProposta: Number(raw.valorProposta) || 0
    };

    if (this.editMode && this.editId) {
      this.negociacaoService.atualizar(this.editId, data).subscribe({
        next: () => {
          this.toast.success('Negociação atualizada!');
          this.loadData();
          this.closeModal();
        },
        error: () => this.toast.error('Erro ao atualizar negociação.')
      });
    } else {
      this.negociacaoService.registrar(data).subscribe({
        next: () => {
          this.toast.success('Negociação registrada!');
          this.loadData();
          this.closeModal();
        },
        error: () => this.toast.error('Erro ao registrar negociação.')
      });
    }
  }

  atualizarStatus(id: number, status: string): void {
    this.negociacaoService.atualizarStatus(id, status).subscribe({
      next: () => this.loadData()
    });
  }

  remove(id: number): void {
    this.negociacaoParaExcluir = id;
    this.showConfirmModal = true;
  }

  confirmarExclusao(): void {
    if (this.negociacaoParaExcluir) {
      this.negociacaoService.excluir(this.negociacaoParaExcluir).subscribe({
        next: () => {
          this.loadData();
          this.cancelarExclusao();
        }
      });
    }
  }

  cancelarExclusao(): void {
    this.showConfirmModal = false;
    this.negociacaoParaExcluir = null;
  }

  getVeiculoNome(veiId: number): string {
    const v = this.veiculos.find(x => x.veiId === veiId);
    return v ? `${v.veiMarca} ${v.veiModelo} ${v.veiAno}` : `#${veiId}`;
  }

  getVeiculoPreco(veiId: number): number {
    const v = this.veiculos.find(x => x.veiId === veiId);
    return v?.veiPreco || 0;
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'PROPOSTA': 'Proposta',
      'CONTRAPROPOSTA': 'Contraproposta',
      'ACEITA': 'Aceita',
      'RECUSADA': 'Recusada',
      'CANCELADA': 'Cancelada'
    };
    return labels[status] || status;
  }

  getCountByStatus(status: string): number {
    return this.negociacoes.filter(n => n.negStatus === status).length;
  }

  ligar(negociacao: Negociacao): void {
    if (negociacao.negTelefone) {
      window.open(`tel:${negociacao.negTelefone}`, '_self');
    }
  }

  abrirWhatsApp(negociacao: Negociacao): void {
    if (negociacao.negTelefone) {
      const phone = negociacao.negTelefone.replace(/\D/g, '');
      const fullPhone = phone.startsWith('55') ? phone : '55' + phone;
      const veiculo = this.getVeiculoNome(negociacao.r_VeiId);
      const msg = encodeURIComponent(`Olá! Sobre a negociação do veículo ${veiculo}, gostaria de conversar sobre a proposta.`);
      window.open(`https://wa.me/${fullPhone}?text=${msg}`, '_blank');
    }
  }

  hasError(field: string, error: string): boolean {
    const control = this.form.get(field);
    return control ? control.hasError(error) && control.touched : false;
  }
}
