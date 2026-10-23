import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-contact', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <div style="min-height:100vh;display:flex;align-items:center;justify-content:center;padding:20px">
    <div class="card" style="width:100%;max-width:560px">
      <h1 style="text-align:center">📩 Contactez-nous</h1>
      <p class="muted" style="text-align:center;margin-top:-8px">Une réclamation, une question sur votre dossier ? Écrivez-nous, nous vous répondrons rapidement.</p>
      <div *ngIf="numero" class="alert ok">✅ Votre message a bien été envoyé. Numéro de suivi : <b>{{ numero }}</b>. Conservez-le pour toute relance.</div>
      <ng-container *ngIf="!numero">
        <div class="form-grid">
          <div><label>Nom complet *</label><input [(ngModel)]="f.nom" maxlength="100"></div>
          <div><label>Téléphone</label><input [(ngModel)]="f.telephone" maxlength="30"></div>
          <div><label>E-mail</label><input type="email" [(ngModel)]="f.email" maxlength="150"></div>
          <div><label>N° de contrat / lot</label><input [(ngModel)]="f.contratRef" maxlength="100"></div></div>
        <div style="margin-top:12px"><label>Objet *</label><input [(ngModel)]="f.objet" maxlength="150"></div>
        <div style="margin-top:12px"><label>Votre message *</label><textarea rows="6" [(ngModel)]="f.message" maxlength="3000"></textarea></div>
        <input style="position:absolute;left:-9999px" tabindex="-1" autocomplete="off" [(ngModel)]="f.website" aria-hidden="true">
        <div class="alert err" *ngIf="error">{{ error }}</div>
        <div style="margin-top:14px"><button class="btn" style="width:100%" [disabled]="busy || !f.nom || !f.objet || !f.message" (click)="envoyer()">Envoyer</button></div>
      </ng-container>
      <div style="text-align:center;margin-top:14px"><a routerLink="/login">← Retour à la connexion</a></div>
    </div>
  </div>`
})
export class ContactComponent {
  private api = inject(ApiService);
  f: any = { nom: '', telephone: '', email: '', contratRef: '', objet: '', message: '', website: '' };
  numero = ''; error = ''; busy = false;
  envoyer() {
    this.busy = true; this.error = '';
    this.api.post<any>('reclamations/public', this.f).subscribe({
      next: r => { this.numero = r.numero; this.busy = false; },
      error: e => { this.error = ApiService.err(e); this.busy = false; } });
  }
}
