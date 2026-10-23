import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-materiels', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <h1>Matériel roulant et non roulant</h1>
  <div class="card">
    <h2>Nouveau matériel</h2>
    <div class="form-grid">
      <div><label>Désignation *</label><input [(ngModel)]="form.designation"></div>
      <div><label>Type</label><select [(ngModel)]="form.type"><option value="Roulant">Roulant</option><option value="NonRoulant">Non roulant</option></select></div>
      <div><label>Immatriculation / N° série</label><input [(ngModel)]="form.immatriculation"></div>
      <div><label>Statut</label><input [(ngModel)]="form.statut"></div>
    </div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div style="margin-top:10px"><button class="btn" [disabled]="!form.designation" (click)="save()">Ajouter</button></div>
  </div>
  <div class="card">
    <div class="row" style="margin-bottom:10px"><select style="width:200px" [(ngModel)]="type" (ngModelChange)="load()">
      <option value="">Tous</option><option value="Roulant">Roulant</option><option value="NonRoulant">Non roulant</option></select></div>
    <table><tr><th>Désignation</th><th>Type</th><th>Immat. / Série</th><th>Statut</th><th class="num">Entretiens</th><th class="num">Total payé</th><th></th></tr>
      <tr *ngFor="let m of list"><td>{{ m.designation }}</td><td><span class="badge">{{ m.type === 'Roulant' ? 'Roulant' : 'Non roulant' }}</span></td>
        <td>{{ m.immatriculation }}</td><td>{{ m.statut }}</td><td class="num">{{ m.nbEntretiens }}</td>
        <td class="num">{{ m.totalPaye | number:'1.2-2' }}</td><td class="right"><a class="btn small" [routerLink]="['/materiels', m.id]">Fiche</a></td></tr></table>
  </div>`
})
export class MaterielsComponent implements OnInit {
  private api = inject(ApiService);
  list: any[] = []; type = ''; error = '';
  form: any = { designation: '', type: 'Roulant', immatriculation: '', statut: 'En service' };
  ngOnInit() { this.load(); }
  load() { this.api.get<any[]>('materiels', { type: this.type }).subscribe(r => this.list = r); }
  save() {
    this.api.post('materiels', this.form).subscribe({
      next: () => { this.form = { designation: '', type: 'Roulant', immatriculation: '', statut: 'En service' }; this.load(); },
      error: e => this.error = ApiService.err(e)
    });
  }
}
