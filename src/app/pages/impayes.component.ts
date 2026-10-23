import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { ExportButtonsComponent } from '../core/export-buttons.component';

@Component({
  selector: 'app-impayes', standalone: true, imports: [CommonModule, FormsModule, ExportButtonsComponent],
  template: `
  <div class="row"><h1>Impayés et relances</h1><span class="spacer"></span>
    <app-export path="exports/impayes" [params]="{ joursMin: joursMin }" name="etat_impayes"></app-export></div>
  <div class="grid grid-3">
    <div class="card"><div class="stat-label">Clients en retard</div><div class="stat" [class.neg]="list.length>0">{{ list.length }}</div></div>
    <div class="card"><div class="stat-label">Montant total en retard</div><div class="stat">{{ total | number:'1.0-0' }} DA</div></div>
    <div class="card"><div class="stat-label">En retard depuis plus de 30 jours</div><div class="stat" [class.neg]="plus30>0">{{ plus30 }}</div></div>
  </div>
  <div class="alert err" *ngIf="error">{{ error }}</div><div class="alert ok" *ngIf="info">{{ info }}</div>
  <div class="card">
    <div class="row" style="margin-bottom:10px"><label style="margin:0">Retard minimum :</label>
      <select style="width:200px" [(ngModel)]="joursMin" (ngModelChange)="load()">
        <option [ngValue]="0">Tous les retards</option><option [ngValue]="8">Plus de 7 jours</option>
        <option [ngValue]="16">Plus de 15 jours</option><option [ngValue]="31">Plus de 30 jours</option></select></div>
    <table><tr><th>Client</th><th>Contrat</th><th class="num">Montant en retard</th><th>Retard</th><th>Relances</th><th>Envoyer une relance</th></tr>
      <ng-container *ngFor="let i of list">
        <tr><td><b>{{ i.clientNom }}</b><div class="muted" style="font-size:12px">{{ i.telephone }} {{ i.email }}</div></td>
          <td>{{ i.chantierNom }} / {{ i.lot }}</td>
          <td class="num"><b>{{ i.montantEnRetard | number:'1.2-2' }}</b></td>
          <td><span class="badge" [class.danger]="i.joursRetard>30" [class.warn]="i.joursRetard<=30">{{ i.joursRetard }} j</span>
            <div class="muted" style="font-size:11px">depuis le {{ i.echeanceDepuis | date:'dd/MM/yyyy' }}</div></td>
          <td>{{ i.nbRelances }}<div class="muted" style="font-size:11px" *ngIf="i.derniereRelance">dernière : {{ i.derniereRelance | date:'dd/MM/yyyy' }}</div></td>
          <td><div style="display:flex;gap:4px;flex-wrap:wrap">
            <a *ngIf="i.whatsApp" class="btn secondary small" [href]="i.whatsApp" target="_blank" rel="noopener" (click)="log(i,'WhatsApp')">WhatsApp</a>
            <a *ngIf="i.sms" class="btn secondary small" [href]="i.sms" (click)="log(i,'SMS')">SMS</a>
            <a *ngIf="i.mailto" class="btn secondary small" [href]="i.mailto" (click)="log(i,'E-mail')">E-mail</a>
            <button class="btn secondary small" (click)="copier(i)">Copier</button>
            <button class="btn secondary small" (click)="log(i,'Appel')">Appelé</button>
            <button class="btn secondary small" (click)="openId = openId===i.contratId ? null : i.contratId">Aperçu</button></div></td></tr>
        <tr *ngIf="openId===i.contratId"><td colspan="6" class="muted" style="white-space:pre-wrap;background:#f7f9fb">{{ i.message }}</td></tr>
      </ng-container></table>
    <div class="muted" *ngIf="!list.length">🎉 Aucun client en retard de paiement.</div>
    <div class="muted" style="margin-top:10px">Les boutons ouvrent WhatsApp, votre application SMS ou votre messagerie avec le message déjà rédigé ; la relance est enregistrée dans l'historique du contrat.</div>
  </div>`
})
export class ImpayesComponent implements OnInit {
  private api = inject(ApiService);
  list: any[] = []; joursMin = 0; error = ''; info = ''; openId: number | null = null;
  get total() { return this.list.reduce((s, i) => s + i.montantEnRetard, 0); }
  get plus30() { return this.list.filter(i => i.joursRetard > 30).length; }
  ngOnInit() { this.load(); }
  load() { this.api.get<any[]>('impayes', { joursMin: this.joursMin }).subscribe({ next: r => this.list = r, error: e => this.error = ApiService.err(e) }); }
  log(i: any, canal: string) {
    this.api.post(`impayes/${i.contratId}/relance`, { canal, message: i.message }).subscribe({
      next: () => { this.info = `Relance (${canal}) enregistrée pour ${i.clientNom}.`; this.load(); },
      error: e => this.error = ApiService.err(e) });
  }
  copier(i: any) {
    navigator.clipboard.writeText(i.message).then(() => this.info = 'Message copié dans le presse-papiers.', () => this.error = 'Copie impossible.');
  }
}
