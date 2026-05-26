import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { SeoService } from '../../../core/services/seo.service';

@Component({
  selector: 'app-exclusao-de-dados',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './exclusao-de-dados.component.html',
  styleUrls: ['../legal-page.scss', './exclusao-de-dados.component.scss']
})
export class ExclusaoDeDadosComponent implements OnInit {
  private seoService = inject(SeoService);
  ano = new Date().getFullYear();

  // Estado do formulario opcional (gera mailto pre-preenchido em vez de
  // enviar pelo backend — mantem a pagina 100% estatica e auditavel.)
  nome = signal('');
  email = signal('');
  motivo = signal('Solicitação de exclusão completa dos meus dados pessoais.');
  copiado = signal(false);

  ngOnInit(): void {
    this.seoService.setMeta({
      title: 'Exclusão de Dados — ConnectVeiculos',
      description: 'Como solicitar a exclusão de seus dados pessoais da plataforma ConnectVeiculos conforme LGPD e diretrizes da Meta.'
    });
  }

  abrirEmail(): void {
    const subject = encodeURIComponent('Solicitação de Exclusão de Dados — ConnectVeiculos');
    const body = encodeURIComponent(
      `Nome: ${this.nome() || '[seu nome]'}\n` +
      `E-mail cadastrado: ${this.email() || '[seu e-mail]'}\n\n` +
      `Motivo: ${this.motivo()}\n\n` +
      `Solicito a exclusão dos meus dados pessoais conforme art. 18 da LGPD ` +
      `e diretrizes de plataformas (Meta, Google).\n\n` +
      `Aguardo confirmação da exclusão por escrito.`
    );
    const mailto = `mailto:desenvolvimento@acsn.com.br?subject=${subject}&body=${body}`;
    window.location.href = mailto;
  }

  copiarEmail(): void {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText('desenvolvimento@acsn.com.br').then(() => {
        this.copiado.set(true);
        setTimeout(() => this.copiado.set(false), 2500);
      });
    }
  }
}
