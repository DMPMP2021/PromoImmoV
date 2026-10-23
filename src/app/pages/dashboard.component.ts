import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { LabPipe } from '../core/lab.pipe';
import { SERVICES } from '../core/models';

@Component({
  selector: 'app-dashboard', standalone: true, imports: [CommonModule, RouterLink, LabPipe],
  template: `
  <h1>Tableau de bord</h1>

  <div class="card" *ngIf="alertes.length">
    <h2>🔔 Alertes ({{ alertes.length }})</h2>
    <div *ngFor="let a of alertes" class="alert" [class.err]="a.niveau==='danger'" [class.warn]="a.niveau==='warn'" style="margin:6px 0">
      <a [routerLink]="a.lien">{{ a.message }}</a></div>
  </div>

  <div class="grid grid-2" *ngIf="gr">
    <div class="card"><h2>Encaissements des 12 derniers mois</h2>
      <div style="display:flex;align-items:flex-end;gap:6px;height:170px">
        <div *ngFor="let m of gr.encaissementsParMois" style="flex:1;display:flex;flex-direction:column;justify-content:flex-end;height:100%;text-align:center">
          <div class="muted" style="font-size:9px">{{ m.total | number:'1.0-0' }}</div>
          <div [style.height.%]="pct(m.total, maxMois)" style="background:var(--primary);border-radius:3px 3px 0 0;min-height:2px"></div>
          <div class="muted" style="font-size:10px">{{ m.mois.slice(5) }}</div></div></div></div>
    <div class="card"><h2>Répartition des dépenses</h2>
      <div *ngFor="let c of gr.depenses" style="margin-bottom:9px">
        <div class="row"><span>{{ c.categorie }}</span><span class="spacer"></span><b>{{ c.montant | number:'1.0-0' }} DA</b></div>
        <div class="bar"><div [style.width.%]="pct(c.montant, maxDep)"></div></div></div></div>
  </div>

  <div class="alert err" *ngIf="g && (g.obligationsEnRetard || g.reclamationsNouvelles || g.remboursementsAValider)">
    <b>À traiter :</b>
    <span *ngIf="g.reclamationsNouvelles"> · <a routerLink="/reclamations">{{ g.reclamationsNouvelles }} réclamation(s) nouvelle(s)</a></span>
    <span *ngIf="g.remboursementsAValider"> · <a routerLink="/remboursements">{{ g.remboursementsAValider }} remboursement(s) à valider</a></span>
    <span *ngIf="g.obligationsEnRetard"> · <a routerLink="/fiscal">{{ g.obligationsEnRetard }} obligation(s) fiscale(s) en retard</a></span></div>

  <h2>Finances</h2>
  <div class="grid grid-4" *ngIf="d">
    <div class="card"><div class="stat-label">Total encaissé</div><div class="stat pos">{{ d.totalEncaisse | number:'1.0-0' }} DA</div></div>
    <div class="card"><div class="stat-label">Total alloué (budgets)</div><div class="stat">{{ d.totalAlloue | number:'1.0-0' }} DA</div></div>
    <div class="card"><div class="stat-label">Trésorerie disponible</div><div class="stat" [class.neg]="d.tresorerie<0">{{ d.tresorerie | number:'1.0-0' }} DA</div>
      <div class="muted" style="font-size:11px">après remboursements ({{ d.remboursementsPayes | number:'1.0-0' }} DA) et budgets</div></div>
    <div class="card"><div class="stat-label">Reste à collecter</div><div class="stat">{{ d.resteACollecter | number:'1.0-0' }} DA</div></div>
  </div>

  <ng-container *ngIf="g">
    <h2>Activité des services</h2>
    <div class="grid grid-4">
      <a class="card" routerLink="/reclamations" style="text-decoration:none;color:inherit"><div class="stat-label">📩 Réclamations nouvelles</div><div class="stat" [class.neg]="g.reclamationsNouvelles>0">{{ g.reclamationsNouvelles }}</div><div class="muted">{{ g.reclamationsEnCours }} en cours</div></a>
      <a class="card" routerLink="/remboursements" style="text-decoration:none;color:inherit"><div class="stat-label">💸 Remboursements</div><div class="stat">{{ g.remboursementsAValider }} à valider</div><div class="muted">{{ g.remboursementsAPayer }} à payer ({{ g.montantRemboursementsAPayer | number:'1.0-0' }} DA)</div></a>
      <a class="card" routerLink="/achats" style="text-decoration:none;color:inherit"><div class="stat-label">🛒 Commandes en cours</div><div class="stat">{{ g.commandesEnCours }}</div><div class="muted">{{ g.montantCommandesEnCours | number:'1.0-0' }} DA — total achats {{ g.achatsTotal | number:'1.0-0' }}</div></a>
      <a class="card" routerLink="/fiscal" style="text-decoration:none;color:inherit"><div class="stat-label">🏛️ Fiscal / parafiscal</div><div class="stat" [class.neg]="g.obligationsEnRetard>0">{{ g.obligationsAPayer }} à payer</div><div class="muted">{{ g.montantObligationsAPayer | number:'1.0-0' }} DA — {{ g.obligationsEnRetard }} en retard, {{ g.obligationsSous7Jours }} sous 7 j</div></a>
      <a class="card" routerLink="/personnel" style="text-decoration:none;color:inherit"><div class="stat-label">👷 Présents aujourd'hui</div><div class="stat pos">{{ g.presentsAujourdhui }} / {{ g.effectifActif }}</div><div class="muted">{{ g.absentsAujourdhui }} absents, {{ g.nonPointes }} non pointés</div></a>
      <a class="card" routerLink="/suivi" style="text-decoration:none;color:inherit"><div class="stat-label">📅 Missions aujourd'hui</div><div class="stat">{{ g.missionsAujourdhui }}</div></a>
      <a class="card" routerLink="/personnel" style="text-decoration:none;color:inherit"><div class="stat-label">💰 Paiements personnel (mois)</div><div class="stat">{{ g.paiementsPersonnelMois | number:'1.0-0' }} DA</div></a>
      <a class="card" routerLink="/divers" style="text-decoration:none;color:inherit"><div class="stat-label">🧾 Dépenses diverses</div><div class="stat">{{ g.depensesDiverses | number:'1.0-0' }} DA</div></a>
    </div>

    <div class="grid grid-2">
      <div class="card"><h2>Tâches par service</h2>
        <table><tr><th>Service</th><th class="num">À prévoir</th><th class="num">En cours</th><th class="num">Bloquées</th><th class="num">Terminées</th><th class="num">En retard</th></tr>
          <tr *ngFor="let s of g.parService"><td><a [routerLink]="['/taches', s.service]">{{ s.service | lab:services }}</a></td>
            <td class="num">{{ s.aPrevoir }}</td><td class="num">{{ s.enCours }}</td><td class="num">{{ s.bloque }}</td><td class="num">{{ s.termine }}</td>
            <td class="num"><span class="badge danger" *ngIf="s.enRetard">{{ s.enRetard }}</span><span *ngIf="!s.enRetard">0</span></td></tr></table></div>
      <div class="card"><h2>Avancement des chantiers</h2>
        <div *ngFor="let c of g.avancement" style="margin-bottom:12px"><div class="row"><b>{{ c.nom }}</b><span class="spacer"></span><span>{{ c.pourcentage }} %</span></div>
          <div class="bar"><div [style.width.%]="c.pourcentage"></div></div><div class="muted" style="font-size:12px" *ngIf="c.phase">Phase : {{ c.phase }}</div></div>
        <div class="muted" *ngIf="!g.avancement.length">Aucun chantier.</div></div>
    </div>
  </ng-container>

  <div class="grid grid-2" *ngIf="d">
    <div class="card"><h2>Finances par chantier</h2>
      <table><tr><th>Chantier</th><th class="num">Encaissé</th><th class="num">Alloué</th><th class="num">Solde</th></tr>
        <tr *ngFor="let c of d.parChantier"><td>{{ c.nom }}</td><td class="num">{{ c.encaisse | number:'1.0-0' }}</td>
          <td class="num">{{ c.alloue | number:'1.0-0' }}</td><td class="num" [class.neg]="c.encaisse-c.alloue<0">{{ c.encaisse-c.alloue | number:'1.0-0' }}</td></tr></table></div>
    <div class="card"><h2>Budgets par service</h2>
      <table><tr><th>Service</th><th class="num">Alloué</th></tr>
        <tr *ngFor="let s of d.parService"><td>{{ s.service }}</td><td class="num">{{ s.montant | number:'1.0-0' }}</td></tr></table>
      <div class="muted" *ngIf="!d.parService.length">Aucune allocation.</div>
      <h3>Dépenses matériel payées</h3><div class="stat">{{ d.depensesMateriel | number:'1.0-0' }} DA</div></div>
  </div>

  <div class="card" *ngIf="recents.length"><h2>Derniers encaissements</h2>
    <table><tr><th>Date</th><th>Reçu</th><th>Client</th><th>Chantier</th><th>Mode</th><th class="num">Montant</th></tr>
      <tr *ngFor="let r of recents.slice(0,8)" [class.strike]="r.annule"><td>{{ r.date | date:'dd/MM/yyyy HH:mm' }}</td><td>{{ r.numeroRecu }}</td>
        <td>{{ r.clientNom }}</td><td>{{ r.chantierNom }}</td><td>{{ r.mode }}</td><td class="num">{{ r.montant | number:'1.2-2' }}</td></tr></table></div>`
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiService);
  services = SERVICES; d: any; g: any; recents: any[] = []; alertes: any[] = []; gr: any;
  get maxMois() { return this.gr ? Math.max(1, ...this.gr.encaissementsParMois.map((m: any) => m.total)) : 1; }
  get maxDep() { return this.gr ? Math.max(1, ...this.gr.depenses.map((c: any) => c.montant)) : 1; }
  pct(v: number, max: number) { return max > 0 ? Math.round(v * 100 / max) : 0; }
  ngOnInit() {
    this.api.get<any>('dashboard').subscribe(r => this.d = r);
    this.api.get<any>('dashboard/global').subscribe(r => this.g = r);
    this.api.get<any[]>('recette/recents').subscribe(r => this.recents = r);
    this.api.get<any[]>('alertes').subscribe(r => this.alertes = r);
    this.api.get<any>('stats/graphiques').subscribe(r => this.gr = r);
  }
}
