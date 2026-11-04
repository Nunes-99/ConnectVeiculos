import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService, VeiculoService } from '../../core/services';
import { Veiculo } from '../../core/models';
import { NfeConfig, NfeFiltro, NfeResumo, NfeService, baixarArquivo, erroApi } from '../../core/services/nfe.service';
import { NfeConfigComponent } from '../../shared/components/renave/nfe-config.component';
import { NfeVeiculoComponent } from '../../shared/components/renave/nfe-veiculo.component';

/** O backend devolve no maximo isso; acima disso a pessoa estreita o filtro. */
const LIMITE_LISTA = 500;

/**
 * Notas fiscais da loja: todas as NF-e de compra e de venda (com DANFE e XML)
 * e a configuracao do emitente. A emissao continua no veiculo; "Abrir" mostra
 * a mesma janela do veiculo, com reenvio, cancelamento e carta de correcao.
 */
@Component({
  selector: 'app-notas-fiscais',
  standalone: true,
  imports: [CommonModule, FormsModule, NfeConfigComponent, NfeVeiculoComponent],
  templateUrl: './notas-fiscais.component.html',
  styleUrl: './notas-fiscais.component.scss'
})
export class NotasFiscaisComponent implements OnInit {
  private nfe = inject(NfeService);
  private veiculoService = inject(VeiculoService);
  private toast = inject(ToastService);
  private route = inject(ActivatedRoute);

  readonly limite = LIMITE_LISTA;

  aba: 'notas' | 'config' = 'notas';
  carregando = true;
  notas: NfeResumo[] = [];
  config: NfeConfig | null = null;
  filtro: NfeFiltro = { de: '', ate: '', tipo: '', status: '', busca: '' };

  veiculoAberto: Veiculo | null = null;
  abrindo: number | null = null;

  ngOnInit(): void {
    // ?aba=config: vindo do Renave, que manda enviar o certificado aqui.
    if (this.route.snapshot.queryParamMap.get('aba') === 'config') this.aba = 'config';
    this.carregarConfig();
    this.buscar();
  }

  carregarConfig(): void {
    this.nfe.obterConfig().subscribe({ next: c => this.config = c, error: () => this.config = null });
  }

  buscar(): void {
    this.carregando = true;
    this.nfe.listar(this.filtro).subscribe({
      next: n => { this.notas = n; this.carregando = false; },
      error: async err => {
        this.carregando = false;
        this.toast.error(await erroApi(err, 'Não foi possível carregar as notas.'));
      }
    });
  }

  limpar(): void {
    this.filtro = { de: '', ate: '', tipo: '', status: '', busca: '' };
    this.buscar();
  }

  get filtrando(): boolean {
    const f = this.filtro;
    return !!(f.de || f.ate || f.tipo || f.status || f.busca);
  }

  /** Soma so as autorizadas: cancelada e rejeitada nao valem como nota. */
  total(tipo: 'ENTRADA' | 'SAIDA'): { qtd: number; valor: number } {
    const validas = this.notas.filter(n => n.tipo === tipo && n.status === 'AUTORIZADA');
    return { qtd: validas.length, valor: validas.reduce((s, n) => s + n.valor, 0) };
  }

  abrir(n: NfeResumo): void {
    this.abrindo = n.id;
    this.veiculoService.getById(n.veiculoId).subscribe({
      next: v => { this.abrindo = null; this.veiculoAberto = v; },
      error: () => {
        this.abrindo = null;
        this.toast.error('O veículo desta nota não existe mais. DANFE e XML continuam disponíveis aqui.');
      }
    });
  }

  fecharVeiculo(): void {
    this.veiculoAberto = null;
    this.buscar();
  }

  configSalva(pronta: boolean): void {
    if (this.config) this.config.pronto = pronta;
  }

  baixarDanfe(n: NfeResumo): void {
    this.nfe.danfe(n.id).subscribe({
      next: b => baixarArquivo(b, `DANFE-${n.numero}.pdf`),
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível gerar o DANFE.'))
    });
  }

  baixarXml(n: NfeResumo): void {
    this.nfe.xml(n.id).subscribe({
      next: b => baixarArquivo(b, `${n.chave}-procNFe.xml`),
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível baixar o XML.'))
    });
  }

  rotuloStatus(s: string): string {
    return ({ PENDENTE: 'Pendente', AUTORIZADA: 'Autorizada', REJEITADA: 'Rejeitada', CANCELADA: 'Cancelada', INUTILIZADA: 'Inutilizada' } as Record<string, string>)[s] || s;
  }

  moeda(v: number): string {
    return v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }
}
