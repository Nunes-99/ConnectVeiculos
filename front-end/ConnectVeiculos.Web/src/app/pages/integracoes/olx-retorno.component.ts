import { Component, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { switchMap, of } from 'rxjs';
import { ToastService } from '../../core/services/toast.service';
import { AuthService } from '../../core/services';
import { OlxService, erroOlx } from '../../core/services/olx.service';

/**
 * Volta do login da OLX (redirect_uri cadastrada la): troca o codigo pela
 * chave de acesso no backend e leva para Integracoes. Mesmo desenho do
 * retorno do NaPista: nao exige sessao, a loja vem do state.
 */
@Component({
  selector: 'app-olx-retorno',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page-container">
      <p>{{ mensagem }}</p>
      @if (terminou && !logado) {
        <p>Volte ao sistema e abra <strong>Integrações → OLX</strong>.</p>
        <a class="btn-primary" routerLink="/login">Entrar</a>
      }
    </div>`
})
export class OlxRetornoComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private olx = inject(OlxService);
  private toast = inject(ToastService);
  private auth = inject(AuthService);
  private platformId = inject(PLATFORM_ID);

  mensagem = 'Conectando a OLX...';
  terminou = false;
  logado = false;

  ngOnInit(): void {
    // O codigo vale uma vez so (10 min): a renderizacao no servidor nao pode gasta-lo.
    if (!isPlatformBrowser(this.platformId)) return;
    this.logado = this.auth.isAuthenticated();

    const q = this.route.snapshot.queryParamMap;
    const code = q.get('code');
    const state = q.get('state');
    const erro = q.get('error');
    if (erro || !code) {
      this.voltar(false, erro === 'access_denied' ? 'Conexão cancelada na OLX.' : 'A OLX não devolveu a autorização.');
      return;
    }
    this.olx.retorno(code, state).pipe(
      switchMap(r => r.conectado ? of(r) : this.olx.retorno(code, state, r.tenant))
    ).subscribe({
      next: r => r.conectado
        ? this.voltar(true, 'OLX conectada.')
        : this.voltar(false, 'Não foi possível conectar a OLX.'),
      error: err => this.voltar(false, erroOlx(err, 'Não foi possível conectar a OLX.'))
    });
  }

  private voltar(ok: boolean, texto: string): void {
    this.mensagem = texto;
    this.terminou = true;
    if (ok) this.toast.success(texto); else this.toast.error(texto);
    // replaceUrl: o codigo (uso unico) nao fica no historico do navegador.
    if (this.logado) this.router.navigate(['/integracoes'], { queryParams: { abrir: 'olx' }, replaceUrl: true });
    else this.router.navigate([], { replaceUrl: true });
  }
}
