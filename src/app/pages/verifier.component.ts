import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../core/api.service';

/** Page publique ouverte par le QR code : indique si le document est authentique. */
@Component({
  selector: 'app-verifier', standalone: true, imports: [CommonModule],
  template: `
  <div style="min-height:100vh;display:flex;align-items:center;justify-content:center;padding:20px">
    <div class="card" style="width:100%;max-width:480px">
      <h1 style="text-align:center">🔍 Vérification de document</h1>
      <div *ngIf="chargement" class="muted" style="text-align:center">Vérification en cours…</div>
      <div *ngIf="erreur" class="alert err">{{ erreur }}</div>

      <ng-container *ngIf="res && !chargement">
        <div *ngIf="!res.valide" class="alert err" style="text-align:center;font-size:16px">
          ❌ <b>Document non reconnu.</b><br>Ce code est invalide ou le document n'a pas été émis par ce système. Méfiez-vous d'une possible falsification.</div>

        <ng-container *ngIf="res.valide">
          <div class="alert ok" style="text-align:center;font-size:16px" *ngIf="!res.annule">✅ <b>Document authentique</b><br>émis par {{ res.societe }}</div>
          <div class="alert err" style="text-align:center;font-size:16px" *ngIf="res.annule">⚠️ <b>Ce reçu a été ANNULÉ</b><br>Il n'est plus valable.</div>

          <table *ngIf="type==='recu'">
            <tr><td class="muted">Type</td><td>{{ res.type }}</td></tr><tr><td class="muted">Numéro</td><td><b>{{ res.numero }}</b></td></tr>
            <tr><td class="muted">Date</td><td>{{ res.date | date:'dd/MM/yyyy HH:mm' }}</td></tr>
            <tr><td class="muted">Montant</td><td><b>{{ res.montant | number:'1.2-2' }} DA</b></td></tr>
            <tr><td class="muted">Client</td><td>{{ res.client }}</td></tr><tr><td class="muted">Chantier</td><td>{{ res.chantier }}</td></tr></table>

          <table *ngIf="type==='doc'">
            <tr><td class="muted">Document</td><td><b>{{ res.titre }}</b></td></tr>
            <tr><td class="muted" *ngIf="res.sousTitre">Période</td><td *ngIf="res.sousTitre">{{ res.sousTitre }}</td></tr>
            <tr><td class="muted">Émis le</td><td>{{ res.dateEmission | date:'dd/MM/yyyy HH:mm' }}</td></tr>
            <tr><td class="muted">Émis par</td><td>{{ res.emisPar }}</td></tr>
            <tr><td class="muted">Empreinte</td><td style="font-family:monospace">{{ res.empreinte }}</td></tr></table>
          <div class="muted" style="margin-top:10px;font-size:12px" *ngIf="type==='doc'">
            L'empreinte résume le contenu des tableaux au moment de l'émission. Pour un contrôle approfondi, demandez à l'entreprise de confirmer ce code.</div>
        </ng-container>
      </ng-container>
    </div>
  </div>`
})
export class VerifierComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  type = ''; res: any; chargement = true; erreur = '';

  ngOnInit() {
    const snap = this.route.snapshot;
    this.type = snap.data['type'];
    const chemin = this.type === 'recu'
      ? `verification/recu/${snap.paramMap.get('id')}/${snap.paramMap.get('sig')}`
      : `verification/doc/${snap.paramMap.get('code')}`;
    this.api.get<any>(chemin).subscribe({
      next: r => { this.res = r; this.chargement = false; },
      error: () => { this.erreur = 'Serveur injoignable : réessayez plus tard.'; this.chargement = false; } });
  }
}
