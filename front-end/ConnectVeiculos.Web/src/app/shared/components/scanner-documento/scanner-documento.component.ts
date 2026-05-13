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

  limiteAtingido = false;
  limiteMensagem: string | null = null;
  limiteProximoReset: string | null = null;

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
      if (err?.status === 429 || err?.error?.codigo === 'limite_diario') {
        this.limiteAtingido = true;
        this.limiteMensagem = err?.error?.message || 'Limite diario de scans atingido. Por favor, preencha os dados manualmente.';
        this.limiteProximoReset = err?.error?.proximoResetBr || err?.error?.proximoResetUtc || null;
        this.fotoPreview = null;
        this.erro = null;
        this.pararQr();
      } else {
        this.erro = err?.error?.message || err?.message || 'Falha ao processar a imagem.';
      }
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

    // 1) Tenta parsear como JSON estruturado (alguns sistemas exportam assim)
    try {
      const parsed = JSON.parse(texto);
      this.resultado = { ...parsed, confianca: 'ALTA' };
      return;
    } catch { /* nao e JSON */ }

    // 2) URL HTTP — link de validacao no SERPRO
    if (/^https?:\/\//i.test(texto)) {
      this.resultado = {
        qrUrl: texto,
        confianca: 'ALTA',
        aviso: 'QR contém apenas link de verificação no SERPRO. Use a aba "Foto" para extrair os dados do veículo.'
      };
      return;
    }

    // 3) Payload binário/assinado (caso típico do QR GRANDE do CRLV-e)
    //    Detectado por presença de bytes não-imprimíveis ou caractere de substituição
    const bytesInvalidos = /[\x00-\x08\x0e-\x1f�]/.test(texto);
    if (bytesInvalidos || texto.length > 500) {
      this.resultado = {
        confianca: 'ALTA',
        aviso: 'QR detectado, mas é o QR grande do CRLV-e (contém assinatura digital binária do SERPRO, não legível como texto). Volte na aba "Foto" e tire uma foto do CRLV — a IA extrai placa, chassi, marca e modelo automaticamente.'
      };
      return;
    }

    // 4) Texto curto/legível mas desconhecido — pode ser identificador interno
    this.resultado = {
      qrUrl: texto,
      confianca: 'BAIXA',
      aviso: 'QR detectado mas formato não reconhecido. Use a aba "Foto" para extrair os dados do CRLV.'
    };
  }

  temDadosUteis(): boolean {
    if (!this.resultado) return false;
    return !!(this.resultado.placa || this.resultado.chassi || this.resultado.marca
           || this.resultado.modelo || this.resultado.anoFabricacao || this.resultado.anoModelo
           || this.resultado.cor || this.resultado.proprietarioNome);
  }

  usarResultado(): void {
    if (this.resultado && this.temDadosUteis()) {
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
    this.limiteAtingido = false;
    this.limiteMensagem = null;
    this.limiteProximoReset = null;
  }

  formatarReset(): string {
    if (!this.limiteProximoReset) return '';
    try {
      const d = new Date(this.limiteProximoReset);
      return d.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
    } catch {
      return this.limiteProximoReset;
    }
  }
}
