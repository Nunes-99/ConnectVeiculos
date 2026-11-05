import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ToastService } from '../../../core/services/toast.service';
import { NapistaService, NapistaStatus, erroNapista } from '../../../core/services/napista.service';

/**
 * Corpo do cartao "NaPista" em Integracoes: conectar a conta da loja no
 * NaPista (mesmo usuario e senha de la), escolher a loja e trazer os leads.
 * Os anuncios ficam na janela de cada veiculo (Veiculos -> menu -> NaPista).
 */
@Component({
  selector: 'app-napista-config',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <p class="np-texto">
      Anuncia os veículos da loja no <strong>NaPista</strong> e traz os leads de lá para a lista de Leads.
      A loja entra com o <strong>mesmo usuário e senha do NaPista</strong>; não há custo para a integração.
    </p>

    @if (!status) {
      <p class="np-texto">Carregando...</p>
    } @else if (!status.configurado) {
      <div class="np-aviso">
        <span class="material-icons">hourglass_top</span>
        <div>Integração em liberação com o NaPista. Assim que o NaPista cadastrar o ConnectVeiculos, o botão de conectar aparece aqui.</div>
      </div>
    } @else if (!status.conectado && outroDominio) {
      <!-- O NaPista devolve sempre para o mesmo endereco (redirect_uri cadastrada). -->
      <div class="np-aviso">
        <span class="material-icons">info</span>
        <div>Para conectar o NaPista, entre pelo endereço <strong>{{ status.siteConexao }}</strong> (o mesmo usuário e senha) e clique em Conectar lá. Depois de conectado, ele funciona em qualquer endereço da loja.</div>
      </div>
      <a class="btn-primary" [href]="status.siteConexao + '/login'">
        <span class="material-icons">open_in_new</span> Abrir {{ status.siteConexao }}
      </a>
    } @else if (!status.conectado) {
      @if (status.erro) {
        <div class="np-erro"><span class="material-icons">error</span> {{ status.erro }}</div>
      }
      <button class="btn-primary" (click)="conectar()" [disabled]="ocupado">
        <span class="material-icons">link</span> Conectar conta do NaPista
      </button>
      @if (status.desenvolvimento) {
        <small class="np-dica">Ambiente de desenvolvimento do NaPista: os anúncios não aparecem no site de verdade.</small>
      }
    } @else {
      <div class="np-ok">
        <span class="material-icons">check_circle</span>
        <div>
          <strong>{{ status.lojaNome || 'Conta conectada' }}</strong>
          <small>
            Conectado em {{ status.conectadoEm | date:'dd/MM/yyyy' }}
            @if (status.validaAte) { · vale até {{ status.validaAte | date:'dd/MM/yyyy HH:mm' }} (depois, conecte de novo) }
            @if (status.desenvolvimento) { · ambiente de desenvolvimento }
          </small>
        </div>
      </div>
      @if (status.erro) {
        <div class="np-erro"><span class="material-icons">error</span> {{ status.erro }}</div>
      }

      <div class="form-group">
        <label>Loja no NaPista</label>
        <select [ngModel]="status.sellerId" (ngModelChange)="escolherLoja($event)" [disabled]="ocupado">
          <option [ngValue]="null" disabled>Escolha a loja...</option>
          @for (l of status.lojas; track l.sellerId) {
            <option [ngValue]="l.sellerId">{{ l.nome }}{{ l.cnpj ? ' · ' + l.cnpj : '' }}</option>
          }
        </select>
      </div>

      @if (status.sellerId) {
        <h4>Leads do NaPista</h4>
        <p class="np-texto">
          Traz os contatos dos últimos 30 dias para <a routerLink="/leads">Leads</a> (origem "NaPista"), com o veículo
          ligado pela placa. Rodar de novo não duplica.
        </p>
        <button class="btn-secondary" (click)="importarLeads()" [disabled]="ocupado">
          <span class="material-icons">download</span> {{ ocupado ? 'Buscando...' : 'Buscar leads do NaPista' }}
        </button>
        <p class="np-texto np-anuncios">
          Para anunciar: <a routerLink="/veiculos">Veículos</a> → menu <span class="material-icons np-ico">more_vert</span> do carro → <strong>Anunciar no NaPista</strong>.
        </p>
      }

      <div class="np-acoes">
        <button class="btn-secondary" (click)="carregar()" [disabled]="ocupado"><span class="material-icons">refresh</span> Atualizar</button>
        <button class="btn-secondary" (click)="desconectar()" [disabled]="ocupado"><span class="material-icons">link_off</span> Desconectar</button>
      </div>
    }
  `,
  styleUrl: './napista.scss'
})
export class NapistaConfigComponent implements OnInit {
  private napista = inject(NapistaService);
  private toast = inject(ToastService);

  @Output() conectadoChange = new EventEmitter<boolean>();

  status: NapistaStatus | null = null;
  ocupado = false;

  ngOnInit(): void {
    this.carregar();
  }

  carregar(): void {
    this.napista.status().subscribe({
      next: s => this.aplicar(s),
      error: err => this.toast.error(erroNapista(err, 'Não foi possível consultar o NaPista.'))
    });
  }

  private aplicar(s: NapistaStatus): void {
    this.status = s;
    this.conectadoChange.emit(s.conectado && !!s.sellerId);
  }

  get outroDominio(): boolean {
    return !!this.status?.siteConexao && typeof window !== 'undefined' && window.location.origin !== this.status.siteConexao;
  }

  conectar(): void {
    this.ocupado = true;
    this.napista.urlAutorizacao().subscribe({
      // Mesma aba: o NaPista devolve para /integracoes/napista com o codigo.
      next: r => window.location.href = r.url,
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'Não foi possível iniciar a conexão.')); }
    });
  }

  escolherLoja(sellerId: string): void {
    this.ocupado = true;
    this.napista.escolherLoja(sellerId).subscribe({
      next: s => { this.ocupado = false; this.aplicar(s); this.toast.success('Loja escolhida.'); },
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'Não foi possível escolher a loja.')); }
    });
  }

  importarLeads(): void {
    this.ocupado = true;
    this.napista.importarLeads(30).subscribe({
      next: r => {
        this.ocupado = false;
        this.toast.success(r.importados
          ? `${r.importados} lead(s) novo(s) do NaPista em Leads.`
          : r.encontrados ? 'Nenhum lead novo: todos já estavam em Leads.' : 'Nenhum lead no NaPista nos últimos 30 dias.');
      },
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'Não foi possível buscar os leads.')); }
    });
  }

  desconectar(): void {
    this.ocupado = true;
    this.napista.desconectar().subscribe({
      next: () => { this.ocupado = false; this.toast.success('NaPista desconectado.'); this.carregar(); },
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'Não foi possível desconectar.')); }
    });
  }
}
