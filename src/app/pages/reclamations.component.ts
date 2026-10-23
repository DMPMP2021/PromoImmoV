import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { LabPipe, BadgePipe } from '../core/lab.pipe';
import { STATUTS_RECL } from '../core/models';

@Component({
  selector: 'app-reclamations', standalone: true, imports: [CommonModule, FormsModule, LabPipe, BadgePipe],
  template: `
  <div class="row"><h1>Réclamations des clients</h1><span class="spacer"></span>
    <select style="width:200px" [(ngModel)]="fStatut" (ngModelChange)="load()"><option value="">Tous les statuts</option>
      <option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
  <div class="muted" style="margin-bottom:12px">Formulaire public pour vos clients : <b>http://localhost:4200/contact</b></div>
  <div class="alert err" *ngIf="error">{{ error }}</div>
  <div class="card" *ngFor="let r of list">
    <div class="row"><b>RCL-{{ ('00000' + r.id).slice(-5) }}</b><b>{{ r.objet }}</b><span class="spacer"></span>
      <span [class]="r.statut | badge">{{ r.statut | lab:statuts }}</span><span class="muted">{{ r.dateCreation | date:'dd/MM/yyyy HH:mm' }}</span></div>
    <div class="muted" style="margin:4px 0 8px">{{ r.nom }} <span *ngIf="r.telephone">· {{ r.telephone }}</span> <span *ngIf="r.email">· {{ r.email }}</span>
      <span *ngIf="r.contratRef">· Contrat/Lot : {{ r.contratRef }}</span></div>
    <div style="white-space:pre-wrap">{{ r.message }}</div>
    <div class="alert ok" *ngIf="r.reponse"><b>Réponse ({{ r.traiteePar }}) :</b> {{ r.reponse }}</div>
    <div *ngIf="open===r.id" style="margin-top:10px">
      <div class="form-grid"><div><label>Nouveau statut</label><select [(ngModel)]="edit.statut"><option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
        <div style="grid-column:span 3"><label>Réponse / suite donnée</label><input [(ngModel)]="edit.reponse"></div></div>
      <div class="row" style="margin-top:8px"><button class="btn" (click)="save(r)">Enregistrer</button><button class="btn secondary" (click)="open=null">Annuler</button></div></div>
    <div style="margin-top:8px" *ngIf="open!==r.id"><button class="btn secondary small" (click)="ouvrir(r)">Traiter</button></div>
  </div>
  <div class="muted" *ngIf="!list.length">Aucune réclamation.</div>`
})
export class ReclamationsComponent implements OnInit {
  private api = inject(ApiService);
  statuts = STATUTS_RECL; list: any[] = []; fStatut = ''; error = ''; open: number | null = null; edit: any = {};
  ngOnInit() { this.load(); }
  load() { this.api.get<any[]>('reclamations', { statut: this.fStatut }).subscribe({ next: r => this.list = r, error: e => this.error = ApiService.err(e) }); }
  ouvrir(r: any) { this.open = r.id; this.edit = { statut: r.statut === 'Nouvelle' ? 'EnCours' : r.statut, reponse: r.reponse || '' }; }
  save(r: any) {
    this.api.put(`reclamations/${r.id}/traiter`, this.edit).subscribe({ next: () => { this.open = null; this.load(); }, error: e => this.error = ApiService.err(e) });
  }
}
