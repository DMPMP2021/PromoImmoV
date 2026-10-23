import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { ExportButtonsComponent } from '../core/export-buttons.component';
import { LabPipe, BadgePipe } from '../core/lab.pipe';
import { STATUTS_OBLIG, TYPES_OBLIG, today } from '../core/models';

@Component({
  selector: 'app-fiscal', standalone: true, imports: [CommonModule, FormsModule, RouterLink, LabPipe, BadgePipe, ExportButtonsComponent],
  template: `
  <div class="row"><h1>Fiscal et parafiscal</h1><span class="spacer"></span>
    <app-export path="exports/obligations" [params]="{ type: fType, statut: fStatut }" name="obligations_fiscales"></app-export>
    <a class="btn secondary" style="margin-left:8px" routerLink="/taches/Fiscal">Voir les tâches →</a></div>
  <div class="grid grid-3">
    <div class="card"><div class="stat-label">À payer</div><div class="stat">{{ aPayer | number:'1.0-0' }} DA</div></div>
    <div class="card"><div class="stat-label">Obligations en retard</div><div class="stat" [class.neg]="enRetard>0">{{ enRetard }}</div></div>
    <div class="card"><div class="stat-label">Déjà payé</div><div class="stat pos">{{ paye | number:'1.0-0' }} DA</div></div></div>
  <div class="card"><h2>{{ form.id ? 'Modifier' : 'Nouvelle' }} obligation</h2>
    <div class="form-grid">
      <div><label>Nature</label><select [(ngModel)]="form.type"><option *ngFor="let t of types" [value]="t.v">{{ t.l }}</option></select></div>
      <div><label>Libellé *</label><input list="libs" [(ngModel)]="form.libelle" placeholder="TVA, IRG, CNAS…">
        <datalist id="libs"><option *ngFor="let l of libelles" [value]="l"></option></datalist></div>
      <div><label>Période *</label><input [(ngModel)]="form.periode" placeholder="Octobre 2026 / T3 2026"></div>
      <div><label>Montant (DA)</label><input type="number" min="0" [(ngModel)]="form.montant"></div>
      <div><label>Échéance</label><input type="date" [(ngModel)]="form.dateEcheance"></div></div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!form.libelle || !form.periode || !form.dateEcheance" (click)="save()">Enregistrer</button>
      <button class="btn secondary" *ngIf="form.id" (click)="form=blank()">Annuler</button></div></div>
  <div class="card"><div class="row" style="margin-bottom:10px">
      <select style="width:180px" [(ngModel)]="fType" (ngModelChange)="load()"><option value="">Fiscal + parafiscal</option><option *ngFor="let t of types" [value]="t.v">{{ t.l }}</option></select>
      <select style="width:180px" [(ngModel)]="fStatut" (ngModelChange)="load()"><option value="">Tous statuts</option><option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
    <table><tr><th>Échéance</th><th>Nature</th><th>Libellé</th><th>Période</th><th class="num">Montant</th><th>Statut</th><th></th></tr>
      <tr *ngFor="let o of list"><td>{{ o.dateEcheance | date:'dd/MM/yyyy' }} <span class="badge danger" *ngIf="o.enRetard">En retard</span></td>
        <td>{{ o.type | lab:types }}</td><td>{{ o.libelle }}</td><td>{{ o.periode }}</td><td class="num">{{ o.montant | number:'1.2-2' }}</td>
        <td><span [class]="o.statut | badge">{{ o.statut | lab:statuts }}</span><div class="muted" style="font-size:11px" *ngIf="o.reference">Réf. {{ o.reference }}</div></td>
        <td class="right"><button class="btn small" *ngIf="o.statut==='APayer'" (click)="payer(o)">Marquer payée</button>
          <button class="btn secondary small" style="margin-left:4px" *ngIf="o.statut==='APayer'" (click)="edit(o)">Modifier</button></td></tr></table></div>`
})
export class FiscalComponent implements OnInit {
  private api = inject(ApiService);
  types = TYPES_OBLIG; statuts = STATUTS_OBLIG; list: any[] = []; fType = ''; fStatut = ''; error = '';
  libelles = ['TVA', 'IRG / salaires', 'IBS', 'TAP', 'Taxe foncière', 'Droits d\'enregistrement', 'CNAS', 'CASNOS', 'CACOBATPH', 'Taxe de formation', 'Autre'];
  form: any = this.blank();
  blank() { return { id: null, type: 'Fiscale', libelle: '', periode: '', montant: 0, dateEcheance: today() }; }
  get aPayer() { return this.list.filter(o => o.statut === 'APayer').reduce((s, o) => s + o.montant, 0); }
  get paye() { return this.list.filter(o => o.statut === 'Payee').reduce((s, o) => s + o.montant, 0); }
  get enRetard() { return this.list.filter(o => o.enRetard).length; }
  ngOnInit() { this.load(); }
  load() { this.api.get<any[]>('obligations', { type: this.fType, statut: this.fStatut }).subscribe({ next: r => this.list = r, error: e => this.error = ApiService.err(e) }); }
  edit(o: any) { this.form = { ...o, dateEcheance: o.dateEcheance.slice(0, 10) }; window.scrollTo(0, 0); }
  save() {
    this.error = '';
    const body = { ...this.form, montant: +this.form.montant };
    const req = this.form.id ? this.api.put(`obligations/${this.form.id}`, body) : this.api.post('obligations', body);
    req.subscribe({ next: () => { this.form = this.blank(); this.load(); }, error: e => this.error = ApiService.err(e) });
  }
  payer(o: any) {
    const reference = prompt(`Référence du paiement (quittance, virement…) pour ${o.libelle} — ${o.periode} :`);
    if (reference === null) return;
    this.api.post(`obligations/${o.id}/payer`, { reference: reference || null, datePaiement: today() }).subscribe({ next: () => this.load(), error: e => this.error = ApiService.err(e) });
  }
}
