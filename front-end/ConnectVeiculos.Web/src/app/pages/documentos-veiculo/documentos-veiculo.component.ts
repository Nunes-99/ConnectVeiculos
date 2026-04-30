import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  VeiculoDocumentoService, VeiculoDocumento,
  TIPOS_DOCUMENTO, STATUS_DOCUMENTO,
  VeiculoService, ToastService
} from '../../core/services';
import { Veiculo } from '../../core/models';
import { ConfirmModalComponent } from '../../shared/components';

@Component({
  selector: 'app-documentos-veiculo',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, ConfirmModalComponent],
  templateUrl: './documentos-veiculo.component.html',
  styleUrl: './documentos-veiculo.component.scss'
})
export class DocumentosVeiculoComponent implements OnInit {
  private docService = inject(VeiculoDocumentoService);
  private veiculoService = inject(VeiculoService);
  private toast = inject(ToastService);
  private fb = inject(FormBuilder);

  documentos: VeiculoDocumento[] = [];
  veiculos: Veiculo[] = [];
  veiculoFiltroId: number | null = null;
  modoVencendo = true;
  diasAFrente = 30;
  loading = false;

  showModal = false;
  editId: number | null = null;
  showConfirm = false;
  idParaExcluir: number | null = null;

  tipos = TIPOS_DOCUMENTO;
  statusOptions = STATUS_DOCUMENTO;

  form: FormGroup = this.fb.group({
    veiculoId: [null, Validators.required],
    tipo: ['CRLV', Validators.required],
    status: ['PENDENTE', Validators.required],
    arquivo: [''],
    observacao: [''],
    dataVencimento: ['']
  });

  ngOnInit(): void {
    this.veiculoService.getAll().subscribe(v => this.veiculos = v);
    this.carregar();
  }

  carregar(): void {
    this.loading = true;
    const obs = this.veiculoFiltroId
      ? this.docService.listarPorVeiculo(this.veiculoFiltroId)
      : this.docService.listarVencendo(this.diasAFrente);
    obs.subscribe({
      next: docs => { this.documentos = docs; this.loading = false; },
      error: () => { this.loading = false; }
    });
  }

  alterarFiltro(): void {
    this.modoVencendo = !this.veiculoFiltroId;
    this.carregar();
  }

  abrirModal(doc?: VeiculoDocumento): void {
    if (doc) {
      this.editId = doc.docId;
      this.form.patchValue({
        veiculoId: doc.r_VeiId,
        tipo: doc.docTipo,
        status: doc.docStatus,
        arquivo: doc.docArquivo || '',
        observacao: doc.docObservacao || '',
        dataVencimento: doc.docDtVencimento ? doc.docDtVencimento.substring(0, 10) : ''
      });
    } else {
      this.editId = null;
      this.form.reset({
        veiculoId: this.veiculoFiltroId,
        tipo: 'CRLV', status: 'PENDENTE',
        arquivo: '', observacao: '', dataVencimento: ''
      });
    }
    this.showModal = true;
  }

  fecharModal(): void {
    this.showModal = false;
    this.editId = null;
  }

  salvar(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.value;
    const payload = {
      veiculoId: Number(v.veiculoId),
      tipo: v.tipo,
      status: v.status,
      arquivo: v.arquivo || null,
      observacao: v.observacao || null,
      dataVencimento: v.dataVencimento || null
    };

    const obs: import('rxjs').Observable<any> = this.editId
      ? this.docService.atualizar(this.editId, payload)
      : this.docService.criar(payload);

    obs.subscribe({
      next: () => {
        this.toast.success(this.editId ? 'Documento atualizado.' : 'Documento criado.');
        this.fecharModal();
        this.carregar();
      }
    });
  }

  alterarStatus(doc: VeiculoDocumento, status: string): void {
    this.docService.alterarStatus(doc.docId, status).subscribe({
      next: () => {
        this.toast.success('Status atualizado.');
        this.carregar();
      }
    });
  }

  pedirExclusao(id: number): void {
    this.idParaExcluir = id;
    this.showConfirm = true;
  }

  confirmarExclusao(): void {
    if (!this.idParaExcluir) return;
    this.docService.excluir(this.idParaExcluir).subscribe({
      next: () => {
        this.toast.success('Documento excluido.');
        this.showConfirm = false;
        this.idParaExcluir = null;
        this.carregar();
      },
      error: () => { this.showConfirm = false; this.idParaExcluir = null; }
    });
  }

  cancelarExclusao(): void {
    this.showConfirm = false;
    this.idParaExcluir = null;
  }

  veiculoNome(id: number): string {
    const v = this.veiculos.find(x => x.veiId === id);
    return v ? `${v.veiMarca} ${v.veiModelo} ${v.veiAno}` : `#${id}`;
  }

  tipoLabel(c: string): string { return this.tipos.find(t => t.codigo === c)?.label || c; }
  statusLabel(c: string): string { return this.statusOptions.find(t => t.codigo === c)?.label || c; }

  diasParaVencer(dt?: string): number | null {
    if (!dt) return null;
    const venc = new Date(dt);
    const hoje = new Date();
    hoje.setHours(0, 0, 0, 0);
    return Math.ceil((venc.getTime() - hoje.getTime()) / (1000 * 60 * 60 * 24));
  }
}
