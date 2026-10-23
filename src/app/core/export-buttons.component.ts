import { Component, Input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiService } from './api.service';

/** Boutons « PDF » et « Excel » : télécharge le fichier généré par l'API. */
@Component({
  selector: 'app-export', standalone: true, imports: [CommonModule],
  template: `
  <span style="display:inline-flex;gap:6px;align-items:center;flex-wrap:wrap">
    <button class="btn secondary small" [disabled]="busy" (click)="go('pdf')">📄 PDF</button>
    <button class="btn secondary small" [disabled]="busy" (click)="go('xlsx')">📊 Excel</button>
    <span class="neg" style="font-size:12px" *ngIf="error">{{ error }}</span>
  </span>`
})
export class ExportButtonsComponent {
  private api = inject(ApiService);
  @Input() path = '';
  @Input() params: any = {};
  @Input() name = 'export';
  busy = false; error = '';

  go(format: string) {
    this.busy = true; this.error = '';
    this.api.download(this.path, { ...this.params, format }, `${this.name}.${format}`).subscribe({
      next: () => this.busy = false,
      error: e => { this.error = ApiService.err(e); this.busy = false; }
    });
  }
}
