import { AfterViewInit, Component, ElementRef, EventEmitter, HostListener, OnDestroy, Output, ViewChild } from '@angular/core';

/**
 * Quadro para desenhar a rubrica com o dedo ou o mouse. Emite o PNG (data URL)
 * a cada traco, ou null quando limpo.
 */
@Component({
  selector: 'app-assinatura-pad',
  standalone: true,
  template: `
    <div class="pad">
      <canvas #canvas (pointerdown)="inicio($event)" (pointermove)="mover($event)" (pointerup)="fim()" (pointerleave)="fim()"></canvas>
      @if (vazio) { <span class="pad-dica">Assine aqui com o dedo ou o mouse</span> }
      <button type="button" class="pad-limpar" (click)="limpar()" [disabled]="vazio">
        <span class="material-icons">backspace</span> Limpar
      </button>
    </div>
  `,
  styles: [`
    .pad { position: relative; border: 1px dashed var(--border-secondary); border-radius: 8px; background: #fff; height: 160px; touch-action: none; }
    canvas { width: 100%; height: 100%; display: block; cursor: crosshair; border-radius: 8px; }
    .pad-dica { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; color: #9e9e9e; font-size: 0.9rem; pointer-events: none; }
    .pad-limpar { position: absolute; right: 6px; bottom: 6px; display: inline-flex; align-items: center; gap: 4px; background: rgba(255,255,255,.9);
      border: 1px solid #ddd; border-radius: 6px; padding: 2px 8px; font-size: 0.75rem; color: #555; cursor: pointer; }
    .pad-limpar .material-icons { font-size: 14px; }
    .pad-limpar:disabled { opacity: .4; cursor: default; }
  `]
})
export class AssinaturaPadComponent implements AfterViewInit, OnDestroy {
  @ViewChild('canvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;
  @Output() mudou = new EventEmitter<string | null>();

  vazio = true;
  private desenhando = false;
  private ctx: CanvasRenderingContext2D | null = null;
  private ultimo: { x: number; y: number } | null = null;

  ngAfterViewInit(): void {
    this.ajustar();
  }

  ngOnDestroy(): void {
    this.ctx = null;
  }

  /** O canvas acompanha o tamanho da tela (e limpa, para o traco nao distorcer). */
  @HostListener('window:resize')
  ajustar(): void {
    const c = this.canvasRef.nativeElement;
    const escala = window.devicePixelRatio || 1;
    c.width = c.clientWidth * escala;
    c.height = c.clientHeight * escala;
    this.ctx = c.getContext('2d');
    if (!this.ctx) return;
    this.ctx.scale(escala, escala);
    this.ctx.lineWidth = 2.2;
    this.ctx.lineCap = 'round';
    this.ctx.lineJoin = 'round';
    this.ctx.strokeStyle = '#111';
    if (!this.vazio) { this.vazio = true; this.mudou.emit(null); }
  }

  inicio(e: PointerEvent): void {
    if (!this.ctx) return;
    this.desenhando = true;
    this.canvasRef.nativeElement.setPointerCapture?.(e.pointerId);
    this.ultimo = this.ponto(e);
    this.ctx.beginPath();
    this.ctx.arc(this.ultimo.x, this.ultimo.y, 1, 0, Math.PI * 2);
    this.ctx.fillStyle = '#111';
    this.ctx.fill();
    this.vazio = false;
  }

  mover(e: PointerEvent): void {
    if (!this.desenhando || !this.ctx || !this.ultimo) return;
    const p = this.ponto(e);
    this.ctx.beginPath();
    this.ctx.moveTo(this.ultimo.x, this.ultimo.y);
    this.ctx.lineTo(p.x, p.y);
    this.ctx.stroke();
    this.ultimo = p;
  }

  fim(): void {
    if (!this.desenhando) return;
    this.desenhando = false;
    this.ultimo = null;
    this.mudou.emit(this.vazio ? null : this.canvasRef.nativeElement.toDataURL('image/png'));
  }

  limpar(): void {
    const c = this.canvasRef.nativeElement;
    this.ctx?.clearRect(0, 0, c.width, c.height);
    this.vazio = true;
    this.mudou.emit(null);
  }

  private ponto(e: PointerEvent): { x: number; y: number } {
    const r = this.canvasRef.nativeElement.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  }
}
