import { Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Html5Qrcode, Html5QrcodeSupportedFormats } from 'html5-qrcode';
import { environment } from '../../../../environments/environment';

export interface CrlvExtraido {
  placa?: string | null;
  renavam?: string | null;
  chassi?: string | null;
  marca?: string | null;
  modelo?: string | null;
  anoFabricacao?: number | null;
  anoModelo?: number | null;
  cor?: string | null;
  combustivel?: string | null;
  proprietarioNome?: string | null;
  proprietarioDoc?: string | null;
  categoria?: string | null;
  especie?: string | null;
  confianca?: string | null;
  aviso?: string | null;
  qrUrl?: string | null;
  fotoBase64?: string | null;
}

type Aba = 'foto' | 'arquivo' | 'qr';

@Component({
  selector: 'app-scanner-documento',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './scanner-documento.component.html',
  styleUrl: './scanner-documento.component.scss'
})
export class ScannerDocumentoComponent implements OnDestroy {
  @Input() show = false;
  @Output() fechar = new EventEmitter<void>();
  @Output() extraido = new EventEmitter<CrlvExtraido>();

  @ViewChild('qrReader') qrReader?: ElementRef<HTMLDivElement>;
  @ViewChild('camInput') camInput?: ElementRef<HTMLInputElement>;
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  private http = inject(HttpClient);
  private apiUrl = environment.apiUrl;

  aba: Aba = 'foto';
  loading = false;
  erro: string | null = null;
  resultado: CrlvExtraido | null = null;
  fotoPreview: string | null = null;
  qrScanner: Html5Qrcode | null = null;
  qrAtivo = false;

  setAba(a: Aba): void {
    if (this.aba === a) return;
    this.pararQr();
    this.aba = a;
    this.erro = null;
    if (a === 'qr') {
      setTimeout(() => this.iniciarQr(), 100);
    }
  }

  ngOnDestroy(): void {
    this.pararQr();
  }

  abrirCamera(): void { this.camInput?.nativeElement.click(); }
  abrirArquivo(): void { this.fileInput?.nativeElement.click(); }

  onArquivoSelecionado(ev: Event): void {
    const input = ev.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.processarArquivo(file);
    input.value = '';
  }

  private async processarArquivo(file: File): Promise<void> {
    this.erro = null;
    this.resultado = null;
    this.loading = true;
    try {
      const base64 = await this.fileToBase64(file);
      this.fotoPreview = base64;
      const resp = await this.http.post<CrlvExtraido>(`${this.apiUrl}/ocr/crlv`, { imagemBase64: base64 }).toPromise();
      if (resp) {
        resp.fotoBase64 = base64;
        this.resultado = resp;
      }
    } catch (err: any) {
      this.erro = err?.error?.message || err?.message || 'Falha ao processar a imagem.';
    } finally {
      this.loading = false;
    }
  }

  private fileToBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as string);
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
  }

  private async iniciarQr(): Promise<void> {
    if (!this.qrReader) return;
    try {
      this.qrScanner = new Html5Qrcode(this.qrReader.nativeElement.id, {
        formatsToSupport: [Html5QrcodeSupportedFormats.QR_CODE],
        verbose: false
      });
      this.qrAtivo = true;
      await this.qrScanner.start(
        { facingMode: 'environment' },
        { fps: 10, qrbox: { width: 250, height: 250 } },
        (texto) => this.onQrDetectado(texto),
        () => { /* ignore decode failures (acontece a cada frame sem match) */ }
      );
    } catch (e: any) {
      this.erro = 'Nao foi possivel abrir a camera: ' + (e?.message || e);
      this.qrAtivo = false;
    }
  }

  private async pararQr(): Promise<void> {
    if (this.qrScanner && this.qrAtivo) {
      try { await this.qrScanner.stop(); } catch { /* ignore */ }
      try { this.qrScanner.clear(); } catch { /* ignore */ }
    }
    this.qrAtivo = false;
    this.qrScanner = null;
  }

  private async onQrDetectado(texto: string): Promise<void> {
    await this.pararQr();
    this.erro = null;

    // QR do CRLV-e geralmente e uma URL para validacao no SERPRO.
    // Tentamos parsear como JSON estruturado primeiro (alguns sistemas exportam assim).
    try {
      const parsed = JSON.parse(texto);
      this.resultado = { ...parsed, confianca: 'ALTA' };
      return;
    } catch { /* nao e JSON */ }

    // URL → so guarda como verificacao e avisa que precisa OCR pra extrair campos
    this.resultado = {
      qrUrl: texto,
      confianca: 'ALTA',
      aviso: 'QR contem apenas link de verificacao no SERPRO. Use a aba "Foto" para extrair os dados do veiculo.'
    };
  }

  usarResultado(): void {
    if (this.resultado) {
      this.extraido.emit(this.resultado);
      this.fechar.emit();
      this.reset();
    }
  }

  cancelar(): void {
    this.pararQr();
    this.reset();
    this.fechar.emit();
  }

  private reset(): void {
    this.resultado = null;
    this.fotoPreview = null;
    this.erro = null;
    this.aba = 'foto';
  }
}
