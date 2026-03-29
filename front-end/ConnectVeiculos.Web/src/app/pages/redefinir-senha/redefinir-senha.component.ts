import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/services';

@Component({
  selector: 'app-redefinir-senha',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './redefinir-senha.component.html',
  styleUrl: './redefinir-senha.component.scss'
})
export class RedefinirSenhaComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  token = '';
  novaSenha = '';
  confirmarSenha = '';
  loading = false;
  mensagem = '';
  erro = '';
  sucesso = false;

  ngOnInit(): void {
    // Pegar token da URL se existir
    this.route.queryParams.subscribe(params => {
      if (params['token']) {
        this.token = params['token'];
      }
    });
  }

  redefinir(): void {
    this.erro = '';

    if (!this.token) {
      this.erro = 'Token e obrigatorio';
      return;
    }

    if (!this.novaSenha || this.novaSenha.length < 6) {
      this.erro = 'A senha deve ter no minimo 6 caracteres';
      return;
    }

    if (this.novaSenha !== this.confirmarSenha) {
      this.erro = 'As senhas nao conferem';
      return;
    }

    this.loading = true;

    this.authService.redefinirSenha(this.token, this.novaSenha, this.confirmarSenha).subscribe({
      next: (response) => {
        this.loading = false;
        this.sucesso = true;
        this.mensagem = response.mensagem;
        // Redirecionar para login apos 3 segundos
        setTimeout(() => {
          this.router.navigate(['/login']);
        }, 3000);
      },
      error: (err) => {
        this.loading = false;
        this.erro = err.error?.message || err.error || 'Erro ao redefinir senha';
      }
    });
  }
}
