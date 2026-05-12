import { Component, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services';

@Component({
  selector: 'app-registro',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './registro.component.html',
  styleUrl: './registro.component.scss'
})
export class RegistroComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private slideInterval: any;

  form: FormGroup;
  loading = false;
  errorMessage = '';
  successMessage = '';
  mostrarSenha = false;
  mostrarConfirmacao = false;

  currentSlide = 0;
  slides = [
    {
      img: 'login/slide1.jpg',
      title: 'Gerencie seu estoque com facilidade',
      desc: 'Controle total dos seus veículos, desde a entrada até a venda.'
    },
    {
      img: 'login/slide2.jpg',
      title: 'Catálogo online para seus clientes',
      desc: 'Seus veículos disponíveis 24h com atualização em tempo real.'
    },
    {
      img: 'login/slide3.jpg',
      title: 'Relatórios e indicadores',
      desc: 'Acompanhe vendas, leads e o desempenho da sua loja.'
    },
    {
      img: 'login/slide4.jpg',
      title: 'Múltiplas lojas, um só sistema',
      desc: 'Gerencie todas as suas unidades de forma centralizada.'
    },
    {
      img: 'login/slide5.jpg',
      title: 'Conecte-se aos seus clientes',
      desc: 'WhatsApp, test drive e leads integrados automaticamente.'
    }
  ];

  constructor() {
    this.form = this.fb.group(
      {
        nomeEmpresa: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(120)]],
        nomeAdmin: ['', [Validators.maxLength(120)]],
        email: ['', [Validators.required, Validators.email]],
        senha: ['', [Validators.required, Validators.minLength(6)]],
        confirmacaoSenha: ['', [Validators.required]]
      },
      { validators: this.senhasIguais }
    );
  }

  ngOnInit(): void {
    this.slideInterval = setInterval(() => this.nextSlide(), 5000);
  }

  ngOnDestroy(): void {
    clearInterval(this.slideInterval);
  }

  nextSlide(): void {
    this.currentSlide = (this.currentSlide + 1) % this.slides.length;
  }

  prevSlide(): void {
    this.currentSlide = (this.currentSlide - 1 + this.slides.length) % this.slides.length;
  }

  goToSlide(index: number): void {
    this.currentSlide = index;
  }

  private senhasIguais(group: AbstractControl): ValidationErrors | null {
    const senha = group.get('senha')?.value;
    const conf = group.get('confirmacaoSenha')?.value;
    return senha && conf && senha !== conf ? { senhasDiferentes: true } : null;
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.authService.registrar(this.form.value).subscribe({
      next: (resp) => {
        this.successMessage = `Conta criada! Sua base: "${resp.tenantSlug}". Entrando...`;
        setTimeout(() => this.router.navigate(['/dashboard']), 800);
      },
      error: (error) => {
        this.loading = false;
        this.errorMessage = error.error?.message || error.message || 'Erro ao criar conta.';
      }
    });
  }
}
