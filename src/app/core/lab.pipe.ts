import { Pipe, PipeTransform } from '@angular/core';
import { Opt, lab } from './models';

@Pipe({ name: 'lab', standalone: true })
export class LabPipe implements PipeTransform {
  transform(v: any, list: Opt[]) { return lab(list, v); }
}

/** Couleur de badge selon la valeur du statut */
@Pipe({ name: 'badge', standalone: true })
export class BadgePipe implements PipeTransform {
  transform(v: string) {
    if (['Termine', 'Terminee', 'Livree', 'Payee', 'Traitee', 'Validee', 'Present', 'Disponible'].includes(v)) return 'badge ok';
    if (['Bloque', 'Annulee', 'Rejetee', 'Absent', 'Urgente', 'Maladie'].includes(v)) return 'badge danger';
    if (['EnCours', 'Retard', 'Demandee', 'Haute', 'Nouvelle', 'Commandee', 'APayer', 'Planifiee', 'Reserve', 'AValider'].includes(v)) return 'badge warn';
    return 'badge';
  }
}
