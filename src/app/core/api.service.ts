import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { tap } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private base = environment.apiUrl + '/api';

  private params(p?: Record<string, any>) {
    let hp = new HttpParams();
    Object.entries(p ?? {}).forEach(([k, v]) => { if (v !== null && v !== undefined && v !== '') hp = hp.set(k, String(v)); });
    return hp;
  }
  get<T>(path: string, p?: Record<string, any>) { return this.http.get<T>(`${this.base}/${path}`, { params: this.params(p) }); }
  post<T>(path: string, body: any) { return this.http.post<T>(`${this.base}/${path}`, body); }
  put<T>(path: string, body: any) { return this.http.put<T>(`${this.base}/${path}`, body); }
  blob(path: string) { return this.http.get(`${this.base}/${path}`, { responseType: 'blob' }); }
  postForm<T>(path: string, fd: FormData) { return this.http.post<T>(`${this.base}/${path}`, fd); }
  delete<T>(path: string) { return this.http.delete<T>(`${this.base}/${path}`); }

  /** Télécharge un fichier généré par l'API (PDF, Excel…) avec le jeton d'authentification. */
  download(path: string, p: Record<string, any>, filename: string) {
    return this.http.get(`${this.base}/${path}`, { params: this.params(p), responseType: 'blob' }).pipe(tap(blob => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = filename;
      document.body.appendChild(a); a.click(); a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    }));
  }

  /** Extrait un message lisible d'une erreur HTTP */
  static err(e: any): string {
    if (typeof Blob !== 'undefined' && e?.error instanceof Blob)
      return e.status === 403 ? 'Accès refusé pour votre rôle.' : 'Export impossible (erreur ' + e.status + ').';
    const b = e?.error;
    if (typeof b === 'string' && b) return b;
    if (b?.message) return b.message;
    if (b?.errors) return Object.values(b.errors).flat().join(' ');
    if (e?.status === 0) return 'Serveur injoignable.';
    if (e?.status === 403) return 'Accès refusé pour votre rôle.';
    return b?.title || e?.message || 'Erreur inconnue.';
  }
}
