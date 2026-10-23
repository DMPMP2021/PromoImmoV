import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-compte', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <h1>Mon compte</h1>
  <div class="card" style="max-width:480px">
    <p>Connecté en tant que <b>{{ auth.user?.nom }}</b> <span class="badge">{{ auth.user?.roles?.join(', ') }}</span></p>
    <h2>Changer mon mot de passe</h2>
    <label>Mot de passe actuel</label><input type="password" [(ngModel)]="ancien">
    <div style="height:10px"></div>
    <label>Nouveau mot de passe</label><input type="password" [(ngModel)]="nouveau">
    <div class="muted" style="font-size:12px">8 caractères minimum, avec une majuscule, une minuscule, un chiffre et un symbole.</div>
    <div style="height:10px"></div>
    <label>Confirmer le nouveau mot de passe</label><input type="password" [(ngModel)]="confirmation">
    <div class="alert err" *ngIf="error">{{ error }}</div><div class="alert ok" *ngIf="ok">{{ ok }}</div>
    <div style="margin-top:12px"><button class="btn" [disabled]="!ancien || !nouveau || busy" (click)="changer()">Enregistrer</button></div>
  </div>`
})
export class CompteComponent {
  private api = inject(ApiService);
  auth = inject(AuthService);
  ancien = ''; nouveau = ''; confirmation = ''; error = ''; ok = ''; busy = false;
  changer() {
    this.error = ''; this.ok = '';
    if (this.nouveau !== this.confirmation) { this.error = 'La confirmation ne correspond pas au nouveau mot de passe.'; return; }
    this.busy = true;
    this.api.post('auth/change-password', { ancien: this.ancien, nouveau: this.nouveau }).subscribe({
      next: () => { this.ok = 'Mot de passe modifié.'; this.ancien = this.nouveau = this.confirmation = ''; this.busy = false; },
      error: e => { this.error = ApiService.err(e); this.busy = false; } });
  }
}
