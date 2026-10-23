import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { today } from '../core/models';

@Component({
  selector: 'app-technique', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <div class="row"><h1>Service technique — suivi des chantiers</h1><span class="spacer"></span>
    <a class="btn secondary" routerLink="/taches/Technique">Voir les tâches techniques →</a></div>
  <div class="row" style="margin-bottom:14px">
    <button class="btn" [class.secondary]="tab!=='synthese'" (click)="tab='synthese'">Synthèse des chantiers</button>
    <button class="btn" [class.secondary]="tab!=='saisie'" (click)="tab='saisie'">Avancement des travaux</button></div>

  <div class="card" *ngIf="tab==='synthese'">
    <table><tr><th>Chantier</th><th>Statut</th><th style="width:220px">Avancement</th><th>Phase</th><th>Dernière mise à jour</th><th class="num">Tâches ouvertes</th><th class="num">En retard</th></tr>
      <tr *ngFor="let c of synthese"><td><b>{{ c.nom }}</b></td><td><span class="badge">{{ c.statut }}</span></td>
        <td><div class="bar"><div [style.width.%]="c.pourcentage"></div></div><small>{{ c.pourcentage }} %</small></td>
        <td>{{ c.phase }}</td><td>{{ c.derniereMaj | date:'dd/MM/yyyy' }}</td>
        <td class="num">{{ c.tachesOuvertes }}</td><td class="num"><span class="badge danger" *ngIf="c.tachesEnRetard">{{ c.tachesEnRetard }}</span><span *ngIf="!c.tachesEnRetard">0</span></td></tr></table>
  </div>

  <ng-container *ngIf="tab==='saisie'">
    <div class="card"><h2>Nouvelle mise à jour d'avancement</h2>
      <div class="form-grid">
        <div><label>Chantier *</label><select [(ngModel)]="form.chantierId"><option [ngValue]="null">— choisir —</option><option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
        <div><label>Date</label><input type="date" [(ngModel)]="form.date"></div>
        <div><label>Phase / lot de travaux *</label><input [(ngModel)]="form.phase" placeholder="Gros œuvre, Étanchéité, Finitions…"></div>
        <div><label>Avancement global (%)</label><input type="number" min="0" max="100" [(ngModel)]="form.pourcentage"></div>
        <div style="grid-column:1/-1"><label>Observations</label><input [(ngModel)]="form.observations"></div></div>
      <div class="alert err" *ngIf="error">{{ error }}</div>
      <div style="margin-top:10px"><button class="btn" [disabled]="!form.chantierId || !form.phase" (click)="save()">Enregistrer</button></div></div>
    <div class="card"><div class="row" style="margin-bottom:10px"><select style="width:240px" [(ngModel)]="filtre" (ngModelChange)="load()">
      <option [ngValue]="null">Tous les chantiers</option><option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <table><tr><th>Date</th><th>Chantier</th><th>Phase</th><th class="num">%</th><th>Observations</th></tr>
        <tr *ngFor="let a of list"><td>{{ a.date | date:'dd/MM/yyyy' }}</td><td>{{ a.chantierNom }}</td><td>{{ a.phase }}</td><td class="num">{{ a.pourcentage }}</td><td>{{ a.observations }}</td></tr></table></div>
  </ng-container>`
})
export class TechniqueComponent implements OnInit {
  private api = inject(ApiService);
  tab: 'synthese' | 'saisie' = 'synthese';
  synthese: any[] = []; list: any[] = []; chantiers: any[] = []; filtre: number | null = null; error = '';
  form: any = { chantierId: null, date: today(), phase: '', pourcentage: 0, observations: '' };
  ngOnInit() { this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r); this.loadSynthese(); this.load(); }
  loadSynthese() { this.api.get<any[]>('technique/synthese').subscribe(r => this.synthese = r); }
  load() { this.api.get<any[]>('avancements', { chantierId: this.filtre }).subscribe(r => this.list = r); }
  save() {
    this.error = '';
    this.api.post('avancements', { ...this.form, pourcentage: +this.form.pourcentage }).subscribe({
      next: () => { this.form = { chantierId: null, date: today(), phase: '', pourcentage: 0, observations: '' }; this.load(); this.loadSynthese(); },
      error: e => this.error = ApiService.err(e) });
  }
}
