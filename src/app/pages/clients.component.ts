import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-clients', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <h1>Clients</h1>
  <div class="card">
    <h2>{{ form.id ? 'Modifier le client' : 'Nouveau client' }}</h2>
    <div class="form-grid">
      <div><label>Nom complet *</label><input [(ngModel)]="form.nom"></div>
      <div><label>Téléphone</label><input [(ngModel)]="form.telephone"></div>
      <div><label>N° CIN</label><input [(ngModel)]="form.cin"></div>
      <div><label>Adresse</label><input [(ngModel)]="form.adresse"></div>
      <div><label>E-mail</label><input type="email" [(ngModel)]="form.email"></div>
    </div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div class="row" style="margin-top:10px">
      <button class="btn" [disabled]="!form.nom" (click)="save()">Enregistrer</button>
      <button class="btn secondary" *ngIf="form.id" (click)="reset()">Annuler</button></div>
  </div>
  <div class="card">
    <div class="row" style="margin-bottom:10px"><input style="max-width:320px" placeholder="Rechercher…" [(ngModel)]="q" (keyup.enter)="load()">
      <button class="btn secondary" (click)="load()">Rechercher</button></div>
    <table><tr><th>Nom</th><th>Téléphone</th><th>CIN</th><th>Adresse</th><th>E-mail</th><th></th></tr>
      <tr *ngFor="let c of list"><td>{{ c.nom }}</td><td>{{ c.telephone }}</td><td>{{ c.cin }}</td><td>{{ c.adresse }}</td><td>{{ c.email }}</td>
        <td class="right"><button class="btn secondary small" (click)="edit(c)">Modifier</button>
          <a class="btn secondary small" style="margin-left:4px" routerLink="/contrats" [queryParams]="{clientId:c.id}">Contrats</a>
          <a class="btn small" style="margin-left:4px" *ngIf="auth.hasRole('Gerant')" [routerLink]="['/clients', c.id]">Historique</a></td></tr></table>
  </div>`
})
export class ClientsComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  list: any[] = []; q = ''; error = ''; form: any = {};
  ngOnInit() { this.load(); }
  load() { this.api.get<any[]>('clients', { q: this.q }).subscribe(r => this.list = r); }
  edit(c: any) { this.form = { ...c }; window.scrollTo(0, 0); }
  reset() { this.form = {}; this.error = ''; }
  save() {
    this.error = '';
    const req = this.form.id ? this.api.put(`clients/${this.form.id}`, this.form) : this.api.post('clients', this.form);
    req.subscribe({ next: () => { this.reset(); this.load(); }, error: e => this.error = ApiService.err(e) });
  }
}
