import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-acesso-negado',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="access-denied-container">
      <div class="access-denied-content">
        <div class="icon-container">
          <i class="bi bi-shield-exclamation"></i>
        </div>
        <h1>Acesso Negado</h1>
        <p>Você não tem permissão para acessar esta página.</p>
        <p class="text-muted">Entre em contato com o administrador do sistema se acredita que deveria ter acesso.</p>
        <div class="actions">
          <a routerLink="/dashboard" class="btn btn-primary">
            <i class="bi bi-house-door me-2"></i>
            Voltar ao Dashboard
          </a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .access-denied-container {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }

    .access-denied-content {
      text-align: center;
      background: white;
      padding: 3rem;
      border-radius: 1rem;
      box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
      max-width: 450px;
      margin: 1rem;
    }

    .icon-container {
      margin-bottom: 1.5rem;
    }

    .icon-container i {
      font-size: 5rem;
      color: #dc3545;
    }

    h1 {
      color: #333;
      margin-bottom: 1rem;
      font-weight: 600;
    }

    p {
      color: #666;
      margin-bottom: 0.5rem;
    }

    .text-muted {
      font-size: 0.9rem;
      margin-bottom: 2rem;
    }

    .actions {
      margin-top: 1.5rem;
    }

    .btn-primary {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      border: none;
      padding: 0.75rem 2rem;
      font-size: 1rem;
      border-radius: 0.5rem;
      transition: transform 0.2s, box-shadow 0.2s;
    }

    .btn-primary:hover {
      transform: translateY(-2px);
      box-shadow: 0 5px 20px rgba(102, 126, 234, 0.4);
    }
  `]
})
export class AcessoNegadoComponent {}
