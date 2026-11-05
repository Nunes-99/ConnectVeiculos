import { Component, HostListener, OnInit, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter, interval } from 'rxjs';
import { ToastComponent } from './shared/components/toast/toast.component';
import { ToastService } from './core/services/toast.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'ConnectVeiculos.Web';

  private sw = inject(SwUpdate);
  private toast = inject(ToastService);
  private platformId = inject(PLATFORM_ID);

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) this.avisarVersaoNova();
  }

  /**
   * Esc fecha a janela que estiver por cima. As janelas do sistema seguem o
   * mesmo molde (.modal-overlay com o X .btn-close/.modal-close, um botao
   * marcado com data-esc, ou um botao Cancelar/Fechar),
   * entao uma regra so aqui vale para todas, inclusive as que vierem depois.
   * Clicar no proprio botao garante o mesmo comportamento do clique do usuario
   * (ex.: janela que pede confirmacao antes de descartar).
   */
  @HostListener('document:keydown.escape', ['$event'])
  fecharJanelaComEsc(ev: Event): void {
    if (ev.defaultPrevented) return;
    const janelas = Array.from(document.querySelectorAll<HTMLElement>('.modal-overlay'))
      .filter(j => j.getBoundingClientRect().width > 0);
    const topo = janelas[janelas.length - 1];
    if (!topo) return;
    const botoes = Array.from(topo.querySelectorAll<HTMLButtonElement>('button:not([disabled])'));
    const fechar = topo.querySelector<HTMLButtonElement>('[data-esc]:not([disabled]), .btn-close:not([disabled]), .modal-close:not([disabled])')
      ?? botoes.find(b => /^\s*(cancelar|fechar)\s*$/i.test(b.innerText));
    if (fechar) {
      ev.preventDefault();
      fechar.click();
    }
  }

  /**
   * O service worker guarda a versão anterior do site: sem isto, quem já tinha
   * aberto o sistema seguia na versão velha depois de uma publicação, sem saber.
   * Não recarrega sozinho — no meio de um cadastro de veículo perderia o que foi
   * digitado; avisa e deixa a pessoa escolher a hora.
   */
  private avisarVersaoNova(): void {
    if (!this.sw.isEnabled) return;
    this.sw.versionUpdates
      .pipe(filter((e): e is VersionReadyEvent => e.type === 'VERSION_READY'))
      .subscribe(() => {
        this.toast.show('Saiu uma versão nova do sistema.', 'info', 0,
          { rotulo: 'Atualizar', executar: () => document.location.reload() });
      });
    // Versão quebrada no cache (arquivo que sumiu do servidor): só recarregando.
    this.sw.unrecoverable.subscribe(() => document.location.reload());
    // O painel fica aberto o dia todo: confere de tempos em tempos, não só ao abrir.
    interval(30 * 60 * 1000).subscribe(() => this.sw.checkForUpdate().catch(() => { /* sem rede: tenta depois */ }));
  }
}
