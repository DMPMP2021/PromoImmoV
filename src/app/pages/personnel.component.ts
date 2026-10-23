import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { LabPipe } from '../core/lab.pipe';
import { TYPES_PAIE, monthNow, today } from '../core/models';
import { PointageComponent } from './pointage.component';
import { ExportButtonsComponent } from '../core/export-buttons.component';

@Component({
  selector: 'app-personnel', standalone: true, imports: [CommonModule, FormsModule, RouterLink, LabPipe, PointageComponent, ExportButtonsComponent],
  template: `
  <div class="row"><h1>Service du personnel</h1><span class="spacer"></span>
    <a class="btn secondary" routerLink="/taches/Personnel">Voir les tâches →</a></div>
  <div class="row" style="margin-bottom:14px">
    <button class="btn" [class.secondary]="tab!=='employes'" (click)="tab='employes'">Employés</button>
    <button class="btn" [class.secondary]="tab!=='pointage'" (click)="tab='pointage'">Pointage</button>
    <button class="btn" [class.secondary]="tab!=='paiements'" (click)="tab='paiements'">Paiements</button>
    <button class="btn" [class.secondary]="tab!=='recap'" (click)="tab='recap'; loadRecap()">Récapitulatif mensuel</button></div>
  <div class="alert err" *ngIf="error">{{ error }}</div>

  <ng-container *ngIf="tab==='employes'">
    <div class="card"><h2>{{ emp.id ? 'Modifier' : 'Nouvel' }} employé</h2>
      <div class="form-grid">
        <div><label>Nom complet *</label><input [(ngModel)]="emp.nom"></div><div><label>Poste *</label><input [(ngModel)]="emp.poste"></div>
        <div><label>Téléphone</label><input [(ngModel)]="emp.telephone"></div>
        <div><label>Salaire journalier (DA)</label><input type="number" min="0" [(ngModel)]="emp.salaireJournalier"></div>
        <div><label>Chantier d'affectation</label><select [(ngModel)]="emp.chantierId"><option [ngValue]="null">— aucun —</option>
          <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div><label>Statut</label><select [(ngModel)]="emp.actif"><option [ngValue]="true">Actif</option><option [ngValue]="false">Inactif</option></select></div></div>
      <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!emp.nom || !emp.poste" (click)="saveEmp()">Enregistrer</button>
        <button class="btn secondary" *ngIf="emp.id" (click)="emp=blankEmp()">Annuler</button></div></div>
    <div class="card"><table><tr><th>Nom</th><th>Poste</th><th>Téléphone</th><th class="num">Salaire/jour</th><th>Chantier</th><th>Statut</th><th></th></tr>
      <tr *ngFor="let e of employes"><td>{{ e.nom }}</td><td>{{ e.poste }}</td><td>{{ e.telephone }}</td><td class="num">{{ e.salaireJournalier | number:'1.2-2' }}</td>
        <td>{{ e.chantierNom }}</td><td><span class="badge" [class.ok]="e.actif">{{ e.actif ? 'Actif' : 'Inactif' }}</span></td>
        <td class="right"><button class="btn secondary small" (click)="editEmp(e)">Modifier</button></td></tr></table></div>
  </ng-container>

  <app-pointage *ngIf="tab==='pointage'"></app-pointage>

  <ng-container *ngIf="tab==='paiements'">
    <div class="card"><h2>Nouveau paiement</h2>
      <div class="form-grid">
        <div><label>Employé *</label><select [(ngModel)]="pay.employeId"><option [ngValue]="null">— choisir —</option><option *ngFor="let e of employes" [ngValue]="e.id">{{ e.nom }}</option></select></div>
        <div><label>Type</label><select [(ngModel)]="pay.type"><option *ngFor="let t of typesPaie" [value]="t.v">{{ t.l }}</option></select></div>
        <div><label>Montant (DA) *</label><input type="number" min="0" [(ngModel)]="pay.montant"></div>
        <div><label>Période (mois)</label><input type="month" [(ngModel)]="pay.periode"></div>
        <div style="grid-column:span 2"><label>Motif</label><input [(ngModel)]="pay.motif"></div></div>
      <div style="margin-top:10px"><button class="btn" [disabled]="!pay.employeId || !pay.montant" (click)="savePay()">Enregistrer le paiement</button></div></div>
    <div class="card"><div class="row" style="margin-bottom:10px"><select style="width:240px" [(ngModel)]="fEmp" (ngModelChange)="loadPay()">
      <option [ngValue]="null">Tous les employés</option><option *ngFor="let e of employes" [ngValue]="e.id">{{ e.nom }}</option></select></div>
      <table><tr><th>Date</th><th>Employé</th><th>Type</th><th>Période</th><th>Motif</th><th class="num">Montant</th></tr>
        <tr *ngFor="let p of paiements"><td>{{ p.date | date:'dd/MM/yyyy' }}</td><td>{{ p.employeNom }}</td><td>{{ p.type | lab:typesPaie }}</td>
          <td>{{ p.periode }}</td><td>{{ p.motif }}</td><td class="num">{{ p.montant | number:'1.2-2' }}</td></tr></table></div>
  </ng-container>

  <div class="card" *ngIf="tab==='recap'">
    <div class="row" style="margin-bottom:10px"><div><label>Mois</label><input type="month" [(ngModel)]="mois" (ngModelChange)="loadRecap()"></div><span class="spacer"></span>
      <app-export path="exports/recap-paie" [params]="{ mois: mois }" name="recap_paie"></app-export></div>
    <table><tr><th>Employé</th><th>Poste</th><th class="num">Salaire/jour</th><th class="num">Jours présents</th><th class="num">Absences</th><th class="num">Heures</th>
      <th class="num">Salaire estimé</th><th class="num">Payé</th><th class="num">Reste</th><th>Bulletin</th></tr>
      <tr *ngFor="let r of recap"><td>{{ r.nom }}</td><td>{{ r.poste }}</td><td class="num">{{ r.salaireJournalier | number:'1.0-0' }}</td>
        <td class="num">{{ r.joursPresents }}</td><td class="num">{{ r.absences }}</td><td class="num">{{ r.heures }}</td>
        <td class="num">{{ r.salaireEstime | number:'1.2-2' }}</td><td class="num">{{ r.totalPaye | number:'1.2-2' }}</td>
        <td class="num"><b [class.neg]="r.reste<0">{{ r.reste | number:'1.2-2' }}</b></td>
        <td><app-export [path]="'exports/bulletin-paie/' + r.id" [params]="{ mois: mois }" name="bulletin_paie"></app-export></td></tr>
      <tr><td colspan="6"><b>Total</b></td><td class="num"><b>{{ tot('salaireEstime') | number:'1.2-2' }}</b></td>
        <td class="num"><b>{{ tot('totalPaye') | number:'1.2-2' }}</b></td><td class="num"><b>{{ tot('reste') | number:'1.2-2' }}</b></td><td></td></tr></table>
    <div class="muted" style="margin-top:8px">Salaire estimé = jours présents (ou en retard) × salaire journalier. Un reste négatif indique une avance supérieure au salaire du mois.</div>
  </div>`
})
export class PersonnelComponent implements OnInit {
  private api = inject(ApiService);
  tab: 'employes' | 'pointage' | 'paiements' | 'recap' = 'employes';
  typesPaie = TYPES_PAIE; employes: any[] = []; chantiers: any[] = []; paiements: any[] = []; recap: any[] = [];
  error = ''; fEmp: number | null = null; mois = monthNow();
  emp: any = this.blankEmp();
  pay: any = { employeId: null, type: 'Salaire', montant: null, periode: monthNow(), motif: '' };
  blankEmp() { return { nom: '', poste: '', telephone: '', salaireJournalier: 0, chantierId: null, actif: true }; }
  tot(k: string) { return this.recap.reduce((s, r) => s + r[k], 0); }

