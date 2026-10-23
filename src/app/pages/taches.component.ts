import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../core/api.service';
import { LabPipe, BadgePipe } from '../core/lab.pipe';
import { PRIORITES, SERVICES, STATUTS_TACHE, lab } from '../core/models';

@Component({
  selector: 'app-taches', standalone: true, imports: [CommonModule, FormsModule, LabPipe, BadgePipe],
  template: `
  <div class="row"><h1>{{ titre }}</h1><span class="spacer"></span>
    <select style="width:190px" [(ngModel)]="fStatut"><option value="">Tous les statuts</option>
      <option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></div>
  <div class="card">
    <h2>{{ form.id ? 'Modifier la tâche' : 'Nouvelle tâche' }}</h2>
    <div class="form-grid">
      <div *ngIf="service==='all'"><label>Service</label><select [(ngModel)]="form.service"><option *ngFor="let s of services" [value]="s.v">{{ s.l }}</option></select></div>
      <div style="grid-column:span 2"><label>Titre *</label><input [(ngModel)]="form.titre"></div>
      <div><label>Chantier concerné</label><select [(ngModel)]="form.chantierId"><option [ngValue]="null">— aucun —</option>
        <option *ngFor="let c of chantiers" [ngValue]="c.id">{{ c.nom }}</option></select></div>
      <div><label>Assignée à</label><input [(ngModel)]="form.assigneA"></div>
      <div><label>Priorité</label><select [(ngModel)]="form.priorite"><option *ngFor="let p of priorites" [value]="p.v">{{ p.l }}</option></select></div>
      <div><label>Échéance</label><input type="date" [(ngModel)]="form.dateEcheance"></div>
      <div style="grid-column:1/-1"><label>Description</label><input [(ngModel)]="form.description"></div>
    </div>
    <div class="alert err" *ngIf="error">{{ error }}</div>
    <div class="row" style="margin-top:10px"><button class="btn" [disabled]="!form.titre" (click)="save()">Enregistrer</button>
      <button class="btn secondary" *ngIf="form.id" (click)="form=blank()">Annuler</button></div>
  </div>
  <div class="card">
    <table><tr><th *ngIf="service==='all'">Service</th><th>Tâche</th><th>Chantier</th><th>Assignée à</th><th>Priorité</th><th>Échéance</th><th>Statut</th><th></th></tr>
      <tr *ngFor="let t of filtered">
        <td *ngIf="service==='all'">{{ t.service | lab:services }}</td>
        <td><b>{{ t.titre }}</b><div class="muted" style="font-size:12px">{{ t.description }}</div></td>
        <td>{{ t.chantierNom }}</td><td>{{ t.assigneA }}</td>
        <td><span [class]="t.priorite | badge">{{ t.priorite | lab:priorites }}</span></td>
        <td>{{ t.dateEcheance | date:'dd/MM/yyyy' }} <span class="badge danger" *ngIf="t.enRetard">En retard</span></td>
        <td><select [ngModel]="t.statut" (ngModelChange)="setStatut(t,$event)">
          <option *ngFor="let s of statuts" [value]="s.v">{{ s.l }}</option></select></td>
        <td class="right"><button class="btn secondary small" (click)="edit(t)">Modifier</button></td></tr></table>
    <div class="muted" *ngIf="!filtered.length">Aucune tâche.</div>
  </div>`
})
export class TachesComponent implements OnInit {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  service = 'all'; list: any[] = []; chantiers: any[] = []; error = ''; fStatut = '';
  statuts = STATUTS_TACHE; priorites = PRIORITES; services = SERVICES;
  form: any = this.blank();

  get titre() { return this.service === 'all' ? 'Toutes les tâches' : 'Tâches — ' + lab(SERVICES, this.service); }
  get filtered() { return this.fStatut ? this.list.filter(t => t.statut === this.fStatut) : this.list; }
  blank() { return { id: null, service: this.service === 'all' ? 'Technique' : this.service, titre: '', description: '', chantierId: null, assigneA: '', priorite: 'Normale', statut: 'APrevoir', dateEcheance: '' }; }

  ngOnInit() {
    this.api.get<any[]>('chantiers').subscribe(r => this.chantiers = r);
    this.route.paramMap.subscribe(p => { this.service = p.get('service') || 'all'; this.form = this.blank(); this.load(); });
  }
  load() {
    this.api.get<any[]>('taches', { service: this.service === 'all' ? null : this.service })
      .subscribe({ next: r => { this.list = r; this.error = ''; }, error: e => this.error = ApiService.err(e) });
  }
  edit(t: any) { this.form = { ...t, service: t.service, dateEcheance: t.dateEcheance ? t.dateEcheance.slice(0, 10) : '' }; window.scrollTo(0, 0); }
  save() {
    const body = { ...this.form, dateEcheance: this.form.dateEcheance || null, chantierId: this.form.chantierId || null };
    const req = this.form.id ? this.api.put(`taches/${this.form.id}`, body) : this.api.post('taches', body);
    req.subscribe({ next: () => { this.form = this.blank(); this.load(); }, error: e => this.error = ApiService.err(e) });
  }
  setStatut(t: any, s: string) {
    this.api.put(`taches/${t.id}/statut`, { statut: s }).subscribe({ next: () => this.load(), error: e => this.error = ApiService.err(e) });
  }
}
