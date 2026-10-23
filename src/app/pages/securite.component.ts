import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../core/api.service';
import { ExportButtonsComponent } from '../core/export-buttons.component';

@Component({
  selector: 'app-securite', standalone: true, imports: [CommonModule, FormsModule, ExportButtonsComponent],
  template: `
  <h1>Sécurité et fiabilité</h1>
  <div class="row" style="margin-bottom:14px">
    <button class="btn" [class.secondary]="tab!=='audit'" (click)="tab='audit'">Journal d'audit</button>
    <button class="btn" [class.secondary]="tab!=='backup'" (click)="tab='backup'; loadBackup()">Sauvegardes</button>
    <button class="btn" [class.secondary]="tab!=='comptes'" (click)="tab='comptes'; loadUsers()">Comptes utilisateurs</button></div>
  <div class="alert err" *ngIf="error">{{ error }}</div><div class="alert ok" *ngIf="ok">{{ ok }}</div>

  <ng-container *ngIf="tab==='audit'">
    <div class="card">
      <div class="form-grid">
        <div><label>Du</label><input type="date" [(ngModel)]="f.du"></div><div><label>Au</label><input type="date" [(ngModel)]="f.au"></div>
        <div><label>Utilisateur</label><input [(ngModel)]="f.utilisateur"></div>
        <div><label>Élément</label><select [(ngModel)]="f.entite"><option value="">Tous</option><option *ngFor="let e of entites" [value]="e">{{ e }}</option></select></div>
        <div><label>Recherche dans les détails</label><input [(ngModel)]="f.q"></div></div>
      <div class="row" style="margin-top:10px"><button class="btn" (click)="page=1; loadAudit()">Filtrer</button><span class="spacer"></span>
        <app-export path="exports/journal-audit" [params]="{ du: f.du, au: f.au }" name="journal_audit"></app-export></div></div>
    <div class="card">
      <table><tr><th>Date</th><th>Utilisateur</th><th>Action</th><th>Élément</th><th>Détails</th><th>IP</th></tr>
        <tr *ngFor="let a of items"><td style="white-space:nowrap">{{ a.date | date:'dd/MM/yyyy HH:mm:ss' }}</td><td>{{ a.userName }}</td>
          <td><span class="badge" [class.warn]="a.action==='Modification'" [class.ok]="a.action==='Création'" [class.danger]="a.action==='Suppression' || a.action.indexOf('échou')>=0 || a.action.indexOf('refus')>=0">{{ a.action }}</span></td>
          <td>{{ a.entite }} <span class="muted" *ngIf="a.entiteId">#{{ a.entiteId }}</span></td>
          <td style="font-size:12px;max-width:520px;word-break:break-word">{{ a.details }}</td><td class="muted">{{ a.ip }}</td></tr></table>
      <div class="muted" *ngIf="!items.length">Aucune trace sur cette période.</div>
      <div class="row" style="margin-top:10px"><button class="btn secondary small" [disabled]="page<=1" (click)="page=page-1; loadAudit()">← Précédent</button>
        <span>Page {{ page }} / {{ pages }} — {{ total }} événement(s)</span>
        <button class="btn secondary small" [disabled]="page>=pages" (click)="page=page+1; loadAudit()">Suivant →</button></div></div>
  </ng-container>

  <ng-container *ngIf="tab==='backup' && bk">
    <div class="card"><h2>Sauvegarde automatique de la base de données</h2>
      <div class="grid grid-3">
        <div><div class="stat-label">État</div><b [class.pos]="bk.actif" [class.neg]="!bk.actif">{{ bk.actif ? 'Activée' : 'Désactivée' }}</b></div>
        <div><div class="stat-label">Heure quotidienne</div><b>{{ bk.heure }} h 00</b></div>
        <div><div class="stat-label">Conservation</div><b>{{ bk.retention }} jours</b></div></div>
      <p>Dossier de destination : <b>{{ bk.dossier }}</b></p>
      <div *ngIf="bk.derniere.date">Dernière tentative : {{ bk.derniere.date | date:'dd/MM/yyyy HH:mm' }} —
        <span class="badge" [class.ok]="bk.derniere.ok" [class.danger]="!bk.derniere.ok">{{ bk.derniere.ok ? 'Réussie' : 'Échec' }}</span>
        <div class="muted" style="font-size:12px">{{ bk.derniere.message }}</div></div>
      <div style="margin-top:12px"><button class="btn" [disabled]="busy" (click)="sauvegarder()">💾 Sauvegarder maintenant</button></div>
      <div class="muted" style="margin-top:10px">Conseil : copiez régulièrement ce dossier sur un disque externe ou un espace en ligne. Une sauvegarde qui reste sur le même disque ne protège pas d'une panne du disque.
        Le dossier doit être accessible en écriture par SQL Server ; il se règle dans appsettings.json (Backup:Directory).</div></div>
    <div class="card"><h2>Fichiers de sauvegarde</h2>
      <table><tr><th>Fichier</th><th>Date</th><th class="num">Taille</th></tr>
        <tr *ngFor="let f of bk.fichiers"><td>{{ f.nom }}</td><td>{{ f.date | date:'dd/MM/yyyy HH:mm' }}</td><td class="num">{{ f.taille / 1048576 | number:'1.1-1' }} Mo</td></tr></table>
      <div class="muted" *ngIf="!bk.fichiers.length">Aucune sauvegarde trouvée dans le dossier.</div></div>
  </ng-container>

  <div class="card" *ngIf="tab==='comptes'">
    <table><tr><th>Identifiant</th><th>Nom</th><th>Rôle(s)</th><th>État</th><th></th></tr>
      <tr *ngFor="let u of users"><td>{{ u.userName }}</td><td>{{ u.nomComplet }}</td><td>{{ u.roles.join(', ') }}</td>
        <td><span class="badge danger" *ngIf="u.desactive">Désactivé</span><span class="badge warn" *ngIf="u.verrouille">Verrouillé</span>
          <span class="badge ok" *ngIf="!u.desactive && !u.verrouille">Actif</span></td>
        <td class="right"><div style="display:flex;gap:4px;justify-content:flex-end;flex-wrap:wrap">
          <button class="btn secondary small" (click)="reset(u)">Nouveau mot de passe</button>
          <button class="btn secondary small" *ngIf="u.desactive || u.verrouille" (click)="unlock(u)">Activer / déverrouiller</button>
          <button class="btn danger small" *ngIf="!u.desactive" (click)="disable(u)">Désactiver</button></div></td></tr></table>
    <div class="muted" style="margin-top:8px">Un compte est verrouillé 15 minutes après 5 mots de passe erronés. La création de comptes se fait dans « Utilisateurs ».</div>
  </div>`
})
export class SecuriteComponent implements OnInit {
  private api = inject(ApiService);
  tab: 'audit' | 'backup' | 'comptes' = 'audit';
  error = ''; ok = ''; busy = false;
  f: any = { du: '', au: '', utilisateur: '', entite: '', q: '' };
  items: any[] = []; entites: string[] = []; page = 1; total = 0; taille = 50;
  bk: any; users: any[] = [];
  get pages() { return Math.max(1, Math.ceil(this.total / this.taille)); }

