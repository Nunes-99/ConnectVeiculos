import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../core/services/toast.service';
import { CredereService, CredereStatus, erroCredere } from '../../../core/services/credere.service';

/**
 * Corpo do cartao "Credere" em Integracoes: conectar a conta da loja (OAuth),
 * escolher a loja e ver em quais bancos ela esta habilitada.
 */
@Component({
  selector: 'app-credere-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <p class="cr-texto">
      Simula o financiamento do cliente nos bancos em que a loja já é credenciada, com parcela, taxa e pré-aprovação de cada um.
      A loja precisa <strong>assinar a Credere</strong>; aqui ela só conecta a conta.
    </p>

    @if (!status) {
      <p class="cr-texto">Carregando...</p>
    } @else if (!status.configurado) {
      <div class="cr-aviso">
        <span class="material-icons">hourglass_top</span>
        <div>Integração em liberação com a Credere. Assim que as chaves do ConnectVeiculos forem cadastradas, o botão de conectar aparece aqui.</div>
      </div>
    } @else if (!status.conectado) {
      <button class="btn-primary" (click)="conectar()" [disabled]="ocupado">
        <span class="material-icons">link</span> Conectar conta da Credere
      </button>
    } @else {
      <div class="cr-ok">
        <span class="material-icons">check_circle</span>
        <div>
          <strong>{{ status.conta || 'Conta conectada' }}</strong>
          <small>Conectada por {{ status.usuario || '—' }} em {{ status.conectadoEm | date:'dd/MM/yyyy' }}</small>
        </div>
      </div>
      @if (status.erro) {
        <div class="cr-erro"><span class="material-icons">error</span> {{ status.erro }}</div>
      }

      <div class="form-group">
        <label>Loja na Credere</label>
        <select [ngModel]="status.lojaId" (ngModelChange)="escolherLoja($event)" [disabled]="ocupado">
          <option [ngValue]="null" disabled>Escolha a loja...</option>
          @for (l of status.lojas; track l.id) {
            <option [ngValue]="l.id">{{ l.nome }}{{ l.cnpj ? ' · ' + l.cnpj : '' }}</option>
          }
        </select>
      </div>

      @if (status.lojaId) {
        <h4>Bancos da loja na Credere</h4>
        @for (b of status.bancos; track b.codigo + b.status) {
          <span class="cr-banco" [attr.data-status]="b.ativo ? b.status : 'inativo'">
            {{ b.nome }} · {{ rotulo(b.ativo ? b.status : 'inativo') }}
          </span>
        } @empty {
          <p class="cr-texto">Nenhum banco cadastrado na Credere para esta loja. Cadastre os acessos dos bancos lá.</p>
        }
      }

      <div class="cr-acoes">
        <button class="btn-secondary" (click)="carregar()" [disabled]="ocupado"><span class="material-icons">refresh</span> Atualizar</button>
        <button class="btn-secondary" (click)="desconectar()" [disabled]="ocupado"><span class="material-icons">link_off</span> Desconectar</button>
      </div>
    }
  `,
  styles: [`
    .cr-texto { color: var(--text-secondary); font-size: 0.85rem; margin: 0 0 0.75rem; }
    .cr-ok, .cr-aviso, .cr-erro {
      display: flex; gap: 0.5rem; align-items: flex-start; padding: 0.6rem 0.75rem; border-radius: 8px; margin-bottom: 0.75rem; font-size: 0.85rem;
      div { display: flex; flex-direction: column; gap: 0.15rem; }
      small { opacity: 0.85; }
    }
    .cr-ok { background: var(--status-success-bg, #e8f5e9); color: var(--status-success, #2e7d32); }
    .cr-aviso { background: var(--status-warning-bg, #fff8e1); color: var(--status-warning, #8d6e00); }
    .cr-erro { background: var(--status-error-bg, #ffebee); color: var(--status-error, #c62828); }
    h4 { margin: 0.75rem 0 0.5rem; font-size: 0.9rem; color: var(--text-primary); }
    .cr-banco {
      display: inline-block; margin: 0 0.35rem 0.35rem 0; padding: 0.15rem 0.55rem; border-radius: 999px; font-size: 0.75rem; font-weight: 600;
      background: var(--status-warning-bg, #fff8e1); color: var(--status-warning, #8d6e00);
      &[data-status='okay'] { background: var(--status-success-bg, #e8f5e9); color: var(--status-success, #2e7d32); }
      &[data-status='unauthorized'], &[data-status='inativo'] { background: var(--status-error-bg, #ffebee); color: var(--status-error, #c62828); }
    }
    .cr-acoes { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-top: 0.75rem; }
  `]
})
export class CredereConfigComponent implements OnInit {
  private credere = inject(CredereService);
  private toast = inject(ToastService);

  @Output() conectadoChange = new EventEmitter<boolean>();

  status: CredereStatus | null = null;
  ocupado = false;

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.credere.status().subscribe({
      next: s => { this.status = s; this.conectadoChange.emit(s.conectado && !!s.lojaId); },
      error: err => this.toast.error(erroCredere(err, 'Não foi possível consultar a Credere.'))
    });
  }

  conectar(): void {
    this.ocupado = true;
    this.credere.urlAutorizacao().subscribe({
      // Mesma aba: a Credere devolve para /integracoes/credere com o codigo.
      next: r => window.location.href = r.url,
      error: err => { this.ocupado = false; this.toast.error(erroCredere(err, 'Não foi possível iniciar a conexão.')); }
    });
  }

  escolherLoja(lojaId: number): void {
    this.ocupado = true;
    this.credere.escolherLoja(lojaId).subscribe({
      next: s => { this.ocupado = false; this.status = s; this.conectadoChange.emit(s.conectado && !!s.lojaId); this.toast.success('Loja escolhida.'); },
      error: err => { this.ocupado = false; this.toast.error(erroCredere(err, 'Não foi possível escolher a loja.')); }
    });
  }

  desconectar(): void {
    this.ocupado = true;
    this.credere.desconectar().subscribe({
      next: () => { this.ocupado = false; this.toast.success('Credere desconectada.'); this.carregar(); },
      error: err => { this.ocupado = false; this.toast.error(erroCredere(err, 'Não foi possível desconectar.')); }
    });
  }

  rotulo(s: string | null): string {
    return ({ okay: 'acesso válido', unauthorized: 'acesso inválido', not_analyzed: 'em verificação', inativo: 'desativado' } as Record<string, string>)[s || ''] || 'sem status';
  }
}
