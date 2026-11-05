import { Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../../core/services/toast.service';
import {
  MotivoRejeicaoZeroKm, RenaveConsulta, RenaveEntrega, RenaveImportacao, RenaveService, RenaveZeroKmPendente, erroRenave
} from '../../../core/services/renave.service';

type Aba = 'zero-km' | 'entregas' | 'consulta' | 'testes';

/**
 * Operacoes do Renave que nao sao de um veiculo so: importar o estoque que
 * ja esta no Renave, zero km vindos da montadora (entrada ou rejeicao),
 * entregas indicadas pela montadora e consulta de veiculo. Fica no cartao do
 * Renave em Integracoes, abaixo da configuracao.
 */
@Component({
  selector: 'app-renave-operacoes',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './renave-operacoes.component.html',
  styleUrl: './renave.scss'
})
export class RenaveOperacoesComponent implements OnInit {
  private renave = inject(RenaveService);
  private toast = inject(ToastService);

  /** Ambiente de testes: mostra a aba que cria zero km ficticio no Serpro. */
  @Input() emTestes = true;

  aba: Aba = 'zero-km';

  // Importacao
  importando = false;
  importacao: RenaveImportacao | null = null;

  // Zero km
  carregandoZeroKm = false;
  pendentes: RenaveZeroKmPendente[] | null = null;
  buscaChassi = '';
  /** Chassi com o formulario aberto e qual (entrada ou rejeicao). */
  aberto: { chassi: string; tipo: 'entrada' | 'rejeicao' } | null = null;
  entrada = { dataEntrada: this.hoje(), valorCompra: 0, quilometragemHodometro: 0, chaveNotaFiscal: '' };
  motivoRejeicao: MotivoRejeicaoZeroKm = 'DESISTENCIA';
  enviando = false;

  // Entregas
  filtroEntregas = 'PENDENTE';
  carregandoEntregas = false;
  entregas: RenaveEntrega[] | null = null;
  /** Entrega aguardando o segundo clique (confirmar) e qual acao. */
  confirmando: { id: number; acao: 'realizar' | 'cancelar' } | null = null;

  // Consulta
  busca = { placa: '', chassi: '', renavam: '' };
  consultando = false;
  consulta: RenaveConsulta | null = null;

  // Testes
  criandoZeroKm = false;
  zeroKmTeste: string | null = null;

  ngOnInit(): void {
    // Nada e' consultado so por abrir o cartao: cada consulta vai ao Serpro.
  }

  private hoje(): string {
    const d = new Date();
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  trocarAba(aba: Aba): void {
    this.aba = aba;
  }

  // ---------------- Importar ----------------

  importar(): void {
    this.importando = true;
    this.importacao = null;
    this.renave.importarEstoque().subscribe({
      next: r => {
        this.importacao = r;
        this.importando = false;
        const novos = r.vinculados + r.criados;
        this.toast.success(novos ? `${novos} veículo(s) trazido(s) do Renave.` : 'Nada novo no estoque do Renave.');
      },
      error: err => { this.importando = false; this.toast.error(erroRenave(err)); }
    });
  }

  rotuloAcao(acao: string): string {
    return ({ 'vinculado': 'Ligado ao veículo do sistema', 'criado': 'Cadastrado (Reservado)', 'ja-vinculado': 'Já estava ligado', 'ignorado': 'Ignorado' } as Record<string, string>)[acao] || acao;
  }

  // ---------------- Zero km ----------------

  carregarZeroKm(): void {
    this.carregandoZeroKm = true;
    this.renave.zeroKmPendentes().subscribe({
      next: l => { this.pendentes = l; this.carregandoZeroKm = false; },
      error: err => { this.carregandoZeroKm = false; this.pendentes = []; this.toast.error(erroRenave(err)); }
    });
  }

  /** Pendentes filtrados pelo trecho de chassi digitado (a lista ja vem do mais recente). */
  get pendentesVisiveis(): RenaveZeroKmPendente[] {
    const busca = this.buscaChassi.replace(/[^0-9a-z]/gi, '').toUpperCase();
    const lista = this.pendentes ?? [];
    return busca ? lista.filter(p => p.chassi.includes(busca)) : lista;
  }

  abrir(chassi: string, tipo: 'entrada' | 'rejeicao'): void {
    const mesmo = this.aberto?.chassi === chassi && this.aberto.tipo === tipo;
    this.aberto = mesmo ? null : { chassi, tipo };
    if (!mesmo && tipo === 'entrada') {
      this.entrada = { dataEntrada: this.hoje(), valorCompra: 0, quilometragemHodometro: 0, chaveNotaFiscal: '' };
    }
  }

  darEntrada(p: RenaveZeroKmPendente): void {
    this.enviando = true;
    this.renave.entradaZeroKm({ chassi: p.chassi, veiculoId: p.veiculoId, ...this.entrada }).subscribe({
      next: () => {
        this.enviando = false;
        this.aberto = null;
        this.toast.success(p.veiculoId
          ? 'Entrada registrada no Renave.'
          : 'Entrada registrada. O carro foi cadastrado em Veículos como Reservado: complete o cadastro.');
        this.carregarZeroKm();
      },
      error: err => { this.enviando = false; this.toast.error(erroRenave(err)); }
    });
  }

  rejeitar(p: RenaveZeroKmPendente): void {
    this.enviando = true;
    this.renave.rejeitarZeroKm(p.chassi, this.motivoRejeicao).subscribe({
      next: () => { this.enviando = false; this.aberto = null; this.toast.success('Zero km rejeitado no Renave.'); this.carregarZeroKm(); },
      error: err => { this.enviando = false; this.toast.error(erroRenave(err)); }
    });
  }

  // ---------------- Entregas ----------------

  carregarEntregas(): void {
    this.carregandoEntregas = true;
    this.confirmando = null;
    this.renave.entregas(this.filtroEntregas).subscribe({
      next: l => { this.entregas = l; this.carregandoEntregas = false; },
      error: err => { this.carregandoEntregas = false; this.entregas = []; this.toast.error(erroRenave(err)); }
    });
  }

  /** Primeiro clique pede confirmacao na propria linha; o segundo executa. */
  acaoEntrega(e: RenaveEntrega, acao: 'realizar' | 'cancelar'): void {
    if (this.confirmando?.id !== e.id || this.confirmando.acao !== acao) {
      this.confirmando = { id: e.id, acao };
      return;
    }
    this.enviando = true;
    const obs = acao === 'realizar' ? this.renave.realizarEntrega(e.id) : this.renave.cancelarEntrega(e.id);
    obs.subscribe({
      next: () => {
        this.enviando = false;
        this.toast.success(acao === 'realizar' ? 'Entrega registrada no Renave.' : 'Entrega cancelada no Renave.');
        this.carregarEntregas();
      },
      error: err => { this.enviando = false; this.confirmando = null; this.toast.error(erroRenave(err)); }
    });
  }

  // ---------------- Consulta ----------------

  consultar(): void {
    this.consultando = true;
    this.consulta = null;
    this.renave.consultar(this.busca).subscribe({
      next: c => { this.consulta = c; this.consultando = false; },
      error: err => { this.consultando = false; this.toast.error(erroRenave(err)); }
    });
  }

  // ---------------- Testes ----------------

  criarZeroKmTeste(): void {
    this.criandoZeroKm = true;
    this.renave.criarZeroKmTeste().subscribe({
      next: z => { this.criandoZeroKm = false; this.zeroKmTeste = z.chassi; },
      error: err => { this.criandoZeroKm = false; this.toast.error(erroRenave(err)); }
    });
  }

  copiar(texto: string): void {
    navigator.clipboard?.writeText(texto).then(() => this.toast.success('Copiado!'));
  }
}
