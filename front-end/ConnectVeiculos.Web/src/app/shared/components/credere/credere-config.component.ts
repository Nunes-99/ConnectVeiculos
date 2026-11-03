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
  styleUrl: './credere-config.component.scss'
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
