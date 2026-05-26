import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SeoService } from '../../../core/services/seo.service';

@Component({
  selector: 'app-termos',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './termos.component.html',
  styleUrls: ['../legal-page.scss']
})
export class TermosComponent implements OnInit {
  private seoService = inject(SeoService);
  ano = new Date().getFullYear();

  ngOnInit(): void {
    this.seoService.setMeta({
      title: 'Termos de Serviço — ConnectVeiculos',
      description: 'Termos e condições de uso da plataforma SaaS ConnectVeiculos.'
    });
  }
}
