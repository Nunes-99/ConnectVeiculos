import { Component, EventEmitter, inject, Input, Output, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { AuthService, ThemeService, LojaService, ToastService } from '../../core/services';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { NotificationsComponent } from '../../shared/components/notifications/notifications.component';
import { ConfirmModalComponent } from '../../shared/components/confirm-modal/confirm-modal.component';
import { Loja } from '../../core/models';

interface MenuItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, NotificationsComponent, ConfirmModalComponent],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  authService = inject(AuthService);
  themeService = inject(ThemeService);
  private lojaService = inject(LojaService);
  private fb = inject(FormBuilder);
  private toast = inject(ToastService);

  @Input() mobileOpen = false;
  @Output() closeSidebar = new EventEmitter<void>();

  lojas: Loja[] = [];
  linkCopiado = false;
  catalogoExpanded = false;

  mostrarTrocarSenha = signal(false);
  salvandoSenha = signal(false);
  mostrarConfirmaTrocaEmpresa = signal(false);
  mostrarSenhaAtual = false;
  mostrarNovaSenha = false;
  mostrarConfirmarSenha = false;

  // Contagem de documentos vencendo nos proximos 30 dias (badge no menu)
  private http = inject(HttpClient);
  docsVencendo = signal(0);

  private carregarDocsVencendo(): void {
    if (!this.authService.isAuthenticated()) return;
    this.http.get<any[]>(`${environment.apiUrl}/veiculos-documentos/vencendo?diasAFrente=30`).subscribe({
      next: (docs) => this.docsVencendo.set(Array.isArray(docs) ? docs.length : 0),
      error: () => this.docsVencendo.set(0)
    });
  }

  trocarEmpresa(): void {
    this.mostrarConfirmaTrocaEmpresa.set(true);
  }

  confirmarTrocaEmpresa(): void {
    this.mostrarConfirmaTrocaEmpresa.set(false);
    // Limpa TODO o estado local — inclusive tenant slug (que normalmente
    // logout preserva). Usuario tem que escolher nova empresa no proximo login.
    if (typeof window !== 'undefined') {
      localStorage.removeItem('connectveiculos_user');
      localStorage.removeItem('connectveiculos_token');
      localStorage.removeItem('connectveiculos_refresh_token');
      localStorage.removeItem('connectveiculos_tenant_slug');
    }
    this.authService.currentUser.set(null);
    this.authService.isAuthenticated.set(false);
    window.location.href = '/login';
  }

  formSenha: FormGroup = this.fb.group({
    senhaAtual: ['', Validators.required],
    novaSenha: ['', [Validators.required, Validators.minLength(6)]],
    confirmarSenha: ['', Validators.required]
  }, { validators: this.senhasIguais });

  private senhasIguais(group: AbstractControl): ValidationErrors | null {
    const nova = group.get('novaSenha')?.value;
    const conf = group.get('confirmarSenha')?.value;
    if (!nova || !conf) return null;
    return nova === conf ? null : { mismatch: true };
  }

  menuGroups = [
    {
      title: 'Principal',
      items: [
        { label: 'Painel Geral', icon: 'dashboard', route: '/dashboard' },
        { label: 'Lojas', icon: 'store', route: '/lojas' },
        { label: 'Veículos', icon: 'directions_car', route: '/veiculos' },
        { label: 'Documentos', icon: 'description', route: '/documentos' },
        { label: 'Vendas', icon: 'point_of_sale', route: '/vendas' },
        // { label: 'Financiamento', icon: 'account_balance', route: '/financiamentos' }, // TODO: ativar quando tiver parceria com bancos (BV, Pan, etc)
      ]
    },
    {
      title: 'Gestão',
      items: [
        { label: 'Usuários', icon: 'people', route: '/usuarios' },
        { label: 'Categorias', icon: 'category', route: '/categorias' },
        { label: 'Relatórios', icon: 'assessment', route: '/relatorios' },
      ]
    },
    {
      title: 'Comercial',
      items: [
        { label: 'Captação de Clientes', icon: 'people_outline', route: '/leads' },
        // { label: 'Negociações', icon: 'handshake', route: '/negociacoes' }, // TODO: reabilitar quando feature estiver pronta
        { label: 'Test Drives', icon: 'event', route: '/test-drives' },
        { label: 'Favoritos', icon: 'favorite', route: '/favoritos' },
      ]
    },
    {
      title: 'Sistema',
      items: [
        { label: 'Integrações', icon: 'hub', route: '/integracoes' },
        { label: 'Logs', icon: 'history', route: '/logs' },
      ]
    }
  ];

  menuItems: MenuItem[] = this.menuGroups.flatMap(g => g.items);

  constructor() {
    this.lojaService.getAll().subscribe({
      next: (lojas) => this.lojas = lojas
    });
    this.carregarDocsVencendo();
  }

  private platformId = inject(PLATFORM_ID);

  abrirCatalogo(loja?: Loja): void {
    if (!isPlatformBrowser(this.platformId)) return;
    window.open(this.getUrlCatalogo(), '_blank');
  }

  copiarLinkCatalogo(loja?: Loja): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const baseUrl = window.location.origin;
    const url = `${baseUrl}${this.getUrlCatalogo()}`;
    navigator.clipboard.writeText(url).then(() => {
      this.linkCopiado = true;
      setTimeout(() => this.linkCopiado = false, 2000);
    });
  }

  private getUrlCatalogo(): string {
    const tenant = this.authService.getTenantSlug();
    return tenant ? `/catalogo/${tenant}` : '/catalogo';
  }

  getInitials(): string {
    const user = this.authService.currentUser();
    if (!user?.usuNome) return '?';
    return user.usuNome.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase();
  }

  logout(): void {
    this.authService.logout();
  }

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  abrirTrocarSenha(): void {
    this.formSenha.reset({ senhaAtual: '', novaSenha: '', confirmarSenha: '' });
    this.mostrarSenhaAtual = false;
    this.mostrarNovaSenha = false;
    this.mostrarConfirmarSenha = false;
    this.mostrarTrocarSenha.set(true);
  }

  fecharTrocarSenha(): void {
    if (this.salvandoSenha()) return;
    this.mostrarTrocarSenha.set(false);
    this.formSenha.reset({ senhaAtual: '', novaSenha: '', confirmarSenha: '' });
  }

  salvarTrocarSenha(): void {
    if (this.formSenha.invalid || this.salvandoSenha()) {
      this.formSenha.markAllAsTouched();
      return;
    }
    const { senhaAtual, novaSenha, confirmarSenha } = this.formSenha.value;
    this.salvandoSenha.set(true);
    this.authService.trocarSenha(senhaAtual, novaSenha, confirmarSenha).subscribe({
      next: (res) => {
        this.toast.success(res?.mensagem ?? 'Senha alterada. Será exigida no próximo login.');
        this.salvandoSenha.set(false);
        this.mostrarTrocarSenha.set(false);
        this.formSenha.reset({ senhaAtual: '', novaSenha: '', confirmarSenha: '' });
      },
      error: (err) => {
        const msg = err?.error?.message ?? err?.error?.mensagem ?? 'Não foi possível trocar a senha.';
        this.toast.error(typeof msg === 'string' ? msg : 'Não foi possível trocar a senha.');
        this.salvandoSenha.set(false);
      }
    });
  }

  hasErrorSenha(field: string, error: string): boolean {
    const ctrl = this.formSenha.get(field);
    return !!(ctrl && ctrl.hasError(error) && ctrl.touched);
  }
}
