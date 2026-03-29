import { inject } from '@angular/core';
import { Router, CanActivateFn, ActivatedRouteSnapshot } from '@angular/router';
import { AuthService } from '../services';

export const roleGuard: CanActivateFn = (route: ActivatedRouteSnapshot) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  // Primeiro verifica se está autenticado
  if (!authService.isAuthenticated()) {
    router.navigate(['/login']);
    return false;
  }

  // Obtém as roles permitidas da configuração da rota
  const allowedRoles = route.data['roles'] as string[] | undefined;

  // Se não há roles definidas, permite acesso (apenas autenticação necessária)
  if (!allowedRoles || allowedRoles.length === 0) {
    return true;
  }

  // Verifica se o usuário tem uma das roles permitidas
  const user = authService.getUser();
  if (user && user.usuFuncao && allowedRoles.includes(user.usuFuncao)) {
    return true;
  }

  // Redireciona para página de acesso negado
  router.navigate(['/acesso-negado']);
  return false;
};
