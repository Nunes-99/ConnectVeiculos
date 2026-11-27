import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastService } from '../../core/services';
import { CategoriaConsulta, ConsultaPlacaService, PrecoServico } from '../../core/services/consulta-placa.service';

const CHAVE_TOKEN = 'cv_admin_token';

/**
 * Precos das consultas por plano (so a plataforma). Entra com o
 * ADMIN_API_TOKEN do servidor, guardado so nesta aba do navegador.
 * Campo vazio = custo da ZapCar + acrescimo (R$) do plano.
 */
@Component({
  selector: 'app-precos-consultas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="pagina">
      <h1>Preços das consultas</h1>

      @if (!servicos) {
        <section class="caixa entrar">
          <p>Tela da plataforma. Informe o token de administrador (ADMIN_API_TOKEN do servidor).</p>
          <input type="password" [(ngModel)]="token" placeholder="Token de administrador" (keyup.enter)="entrar()" autocomplete="off" />
          <button class="btn-primary" [disabled]="!token || carregando" (click)="entrar()">{{ carregando ? 'Entrando...' : 'Entrar' }}</button>
        </section>
      } @else {
        <section class="caixa ajuda">
          <p>
            Cada célula é o preço que a loja daquele plano paga. <strong>Vazio = custo da ZapCar + acréscimo do plano</strong>
            (o valor em cinza). Digite um valor e saia do campo para fixar; apague para voltar ao acréscimo.
            A coluna <em>Todos</em> fixa o mesmo valor para os planos que não têm valor próprio.
          </p>
          @if (teste) { <p class="teste">A chave da ZapCar no servidor é a de teste: os custos são os reais, mas as consultas não cobram.</p> }
          <button class="btn-secondary" (click)="sair()">Sair</button>
        </section>

        @for (cat of categorias; track cat.id) {
          @if (doGrupo(cat.id).length) {
            <h2>{{ cat.nome }}</h2>
            <div class="tabela">
              <table>
                <thead>
                  <tr>
                    <th>Consulta</th>
                    <th class="num">Custo</th>
                    <th class="num">Todos</th>
                    @for (p of servicos[0].planos; track p.planoId) {
                      <th class="num">{{ p.plano }} <small>+{{ p.acrescimo | currency:'BRL':'symbol':'1.0-2':'pt-BR' }}</small></th>
                    }
                  </tr>
                </thead>
                <tbody>
                  @for (s of doGrupo(cat.id); track s.servico) {
                    <tr>
                      <td>{{ s.nome }}</td>
                      <td class="num custo">{{ s.variaPorUf ? 'por estado' : (s.custo | currency:'BRL':'symbol':'1.2-2':'pt-BR') }}</td>
                      @if (s.variaPorUf) {
                        <td class="num" [attr.colspan]="s.planos.length + 1"><small>custo do estado + acréscimo do plano</small></td>
                      } @else {
                        <td class="num">
                          <input type="number" min="0.01" step="0.01" [ngModel]="s.fixoTodos" [placeholder]="''"
                                 (change)="salvar(s, null, $any($event.target).value)" />
                        </td>
                        @for (p of s.planos; track p.planoId) {
                          <td class="num" [class.prejuizo]="p.preco !== null && s.custo !== null && p.preco < s.custo">
                            <input type="number" min="0.01" step="0.01" [ngModel]="p.fixo"
                                   [placeholder]="p.preco !== null ? p.preco.toFixed(2) : ''"
                                   (change)="salvar(s, p.planoId, $any($event.target).value)" />
                          </td>
                        }
                      }
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      }
    </div>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; background: var(--bg-secondary, #f5f5f5); }
    .pagina { max-width: 1200px; margin: 0 auto; padding: 24px 16px 48px; }
    h1 { margin: 0 0 16px; color: var(--text-primary); }
    h2 { margin: 24px 0 8px; font-size: 1.05rem; color: var(--text-primary); }
    .caixa { background: var(--bg-primary); border-radius: 12px; box-shadow: var(--shadow-sm); padding: 16px 20px; color: var(--text-secondary); font-size: 14px; }
    .caixa p { margin: 0 0 10px; }
    .entrar { display: flex; flex-direction: column; gap: 10px; max-width: 420px; }
    .entrar input { padding: 10px 12px; border: 1px solid var(--border-secondary); border-radius: 6px; background: var(--bg-primary); color: var(--text-primary); }
    .teste { color: var(--status-warning); }
    .btn-primary, .btn-secondary {
      align-self: flex-start; padding: 9px 18px; border-radius: 6px; font-size: 14px; font-weight: 600; cursor: pointer;
    }
    .btn-primary { background: var(--brand-primary); color: var(--text-inverse, #fff); border: 0; }
    .btn-primary:disabled { opacity: .6; cursor: default; }
    .btn-secondary { background: var(--bg-primary); color: var(--text-primary); border: 1px solid var(--border-secondary); }
    .tabela { overflow-x: auto; background: var(--bg-primary); border-radius: 12px; box-shadow: var(--shadow-sm); }
    table { width: 100%; border-collapse: collapse; font-size: 13px; }
    th, td { padding: 8px 10px; border-bottom: 1px solid var(--border-light); text-align: left; color: var(--text-primary); white-space: nowrap; }
    th { background: var(--bg-tertiary); font-weight: 600; }
    th small { color: var(--text-tertiary); font-weight: 400; }
    .num { text-align: right; }
    .custo { color: var(--text-secondary); }
    td input { width: 92px; padding: 6px 8px; border: 1px solid var(--border-secondary); border-radius: 6px; text-align: right; background: var(--bg-primary); color: var(--text-primary); }
    td input::placeholder { color: var(--text-tertiary); }
    td.prejuizo input { border-color: var(--status-error); }
  `]
})
export class PrecosConsultasComponent implements OnInit {
  private api = inject(ConsultaPlacaService);
  private toast = inject(ToastService);

  readonly categorias: { id: CategoriaConsulta; nome: string }[] = [
    { id: 'CONSULTAS', nome: 'Veículo' },
    { id: 'DOCUMENTOS', nome: 'Documentos' },
    { id: 'ANALISE_PESSOA', nome: 'Pessoa e empresa' }
  ];

  token = '';
  servicos: PrecoServico[] | null = null;
  teste = false;
  carregando = false;

  ngOnInit(): void {
    try { this.token = sessionStorage.getItem(CHAVE_TOKEN) || ''; } catch { this.token = ''; }
    if (this.token) this.entrar();
  }

  doGrupo(categoria: CategoriaConsulta): PrecoServico[] {
    return (this.servicos ?? []).filter(s => s.categoria === categoria);
  }

  entrar(): void {
    if (!this.token) return;
    this.carregando = true;
    this.api.precos(this.token).subscribe({
      next: r => {
        this.carregando = false;
        this.servicos = r.servicos;
        this.teste = r.teste;
        try { sessionStorage.setItem(CHAVE_TOKEN, this.token); } catch { /* sem storage */ }
      },
      error: err => {
        this.carregando = false;
        this.servicos = null;
        try { sessionStorage.removeItem(CHAVE_TOKEN); } catch { /* sem storage */ }
        this.toast.error(err?.error?.error || 'Não consegui entrar.');
      }
    });
  }

  sair(): void {
    try { sessionStorage.removeItem(CHAVE_TOKEN); } catch { /* sem storage */ }
    this.token = '';
    this.servicos = null;
  }

  salvar(s: PrecoServico, planoId: number | null, valor: string): void {
    const preco = valor === '' || valor === null ? null : Number(valor);
    if (preco !== null && (!isFinite(preco) || preco <= 0)) {
      this.toast.error('Digite um valor maior que zero, ou apague para usar o acréscimo do plano.');
      return;
    }
    this.api.definirPreco(this.token, s.servico, planoId, preco).subscribe({
      next: r => {
        this.servicos = r.servicos;
        const custo = s.custo ?? 0;
        if (preco !== null && preco < custo) this.toast.error(`Atenção: ${s.nome} ficou abaixo do custo (R$ ${custo.toFixed(2)}).`);
        else this.toast.success(preco === null ? `${s.nome}: voltou para o acréscimo do plano.` : `${s.nome}: preço salvo.`);
      },
      error: err => this.toast.error(err?.error?.error || 'Não consegui salvar.')
    });
  }
}
