import { Injectable, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { environment } from '../../environments/environment';
import { ApiService } from './api.service';
import { AuthService } from './auth.service';
import { Notif } from './models';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  private hub?: HubConnection;
  items: Notif[] = [];
  toasts: Notif[] = [];

  get unread() { return this.items.filter(n => !n.lu).length; }

  load() { this.api.get<Notif[]>('notifications').subscribe(r => this.items = r); }

  start() {
    if (this.hub) return;
    this.hub = new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/notifications`, { accessTokenFactory: () => this.auth.token })
      .withAutomaticReconnect().build();
    this.hub.on('nouvelleNotification', (n: Notif) => {
      this.items = [n, ...this.items];
      this.toasts.push(n);
      setTimeout(() => this.toasts = this.toasts.filter(t => t !== n), 8000);
    });
    this.hub.start().catch(err => console.error('SignalR', err));
  }

  stop() { this.hub?.stop(); this.hub = undefined; this.items = []; this.toasts = []; }

  markRead(n: Notif) { if (!n.lu) this.api.put(`notifications/${n.id}/lu`, {}).subscribe(() => n.lu = true); }
  markAllRead() { this.items.filter(n => !n.lu).forEach(n => this.markRead(n)); }
}
