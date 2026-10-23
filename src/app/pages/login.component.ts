import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-login', standalone: true, imports: [CommonModule, FormsModule, RouterLink],
  template: `
  <div style="min-height:100vh;display:flex;align-items:center;justify-content:center">
    <div class="card" style="width:360px">
      <h1 style="text-align:center">🏗️ PromoImmo</h1>
      <p class="muted" style="text-align:center;margin-top:-8px">Connexion</p>
      <label>Utilisateur</label><input [(ngModel)]="userName" (keyup.enter)="go()" autofocus>
      <div style="height:10px"></div>
      <label>Mot de passe</label><input type="password" [(ngModel)]="password" (keyup.enter)="go()">
      <div class="alert err" *ngIf="error">{{ error }}</div>
      <div style="height:12px"></div>
      <button class="btn" style="width:100%" [disabled]="busy" (click)="go()">Se connecter</button>
      <div style="text-align:center;margin-top:14px"><a routerLink="/contact">📩 Contactez-nous / Réclamation client</a></div>
    </div>
  </div>`
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);
  userName = ''; password = ''; error = ''; busy = false;
  go() {
    this.busy = true; this.error = '';
    this.auth.login(this.userName, this.password).subscribe({
      next: () => this.router.navigate(['/']),
      error: e => { this.error = ApiService.err(e); this.busy = false; }
    });
  }
}
