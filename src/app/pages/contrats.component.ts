import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-contrats', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <h1>Contrats et échéanciers</h1>
  <div class="card">
    <h2>Nouveau contrat</h2>
    <div class="form-grid">
      <div><label>Client *</label><select [(ngModel)]="form.clientId"><option [ngValue]="null">— choisir —</option>
        <option *ngFor="let c of clients" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <div><label>Chantier *</label><select [(ngModel)]="form.chantierId"><option [ngValue]="null">— choisir —</option>
        <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <div><label>Lot / appartement *</label><input [(ngModel)]="form.lot"></div>
      <div><label>Prix total (DA) *</label><input type="number" min="0" [(ngModel)]="form.prixTotal"></div>
      <div><label>Nombre de tranches</label><input type="number" min="1" [(ngModel)]="form.nombreTranches"></div>
      <div><label>Date 1ʳᵉ tranche</label><input type="date" [(ngModel)]="form.datePremiere"></div>
      <div><label>Intervalle (mois)</label><input type="number" min="1" max="12" [(ngModel)]="form.intervalleMois"></div>
    </div>
    <div class="muted" style="margin-top:8px" *ngIf="form.prixTotal && form.nombreTranches">
      ≈ {{ form.prixTotal / form.nombreTranches | number:'1.2-2' }} DA par tranche</div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div style="margin-top:10px"><button class="btn" [disabled]="!form.clientId || !form.chantierId || !form.lot || !form.prixTotal" (click)="save()">Créer le contrat</button></div>
  </div>
  <div class="card">
    <div class="row" style="margin-bottom:10px"><select style="width:260px" [(ngModel)]="filtreClient" (ngModelChange)="load()">
      <option [ngValue]="null">Tous les clients</option><option *ngFor="let c of clients" [ngValue]="c.id">{{ c.nom }}</option></select></div>
    <table><tr><th>#</th><th>Client</th><th>Chantier</th><th>Lot</th><th class="num">Prix</th><th class="num">Payé</th><th class="num">Reste</th><th></th></tr>
      <tr *ngFor="let c of list"><td>{{ c.id }}</td><td>{{ c.clientNom }}</td><td>{{ c.chantierNom }}</td><td>{{ c.lot }}</td>
        <td class="num">{{ c.prixTotal | number:'1.2-2' }}</td><td class="num">{{ c.totalPaye | number:'1.2-2' }}</td>
        <td class="num"><b>{{ c.prixTotal-c.totalPaye | number:'1.2-2' }}</b></td>
        <td><a *ngIf="auth.hasRole('Gerant')" [routerLink]="['/clients', c.clientId]">Historique</a></td></tr></table>
  </div>`
})
export class ContratsComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  auth = inject(AuthService);
  clients: any[] = []; chantiers: any[] = []; list: any[] = []; error = ''; filtreClient: number | null = null;
  form: any = this.blank();
  blank() { return { clientId: null, chantierId: null, lot: '', prixTotal: null, nombreTranches: 4, datePremiere: new Date().toISOString().slice(0, 10), intervalleMois: 1 }; }
  ngOnInit() {
    const cid = this.route.snapshot.queryParamMap.get('clientId');
    if (cid) { this.filtreClient = +cid; this.form.clientId = +cid; }
    this.api.get<any[]>('clients').subscribe(r => this.clients = r);
    this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r);
    this.load();
  }
  load() { this.api.get<any[]>('contrats', { clientId: this.filtreClient }).subscribe(r => this.list = r); }
  save() {
    this.error = '';
    this.api.post('contrats', { ...this.form, prixTotal: +this.form.prixTotal }).subscribe({
      next: () => { const c = this.form.clientId; this.form = this.blank(); this.form.clientId = c; this.load(); },
      error: e => this.error = ApiService.err(e)
    });
  }
}
