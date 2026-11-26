import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../../core/services/toast.service';
import { OlxService, OlxStatus, erroOlx } from '../../../core/services/olx.service';

/**
 * Corpo do cartao "OLX" em Integracoes: conectar a conta da loja na OLX
 * (usuario da OLX com plano profissional) e ver o saldo de anuncios. Os
 * anuncios ficam na janela de cada veiculo (Veiculos -> menu -> OLX).
 */
@Component({
  selector: 'app-olx-config',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <p class="np-texto">
      Anuncia os carros da loja na <strong>OLX</strong>. A loja entra com o usuário dela na OLX, que precisa ter um
      <strong>plano profissional</strong> (Essencial, Plus ou Premium Empresa): o plano de vendedor autônomo não permite integração.
    </p>

    @if (!status) {
      <p class="np-texto">Carregando...</p>
    } @else if (!status.configurado) {
      <div class="np-aviso">
        <span class="material-icons">hourglass_top</span>
        <div>Integração em liberação com a OLX. Assim que a OLX cadastrar o ConnectVeiculos como integrador, o botão de conectar aparece aqui.</div>
      </div>
    } @else if (!status.conectado && outroDominio) {
      <!-- A OLX devolve sempre para o mesmo endereco (redirect_uri cadastrada). -->
      <div class="np-aviso">
        <span class="material-icons">info</span>
        <div>Para conectar a OLX, entre pelo endereço <strong>{{ status.siteConexao }}</strong> (o mesmo usuário e senha) e clique em Conectar lá. Depois de conectada, ela funciona em qualquer endereço da loja.</div>
      </div>
      <a class="btn-primary" [href]="status.siteConexao + '/login'">
        <span class="material-icons">open_in_new</span> Abrir {{ status.siteConexao }}
      </a>
    } @else if (!status.conectado) {
      @if (status.erro) {
        <div class="np-erro"><span class="material-icons">error</span> {{ status.erro }}</div>
      }
      <button class="btn-primary" (click)="conectar()" [disabled]="ocupado">
        <span class="material-icons">link</span> Conectar conta da OLX
      </button>
    } @else {
      <div class="np-ok">
        <span class="material-icons">check_circle</span>
        <div>
          <strong>{{ status.conta || 'Conta conectada' }}</strong>
          <small>
            Conectada em {{ status.conectadoEm | date:'dd/MM/yyyy' }}
            @if (status.anunciosTotal != null) {
              · {{ status.anunciosUsados }} de {{ status.anunciosTotal }} anúncios usados ({{ status.anunciosDisponiveis }} livres)
            }
            @if (status.proximaRenovacao) { · renova em {{ status.proximaRenovacao | date:'dd/MM/yyyy' }} }
          </small>
        </div>
      </div>
      @if (status.erro) {
        <div class="np-erro"><span class="material-icons">error</span> {{ status.erro }}</div>
      }
      <p class="np-texto np-anuncios">
        Para anunciar: <a routerLink="/veiculos">Veículos</a> → menu <span class="material-icons np-ico">more_vert</span> do carro → <strong>Anunciar na OLX</strong>.
        Telefone e CEP do anúncio vêm do cadastro da loja.
      </p>

      <div class="np-acoes">
        <button class="btn-secondary" (click)="carregar()" [disabled]="ocupado"><span class="material-icons">refresh</span> Atualizar</button>
        <button class="btn-secondary" (click)="desconectar()" [disabled]="ocupado"><span class="material-icons">link_off</span> Desconectar</button>
      </div>
    }
  `,
  styleUrl: '../napista/napista.scss'
})
export class OlxConfigComponent implements OnInit {
  private olx = inject(OlxService);
  private toast = inject(ToastService);

  @Output() conectadoChange = new EventEmitter<boolean>();

  status: OlxStatus | null = null;
  ocupado = false;

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.olx.status().subscribe({
      next: s => { this.status = s; this.conectadoChange.emit(s.conectado); },
      error: err => this.toast.error(erroOlx(err, 'Não foi possível consultar a OLX.'))
    });
  }

  get outroDominio(): boolean {
    return !!this.status?.siteConexao && typeof window !== 'undefined' && window.location.origin !== this.status.siteConexao;
  }

  conectar(): void {
    this.ocupado = true;
    this.olx.urlAutorizacao().subscribe({
      // Mesma aba: a OLX devolve para /integracoes/olx com o codigo.
      next: r => window.location.href = r.url,
      error: err => { this.ocupado = false; this.toast.error(erroOlx(err, 'Não foi possível iniciar a conexão.')); }
    });
  }

  desconectar(): void {
    this.ocupado = true;
    this.olx.desconectar().subscribe({
      next: () => { this.ocupado = false; this.toast.success('OLX desconectada.'); this.carregar(); },
      error: err => { this.ocupado = false; this.toast.error(erroOlx(err, 'Não foi possível desconectar.')); }
    });
  }
}
