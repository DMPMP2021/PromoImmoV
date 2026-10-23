import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-budgets', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <h1>Ordres budgétaires</h1>
  <div class="card">
    <div class="row" style="margin-bottom:12px">
      <span>Trésorerie disponible :</span><b [class.neg]="tresorerie<0">{{ tresorerie | number:'1.2-2' }} DA</b></div>
    <div class="form-grid">
      <div><label>Destination</label>
        <select [(ngModel)]="cible"><option value="chantier">Un chantier</option><option value="service">Un service</option></select></div>
      <div *ngIf="cible==='chantier'"><label>Chantier</label>
        <select [(ngModel)]="form.chantierId"><option [ngValue]="null">— choisir —</option>
          <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <div *ngIf="cible==='service'"><label>Service</label>
        <select [(ngModel)]="form.service"><option [ngValue]="null">— choisir —</option>
          <option *ngFor="let s of services" [ngValue]="s">{{ s }}</option></select></div>
      <div><label>Montant (DA)</label><input type="number" min="0" [(ngModel)]="form.montant"></div>
      <div style="grid-column:span 2"><label>Motif / instruction</label><input [(ngModel)]="form.motif"></div>
    </div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div class="alert ok" *ngIf="ok">{{ ok }}</div>
    <div style="margin-top:10px"><button class="btn" [disabled]="!form.montant || !form.motif" (click)="envoyer(false)">Valider l'ordre</button></div>
  </div>

  <div class="card"><h2>Historique des allocations</h2>
    <div class="row" style="margin-bottom:10px">
      <select style="width:220px" [(ngModel)]="fChantier" (ngModelChange)="load()"><option [ngValue]="null">Tous les chantiers</option>
        <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select>
      <select style="width:220px" [(ngModel)]="fService" (ngModelChange)="load()"><option [ngValue]="null">Tous les services</option>
        <option *ngFor="let s of services" [ngValue]="s">{{ s }}</option></select></div>
    <table><tr><th>Date</th><th>Destination</th><th>Motif</th><th>Décidé par</th><th class="num">Montant</th></tr>
      <tr *ngFor="let a of list"><td>{{ a.createdAt | date:'dd/MM/yyyy HH:mm' }}</td><td>{{ a.chantier || a.service }}</td>
        <td>{{ a.motif }}</td><td>{{ a.decidePar }}</td><td class="num">{{ a.montant | number:'1.2-2' }}</td></tr></table>
  </div>`
})
export class BudgetsComponent implements OnInit {
  private api = inject(ApiService);
  chantiers: any[] = []; services: string[] = []; list: any[] = []; tresorerie = 0;
  cible: 'chantier' | 'service' = 'chantier';
  form: any = { chantierId: null, service: null, montant: null, motif: '' };
  fChantier: number | null = null; fService: string | null = null;
  error = ''; ok = '';

  ngOnInit() {
    this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r);
    this.api.get<string[]>('budgets/services').subscribe(r => this.services = r);
    this.load(); this.refreshTreso();
  }
  refreshTreso() { this.api.get<any>('dashboard').subscribe(r => this.tresorerie = r.tresorerie); }
  load() { this.api.get<any[]>('budgets', { chantierId: this.fChantier, service: this.fService }).subscribe(r => this.list = r); }

  envoyer(forcer: boolean) {
    this.error = ''; this.ok = '';
    const body = {
      chantierId: this.cible === 'chantier' ? this.form.chantierId : null,
      service: this.cible === 'service' ? this.form.service : null,
      montant: +this.form.montant, motif: this.form.motif, forcer
    };
    this.api.post('budgets', body).subscribe({
      next: () => {
        this.ok = 'Ordre budgétaire enregistré.';
        this.form = { chantierId: null, service: null, montant: null, motif: '' };
        this.load(); this.refreshTreso();
      },
      error: (e: HttpErrorResponse) => {
        const msg = ApiService.err(e);
        if (e.status === 409 && confirm(msg + '\n\nVoulez-vous quand même forcer cette allocation ?')) this.envoyer(true);
        else this.error = msg;
      }
    });
  }
}
