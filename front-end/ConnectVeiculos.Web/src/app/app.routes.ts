import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./pages/landing/landing.component').then(m => m.LandingComponent)
  },
  {
    path: 'catalogo',
    loadComponent: () => import('./pages/catalogo/catalogo.component').then(m => m.CatalogoComponent)
  },
  {
    path: 'catalogo/:tenantSlug',
    loadComponent: () => import('./pages/catalogo/catalogo.component').then(m => m.CatalogoComponent)
  },
  {
    path: 'catalogo/:tenantSlug/veiculo/:veiculoId',
    loadComponent: () => import('./pages/catalogo/catalogo.component').then(m => m.CatalogoComponent)
  },
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'registro',
    loadComponent: () => import('./pages/registro/registro.component').then(m => m.RegistroComponent)
  },
  {
    path: 'recuperar-senha',
    loadComponent: () => import('./pages/recuperar-senha/recuperar-senha.component').then(m => m.RecuperarSenhaComponent)
  },
  {
    path: 'redefinir-senha',
    loadComponent: () => import('./pages/redefinir-senha/redefinir-senha.component').then(m => m.RedefinirSenhaComponent)
  },
  {
    path: 'acesso-negado',
    loadComponent: () => import('./pages/acesso-negado/acesso-negado.component').then(m => m.AcessoNegadoComponent)
  },
  {
    path: 'privacidade',
    loadComponent: () => import('./pages/legal/privacidade/privacidade.component').then(m => m.PrivacidadeComponent)
  },
  {
    path: 'termos',
    loadComponent: () => import('./pages/legal/termos/termos.component').then(m => m.TermosComponent)
  },
  {
    path: 'exclusao-de-dados',
    loadComponent: () => import('./pages/legal/exclusao-de-dados/exclusao-de-dados.component').then(m => m.ExclusaoDeDadosComponent)
  },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./pages/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'usuarios',
        loadComponent: () => import('./pages/usuarios/usuarios.component').then(m => m.UsuariosComponent),
        canActivate: [roleGuard],
        data: { roles: ['Administrador'] }
      },
      {
        path: 'veiculos',
        loadComponent: () => import('./pages/veiculos/veiculos.component').then(m => m.VeiculosComponent)
      },
      {
        path: 'lojas',
        loadComponent: () => import('./pages/lojas/lojas.component').then(m => m.LojasComponent),
        canActivate: [roleGuard],
        data: { roles: ['Administrador', 'Gerente'] }
      },
      {
        path: 'acessos',
        loadComponent: () => import('./pages/acessos/acessos.component').then(m => m.AcessosComponent),
        canActivate: [roleGuard],
        data: { roles: ['Administrador'] }
      },
      {
        path: 'categorias',
        loadComponent: () => import('./pages/categorias/categorias.component').then(m => m.CategoriasComponent)
      },
      {
        path: 'vendas',
        loadComponent: () => import('./pages/vendas/vendas.component').then(m => m.VendasComponent)
      },
      // TODO: ativar quando tiver parceria com bancos (BV, Pan, etc)
      // {
      //   path: 'financiamentos',
      //   loadComponent: () => import('./pages/financiamentos/financiamentos.component').then(m => m.FinanciamentosComponent)
      // },
      {
        path: 'relatorios',
        loadComponent: () => import('./pages/relatorios/relatorios.component').then(m => m.RelatoriosComponent),
        canActivate: [roleGuard],
        data: { roles: ['Administrador', 'Gerente'] }
      },
      {
        path: 'test-drives',
        loadComponent: () => import('./pages/test-drives/test-drives.component').then(m => m.TestDrivesComponent)
      },
      {
        path: 'leads',
        loadComponent: () => import('./pages/leads/leads.component').then(m => m.LeadsComponent)
      },
      // Negociações desabilitada temporariamente — feature em desenvolvimento.
      // {
      //   path: 'negociacoes',
      //   loadComponent: () => import('./pages/negociacoes/negociacoes.component').then(m => m.NegociacoesComponent)
      // },
      {
        path: 'documentos',
        loadComponent: () => import('./pages/documentos-veiculo/documentos-veiculo.component').then(m => m.DocumentosVeiculoComponent)
      },
      {
        path: 'integracoes',
        loadComponent: () => import('./pages/integracoes/integracoes.component').then(m => m.IntegracoesComponent),
        canActivate: [roleGuard],
        data: { roles: ['Administrador', 'Gerente'] }
      },
       {
         path: 'plano',
         loadComponent: () => import('./pages/plano/plano.component').then(m => m.PlanoComponent),
         canActivate: [roleGuard],
         data: { roles: ['Administrador', 'Gerente'] }
       },
      {
        path: 'favoritos',
        loadComponent: () => import('./pages/favoritos-admin/favoritos-admin.component').then(m => m.FavoritosAdminComponent),
        canActivate: [roleGuard],
        data: { roles: ['Administrador', 'Gerente'] }
      },
      {
        path: 'logs',
        loadComponent: () => import('./pages/logs/logs.component').then(m => m.LogsComponent),
        canActivate: [roleGuard],
        data: { roles: ['Administrador'] }
      }
    ]
  },
  {
    path: '**',
    redirectTo: ''
  }
];
