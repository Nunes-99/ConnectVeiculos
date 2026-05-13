import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FinanciamentoBancoService, SimulacaoRequest, SimulacaoResultado, BancoInfo } from '../../core/services';
import { VeiculoService } from '../../core/services';
import { ToastService } from '../../core/services';
import { Veiculo } from '../../core/models';
import { MaskDirective, CurrencyMaskDirective } from '../../shared/directives';

@Component({
  selector: 'app-financiamentos',
  standalone: true,
  imports: [CommonModule, FormsModule, MaskDirective, CurrencyMaskDirective],
  templateUrl: './financiamentos.component.html',
  styleUrl: './financiamentos.component.scss'
})
export class FinanciamentosComponent implements OnInit {
  private financiamentoService = inject(FinanciamentoBancoService);
  private veiculoService = inject(VeiculoService);
  private toast = inject(ToastService);

  bancos: BancoInfo[] = [];
  veiculos: Veiculo[] = [];
  resultados: SimulacaoResultado[] = [];
  loading = false;
  simulado = false;

  // Formulario
  veiculoSelecionadoId: number | null = null;
  veiculoSelecionado: Veiculo | null = null;
  cpfCliente = '';
  rendaMensal = 0;
  valorEntrada = 0;
  parcelas = 48;
  bancoSelecionado = 'TODOS';

  opcoesParc = [12, 24, 36, 48, 60];

  ngOnInit(): void {
    this.financiamentoService.listarBancos().subscribe({
      next: (bancos) => this.bancos = bancos
    });
    this.veiculoService.getAll().subscribe({
      next: (veiculos) => this.veiculos = veiculos.filter((v: any) => v.veiSts === 'D')
    });
  }

  onVeiculoChange(): void {
    if (this.veiculoSelecionadoId) {
      this.veiculoSelecionado = this.veiculos.find(v => v.veiId === this.veiculoSelecionadoId) || null;
      if (this.veiculoSelecionado) {
        this.valorEntrada = Math.round(this.veiculoSelecionado.veiPreco * 0.2);
      }
    } else {
      this.veiculoSelecionado = null;
    }
    this.simulado = false;
    this.resultados = [];
  }

  simular(): void {
    if (!this.veiculoSelecionado || !this.cpfCliente || !this.rendaMensal) {
      this.toast.warning('Preencha todos os campos obrigatórios.');
      return;
    }

    this.loading = true;
    this.simulado = false;
    this.resultados = [];

    const request: SimulacaoRequest = {
      valorVeiculo: this.veiculoSelecionado.veiPreco,
      valorEntrada: this.valorEntrada,
      parcelas: this.parcelas,
      anoVeiculo: this.veiculoSelecionado.veiAno,
      tipoVeiculo: 'USADO',
      cpfCliente: this.cpfCliente,
      rendaMensal: this.rendaMensal
    };

    if (this.bancoSelecionado === 'TODOS') {
      this.financiamentoService.simularTodos(request).subscribe({
        next: (resultados) => {
          this.resultados = resultados;
          this.simulado = true;
          this.loading = false;
        },
        error: () => {
          this.toast.error('Erro ao simular financiamento.');
          this.loading = false;
        }
      });
    } else {
      this.financiamentoService.simularBanco(this.bancoSelecionado, request).subscribe({
        next: (resultado) => {
          this.resultados = [resultado];
          this.simulado = true;
          this.loading = false;
        },
        error: () => {
          this.toast.error('Erro ao simular financiamento.');
          this.loading = false;
        }
      });
    }
  }

  formatarMoeda(valor: number): string {
    return valor?.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }) || 'R$ 0,00';
  }

  formatarTaxa(valor: number): string {
    return valor?.toFixed(2) + '% a.m.' || '0,00% a.m.';
  }
}
