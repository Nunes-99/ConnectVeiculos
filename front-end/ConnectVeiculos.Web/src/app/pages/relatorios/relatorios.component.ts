import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RelatorioService, LojaService, CategoriaService } from '../../core/services';
import { RelatorioVendas, RelatorioEstoque, RelatorioFinanceiro, Loja, Categoria } from '../../core/models';
import jsPDF from 'jspdf';
import autoTable from 'jspdf-autotable';
import * as XLSX from 'xlsx';

@Component({
  selector: 'app-relatorios',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './relatorios.component.html',
  styleUrl: './relatorios.component.scss'
})
export class RelatoriosComponent implements OnInit {
  private relatorioService = inject(RelatorioService);
  private lojaService = inject(LojaService);
  private categoriaService = inject(CategoriaService);

  // Dados
  lojas: Loja[] = [];
  categorias: Categoria[] = [];
  relatorioVendas: RelatorioVendas | null = null;
  relatorioEstoque: RelatorioEstoque | null = null;
  relatorioFinanceiro: RelatorioFinanceiro | null = null;

  // Tab ativa
  activeTab = 'vendas';

  // Filtros
  dataInicio = '';
  dataFim = '';
  filtroLoja: number | null = null;
  filtroCategoria: number | null = null;

  // Loading
  loading = false;

  ngOnInit(): void {
    this.setDefaultDates();
    this.loadLojas();
    this.loadCategorias();
    this.loadRelatorioVendas();
  }

  setDefaultDates(): void {
    const hoje = new Date();
    const primeiroDia = new Date(hoje.getFullYear(), hoje.getMonth(), 1);
    this.dataInicio = primeiroDia.toISOString().split('T')[0];
    this.dataFim = hoje.toISOString().split('T')[0];
  }

  loadLojas(): void {
    this.lojaService.getAll().subscribe({
      next: (data) => this.lojas = data
    });
  }

  loadCategorias(): void {
    this.categoriaService.getAll().subscribe({
      next: (data) => this.categorias = data
    });
  }

  setActiveTab(tab: string): void {
    this.activeTab = tab;
    if (tab === 'vendas' && !this.relatorioVendas) {
      this.loadRelatorioVendas();
    } else if (tab === 'estoque' && !this.relatorioEstoque) {
      this.loadRelatorioEstoque();
    } else if (tab === 'financeiro' && !this.relatorioFinanceiro) {
      this.loadRelatorioFinanceiro();
    }
  }

