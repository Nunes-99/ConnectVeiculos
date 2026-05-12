import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { Usuario, LoginResponse } from '../models';

export interface RegistrarResponse {
  tenantSlug: string;
  tenantNome: string;
  token: string;
  expiration: string;
  usuId: number;
  usuNome: string;
  usuEmail: string;
  usuFuncao: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService extends ApiService {
  private readonly USER_STORAGE_KEY = 'connectveiculos_user';
  private readonly TOKEN_STORAGE_KEY = 'connectveiculos_token';
  private readonly REFRESH_TOKEN_KEY = 'connectveiculos_refresh_token';
  private readonly TENANT_SLUG_KEY = 'connectveiculos_tenant_slug';
  private platformId = inject(PLATFORM_ID);

  currentUser = signal<Usuario | null>(null);
  isAuthenticated = signal<boolean>(false);

  constructor(http: HttpClient, private router: Router) {
    super(http);
    this.loadUserFromStorage();
  }

  private loadUserFromStorage(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const storedUser = localStorage.getItem(this.USER_STORAGE_KEY);
    const storedToken = localStorage.getItem(this.TOKEN_STORAGE_KEY);

    if (storedUser && storedToken) {
      const user = JSON.parse(storedUser) as Usuario;
      this.currentUser.set(user);
      this.isAuthenticated.set(true);
    }
  }

  login(email: string, senha: string): Observable<LoginResponse> {
    return this.post<LoginResponse>('auth/login', { email, senha }).pipe(
      tap(response => {
        const user: Usuario = {
          usuId: response.usuId,
          usuNome: response.usuNome,
          usuEmail: response.usuEmail,
          usuFuncao: response.usuFuncao,
          r_LojId: 0,
          r_AcsId: 0,
          lojaNome: '',
          acessoNome: '',
          usuCPF: '',
          usuRG: '',
          usuSts: true
        };

        this.currentUser.set(user);
        this.isAuthenticated.set(true);
        if (isPlatformBrowser(this.platformId)) {
          localStorage.setItem(this.USER_STORAGE_KEY, JSON.stringify(user));
          localStorage.setItem(this.TOKEN_STORAGE_KEY, response.token);
          if (response.refreshToken) {
            localStorage.setItem(this.REFRESH_TOKEN_KEY, response.refreshToken);
          }
          if (response.tenantSlug) {
            localStorage.setItem(this.TENANT_SLUG_KEY, response.tenantSlug);
          }
        }
      })
    );
  }

  refreshSession(): Observable<LoginResponse> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('Sem refresh token armazenado.');
    }
    return this.post<LoginResponse>('auth/refresh', { refreshToken }).pipe(
      tap(response => {
        if (isPlatformBrowser(this.platformId)) {
          localStorage.setItem(this.TOKEN_STORAGE_KEY, response.token);
          if (response.refreshToken) {
            localStorage.setItem(this.REFRESH_TOKEN_KEY, response.refreshToken);
          }
        }
      })
    );
  }

  getRefreshToken(): string | null {
    if (!isPlatformBrowser(this.platformId)) return null;
    return localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }

  registrar(input: { nomeEmpresa: string; email: string; senha: string; confirmacaoSenha: string; nomeAdmin?: string }): Observable<RegistrarResponse> {
    return this.post<RegistrarResponse>('auth/registrar', input).pipe(
      tap(response => {
        const user: Usuario = {
          usuId: response.usuId,
          usuNome: response.usuNome,
          usuEmail: response.usuEmail,
          usuFuncao: response.usuFuncao,
          r_LojId: 0,
          r_AcsId: 0,
          lojaNome: '',
          acessoNome: '',
          usuCPF: '',
          usuRG: '',
          usuSts: true
        };
        this.currentUser.set(user);
        this.isAuthenticated.set(true);
        if (isPlatformBrowser(this.platformId)) {
          localStorage.setItem(this.USER_STORAGE_KEY, JSON.stringify(user));
          localStorage.setItem(this.TOKEN_STORAGE_KEY, response.token);
          localStorage.setItem(this.TENANT_SLUG_KEY, response.tenantSlug);
        }
      })
    );
  }

  logout(): void {
    // Avisa o backend para revogar o refresh token (best-effort — nao bloqueia o logout).
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.post('auth/logout', { refreshToken }).subscribe({
        next: () => {},
        error: () => {} // ignore — logout local segue de qualquer jeito
      });
    }

    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    if (isPlatformBrowser(this.platformId)) {
      localStorage.removeItem(this.USER_STORAGE_KEY);
      localStorage.removeItem(this.TOKEN_STORAGE_KEY);
      localStorage.removeItem(this.REFRESH_TOKEN_KEY);
      // Preserva TENANT_SLUG_KEY de proposito: o navegador fica "pinado" no
      // tenant do usuario para que /login funcione mesmo apos logout.
    }
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    if (!isPlatformBrowser(this.platformId)) return null;
    return localStorage.getItem(this.TOKEN_STORAGE_KEY);
  }

  getTenantSlug(): string | null {
    if (!isPlatformBrowser(this.platformId)) return null;
    return localStorage.getItem(this.TENANT_SLUG_KEY);
  }

  getUser(): Usuario | null {
    return this.currentUser();
  }

  getUserLojaId(): number | null {
    const user = this.currentUser();
    return user ? user.r_LojId : null;
  }

  getUserRole(): string | null {
    const user = this.currentUser();
    return user ? user.usuFuncao : null;
  }

  hasRole(roles: string | string[]): boolean {
    const userRole = this.getUserRole();
    if (!userRole) return false;

    const rolesArray = Array.isArray(roles) ? roles : [roles];
    return rolesArray.includes(userRole);
  }

  isAdmin(): boolean {
    return this.hasRole('Administrador');
  }

  isGerente(): boolean {
    return this.hasRole(['Administrador', 'Gerente']);
  }

  isVendedor(): boolean {
    return this.hasRole(['Administrador', 'Gerente', 'Vendedor']);
  }

  solicitarRecuperacaoSenha(email: string): Observable<{ mensagem: string; token: string }> {
    return this.post<{ mensagem: string; token: string }>('auth/recuperar-senha', { email });
  }

  redefinirSenha(token: string, novaSenha: string, confirmarSenha: string): Observable<{ mensagem: string }> {
    return this.post<{ mensagem: string }>('auth/redefinir-senha', {
      token,
      novaSenha,
      confirmarSenha
    });
  }

  trocarSenha(senhaAtual: string, novaSenha: string, confirmarSenha: string): Observable<{ mensagem: string }> {
    return this.post<{ mensagem: string }>('auth/trocar-senha', {
      senhaAtual,
      novaSenha,
      confirmarSenha
    });
  }
}
