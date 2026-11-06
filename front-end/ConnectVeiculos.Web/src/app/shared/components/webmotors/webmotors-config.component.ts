import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../../core/services/toast.service';
import { WebmotorsService, WebmotorsStatus, erroWebmotors } from '../../../core/services/webmotors.service';

/**
 * Corpo do cartao "Webmotors" em Integracoes: a loja informa o usuario de
 * integracao que a Webmotors forneceu e escolhe a modalidade de anuncio.
 */
@Component({
  selector: 'app-webmotors-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <p class="wm-texto">
      Anuncia os veículos no estoque da loja na Webmotors. Depois de anunciar um veículo, mudar preço, quilometragem ou fotos
      atualiza o anúncio sozinho; vender ou inativar tira do ar.
    </p>

    @if (!status) {
      <p class="wm-texto">Carregando...</p>
    } @else if (!status.conectado) {
      <div class="form-group">
        <label>CNPJ da loja</label>
        <input type="text" [(ngModel)]="cnpj" placeholder="00.000.000/0000-00" autocomplete="off" />
      </div>
      <div class="form-group">
        <label>E-mail do usuário de integração</label>
        <input type="email" [(ngModel)]="email" autocomplete="off" />
      </div>
      <div class="form-group">
        <label>Senha do usuário de integração</label>
        <input type="password" [(ngModel)]="senha" autocomplete="new-password" />
        <small class="wm-dica">Quem fornece esse usuário é a Webmotors (não é o login do site). A senha fica salva criptografada.</small>
      </div>
      <button class="btn-primary" (click)="conectar()" [disabled]="ocupado || !cnpj || !email || !senha">
        <span class="material-icons">link</span> {{ ocupado ? 'Conectando...' : 'Conectar' }}
      </button>
    } @else {
      @if (status.homologacao) {
        <div class="wm-aviso"><span class="material-icons">science</span>
          <div>Ambiente de <strong>testes</strong> da Webmotors: os anúncios não aparecem no site.</div>
        </div>
      }
      <div class="wm-ok">
        <span class="material-icons">check_circle</span>
        <div>
          <strong>CNPJ {{ status.cnpj }}</strong>
          <small>{{ status.email }} · conectado em {{ status.conectadoEm | date:'dd/MM/yyyy' }}</small>
        </div>
      </div>
      @if (status.erro) {
        <div class="wm-erro"><span class="material-icons">error</span> {{ status.erro }}</div>
      }

      @if (!status.erro && !status.modalidades.length) {
        <div class="wm-aviso">
          <span class="material-icons">info</span>
          <div>
            <strong>Nenhum plano ativo na Webmotors</strong>
            <small>A conta conectou, mas a Webmotors não liberou nenhuma modalidade de anúncio para esta loja. Fale com o seu comercial Webmotors para contratar um plano e depois clique em Atualizar.</small>
          </div>
        </div>
      } @else {
      <div class="form-group">
        <label>Modalidade de anúncio</label>
        <select [ngModel]="status.modalidadeId" (ngModelChange)="escolherModalidade($event)" [disabled]="ocupado">
          <option [ngValue]="null" disabled>Escolha...</option>
          @for (m of status.modalidades; track m.codigo) {
            <option [ngValue]="m.codigo">{{ m.nome }} · {{ m.emUso }} de {{ m.limite }} em uso</option>
          }
        </select>
      </div>
      }

      <div class="wm-acoes">
        <button class="btn-secondary" (click)="carregar()" [disabled]="ocupado"><span class="material-icons">refresh</span> Atualizar</button>
        <button class="btn-secondary" (click)="desconectar()" [disabled]="ocupado"><span class="material-icons">link_off</span> Desconectar</button>
      </div>
      <small class="wm-dica">Para anunciar, use o botão <span class="material-icons wm-ico">directions_car</span> na lista de veículos.</small>
    }
  `,
  styleUrl: './webmotors.scss'
})
export class WebmotorsConfigComponent implements OnInit {
  private webmotors = inject(WebmotorsService);
  private toast = inject(ToastService);

  @Output() conectadoChange = new EventEmitter<boolean>();

  status: WebmotorsStatus | null = null;
  ocupado = false;
  cnpj = '';
  email = '';
  senha = '';

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.webmotors.status().subscribe({
      next: s => this.aplicar(s),
      error: err => this.toast.error(erroWebmotors(err, 'Não foi possível consultar a Webmotors.'))
    });
  }

  conectar(): void {
    this.ocupado = true;
    this.webmotors.conectar(this.cnpj, this.email, this.senha).subscribe({
      next: s => { this.ocupado = false; this.senha = ''; this.aplicar(s); this.toast.success('Webmotors conectada.'); },
      error: err => { this.ocupado = false; this.toast.error(erroWebmotors(err, 'Não foi possível conectar.')); }
    });
  }

  escolherModalidade(id: number): void {
    this.ocupado = true;
    this.webmotors.escolherModalidade(id).subscribe({
      next: s => { this.ocupado = false; this.aplicar(s); this.toast.success('Modalidade escolhida.'); },
      error: err => { this.ocupado = false; this.toast.error(erroWebmotors(err, 'Não foi possível escolher a modalidade.')); }
    });
  }

  desconectar(): void {
    if (!confirm('Desconectar a Webmotors? Os anúncios que já estão lá continuam no ar, mas deixam de ser atualizados.')) return;
    this.ocupado = true;
    this.webmotors.desconectar().subscribe({
      next: () => { this.ocupado = false; this.toast.success('Webmotors desconectada.'); this.carregar(); },
      error: err => { this.ocupado = false; this.toast.error(erroWebmotors(err, 'Não foi possível desconectar.')); }
    });
  }

  private aplicar(s: WebmotorsStatus): void {
    this.status = s;
    this.conectadoChange.emit(s.conectado && !s.erro);
  }
}
