import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { LabPipe, BadgePipe } from '../core/lab.pipe';
import { STATUTS_MISSION, today } from '../core/models';
import { PointageComponent } from './pointage.component';

@Component({
  selector: 'app-suivi', standalone: true, imports: [CommonModule, FormsModule, LabPipe, BadgePipe, PointageComponent],
  template: `
  <h1>Suivi journalier des travaux et du personnel</h1>
  <div class="row" style="margin-bottom:14px">
    <button class="btn" [class.secondary]="tab!=='journee'" (click)="tab='journee'; loadJournee()">Vue de la journée</button>
    <button class="btn" [class.secondary]="tab!=='rapports'" (click)="tab='rapports'">Rapports journaliers</button>
    <button class="btn" [class.secondary]="tab!=='missions'" (click)="tab='missions'">Programmation des missions</button>
    <button class="btn" [class.secondary]="tab!=='pointage'" (click)="tab='pointage'">Pointage du personnel</button></div>
  <div class="alert err" *ngIf="error">{{ error }}</div>

  <ng-container *ngIf="tab==='journee'">
    <div class="row" style="margin-bottom:12px"><div><label>Date</label><input type="date" [(ngModel)]="jour" (ngModelChange)="loadJournee()"></div></div>
    <div class="grid grid-2"><div class="card" *ngFor="let c of journee">
      <div class="row"><h2 style="margin:0">{{ c.nom }}</h2><span class="spacer"></span>
        <span class="badge ok">{{ c.presents }} présents</span><span class="badge" [class.danger]="c.absents>0">{{ c.absents }} absents</span></div>
      <h3>Travaux réalisés</h3><div *ngIf="c.rapport">{{ c.rapport }}</div>
      <div class="muted" *ngIf="!c.rapport">⚠ Aucun rapport saisi pour cette journée.</div>
      <div class="alert err" *ngIf="c.incidents">Incident : {{ c.incidents }}</div>
      <h3>Missions du jour</h3><div *ngFor="let m of c.missions">• {{ m }}</div><div class="muted" *ngIf="!c.missions.length">Aucune mission programmée.</div></div></div>
  </ng-container>

  <ng-container *ngIf="tab==='rapports'">
    <div class="card"><h2>Rapport journalier de chantier</h2>
      <div class="form-grid">
        <div><label>Chantier *</label><select [(ngModel)]="rap.chantierId"><option [ngValue]="null">— choisir —</option><option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div><label>Date</label><input type="date" [(ngModel)]="rap.date"></div>
        <div><label>Météo</label><input [(ngModel)]="rap.meteo" placeholder="Ensoleillé, pluie…"></div>
        <div style="grid-column:1/-1"><label>Travaux réalisés *</label><textarea rows="3" [(ngModel)]="rap.travaux"></textarea></div>
        <div style="grid-column:1/-1"><label>Incidents / retards</label><input [(ngModel)]="rap.incidents"></div></div>
      <div class="muted" style="margin-top:6px">Un nouveau rapport pour le même chantier et la même date remplace le précédent.</div>
      <div style="margin-top:10px"><button class="btn" [disabled]="!rap.chantierId || !rap.travaux" (click)="saveRap()">Enregistrer</button></div></div>
    <div class="card"><div class="row" style="margin-bottom:10px"><select style="width:240px" [(ngModel)]="fChantier" (ngModelChange)="loadRap()">
      <option [ngValue]="null">Tous les chantiers</option><option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <table><tr><th>Date</th><th>Chantier</th><th>Travaux</th><th>Incidents</th><th>Météo</th></tr>
        <tr *ngFor="let r of rapports"><td>{{ r.date | date:'dd/MM/yyyy' }}</td><td>{{ r.chantierNom }}</td><td>{{ r.travaux }}</td><td>{{ r.incidents }}</td><td>{{ r.meteo }}</td></tr></table></div>
  </ng-container>

  <ng-container *ngIf="tab==='missions'">
    <div class="card"><h2>{{ mis.id ? 'Modifier la mission' : 'Programmer une mission' }}</h2>
      <div class="form-grid">
        <div><label>Chantier *</label><select [(ngModel)]="mis.chantierId"><option [ngValue]="null">— choisir —</option><option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div style="grid-column:span 2"><label>Intitulé *</label><input [(ngModel)]="mis.titre"></div>
        <div><label>Début</label><input type="date" [(ngModel)]="mis.dateDebut"></div><div><label>Fin</label><input type="date" [(ngModel)]="mis.dateFin"></div>
        <div style="grid-column:1/-1"><label>Description</label><input [(ngModel)]="mis.description"></div></div>
      <h3>Personnel affecté</h3>
      <div class="row"><label *ngFor="let e of employes" style="display:flex;gap:6px;align-items:center;margin:0;font-size:13px;color:inherit">
        <input type="checkbox" style="width:auto" [checked]="mis.employeIds.includes(e.id)" (change)="toggle(e.id)"> {{ e.nom }}</label></div>
      <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!mis.chantierId || !mis.titre" (click)="saveMis()">Enregistrer</button>
        <button class="btn secondary" *ngIf="mis.id" (click)="mis=blankMis()">Annuler</button></div></div>
    <div class="card"><table><tr><th>Période</th><th>Chantier</th><th>Mission</th><th>Personnel</th><th>Statut</th><th></th></tr>
      <tr *ngFor="let m of missions"><td>{{ m.dateDebut | date:'dd/MM' }} → {{ m.dateFin | date:'dd/MM/yyyy' }}</td><td>{{ m.chantierNom }}</td>
        <td><b>{{ m.titre }}</b><div class="muted" style="font-size:12px">{{ m.description }}</div></td><td>{{ m.employes.join(', ') }}</td>
        <td><select [ngModel]="m.statut" (ngModelChange)="setStatut(m,$event)"><option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></td>
        <td class="right"><button class="btn secondary small" (click)="editMis(m)">Modifier</button></td></tr></table></div>
  </ng-container>

  <app-pointage *ngIf="tab==='pointage'"></app-pointage>`
})
export class SuiviComponent implements OnInit {
  private api = inject(ApiService);
  tab: 'journee' | 'rapports' | 'missions' | 'pointage' = 'journee';
  statuts = STATUTS_MISSION; chantiers: any[] = []; employes: any[] = []; journee: any[] = []; rapports: any[] = []; missions: any[] = [];
  jour = today(); fChantier: number | null = null; error = '';
  rap: any = { chantierId: null, date: today(), travaux: '', incidents: '', meteo: '' };
  mis: any = this.blankMis();
  blankMis() { return { id: null, chantierId: null, titre: '', description: '', dateDebut: today(), dateFin: today(), statut: 'Planifiee', employeIds: [] as number[] }; }

