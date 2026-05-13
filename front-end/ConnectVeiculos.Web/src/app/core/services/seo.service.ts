import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { DOCUMENT } from '@angular/common';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SeoService {
  private meta = inject(Meta);
  private title = inject(Title);
  private doc = inject(DOCUMENT);

  setVehiclePage(veiculo: any, baseUrl?: string): void {
    const base = baseUrl || environment.apiUrl.replace('/api', '');
    const titleText = `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno} - ${this.formatPreco(veiculo.veiPreco)}`;
    const description = `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}, ${veiculo.veiCor || ''}, ${this.formatKm(veiculo.veiKm)}. ${this.formatPreco(veiculo.veiPreco)}. ${veiculo.lojaNome} - ${veiculo.lojaCidade}/${veiculo.lojaEstado}`;
    const imageUrl = veiculo.imagens?.length > 0
      ? `${base}/api/imagens/file?path=${encodeURIComponent(veiculo.imagens[0])}`
      : '';

    this.title.setTitle(titleText);

    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });

    // Open Graph
    this.meta.updateTag({ property: 'og:title', content: titleText });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'product' });
    if (imageUrl) {
      this.meta.updateTag({ property: 'og:image', content: imageUrl });
    }

    // Twitter Card
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: titleText });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    if (imageUrl) {
      this.meta.updateTag({ name: 'twitter:image', content: imageUrl });
    }
  }

  setCatalogPage(loja?: any): void {
    const titleText = loja
      ? `${loja.lojNome} - Catalogo de Veiculos`
      : 'Catalogo de Veiculos';
    const description = loja
      ? `Veja os veículos disponíveis em ${loja.lojNome}, ${loja.lojCidade}/${loja.lojaEstado}. Carros, motos e muito mais.`
      : 'Encontre o veículo ideal. Catálogo completo com fotos, preços e detalhes.';

    this.title.setTitle(titleText);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
    this.meta.updateTag({ property: 'og:title', content: titleText });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
  }

  setVehicleJsonLd(veiculo: any, baseUrl?: string): void {
    const base = baseUrl || environment.apiUrl.replace('/api', '');
    const jsonLd = {
      '@context': 'https://schema.org',
      '@type': 'Vehicle',
      'name': `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}`,
      'brand': { '@type': 'Brand', 'name': veiculo.veiMarca },
      'model': veiculo.veiModelo,
      'modelDate': String(veiculo.veiAno),
      'color': veiculo.veiCor,
      'mileageFromOdometer': {
        '@type': 'QuantitativeValue',
        'value': veiculo.veiKm,
        'unitCode': 'KMT'
      },
      'offers': {
        '@type': 'Offer',
        'price': veiculo.veiPreco,
        'priceCurrency': 'BRL',
        'availability': 'https://schema.org/InStock',
        'seller': {
          '@type': 'AutoDealer',
          'name': veiculo.lojaNome,
          'address': {
            '@type': 'PostalAddress',
            'addressLocality': veiculo.lojaCidade,
            'addressRegion': veiculo.lojaEstado,
            'addressCountry': 'BR'
          }
        }
      },
      'image': veiculo.imagens?.map((img: string) => `${base}/api/imagens/file?path=${encodeURIComponent(img)}`) || [],
      'description': veiculo.veiObservacao || `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno} ${veiculo.veiCor || ''}`
    };

    this.setJsonLd(jsonLd);
  }

  setCatalogJsonLd(veiculos: any[], baseUrl?: string): void {
    const base = baseUrl || '';
    const jsonLd = {
      '@context': 'https://schema.org',
      '@type': 'ItemList',
      'itemListElement': veiculos.slice(0, 30).map((v: any, i: number) => ({
        '@type': 'ListItem',
        'position': i + 1,
        'name': `${v.veiMarca} ${v.veiModelo} ${v.veiAno}`,
        'url': `${base}/catalogo/veiculo/${v.veiId}`
      }))
    };

    this.setJsonLd(jsonLd);
  }

  private setJsonLd(data: object): void {
    const existing = this.doc.querySelector('script[type="application/ld+json"]');
    if (existing) existing.remove();

    const script = this.doc.createElement('script');
    script.type = 'application/ld+json';
    script.textContent = JSON.stringify(data);
    this.doc.head.appendChild(script);
  }

  clearMeta(): void {
    this.meta.removeTag('property="og:title"');
    this.meta.removeTag('property="og:description"');
    this.meta.removeTag('property="og:image"');
    this.meta.removeTag('property="og:type"');
    this.meta.removeTag('name="twitter:card"');
    this.meta.removeTag('name="twitter:title"');
    this.meta.removeTag('name="twitter:description"');
    this.meta.removeTag('name="twitter:image"');
  }

  private formatPreco(valor: number): string {
    return valor?.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }) || '';
  }

  private formatKm(km: number): string {
    return km ? km.toLocaleString('pt-BR') + ' km' : '';
  }
}
