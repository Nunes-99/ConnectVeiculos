import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../core/services/toast.service';
import { CertificadoInfo, NfeService, erroApi } from '../../../core/services/nfe.service';

/** Bloco do certificado A1 da loja (o mesmo para Renave e NF-e). */
@Component({
  selector: 'app-certificado-a1',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="rnv-bloco">
      <h4><span class="material-icons">verified_user</span> Certificado digital A1 (e-CNPJ)</h4>
      @if (info?.cadastrado) {
        <div class="rnv-cert" [class.rnv-erro]="!info!.valido">
          <span class="material-icons">{{ info!.valido ? 'check_circle' : 'error' }}</span>
          <div>
            <strong>{{ info!.titular || 'Certificado guardado' }}</strong>
            <small>
              {{ info!.valido ? 'Válido até' : 'Venceu em' }} {{ info!.validade | date:'dd/MM/yyyy' }}
              @if (info!.cnpj) { · CNPJ {{ info!.cnpj }} } · guardado criptografado
            </small>
          </div>
          <button class="btn-mini-rnv" (click)="remover()" title="Remover certificado"><span class="material-icons">delete</span></button>
        </div>
      } @else if (dica) {
        <small class="rnv-dica">{{ dica }}</small>
      }
      <div class="rnv-cert-envio">
        <input type="file" accept=".pfx,.p12" (change)="escolher($event)" />
        <input type="password" [(ngModel)]="senha" placeholder="Senha do certificado" autocomplete="new-password" />
        <button class="btn-secondary" (click)="enviar()" [disabled]="!arquivo || enviando">
          {{ enviando ? 'Enviando...' : (info?.cadastrado ? 'Trocar' : 'Guardar') }}
        </button>
      </div>
      <small class="rnv-dica">É o mesmo certificado para o Renave e para a NF-e.</small>
    </div>
  `,
  styleUrl: './renave.scss'
})
export class CertificadoA1Component implements OnInit {
  private nfe = inject(NfeService);
  private toast = inject(ToastService);

  @Input() dica = '';
  @Output() alterado = new EventEmitter<CertificadoInfo>();

  info: CertificadoInfo | null = null;
  arquivo: File | null = null;
  senha = '';
  enviando = false;

  ngOnInit(): void {
    this.nfe.certificado().subscribe({ next: i => this.info = i, error: () => {} });
  }

  escolher(event: Event): void {
    this.arquivo = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  enviar(): void {
    if (!this.arquivo) return;
    this.enviando = true;
    this.nfe.enviarCertificado(this.arquivo, this.senha).subscribe({
      next: i => {
        this.info = i; this.enviando = false; this.arquivo = null; this.senha = '';
        this.alterado.emit(i);
        this.toast.success('Certificado guardado.');
      },
      error: async err => { this.enviando = false; this.toast.error(await erroApi(err, 'Não foi possível guardar o certificado.')); }
    });
  }

  remover(): void {
    this.nfe.removerCertificado().subscribe({
      next: i => { this.info = i; this.alterado.emit(i); this.toast.success('Certificado removido.'); },
      error: async err => this.toast.error(await erroApi(err, 'Não foi possível remover.'))
    });
  }
}
