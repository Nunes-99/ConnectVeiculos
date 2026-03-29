import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'light' | 'dark';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly STORAGE_KEY = 'theme';

  // Signal para o tema atual
  theme = signal<Theme>(this.getInitialTheme());

  // Computed para saber se esta no dark mode
  isDarkMode = () => this.theme() === 'dark';

  constructor() {
    // Efeito para aplicar o tema quando mudar
    effect(() => {
      this.applyTheme(this.theme());
    });
  }

  private getInitialTheme(): Theme {
    // Verificar preferencia salva
    const savedTheme = localStorage.getItem(this.STORAGE_KEY) as Theme;
    if (savedTheme) {
      return savedTheme;
    }

    // Verificar preferencia do sistema
    if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {
      return 'dark';
    }

    return 'light';
  }

  private applyTheme(theme: Theme): void {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem(this.STORAGE_KEY, theme);
  }

  toggleTheme(): void {
    this.theme.set(this.theme() === 'light' ? 'dark' : 'light');
  }

  setTheme(theme: Theme): void {
    this.theme.set(theme);
  }
}
