import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Publicacao {
  pubId: number;
  pubPlataforma: string;
  pubExternoId: string;
  pubStatus: string;
  pubUrl: string;
  pubDtPublicacao: string;
  pubDtRemocao: string;
}

export interface MercadoLivreContaInfo {
  nickname: string;
  email: string;
  userId: string;
  pais?: string;
  urlPerfil?: string;
}

export interface WhatsAppConfigInfo {
  configurado: boolean;
  phoneId?: string;
  verifyTokenDefinido: boolean;
}

export interface EmailConfigInfo {
  configurado: boolean;
  smtpServer?: string;
  smtpPort: number;
  senderEmail?: string;
  senderName?: string;
  username?: string;
  enableSsl: boolean;
}

export interface SmtpConfigInput {
  smtpServer: string;
  smtpPort: number;
  username: string;
  password: string;
  senderEmail: string;
  senderName: string;
  enableSsl: boolean;
}

export interface FacebookConfigInfo {
  configurado: boolean;
  catalogId?: string;
  apiVersion?: string;
  tokenDefinido: boolean;
}

export interface FacebookConfigInput {
  accessToken: string;
  catalogId: string;
  apiVersion: string;
}

export interface GoogleMerchantConfigInfo {
  configurado: boolean;
  merchantId?: string;
  clientId?: string;
  clientSecretDefinido: boolean;
  refreshTokenDefinido: boolean;
}

export interface GoogleMerchantConfigInput {
  clientId: string;
  clientSecret: string;
  refreshToken: string;
  merchantId: string;
}

export interface TestIntegracaoResult {
  sucesso: boolean;
  mensagem?: string;
}

@Injectable({
  providedIn: 'root'
})
export class IntegracaoService {
  private http = inject(HttpClient);
  private baseUrl = environment.apiUrl;

  // Mercado Livre
  getMercadoLivreAuthUrl(): Observable<{ url: string }> {
    return this.http.get<{ url: string }>(`${this.baseUrl}/integracoes/mercadolivre/auth-url`);
  }

  getMercadoLivreStatus(): Observable<{ conectado: boolean }> {
    return this.http.get<{ conectado: boolean }>(`${this.baseUrl}/integracoes/mercadolivre/status`);
  }

  getMercadoLivreInfo(): Observable<{ conectado: boolean; info?: MercadoLivreContaInfo }> {
    return this.http.get<{ conectado: boolean; info?: MercadoLivreContaInfo }>(`${this.baseUrl}/integracoes/mercadolivre/info`);
  }

  desconectarMercadoLivre(): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/mercadolivre/desconectar`, {});
  }

  // WhatsApp
  getWhatsAppStatus(): Observable<{ configurado: boolean }> {
    return this.http.get<{ configurado: boolean }>(`${this.baseUrl}/integracoes/whatsapp/status`);
  }

  getWhatsAppConfig(): Observable<WhatsAppConfigInfo> {
    return this.http.get<WhatsAppConfigInfo>(`${this.baseUrl}/integracoes/whatsapp/config`);
  }

  saveWhatsAppConfig(data: { accessToken: string; phoneId: string; verifyToken: string }): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/whatsapp/config`, data);
  }

  desconectarWhatsApp(): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/whatsapp/desconectar`, {});
  }

  enviarWhatsApp(data: { telefone: string; mensagem: string }): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/whatsapp/enviar`, data);
  }

  // SMTP / E-mail
  getSmtpConfig(): Observable<EmailConfigInfo> {
    return this.http.get<EmailConfigInfo>(`${this.baseUrl}/integracoes/smtp/config`);
  }

  saveSmtpConfig(data: SmtpConfigInput): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/smtp/config`, data);
  }

  desconectarSmtp(): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/smtp/desconectar`, {});
  }

  testarSmtp(destinatario: string): Observable<{ sucesso: boolean; mensagem: string }> {
    return this.http.post<{ sucesso: boolean; mensagem: string }>(`${this.baseUrl}/integracoes/smtp/test`, { destinatario });
  }

  publicarMercadoLivre(veiculoId: number): Observable<any> {
    return this.http.post(`${this.baseUrl}/integracoes/mercadolivre/publicar/${veiculoId}`, {});
  }

  removerMercadoLivre(veiculoId: number): Observable<any> {
    return this.http.delete(`${this.baseUrl}/integracoes/mercadolivre/remover/${veiculoId}`);
  }

  // Publicacoes
  getPublicacoes(veiculoId: number): Observable<Publicacao[]> {
    return this.http.get<Publicacao[]>(`${this.baseUrl}/integracoes/publicacoes/${veiculoId}`);
  }

  // Facebook Catalog
  getFacebookConfig(): Observable<FacebookConfigInfo> {
    return this.http.get<FacebookConfigInfo>(`${this.baseUrl}/integracoes/facebook/config`);
  }

  saveFacebookConfig(data: FacebookConfigInput): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/facebook/config`, data);
  }

  desconectarFacebook(): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/facebook/desconectar`, {});
  }

  testarFacebook(): Observable<TestIntegracaoResult> {
    return this.http.post<TestIntegracaoResult>(`${this.baseUrl}/integracoes/facebook/test`, {});
  }

  // Google Merchant
  getGoogleConfig(): Observable<GoogleMerchantConfigInfo> {
    return this.http.get<GoogleMerchantConfigInfo>(`${this.baseUrl}/integracoes/google/config`);
  }

  saveGoogleConfig(data: GoogleMerchantConfigInput): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/google/config`, data);
  }

  desconectarGoogle(): Observable<{ mensagem: string }> {
    return this.http.post<{ mensagem: string }>(`${this.baseUrl}/integracoes/google/desconectar`, {});
  }

  testarGoogle(): Observable<TestIntegracaoResult> {
    return this.http.post<TestIntegracaoResult>(`${this.baseUrl}/integracoes/google/test`, {});
  }

  // Feeds
  getFacebookFeedUrl(): string {
    return `${this.baseUrl}/feed/facebook`;
  }

  getGoogleFeedUrl(): string {
    return `${this.baseUrl}/feed/google`;
  }
}
