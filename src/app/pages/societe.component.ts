import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ApiService } from '../core/api.service';

@Component({
  selector: 'app-societe', standalone: true, imports: [CommonModule, FormsModule],
  template: `
  <div class="row"><h1>Identité, signature et QR code</h1><span class="spacer"></span>
    <button class="btn secondary" (click)="apercu()">📄 Aperçu d'un document officiel (PDF)</button></div>
  <div class="muted" style="margin-bottom:12px">Ces informations apparaissent sur tous les documents officiels : reçus, situation client, bulletins de paie, états des impayés, obligations fiscales, journal des encaissements.</div>
  <div class="alert err" *ngIf="error">{{ error }}</div><div class="alert ok" *ngIf="ok">{{ ok }}</div>

  <div class="grid grid-2">
    <div class="card"><h2>Informations de l'entreprise</h2>
      <div class="form-grid">
        <div style="grid-column:1/-1"><label>Raison sociale *</label><input [(ngModel)]="f.nom"></div>
        <div style="grid-column:1/-1"><label>Adresse</label><input [(ngModel)]="f.adresse"></div>
        <div><label>Téléphone</label><input [(ngModel)]="f.telephone"></div><div><label>E-mail</label><input [(ngModel)]="f.email"></div>
        <div style="grid-column:1/-1"><label>Identifiants légaux</label><input [(ngModel)]="f.identifiants" placeholder="NIF : … — RC : … — NIS : … — AI : …"></div>
        <div style="grid-column:1/-1"><label>Pied de page</label><input [(ngModel)]="f.piedDePage" placeholder="Mentions légales, coordonnées bancaires…"></div>
        <div style="grid-column:1/-1"><label>Adresse publique de l'application (pour le QR code)</label>
          <input [(ngModel)]="f.urlPublique" placeholder="https://gestion.monentreprise.dz"></div>
        <div><label>Nom du signataire</label><input [(ngModel)]="f.signataireNom"></div>
        <div><label>Fonction du signataire</label><input [(ngModel)]="f.signataireFonction" placeholder="Le Gérant"></div></div>
      <div class="muted" style="margin-top:8px"><b>Important :</b> le QR code ouvre l'adresse publique ci-dessus. Elle doit être joignable depuis le téléphone de la personne qui scanne
        (exemple sur le même réseau Wi-Fi : http://192.168.1.10:4200 ; en ligne : le nom de domaine de votre application).</div>
      <div style="margin-top:12px"><button class="btn" [disabled]="!f.nom || busy" (click)="save()">Enregistrer</button></div></div>

    <div>
      <div class="card"><h2>Logo</h2>
        <div style="min-height:90px;display:flex;align-items:center;gap:14px">
          <img *ngIf="logoUrl" [src]="logoUrl" style="max-height:90px;max-width:200px;object-fit:contain;border:1px solid var(--border);padding:4px">
          <span class="muted" *ngIf="!logoUrl">Aucun logo enregistré.</span></div>
        <div class="row" style="margin-top:10px"><input type="file" accept="image/png,image/jpeg" (change)="envoyer('logo', $event)" style="max-width:260px">
          <button class="btn danger small" *ngIf="logoUrl" (click)="retirer('logo')">Retirer</button></div>
        <div class="muted" style="margin-top:6px;font-size:12px">PNG ou JPEG, 600 Ko maximum. Format carré ou horizontal conseillé.</div></div>
      <div class="card"><h2>Signature</h2>
        <div style="min-height:90px;display:flex;align-items:center;gap:14px">
          <img *ngIf="sigUrl" [src]="sigUrl" style="max-height:90px;max-width:220px;object-fit:contain;border:1px solid var(--border);padding:4px;background:#fff">
          <span class="muted" *ngIf="!sigUrl">Aucune signature enregistrée.</span></div>
        <div class="row" style="margin-top:10px"><input type="file" accept="image/png,image/jpeg" (change)="envoyer('signature', $event)" style="max-width:260px">
          <button class="btn danger small" *ngIf="sigUrl" (click)="retirer('signature')">Retirer</button></div>
        <div class="muted" style="margin-top:6px;font-size:12px">Idéalement un PNG à fond transparent (signature scannée puis détourée). Elle n'est ajoutée qu'aux documents officiels, pas aux documents internes
          (rentabilité, journal d'audit). Il s'agit d'une image : ce n'est pas une signature électronique qualifiée au sens juridique.</div></div>
    </div>
  </div>`
})
export class SocieteComponent implements OnInit {
  private api = inject(ApiService);
  private san = inject(DomSanitizer);
  f: any = {}; error = ''; ok = ''; busy = false;
  logoUrl: SafeUrl | null = null; sigUrl: SafeUrl | null = null;

  ngOnInit() { this.load(); }
  load() {
    this.api.get<any>('societe').subscribe({
      next: r => { this.f = r; this.charger('logo', r.aLogo); this.charger('signature', r.aSignature); },
      error: e => this.error = ApiService.err(e) });
  }
  private charger(type: 'logo' | 'signature', existe: boolean) {
    const set = (u: SafeUrl | null) => { if (type === 'logo') this.logoUrl = u; else this.sigUrl = u; };
    if (!existe) { set(null); return; }
    this.api.blob(`societe/${type}`).subscribe(b => set(this.san.bypassSecurityTrustUrl(URL.createObjectURL(b))));
  }
  save() {
    this.busy = true; this.error = ''; this.ok = '';
    this.api.put('societe', this.f).subscribe({
      next: () => { this.ok = 'Informations enregistrées.'; this.busy = false; },
      error: e => { this.error = ApiService.err(e); this.busy = false; } });
  }
  envoyer(type: 'logo' | 'signature', ev: Event) {
    const input = ev.target as HTMLInputElement;
    const file = input.files && input.files[0];
    if (!file) return;
    this.error = ''; this.ok = '';
    const fd = new FormData();
    fd.append('fichier', file);
    this.api.postForm(`societe/${type}`, fd).subscribe({
      next: () => { this.ok = type === 'logo' ? 'Logo enregistré.' : 'Signature enregistrée.'; input.value = ''; this.load(); },
      error: e => { this.error = ApiService.err(e); input.value = ''; } });
  }
  retirer(type: 'logo' | 'signature') {
    if (!confirm('Retirer cette image des documents ?')) return;
    this.api.delete(`societe/${type}`).subscribe({ next: () => this.load(), error: e => this.error = ApiService.err(e) });
  }
  apercu() {
    this.error = '';
    this.api.download('exports/apercu', { format: 'pdf' }, 'apercu_document.pdf').subscribe({ error: e => this.error = ApiService.err(e) });
  }
}
