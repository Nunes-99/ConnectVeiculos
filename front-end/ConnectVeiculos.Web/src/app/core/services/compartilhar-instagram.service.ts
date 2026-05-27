import { Injectable, inject } from '@angular/core';
import { ImagemService } from './imagem.service';
import { Veiculo, Loja } from '../models';

export interface CompartilharVeiculoInput {
  veiculo: Veiculo;
  // Caminho da imagem principal (img.imgCaminho). Se ausente, tenta usar a
  // primeira de veiculo.imagens.
  imagemCaminho?: string;
  loja?: Loja | null;
  // URL do catalogo publico do veiculo (link na bio)
  urlCatalogo?: string;
}

/**
 * Gera imagem composta 1080x1080 (Canvas API, sem dependencia externa) +
 * legenda pronta pra postar no Instagram. Funciona em qualquer conta IG sem
 * API, App Review ou MEI — usuario so toca "Compartilhar" no dispositivo.
 *
 * Mobile: usa Web Share API Level 2 (`navigator.share({ files })`) que abre
 *   o seletor nativo do SO incluindo o app do Instagram.
 * Desktop: baixa o JPG e copia a legenda pro clipboard.
 */
@Injectable({ providedIn: 'root' })
export class CompartilharInstagramService {
  private imagemService = inject(ImagemService);

  async compartilhar(input: CompartilharVeiculoInput): Promise<{ ok: boolean; mensagem: string }> {
    const { veiculo, loja } = input;
    const caminho = input.imagemCaminho || (veiculo.imagens && veiculo.imagens.length > 0 ? veiculo.imagens[0].imgCaminho : null);
    if (!caminho) {
      return { ok: false, mensagem: 'Veiculo sem imagem cadastrada.' };
    }

    // Pede a foto ja redimensionada pra 1080px (ImagensController A2).
    const urlFonte = `${this.imagemService.getImageUrl(caminho)}&max=1080&format=jpeg`;

    let blob: Blob;
    try {
      blob = await this.gerarImagemComposta(urlFonte, veiculo);
    } catch (e: any) {
      return { ok: false, mensagem: 'Falha ao gerar imagem: ' + (e?.message || e) };
    }

    const legenda = this.montarLegenda(veiculo, loja, input.urlCatalogo);
    const nomeArquivo = `${this.slug(veiculo.veiMarca)}-${this.slug(veiculo.veiModelo)}-${veiculo.veiAno}.jpg`;
    const file = new File([blob], nomeArquivo, { type: 'image/jpeg' });

    // Mobile: tenta Web Share API Level 2 (suporta arquivos).
    const navAny = navigator as any;
    if (navAny.canShare && navAny.canShare({ files: [file] })) {
      try {
        await navAny.share({
          files: [file],
          title: `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}`,
          text: legenda
        });
        return { ok: true, mensagem: 'Selecione "Instagram" no compartilhamento.' };
      } catch (e: any) {
        // Usuario cancelou — nao retorna erro.
        if (e?.name === 'AbortError') return { ok: false, mensagem: 'Cancelado.' };
        // Cai pro fallback.
      }
    }

    // Desktop fallback: download + copia legenda.
    this.baixarBlob(blob, nomeArquivo);
    try {
      await navigator.clipboard.writeText(legenda);
      return {
        ok: true,
        mensagem: 'Imagem baixada e legenda copiada. Abra o Instagram, escolha a imagem e cole a legenda.'
      };
    } catch {
      return { ok: true, mensagem: 'Imagem baixada. Copie a legenda manualmente do toast.' };
    }
  }

