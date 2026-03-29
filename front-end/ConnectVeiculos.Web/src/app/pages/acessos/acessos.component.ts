import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { AcessoService } from '../../core/services';
import { Acesso } from '../../core/models';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ConfirmModalComponent } from '../../shared/components/confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-acessos',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, PaginationComponent, ConfirmModalComponent],
  templateUrl: './acessos.component.html',
  styleUrl: './acessos.component.scss'
})
export class AcessosComponent implements OnInit {
  private acessoService = inject(AcessoService);
  private fb = inject(FormBuilder);

  acessos: Acesso[] = [];
  loading = false;
  showModal = false;
  editMode = false;
  editId: number | null = null;

  // Modal de confirmacao
  showConfirmModal = false;
  acessoParaExcluir: number | null = null;

  // Paginação
  page = 1;
  pageSize = 10;
  totalItems = 0;
  totalPages = 0;
  searchTerm = '';

  form: FormGroup = this.fb.group({
    acsNome: ['', [Validators.required, Validators.minLength(3)]],
    acsDesc: [''],
    acsSts: [true]
  });

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;
    this.acessoService.getAllPaged(this.page, this.pageSize, this.searchTerm).subscribe({
      next: (result) => {
        this.acessos = result.items;
        this.totalItems = result.totalItems;
        this.totalPages = result.totalPages;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onSearch(): void {
    this.page = 1;
    this.loadData();
  }

  onPageChange(page: number): void {
    this.page = page;
    this.loadData();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.page = 1;
    this.loadData();
  }

  openModal(acesso?: Acesso): void {
    this.editMode = !!acesso;
    if (acesso) {
      this.editId = acesso.acsId;
      this.form.patchValue(acesso);
    } else {
      this.editId = null;
      this.form.reset({ acsSts: true });
    }
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.form.reset({ acsSts: true });
    this.editId = null;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const data = this.form.value;

    if (this.editMode && this.editId) {
      this.acessoService.update(this.editId, data).subscribe({
        next: () => {
          this.loadData();
          this.closeModal();
        }
      });
    } else {
      this.acessoService.create(data).subscribe({
        next: () => {
          this.loadData();
          this.closeModal();
        }
      });
    }
  }

  remove(id: number): void {
    this.acessoParaExcluir = id;
    this.showConfirmModal = true;
  }

  confirmarExclusao(): void {
    if (this.acessoParaExcluir) {
      this.acessoService.remove(this.acessoParaExcluir).subscribe({
        next: () => {
          this.loadData();
          this.cancelarExclusao();
        }
      });
    }
  }

  cancelarExclusao(): void {
    this.showConfirmModal = false;
    this.acessoParaExcluir = null;
  }

  hasError(field: string, error: string): boolean {
    const control = this.form.get(field);
    return control ? control.hasError(error) && control.touched : false;
  }
}
