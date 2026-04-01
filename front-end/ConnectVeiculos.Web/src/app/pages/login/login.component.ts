import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private authService = inject(AuthService);
  private router = inject(Router);
  private slideInterval: any;

  loginForm: FormGroup;
  loading = false;
  errorMessage = '';

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
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      senha: ['', [Validators.required, Validators.minLength(4)]]
    });
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

  onSubmit(): void {
    if (this.loginForm.invalid) {
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    const { email, senha } = this.loginForm.value;

    this.authService.login(email, senha).subscribe({
      next: () => {
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        this.loading = false;
        this.errorMessage = error.message || 'Erro ao realizar login';
      }
    });
  }
}
