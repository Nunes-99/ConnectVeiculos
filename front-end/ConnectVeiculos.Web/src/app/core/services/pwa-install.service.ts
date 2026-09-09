import { Injectable, PLATFORM_ID, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

/**
 * Evento nao padronizado do Chrome/Edge. Nao existe na lib.dom, entao a forma
 * minima que usamos fica declarada aqui.
 */
interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  readonly userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

/**
 * Guarda o convite de instalacao do PWA para que a aplicacao possa oferece-lo
 * na hora certa, num botao proprio.
 *
 * O navegador dispara `beforeinstallprompt` uma unica vez e so aceita
 * `prompt()` dentro de um gesto do usuario. Se ninguem chamar preventDefault no
 * evento, o Chrome mostra (ou nao) o proprio banner e o convite se perde — por
 * isso ele e' interceptado assim que a aplicacao sobe e guardado aqui.
 *
 * `podeInstalar` fica falso quando o app ja esta instalado, quando o navegador
 * nao suporta instalacao (Firefox, e o iOS, que so instala pelo menu Compartilhar)
 * ou quando os criterios de instalabilidade nao foram atendidos.
 */
@Injectable({ providedIn: 'root' })
export class PwaInstallService {
  private readonly platformId = inject(PLATFORM_ID);
  private convite: BeforeInstallPromptEvent | null = null;

  /** Ha um convite de instalacao guardado e pronto para ser disparado. */
  readonly podeInstalar = signal(false);

  /** A aplicacao esta rodando como app instalado, e nao numa aba do navegador. */
  readonly jaInstalado = signal(false);

  constructor() {
    if (!isPlatformBrowser(this.platformId)) return;

    this.jaInstalado.set(this.rodandoInstalado());

    window.addEventListener('beforeinstallprompt', (e: Event) => {
      // Sem preventDefault o navegador assume o controle e o convite se perde.
      e.preventDefault();
      this.convite = e as BeforeInstallPromptEvent;
      this.podeInstalar.set(true);
    });

    window.addEventListener('appinstalled', () => {
      this.convite = null;
      this.podeInstalar.set(false);
      this.jaInstalado.set(true);
    });
  }

  /**
   * Abre o dialogo nativo de instalacao. Precisa ser chamado a partir de um
   * clique — o navegador ignora a chamada fora de um gesto do usuario.
   * Devolve true se a pessoa aceitou instalar.
   */
  async instalar(): Promise<boolean> {
    if (!this.convite) return false;

    await this.convite.prompt();
    const { outcome } = await this.convite.userChoice;

    // O convite e' de uso unico: aceito ou recusado, o navegador nao permite
    // dispara-lo de novo com o mesmo evento.
    this.convite = null;
    this.podeInstalar.set(false);

    return outcome === 'accepted';
  }

  private rodandoInstalado(): boolean {
    const comoApp = window.matchMedia?.('(display-mode: standalone)').matches;
    // iOS nao implementa display-mode e usa esta propriedade propria.
    const iosStandalone = (window.navigator as unknown as { standalone?: boolean }).standalone === true;
    return Boolean(comoApp || iosStandalone);
  }
}
