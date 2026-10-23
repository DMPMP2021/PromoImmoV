import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { LabPipe, BadgePipe } from '../core/lab.pipe';
import { STATUTS_REMB, TYPES_REMB } from '../core/models';

@Component({
  selector: 'app-remboursements', standalone: true, imports: [CommonModule, FormsModule, RouterLink, LabPipe, BadgePipe],
  template: `
  <div class="row"><h1>Remboursements — désistements et trop-perçus</h1><span class="spacer"></span>
    <a class="btn secondary" routerLink="/taches/Remboursement">Voir les tâches →</a></div>
  <div class="card"><h2>Nouvelle demande</h2>
    <div class="form-grid">
      <div><label>Client</label><select [(ngModel)]="clientId" (ngModelChange)="choisirClient()"><option [ngValue]="null">— choisir —</option>
        <option *ngFor="let c of clients" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <div><label>Contrat</label><select [(ngModel)]="form.contratId"><option [ngValue]="null">— choisir —</option>
        <option *ngFor="let c of contrats" [ngValue]="c.id">{{ c.chantierNom }} / {{ c.lot }} (payé {{ c.totalPaye | number:'1.0-0' }})</option></select></div>
      <div><label>Type</label><select [(ngModel)]="form.type"><option *ngFor="let t of types" [value]="t.v">{{ t.l }}</option></select></div>
      <div><label>Montant (DA)</label><input type="number" min="0" [(ngModel)]="form.montant"></div>
      <div style="grid-column:1/-1"><label>Motif *</label><input [(ngModel)]="form.motif"></div></div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div style="margin-top:10px"><button class="btn" [disabled]="!form.contratId || !form.montant || !form.motif" (click)="creer()">Soumettre au gérant</button></div></div>

  <div class="card"><div class="row" style="margin-bottom:10px"><select style="width:220px" [(ngModel)]="fStatut" (ngModelChange)="load()">
      <option value="">Tous les statuts</option><option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
    <table><tr><th>Dossier</th><th>Client</th><th>Contrat</th><th>Type</th><th>Motif</th><th class="num">Montant</th><th>Statut</th><th></th></tr>
      <tr *ngFor="let r of list"><td>#{{ r.id }}<div class="muted" style="font-size:11px">{{ r.createdAt | date:'dd/MM/yyyy' }}</div></td>
        <td>{{ r.clientNom }}</td><td>{{ r.chantierNom }} / {{ r.lot }}</td><td>{{ r.type | lab:types }}</td>
        <td>{{ r.motif }}<div class="muted" *ngIf="r.commentaire" style="font-size:11px">Gérant : {{ r.commentaire }}</div></td>
        <td class="num">{{ r.montant | number:'1.2-2' }}</td>
        <td><span [class]="r.statut | badge">{{ r.statut | lab:statuts }}</span></td>
        <td class="right">
          <ng-container *ngIf="auth.hasRole('Gerant') && r.statut==='Demandee'">
            <button class="btn small" (click)="decider(r,true)">Valider</button>
            <button class="btn danger small" style="margin-left:4px" (click)="decider(r,false)">Rejeter</button></ng-container>
          <button class="btn small" *ngIf="r.statut==='Validee'" (click)="payer(r)">Payer</button></td></tr></table>
    <div class="muted" *ngIf="!list.length">Aucune demande.</div></div>`
})
export class RemboursementsComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  statuts = STATUTS_REMB; types = TYPES_REMB;
  clients: any[] = []; contrats: any[] = []; list: any[] = []; clientId: number | null = null; fStatut = ''; error = '';
  form: any = { contratId: null, type: 'Desistement', montant: null, motif: '' };
  ngOnInit() { this.api.get<any[]>('clients').subscribe(r => this.clients = r); this.load(); }
  load() { this.api.get<any[]>('remboursements', { statut: this.fStatut }).subscribe(r => this.list = r); }
  choisirClient() { this.form.contratId = null; this.contrats = []; if (this.clientId) this.api.get<any[]>('contrats', { clientId: this.clientId }).subscribe(r => this.contrats = r); }
  creer() {
    this.error = '';
    this.api.post('remboursements', { ...this.form, montant: +this.form.montant }).subscribe({
      next: () => { this.form = { contratId: null, type: 'Desistement', montant: null, motif: '' }; this.load(); },
      error: e => this.error = ApiService.err(e) });
  }
  decider(r: any, valider: boolean) {
    const commentaire = prompt(valider ? 'Commentaire (facultatif) :' : 'Motif du rejet :') ?? '';
    if (!valider && !commentaire) return;
    this.api.post(`remboursements/${r.id}/decision`, { valider, commentaire }).subscribe({ next: () => this.load(), error: e => this.error = ApiService.err(e) });
  }
  payer(r: any) {
    const ref = prompt('Référence du chèque / virement (laisser vide si paiement en espèces) :') ;
    if (ref === null) return;
    this.api.post(`remboursements/${r.id}/payer`, { mode: ref ? 'Bancaire' : 'Especes', reference: ref || null })
      .subscribe({ next: () => this.load(), error: e => this.error = ApiService.err(e) });
  }
}
