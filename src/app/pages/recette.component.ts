import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { MODES } from '../core/models';
import { imprimerRecu } from '../core/print-recu';
import { ExportButtonsComponent } from '../core/export-buttons.component';

@Component({
  selector: 'app-recette', standalone: true, imports: [CommonModule, FormsModule, RouterLink, ExportButtonsComponent],
  template: `
  <h1>Recette — encaissement des clients</h1>
  <div class="grid grid-2">
    <div>
      <div class="card">
        <h2>1. Client</h2>
        <div class="row"><input style="flex:1" placeholder="Nom, téléphone ou CIN…" [(ngModel)]="q" (keyup.enter)="chercher()">
          <button class="btn" (click)="chercher()">Rechercher</button>
          <a class="btn secondary" routerLink="/clients">+ Nouveau</a></div>
        <table style="margin-top:10px">
          <tr class="click" *ngFor="let c of clients" [class.sel]="client?.id===c.id" (click)="choisir(c)">
            <td>{{ c.nom }}</td><td class="muted">{{ c.telephone }}</td><td class="muted">{{ c.cin }}</td></tr>
        </table>
        <div class="muted" *ngIf="!clients.length">Aucun client.</div>
      </div>

      <div class="card" *ngIf="client">
        <h2>2. Contrat de {{ client.nom }}</h2>
        <table>
          <tr><th>Chantier</th><th>Lot</th><th class="num">Prix</th><th class="num">Payé</th><th class="num">Reste</th></tr>
          <tr class="click" *ngFor="let c of contrats" [class.sel]="contrat?.id===c.id" (click)="ouvrir(c)">
            <td>{{ c.chantierNom }}</td><td>{{ c.lot }}</td><td class="num">{{ c.prixTotal | number:'1.2-2' }}</td>
            <td class="num">{{ c.totalPaye | number:'1.2-2' }}</td>
            <td class="num"><b>{{ c.prixTotal - c.totalPaye | number:'1.2-2' }}</b></td></tr>
        </table>
        <div class="muted" *ngIf="!contrats.length">Aucun contrat. <a routerLink="/contrats" [queryParams]="{clientId: client.id}">Créer un contrat</a></div>
      </div>

      <div class="card" *ngIf="contrat">
        <h2>3. Encaissement — {{ contrat.chantierNom }} / {{ contrat.lot }}</h2>
        <div class="row" style="margin-bottom:8px"><span>Reste à payer :</span><b>{{ contrat.reste | number:'1.2-2' }} DA</b></div>
        <div class="bar"><div [style.width.%]="contrat.prixTotal ? contrat.totalPaye*100/contrat.prixTotal : 0"></div></div>
        <table style="margin:10px 0">
          <tr><th>Tranche</th><th>Date prévue</th><th class="num">Prévu</th><th class="num">Payé</th><th></th></tr>
          <tr *ngFor="let e of contrat.echeances">
            <td>#{{ e.numero }}</td><td>{{ e.datePrevue | date:'dd/MM/yyyy' }}</td>
            <td class="num">{{ e.montantPrevu | number:'1.2-2' }}</td><td class="num">{{ e.paye | number:'1.2-2' }}</td>
            <td><span class="badge ok" *ngIf="e.reste===0">Soldée</span>
                <span class="badge warn" *ngIf="e.reste>0 && e.paye>0">Partielle</span></td></tr>
        </table>
        <div class="form-grid">
          <div><label>Tranche concernée</label>
            <select [(ngModel)]="form.echeanceId" (ngModelChange)="onEcheance()">
              <option [ngValue]="null">— Aucune / libre —</option>
              <option *ngFor="let e of contrat.echeances" [ngValue]="e.id" [disabled]="e.reste===0">#{{ e.numero }} (reste {{ e.reste | number:'1.2-2' }})</option>
            </select></div>
          <div><label>Montant (DA)</label><input type="number" min="0" [(ngModel)]="form.montant"></div>
          <div><label>Mode</label><select [(ngModel)]="form.mode"><option *ngFor="let m of modes" [value]="m.v">{{ m.l }}</option></select></div>
          <div *ngIf="form.mode==='Bancaire'"><label>Réf. chèque / virement</label><input [(ngModel)]="form.refBancaire"></div>
        </div>
        <div class="alert err" *ngIf="error">{{ error }}</div>
        <div class="alert ok" *ngIf="dernierRecu">
          ✅ Encaissement enregistré — reçu <b>{{ dernierRecu.numeroRecu }}</b>
          <button class="btn small" style="margin-left:10px" (click)="imprimer(dernierRecu.id)">🖨️ Imprimer le reçu</button>
          <button class="btn small" style="margin-left:6px" (click)="pdfRecu(dernierRecu.id)">📄 Reçu PDF officiel</button></div>
        <div style="margin-top:10px"><button class="btn" [disabled]="busy || !form.montant" (click)="valider()">Encaisser</button></div>
      </div>
    </div>

    <div class="card">
      <h2>Derniers encaissements</h2>
      <table>
        <tr><th>Reçu</th><th>Client</th><th class="num">Montant</th><th>Mode</th><th></th></tr>
        <tr *ngFor="let r of recents" [class.strike]="r.annule">
          <td>{{ r.numeroRecu }}<div class="muted" style="font-size:11px">{{ r.date | date:'dd/MM HH:mm' }}</div></td>
          <td>{{ r.clientNom }}<div class="muted" style="font-size:11px">{{ r.chantierNom }}</div></td>
          <td class="num">{{ r.montant | number:'1.2-2' }}</td>
          <td>{{ r.mode === 'Especes' ? 'Espèces' : 'Bancaire' }}</td>
          <td><button class="btn secondary small" (click)="imprimer(r.id)">🖨️</button> <button class="btn secondary small" title="PDF officiel avec QR code" (click)="pdfRecu(r.id)">PDF</button></td></tr>
      </table>
      <h3 style="margin-top:16px">Journal des encaissements (PDF / Excel)</h3>
      <div class="row"><div><label>Du</label><input type="date" [(ngModel)]="exDu"></div><div><label>Au</label><input type="date" [(ngModel)]="exAu"></div>
        <div style="align-self:flex-end"><app-export path="exports/journal-encaissements" [params]="{ du: exDu, au: exAu }" name="journal_encaissements"></app-export></div></div>
    </div>
  </div>`
})
export class RecetteComponent implements OnInit {
  private api = inject(ApiService);
  modes = MODES;
  exDu = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10); exAu = this.exDu;
  q = ''; clients: any[] = []; client: any; contrats: any[] = []; contrat: any;
  form: any = this.blank(); error = ''; dernierRecu: any; recents: any[] = []; busy = false;

  blank() { return { echeanceId: null, montant: null, mode: 'Especes', refBancaire: '' }; }
  ngOnInit() { this.chercher(); this.loadRecents(); }
  chercher() { this.api.get<any[]>('clients', { q: this.q }).subscribe(r => this.clients = r); }
  loadRecents() { this.api.get<any[]>('recette/recents').subscribe(r => this.recents = r); }
  choisir(c: any) {
    this.client = c; this.contrat = null; this.dernierRecu = null;
    this.api.get<any[]>('contrats', { clientId: c.id }).subscribe(r => this.contrats = r);
  }
  ouvrir(c: any) {
    this.api.get<any>('contrats/' + c.id).subscribe(r => { this.contrat = r; this.form = this.blank(); this.error = ''; });
  }
  onEcheance() {
    const e = this.contrat.echeances.find((x: any) => x.id === this.form.echeanceId);
    if (e) this.form.montant = e.reste;
  }
  valider() {
    this.busy = true; this.error = ''; this.dernierRecu = null;
    this.api.post<any>('encaissements', {
      contratId: this.contrat.id, echeanceId: this.form.echeanceId, montant: +this.form.montant,
      mode: this.form.mode, refBancaire: this.form.refBancaire || null
    }).subscribe({
      next: r => {
        this.dernierRecu = r; this.busy = false;
        this.ouvrir(this.contrat); this.choisir(this.client); this.loadRecents();
      },
      error: e => { this.error = ApiService.err(e); this.busy = false; }
    });
  }
  imprimer(id: number) { this.api.get<any>(`encaissements/${id}/recu`).subscribe(r => imprimerRecu(r)); }
  pdfRecu(id: number) { this.api.download(`exports/recu/${id}`, { format: 'pdf' }, `recu_${id}.pdf`).subscribe({ error: e => this.error = ApiService.err(e) }); }
}
