import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SwPush } from '@angular/service-worker';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class PushService {
  private http = inject(HttpClient);
  private swPush = inject(SwPush);
  private baseUrl = `${environment.apiUrl}/push`;

  isSupported(): boolean { return this.swPush.isEnabled; }

  async getServerPublicKey(): Promise<string | null> {
    try {
      const r = await firstValueFrom(this.http.get<{ publicKey: string }>(`${this.baseUrl}/public-key`));
      return r?.publicKey || null;
    } catch { return null; }
  }

  async subscribeToServer(): Promise<boolean> {
    if (!this.swPush.isEnabled) return false;

    const publicKey = await this.getServerPublicKey();
    if (!publicKey) return false;

    try {
      const sub = await this.swPush.requestSubscription({ serverPublicKey: publicKey });
      const json = sub.toJSON() as any;
      await firstValueFrom(this.http.post(`${this.baseUrl}/subscribe`, {
        endpoint: json.endpoint,
        keys: { p256dh: json.keys.p256dh, auth: json.keys.auth },
        userAgent: navigator.userAgent
      }));
      return true;
    } catch (e) {
      console.error('Erro ao registrar push subscription', e);
      return false;
    }
  }

  observeMessages() { return this.swPush.messages; }
  observeClicks()   { return this.swPush.notificationClicks; }
}
