import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.scss'
})
export class PaginationComponent {
  private _page = 1;

  @Input()
  set page(value: number) { this._page = value; }
  get page(): number { return this._page; }

  // Alias para compatibilidade
  @Input()
  set currentPage(value: number) { this._page = value; }

  @Input() pageSize = 10;
  @Input() totalItems = 0;
  @Input() totalPages = 0;
  @Input() pageSizeOptions = [10, 25, 50, 100];

  @Output() pageChange = new EventEmitter<number>();
  @Output() pageSizeChange = new EventEmitter<number>();

  get calculatedTotalPages(): number {
    if (this.totalPages > 0) return this.totalPages;
    return this.pageSize > 0 ? Math.ceil(this.totalItems / this.pageSize) : 0;
  }

  get pages(): number[] {
    const pages: number[] = [];
    const maxVisible = 5;
    const total = this.calculatedTotalPages;
    let start = Math.max(1, this.page - Math.floor(maxVisible / 2));
    let end = Math.min(total, start + maxVisible - 1);

    if (end - start + 1 < maxVisible) {
      start = Math.max(1, end - maxVisible + 1);
    }

    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    return pages;
  }

  get startItem(): number {
    if (this.totalItems === 0) return 0;
    return (this.page - 1) * this.pageSize + 1;
  }

  get endItem(): number {
    return Math.min(this.page * this.pageSize, this.totalItems);
  }

  goToPage(pageNum: number): void {
    const total = this.calculatedTotalPages;
    if (pageNum >= 1 && pageNum <= total && pageNum !== this.page) {
      this.pageChange.emit(pageNum);
    }
  }

  previousPage(): void {
    if (this.page > 1) {
      this.goToPage(this.page - 1);
    }
  }

  nextPage(): void {
    if (this.page < this.calculatedTotalPages) {
      this.goToPage(this.page + 1);
    }
  }

  onPageSizeChange(event: Event): void {
    const newSize = +(event.target as HTMLSelectElement).value;
    this.pageSizeChange.emit(newSize);
  }
}
