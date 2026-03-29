import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { VendaService, VeiculoService, UsuarioService, ToastService } from '../../core/services';
import { Venda, Veiculo, Usuario } from '../../core/models';
import { MaskDirective, CurrencyMaskDirective } from '../../shared/directives';
import { PaginationComponent } from '../../shared/components';

@Component({
  selector: 'app-vendas',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MaskDirective, CurrencyMaskDirective, PaginationComponent],
  templateUrl: './vendas.component.html',
  styleUrl: './vendas.component.scss'
})
export class VendasComponent implements OnInit {
  private vendaService = inject(VendaService);
  private veiculoService = inject(VeiculoService);
  private usuarioService = inject(UsuarioService);
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private toast = inject(ToastService);

  vendas: Venda[] = [];
  vendasPaginadas: Venda[] = [];
  veiculos: Veiculo[] = [];
  usuarios: Usuario[] = [];
  loading = false;
  loadingCep = false;
  showModal = false;

  // Paginacao
  currentPage = 1;
  pageSize = 10;
  totalItems = 0;

  form: FormGroup = this.fb.group({
    r_VeiId: [0, [Validators.required, Validators.min(1)]],
    r_UsuId: [0, [Validators.required, Validators.min(1)]],
    venDtVenda: [new Date().toISOString().split('T')[0], Validators.required],
    venValor: [0, [Validators.required, Validators.min(0)]],
    venComissaoPorc: [5, [Validators.required, Validators.min(0), Validators.max(100)]],
    // Dados do Comprador
    venCompradorNome: ['', [Validators.required, Validators.minLength(3)]],
    venCompradorCpf: [''],
    venCompradorTelefone: [''],
    venCompradorEmail: ['', Validators.email],
    // Endereco
    cep: [''],
    logradouro: [''],
    bairro: [''],
    cidade: [''],
    uf: [''],
    numero: [''],
    // Forma de Pagamento
    venFormaPagamento: [''],
    venObservacao: ['']
  });

  formasPagamento = [
    { value: 'DINHEIRO', label: 'Dinheiro' },
    { value: 'PIX', label: 'PIX' },
    { value: 'CARTAO_CREDITO', label: 'Cartão de Crédito' },
    { value: 'CARTAO_DEBITO', label: 'Cartão de Débito' },
    { value: 'FINANCIAMENTO', label: 'Financiamento' },
    { value: 'CONSORCIO', label: 'Consórcio' },
    { value: 'TROCA', label: 'Troca' },
    { value: 'MISTO', label: 'Misto' }
  ];

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;
    this.vendaService.getAll().subscribe({
      next: (data) => {
        this.vendas = data;
        this.totalItems = data.length;
        this.updatePaginatedVendas();
        this.loading = false;
      },
      error: () => this.loading = false
    });

    this.veiculoService.getAll().subscribe({
      next: (data) => {
        // Filtrar apenas veiculos disponiveis (status D)
        this.veiculos = data.filter(v => v.veiSts === 'D');
      }
    });

    this.usuarioService.getAll().subscribe({
      next: (data) => this.usuarios = data
    });
  }

  openModal(): void {
    this.form.reset({
      r_VeiId: 0,
      r_UsuId: 0,
      venDtVenda: new Date().toISOString().split('T')[0],
      venValor: 0,
      venComissaoPorc: 5,
      venCompradorNome: '',
      venCompradorCpf: '',
      venCompradorTelefone: '',
      venCompradorEmail: '',
      cep: '',
      logradouro: '',
      bairro: '',
      cidade: '',
      uf: '',
      numero: '',
      venFormaPagamento: '',
      venObservacao: ''
    });
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.form.reset();
  }

  buscarCep(): void {
    const cep = this.form.get('cep')?.value?.replace(/\D/g, '');
    if (!cep || cep.length !== 8) return;

    this.loadingCep = true;
    this.http.get<any>(`https://viacep.com.br/ws/${cep}/json/`).subscribe({
      next: (data) => {
        this.loadingCep = false;
        if (data.erro) {
          this.toast.warning('CEP nao encontrado.');
          return;
        }
        this.form.patchValue({
          logradouro: data.logradouro || '',
          bairro: data.bairro || '',
          cidade: data.localidade || '',
          uf: data.uf || ''
        });
      },
      error: () => {
        this.loadingCep = false;
        this.toast.error('Erro ao buscar CEP.');
      }
    });
  }

  onVeiculoChange(): void {
    const veiculoId = this.form.get('r_VeiId')?.value;
    const veiculo = this.veiculos.find(v => v.veiId == veiculoId);
    if (veiculo) {
      this.form.patchValue({ venValor: veiculo.veiPreco });
    }
  }

  calcularComissao(): number {
    const valor = this.form.get('venValor')?.value || 0;
    const porc = this.form.get('venComissaoPorc')?.value || 0;
    return valor * (porc / 100);
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.value;
    const partes = [formValue.logradouro, formValue.numero, formValue.bairro, formValue.cidade, formValue.uf, formValue.cep].filter(p => p);
    const data = {
      ...formValue,
      venCompradorEndereco: partes.join(', '),
      venDtVenda: new Date(formValue.venDtVenda)
    };
    delete data.cep;
    delete data.logradouro;
    delete data.bairro;
    delete data.cidade;
    delete data.uf;
    delete data.numero;

    this.vendaService.create(data).subscribe({
      next: () => {
        this.loadData();
        this.closeModal();
        this.toast.success('Venda registrada com sucesso!');
      },
      error: (err) => {
        this.toast.error(err.error?.message || 'Erro ao registrar venda');
      }
    });
  }

  formatarPreco(valor: number): string {
    return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  formatarData(data: Date): string {
    return new Date(data).toLocaleDateString('pt-BR');
  }

  hasError(field: string, error: string): boolean {
    const control = this.form.get(field);
    return control ? control.hasError(error) && control.touched : false;
  }

  estornar(venda: Venda): void {
    if (venda.venStatus === 'E') {
      this.toast.warning('Esta venda ja foi estornada.');
      return;
    }

    if (confirm(`Deseja estornar a venda de ${venda.venMarca} ${venda.venModelo}?\nO veiculo voltara para o status Disponivel.`)) {
      this.vendaService.estornar(venda.venId).subscribe({
        next: () => {
          this.loadData();
          this.toast.success('Venda estornada com sucesso!');
        },
        error: (err) => {
          this.toast.error(err.error?.message || 'Erro ao estornar venda');
        }
      });
    }
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'A': 'Ativa',
      'E': 'Estornada'
    };
    return labels[status] || status;
  }

  getFormaPagamentoLabel(formaPagamento: string): string {
    const forma = this.formasPagamento.find(f => f.value === formaPagamento);
    return forma ? forma.label : formaPagamento || '-';
  }

  // Paginacao
  updatePaginatedVendas(): void {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    const endIndex = startIndex + this.pageSize;
    this.vendasPaginadas = this.vendas.slice(startIndex, endIndex);
  }

  onPageChange(page: number): void {
    this.currentPage = page;
    this.updatePaginatedVendas();
  }
}
