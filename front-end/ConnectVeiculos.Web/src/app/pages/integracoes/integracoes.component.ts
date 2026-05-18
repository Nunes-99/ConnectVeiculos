import { Component, inject, OnInit, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { IntegracaoService, MercadoLivreContaInfo, MercadoLivreSincronizacaoResult, WhatsAppConfigInfo, EmailConfigInfo, FacebookConfigInfo, GoogleMerchantConfigInfo, TestIntegracaoResult } from '../../core/services/integracao.service';
import { ToastService } from '../../core/services';
import { ConfirmModalComponent } from '../../shared/components/confirm-modal/confirm-modal.component';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-integracoes',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmModalComponent],
  templateUrl: './integracoes.component.html',
  styleUrl: './integracoes.component.scss'
})
export class IntegracoesComponent implements OnInit {
  private integracaoService = inject(IntegracaoService);
  private toast = inject(ToastService);
  private platformId = inject(PLATFORM_ID);
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);

  // WhatsApp templates modal
  showWaTemplatesModal = false;

  templateConfirmado = `Olá {{1}}, tudo bem?

Seu test drive está CONFIRMADO! ✅

📅 Data: {{2}} às {{3}}
🚗 Veículo: {{4}}
📍 Local: {{5}}

Lembre-se de trazer um documento de identificação com foto (CNH ou RG).
Se precisar reagendar, é só responder esta mensagem.

Te esperamos!
_{{6}}_`;

  templateCancelado = `Olá {{1}},

Infelizmente precisamos CANCELAR seu test drive ❌

📅 Data: {{2}} às {{3}}
🚗 Veículo: {{4}}

Quer reagendar para outro horário? Responda esta mensagem ou nos chame que organizamos uma nova data.

Pedimos desculpas pelo transtorno.
_{{5}}_`;

  templateLembrete = `Olá {{1}}, tudo bem?

Passando pra LEMBRAR do seu test drive AMANHÃ! ⏰

📅 Data: {{2}} às {{3}}
🚗 Veículo: {{4}}
📍 Local: {{5}}

Não esqueça:
✅ Documento com foto (CNH ou RG)
✅ Chegue 10 minutos antes

Se precisar remarcar, é só responder esta mensagem.

Te esperamos!
_{{6}}_`;

  copiarTexto(texto: string): void {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(texto).then(() => this.toast.success('Texto copiado!'));
    }
  }

  mlConectado = false;
  mlConta: MercadoLivreContaInfo | null = null;
  mlLoading = false;
  mlSincronizando = false;
  mlSincronizacaoResult: MercadoLivreSincronizacaoResult | null = null;
  feedFacebookUrl = '';
  feedGoogleUrl = '';

  // Modais ML
  showSubstituirModal = false;
  showDesconectarModal = false;

  // WhatsApp
  waConfig: WhatsAppConfigInfo = { configurado: false, verifyTokenDefinido: false };
  waLoading = false;
  showWaConfigModal = false;
  showWaDesconectarModal = false;
  waAbaTutorial = true;
  webhookUrl = '';

  waForm: FormGroup = this.fb.group({
    accessToken: ['', Validators.required],
    phoneId: ['', Validators.required],
    verifyToken: ['connectveiculos-verify', Validators.required]
  });
  waSalvando = false;
  waMostrarToken = false;

  // SMTP / E-mail
  smtpConfig: EmailConfigInfo = { configurado: false, smtpPort: 587, enableSsl: true };
  smtpLoading = false;
  showSmtpConfigModal = false;
  showSmtpDesconectarModal = false;
  smtpForm: FormGroup = this.fb.group({
    smtpServer: ['smtp.gmail.com', Validators.required],
    smtpPort: [587, [Validators.required, Validators.min(1)]],
    username: ['', Validators.required],
    password: [''],
    senderEmail: ['', [Validators.required, Validators.email]],
    senderName: ['ConnectVeiculos', Validators.required],
    enableSsl: [true]
  });
  smtpSalvando = false;
  smtpMostrarSenha = false;
  smtpEmailTeste = '';
  smtpTestando = false;
  smtpTesteResultado: { sucesso: boolean; mensagem: string } | null = null;

  // Facebook Catalog (Push API)
  fbConfig: FacebookConfigInfo = { configurado: false, tokenDefinido: false };
  fbLoading = false;
  showFbConfigModal = false;
  showFbDesconectarModal = false;
  fbAbaTutorial = true;
  fbForm: FormGroup = this.fb.group({
    accessToken: ['', Validators.required],
    catalogId: ['', Validators.required],
    apiVersion: ['v18.0', Validators.required]
  });
  fbSalvando = false;
  fbMostrarToken = false;
  fbTestando = false;
  fbTesteResultado: TestIntegracaoResult | null = null;
  fbVerifCode = '';
  fbVerifSalvando = false;
  fbVerifEditando = false;
  fbVerifMensagem: { sucesso: boolean; texto: string } | null = null;

  // Google Merchant (Push API)
  gmConfig: GoogleMerchantConfigInfo = { configurado: false, clientSecretDefinido: false, refreshTokenDefinido: false };
  gmLoading = false;
  showGmConfigModal = false;
  showGmDesconectarModal = false;
  gmAbaTutorial = true;
  gmForm: FormGroup = this.fb.group({
    clientId: ['', Validators.required],
    clientSecret: ['', Validators.required],
    refreshToken: ['', Validators.required],
    merchantId: ['', Validators.required]
  });
  gmSalvando = false;
  gmMostrarSecret = false;
  gmMostrarRefresh = false;
  gmTrocarSecret = false;
  gmTrocarRefresh = false;
  gmTestando = false;
  gmTesteResultado: TestIntegracaoResult | null = null;
  gmVerifCode = '';
  gmVerifSalvando = false;
  gmVerifEditando = false;
  gmVerifMensagem: { sucesso: boolean; texto: string } | null = null;

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      // Lidos no client porque dependem do tenant slug do localStorage,
      // indisponivel durante SSR.
      this.feedFacebookUrl = this.integracaoService.getFacebookFeedUrl();
      this.feedGoogleUrl = this.integracaoService.getGoogleFeedUrl();
      this.webhookUrl = `${window.location.origin}/api/integracoes/whatsapp/webhook`;
    }
    this.checkMercadoLivreStatus();
    this.checkWhatsAppStatus();
    this.checkSmtpStatus();
    this.checkFacebookStatus();
    this.checkGoogleStatus();
    this.loadVerificationCodes();
  }

  // ============================================================
  // Verificacao de dominio (Google + Facebook)
  // ============================================================
  loadVerificationCodes(): void {
    this.integracaoService.getFacebookVerificationCode().subscribe({
      next: (r) => { this.fbVerifCode = r.code ?? ''; },
      error: () => { /* silencioso — usuario sem permissao ou tenant nao resolvido */ }
    });
    this.integracaoService.getGoogleVerificationCode().subscribe({
      next: (r) => { this.gmVerifCode = r.code ?? ''; },
      error: () => { /* silencioso */ }
    });
  }

  salvarFbVerifCode(): void {
    if (this.fbVerifSalvando) return;
    this.fbVerifSalvando = true;
    this.fbVerifMensagem = null;
    const code = (this.fbVerifCode || '').trim();
    this.integracaoService.saveFacebookVerificationCode(code).subscribe({
      next: () => {
        this.fbVerifSalvando = false;
        this.fbVerifEditando = false;
        this.fbVerifMensagem = { sucesso: true, texto: code ? 'Codigo salvo. Aguarde ate 5 minutos para o site refletir e clique em Verificar no Meta.' : 'Codigo removido.' };
        this.toast.success('Codigo Facebook salvo.');
      },
      error: (err) => {
        this.fbVerifSalvando = false;
        const msg = err?.error?.error || 'Erro ao salvar codigo Facebook.';
        this.fbVerifMensagem = { sucesso: false, texto: msg };
        this.toast.error(msg);
      }
    });
  }

  salvarGmVerifCode(): void {
    if (this.gmVerifSalvando) return;
    this.gmVerifSalvando = true;
    this.gmVerifMensagem = null;
    const code = (this.gmVerifCode || '').trim();
    this.integracaoService.saveGoogleVerificationCode(code).subscribe({
      next: () => {
        this.gmVerifSalvando = false;
        this.gmVerifEditando = false;
        this.gmVerifMensagem = { sucesso: true, texto: code ? 'Codigo salvo. Aguarde ate 5 minutos para o site refletir e clique em Verificar no Google.' : 'Codigo removido.' };
        this.toast.success('Codigo Google salvo.');
      },
      error: (err) => {
        this.gmVerifSalvando = false;
        const msg = err?.error?.error || 'Erro ao salvar codigo Google.';
        this.gmVerifMensagem = { sucesso: false, texto: msg };
        this.toast.error(msg);
      }
    });
  }

  // ============================================================
  // Mercado Livre
  // ============================================================
  checkMercadoLivreStatus(): void {
    this.mlLoading = true;
    this.integracaoService.getMercadoLivreInfo().subscribe({
      next: (result) => {
        this.mlConectado = result.conectado;
        this.mlConta = result.info ?? null;
        this.mlLoading = false;
      },
      error: () => {
        this.mlConectado = false;
        this.mlConta = null;
        this.mlLoading = false;
      }
    });
  }

  conectarMercadoLivre(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (this.mlConectado && this.mlConta) { this.showSubstituirModal = true; return; }
    this.abrirPopupAutorizacao();
  }

  confirmarSubstituicao(): void { this.showSubstituirModal = false; this.abrirPopupAutorizacao(); }
  cancelarSubstituicao(): void { this.showSubstituirModal = false; }

  private abrirPopupAutorizacao(): void {
    this.integracaoService.getMercadoLivreAuthUrl().subscribe({
      next: (result) => {
        const popup = window.open(result.url, '_blank', 'width=600,height=700');
        const timer = setInterval(() => {
          if (popup?.closed) {
            clearInterval(timer);
            setTimeout(() => this.checkMercadoLivreStatus(), 1000);
          }
        }, 500);
      },
      error: () => this.toast.error('Erro ao obter URL de autorização do Mercado Livre.')
    });
  }

  pedirDesconectar(): void { this.showDesconectarModal = true; }
  confirmarDesconectar(): void {
    this.showDesconectarModal = false;
    this.integracaoService.desconectarMercadoLivre().subscribe({
      next: () => { this.toast.success('Mercado Livre desconectado.'); this.checkMercadoLivreStatus(); },
      error: () => this.toast.error('Erro ao desconectar Mercado Livre.')
    });
  }
  cancelarDesconectar(): void { this.showDesconectarModal = false; }

  sincronizarMlDisponiveis(): void {
    if (this.mlSincronizando) return;
    this.mlSincronizando = true;
    this.mlSincronizacaoResult = null;
    this.integracaoService.sincronizarMercadoLivreDisponiveis().subscribe({
      next: (res) => {
        this.mlSincronizacaoResult = res;
        this.mlSincronizando = false;
        if (res.novosPublicados > 0) {
          this.toast.success(`${res.novosPublicados} veiculo(s) publicado(s) no Mercado Livre.`);
        } else if (res.totalDisponiveis === 0) {
          this.toast.success('Nenhum veiculo disponivel para publicar.');
        } else if (res.falhas.length === 0) {
          this.toast.success('Todos os veiculos disponiveis ja estao publicados.');
        } else {
          this.toast.error(`${res.falhas.length} veiculo(s) falharam ao publicar. Veja os detalhes.`);
        }
      },
      error: (err) => {
        this.mlSincronizando = false;
        this.toast.error(err?.error?.error || 'Erro ao sincronizar veiculos no Mercado Livre.');
      }
    });
  }

  // ============================================================
  // WhatsApp
  // ============================================================
  checkWhatsAppStatus(): void {
    this.waLoading = true;
    this.integracaoService.getWhatsAppConfig().subscribe({
      next: (info) => { this.waConfig = info; this.waLoading = false; },
      error: () => {
        // Se nao for admin, fallback pra status (publico)
        this.integracaoService.getWhatsAppStatus().subscribe({
          next: (s) => { this.waConfig = { configurado: s.configurado, verifyTokenDefinido: false }; this.waLoading = false; },
          error: () => { this.waConfig = { configurado: false, verifyTokenDefinido: false }; this.waLoading = false; }
        });
      }
    });
  }

  // Nonce regenerado a cada abertura de modal — usado no atributo `name` dos inputs
  // pra impedir que o navegador identifique campos de credenciais e dispare autofill
  // (Chrome estava enchendo Access Token com a senha salva e Phone ID com o email).
  inputNonce = Math.random().toString(36).slice(2, 10);

  abrirConfigWhatsApp(): void {
    this.waAbaTutorial = !this.waConfig.configurado;
    this.waMostrarToken = false;
    this.inputNonce = Math.random().toString(36).slice(2, 10);
    this.waForm.reset({
      accessToken: '',
      phoneId: this.waConfig.phoneId || '',
      verifyToken: 'connectveiculos-verify'
    });
    this.showWaConfigModal = true;
  }

  fecharConfigWhatsApp(): void { this.showWaConfigModal = false; }

  salvarWhatsApp(): void {
    if (this.waForm.invalid) { this.waForm.markAllAsTouched(); return; }
    this.waSalvando = true;
    this.integracaoService.saveWhatsAppConfig(this.waForm.value).subscribe({
      next: () => {
        this.toast.success('Credenciais WhatsApp salvas.');
        this.waSalvando = false;
        this.showWaConfigModal = false;
        this.checkWhatsAppStatus();
      },
      error: () => {
        this.toast.error('Erro ao salvar credenciais.');
        this.waSalvando = false;
      }
    });
  }

  pedirDesconectarWhatsApp(): void { this.showWaDesconectarModal = true; }

  confirmarDesconectarWhatsApp(): void {
    this.showWaDesconectarModal = false;
    this.integracaoService.desconectarWhatsApp().subscribe({
      next: () => { this.toast.success('WhatsApp desconectado.'); this.checkWhatsAppStatus(); },
      error: () => this.toast.error('Erro ao desconectar WhatsApp.')
    });
  }

  cancelarDesconectarWhatsApp(): void { this.showWaDesconectarModal = false; }

  // ============================================================
  // SMTP / E-mail
  // ============================================================
  checkSmtpStatus(): void {
    this.smtpLoading = true;
    this.integracaoService.getSmtpConfig().subscribe({
      next: (info) => { this.smtpConfig = info; this.smtpLoading = false; },
      error: () => {
        this.smtpConfig = { configurado: false, smtpPort: 587, enableSsl: true };
        this.smtpLoading = false;
      }
    });
  }

  abrirConfigSmtp(): void {
    this.smtpMostrarSenha = false;
    this.smtpTesteResultado = null;
    this.smtpForm.reset({
      smtpServer: this.smtpConfig.smtpServer || 'smtp.gmail.com',
      smtpPort: this.smtpConfig.smtpPort || 587,
      username: this.smtpConfig.username || '',
      password: '',
      senderEmail: this.smtpConfig.senderEmail || '',
      senderName: this.smtpConfig.senderName || 'ConnectVeiculos',
      enableSsl: this.smtpConfig.enableSsl ?? true
    });
    this.showSmtpConfigModal = true;
  }

  fecharConfigSmtp(): void {
    this.showSmtpConfigModal = false;
    this.smtpTesteResultado = null;
  }

  salvarSmtp(): void {
    if (this.smtpForm.invalid) { this.smtpForm.markAllAsTouched(); return; }
    this.smtpSalvando = true;
    this.integracaoService.saveSmtpConfig(this.smtpForm.value).subscribe({
      next: () => {
        this.toast.success('Configuração SMTP salva.');
        this.smtpSalvando = false;
        this.showSmtpConfigModal = false;
        this.checkSmtpStatus();
      },
      error: () => { this.toast.error('Erro ao salvar SMTP.'); this.smtpSalvando = false; }
    });
  }

  testarSmtp(): void {
    if (!this.smtpEmailTeste || !this.smtpEmailTeste.includes('@')) {
      this.toast.warning('Informe um e-mail válido para o teste.');
      return;
    }
    this.smtpTestando = true;
    this.smtpTesteResultado = null;
    this.integracaoService.testarSmtp(this.smtpEmailTeste).subscribe({
      next: (r) => { this.smtpTesteResultado = r; this.smtpTestando = false; },
      error: (err) => {
        this.smtpTesteResultado = { sucesso: false, mensagem: err?.error?.mensagem || 'Falha no envio.' };
        this.smtpTestando = false;
      }
    });
  }

  pedirDesconectarSmtp(): void { this.showSmtpDesconectarModal = true; }
  confirmarDesconectarSmtp(): void {
    this.showSmtpDesconectarModal = false;
    this.integracaoService.desconectarSmtp().subscribe({
      next: () => { this.toast.success('SMTP desconectado.'); this.checkSmtpStatus(); },
      error: () => this.toast.error('Erro ao desconectar SMTP.')
    });
  }
  cancelarDesconectarSmtp(): void { this.showSmtpDesconectarModal = false; }

  // ============================================================
  // Facebook Catalog (Push API)
  // ============================================================
  checkFacebookStatus(): void {
    this.fbLoading = true;
    this.integracaoService.getFacebookConfig().subscribe({
      next: (info) => { this.fbConfig = info; this.fbLoading = false; },
      error: () => {
        this.fbConfig = { configurado: false, tokenDefinido: false };
        this.fbLoading = false;
      }
    });
  }

  abrirConfigFacebook(): void {
    this.fbAbaTutorial = !this.fbConfig.configurado;
    this.fbMostrarToken = false;
    this.fbTesteResultado = null;
    this.inputNonce = Math.random().toString(36).slice(2, 10);
    this.fbForm.reset({
      accessToken: '',
      catalogId: this.fbConfig.catalogId || '',
      apiVersion: this.fbConfig.apiVersion || 'v18.0'
    });
    this.showFbConfigModal = true;
  }

  fecharConfigFacebook(): void {
    this.showFbConfigModal = false;
    this.fbTesteResultado = null;
  }

  salvarFacebook(): void {
    if (this.fbForm.invalid) { this.fbForm.markAllAsTouched(); return; }
    this.fbSalvando = true;
    this.integracaoService.saveFacebookConfig(this.fbForm.value).subscribe({
      next: () => {
        this.toast.success('Facebook Catalog configurado.');
        this.fbSalvando = false;
        this.showFbConfigModal = false;
        this.checkFacebookStatus();
      },
      error: (err) => {
        this.toast.error(err?.error?.error || 'Erro ao salvar Facebook Catalog.');
        this.fbSalvando = false;
      }
    });
  }

  testarFacebook(): void {
    this.fbTestando = true;
    this.fbTesteResultado = null;
    this.integracaoService.testarFacebook().subscribe({
      next: (r) => { this.fbTesteResultado = r; this.fbTestando = false; },
      error: (err) => {
        this.fbTesteResultado = err?.error || { sucesso: false, mensagem: 'Falha no teste.' };
        this.fbTestando = false;
      }
    });
  }

  pedirDesconectarFacebook(): void { this.showFbDesconectarModal = true; }
  confirmarDesconectarFacebook(): void {
    this.showFbDesconectarModal = false;
    this.integracaoService.desconectarFacebook().subscribe({
      next: () => { this.toast.success('Facebook Catalog desconectado.'); this.checkFacebookStatus(); },
      error: () => this.toast.error('Erro ao desconectar Facebook Catalog.')
    });
  }
  cancelarDesconectarFacebook(): void { this.showFbDesconectarModal = false; }

  // ============================================================
  // Google Merchant (Push API)
  // ============================================================
  checkGoogleStatus(): void {
    this.gmLoading = true;
    this.integracaoService.getGoogleConfig().subscribe({
      next: (info) => { this.gmConfig = info; this.gmLoading = false; },
      error: () => {
        this.gmConfig = { configurado: false, clientSecretDefinido: false, refreshTokenDefinido: false };
        this.gmLoading = false;
      }
    });
  }

  abrirConfigGoogle(): void {
    this.gmAbaTutorial = !this.gmConfig.configurado;
    this.gmMostrarSecret = false;
    this.gmMostrarRefresh = false;
    this.gmTesteResultado = null;
    this.inputNonce = Math.random().toString(36).slice(2, 10);

    // Secret/Refresh sao required apenas quando ainda nao estao salvos no backend.
    // Se ja salvos, exibimos campo travado com botao "Trocar" e o backend mantem o valor existente.
    this.gmTrocarSecret = !this.gmConfig.clientSecretDefinido;
    this.gmTrocarRefresh = !this.gmConfig.refreshTokenDefinido;

    const secretCtrl = this.gmForm.get('clientSecret');
    const refreshCtrl = this.gmForm.get('refreshToken');
    if (this.gmConfig.clientSecretDefinido) {
      secretCtrl?.clearValidators();
    } else {
      secretCtrl?.setValidators([Validators.required]);
    }
    if (this.gmConfig.refreshTokenDefinido) {
      refreshCtrl?.clearValidators();
    } else {
      refreshCtrl?.setValidators([Validators.required]);
    }
    secretCtrl?.updateValueAndValidity();
    refreshCtrl?.updateValueAndValidity();

    this.gmForm.reset({
      clientId: this.gmConfig.clientId || '',
      clientSecret: '',
      refreshToken: '',
      merchantId: this.gmConfig.merchantId || ''
    });
    this.showGmConfigModal = true;
  }

  trocarGmSecret(): void {
    this.gmTrocarSecret = true;
    const ctrl = this.gmForm.get('clientSecret');
    ctrl?.setValidators([Validators.required]);
    ctrl?.updateValueAndValidity();
    ctrl?.setValue('');
  }

  trocarGmRefresh(): void {
    this.gmTrocarRefresh = true;
    const ctrl = this.gmForm.get('refreshToken');
    ctrl?.setValidators([Validators.required]);
    ctrl?.updateValueAndValidity();
    ctrl?.setValue('');
  }

  fecharConfigGoogle(): void {
    this.showGmConfigModal = false;
    this.gmTesteResultado = null;
  }

  salvarGoogle(): void {
    if (this.gmForm.invalid) { this.gmForm.markAllAsTouched(); return; }
    this.gmSalvando = true;
    this.integracaoService.saveGoogleConfig(this.gmForm.value).subscribe({
      next: () => {
        this.toast.success('Google Merchant configurado.');
        this.gmSalvando = false;
        this.showGmConfigModal = false;
        this.checkGoogleStatus();
      },
      error: (err) => {
        this.toast.error(err?.error?.error || 'Erro ao salvar Google Merchant.');
        this.gmSalvando = false;
      }
    });
  }

  testarGoogle(): void {
    this.gmTestando = true;
    this.gmTesteResultado = null;
    this.integracaoService.testarGoogle().subscribe({
      next: (r) => { this.gmTesteResultado = r; this.gmTestando = false; },
      error: (err) => {
        this.gmTesteResultado = err?.error || { sucesso: false, mensagem: 'Falha no teste.' };
        this.gmTestando = false;
      }
    });
  }

  pedirDesconectarGoogle(): void { this.showGmDesconectarModal = true; }
  confirmarDesconectarGoogle(): void {
    this.showGmDesconectarModal = false;
    this.integracaoService.desconectarGoogle().subscribe({
      next: () => { this.toast.success('Google Merchant desconectado.'); this.checkGoogleStatus(); },
      error: () => this.toast.error('Erro ao desconectar Google Merchant.')
    });
  }
  cancelarDesconectarGoogle(): void { this.showGmDesconectarModal = false; }

  // ============================================================
  // Util
  // ============================================================
  copiarUrl(url: string): void {
    if (!isPlatformBrowser(this.platformId)) return;
    navigator.clipboard.writeText(url).then(() => this.toast.success('URL copiada!'));
  }
}
