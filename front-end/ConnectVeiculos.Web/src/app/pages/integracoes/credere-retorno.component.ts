import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastService } from '../../core/services/toast.service';
import { CredereService, erroCredere } from '../../core/services/credere.service';

/**
 * Volta do login da Credere (redirect_uri cadastrada la): troca o codigo pela
 * chave de acesso no backend e leva para Integracoes.
 */
@Component({
  selector: 'app-credere-retorno',
  standalone: true,
  template: `<div class="page-container"><p>{{ mensagem }}</p></div>`
})
export class CredereRetornoComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private credere = inject(CredereService);
  private toast = inject(ToastService);

  mensagem = 'Conectando a Credere...';

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const code = q.get('code');
    const erro = q.get('error');
    if (erro || !code) {
      this.voltar(false, erro === 'access_denied' ? 'Conexão cancelada na Credere.' : 'A Credere não devolveu a autorização.');
      return;
    }
    this.credere.conectar(code, q.get('state')).subscribe({
      next: () => this.voltar(true, 'Credere conectada.'),
      error: err => this.voltar(false, erroCredere(err, 'Não foi possível conectar a Credere.'))
    });
  }

  private voltar(ok: boolean, texto: string): void {
    this.mensagem = texto;
    if (ok) this.toast.success(texto); else this.toast.error(texto);
    // replaceUrl: o codigo (uso unico) nao fica no historico do navegador.
    this.router.navigate(['/integracoes'], { replaceUrl: true });
  }
}
