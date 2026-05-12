import { Component, inject, OnInit, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { IntegracaoService, MercadoLivreContaInfo, WhatsAppConfigInfo, EmailConfigInfo, FacebookConfigInfo, GoogleMerchantConfigInfo, TestIntegracaoResult } from '../../core/services/integracao.service';
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

  // Gemini OCR
  geminiConfigurado = false;
  geminiMascara: string | null = null;
  geminiFonte: string | null = null;
  geminiNovaChave = '';
  geminiMostrar = false;
  geminiSalvando = false;

  private carregarGeminiConfig(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    this.http.get<any>(`${environment.apiUrl}/ocr/config`).subscribe({
      next: (resp) => {
        this.geminiConfigurado = !!resp?.configurado;
        this.geminiMascara = resp?.mascara || null;
        this.geminiFonte = resp?.fonte || null;
      },
      error: () => { /* sem permissao ou erro — UI fica em "nao configurado" */ }
    });
  }

  salvarGemini(): void {
    if (!this.geminiNovaChave) return;
    this.geminiSalvando = true;
    this.http.post(`${environment.apiUrl}/ocr/config`, { chave: this.geminiNovaChave }).subscribe({
      next: () => {
        this.toast.success('Chave do Gemini salva com sucesso!');
        this.geminiNovaChave = '';
        this.carregarGeminiConfig();
      },
      error: (err) => this.toast.error(err.error?.message || 'Erro ao salvar chave.'),
      complete: () => this.geminiSalvando = false
    });
  }

  removerGemini(): void {
    this.http.delete(`${environment.apiUrl}/ocr/config`).subscribe({
      next: () => {
        this.toast.success('Chave removida.');
        this.carregarGeminiConfig();
      },
      error: (err) => this.toast.error(err.error?.message || 'Erro ao remover chave.')
    });
  }

  mlConectado = false;
  mlConta: MercadoLivreContaInfo | null = null;
  mlLoading = false;
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
  gmTestando = false;
  gmTesteResultado: TestIntegracaoResult | null = null;

  ngOnInit(): void {
    this.feedFacebookUrl = this.integracaoService.getFacebookFeedUrl();
    this.feedGoogleUrl = this.integracaoService.getGoogleFeedUrl();
    if (isPlatformBrowser(this.platformId)) {
      this.webhookUrl = `${window.location.origin}/api/integracoes/whatsapp/webhook`;
    }
    this.checkMercadoLivreStatus();
    this.checkWhatsAppStatus();
    this.checkSmtpStatus();
    this.checkFacebookStatus();
    this.checkGoogleStatus();
    this.carregarGeminiConfig();
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
      error: () => this.toast.error('Erro ao obter URL de autorizacao do Mercado Livre.')
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

  abrirConfigWhatsApp(): void {
    this.waAbaTutorial = !this.waConfig.configurado;
    this.waMostrarToken = false;
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
        this.toast.success('Configuracao SMTP salva.');
        this.smtpSalvando = false;
        this.showSmtpConfigModal = false;
        this.checkSmtpStatus();
      },
      error: () => { this.toast.error('Erro ao salvar SMTP.'); this.smtpSalvando = false; }
    });
  }

  testarSmtp(): void {
    if (!this.smtpEmailTeste || !this.smtpEmailTeste.includes('@')) {
      this.toast.warning('Informe um e-mail valido para o teste.');
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
    this.gmForm.reset({
      clientId: this.gmConfig.clientId || '',
      clientSecret: '',
      refreshToken: '',
      merchantId: this.gmConfig.merchantId || ''
    });
    this.showGmConfigModal = true;
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
