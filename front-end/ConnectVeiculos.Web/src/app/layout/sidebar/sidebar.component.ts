import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService, ThemeService, LojaService } from '../../core/services';
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
  imports: [CommonModule, RouterModule, NotificationsComponent],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  authService = inject(AuthService);
  themeService = inject(ThemeService);
  private lojaService = inject(LojaService);

  lojas: Loja[] = [];
  linkCopiado = false;

  menuGroups = [
    {
      title: 'Principal',
      items: [
        { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
        { label: 'Veículos', icon: 'directions_car', route: '/veiculos' },
        { label: 'Vendas', icon: 'point_of_sale', route: '/vendas' },
        { label: 'Relatórios', icon: 'assessment', route: '/relatorios' },
      ]
    },
    {
      title: 'Gestão',
      items: [
        { label: 'Usuários', icon: 'people', route: '/usuarios' },
        { label: 'Lojas', icon: 'store', route: '/lojas' },
        { label: 'Categorias', icon: 'category', route: '/categorias' },
        { label: 'Acessos', icon: 'security', route: '/acessos' },
      ]
    },
    {
      title: 'CRM',
      items: [
        { label: 'Leads', icon: 'people_outline', route: '/leads' },
        { label: 'Test Drives', icon: 'event', route: '/test-drives' },
        { label: 'Favoritos', icon: 'favorite', route: '/favoritos' },
      ]
    },
    {
      title: 'Sistema',
      items: [
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

  abrirCatalogo(loja?: Loja): void {
    const param = loja?.lojSlug || loja?.lojId;
    const url = param ? `/catalogo/${param}` : '/catalogo';
    window.open(url, '_blank');
  }

  copiarLinkCatalogo(loja?: Loja): void {
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
}
