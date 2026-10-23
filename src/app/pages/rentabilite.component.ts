import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { ExportButtonsComponent } from '../core/export-buttons.component';

@Component({
  selector: 'app-rentabilite', standalone: true, imports: [CommonModule, RouterLink, ExportButtonsComponent],
  template: `
  <div class="row"><h1>Rentabilité par chantier</h1><span class="spacer"></span>
    <app-export path="exports/rentabilite" name="rentabilite"></app-export></div>
  <div class="muted" style="margin-bottom:12px">
    Dépensé = achats engagés + main-d'œuvre pointée + paiements matériel + dépenses diverses rattachés au chantier.
    Marge réelle = encaissé − remboursements − dépensé.</div>
  <div class="alert err" *ngIf="data && data.totaux.enDepassement">⚠ {{ data.totaux.enDepassement }} chantier(s) dépassent leur budget.</div>
  <div class="alert err" *ngIf="error">{{ error }}</div>

  <div class="grid grid-4" *ngIf="data">
    <div class="card"><div class="stat-label">Chiffre d'affaires contractuel</div><div class="stat">{{ data.totaux.chiffreAffaires | number:'1.0-0' }} DA</div></div>
    <div class="card"><div class="stat-label">Encaissé (net des remboursements)</div><div class="stat pos">{{ data.totaux.encaisse - data.totaux.rembourse | number:'1.0-0' }} DA</div></div>
    <div class="card"><div class="stat-label">Total dépensé</div><div class="stat">{{ data.totaux.depense | number:'1.0-0' }} DA</div></div>
    <div class="card"><div class="stat-label">Marge réelle</div><div class="stat" [class.neg]="data.totaux.margeReelle<0" [class.pos]="data.totaux.margeReelle>=0">{{ data.totaux.margeReelle | number:'1.0-0' }} DA</div></div>
  </div>

  <div class="card" *ngFor="let c of chantiers">
    <div class="row"><h2 style="margin:0">{{ c.nom }}</h2>
      <span [class]="badgeCls(c.alerte)">{{ badgeTxt(c.alerte) }}</span><span class="spacer"></span>
      <a routerLink="/chantiers">Modifier le budget</a></div>
    <div class="grid grid-4" style="margin-top:12px">
      <div><div class="stat-label">CA contractuel</div><b>{{ c.chiffreAffaires | number:'1.0-0' }} DA</b></div>
      <div><div class="stat-label">Encaissé (net)</div><b class="pos">{{ c.encaisse - c.rembourse | number:'1.0-0' }} DA</b></div>
      <div><div class="stat-label">Dépensé</div><b>{{ c.depense | number:'1.0-0' }} DA</b></div>
      <div><div class="stat-label">Marge réelle</div><b [class.neg]="c.margeReelle<0" [class.pos]="c.margeReelle>=0">{{ c.margeReelle | number:'1.0-0' }} DA</b></div>
    </div>
    <div *ngIf="c.budgetReference>0" style="margin-top:12px">
      <div class="row"><span>Budget {{ c.budgetPrevu>0 ? 'prévu' : 'alloué' }} : <b>{{ c.budgetReference | number:'1.0-0' }} DA</b></span>
        <span class="spacer"></span><b>{{ c.tauxConsommation }} % consommé</b></div>
      <div class="bar"><div [style.width.%]="cap(c.tauxConsommation)" [style.background]="color(c.alerte)"></div></div>
    </div>
    <div class="muted" *ngIf="!(c.budgetReference>0)" style="margin-top:10px">Aucun budget défini : saisissez un « budget prévu » dans la page Chantiers pour activer l'alerte de dépassement.</div>
    <table style="margin-top:12px"><tr><th>Poste de dépense</th><th class="num">Montant</th><th class="num">Part</th></tr>
      <tr><td>Achats (commandes engagées)</td><td class="num">{{ c.achats | number:'1.0-0' }}</td><td class="num">{{ part(c.achats, c.depense) }} %</td></tr>
      <tr><td>Main-d'œuvre (pointages × salaire journalier)</td><td class="num">{{ c.mainOeuvre | number:'1.0-0' }}</td><td class="num">{{ part(c.mainOeuvre, c.depense) }} %</td></tr>
      <tr><td>Matériel (paiements rattachés au chantier)</td><td class="num">{{ c.materiel | number:'1.0-0' }}</td><td class="num">{{ part(c.materiel, c.depense) }} %</td></tr>
      <tr><td>Dépenses diverses</td><td class="num">{{ c.divers | number:'1.0-0' }}</td><td class="num">{{ part(c.divers, c.depense) }} %</td></tr></table>
  </div>
  <div class="muted" *ngIf="data && !chantiers.length">Aucun chantier.</div>`
})
export class RentabiliteComponent implements OnInit {
  private api = inject(ApiService);
  data: any; error = '';
  get chantiers(): any[] { return this.data ? this.data.chantiers : []; }
  ngOnInit() { this.api.get<any>('rentabilite').subscribe({ next: r => this.data = r, error: e => this.error = ApiService.err(e) }); }
  cap(v: number) { return v > 100 ? 100 : v; }
  part(v: number, total: number) { return total > 0 ? Math.round(v * 1000 / total) / 10 : 0; }
  color(a: string) { return a === 'Depassement' ? 'var(--danger)' : a === 'Attention' ? 'var(--warn)' : 'var(--ok)'; }
  badgeCls(a: string) { return a === 'Depassement' ? 'badge danger' : a === 'Attention' ? 'badge warn' : a === 'Ok' ? 'badge ok' : 'badge'; }
  badgeTxt(a: string) { return a === 'Depassement' ? 'Budget dépassé' : a === 'Attention' ? 'Proche du budget' : a === 'Ok' ? 'Dans le budget' : 'Budget non défini'; }
}
