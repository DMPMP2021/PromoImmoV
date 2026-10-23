import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { BadgePipe } from '../core/lab.pipe';
import { STATUTS_COMMANDE } from '../core/models';

@Component({
  selector: 'app-achats', standalone: true, imports: [CommonModule, FormsModule, RouterLink, BadgePipe],
  template: `
  <div class="row"><h1>Achats et approvisionnement</h1><span class="spacer"></span>
    <a class="btn secondary" routerLink="/taches/Achats">Voir les tâches →</a></div>
  <div class="row" style="margin-bottom:14px">
    <button class="btn" [class.secondary]="tab!=='commandes'" (click)="tab='commandes'">Commandes</button>
    <button class="btn" [class.secondary]="tab!=='fournisseurs'" (click)="tab='fournisseurs'">Fournisseurs</button></div>
  <div class="alert err" *ngIf="error">{{ error }}</div>

  <ng-container *ngIf="tab==='commandes'">
    <div class="grid grid-3">
      <div class="card"><div class="stat-label">Commandes en cours</div><div class="stat">{{ enCours }}</div></div>
      <div class="card"><div class="stat-label">Montant en cours</div><div class="stat">{{ montantEnCours | number:'1.0-0' }} DA</div></div>
      <div class="card"><div class="stat-label">Total des achats</div><div class="stat">{{ total | number:'1.0-0' }} DA</div></div></div>
    <div class="card"><h2>Nouvelle commande / demande d'achat</h2>
      <div class="form-grid">
        <div><label>Fournisseur *</label><select [(ngModel)]="cmd.fournisseurId"><option [ngValue]="null">— choisir —</option><option *ngFor="let f of fournisseurs" [ngValue]="f.id">{{ f.nom }}</option></select></div>
        <div><label>Chantier destinataire</label><select [(ngModel)]="cmd.chantierId"><option [ngValue]="null">— stock / général —</option><option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div style="grid-column:span 2"><label>Objet / article *</label><input [(ngModel)]="cmd.objet" placeholder="Ciment CPJ 42,5 — sacs de 50 kg"></div>
        <div><label>Quantité</label><input type="number" min="0" [(ngModel)]="cmd.quantite"></div>
        <div><label>Unité</label><input [(ngModel)]="cmd.unite"></div>
        <div><label>Montant total (DA)</label><input type="number" min="0" [(ngModel)]="cmd.montant"></div>
        <div><label>Livraison prévue</label><input type="date" [(ngModel)]="cmd.dateLivraisonPrevue"></div></div>
      <div style="margin-top:10px"><button class="btn" [disabled]="!cmd.fournisseurId || !cmd.objet" (click)="addCmd()">Enregistrer</button></div></div>
    <div class="card"><div class="row" style="margin-bottom:10px"><select style="width:200px" [(ngModel)]="fStatut" (ngModelChange)="loadCmd()">
      <option value="">Tous les statuts</option><option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
      <table><tr><th>Date</th><th>Fournisseur</th><th>Objet</th><th>Chantier</th><th class="num">Qté</th><th class="num">Montant</th><th>Livraison</th><th>Statut</th></tr>
        <tr *ngFor="let c of commandes"><td>{{ c.dateCommande | date:'dd/MM/yyyy' }}</td><td>{{ c.fournisseurNom }}</td><td>{{ c.objet }}</td><td>{{ c.chantierNom }}</td>
          <td class="num">{{ c.quantite }} {{ c.unite }}</td><td class="num">{{ c.montant | number:'1.2-2' }}</td>
          <td>{{ (c.dateLivraison || c.dateLivraisonPrevue) | date:'dd/MM/yyyy' }}</td>
          <td><ng-container *ngIf="c.statut==='AValider'"><span class="badge warn">À valider (gérant)</span>
            <span *ngIf="auth.hasRole('Gerant')"> <button class="btn small" (click)="decider(c,true)">Accepter</button> <button class="btn danger small" (click)="decider(c,false)">Refuser</button></span></ng-container>
          <select *ngIf="c.statut!=='AValider'" [ngModel]="c.statut" (ngModelChange)="setStatut(c,$event)"><option *ngFor="let s of statutsEdit" [value]="s.v">{{ s.l }}</option></select></td></tr></table></div>
  </ng-container>

  <ng-container *ngIf="tab==='fournisseurs'">
    <div class="card"><h2>{{ four.id ? 'Modifier' : 'Nouveau' }} fournisseur</h2>
      <div class="form-grid"><div><label>Nom *</label><input [(ngModel)]="four.nom"></div>
        <div><label>Téléphone</label><input [(ngModel)]="four.telephone"></div><div><label>Adresse</label><input [(ngModel)]="four.adresse"></div></div>
      <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!four.nom" (click)="saveFour()">Enregistrer</button>
        <button class="btn secondary" *ngIf="four.id" (click)="four={}">Annuler</button></div></div>
    <div class="card"><table><tr><th>Nom</th><th>Téléphone</th><th>Adresse</th><th></th></tr>
      <tr *ngFor="let f of fournisseurs"><td>{{ f.nom }}</td><td>{{ f.telephone }}</td><td>{{ f.adresse }}</td>
        <td class="right"><button class="btn secondary small" (click)="editFour(f)">Modifier</button></td></tr></table></div>
  </ng-container>`
})
export class AchatsComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  tab: 'commandes' | 'fournisseurs' = 'commandes';
  statuts = STATUTS_COMMANDE; statutsEdit = STATUTS_COMMANDE.filter(s => s.v !== 'AValider'); fournisseurs: any[] = []; commandes: any[] = []; chantiers: any[] = []; fStatut = ''; error = '';
  cmd: any = this.blank(); four: any = {};
  blank() { return { fournisseurId: null, chantierId: null, objet: '', quantite: 1, unite: 'u', montant: 0, dateLivraisonPrevue: '' }; }
  get enCours() { return this.commandes.filter(c => c.statut === 'Demandee' || c.statut === 'Commandee').length; }
  get montantEnCours() { return this.commandes.filter(c => c.statut === 'Demandee' || c.statut === 'Commandee').reduce((s, c) => s + c.montant, 0); }
  get total() { return this.commandes.filter(c => c.statut !== 'Annulee').reduce((s, c) => s + c.montant, 0); }

  ngOnInit() { this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r); this.loadFour(); this.loadCmd(); }
  loadFour() { this.api.get<any[]>('fournisseurs').subscribe({ next: r => this.fournisseurs = r, error: e => this.error = ApiService.err(e) }); }
  loadCmd() { this.api.get<any[]>('commandes', { statut: this.fStatut }).subscribe({ next: r => this.commandes = r, error: e => this.error = ApiService.err(e) }); }
  addCmd() {
    this.error = '';
    this.api.post<any>('commandes', { ...this.cmd, quantite: +this.cmd.quantite, montant: +this.cmd.montant, dateLivraisonPrevue: this.cmd.dateLivraisonPrevue || null })
      .subscribe({ next: r => { this.cmd = this.blank(); this.loadCmd(); if (r.aValider) this.error = 'Montant supérieur au seuil : la commande attend la validation du gérant.'; }, error: e => this.error = ApiService.err(e) });
  }
  decider(c: any, accepter: boolean) { this.api.post(`commandes/${c.id}/decision`, { accepter }).subscribe({ next: () => this.loadCmd(), error: e => this.error = ApiService.err(e) }); }
  setStatut(c: any, s: string) { this.api.put(`commandes/${c.id}/statut`, { statut: s }).subscribe({ next: () => this.loadCmd(), error: e => this.error = ApiService.err(e) }); }
  editFour(f: any) { this.four = { ...f }; window.scrollTo(0, 0); }
  saveFour() {
    const req = this.four.id ? this.api.put(`fournisseurs/${this.four.id}`, this.four) : this.api.post('fournisseurs', this.four);
    req.subscribe({ next: () => { this.four = {}; this.loadFour(); }, error: e => this.error = ApiService.err(e) });
  }
}