  ngOnInit() {
    this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r);
    this.api.get<any[]>('employes', { actif: true }).subscribe(r => this.employes = r);
    this.loadJournee(); this.loadRap(); this.loadMis();
  }
  loadJournee() { this.api.get<any[]>('suivi/journee', { date: this.jour }).subscribe({ next: r => this.journee = r, error: e => this.error = ApiService.err(e) }); }
  loadRap() { this.api.get<any[]>('rapports', { chantierId: this.fChantier }).subscribe(r => this.rapports = r); }
  saveRap() {
    this.error = '';
    this.api.post('rapports', this.rap).subscribe({
      next: () => { this.rap = { chantierId: null, date: today(), travaux: '', incidents: '', meteo: '' }; this.loadRap(); this.loadJournee(); },
      error: e => this.error = ApiService.err(e) });
  }
  loadMis() { this.api.get<any[]>('missions').subscribe(r => this.missions = r); }
  toggle(id: number) { const i = this.mis.employeIds.indexOf(id); i >= 0 ? this.mis.employeIds.splice(i, 1) : this.mis.employeIds.push(id); }
  editMis(m: any) { this.mis = { ...m, dateDebut: m.dateDebut.slice(0, 10), dateFin: m.dateFin.slice(0, 10), employeIds: [...m.employeIds] }; window.scrollTo(0, 0); }
  saveMis() {
    this.error = '';
    const req = this.mis.id ? this.api.put(`missions/${this.mis.id}`, this.mis) : this.api.post('missions', this.mis);
    req.subscribe({ next: () => { this.mis = this.blankMis(); this.loadMis(); this.loadJournee(); }, error: e => this.error = ApiService.err(e) });
  }
  setStatut(m: any, s: string) { this.api.put(`missions/${m.id}/statut`, { statut: s }).subscribe({ next: () => this.loadMis(), error: e => this.error = ApiService.err(e) }); }
}
