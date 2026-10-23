import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { NotificationService } from '../core/notification.service';

@Component({ standalone: true, template: '' })
export class HomeRedirectComponent {
  constructor() {
    const a = inject(AuthService);
    const first = [['Gerant', '/dashboard'], ['Recette', '/recette'], ['Technique', '/technique'], ['Achats', '/achats'],
      ['Remboursement', '/remboursements'], ['Fiscal', '/fiscal'], ['Personnel', '/personnel'], ['Suivi', '/suivi'], ['Materiel', '/materiels']]
      .find(([role]) => a.hasRole(role));
    inject(Router).navigate([first ? first[1] : '/login'], { replaceUrl: true });
  }
}

@Component({
  selector: 'app-shell', standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  styles: [`
    .layout{display:flex;min-height:100vh}
    aside{width:230px;background:#12263a;color:#fff;padding:16px 0;flex-shrink:0;overflow-y:auto}
    aside .brand{font-size:18px;font-weight:700;padding:0 18px 16px;border-bottom:1px solid #243b52;margin-bottom:8px}
    aside a{display:block;color:#c9d6e3;padding:9px 18px;text-decoration:none} aside a:hover{background:#1c3550}
    aside a.active{background:var(--primary);color:#fff}
    main{flex:1;min-width:0} header{display:flex;align-items:center;gap:16px;background:#fff;border-bottom:1px solid var(--border);padding:10px 24px;position:relative}
    .content{padding:24px}
    .bell{position:relative;cursor:pointer;font-size:20px;background:none;border:0}
    .bell .n{position:absolute;top:-6px;right:-8px;background:var(--danger);color:#fff;border-radius:10px;font-size:11px;padding:1px 6px}
    .drop{position:absolute;right:24px;top:52px;width:380px;max-height:420px;overflow:auto;background:#fff;border:1px solid var(--border);border-radius:8px;box-shadow:0 8px 24px #0002;z-index:50}
    .drop .it{padding:10px 14px;border-bottom:1px solid var(--border);cursor:pointer} .drop .it.new{background:#eef5fd}
    .toasts{position:fixed;right:20px;bottom:20px;display:flex;flex-direction:column;gap:8px;z-index:100}
    .toast{background:#12263a;color:#fff;padding:12px 16px;border-radius:8px;max-width:360px;box-shadow:0 4px 16px #0004;border-left:4px solid #4caf50}
  `],
  template: `
  <div class="layout">
    <aside>
      <div class="brand">🏗️ PromoImmo</div>
      <a *ngFor="let l of visibleLinks" [routerLink]="l.path" routerLinkActive="active">{{ l.icon }} {{ l.label }}</a>
    </aside>
    <main>
      <header>
        <span class="spacer"></span>
        <button class="bell" *ngIf="auth.hasRole('Gerant')" (click)="open=!open" title="Notifications">🔔
          <span class="n" *ngIf="notif.unread">{{ notif.unread }}</span></button>
        <span><a routerLink="/compte" title="Mon compte">⚙️ {{ auth.user?.nom }}</a> <span class="badge">{{ auth.user?.roles?.join(', ') }}</span></span>
        <button class="btn secondary small" (click)="logout()">Déconnexion</button>
        <div class="drop" *ngIf="open">
          <div class="row" style="padding:10px 14px"><b>Notifications</b><span class="spacer"></span>
            <a href="javascript:void(0)" (click)="notif.markAllRead()">Tout marquer lu</a></div>
          <div class="it" [class.new]="!n.lu" *ngFor="let n of notif.items.slice(0,15)" (click)="notif.markRead(n)">
            <div>{{ n.message }}</div><div class="muted" style="font-size:11px">{{ n.date | date:'dd/MM/yyyy HH:mm' }}</div>
          </div>
          <div class="it muted" *ngIf="!notif.items.length">Aucune notification</div>
          <div class="it right"><a routerLink="/notifications" (click)="open=false">Voir tout</a></div>
        </div>
      </header>
      <div class="content"><router-outlet></router-outlet></div>
    </main>
  </div>
  <div class="toasts"><div class="toast" *ngFor="let t of notif.toasts">{{ icon(t.type) }} {{ t.message }}</div></div>
  `
})
export class ShellComponent implements OnInit {
  auth = inject(AuthService);
  notif = inject(NotificationService);
  open = false;
  links = [
    { path: '/dashboard', label: 'Tableau de bord', icon: '📊', roles: ['Gerant'] },
    { path: '/recette', label: 'Recette', icon: '💵', roles: ['Recette', 'Gerant'] },
    { path: '/clients', label: 'Clients', icon: '👥', roles: ['Gerant', 'Recette', 'Remboursement'] },
    { path: '/contrats', label: 'Contrats', icon: '📄', roles: ['Gerant', 'Recette'] },
    { path: '/reclamations', label: 'Réclamations', icon: '📩', roles: ['Gerant', 'Recette'] },
    { path: '/chantiers', label: 'Chantiers', icon: '🏗️', roles: ['Gerant'] },
    { path: '/budgets', label: 'Budgets', icon: '🧾', roles: ['Gerant'] },
    { path: '/rentabilite', label: 'Rentabilité', icon: '📈', roles: ['Gerant'] },
    { path: '/impayes', label: 'Impayés & relances', icon: '⏰', roles: ['Gerant', 'Recette'] },
    { path: '/lots', label: 'Lots à vendre', icon: '🏠', roles: ['Gerant', 'Recette'] },
    { path: '/technique', label: 'Service technique', icon: '📐', roles: ['Gerant', 'Technique'] },
    { path: '/achats', label: 'Achats & approvis.', icon: '🛒', roles: ['Gerant', 'Achats'] },
    { path: '/remboursements', label: 'Remboursements', icon: '💸', roles: ['Gerant', 'Remboursement'] },
    { path: '/fiscal', label: 'Fiscal & parafiscal', icon: '🏛️', roles: ['Gerant', 'Fiscal'] },
    { path: '/personnel', label: 'Personnel', icon: '👷', roles: ['Gerant', 'Personnel'] },
    { path: '/suivi', label: 'Suivi journalier', icon: '📅', roles: ['Gerant', 'Suivi'] },
    { path: '/materiels', label: 'Matériel', icon: '🚜', roles: ['Gerant', 'Materiel'] },
    { path: '/divers', label: 'Divers', icon: '🧰', roles: ['Gerant', 'Achats'] },
    { path: '/taches/all', label: 'Toutes les tâches', icon: '✅', roles: ['Gerant'] },
    { path: '/taches/Technique', label: 'Tâches techniques', icon: '✅', roles: ['Technique'] },
    { path: '/taches/Achats', label: 'Tâches achats', icon: '✅', roles: ['Achats'] },
    { path: '/taches/Remboursement', label: 'Tâches remboursements', icon: '✅', roles: ['Remboursement'] },
    { path: '/taches/Fiscal', label: 'Tâches fiscales', icon: '✅', roles: ['Fiscal'] },
    { path: '/taches/Personnel', label: 'Tâches personnel', icon: '✅', roles: ['Personnel'] },
    { path: '/taches/Suivi', label: 'Tâches de suivi', icon: '✅', roles: ['Suivi'] },
    { path: '/notifications', label: 'Notifications', icon: '🔔', roles: ['Gerant'] },
    { path: '/utilisateurs', label: 'Utilisateurs', icon: '🔑', roles: ['Gerant'] },
    { path: '/securite', label: 'Sécurité & sauvegardes', icon: '🛡️', roles: ['Gerant'] },
    { path: '/societe', label: 'Identité & signature', icon: '🖋️', roles: ['Gerant'] }
  ];
  get visibleLinks() { return this.links.filter(l => this.auth.hasRole(...l.roles)); }
  icon(type: string) { return ({ ENCAISSEMENT: '💰', RECLAMATION: '📩', REMBOURSEMENT: '💸', ALERTE: '⚠️', ACHAT: '🛒' } as any)[type] ?? '🔔'; }

  ngOnInit() { if (this.auth.hasRole('Gerant')) { this.notif.load(); this.notif.start(); } }
  logout() { this.notif.stop(); this.auth.logout(); }
}