  loadRelatorioVendas(): void {
    this.loading = true;
    this.relatorioService.getRelatorioVendas(
      this.dataInicio || undefined,
      this.dataFim || undefined,
      this.filtroLoja || undefined
    ).subscribe({
      next: (data) => {
        this.relatorioVendas = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  loadRelatorioEstoque(): void {
    this.loading = true;
    this.relatorioService.getRelatorioEstoque(
      this.filtroLoja || undefined,
      this.filtroCategoria || undefined
    ).subscribe({
      next: (data) => {
        this.relatorioEstoque = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  loadRelatorioFinanceiro(): void {
    this.loading = true;
    this.relatorioService.getRelatorioFinanceiro(
      this.dataInicio || undefined,
      this.dataFim || undefined,
      this.filtroLoja || undefined
    ).subscribe({
      next: (data) => {
        this.relatorioFinanceiro = data;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  aplicarFiltros(): void {
    if (this.activeTab === 'vendas') {
      this.loadRelatorioVendas();
    } else if (this.activeTab === 'estoque') {
      this.loadRelatorioEstoque();
    } else if (this.activeTab === 'financeiro') {
      this.loadRelatorioFinanceiro();
    }
  }

  limparFiltros(): void {
    this.setDefaultDates();
    this.filtroLoja = null;
    this.filtroCategoria = null;
    this.aplicarFiltros();
  }

  formatarPreco(valor: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor);
  }

  formatarPorcentagem(valor: number): string {
    return valor.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + '%';
  }

  imprimirRelatorio(): void {
    window.print();
  }

  getTituloRelatorio(): string {
    switch (this.activeTab) {
      case 'vendas':
        return 'Relatório de Vendas';
      case 'estoque':
        return 'Relatório de Estoque';
      case 'financeiro':
        return 'Relatório Financeiro';
      default:
        return 'Relatório';
    }
  }

  getPeriodoRelatorio(): string {
    if (this.activeTab === 'estoque') {
      return 'Data: ' + new Date().toLocaleDateString('pt-BR');
    }
    const inicio = this.dataInicio ? new Date(this.dataInicio).toLocaleDateString('pt-BR') : 'N/A';
    const fim = this.dataFim ? new Date(this.dataFim).toLocaleDateString('pt-BR') : 'N/A';
    return `Período: ${inicio} a ${fim}`;
  }

  getSelectedLojaNome(): string {
    if (!this.filtroLoja) return '';
    const loja = this.lojas.find(l => l.lojId === this.filtroLoja);
    return loja?.lojNome || 'N/A';
  }

  // ==================== EXPORTACAO PDF ====================

  exportarPDF(): void {
    const doc = new jsPDF();
    const titulo = this.getTituloRelatorio();
    const periodo = this.getPeriodoRelatorio();

    // Cabecalho
    doc.setFontSize(18);
    doc.text('ConnectVeiculos', 14, 20);
    doc.setFontSize(14);
    doc.text(titulo, 14, 30);
    doc.setFontSize(10);
    doc.text(periodo, 14, 38);
    if (this.filtroLoja) {
      doc.text(`Loja: ${this.getSelectedLojaNome()}`, 14, 45);
    }

    let startY = this.filtroLoja ? 55 : 48;

    if (this.activeTab === 'vendas' && this.relatorioVendas) {
      this.exportarVendasPDF(doc, startY);
    } else if (this.activeTab === 'estoque' && this.relatorioEstoque) {
      this.exportarEstoquePDF(doc, startY);
    } else if (this.activeTab === 'financeiro' && this.relatorioFinanceiro) {
      this.exportarFinanceiroPDF(doc, startY);
    }

    doc.save(`${titulo.toLowerCase().replace(/ /g, '_')}_${new Date().toISOString().split('T')[0]}.pdf`);
  }

  private exportarVendasPDF(doc: jsPDF, startY: number): void {
    const r = this.relatorioVendas!;

    // Resumo
    doc.setFontSize(12);
    doc.text('Resumo', 14, startY);
    doc.setFontSize(10);
    doc.text(`Total de Vendas: ${r.totalVendas}`, 14, startY + 8);
    doc.text(`Valor Total: ${this.formatarPreco(r.valorTotalVendas)}`, 14, startY + 14);
    doc.text(`Total Comissoes: ${this.formatarPreco(r.totalComissoes)}`, 14, startY + 20);
    doc.text(`Vendas Estornadas: ${r.vendasEstornadas}`, 14, startY + 26);

    let y = startY + 38;

    // Tabela Vendas por Mes
    if (r.vendasPorMes?.length > 0) {
      doc.setFontSize(11);
      doc.text('Vendas por Mes', 14, y);
      autoTable(doc, {
        startY: y + 4,
        head: [['Periodo', 'Quantidade', 'Valor Total']],
        body: r.vendasPorMes.map(item => [
          item.periodo,
          item.quantidade.toString(),
          this.formatarPreco(item.valorTotal)
        ]),
        theme: 'striped',
        headStyles: { fillColor: [26, 35, 126] }
      });
      y = (doc as any).lastAutoTable.finalY + 10;
    }

    // Tabela Vendas por Vendedor
    if (r.vendasPorVendedor?.length > 0) {
      doc.setFontSize(11);
      doc.text('Vendas por Vendedor', 14, y);
      autoTable(doc, {
        startY: y + 4,
        head: [['Vendedor', 'Quantidade', 'Valor Total', 'Comissoes']],
        body: r.vendasPorVendedor.map(item => [
          item.vendedorNome,
          item.quantidade.toString(),
          this.formatarPreco(item.valorTotal),
          this.formatarPreco(item.totalComissoes)
        ]),
        theme: 'striped',
        headStyles: { fillColor: [26, 35, 126] }
      });
    }
  }

  private exportarEstoquePDF(doc: jsPDF, startY: number): void {
    const r = this.relatorioEstoque!;

    // Resumo
    doc.setFontSize(12);
    doc.text('Resumo', 14, startY);
    doc.setFontSize(10);
    doc.text(`Total Veiculos: ${r.totalVeiculos}`, 14, startY + 8);
    doc.text(`Disponiveis: ${r.veiculosDisponiveis}`, 14, startY + 14);
    doc.text(`Valor Total Estoque: ${this.formatarPreco(r.valorTotalEstoque)}`, 14, startY + 20);
    doc.text(`Valor Medio: ${this.formatarPreco(r.valorMedioVeiculo)}`, 14, startY + 26);

    let y = startY + 38;

    // Tabela Estoque por Loja
    if (r.estoquePorLoja?.length > 0) {
      doc.setFontSize(11);
      doc.text('Estoque por Loja', 14, y);
      autoTable(doc, {
        startY: y + 4,
        head: [['Loja', 'Quantidade', 'Valor Total']],
        body: r.estoquePorLoja.map(item => [
          item.lojaNome,
          item.quantidade.toString(),
          this.formatarPreco(item.valorTotal)
        ]),
        theme: 'striped',
        headStyles: { fillColor: [26, 35, 126] }
      });
      y = (doc as any).lastAutoTable.finalY + 10;
    }

    // Tabela Estoque por Categoria
    if (r.estoquePorCategoria?.length > 0) {
      doc.setFontSize(11);
      doc.text('Estoque por Categoria', 14, y);
      autoTable(doc, {
        startY: y + 4,
        head: [['Categoria', 'Quantidade', 'Valor Total']],
        body: r.estoquePorCategoria.map(item => [
          item.categoriaNome,
          item.quantidade.toString(),
          this.formatarPreco(item.valorTotal)
        ]),
        theme: 'striped',
        headStyles: { fillColor: [26, 35, 126] }
      });
    }
  }

  private exportarFinanceiroPDF(doc: jsPDF, startY: number): void {
    const r = this.relatorioFinanceiro!;

    // Resumo
    doc.setFontSize(12);
    doc.text('Resumo', 14, startY);
    doc.setFontSize(10);
    doc.text(`Receita Bruta: ${this.formatarPreco(r.receitaBruta)}`, 14, startY + 8);
    doc.text(`Custo Total: ${this.formatarPreco(r.custoTotal)}`, 14, startY + 14);
    doc.text(`Lucro Liquido: ${this.formatarPreco(r.lucroLiquido)}`, 14, startY + 20);
    doc.text(`Margem de Lucro: ${this.formatarPorcentagem(r.margemLucro)}`, 14, startY + 26);
    doc.text(`Ticket Medio: ${this.formatarPreco(r.ticketMedio)}`, 100, startY + 8);
    doc.text(`Total Comissoes: ${this.formatarPreco(r.totalComissoes)}`, 100, startY + 14);
    doc.text(`Lucro Bruto: ${this.formatarPreco(r.lucroBruto)}`, 100, startY + 20);

    let y = startY + 38;

    // Tabela Financeiro por Mes
    if (r.financeiroPorMes?.length > 0) {
      doc.setFontSize(11);
      doc.text('Resultado por Mes', 14, y);
      autoTable(doc, {
        startY: y + 4,
        head: [['Periodo', 'Receita', 'Custo', 'Lucro']],
        body: r.financeiroPorMes.map(item => [
          item.periodo,
          this.formatarPreco(item.receita),
          this.formatarPreco(item.custo),
          this.formatarPreco(item.lucro)
        ]),
        theme: 'striped',
        headStyles: { fillColor: [26, 35, 126] }
      });
      y = (doc as any).lastAutoTable.finalY + 10;
    }

    // Tabela Financeiro por Loja
    if (r.financeiroPorLoja?.length > 0) {
      doc.setFontSize(11);
      doc.text('Resultado por Loja', 14, y);
      autoTable(doc, {
        startY: y + 4,
        head: [['Loja', 'Receita', 'Custo', 'Lucro']],
        body: r.financeiroPorLoja.map(item => [
          item.lojaNome,
          this.formatarPreco(item.receita),
          this.formatarPreco(item.custo),
          this.formatarPreco(item.lucro)
        ]),
        theme: 'striped',
        headStyles: { fillColor: [26, 35, 126] }
      });
    }
  }

  // ==================== EXPORTACAO EXCEL ====================

  exportarExcel(): void {
    const wb = XLSX.utils.book_new();
    const titulo = this.getTituloRelatorio();

    if (this.activeTab === 'vendas' && this.relatorioVendas) {
      this.exportarVendasExcel(wb);
    } else if (this.activeTab === 'estoque' && this.relatorioEstoque) {
      this.exportarEstoqueExcel(wb);
    } else if (this.activeTab === 'financeiro' && this.relatorioFinanceiro) {
      this.exportarFinanceiroExcel(wb);
    }

    XLSX.writeFile(wb, `${titulo.toLowerCase().replace(/ /g, '_')}_${new Date().toISOString().split('T')[0]}.xlsx`);
  }

  private exportarVendasExcel(wb: XLSX.WorkBook): void {
    const r = this.relatorioVendas!;

    // Resumo
    const resumo = [
      ['RELATORIO DE VENDAS'],
      [this.getPeriodoRelatorio()],
      [],
      ['RESUMO'],
      ['Total de Vendas', r.totalVendas],
      ['Valor Total', r.valorTotalVendas],
      ['Total Comissoes', r.totalComissoes],
      ['Vendas Estornadas', r.vendasEstornadas]
    ];
    const wsResumo = XLSX.utils.aoa_to_sheet(resumo);
    XLSX.utils.book_append_sheet(wb, wsResumo, 'Resumo');

    // Vendas por Mes
    if (r.vendasPorMes?.length > 0) {
      const dataVendasMes = [
        ['Periodo', 'Quantidade', 'Valor Total'],
        ...r.vendasPorMes.map(item => [item.periodo, item.quantidade, item.valorTotal])
      ];
      const wsVendasMes = XLSX.utils.aoa_to_sheet(dataVendasMes);
      XLSX.utils.book_append_sheet(wb, wsVendasMes, 'Vendas por Mes');
    }

    // Vendas por Vendedor
    if (r.vendasPorVendedor?.length > 0) {
      const dataVendedor = [
        ['Vendedor', 'Quantidade', 'Valor Total', 'Comissoes'],
        ...r.vendasPorVendedor.map(item => [item.vendedorNome, item.quantidade, item.valorTotal, item.totalComissoes])
      ];
      const wsVendedor = XLSX.utils.aoa_to_sheet(dataVendedor);
      XLSX.utils.book_append_sheet(wb, wsVendedor, 'Por Vendedor');
    }

    // Vendas por Forma de Pagamento
    if (r.vendasPorFormaPagamento?.length > 0) {
      const dataFormaPgto = [
        ['Forma de Pagamento', 'Quantidade', 'Valor Total'],
        ...r.vendasPorFormaPagamento.map(item => [item.formaPagamento, item.quantidade, item.valorTotal])
      ];
      const wsFormaPgto = XLSX.utils.aoa_to_sheet(dataFormaPgto);
      XLSX.utils.book_append_sheet(wb, wsFormaPgto, 'Forma Pagamento');
    }
  }

  private exportarEstoqueExcel(wb: XLSX.WorkBook): void {
    const r = this.relatorioEstoque!;

    // Resumo
    const resumo = [
      ['RELATORIO DE ESTOQUE'],
      ['Data: ' + new Date().toLocaleDateString('pt-BR')],
      [],
      ['RESUMO'],
      ['Total Veiculos', r.totalVeiculos],
      ['Disponiveis', r.veiculosDisponiveis],
      ['Valor Total Estoque', r.valorTotalEstoque],
      ['Valor Medio', r.valorMedioVeiculo]
    ];
    const wsResumo = XLSX.utils.aoa_to_sheet(resumo);
    XLSX.utils.book_append_sheet(wb, wsResumo, 'Resumo');

    // Estoque por Loja
    if (r.estoquePorLoja?.length > 0) {
      const dataLoja = [
        ['Loja', 'Quantidade', 'Valor Total'],
        ...r.estoquePorLoja.map(item => [item.lojaNome, item.quantidade, item.valorTotal])
      ];
      const wsLoja = XLSX.utils.aoa_to_sheet(dataLoja);
      XLSX.utils.book_append_sheet(wb, wsLoja, 'Por Loja');
    }

    // Estoque por Categoria
    if (r.estoquePorCategoria?.length > 0) {
      const dataCategoria = [
        ['Categoria', 'Quantidade', 'Valor Total'],
        ...r.estoquePorCategoria.map(item => [item.categoriaNome, item.quantidade, item.valorTotal])
      ];
      const wsCategoria = XLSX.utils.aoa_to_sheet(dataCategoria);
      XLSX.utils.book_append_sheet(wb, wsCategoria, 'Por Categoria');
    }

    // Estoque por Marca
    if (r.estoquePorMarca?.length > 0) {
      const dataMarca = [
        ['Marca', 'Quantidade', 'Valor Total'],
        ...r.estoquePorMarca.map(item => [item.marca, item.quantidade, item.valorTotal])
      ];
      const wsMarca = XLSX.utils.aoa_to_sheet(dataMarca);
      XLSX.utils.book_append_sheet(wb, wsMarca, 'Por Marca');
    }
  }

  private exportarFinanceiroExcel(wb: XLSX.WorkBook): void {
    const r = this.relatorioFinanceiro!;

    // Resumo
    const resumo = [
      ['RELATORIO FINANCEIRO'],
      [this.getPeriodoRelatorio()],
      [],
      ['RESUMO'],
      ['Receita Bruta', r.receitaBruta],
      ['Custo Total', r.custoTotal],
      ['Lucro Bruto', r.lucroBruto],
      ['Lucro Liquido', r.lucroLiquido],
      ['Margem de Lucro (%)', r.margemLucro],
      ['Ticket Medio', r.ticketMedio],
      ['Total Comissoes', r.totalComissoes]
    ];
    const wsResumo = XLSX.utils.aoa_to_sheet(resumo);
    XLSX.utils.book_append_sheet(wb, wsResumo, 'Resumo');

    // Financeiro por Mes
    if (r.financeiroPorMes?.length > 0) {
      const dataMes = [
        ['Periodo', 'Receita', 'Custo', 'Lucro'],
        ...r.financeiroPorMes.map(item => [item.periodo, item.receita, item.custo, item.lucro])
      ];
      const wsMes = XLSX.utils.aoa_to_sheet(dataMes);
      XLSX.utils.book_append_sheet(wb, wsMes, 'Por Mes');
    }

    // Financeiro por Loja
    if (r.financeiroPorLoja?.length > 0) {
      const dataLoja = [
        ['Loja', 'Receita', 'Custo', 'Lucro'],
        ...r.financeiroPorLoja.map(item => [item.lojaNome, item.receita, item.custo, item.lucro])
      ];
      const wsLoja = XLSX.utils.aoa_to_sheet(dataLoja);
      XLSX.utils.book_append_sheet(wb, wsLoja, 'Por Loja');
    }
  }
}
