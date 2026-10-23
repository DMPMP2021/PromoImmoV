import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { STATUTS_POINTAGE, today } from '../core/models';

@Component({
  selector: 'app-pointage', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <div class="card">
    <div class="row">
      <div><label>Date</label><input type="date" [(ngModel)]="date" (ngModelChange)="load()"></div>
      <div><label>Chantier</label><select [(ngModel)]="chantierId" (ngModelChange)="load()"><option [ngValue]="null">Tous (selon affectation)</option>
        <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <span class="spacer"></span><div>Présents : <b>{{ presents }}</b> / {{ rows.length }}</div></div>
    <table style="margin-top:10px"><tr><th>Employé</th><th>Poste</th><th>Statut</th><th style="width:90px">Heures</th><th>Remarque</th></tr>
      <tr *ngFor="let r of rows"><td>{{ r.nom }} <span class="badge ok" *ngIf="r.pointe">pointé</span></td><td class="muted">{{ r.poste }}</td>
        <td><select [(ngModel)]="r.statut"><option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></td>
        <td><input type="number" min="0" max="24" [(ngModel)]="r.heures"></td><td><input [(ngModel)]="r.remarque"></td></tr></table>
    <div class="muted" *ngIf="!rows.length">Aucun employé actif pour ce filtre.</div>
    <div class="alert err" *ngIf="error">{{ error }}</div><div class="alert ok" *ngIf="ok">{{ ok }}</div>
    <div style="margin-top:10px"><button class="btn" [disabled]="!rows.length" (click)="save()">Enregistrer le pointage</button></div>
  </div>`
})
export class PointageComponent implements OnInit {
  private api = inject(ApiService);
  statuts = STATUTS_POINTAGE; date = today(); chantierId: number | null = null;
  chantiers: any[] = []; rows: any[] = []; error = ''; ok = '';
  get presents() { return this.rows.filter(r => r.statut === 'Present' || r.statut === 'Retard').length; }
  ngOnInit() { this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r); this.load(); }
  load() {
    this.ok = ''; this.error = '';
    this.api.get<any[]>('pointages/feuille', { date: this.date, chantierId: this.chantierId })
      .subscribe({ next: r => this.rows = r.map(x => ({ ...x, statut: x.statut || 'Present', heures: x.heures ?? 8 })), error: e => this.error = ApiService.err(e) });
  }
  save() {
    this.ok = ''; this.error = '';
    this.api.post('pointages/feuille', { date: this.date, chantierId: this.chantierId,
      lignes: this.rows.map(r => ({ employeId: r.employeId, statut: r.statut, heures: +r.heures, remarque: r.remarque || null })) })
      .subscribe({ next: () => { this.ok = 'Pointage enregistré.'; this.load(); this.ok = 'Pointage enregistré.'; }, error: e => this.error = ApiService.err(e) });
  }
}
