import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { ApiService } from './api.service';
import { Usuario, LoginResponse } from '../models';

@Injectable({
  providedIn: 'root'
})
export class AuthService extends ApiService {
  private readonly USER_STORAGE_KEY = 'connectveiculos_user';
  private readonly TOKEN_STORAGE_KEY = 'connectveiculos_token';

  currentUser = signal<Usuario | null>(null);
  isAuthenticated = signal<boolean>(false);

  constructor(http: HttpClient, private router: Router) {
    super(http);
    this.loadUserFromStorage();
  }

  private loadUserFromStorage(): void {
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
        localStorage.setItem(this.USER_STORAGE_KEY, JSON.stringify(user));
        localStorage.setItem(this.TOKEN_STORAGE_KEY, response.token);
      })
    );
  }

  logout(): void {
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    localStorage.removeItem(this.USER_STORAGE_KEY);
    localStorage.removeItem(this.TOKEN_STORAGE_KEY);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_STORAGE_KEY);
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
}
