import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../core/services/toast.service';
import { NfeConfig, NfeMunicipio, NfeService, erroApi } from '../../../core/services/nfe.service';
import { CertificadoA1Component } from './certificado-a1.component';

/**
 * Corpo do cartao "NF-e" em Integracoes: dados da loja (emitente), numeracao
 * e as regras fiscais da compra e da venda de veiculo. As regras vem com um
 * padrao, mas quem decide e' o contador da loja.
 */
@Component({
  selector: 'app-nfe-config',
  standalone: true,
  imports: [CommonModule, FormsModule, CertificadoA1Component],
  templateUrl: './nfe-config.component.html',
  styleUrl: './renave.scss'
})
export class NfeConfigComponent implements OnInit {
  private nfe = inject(NfeService);
  private toast = inject(ToastService);

  @Output() prontoChange = new EventEmitter<boolean>();

  config: NfeConfig | null = null;
  salvando = false;
  consultando = false;
  statusSefaz: string | null = null;
  mostrarRegras = false;

  buscaMunicipio = '';
  municipios: NfeMunicipio[] = [];

  readonly ufs = ['AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA', 'PB', 'PR',
    'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SP', 'SC', 'SE', 'TO'];

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.nfe.obterConfig().subscribe({
      next: c => { this.config = c; this.prontoChange.emit(c.pronto); },
      error: () => this.toast.error('Não foi possível carregar a configuração da NF-e.')
    });
  }

  get simples(): boolean {
    return this.config?.crt === 1 || this.config?.crt === 4;
  }

  buscarMunicipios(): void {
    const c = this.config;
    if (!c?.uf || this.buscaMunicipio.trim().length < 3) return;
    this.nfe.municipios(c.uf, this.buscaMunicipio.trim()).subscribe({
      next: m => { this.municipios = m; if (m.length === 1) this.escolherMunicipio(m[0]); },
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível buscar o município.'))
    });
  }

  escolherMunicipio(m: NfeMunicipio | undefined): void {
    if (!m || !this.config) return;
    this.config.codigoMunicipio = m.codigo;
    this.config.municipio = m.nome;
    this.municipios = [];
    this.buscaMunicipio = '';
  }

  salvar(): void {
    if (!this.config) return;
    this.salvando = true;
    this.nfe.salvarConfig(this.config).subscribe({
      next: c => { this.config = c; this.salvando = false; this.prontoChange.emit(c.pronto); this.toast.success('Configuração da NF-e salva.'); },
      error: async err => { this.salvando = false; this.toast.error(await erroApi(err, 'Não foi possível salvar.')); }
    });
  }

  consultarSefaz(): void {
    this.consultando = true;
    this.statusSefaz = null;
    this.nfe.statusSefaz().subscribe({
      next: s => { this.consultando = false; this.statusSefaz = `${s.cStat} - ${s.motivo}`; },
      error: async err => { this.consultando = false; this.toast.error(await erroApi(err, 'Não foi possível consultar a Sefaz.')); }
    });
  }
}
