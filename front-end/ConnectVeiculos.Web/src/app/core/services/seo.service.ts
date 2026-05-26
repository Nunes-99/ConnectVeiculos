import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { DOCUMENT } from '@angular/common';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SeoService {
  private meta = inject(Meta);
  private title = inject(Title);
  private doc = inject(DOCUMENT);

  setVehiclePage(veiculo: any, pageUrl?: string): void {
    const origin = this.resolveOrigin(pageUrl);
    const titleText = `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno} - ${this.formatPreco(veiculo.veiPreco)}`;
    const description = `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}, ${veiculo.veiCor || ''}, ${this.formatKm(veiculo.veiKm)}. ${this.formatPreco(veiculo.veiPreco)}. ${veiculo.lojaNome}${veiculo.lojaCidade ? ' - ' + veiculo.lojaCidade : ''}${veiculo.lojaEstado ? '/' + veiculo.lojaEstado : ''}`.trim();
    const imageUrl = veiculo.imagens?.length > 0
      ? `${origin}/api/imagens/file?path=${encodeURIComponent(veiculo.imagens[0])}`
      : '';
    const canonicalUrl = this.resolveCanonicalUrl(origin, pageUrl);
    const imageAlt = `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}${veiculo.veiCor ? ' ' + veiculo.veiCor : ''}`;

    this.title.setTitle(titleText);

    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
    this.setCanonical(canonicalUrl);

    // Open Graph (Facebook, WhatsApp, LinkedIn, Telegram)
    this.meta.updateTag({ property: 'og:title', content: titleText });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'product' });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this.meta.updateTag({ property: 'og:site_name', content: 'ConnectVeiculos' });
    this.meta.updateTag({ property: 'og:locale', content: 'pt_BR' });
    if (imageUrl) {
      // WhatsApp/Facebook escolhem a melhor imagem com base nas dimensoes.
      // 1200x630 e o tamanho recomendado pelo Facebook para summary_large_image.
      // Mesmo que a imagem real seja outra, declarar dimensoes evita lazy parse
      // que pode resultar em "no preview" no WhatsApp.
      this.meta.updateTag({ property: 'og:image', content: imageUrl });
      this.meta.updateTag({ property: 'og:image:secure_url', content: imageUrl });
      this.meta.updateTag({ property: 'og:image:type', content: 'image/jpeg' });
      this.meta.updateTag({ property: 'og:image:width', content: '1200' });
      this.meta.updateTag({ property: 'og:image:height', content: '630' });
      this.meta.updateTag({ property: 'og:image:alt', content: imageAlt });
    }

    // Product-specific (rich preview com preco em alguns scrapers)
    if (veiculo.veiPreco) {
      this.meta.updateTag({ property: 'product:price:amount', content: String(veiculo.veiPreco) });
      this.meta.updateTag({ property: 'product:price:currency', content: 'BRL' });
      this.meta.updateTag({ property: 'product:availability', content: 'in stock' });
      this.meta.updateTag({ property: 'product:condition', content: 'used' });
    }

    // Twitter Card
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: titleText });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:url', content: canonicalUrl });
    if (imageUrl) {
      this.meta.updateTag({ name: 'twitter:image', content: imageUrl });
      this.meta.updateTag({ name: 'twitter:image:alt', content: imageAlt });
    }
  }

  // Resolve a origem (https://dominio.com) priorizando, nessa ordem:
  // 1. URL absoluta passada pelo chamador (se ja contem origin)
  // 2. window.location no client (browser real)
  // 3. environment.siteBaseUrl (fallback robusto pro SSR — document.location.origin
  //    via Angular CommonEngine/domino retorna vazio, entao precisamos disso)
  // 4. environment.apiUrl como ultimo recurso
  private resolveOrigin(pageUrl?: string): string {
    if (pageUrl && pageUrl.startsWith('http')) {
      try {
        const u = new URL(pageUrl);
        return `${u.protocol}//${u.host}`;
      } catch { /* fall through */ }
    }
    if (this.doc.location && this.doc.location.origin) {
      return this.doc.location.origin;
    }
    const envOrigin = (environment as any).siteBaseUrl;
    if (envOrigin) return envOrigin;
    return environment.apiUrl.replace(/\/api\/?$/, '');
  }

  // Resolve URL canonica completa a partir do origin + path.
  // Aceita pageUrl como (a) URL absoluta completa, (b) path relativo "/catalogo/..."
  // ou (c) undefined (deriva do document.location.pathname).
  private resolveCanonicalUrl(origin: string, pageUrl?: string): string {
    if (pageUrl) {
      if (pageUrl.startsWith('http')) return pageUrl;
      return `${origin}${pageUrl.startsWith('/') ? '' : '/'}${pageUrl}`;
    }
    const path = this.doc.location?.pathname;
    if (path) return `${origin}${path}`;
    return origin;
  }

  private setCanonical(url: string): void {
    const existing = this.doc.querySelector('link[rel="canonical"]');
    if (existing) {
      existing.setAttribute('href', url);
      return;
    }
    const link = this.doc.createElement('link');
    link.setAttribute('rel', 'canonical');
    link.setAttribute('href', url);
    this.doc.head.appendChild(link);
  }

  // Helper generico para paginas estaticas (termos, privacidade, etc). Define
  // apenas titulo + meta description + canonical. Sem JSON-LD nem OG.
  setMeta(opts: { title: string; description: string }): void {
    this.title.setTitle(opts.title);
    this.meta.updateTag({ name: 'description', content: opts.description });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
    const origin = this.resolveOrigin();
    const canonical = this.resolveCanonicalUrl(origin);
    this.setCanonical(canonical);
    this.meta.updateTag({ property: 'og:title', content: opts.title });
    this.meta.updateTag({ property: 'og:description', content: opts.description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:url', content: canonical });
    this.meta.updateTag({ property: 'og:site_name', content: 'ConnectVeiculos' });
    this.meta.updateTag({ property: 'og:locale', content: 'pt_BR' });
  }

  setLandingPage(): void {
    const titleText = 'ConnectVeiculos — Sistema de Gestão para Revendas de Veículos';
    const description = 'Plataforma SaaS completa para revendedores de veículos: estoque, catálogo online, leads, vendas, integrações com Google Merchant, Facebook Catalog, Mercado Livre e muito mais.';

    this.title.setTitle(titleText);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
    this.meta.updateTag({ name: 'keywords', content: 'connectveiculos, sistema revenda, gestão de veículos, catálogo online, crm automotivo, software para revendedor' });

    // Open Graph
    this.meta.updateTag({ property: 'og:title', content: titleText });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: 'ConnectVeiculos' });
    this.meta.updateTag({ property: 'og:locale', content: 'pt_BR' });

    // Twitter Card
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: titleText });
    this.meta.updateTag({ name: 'twitter:description', content: description });

    // JSON-LD SoftwareApplication para rich results
    const jsonLd = {
      '@context': 'https://schema.org',
      '@type': 'SoftwareApplication',
      'name': 'ConnectVeiculos',
      'description': description,
      'applicationCategory': 'BusinessApplication',
      'operatingSystem': 'Web',
      'offers': {
        '@type': 'Offer',
        'priceCurrency': 'BRL',
        'price': '0'
      }
    };
    this.setJsonLd(jsonLd);
  }

  setCatalogPage(loja?: any, pageUrl?: string): void {
    const origin = this.resolveOrigin(pageUrl);
    const canonicalUrl = this.resolveCanonicalUrl(origin, pageUrl);
    const titleText = loja
      ? `${loja.lojNome} - Catalogo de Veiculos`
      : 'Catalogo de Veiculos';
    const description = loja
      ? `Veja os veículos disponíveis em ${loja.lojNome}${loja.lojCidade ? ', ' + loja.lojCidade : ''}${loja.lojEstado ? '/' + loja.lojEstado : ''}. Carros, motos e muito mais.`
      : 'Encontre o veículo ideal. Catálogo completo com fotos, preços e detalhes.';
    const imageUrl = this.resolveLojaLogo(origin, loja?.lojImg);

    this.title.setTitle(titleText);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'robots', content: 'index, follow' });
    this.setCanonical(canonicalUrl);

    // Open Graph
    this.meta.updateTag({ property: 'og:title', content: titleText });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:url', content: canonicalUrl });
    this.meta.updateTag({ property: 'og:site_name', content: loja?.lojNome || 'ConnectVeiculos' });
    this.meta.updateTag({ property: 'og:locale', content: 'pt_BR' });
    if (imageUrl) {
      this.meta.updateTag({ property: 'og:image', content: imageUrl });
      this.meta.updateTag({ property: 'og:image:secure_url', content: imageUrl });
      this.meta.updateTag({ property: 'og:image:alt', content: `Logo ${loja?.lojNome || 'Loja'}` });
    }

    // Twitter Card
    this.meta.updateTag({ name: 'twitter:card', content: imageUrl ? 'summary_large_image' : 'summary' });
    this.meta.updateTag({ name: 'twitter:title', content: titleText });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:url', content: canonicalUrl });
    if (imageUrl) {
      this.meta.updateTag({ name: 'twitter:image', content: imageUrl });
    }
  }

  // Logo vem do backend como path relativo (ex: "/uploads/lojas/123.jpg") ou
   // ja-absoluto ("https://..."). Bots do WhatsApp/Facebook precisam URL absoluta.
   private resolveLojaLogo(origin: string, lojImg?: string | null): string {
     if (!lojImg) return '';
     if (lojImg.startsWith('http://') || lojImg.startsWith('https://')) return lojImg;
     return `${origin}${lojImg.startsWith('/') ? '' : '/'}${lojImg}`;
   }

  setVehicleJsonLd(veiculo: any, pageUrl?: string): void {
    const origin = this.resolveOrigin(pageUrl);
    const canonicalUrl = this.resolveCanonicalUrl(origin, pageUrl);
    const jsonLd: any = {
      '@context': 'https://schema.org',
      '@type': 'Vehicle',
      'name': `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno}`,
      'url': canonicalUrl,
      'brand': { '@type': 'Brand', 'name': veiculo.veiMarca },
      'model': veiculo.veiModelo,
      'modelDate': String(veiculo.veiAno),
      'vehicleModelDate': String(veiculo.veiAno),
      'color': veiculo.veiCor,
      'itemCondition': 'https://schema.org/UsedCondition',
      'mileageFromOdometer': {
        '@type': 'QuantitativeValue',
        'value': veiculo.veiKm,
        'unitCode': 'KMT'
      },
      'offers': {
        '@type': 'Offer',
        'url': canonicalUrl,
        'price': veiculo.veiPreco,
        'priceCurrency': 'BRL',
        'availability': 'https://schema.org/InStock',
        'itemCondition': 'https://schema.org/UsedCondition',
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
      'image': veiculo.imagens?.map((img: string) => `${origin}/api/imagens/file?path=${encodeURIComponent(img)}`) || [],
      'description': veiculo.veiObservacao || `${veiculo.veiMarca} ${veiculo.veiModelo} ${veiculo.veiAno} ${veiculo.veiCor || ''}`.trim()
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
    // Open Graph
    this.meta.removeTag('property="og:title"');
    this.meta.removeTag('property="og:description"');
    this.meta.removeTag('property="og:image"');
    this.meta.removeTag('property="og:image:secure_url"');
    this.meta.removeTag('property="og:image:type"');
    this.meta.removeTag('property="og:image:width"');
    this.meta.removeTag('property="og:image:height"');
    this.meta.removeTag('property="og:image:alt"');
    this.meta.removeTag('property="og:type"');
    this.meta.removeTag('property="og:url"');
    this.meta.removeTag('property="og:site_name"');
    this.meta.removeTag('property="og:locale"');
    // Product
    this.meta.removeTag('property="product:price:amount"');
    this.meta.removeTag('property="product:price:currency"');
    this.meta.removeTag('property="product:availability"');
    this.meta.removeTag('property="product:condition"');
    // Twitter
    this.meta.removeTag('name="twitter:card"');
    this.meta.removeTag('name="twitter:title"');
    this.meta.removeTag('name="twitter:description"');
    this.meta.removeTag('name="twitter:image"');
    this.meta.removeTag('name="twitter:image:alt"');
    this.meta.removeTag('name="twitter:url"');
  }

  private formatPreco(valor: number): string {
    return valor?.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }) || '';
  }

  private formatKm(km: number): string {
    return km ? km.toLocaleString('pt-BR') + ' km' : '';
  }
}
