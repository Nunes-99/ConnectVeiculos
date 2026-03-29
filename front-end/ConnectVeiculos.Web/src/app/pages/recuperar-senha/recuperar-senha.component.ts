import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services';

@Component({
  selector: 'app-recuperar-senha',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './recuperar-senha.component.html',
  styleUrl: './recuperar-senha.component.scss'
})
export class RecuperarSenhaComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  email = '';
  loading = false;
  mensagem = '';
  erro = '';
  enviado = false;

  solicitar(): void {
    if (!this.email) {
      this.erro = 'Informe seu e-mail';
      return;
    }

    this.loading = true;
    this.erro = '';
    this.mensagem = '';

    this.authService.solicitarRecuperacaoSenha(this.email).subscribe({
      next: (response) => {
        this.loading = false;
        this.enviado = true;
        this.mensagem = response.mensagem;
        // Em ambiente de desenvolvimento, mostramos o token
        // Em producao, o token seria enviado por email
        if (response.token) {
          this.mensagem += ` Token para teste: ${response.token.substring(0, 20)}...`;
        }
      },
      error: (err) => {
        this.loading = false;
        this.erro = err.error?.message || err.error || 'Erro ao solicitar recuperacao de senha';
      }
    });
  }
}
