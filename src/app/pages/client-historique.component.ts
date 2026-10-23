import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { imprimerRecu } from '../core/print-recu';
import { ExportButtonsComponent } from '../core/export-buttons.component';

@Component({
  selector: 'app-client-historique', standalone: true, imports: [CommonModule, FormsModule, RouterLink, ExportButtonsComponent],
  template: `
  <a routerLink="/clients">← Clients</a>
  <div class="row" style="margin-top:8px"><h1>Historique — {{ h?.nom }}</h1><span class="spacer"></span>
    <app-export [path]="'exports/situation-client/' + id" name="situation_client"></app-export></div>
  <div class="card"><div class="row">
    <div><label>Du</label><input type="date" [(ngModel)]="du"></div>
    <div><label>Au</label><input type="date" [(ngModel)]="au"></div>
    <div style="align-self:flex-end"><button class="btn" (click)="load()">Filtrer</button></div></div></div>
  <div class="alert err" *ngIf="error">{{ error }}</div>
  <div class="card" *ngFor="let c of h?.contrats">
    <div class="row"><h2 style="margin:0">{{ c.chantier }} — Lot {{ c.lot }}</h2><span class="spacer"></span>
      <span>Prix : <b>{{ c.prixTotal | number:'1.2-2' }}</b></span>
      <span>Payé : <b class="pos">{{ c.totalPaye | number:'1.2-2' }}</b></span>
      <span>Reste : <b [class.neg]="c.reste>0">{{ c.reste | number:'1.2-2' }}</b></span></div>
    <table style="margin-top:10px"><tr><th>Date</th><th>Reçu</th><th>Mode</th><th>Référence</th><th>Agent</th><th class="num">Montant</th><th></th></tr>
      <tr *ngFor="let e of c.encaissements" [class.strike]="e.annule">
        <td>{{ e.date | date:'dd/MM/yyyy HH:mm' }}</td><td>{{ e.numeroRecu }}</td><td>{{ e.mode === 'Especes' ? 'Espèces' : 'Bancaire' }}</td>
        <td>{{ e.refBancaire }}</td><td>{{ e.agent }}</td><td class="num">{{ e.montant | number:'1.2-2' }}</td>
        <td class="right"><button class="btn secondary small" *ngIf="e.montant>0" (click)="recu(e.id)">🖨️</button>
          <button class="btn secondary small" style="margin-left:4px" *ngIf="e.montant>0" (click)="pdfRecu(e.id)">PDF</button>
          <button class="btn danger small" style="margin-left:4px" *ngIf="!e.annule && e.montant>0" (click)="annuler(e)">Annuler</button></td></tr></table>
    <div class="muted" *ngIf="!c.encaissements.length">Aucun encaissement sur la période.</div>
  </div>
  <div class="muted" *ngIf="h && !h.contrats.length">Aucun contrat pour ce client.</div>`
})
export class ClientHistoriqueComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  id = 0; h: any; du = ''; au = ''; error = '';
  ngOnInit() { this.id = +this.route.snapshot.paramMap.get('id')!; this.load(); }
  load() {
    this.error = '';
    this.api.get<any>(`clients/${this.id}/historique`, { du: this.du, au: this.au ? this.au + 'T23:59:59' : '' })
      .subscribe({ next: r => this.h = r, error: e => this.error = ApiService.err(e) });
  }
  recu(id: number) { this.api.get<any>(`encaissements/${id}/recu`).subscribe(r => imprimerRecu(r)); }
  pdfRecu(id: number) { this.api.download(`exports/recu/${id}`, { format: 'pdf' }, `recu_${id}.pdf`).subscribe({ error: x => this.error = ApiService.err(x) }); }
  annuler(e: any) {
    const motif = prompt(`Motif d'annulation du reçu ${e.numeroRecu} :`);
    if (!motif) return;
    this.api.post(`encaissements/${e.id}/annuler`, { motif }).subscribe({ next: () => this.load(), error: x => this.error = ApiService.err(x) });
  }
}
