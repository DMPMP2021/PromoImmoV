import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../core/notification.service';

@Component({
  selector: 'app-notifications', standalone: true, imports: [CommonModule],
  template: `
  <div class="row"><h1>Notifications</h1><span class="spacer"></span>
    <button class="btn secondary" (click)="n.markAllRead()">Tout marquer comme lu</button></div>
  <div class="card"><table><tr><th>Date</th><th>Message</th><th></th></tr>
    <tr *ngFor="let x of n.items" [style.font-weight]="x.lu ? 400 : 600">
      <td>{{ x.date | date:'dd/MM/yyyy HH:mm' }}</td><td>{{ x.message }}</td>
      <td class="right"><button class="btn secondary small" *ngIf="!x.lu" (click)="n.markRead(x)">Marquer lu</button></td></tr></table>
    <div class="muted" *ngIf="!n.items.length">Aucune notification.</div></div>`
})
export class NotificationsComponent { n = inject(NotificationService); }