  ngOnInit() {
    this.api.get<string[]>('audit/entites').subscribe(r => this.entites = r);
    this.loadAudit();
  }
  loadAudit() {
    this.error = '';
    this.api.get<any>('audit', { ...this.f, page: this.page, taille: this.taille })
      .subscribe({ next: r => { this.items = r.items; this.total = r.total; }, error: e => this.error = ApiService.err(e) });
  }
  loadBackup() { this.api.get<any>('backup').subscribe({ next: r => this.bk = r, error: e => this.error = ApiService.err(e) }); }
  sauvegarder() {
    this.busy = true; this.error = ''; this.ok = '';
    this.api.post<any>('backup/now', {}).subscribe({
      next: r => { this.ok = 'Sauvegarde créée : ' + r.fichier; this.busy = false; this.loadBackup(); },
      error: e => { this.error = ApiService.err(e); this.busy = false; } });
  }
  loadUsers() { this.api.get<any[]>('auth/users').subscribe({ next: r => this.users = r, error: e => this.error = ApiService.err(e) }); }
  reset(u: any) {
    const nouveau = prompt(`Nouveau mot de passe pour ${u.userName} (8 caractères min., majuscule, minuscule, chiffre, symbole) :`);
    if (!nouveau) return;
    this.api.post(`auth/users/${u.id}/reset-password`, { nouveau }).subscribe({ next: () => { this.ok = 'Mot de passe modifié.'; this.error = ''; }, error: e => this.error = ApiService.err(e) });
  }
  unlock(u: any) { this.api.post(`auth/users/${u.id}/deverrouiller`, {}).subscribe({ next: () => this.loadUsers(), error: e => this.error = ApiService.err(e) }); }
  disable(u: any) {
    if (!confirm(`Désactiver le compte ${u.userName} ?`)) return;
    this.api.post(`auth/users/${u.id}/desactiver`, {}).subscribe({ next: () => this.loadUsers(), error: e => this.error = ApiService.err(e) });
  }
}
