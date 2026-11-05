import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap, of } from 'rxjs';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services';
import { CredereService, erroCredere } from '../../core/services/credere.service';

/**
 * Volta do login da Credere (redirect_uri cadastrada la): troca o codigo pela
 * chave de acesso no backend e leva para Integracoes.
 *
 * Nao exige sessao: quem clicou em Conectar pelo dominio proprio da loja chega
 * aqui sem login no connectveiculos.dev.br. A loja vem do state.
 */
@Component({
  selector: 'app-credere-retorno',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page-container">
      <p>{{ mensagem }}</p>
      @if (terminou && !logado) {
        <p>Volte ao sistema e abra <strong>Integrações → Credere</strong> para escolher a loja.</p>
        <a class="btn-primary" routerLink="/login">Entrar</a>
      }
    </div>`
})
export class CredereRetornoComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private credere = inject(CredereService);
  private toast = inject(ToastService);
  private auth = inject(AuthService);
  private platformId = inject(PLATFORM_ID);

  mensagem = 'Conectando a Credere...';
  terminou = false;
  logado = false;

  ngOnInit(): void {
    // O codigo vale uma vez so: a renderizacao no servidor nao pode gasta-lo.
    if (!isPlatformBrowser(this.platformId)) return;
    this.logado = this.auth.isAuthenticated();

    const q = this.route.snapshot.queryParamMap;
    const code = q.get('code');
    const state = q.get('state');
    const erro = q.get('error');
    if (erro || !code) {
      this.voltar(false, erro === 'access_denied' ? 'Conexão cancelada na Credere.' : 'A Credere não devolveu a autorização.');
      return;
    }
    // A primeira chamada cai na loja deste dominio; se o state for de outra,
    // o backend devolve o slug e a chamada se repete nela.
    this.credere.retorno(code, state).pipe(
      switchMap(r => r.conectado ? of(r) : this.credere.retorno(code, state, r.tenant))
    ).subscribe({
      next: r => r.conectado
        ? this.voltar(true, 'Credere conectada.')
        : this.voltar(false, 'Não foi possível conectar a Credere.'),
      error: err => this.voltar(false, erroCredere(err, 'Não foi possível conectar a Credere.'))
    });
  }

  private voltar(ok: boolean, texto: string): void {
    this.mensagem = texto;
    this.terminou = true;
    if (ok) this.toast.success(texto); else this.toast.error(texto);
    // replaceUrl: o codigo (uso unico) nao fica no historico do navegador.
    if (this.logado) this.router.navigate(['/integracoes'], { queryParams: { abrir: 'credere' }, replaceUrl: true });
    else this.router.navigate([], { replaceUrl: true });
  }
}
