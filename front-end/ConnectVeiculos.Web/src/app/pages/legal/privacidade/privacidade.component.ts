import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SeoService } from '../../../core/services/seo.service';

@Component({
  selector: 'app-privacidade',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './privacidade.component.html',
  styleUrls: ['../legal-page.scss']
})
export class PrivacidadeComponent implements OnInit {
  private seoService = inject(SeoService);
  ano = new Date().getFullYear();

  ngOnInit(): void {
    this.seoService.setMeta({
      title: 'Política de Privacidade — ConnectVeiculos',
      description: 'Como a ConnectVeiculos coleta, usa e protege seus dados pessoais conforme a LGPD.'
    });
  }
}
