import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SalvandoDirective } from '../../shared/directives';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { CategoriaService } from '../../core/services';
import { Categoria } from '../../core/models';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ConfirmModalComponent } from '../../shared/components/confirm-modal/confirm-modal.component';

@Component({
  selector: 'app-categorias',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, PaginationComponent, ConfirmModalComponent, SalvandoDirective],
  templateUrl: './categorias.component.html',
  styleUrl: './categorias.component.scss'
})
export class CategoriasComponent implements OnInit {
  private categoriaService = inject(CategoriaService);
  private fb = inject(FormBuilder);

  categorias: Categoria[] = [];
  loading = false;
  showModal = false;
  editMode = false;
  editId: number | null = null;

  // Modal de confirmacao
  showConfirmModal = false;
  // Trava o formulario durante a gravacao: sem isso dava pra editar os
  // campos no meio do envio e clicar em Salvar de novo.
  salvando = false;
  categoriaParaExcluir: number | null = null;

  // Paginação
  page = 1;
  pageSize = 10;
  totalItems = 0;
  totalPages = 0;
  searchTerm = '';

  form: FormGroup = this.fb.group({
    catNome: ['', [Validators.required, Validators.minLength(3)]],
    catDesc: [''],
    catSts: [true]
  });

  ngOnInit(): void {
    this.loadData();
  }

  private loadData(): void {
    this.loading = true;
    this.categoriaService.getAllPaged(this.page, this.pageSize, this.searchTerm).subscribe({
      next: (result) => {
        this.categorias = result.items;
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

  openModal(categoria?: Categoria): void {
    this.editMode = !!categoria;
    if (categoria) {
      this.editId = categoria.catId;
      this.form.patchValue(categoria);
    } else {
      this.editId = null;
      this.form.reset({ catSts: true });
    }
    this.showModal = true;
  }

  closeModal(): void {
    this.showModal = false;
    this.form.reset({ catSts: true });
    this.editId = null;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const data = this.form.value;

    this.salvando = true;

    const gravacao = this.editMode && this.editId
      ? this.categoriaService.update(this.editId, data)
      : this.categoriaService.create(data);

    gravacao.subscribe({
      next: () => {
        this.salvando = false;
        this.loadData();
        this.closeModal();
      },
      // Sem isto o formulario ficaria trancado pra sempre se a gravacao falhar.
      error: () => this.salvando = false
    });
  }

  remove(id: number): void {
    this.categoriaParaExcluir = id;
    this.showConfirmModal = true;
  }

  confirmarExclusao(): void {
    if (this.categoriaParaExcluir) {
      this.categoriaService.remove(this.categoriaParaExcluir).subscribe({
        next: () => {
          this.loadData();
          this.cancelarExclusao();
        }
      });
    }
  }

  cancelarExclusao(): void {
    this.showConfirmModal = false;
    this.categoriaParaExcluir = null;
  }

  hasError(field: string, error: string): boolean {
    const control = this.form.get(field);
    return control ? control.hasError(error) && control.touched : false;
  }
}
