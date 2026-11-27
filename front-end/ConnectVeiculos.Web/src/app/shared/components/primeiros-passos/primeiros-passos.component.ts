import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { AuthService } from '../../../core/services/auth.service';

interface Passo {
  id: string;
  titulo: string;
  descricao: string;
  link: string;
  icone: string;
  feito: boolean;
  soAdministrador: boolean;
}

interface PrimeirosPassos {
  oculto: boolean;
  feitos: number;
  total: number;
  concluido: boolean;
  passos: Passo[];
}

/**
 * "Primeiros passos" no topo do Painel para a loja nova (gestao). Cada passo
 * se marca sozinho pelo que ja esta cadastrado; da para esconder e mostrar
 * de novo. Quando tudo esta feito, some sozinho.
 */
@Component({
  selector: 'app-primeiros-passos',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    @if (dados && !concluido) {
      @if (!dados.oculto) {
        <section class="pp" aria-labelledby="pp-titulo">
          <div class="pp-topo">
            <div>
              <h2 id="pp-titulo"><span class="material-icons">rocket_launch</span> Primeiros passos</h2>
              <p>Deixe a loja pronta para vender: {{ feitos }} de {{ total }} feitos.</p>
            </div>
            <button type="button" class="pp-ocultar" (click)="ocultar(true)" title="Esconder (dá para mostrar de novo no topo do Painel)">
              <span class="material-icons">visibility_off</span> Esconder
            </button>
          </div>
          <div class="pp-barra" role="progressbar" [attr.aria-valuenow]="feitos" aria-valuemin="0" [attr.aria-valuemax]="total">
            <span [style.width.%]="total ? feitos / total * 100 : 0"></span>
          </div>
          <ol class="pp-lista">
            @for (p of passosVisiveis; track p.id) {
              <li [class.feito]="p.feito">
                <span class="pp-check material-icons">{{ p.feito ? 'check_circle' : p.icone }}</span>
                <div class="pp-texto">
                  <strong>{{ p.titulo }}</strong>
                  <small>{{ p.descricao }}</small>
                </div>
                @if (!p.feito) {
                  <a class="pp-ir" [routerLink]="p.link">Fazer <span class="material-icons">arrow_forward</span></a>
                }
              </li>
            }
          </ol>
        </section>
      } @else {
        <button type="button" class="pp-mostrar" (click)="ocultar(false)">
          <span class="material-icons">rocket_launch</span> Mostrar primeiros passos ({{ feitos }} de {{ total }})
        </button>
      }
    }
  `,
  styles: [`
    .pp {
      background: var(--bg-primary);
      border-radius: 12px;
      box-shadow: var(--shadow-sm);
      padding: 20px 24px;
      margin-bottom: 24px;
      border-left: 4px solid var(--brand-primary);
    }
    .pp-topo { display: flex; justify-content: space-between; align-items: flex-start; gap: 12px; }
    h2 { display: flex; align-items: center; gap: 8px; margin: 0; font-size: 1.15rem; color: var(--text-primary); }
    h2 .material-icons { color: var(--brand-primary); }
    p { margin: 4px 0 0; color: var(--text-secondary); font-size: 14px; }
    .pp-ocultar, .pp-mostrar {
      display: inline-flex; align-items: center; gap: 6px;
      background: none; border: 1px solid var(--border-secondary); border-radius: 6px;
      padding: 6px 12px; color: var(--text-secondary); font-size: 13px; cursor: pointer; white-space: nowrap;
      .material-icons { font-size: 18px; }
    }
    .pp-mostrar { margin-bottom: 16px; }
    .pp-barra { height: 8px; border-radius: 4px; background: var(--bg-tertiary); overflow: hidden; margin: 14px 0 8px; }
    .pp-barra span { display: block; height: 100%; background: var(--status-success); transition: width .3s; }
    .pp-lista { list-style: none; margin: 0; padding: 0; }
    .pp-lista li {
      display: flex; align-items: center; gap: 14px;
      padding: 12px 0; border-bottom: 1px solid var(--border-light);
      &:last-child { border-bottom: 0; }
    }
    .pp-check { color: var(--brand-primary); font-size: 26px; flex-shrink: 0; }
    .pp-texto { flex: 1; display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .pp-texto strong { color: var(--text-primary); font-size: 14px; }
    .pp-texto small { color: var(--text-secondary); font-size: 12px; }
    li.feito .pp-check { color: var(--status-success); }
    li.feito .pp-texto strong { color: var(--text-tertiary); text-decoration: line-through; }
    li.feito .pp-texto small { display: none; }
    .pp-ir {
      display: inline-flex; align-items: center; gap: 4px; white-space: nowrap;
      padding: 8px 14px; border-radius: 6px; background: var(--brand-primary); color: var(--text-inverse);
      font-size: 13px; font-weight: 600; text-decoration: none;
      .material-icons { font-size: 16px; }
      &:hover { background: var(--brand-primary-hover); }
    }
    @media (max-width: 600px) {
      .pp { padding: 16px; }
      .pp-topo { flex-direction: column; }
      .pp-lista li { flex-wrap: wrap; gap: 10px; }
      .pp-ir { margin-left: 40px; }
    }
  `]
})
export class PrimeirosPassosComponent implements OnInit {
  private http = inject(HttpClient);
  private auth = inject(AuthService);
  private url = `${environment.apiUrl}/primeiros-passos`;

  dados: PrimeirosPassos | null = null;

  /** Cadastro de usuarios e' so do Administrador: o Gerente nao ve esse passo. */
  get passosVisiveis(): Passo[] {
    const admin = this.auth.hasRole('Administrador');
    return (this.dados?.passos ?? []).filter(p => admin || !p.soAdministrador);
  }

  get feitos(): number { return this.passosVisiveis.filter(p => p.feito).length; }
  get total(): number { return this.passosVisiveis.length; }
  get concluido(): boolean { return this.total > 0 && this.feitos === this.total; }

  ngOnInit(): void {
    if (!this.auth.isGerente()) return;
    this.http.get<PrimeirosPassos>(this.url).subscribe({ next: d => this.dados = d, error: () => {} });
  }

  ocultar(oculto: boolean): void {
    this.http.put<PrimeirosPassos>(`${this.url}/oculto`, { oculto }).subscribe({ next: d => this.dados = d, error: () => {} });
  }
}
