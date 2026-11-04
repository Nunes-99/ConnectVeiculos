import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AssinaturaPublica, ContratoService, baixarBlob, erroContrato } from '../../core/services/contrato.service';
import { AssinaturaPadComponent } from '../../shared/components/contratos/assinatura-pad.component';
import { ContratoTextoComponent } from '../../shared/components/contratos/contrato-texto.component';

/**
 * Pagina publica (sem login) em que o cliente le e assina o documento:
 * confere o texto, pede o codigo por e-mail, digita nome e CPF e desenha a
 * rubrica.
 */
@Component({
  selector: 'app-assinar',
  standalone: true,
  imports: [CommonModule, FormsModule, AssinaturaPadComponent, ContratoTextoComponent],
  templateUrl: './assinar.component.html',
  styleUrl: './assinar.component.scss'
})
export class AssinarComponent implements OnInit {
  private api = inject(ContratoService);
  private route = inject(ActivatedRoute);

  tenant = '';
  token = '';
  carregando = true;
  erro: string | null = null;
  info: AssinaturaPublica | null = null;

  nome = '';
  documento = '';
  codigo = '';
  imagem: string | null = null;
  aceite = false;
  codigoEnviadoPara: string | null = null;
  enviandoCodigo = false;
  assinando = false;
  aviso: string | null = null;

  ngOnInit(): void {
    this.tenant = this.route.snapshot.paramMap.get('tenantSlug') || '';
    this.token = this.route.snapshot.paramMap.get('token') || '';
    this.api.abrir(this.tenant, this.token).subscribe({
      next: i => { this.info = i; this.nome = i.nome; this.carregando = false; },
      error: async err => { this.carregando = false; this.erro = await erroContrato(err, 'Não foi possível abrir o documento.'); }
    });
  }

  get podeAssinar(): boolean {
    return !!this.imagem && this.nome.trim().length >= 3 && this.aceite && (!this.info?.precisaCodigo || this.codigo.replace(/\D/g, '').length === 6);
  }

  pedirCodigo(): void {
    this.enviandoCodigo = true;
    this.aviso = null;
    this.api.pedirCodigo(this.tenant, this.token).subscribe({
      next: r => { this.enviandoCodigo = false; this.codigoEnviadoPara = r.email; },
      error: async err => { this.enviandoCodigo = false; this.aviso = await erroContrato(err, 'Não foi possível enviar o código.'); }
    });
  }

  assinar(): void {
    if (!this.podeAssinar || !this.imagem) return;
    this.assinando = true;
    this.aviso = null;
    this.api.assinarPeloLink(this.tenant, this.token, {
      nome: this.nome, documento: this.documento || null, codigo: this.codigo, imagem: this.imagem, aceite: this.aceite
    }).subscribe({
      next: i => { this.assinando = false; this.info = i; window.scrollTo({ top: 0, behavior: 'smooth' }); },
      error: async err => { this.assinando = false; this.aviso = await erroContrato(err, 'Não foi possível assinar.'); }
    });
  }

  baixarPdf(): void {
    this.api.pdfPeloLink(this.tenant, this.token).subscribe({
      next: b => baixarBlob(b, 'documento-assinado.pdf'),
      error: async err => this.aviso = await erroContrato(err, 'Não foi possível baixar o PDF.')
    });
  }
}
