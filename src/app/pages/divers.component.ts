import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { today } from '../core/models';

@Component({
  selector: 'app-divers', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <div class="row"><h1>Divers — dépenses diverses</h1><span class="spacer"></span>
    <a class="btn secondary" routerLink="/taches/Divers">Voir les tâches →</a></div>
  <div class="card"><h2>Nouvelle dépense</h2>
    <div class="form-grid">
      <div style="grid-column:span 2"><label>Libellé *</label><input [(ngModel)]="form.libelle"></div>
      <div><label>Catégorie *</label><input list="cats" [(ngModel)]="form.categorie"><datalist id="cats"><option *ngFor="let c of cats" [value]="c"></option></datalist></div>
      <div><label>Montant (DA) *</label><input type="number" min="0" [(ngModel)]="form.montant"></div>
      <div><label>Date</label><input type="date" [(ngModel)]="form.date"></div>
      <div><label>Chantier</label><select [(ngModel)]="form.chantierId"><option [ngValue]="null">— général —</option><option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <div><label>Référence (facture, reçu)</label><input [(ngModel)]="form.reference"></div></div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div style="margin-top:10px"><button class="btn" [disabled]="!form.libelle || !form.categorie || !form.montant" (click)="save()">Enregistrer</button></div></div>
  <div class="card"><div class="row" style="margin-bottom:10px"><b>Total affiché : {{ total | number:'1.2-2' }} DA</b></div>
    <table><tr><th>Date</th><th>Libellé</th><th>Catégorie</th><th>Chantier</th><th>Référence</th><th class="num">Montant</th></tr>
      <tr *ngFor="let d of list"><td>{{ d.date | date:'dd/MM/yyyy' }}</td><td>{{ d.libelle }}</td><td><span class="badge">{{ d.categorie }}</span></td>
        <td>{{ d.chantierNom }}</td><td>{{ d.reference }}</td><td class="num">{{ d.montant | number:'1.2-2' }}</td></tr></table></div>`
})
export class DiversComponent implements OnInit {
  private api = inject(ApiService);
  cats = ['Fournitures de bureau', 'Carburant', 'Transport', 'Communication', 'Frais de mission', 'Assurance', 'Entretien locaux', 'Autre'];
  list: any[] = []; chantiers: any[] = []; error = '';
  form: any = this.blank();
  blank() { return { libelle: '', categorie: '', montant: null, date: today(), chantierId: null, reference: '' }; }
  get total() { return this.list.reduce((s, d) => s + d.montant, 0); }
  ngOnInit() { this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r); this.load(); }
  load() { this.api.get<any[]>('depenses').subscribe({ next: r => this.list = r, error: e => this.error = ApiService.err(e) }); }
  save() {
    this.error = '';
    this.api.post('depenses', { ...this.form, montant: +this.form.montant }).subscribe({ next: () => { this.form = this.blank(); this.load(); }, error: e => this.error = ApiService.err(e) });
  }
}
