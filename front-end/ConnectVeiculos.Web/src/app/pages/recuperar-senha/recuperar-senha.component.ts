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
        // O token nunca aparece aqui: ele chega por e-mail e so. A API
        // devolvia "pra teste", entao bastava digitar o e-mail de outra
        // pessoa pra conseguir trocar a senha dela.
        this.mensagem = response.mensagem;
      },
      error: (err) => {
        this.loading = false;
        this.erro = err.error?.message || err.error || 'Erro ao solicitar recuperacao de senha';
      }
    });
  }
}
