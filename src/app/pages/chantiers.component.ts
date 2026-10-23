import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { STATUTS } from '../core/models';

@Component({
  selector: 'app-chantiers', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <h1>Chantiers</h1>
  <div class="card">
    <h2>{{ form.id ? 'Modifier le chantier' : 'Nouveau chantier' }}</h2>
    <div class="form-grid">
      <div><label>Nom *</label><input [(ngModel)]="form.nom"></div>
      <div><label>Localisation</label><input [(ngModel)]="form.localisation"></div>
      <div><label>Statut</label><select [(ngModel)]="form.statut"><option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
      <div><label>Budget prévu (DA)</label><input type="number" min="0" [(ngModel)]="form.budgetPrevu"></div>
    </div>
    <div class="muted" style="margin-top:6px">Le budget prévu sert à l'alerte de dépassement dans « Rentabilité ». À 0, le budget alloué par le gérant est utilisé.</div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!form.nom" (click)="save()">Enregistrer</button>
      <button class="btn secondary" *ngIf="form.id" (click)="reset()">Annuler</button></div>
  </div>
  <div class="card"><table><tr><th>Nom</th><th>Localisation</th><th>Statut</th><th class="num">Budget prévu</th><th class="num">Encaissé</th><th class="num">Alloué</th><th class="num">Solde</th><th></th></tr>
    <tr *ngFor="let c of list"><td>{{ c.nom }}</td><td>{{ c.localisation }}</td><td><span class="badge">{{ c.statut }}</span></td>
      <td class="num">{{ c.budgetPrevu | number:'1.0-0' }}</td>
      <ng-container *ngIf="fin[c.id] as f"><td class="num">{{ f.encaisse | number:'1.0-0' }}</td><td class="num">{{ f.alloue | number:'1.0-0' }}</td>
        <td class="num" [class.neg]="f.solde<0">{{ f.solde | number:'1.0-0' }}</td></ng-container>
      <td class="right"><button class="btn secondary small" (click)="edit(c)">Modifier</button></td></tr></table></div>`
})
export class ChantiersComponent implements OnInit {
  private api = inject(ApiService);
  statuts = STATUTS; list: any[] = []; fin: Record<number, any> = {}; form: any = this.blank(); error = '';
  blank() { return { nom: '', localisation: '', statut: 'EnCours', budgetPrevu: 0 }; }
  ngOnInit() { this.load(); }
  load() {
    this.api.get<any[]>('chantiers').subscribe(r => {
      this.list = r;
      r.forEach(c => this.api.get<any>(`chantiers/${c.id}/finances`).subscribe(f => this.fin[c.id] = f));
    });
  }
  edit(c: any) { this.form = { id: c.id, nom: c.nom, localisation: c.localisation, statut: c.statut, budgetPrevu: c.budgetPrevu }; window.scrollTo(0, 0); }
  reset() { this.form = this.blank(); this.error = ''; }
  save() {
    const body = { ...this.form, budgetPrevu: +this.form.budgetPrevu || 0 };
    const req = this.form.id ? this.api.put(`chantiers/${this.form.id}`, body) : this.api.post('chantiers', body);
    req.subscribe({ next: () => { this.reset(); this.load(); }, error: e => this.error = ApiService.err(e) });
  }
}
