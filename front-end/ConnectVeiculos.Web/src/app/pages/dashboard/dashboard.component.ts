import { Component, inject, OnInit, OnDestroy, ElementRef, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DashboardService, VeiculoService, VendaService } from '../../core/services';
import { LeadService, Lead } from '../../core/services/lead.service';
import { TestDriveService, TestDrive } from '../../core/services/testdrive.service';
import {
  Dashboard, Veiculo, Venda, VeiculoPorCategoria, VeiculoPorLoja, VeiculoRecente,
  VendasPorPeriodo, FaturamentoMensal, TopVeiculosVendidos, ComparativoMensal
} from '../../core/models';
import { Chart, registerables } from 'chart.js';
import { forkJoin } from 'rxjs';

// Registrar todos os componentes do Chart.js
Chart.register(...registerables);

export interface VeiculoAlertaEstoque {
  veiId: number;
  marca: string;
  modelo: string;
  ano: number;
  preco: number;
  loja: string;
  diasEmEstoque: number;
  dtEntrada: Date;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit, OnDestroy, AfterViewInit {
  private dashboardService = inject(DashboardService);
  private leadService = inject(LeadService);
  private testDriveService = inject(TestDriveService);
  private veiculoService = inject(VeiculoService);
  private vendaService = inject(VendaService);

  @ViewChild('chartCategorias') chartCategoriasCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('chartLojas') chartLojasCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('chartStatus') chartStatusCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('chartVendasPeriodo') chartVendasPeriodoCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('chartFaturamento') chartFaturamentoCanvas!: ElementRef<HTMLCanvasElement>;

  private chartCategorias: Chart | null = null;
  private chartLojas: Chart | null = null;
  private chartStatus: Chart | null = null;
  private chartVendasPeriodo: Chart | null = null;
  private chartFaturamento: Chart | null = null;

  loading = false;
  loadingAvancado = false;
  dashboard: Dashboard | null = null;

  // Dashboard Avancado
  vendasPorPeriodo: VendasPorPeriodo | null = null;
  faturamentoMensal: FaturamentoMensal | null = null;
  topVeiculos: TopVeiculosVendidos | null = null;
  comparativoMensal: ComparativoMensal | null = null;

  // Novas estatisticas
  leadsEsteMes: number = 0;
  testDrivesPendentes: number = 0;
  veiculosParados30Dias: number = 0;
  tempoMedioVendaDias: number = 0;
  margemLucroMedia: number = 0;
  alertasEstoque: VeiculoAlertaEstoque[] = [];

  // Filtros
  anoFaturamento: number = new Date().getFullYear();
  anosDisponiveis: number[] = [];
  periodoVendas = 30;

  ngOnInit(): void {
    this.initAnosDisponiveis();
    this.loadDashboard();
    this.loadDashboardAvancado();
    this.loadNovasEstatisticas();
  }

  ngAfterViewInit(): void {
    // Charts will be created after data loads
  }

  ngOnDestroy(): void {
    this.destroyCharts();
  }

  private initAnosDisponiveis(): void {
    const anoAtual = new Date().getFullYear();
    for (let i = anoAtual; i >= anoAtual - 5; i--) {
      this.anosDisponiveis.push(i);
    }
  }

  private destroyCharts(): void {
    if (this.chartCategorias) {
      this.chartCategorias.destroy();
      this.chartCategorias = null;
    }
    if (this.chartLojas) {
      this.chartLojas.destroy();
      this.chartLojas = null;
    }
    if (this.chartStatus) {
      this.chartStatus.destroy();
      this.chartStatus = null;
    }
    if (this.chartVendasPeriodo) {
      this.chartVendasPeriodo.destroy();
      this.chartVendasPeriodo = null;
    }
    if (this.chartFaturamento) {
      this.chartFaturamento.destroy();
      this.chartFaturamento = null;
    }
  }

  private loadDashboard(): void {
    this.loading = true;
    this.dashboardService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard = data;
        this.loading = false;
        // Aguardar o DOM atualizar antes de criar os graficos
        setTimeout(() => this.createCharts(), 100);
      },
      error: () => this.loading = false
    });
  }

  private createCharts(): void {
    if (!this.dashboard) return;
    this.destroyCharts();
    this.createCategoriasChart();
    this.createLojasChart();
    this.createStatusChart();
  }

  private createCategoriasChart(): void {
    if (!this.chartCategoriasCanvas || !this.dashboard?.veiculosPorCategoria.length) return;

    const ctx = this.chartCategoriasCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const data = this.dashboard.veiculosPorCategoria;
    const colors = this.generateColors(data.length);

    this.chartCategorias = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: data.map(d => d.categoria),
        datasets: [{
          data: data.map(d => d.quantidade),
          backgroundColor: colors,
          borderWidth: 2,
          borderColor: '#fff'
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'right',
            labels: {
              padding: 15,
              usePointStyle: true
            }
          },
          title: {
            display: false
          }
        }
      }
    });
  }

  private createLojasChart(): void {
    if (!this.chartLojasCanvas || !this.dashboard?.veiculosPorLoja.length) return;

    const ctx = this.chartLojasCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const data = this.dashboard.veiculosPorLoja;

    this.chartLojas = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: data.map(d => d.loja),
        datasets: [
          {
            label: 'Quantidade',
            data: data.map(d => d.quantidade),
            backgroundColor: 'rgba(26, 35, 126, 0.7)',
            borderColor: 'rgba(26, 35, 126, 1)',
            borderWidth: 1,
            yAxisID: 'y'
          },
          {
            label: 'Valor (R$ mil)',
            data: data.map(d => d.valorTotal / 1000),
            backgroundColor: 'rgba(76, 175, 80, 0.7)',
            borderColor: 'rgba(76, 175, 80, 1)',
            borderWidth: 1,
            yAxisID: 'y1'
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'top'
          }
        },
        scales: {
          y: {
            type: 'linear',
            display: true,
            position: 'left',
            title: {
              display: true,
              text: 'Quantidade'
            }
          },
          y1: {
            type: 'linear',
            display: true,
            position: 'right',
            title: {
              display: true,
              text: 'Valor (R$ mil)'
            },
            grid: {
              drawOnChartArea: false
            }
          }
        }
      }
    });
  }

  private createStatusChart(): void {
    if (!this.chartStatusCanvas || !this.dashboard) return;

    const ctx = this.chartStatusCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    this.chartStatus = new Chart(ctx, {
      type: 'pie',
      data: {
        labels: ['Disponiveis', 'Vendidos', 'Reservados'],
        datasets: [{
          data: [
            this.dashboard.veiculosDisponiveis,
            this.dashboard.veiculosVendidos,
            this.dashboard.veiculosReservados
          ],
          backgroundColor: [
            'rgba(76, 175, 80, 0.8)',
            'rgba(33, 150, 243, 0.8)',
            'rgba(255, 152, 0, 0.8)'
          ],
          borderWidth: 2,
          borderColor: '#fff'
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom',
            labels: {
              padding: 15,
              usePointStyle: true
            }
          }
        }
      }
    });
  }

  private generateColors(count: number): string[] {
    const baseColors = [
      'rgba(26, 35, 126, 0.8)',
      'rgba(76, 175, 80, 0.8)',
      'rgba(255, 152, 0, 0.8)',
      'rgba(33, 150, 243, 0.8)',
      'rgba(156, 39, 176, 0.8)',
      'rgba(244, 67, 54, 0.8)',
      'rgba(0, 150, 136, 0.8)',
      'rgba(255, 193, 7, 0.8)'
    ];

    const colors: string[] = [];
    for (let i = 0; i < count; i++) {
      colors.push(baseColors[i % baseColors.length]);
    }
    return colors;
  }

  formatarPreco(valor: number): string {
    return valor.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  getStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      'D': 'Disponivel',
      'V': 'Vendido',
      'R': 'Reservado'
    };
    return labels[status] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      'D': 'disponivel',
      'V': 'vendido',
      'R': 'reservado'
    };
    return classes[status] || '';
  }

  getMaxQuantidade(items: VeiculoPorCategoria[] | VeiculoPorLoja[]): number {
    if (!items || items.length === 0) return 1;
    return Math.max(...items.map(i => i.quantidade));
  }

  getBarWidth(quantidade: number, max: number): number {
    return (quantidade / max) * 100;
  }

  // Dashboard Avancado Methods
  private loadDashboardAvancado(): void {
    this.loadingAvancado = true;

    // Carregar comparativo mensal
    this.dashboardService.getComparativoMensal().subscribe({
      next: (data) => this.comparativoMensal = data,
      error: () => console.error('Erro ao carregar comparativo mensal')
    });

    // Carregar top veiculos
    this.dashboardService.getTopVeiculos(10).subscribe({
      next: (data) => this.topVeiculos = data,
      error: () => console.error('Erro ao carregar top veiculos')
    });

    // Carregar vendas por periodo
    this.loadVendasPorPeriodo();

    // Carregar faturamento mensal
    this.loadFaturamentoMensal();
  }

  loadVendasPorPeriodo(): void {
    const dataFim = new Date();
    const dataInicio = new Date();
    dataInicio.setDate(dataInicio.getDate() - this.periodoVendas);

    this.dashboardService.getVendasPorPeriodo(dataInicio, dataFim).subscribe({
      next: (data) => {
        this.vendasPorPeriodo = data;
        this.loadingAvancado = false;
        setTimeout(() => this.createVendasPeriodoChart(), 100);
      },
      error: () => this.loadingAvancado = false
    });
  }

  loadFaturamentoMensal(): void {
    this.dashboardService.getFaturamentoMensal(this.anoFaturamento).subscribe({
      next: (data) => {
        this.faturamentoMensal = data;
        setTimeout(() => this.createFaturamentoChart(), 100);
      },
      error: () => console.error('Erro ao carregar faturamento mensal')
    });
  }

  onPeriodoChange(): void {
    this.loadVendasPorPeriodo();
  }

  onAnoChange(): void {
    this.loadFaturamentoMensal();
  }

  private createVendasPeriodoChart(): void {
    if (!this.chartVendasPeriodoCanvas || !this.vendasPorPeriodo?.vendas.length) return;

    if (this.chartVendasPeriodo) {
      this.chartVendasPeriodo.destroy();
    }

    const ctx = this.chartVendasPeriodoCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const data = this.vendasPorPeriodo.vendas;

    this.chartVendasPeriodo = new Chart(ctx, {
      type: 'line',
      data: {
        labels: data.map(d => this.formatarDataCurta(d.data)),
        datasets: [
          {
            label: 'Valor (R$)',
            data: data.map(d => d.valor),
            borderColor: 'rgba(26, 35, 126, 1)',
            backgroundColor: 'rgba(26, 35, 126, 0.1)',
            fill: true,
            tension: 0.4,
            yAxisID: 'y'
          },
          {
            label: 'Quantidade',
            data: data.map(d => d.quantidade),
            borderColor: 'rgba(76, 175, 80, 1)',
            backgroundColor: 'rgba(76, 175, 80, 0.1)',
            fill: false,
            tension: 0.4,
            yAxisID: 'y1'
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          mode: 'index',
          intersect: false
        },
        plugins: {
          legend: {
            position: 'top'
          },
          tooltip: {
            callbacks: {
              label: (context) => {
                const label = context.dataset.label || '';
                const value = context.parsed.y ?? 0;
                if (label.includes('Valor')) {
                  return `${label}: ${this.formatarPreco(value)}`;
                }
                return `${label}: ${value}`;
              }
            }
          }
        },
        scales: {
          y: {
            type: 'linear',
            display: true,
            position: 'left',
            title: {
              display: true,
              text: 'Valor (R$)'
            },
            ticks: {
              callback: (value) => this.formatarPrecoResumido(Number(value))
            }
          },
          y1: {
            type: 'linear',
            display: true,
            position: 'right',
            title: {
              display: true,
              text: 'Quantidade'
            },
            grid: {
              drawOnChartArea: false
            }
          }
        }
      }
    });
  }

  private createFaturamentoChart(): void {
    if (!this.chartFaturamentoCanvas || !this.faturamentoMensal?.meses.length) return;

    if (this.chartFaturamento) {
      this.chartFaturamento.destroy();
    }

    const ctx = this.chartFaturamentoCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const data = this.faturamentoMensal.meses;

    this.chartFaturamento = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: data.map(d => d.mes),
        datasets: [
          {
            label: 'Faturamento',
            data: data.map(d => d.faturamento),
            backgroundColor: 'rgba(26, 35, 126, 0.7)',
            borderColor: 'rgba(26, 35, 126, 1)',
            borderWidth: 1
          },
          {
            label: 'Lucro',
            data: data.map(d => d.lucro),
            backgroundColor: 'rgba(76, 175, 80, 0.7)',
            borderColor: 'rgba(76, 175, 80, 1)',
            borderWidth: 1
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'top'
          },
          tooltip: {
            callbacks: {
              label: (context) => {
                const label = context.dataset.label || '';
                const value = context.parsed.y ?? 0;
                return `${label}: ${this.formatarPreco(value)}`;
              }
            }
          }
        },
        scales: {
          y: {
            beginAtZero: true,
            ticks: {
              callback: (value) => this.formatarPrecoResumido(Number(value))
            }
          }
        }
      }
    });
  }

  formatarDataCurta(dataStr: string): string {
    const data = new Date(dataStr);
    return data.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' });
  }

  formatarPrecoResumido(valor: number): string {
    if (valor >= 1000000) {
      return `R$ ${(valor / 1000000).toFixed(1)}M`;
    }
    if (valor >= 1000) {
      return `R$ ${(valor / 1000).toFixed(0)}K`;
    }
    return `R$ ${valor.toFixed(0)}`;
  }

  getVariacaoClass(variacao: number): string {
    if (variacao > 0) return 'positivo';
    if (variacao < 0) return 'negativo';
    return 'neutro';
  }

  getVariacaoIcon(variacao: number): string {
    if (variacao > 0) return 'trending_up';
    if (variacao < 0) return 'trending_down';
    return 'trending_flat';
  }

  // Novas estatisticas
  private loadNovasEstatisticas(): void {
    forkJoin({
      leads: this.leadService.listar(),
      testDrives: this.testDriveService.listar(),
      veiculos: this.veiculoService.getAll(),
      vendas: this.vendaService.getAll()
    }).subscribe({
      next: ({ leads, testDrives, veiculos, vendas }) => {
        this.calcularLeadsEsteMes(leads);
        this.calcularTestDrivesPendentes(testDrives);
        this.calcularVeiculosParados(veiculos);
        this.calcularTempoMedioVenda(veiculos, vendas);
        this.calcularMargemLucroMedia(vendas, veiculos);
      },
      error: (err) => console.error('Erro ao carregar estatisticas adicionais', err)
    });
  }

  private calcularLeadsEsteMes(leads: Lead[]): void {
    const agora = new Date();
    const inicioMes = new Date(agora.getFullYear(), agora.getMonth(), 1);
    this.leadsEsteMes = leads.filter(l => {
      const dtCriacao = new Date(l.leaDtCriacao);
      return dtCriacao >= inicioMes;
    }).length;
  }

  private calcularTestDrivesPendentes(testDrives: TestDrive[]): void {
    this.testDrivesPendentes = testDrives.filter(td =>
      td.tdrStatus?.toUpperCase() === 'PENDENTE' ||
      td.tdrStatus?.toUpperCase() === 'AGENDADO' ||
      td.tdrStatus?.toUpperCase() === 'P' ||
      td.tdrStatus?.toUpperCase() === 'A'
    ).length;
  }

  private calcularVeiculosParados(veiculos: Veiculo[]): void {
    const agora = new Date();
    const limiar30Dias = new Date();
    limiar30Dias.setDate(agora.getDate() - 30);

    const disponiveis = veiculos.filter(v =>
      v.veiSts === 'D' || v.veiSts?.toUpperCase() === 'DISPONIVEL'
    );

    this.alertasEstoque = disponiveis
      .filter(v => {
        if (!v.veiDtEntrada) return false;
        const dtEntrada = new Date(v.veiDtEntrada);
        return dtEntrada <= limiar30Dias;
      })
      .map(v => {
        const dtEntrada = new Date(v.veiDtEntrada);
        const diffMs = agora.getTime() - dtEntrada.getTime();
        const diasEmEstoque = Math.floor(diffMs / (1000 * 60 * 60 * 24));
        return {
          veiId: v.veiId,
          marca: v.veiMarca,
          modelo: v.veiModelo,
          ano: v.veiAno,
          preco: v.veiPreco,
          loja: v.lojaNome || '',
          diasEmEstoque,
          dtEntrada
        };
      })
      .sort((a, b) => b.diasEmEstoque - a.diasEmEstoque);

    this.veiculosParados30Dias = this.alertasEstoque.length;
  }

  private calcularTempoMedioVenda(veiculos: Veiculo[], vendas: Venda[]): void {
    const vendasConcluidas = vendas.filter(v => v.venStatus !== 'E'); // excluir estornos
    if (vendasConcluidas.length === 0) {
      this.tempoMedioVendaDias = 0;
      return;
    }

    // Criar mapa de veiculos por ID para buscar data de entrada
    const veiculoMap = new Map<number, Veiculo>();
    veiculos.forEach(v => veiculoMap.set(v.veiId, v));

    let totalDias = 0;
    let count = 0;

    vendasConcluidas.forEach(venda => {
      const veiculo = veiculoMap.get(venda.r_VeiId);
      if (veiculo?.veiDtEntrada && venda.venDtVenda) {
        const dtEntrada = new Date(veiculo.veiDtEntrada);
        const dtVenda = new Date(venda.venDtVenda);
        const diffMs = dtVenda.getTime() - dtEntrada.getTime();
        const dias = Math.floor(diffMs / (1000 * 60 * 60 * 24));
        if (dias >= 0) {
          totalDias += dias;
          count++;
        }
      }
    });

    this.tempoMedioVendaDias = count > 0 ? Math.round(totalDias / count) : 0;
  }

  private calcularMargemLucroMedia(vendas: Venda[], veiculos: Veiculo[]): void {
    const veiculoMap = new Map<number, Veiculo>();
    veiculos.forEach(v => veiculoMap.set(v.veiId, v));

    const vendasConcluidas = vendas.filter(v => v.venStatus !== 'E');
    let totalMargem = 0;
    let count = 0;

    vendasConcluidas.forEach(venda => {
      const veiculo = veiculoMap.get(venda.r_VeiId);
      if (veiculo?.veiPrecoCompra && veiculo.veiPrecoCompra > 0 && venda.venValor > 0) {
        const margem = ((venda.venValor - veiculo.veiPrecoCompra) / veiculo.veiPrecoCompra) * 100;
        totalMargem += margem;
        count++;
      }
    });

    this.margemLucroMedia = count > 0 ? Math.round(totalMargem / count * 10) / 10 : 0;
  }

  getDiasEstoqueClass(dias: number): string {
    if (dias > 90) return 'critico';
    if (dias > 60) return 'alerta';
    return 'atencao';
  }
}
