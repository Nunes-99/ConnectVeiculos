import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

interface Campo { rotulo: string; valor: unknown; tipo: 'texto' | 'objeto' | 'lista' | 'listaTexto'; }

/**
 * Mostra o bloco "dados" de qualquer consulta da ZapCar. Cada servico devolve
 * um formato (vem da base de origem), entao a tela percorre o JSON: texto
 * vira linha "rotulo: valor", objeto vira grupo e lista vira cartoes.
 */
@Component({
  selector: 'app-dados-consulta',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (campos(dados).length) {
      <dl class="dc" [class.aninhado]="nivel > 0">
        @for (c of campos(dados); track c.rotulo) {
          @switch (c.tipo) {
            @case ('texto') {
              <div class="dc-linha"><dt>{{ c.rotulo }}</dt><dd>{{ texto(c.valor) }}</dd></div>
            }
            @case ('listaTexto') {
              <div class="dc-linha"><dt>{{ c.rotulo }}</dt><dd>{{ listaTexto(c.valor) }}</dd></div>
            }
            @case ('objeto') {
              <div class="dc-grupo">
                <h4>{{ c.rotulo }}</h4>
                <app-dados-consulta [dados]="c.valor" [nivel]="nivel + 1" />
              </div>
            }
            @case ('lista') {
              <div class="dc-grupo">
                <h4>{{ c.rotulo }} <small>({{ $any(c.valor).length }})</small></h4>
                @for (item of $any(c.valor); track $index) {
                  <div class="dc-item"><app-dados-consulta [dados]="item" [nivel]="nivel + 1" /></div>
                }
              </div>
            }
          }
        }
      </dl>
    } @else if (nivel === 0) {
      <p class="dc-vazio">A base não devolveu dados além do resumo.</p>
    }
  `,
  styles: [`
    :host { display: block; }
    .dc { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 10px 20px; margin: 0; }
    .dc.aninhado { gap: 6px 16px; }
    .dc-linha { min-width: 0; }
    dt { font-size: 12px; color: var(--text-tertiary); }
    dd { margin: 2px 0 0; color: var(--text-primary); font-weight: 500; overflow-wrap: anywhere; white-space: pre-line; }
    .dc-grupo { grid-column: 1 / -1; border-top: 1px solid var(--border-light); padding-top: 10px; }
    h4 { margin: 0 0 8px; font-size: 13px; color: var(--text-secondary); text-transform: uppercase; letter-spacing: .3px; }
    h4 small { text-transform: none; font-weight: 400; }
    .dc-item { padding: 10px 12px; border: 1px solid var(--border-light); border-radius: 8px; margin-bottom: 8px; background: var(--bg-secondary, var(--bg-tertiary)); }
    .dc-vazio { color: var(--text-tertiary); font-size: 13px; margin: 0; }
  `]
})
export class DadosConsultaComponent {
  @Input() dados: unknown;
  @Input() nivel = 0;

  campos(dados: unknown): Campo[] {
    if (!dados || typeof dados !== 'object' || Array.isArray(dados)) {
      return dados === null || dados === undefined ? [] : [{ rotulo: 'Valor', valor: dados, tipo: 'texto' }];
    }
    return Object.entries(dados as Record<string, unknown>)
      .filter(([k, v]) => !k.startsWith('_') && v !== null && v !== undefined && v !== '' && !(Array.isArray(v) && v.length === 0))
      .map(([k, v]) => {
        const rotulo = this.rotulo(k);
        if (Array.isArray(v)) {
          return v.every(x => x === null || typeof x !== 'object')
            ? { rotulo, valor: v, tipo: 'listaTexto' as const }
            : { rotulo, valor: v, tipo: 'lista' as const };
        }
        if (typeof v === 'object') return { rotulo, valor: v, tipo: 'objeto' as const };
        return { rotulo, valor: v, tipo: 'texto' as const };
      })
      // Linhas simples primeiro, grupos depois.
      .sort((a, b) => (a.tipo === 'texto' || a.tipo === 'listaTexto' ? 0 : 1) - (b.tipo === 'texto' || b.tipo === 'listaTexto' ? 0 : 1));
  }

  /** As bases mandam as chaves sem acento: corrige as palavras mais comuns. */
  private static readonly ACENTOS: Record<string, string> = {
    identificacao: 'identificação', endereco: 'endereço', enderecos: 'endereços', numero: 'número',
    situacao: 'situação', mae: 'mãe', emissao: 'emissão', informacoes: 'informações', informacao: 'informação',
    ocorrencia: 'ocorrência', ocorrencias: 'ocorrências', restricao: 'restrição', restricoes: 'restrições',
    proprietario: 'proprietário', proprietarios: 'proprietários', debito: 'débito', debitos: 'débitos',
    veiculo: 'veículo', veiculos: 'veículos', inscricao: 'inscrição', razao: 'razão', credito: 'crédito',
    pendencia: 'pendência', pendencias: 'pendências', nao: 'não', codigo: 'código', descricao: 'descrição',
    orgao: 'órgão', municipio: 'município', exercicio: 'exercício', ultimo: 'último', ultima: 'última',
    historico: 'histórico', infracao: 'infração', infracoes: 'infrações', leilao: 'leilão', sinistro: 'sinistro',
    telefone: 'telefone', socios: 'sócios', socio: 'sócio', participacao: 'participação', acoes: 'ações',
    acao: 'ação', protestos: 'protestos', cheques: 'cheques', consultas: 'consultas', validade: 'validade',
    categoria: 'categoria', habilitacao: 'habilitação', pontuacao: 'pontuação', fabricacao: 'fabricação',
    combustivel: 'combustível', potencia: 'potência', especie: 'espécie', procedencia: 'procedência'
  };

  /** "data_venda" / "dataVenda" / "DATA_VENDA" -> "Data venda". */
  rotulo(chave: string): string {
    const t = chave
      .replace(/_+/g, ' ')
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
      .trim()
      .toLowerCase()
      .split(/\s+/)
      .map(p => DadosConsultaComponent.ACENTOS[p] ?? p)
      .join(' ');
    return t.charAt(0).toUpperCase() + t.slice(1);
  }

  texto(v: unknown): string {
    if (typeof v === 'boolean') return v ? 'Sim' : 'Não';
    if (typeof v === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(v)) {
      const d = new Date(v);
      return isNaN(d.getTime()) ? v : d.toLocaleString('pt-BR');
    }
    return String(v);
  }

  listaTexto(v: unknown): string {
    return (v as unknown[]).filter(x => x !== null && x !== undefined).map(x => this.texto(x)).join(', ');
  }
}
