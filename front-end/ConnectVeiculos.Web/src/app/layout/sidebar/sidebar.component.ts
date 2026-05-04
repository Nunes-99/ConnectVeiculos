import { Component, EventEmitter, inject, Input, Output, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { AuthService, ThemeService, LojaService, ToastService } from '../../core/services';
import { NotificationsComponent } from '../../shared/components/notifications/notifications.component';
import { Loja } from '../../core/models';

interface MenuItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterModule, NotificationsComponent],
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
        { label: 'Veículos', icon: 'directions_car', route: '/veiculos' },
        { label: 'Vendas', icon: 'point_of_sale', route: '/vendas' },
        { label: 'Documentos', icon: 'description', route: '/documentos' },
        // { label: 'Financiamento', icon: 'account_balance', route: '/financiamentos' }, // TODO: ativar quando tiver parceria com bancos (BV, Pan, etc)
        { label: 'Relatórios', icon: 'assessment', route: '/relatorios' },
      ]
    },
    {
      title: 'Gestão',
      items: [
        { label: 'Usuários', icon: 'people', route: '/usuarios' },
        { label: 'Lojas', icon: 'store', route: '/lojas' },
        { label: 'Categorias', icon: 'category', route: '/categorias' },
      ]
    },
    {
      title: 'Comercial',
      items: [
        { label: 'Captação de Clientes', icon: 'people_outline', route: '/leads' },
        { label: 'Negociações', icon: 'handshake', route: '/negociacoes' },
        { label: 'Test Drives', icon: 'event', route: '/test-drives' },
        { label: 'Favoritos', icon: 'favorite', route: '/favoritos' },
      ]
    },
    {
      title: 'Sistema',
      items: [
        { label: 'Integracoes', icon: 'hub', route: '/integracoes' },
        { label: 'Logs', icon: 'history', route: '/logs' },
      ]
    }
  ];

  menuItems: MenuItem[] = this.menuGroups.flatMap(g => g.items);

  constructor() {
    this.lojaService.getAll().subscribe({
      next: (lojas) => this.lojas = lojas
    });
  }

  private platformId = inject(PLATFORM_ID);

  abrirCatalogo(loja?: Loja): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const param = loja?.lojSlug || loja?.lojId;
    const url = param ? `/catalogo/${param}` : '/catalogo';
    window.open(url, '_blank');
  }

  copiarLinkCatalogo(loja?: Loja): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const baseUrl = window.location.origin;
    const param = loja?.lojSlug || loja?.lojId;
    const url = param ? `${baseUrl}/catalogo/${param}` : `${baseUrl}/catalogo`;
    navigator.clipboard.writeText(url).then(() => {
      this.linkCopiado = true;
      setTimeout(() => this.linkCopiado = false, 2000);
    });
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
        this.toast.success(res?.mensagem ?? 'Senha alterada. Sera exigida no proximo login.');
        this.salvandoSenha.set(false);
        this.mostrarTrocarSenha.set(false);
        this.formSenha.reset({ senhaAtual: '', novaSenha: '', confirmarSenha: '' });
      },
      error: (err) => {
        const msg = err?.error?.message ?? err?.error?.mensagem ?? 'Nao foi possivel trocar a senha.';
        this.toast.error(typeof msg === 'string' ? msg : 'Nao foi possivel trocar a senha.');
        this.salvandoSenha.set(false);
      }
    });
  }

  hasErrorSenha(field: string, error: string): boolean {
    const ctrl = this.formSenha.get(field);
    return !!(ctrl && ctrl.hasError(error) && ctrl.touched);
  }
}
