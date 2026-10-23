import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-materiel-detail', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <a routerLink="/materiels">← Matériel</a>
  <ng-container *ngIf="m">
    <div class="row" style="margin:8px 0 16px"><h1 style="margin:0">{{ m.designation }}</h1>
      <span class="badge">{{ m.type === 'Roulant' ? 'Roulant' : 'Non roulant' }}</span>
      <span class="muted">{{ m.immatriculation }}</span><span class="spacer"></span>
      <span>Total payé : <b>{{ m.totalPaye | number:'1.2-2' }} DA</b></span></div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div class="grid grid-2">
      <div class="card"><h2>Entretiens</h2>
        <div class="form-grid">
          <div><label>Type d'entretien *</label><input [(ngModel)]="ent.type" placeholder="Vidange, révision, réparation…"></div>
          <div><label>Date</label><input type="date" [(ngModel)]="ent.date"></div>
          <div style="grid-column:1/-1"><label>Description</label><input [(ngModel)]="ent.description"></div></div>
        <div style="margin:10px 0"><button class="btn" [disabled]="!ent.type" (click)="addEntretien()">Ajouter l'entretien</button></div>
        <table><tr><th>Date</th><th>Type</th><th>Description</th></tr>
          <tr *ngFor="let e of m.entretiens"><td>{{ e.date | date:'dd/MM/yyyy' }}</td><td>{{ e.type }}</td><td>{{ e.description }}</td></tr></table>
      </div>
      <div class="card"><h2>Paiements</h2>
        <div class="form-grid">
          <div><label>Montant (DA) *</label><input type="number" min="0" [(ngModel)]="pay.montant"></div>
          <div><label>Entretien lié</label><select [(ngModel)]="pay.entretienId"><option [ngValue]="null">— aucun —</option>
            <option *ngFor="let e of m.entretiens" [ngValue]="e.id">{{ e.date | date:'dd/MM/yy' }} – {{ e.type }}</option></select></div>
          <div><label>Chantier concerné</label><select [(ngModel)]="pay.chantierId"><option [ngValue]="null">— non affecté —</option>
            <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
          <div style="grid-column:1/-1"><label>Motif du paiement *</label><input [(ngModel)]="pay.motif"></div></div>
        <div style="margin:10px 0"><button class="btn" [disabled]="!pay.montant || !pay.motif" (click)="payer()">Enregistrer le paiement</button></div>
        <table><tr><th>Date</th><th>Motif</th><th class="num">Montant</th></tr>
          <tr *ngFor="let p of m.paiements"><td>{{ p.date | date:'dd/MM/yyyy' }}</td><td>{{ p.motif }}</td><td class="num">{{ p.montant | number:'1.2-2' }}</td></tr></table>
      </div>
    </div>
  </ng-container>`
})
export class MaterielDetailComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  id = 0; m: any; error = ''; chantiers: any[] = [];
  ent: any = { type: '', date: new Date().toISOString().slice(0, 10), description: '' };
  pay: any = { montant: null, entretienId: null, chantierId: null, motif: '' };
  ngOnInit() { this.id = +this.route.snapshot.paramMap.get('id')!; this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r); this.load(); }
  load() { this.api.get<any>('materiels/' + this.id).subscribe(r => this.m = r); }
  addEntretien() {
    this.error = '';
    this.api.post(`materiels/${this.id}/entretiens`, this.ent).subscribe({
      next: () => { this.ent = { type: '', date: new Date().toISOString().slice(0, 10), description: '' }; this.load(); },
      error: e => this.error = ApiService.err(e) });
  }
  payer() {
    this.error = '';
    this.api.post('paiements-materiel', { materielId: this.id, entretienId: this.pay.entretienId, chantierId: this.pay.chantierId, montant: +this.pay.montant, motif: this.pay.motif }).subscribe({
      next: () => { this.pay = { montant: null, entretienId: null, chantierId: null, motif: '' }; this.load(); },
      error: e => this.error = ApiService.err(e) });
  }
}
