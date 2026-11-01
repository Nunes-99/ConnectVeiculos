import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../core/services/toast.service';
import { RenaveConexao, RenaveConfig, RenaveService, RenaveVeiculoTeste, erroRenave } from '../../../core/services/renave.service';

/**
 * Corpo do cartao "Renave" em Integracoes: ambiente, operador responsavel,
 * e-mail da loja, certificado A1 e teste de conexao. O cabecalho do cartao
 * fica na pagina (estilos dela); este componente avisa se esta pronto.
 */
@Component({
  selector: 'app-renave-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './renave-config.component.html',
  styleUrl: './renave.scss'
})
export class RenaveConfigComponent implements OnInit {
  private renave = inject(RenaveService);
  private toast = inject(ToastService);

  @Output() prontoChange = new EventEmitter<boolean>();

  config: RenaveConfig | null = null;
  form = { ambiente: 'homologacao', operadorCpf: '', operadorNome: '', emailLoja: '', cnpjEstabelecimento: '' };
  mostrarAvancado = false;
  salvando = false;

  arquivo: File | null = null;
  senhaCertificado = '';
  enviandoCertificado = false;

  testando = false;
  conexao: RenaveConexao | null = null;

  criandoTeste = false;
  veiculoTeste: RenaveVeiculoTeste | null = null;

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.renave.obterConfig().subscribe({
      next: c => this.aplicar(c),
      error: () => this.toast.error('Não foi possível carregar a configuração do Renave.')
    });
  }

  private aplicar(c: RenaveConfig): void {
    this.config = c;
    this.form = {
      ambiente: c.ambiente,
      operadorCpf: c.operadorCpf ?? '',
      operadorNome: c.operadorNome ?? '',
      emailLoja: c.emailLoja ?? '',
      cnpjEstabelecimento: c.cnpjEstabelecimento ?? ''
    };
    this.mostrarAvancado = this.mostrarAvancado || !!c.cnpjEstabelecimento;
    this.prontoChange.emit(c.pronto);
  }

  get emProducao(): boolean {
    return this.form.ambiente === 'producao';
  }

  get certificadoVencido(): boolean {
    return !!this.config?.certificadoValidade && new Date(this.config.certificadoValidade) <= new Date();
  }

  salvar(): void {
    this.salvando = true;
    this.renave.salvarConfig({
      ambiente: this.form.ambiente as RenaveConfig['ambiente'],
      operadorCpf: this.form.operadorCpf,
      operadorNome: this.form.operadorNome,
      emailLoja: this.form.emailLoja,
      cnpjEstabelecimento: this.form.cnpjEstabelecimento
    }).subscribe({
      next: c => { this.aplicar(c); this.salvando = false; this.conexao = null; this.toast.success('Configuração do Renave salva.'); },
      error: err => { this.salvando = false; this.toast.error(erroRenave(err)); }
    });
  }

  escolherArquivo(event: Event): void {
    this.arquivo = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  enviarCertificado(): void {
    if (!this.arquivo) return;
    this.enviandoCertificado = true;
    this.renave.enviarCertificado(this.arquivo, this.senhaCertificado).subscribe({
      next: c => {
        this.aplicar(c);
        this.enviandoCertificado = false;
        this.arquivo = null;
        this.senhaCertificado = '';
        this.toast.success('Certificado guardado.');
      },
      error: err => { this.enviandoCertificado = false; this.toast.error(erroRenave(err)); }
    });
  }

  removerCertificado(): void {
    this.renave.removerCertificado().subscribe({
      next: c => { this.aplicar(c); this.toast.success('Certificado removido.'); },
      error: err => this.toast.error(erroRenave(err))
    });
  }

  testar(): void {
    this.testando = true;
    this.conexao = null;
    this.renave.testarConexao().subscribe({
      next: c => { this.conexao = c; this.testando = false; },
      error: err => { this.testando = false; this.toast.error(erroRenave(err)); }
    });
  }

  criarVeiculoTeste(): void {
    this.criandoTeste = true;
    this.renave.criarVeiculoTeste().subscribe({
      next: v => { this.veiculoTeste = v; this.criandoTeste = false; },
      error: err => { this.criandoTeste = false; this.toast.error(erroRenave(err)); }
    });
  }

  copiar(texto: string): void {
    navigator.clipboard?.writeText(texto).then(() => this.toast.success('Copiado!'));
  }
}
