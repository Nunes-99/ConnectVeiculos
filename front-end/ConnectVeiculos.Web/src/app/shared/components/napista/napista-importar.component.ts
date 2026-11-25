import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ToastService } from '../../../core/services/toast.service';
import { LojaService } from '../../../core/services/loja.service';
import { CategoriaService } from '../../../core/services/categoria.service';
import { Categoria, Loja } from '../../../core/models';
import {
  NapistaAnuncioExistente, NapistaImportacaoResultado, NapistaService, erroNapista
} from '../../../core/services/napista.service';

interface Linha extends NapistaAnuncioExistente {
  marcado: boolean;
  placaDigitada: string;
}

/**
 * "Anuncios que ja estao no NaPista" (cartao em Integracoes): para a loja que
 * ja anunciava la antes do ConnectVeiculos. Lista os publicados, e importar
 * cria o veiculo com fotos ligado ao anuncio (nao cria outro no NaPista).
 * Placa ja cadastrada so liga os dois.
 */
@Component({
  selector: 'app-napista-importar',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <h4>Anúncios que já estão no NaPista</h4>
    <p class="np-texto">
      Se a loja já anunciava no NaPista, traga esses carros para o ConnectVeiculos. Eles entram com fotos e ficam
      ligados ao anúncio de lá: editar ou vender aqui atualiza o NaPista.
    </p>

    @if (!linhas) {
      <button class="btn-secondary" (click)="carregar()" [disabled]="ocupado">
        <span class="material-icons">travel_explore</span> {{ ocupado ? 'Buscando...' : 'Ver anúncios do NaPista' }}
      </button>
    } @else if (linhas.length === 0) {
      <div class="np-aviso"><span class="material-icons">info</span><div>Nenhum anúncio publicado na loja do NaPista.</div></div>
      <button class="btn-secondary" (click)="carregar()" [disabled]="ocupado"><span class="material-icons">refresh</span> Buscar de novo</button>
    } @else {
      <div class="np-grid">
        <div class="form-group">
          <label>Entram na loja</label>
          <select [(ngModel)]="lojaId" [disabled]="ocupado">
            @for (l of lojas; track l.lojId) { <option [ngValue]="l.lojId">{{ l.lojNome }}</option> }
          </select>
        </div>
        <div class="form-group">
          <label>Categoria</label>
          <select [(ngModel)]="categoriaId" [disabled]="ocupado">
            @for (c of categorias; track c.catId) { <option [ngValue]="c.catId">{{ c.catNome }}</option> }
          </select>
        </div>
      </div>

      <label class="np-check np-todos">
        <input type="checkbox" [checked]="todosMarcados" (change)="marcarTodos($any($event.target).checked)" [disabled]="ocupado">
        {{ importaveis.length }} para importar · {{ linhas.length - importaveis.length }} já no ConnectVeiculos
      </label>

      <ul class="np-lista">
        @for (a of linhas; track a.anuncioId) {
          <li [class.np-feito]="!!a.veiculoVinculadoId">
            <input type="checkbox" [(ngModel)]="a.marcado" [disabled]="ocupado || !!a.veiculoVinculadoId" [attr.aria-label]="'Importar ' + a.titulo">
            @if (a.fotoCapa) {
              <!-- O desenvolvimento do NaPista responde 403 nas fotos: sem miniatura, fica o icone. -->
              <img [src]="a.fotoCapa" alt="" loading="lazy" referrerpolicy="no-referrer" (error)="a.fotoCapa = null">
            } @else {
              <span class="np-semfoto material-icons">directions_car</span>
            }
            <div class="np-info">
              <strong>{{ a.titulo }}</strong>
              <small>
                {{ a.anoFabricacao }}/{{ a.anoModelo }} · {{ a.km | number:'1.0-0':'pt-BR' }} km · {{ a.preco | currency:'BRL':'symbol':'1.0-0':'pt-BR' }}
                @if (a.cor) { · {{ a.cor }} } · {{ a.fotos }} foto(s)
              </small>
              @if (a.veiculoVinculadoId) {
                <span class="np-selo np-selo-ok">Já no ConnectVeiculos: {{ a.veiculoDescricao || ('veículo #' + a.veiculoVinculadoId) }}</span>
              } @else if (a.veiculoMesmaPlacaId) {
                <span class="np-selo">Placa {{ a.placa }} já cadastrada: só liga a {{ a.veiculoDescricao }}</span>
              } @else if (a.placaEscondida) {
                <input class="np-placa" type="text" maxlength="8" [(ngModel)]="a.placaDigitada" [disabled]="ocupado"
                       [placeholder]="'Placa (termina em ' + finalDaPlaca(a.placa) + ')'" (ngModelChange)="a.marcado = true">
              } @else {
                <small>Placa {{ a.placa }}</small>
              }
            </div>
          </li>
        }
      </ul>

      <div class="np-acoes">
        <button class="btn-primary" (click)="importar()" [disabled]="ocupado || marcados.length === 0 || !lojaId || !categoriaId">
          <span class="material-icons">download</span>
          {{ ocupado ? 'Importando...' : 'Importar ' + marcados.length + ' anúncio(s)' }}
        </button>
        <button class="btn-secondary" (click)="carregar()" [disabled]="ocupado"><span class="material-icons">refresh</span> Atualizar lista</button>
      </div>
    }

    @if (resultado) {
      <div [class]="resultado.erros ? 'np-aviso' : 'np-ok'">
        <span class="material-icons">{{ resultado.erros ? 'warning' : 'check_circle' }}</span>
        <div>
          <strong>{{ resultado.criados }} criado(s) · {{ resultado.vinculados }} ligado(s) a veículo existente · {{ resultado.erros }} com problema</strong>
          @for (d of resultado.detalhes; track d.anuncioId) {
            <small>
              {{ d.sucesso ? '✓' : '✗' }} {{ d.titulo || d.anuncioId }}: {{ d.mensagem }}
            </small>
          }
          @if (resultado.criados) { <small>Confira em <a routerLink="/veiculos">Veículos</a>: preço de compra e chassi ficam para preencher.</small> }
        </div>
      </div>
    }
  `,
  styleUrl: './napista.scss'
})
export class NapistaImportarComponent {
  private napista = inject(NapistaService);
  private lojaService = inject(LojaService);
  private categoriaService = inject(CategoriaService);
  private toast = inject(ToastService);

  linhas: Linha[] | null = null;
  lojas: Loja[] = [];
  categorias: Categoria[] = [];
  lojaId: number | null = null;
  categoriaId: number | null = null;
  ocupado = false;
  resultado: NapistaImportacaoResultado | null = null;

  get importaveis(): Linha[] { return (this.linhas ?? []).filter(l => !l.veiculoVinculadoId); }
  get marcados(): Linha[] { return this.importaveis.filter(l => l.marcado); }
  get todosMarcados(): boolean { return this.importaveis.length > 0 && this.importaveis.every(l => l.marcado); }

  finalDaPlaca(placa: string | null): string {
    return (placa ?? '').split('*').pop() || '?';
  }

  marcarTodos(v: boolean): void {
    this.importaveis.forEach(l => l.marcado = v);
  }

  carregar(): void {
    this.ocupado = true;
    forkJoin({
      anuncios: this.napista.anunciosExistentes(),
      lojas: this.lojaService.getAll(),
      categorias: this.categoriaService.getAll()
    }).subscribe({
      next: r => {
        this.ocupado = false;
        this.lojas = r.lojas;
        this.categorias = r.categorias;
        this.lojaId ??= r.lojas[0]?.lojId ?? null;
        this.categoriaId ??= r.categorias[0]?.catId ?? null;
        this.linhas = r.anuncios.map(a => ({ ...a, marcado: false, placaDigitada: '' }));
      },
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'Não foi possível buscar os anúncios do NaPista.')); }
    });
  }

  importar(): void {
    if (!this.lojaId || !this.categoriaId) return;
    const faltaPlaca = this.marcados.find(l => l.placaEscondida && !l.veiculoMesmaPlacaId && !l.placaDigitada.trim());
    if (faltaPlaca) {
      this.toast.warning(`Digite a placa de "${faltaPlaca.titulo}": o NaPista mostra só o final.`);
      return;
    }
    this.ocupado = true;
    this.resultado = null;
    this.napista.importarAnuncios({
      lojaId: this.lojaId,
      categoriaId: this.categoriaId,
      itens: this.marcados.map(l => ({ anuncioId: l.anuncioId, placa: l.placaEscondida ? l.placaDigitada.trim() : null }))
    }).subscribe({
      next: r => {
        this.ocupado = false;
        this.resultado = r;
        if (r.criados + r.vinculados > 0) this.toast.success(`${r.criados + r.vinculados} anúncio(s) do NaPista no ConnectVeiculos.`);
        this.carregar();
      },
      error: err => { this.ocupado = false; this.toast.error(erroNapista(err, 'Não foi possível importar.')); }
    });
  }
}
