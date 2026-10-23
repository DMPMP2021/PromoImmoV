import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { LabPipe, BadgePipe } from '../core/lab.pipe';
import { STATUTS_LOT, today } from '../core/models';

@Component({
  selector: 'app-lots', standalone: true, imports: [CommonModule, FormsModule, LabPipe, BadgePipe],
  template: `
  <h1>Gestion des lots à vendre</h1>
  <div class="row" style="margin-bottom:14px">
    <button class="btn" [class.secondary]="tab!=='synthese'" (click)="tab='synthese'">Synthèse commerciale</button>
    <button class="btn" [class.secondary]="tab!=='tableau'" (click)="tab='tableau'">Tableau des lots</button>
    <button class="btn" *ngIf="auth.hasRole('Gerant')" [class.secondary]="tab!=='ajout'" (click)="tab='ajout'">Ajouter des lots</button></div>
  <div class="alert err" *ngIf="error">{{ error }}</div><div class="alert ok" *ngIf="ok">{{ ok }}</div>

  <div class="grid grid-2" *ngIf="tab==='synthese'">
    <div class="card" *ngFor="let s of synth">
      <div class="row"><h2 style="margin:0">{{ s.nom }}</h2><span class="spacer"></span><b>{{ s.tauxCommercialisation }} % commercialisé</b></div>
      <div class="bar" style="display:flex;margin:10px 0">
        <div [style.width.%]="s.tauxVente" style="background:var(--ok)"></div>
        <div [style.width.%]="s.tauxReserve" style="background:var(--warn)"></div></div>
      <div class="grid grid-4">
        <div><div class="stat-label">Total</div><b>{{ s.total }}</b></div>
        <div><div class="stat-label">Disponibles</div><b>{{ s.disponibles }}</b></div>
        <div><div class="stat-label">Réservés</div><b>{{ s.reserves }}</b></div>
        <div><div class="stat-label">Vendus</div><b class="pos">{{ s.vendus }}</b></div></div>
      <div class="muted" style="margin-top:10px">Stock à vendre : {{ s.valeurStock | number:'1.0-0' }} DA — Déjà vendu : {{ s.chiffreVendu | number:'1.0-0' }} DA</div>
    </div>
    <div class="muted" *ngIf="!synth.length">Aucun chantier.</div>
  </div>

  <ng-container *ngIf="tab==='tableau'">
    <div class="card" *ngIf="panel">
      <h2>{{ panel.type==='reserver' ? 'Réserver le lot' : 'Créer le contrat de vente' }} {{ panel.lot.reference }} — {{ panel.lot.chantierNom }}</h2>
      <div class="form-grid">
        <div><label>Client *</label><select [(ngModel)]="panel.clientId"><option [ngValue]="null">— choisir —</option>
          <option *ngFor="let c of clients" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div *ngIf="panel.type==='reserver'"><label>Durée de réservation (jours)</label><input type="number" min="1" [(ngModel)]="panel.jours"></div>
        <ng-container *ngIf="panel.type==='contrat'">
          <div><label>Prix de vente (DA)</label><input type="number" min="0" [(ngModel)]="panel.prix"></div>
          <div><label>Nombre de tranches</label><input type="number" min="1" [(ngModel)]="panel.tranches"></div>
          <div><label>Date 1ʳᵉ tranche</label><input type="date" [(ngModel)]="panel.date"></div>
          <div><label>Intervalle (mois)</label><input type="number" min="1" max="12" [(ngModel)]="panel.intervalle"></div></ng-container></div>
      <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!panel.clientId" (click)="confirmer()">Confirmer</button>
        <button class="btn secondary" (click)="panel=null">Annuler</button></div></div>

    <div class="card">
      <div class="row" style="margin-bottom:10px">
        <select style="width:230px" [(ngModel)]="fChantier" (ngModelChange)="load()"><option [ngValue]="null">Tous les chantiers</option>
          <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select>
        <select style="width:180px" [(ngModel)]="fStatut" (ngModelChange)="load()"><option value="">Tous les statuts</option>
          <option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
      <table><tr><th>Chantier</th><th>Lot</th><th>Type</th><th>Étage</th><th class="num">Surface m²</th><th class="num">Prix</th><th>Statut</th><th>Client</th><th></th></tr>
        <tr *ngFor="let l of lots"><td>{{ l.chantierNom }}</td><td><b>{{ l.reference }}</b></td><td>{{ l.typeBien }}</td><td>{{ l.etage }}</td>
          <td class="num">{{ l.surface | number:'1.0-2' }}</td><td class="num">{{ l.prix | number:'1.0-0' }}</td>
          <td><span [class]="l.statut | badge">{{ l.statut | lab:statuts }}</span>
            <span class="badge danger" *ngIf="l.expiree">Réservation expirée</span>
            <div class="muted" style="font-size:11px" *ngIf="l.statut==='Reserve'">jusqu'au {{ l.dateFinReservation | date:'dd/MM/yyyy' }}</div></td>
          <td>{{ l.clientNom }}<div class="muted" style="font-size:11px" *ngIf="l.contratId">Contrat n°{{ l.contratId }}</div></td>
          <td class="right"><div style="display:flex;gap:4px;justify-content:flex-end;flex-wrap:wrap">
            <button class="btn small" *ngIf="l.statut==='Disponible'" (click)="ouvrir(l,'reserver')">Réserver</button>
            <button class="btn small" *ngIf="l.statut!=='Vendu'" (click)="ouvrir(l,'contrat')">Créer le contrat</button>
            <button class="btn secondary small" *ngIf="l.statut==='Reserve'" (click)="liberer(l)">Libérer</button>
            <button class="btn secondary small" *ngIf="auth.hasRole('Gerant')" (click)="editer(l)">Modifier</button>
            <button class="btn danger small" *ngIf="auth.hasRole('Gerant') && l.statut==='Disponible'" (click)="supprimer(l)">Suppr.</button></div></td></tr></table>
      <div class="muted" *ngIf="!lots.length">Aucun lot.</div>
    </div>
  </ng-container>

  <ng-container *ngIf="tab==='ajout' && auth.hasRole('Gerant')">
    <div class="card"><h2>{{ form.id ? 'Modifier le lot' : 'Nouveau lot' }}</h2>
      <div class="form-grid">
        <div><label>Chantier *</label><select [(ngModel)]="form.chantierId" [disabled]="!!form.id"><option [ngValue]="null">— choisir —</option>
          <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div><label>Référence *</label><input [(ngModel)]="form.reference" placeholder="A-12"></div>
        <div><label>Type</label><input [(ngModel)]="form.typeBien" placeholder="F3, F4, local…"></div>
        <div><label>Étage</label><input [(ngModel)]="form.etage"></div>
        <div><label>Surface (m²)</label><input type="number" min="0" [(ngModel)]="form.surface"></div>
        <div><label>Prix (DA)</label><input type="number" min="0" [(ngModel)]="form.prix"></div>
        <div style="grid-column:1/-1"><label>Notes</label><input [(ngModel)]="form.notes"></div></div>
      <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!form.chantierId || !form.reference" (click)="save()">Enregistrer</button>
        <button class="btn secondary" *ngIf="form.id" (click)="form=blank()">Annuler</button></div></div>
    <div class="card"><h2>Création en série</h2>
      <div class="muted" style="margin-bottom:8px">Exemple : préfixe « A- », de 1 à 20 → lots A-1, A-2 … A-20 (les références déjà existantes sont ignorées).</div>
      <div class="form-grid">
        <div><label>Chantier *</label><select [(ngModel)]="serie.chantierId"><option [ngValue]="null">— choisir —</option>
          <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div><label>Préfixe</label><input [(ngModel)]="serie.prefixe"></div>
        <div><label>De</label><input type="number" [(ngModel)]="serie.de"></div><div><label>À</label><input type="number" [(ngModel)]="serie.a"></div>
        <div><label>Type</label><input [(ngModel)]="serie.typeBien"></div>
        <div><label>Surface (m²)</label><input type="number" [(ngModel)]="serie.surface"></div>
        <div><label>Prix (DA)</label><input type="number" [(ngModel)]="serie.prix"></div></div>
      <div style="margin-top:10px"><button class="btn" [disabled]="!serie.chantierId" (click)="saveSerie()">Créer la série</button></div></div>
  </ng-container>`
})
export class LotsComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  statuts = STATUTS_LOT;
  tab: 'synthese' | 'tableau' | 'ajout' = 'synthese';
  synth: any[] = []; lots: any[] = []; chantiers: any[] = []; clients: any[] = [];
  fChantier: number | null = null; fStatut = ''; error = ''; ok = '';
  panel: any = null;
  form: any = this.blank();
  serie: any = { chantierId: null, prefixe: 'A-', de: 1, a: 10, typeBien: '', surface: 0, prix: 0 };

  blank() { return { id: null, chantierId: null, reference: '', typeBien: '', etage: '', surface: 0, prix: 0, notes: '' }; }

  ngOnInit() {
    this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r);
    this.api.get<any[]>('clients').subscribe(r => this.clients = r);
    this.refresh();
  }
  refresh() { this.loadSynth(); this.load(); }
  loadSynth() { this.api.get<any[]>('lots/synthese').subscribe({ next: r => this.synth = r, error: e => this.error = ApiService.err(e) }); }
  load() { this.api.get<any[]>('lots', { chantierId: this.fChantier, statut: this.fStatut }).subscribe({ next: r => this.lots = r, error: e => this.error = ApiService.err(e) }); }

  private done(msg: string) { this.ok = msg; this.error = ''; this.panel = null; this.refresh(); }
  private fail(e: any) { this.error = ApiService.err(e); this.ok = ''; }

  ouvrir(l: any, type: string) {
    this.ok = ''; this.error = '';
    this.panel = { type, lot: l, clientId: l.clientId || null, jours: 15, prix: l.prix, tranches: 4, date: today(), intervalle: 1 };
  }
  confirmer() {
    const p = this.panel;
    if (p.type === 'reserver')
      this.api.post(`lots/${p.lot.id}/reserver`, { clientId: p.clientId, jours: +p.jours }).subscribe({ next: () => this.done('Lot réservé.'), error: e => this.fail(e) });
    else
      this.api.post<any>(`lots/${p.lot.id}/contrat`, { clientId: p.clientId, prix: +p.prix, nombreTranches: +p.tranches, datePremiere: p.date, intervalleMois: +p.intervalle })
        .subscribe({ next: r => this.done(`Contrat n°${r.id} créé : le lot est vendu et l'échéancier est prêt dans Recette / Contrats.`), error: e => this.fail(e) });
  }
  liberer(l: any) {
    if (!confirm(`Libérer le lot ${l.reference} ?`)) return;
    this.api.post(`lots/${l.id}/liberer`, {}).subscribe({ next: () => this.done('Lot libéré.'), error: e => this.fail(e) });
  }
  supprimer(l: any) {
    if (!confirm(`Supprimer le lot ${l.reference} ?`)) return;
    this.api.delete(`lots/${l.id}`).subscribe({ next: () => this.done('Lot supprimé.'), error: e => this.fail(e) });
  }
  editer(l: any) { this.form = { id: l.id, chantierId: l.chantierId, reference: l.reference, typeBien: l.typeBien, etage: l.etage, surface: l.surface, prix: l.prix, notes: l.notes }; this.tab = 'ajout'; }
  save() {
    const body = { ...this.form, surface: +this.form.surface, prix: +this.form.prix };
    const req = this.form.id ? this.api.put(`lots/${this.form.id}`, body) : this.api.post('lots', body);
    req.subscribe({ next: () => { this.form = this.blank(); this.done('Lot enregistré.'); }, error: e => this.fail(e) });
  }
  saveSerie() {
    this.api.post<any>('lots/serie', { ...this.serie, de: +this.serie.de, a: +this.serie.a, surface: +this.serie.surface, prix: +this.serie.prix })
      .subscribe({ next: r => this.done(`${r.crees} lot(s) créé(s).`), error: e => this.fail(e) });
  }
}
