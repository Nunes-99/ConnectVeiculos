import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LogService } from '../../core/services';
import { LogAuditoria } from '../../core/models';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

@Component({
  selector: 'app-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, PaginationComponent],
  templateUrl: './logs.component.html',
  styleUrl: './logs.component.scss'
})
export class LogsComponent implements OnInit {
  private logService = inject(LogService);

  logs: LogAuditoria[] = [];
  loading = false;
  showDetalheModal = false;
  logSelecionado: LogAuditoria | null = null;

  // Paginacao
  page = 1;
  pageSize = 10;
  totalItems = 0;
  totalPages = 0;

  // Filtros
  filtroTabela = '';
  filtroAcao = '';
  filtroDataInicio = '';
  filtroDataFim = '';

  tabelas: string[] = [];
  acoes: string[] = [];

  acaoLabels: Record<string, string> = {
    'INSERT': 'Cadastro',
    'UPDATE': 'Alteração',
    'DELETE': 'Exclusão'
  };

  tabelaLabels: Record<string, string> = {
    'Usuario': 'Usuário',
    'Veiculo': 'Veículo',
    'Loja': 'Loja',
    'Categoria': 'Categoria',
    'Acesso': 'Acesso',
    'Venda': 'Venda'
  };

  ngOnInit(): void {
    this.loadFiltros();
    this.loadData();
  }

  private loadFiltros(): void {
    this.logService.getTabelas().subscribe({
      next: (data) => this.tabelas = data
    });
    this.logService.getAcoes().subscribe({
      next: (data) => this.acoes = data
    });
  }

  private loadData(): void {
    this.loading = true;
    this.logService.getPaged(
      this.page,
      this.pageSize,
      this.filtroTabela || undefined,
      this.filtroAcao || undefined,
      this.filtroDataInicio || undefined,
      this.filtroDataFim || undefined
    ).subscribe({
      next: (result) => {
        this.logs = result.items;
        this.totalItems = result.totalItems;
        this.totalPages = result.totalPages;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  aplicarFiltros(): void {
    this.page = 1;
    this.loadData();
  }

  limparFiltros(): void {
    this.filtroTabela = '';
    this.filtroAcao = '';
    this.filtroDataInicio = '';
    this.filtroDataFim = '';
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

  verDetalhes(log: LogAuditoria): void {
    this.logService.getById(log.logId).subscribe({
      next: (data) => {
        this.logSelecionado = data;
        this.showDetalheModal = true;
      }
    });
  }

  fecharDetalheModal(): void {
    this.showDetalheModal = false;
    this.logSelecionado = null;
  }

  getAcaoClass(acao: string): string {
    switch (acao) {
      case 'INSERT': return 'badge-insert';
      case 'UPDATE': return 'badge-update';
      case 'DELETE': return 'badge-delete';
      default: return '';
    }
  }

  formatarData(data: string): string {
    if (!data) return '-';
    return new Date(data).toLocaleString('pt-BR');
  }

  formatarJson(json: string | null): string {
    if (!json) return '-';
    try {
      return JSON.stringify(JSON.parse(json), null, 2);
    } catch {
      return json;
    }
  }
}