  /**
   * Compoe canvas 1080x1080: foto centralizada (object-fit cover) + faixa
   * inferior escura com marca/modelo/ano (topo) e preco em destaque (centro).
   * Tudo desenhado com Canvas API — fonts do sistema, sem dependencia.
   */
  private async gerarImagemComposta(urlFonte: string, veiculo: Veiculo): Promise<Blob> {
    const img = await this.carregarImagem(urlFonte);

    const W = 1080;
    const H = 1080;
    const canvas = document.createElement('canvas');
    canvas.width = W;
    canvas.height = H;
    const ctx = canvas.getContext('2d')!;

    // Background preto pra caso a foto nao preencher tudo.
    ctx.fillStyle = '#000000';
    ctx.fillRect(0, 0, W, H);

    // Desenha foto em "cover" (preenche o canvas mantendo proporcao).
    const ratio = Math.max(W / img.width, H / img.height);
    const drawW = img.width * ratio;
    const drawH = img.height * ratio;
    const dx = (W - drawW) / 2;
    const dy = (H - drawH) / 2;
    ctx.drawImage(img, dx, dy, drawW, drawH);

    // Faixa inferior com gradiente preto pra contraste do texto.
    const grad = ctx.createLinearGradient(0, H * 0.55, 0, H);
    grad.addColorStop(0, 'rgba(0,0,0,0)');
    grad.addColorStop(0.4, 'rgba(0,0,0,0.5)');
    grad.addColorStop(1, 'rgba(0,0,0,0.85)');
    ctx.fillStyle = grad;
    ctx.fillRect(0, H * 0.55, W, H * 0.45);

    // Texto: marca + modelo (linha 1)
    ctx.fillStyle = '#ffffff';
    ctx.textAlign = 'center';
    ctx.shadowColor = 'rgba(0,0,0,0.6)';
    ctx.shadowBlur = 8;

    const tituloLinha1 = `${veiculo.veiMarca} ${veiculo.veiModelo}`.toUpperCase();
    ctx.font = 'bold 60px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
    this.desenharTextoQuebrado(ctx, tituloLinha1, W / 2, H - 280, W - 100, 70);

    // Ano + KM (linha 2)
    ctx.font = '500 38px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
    const km = veiculo.veiKm > 0 ? ` · ${this.formatarKm(veiculo.veiKm)} km` : '';
    ctx.fillText(`${veiculo.veiAno}${km}`, W / 2, H - 175);

    // Preco em destaque (faixa amarela/dourada)
    ctx.shadowBlur = 0;
    const precoTxt = `R$ ${this.formatarPreco(veiculo.veiPreco)}`;
    ctx.font = 'bold 90px system-ui, -apple-system, "Segoe UI", Roboto, sans-serif';
    const precoW = ctx.measureText(precoTxt).width + 80;
    const precoH = 110;
    const precoX = (W - precoW) / 2;
    const precoY = H - 130;
    ctx.fillStyle = '#fbbf24';
    this.desenharRetanguloArredondado(ctx, precoX, precoY, precoW, precoH, 16);
    ctx.fillStyle = '#1f2937';
    ctx.textBaseline = 'middle';
    ctx.fillText(precoTxt, W / 2, precoY + precoH / 2 + 4);

    return await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob(b => b ? resolve(b) : reject(new Error('toBlob retornou null')), 'image/jpeg', 0.9);
    });
  }

  private desenharRetanguloArredondado(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number): void {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
    ctx.fill();
  }

  private desenharTextoQuebrado(ctx: CanvasRenderingContext2D, texto: string, x: number, y: number, larguraMax: number, alturaLinha: number): void {
    const palavras = texto.split(' ');
    const linhas: string[] = [];
    let linhaAtual = '';
    for (const p of palavras) {
      const tentativa = linhaAtual ? `${linhaAtual} ${p}` : p;
      if (ctx.measureText(tentativa).width > larguraMax && linhaAtual) {
        linhas.push(linhaAtual);
        linhaAtual = p;
      } else {
        linhaAtual = tentativa;
      }
    }
    if (linhaAtual) linhas.push(linhaAtual);
    // Limita a 2 linhas pra nao invadir o preco
    const linhasFinais = linhas.slice(0, 2);
    const startY = y - ((linhasFinais.length - 1) * alturaLinha) / 2;
    linhasFinais.forEach((l, i) => ctx.fillText(l, x, startY + i * alturaLinha));
  }

  private carregarImagem(url: string): Promise<HTMLImageElement> {
    return new Promise((resolve, reject) => {
      const img = new Image();
      // Same-origin: nao precisa crossOrigin
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error('Falha ao carregar imagem'));
      img.src = url;
    });
  }

  private montarLegenda(veiculo: Veiculo, loja: Loja | null | undefined, urlCatalogo?: string): string {
    const linhas: string[] = [];
    linhas.push(`🚗 ${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}`);
    linhas.push(`💰 R$ ${this.formatarPreco(veiculo.veiPreco)}`);
    if (veiculo.veiKm > 0) linhas.push(`📊 ${this.formatarKm(veiculo.veiKm)} km`);
    if (veiculo.veiCor) linhas.push(`🎨 ${veiculo.veiCor}`);
    if (veiculo.veiOpcionais) {
      linhas.push(`✨ ${veiculo.veiOpcionais.replace(/,/g, ' · ')}`);
    }
    linhas.push('');
    linhas.push('👉 Link na bio pra mais detalhes!');
    if (loja?.lojWhatsApp) linhas.push(`📲 WhatsApp: ${loja.lojWhatsApp}`);
    if (urlCatalogo) linhas.push(`🔗 ${urlCatalogo}`);
    linhas.push('');
    const marca = this.tagHash(veiculo.veiMarca);
    const modelo = this.tagHash(veiculo.veiMarca + veiculo.veiModelo);
    const tags = [`#${marca}`, `#${modelo}`, '#carros', '#usados', '#seminovos'];
    if (loja?.lojCidade) {
      tags.push(`#${this.tagHash(loja.lojCidade)}`);
      tags.push(`#${this.tagHash('carros' + loja.lojCidade)}`);
    }
    linhas.push(tags.join(' '));
    return linhas.join('\n');
  }

  private baixarBlob(blob: Blob, nome: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = nome;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  private slug(raw: string): string {
    return (raw || '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || 'veiculo';
  }

  private tagHash(raw: string): string {
    return (raw || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-zA-Z0-9]/g, '') || 'carros';
  }

  private formatarPreco(v: number): string {
    return v.toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  private formatarKm(v: number): string {
    return v.toLocaleString('pt-BR');
  }
}
