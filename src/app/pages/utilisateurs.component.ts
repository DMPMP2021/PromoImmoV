import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-utilisateurs', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <h1>Utilisateurs</h1>
  <div class="card"><h2>Créer un compte</h2>
    <div class="form-grid">
      <div><label>Identifiant *</label><input [(ngModel)]="form.userName"></div>
      <div><label>Nom complet *</label><input [(ngModel)]="form.nomComplet"></div>
      <div><label>Mot de passe *</label><input type="password" [(ngModel)]="form.password"></div>
      <div><label>Rôle</label><select [(ngModel)]="form.role"><option value="Recette">Recette</option><option value="Materiel">Matériel</option><option value="Technique">Service technique</option><option value="Achats">Achats & approvisionnement</option><option value="Remboursement">Remboursements</option><option value="Fiscal">Fiscal & parafiscal</option><option value="Personnel">Personnel</option><option value="Suivi">Suivi journalier</option><option value="Gerant">Gérant</option></select></div>
    </div>
    <div class="muted" style="margin-top:6px">Mot de passe : 6 caractères min., avec majuscule, minuscule et chiffre.</div>
    <div class="alert err" *ngIf="error">{{ error }}</div><div class="alert ok" *ngIf="ok">{{ ok }}</div>
    <div style="margin-top:10px"><button class="btn" [disabled]="!form.userName || !form.nomComplet || !form.password" (click)="save()">Créer</button></div></div>
  <div class="card"><table><tr><th>Identifiant</th><th>Nom</th></tr>
    <tr *ngFor="let u of list"><td>{{ u.userName }}</td><td>{{ u.nomComplet }}</td></tr></table></div>`
})
export class UtilisateursComponent implements OnInit {
  private api = inject(ApiService);
  list: any[] = []; error = ''; ok = '';
  form: any = { userName: '', nomComplet: '', password: '', role: 'Recette' };
  ngOnInit() { this.load(); }
  load() { this.api.get<any[]>('auth/users').subscribe(r => this.list = r); }
  save() {
    this.error = ''; this.ok = '';
    this.api.post('auth/users', this.form).subscribe({
      next: () => { this.ok = 'Compte créé.'; this.form = { userName: '', nomComplet: '', password: '', role: 'Recette' }; this.load(); },
      error: e => this.error = ApiService.err(e) });
  }
}