  ngOnInit() { this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r); this.loadEmp(); this.loadPay(); }
  loadEmp() { this.api.get<any[]>('employes').subscribe({ next: r => this.employes = r, error: e => this.error = ApiService.err(e) }); }
  editEmp(e: any) { this.emp = { ...e }; window.scrollTo(0, 0); }
  saveEmp() {
    this.error = '';
    const body = { ...this.emp, salaireJournalier: +this.emp.salaireJournalier, chantierId: this.emp.chantierId || null };
    const req = this.emp.id ? this.api.put(`employes/${this.emp.id}`, body) : this.api.post('employes', body);
    req.subscribe({ next: () => { this.emp = this.blankEmp(); this.loadEmp(); }, error: e => this.error = ApiService.err(e) });
  }
  loadPay() { this.api.get<any[]>('paiements-personnel', { employeId: this.fEmp }).subscribe({ next: r => this.paiements = r, error: e => this.error = ApiService.err(e) }); }
  savePay() {
    this.error = '';
    this.api.post('paiements-personnel', { ...this.pay, montant: +this.pay.montant, date: today() }).subscribe({
      next: () => { this.pay = { employeId: null, type: 'Salaire', montant: null, periode: monthNow(), motif: '' }; this.loadPay(); },
      error: e => this.error = ApiService.err(e) });
  }
  loadRecap() { this.api.get<any[]>('personnel/recap', { mois: this.mois }).subscribe({ next: r => this.recap = r, error: e => this.error = ApiService.err(e) }); }
}
