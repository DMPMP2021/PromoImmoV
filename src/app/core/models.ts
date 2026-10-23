export interface Notif { id: number; type: string; message: string; referenceId?: number; lu: boolean; date: string; }
export interface UserInfo { token: string; nom: string; roles: string[]; }
export const MODES = [{ v: 'Especes', l: 'Espèces' }, { v: 'Bancaire', l: 'Bancaire (chèque / virement)' }];
export const STATUTS = [{ v: 'EnCours', l: 'En cours' }, { v: 'Termine', l: 'Terminé' }, { v: 'Suspendu', l: 'Suspendu' }];

export interface Opt { v: string; l: string; }
export const SERVICES: Opt[] = [
  { v: 'Technique', l: 'Service technique' }, { v: 'Achats', l: 'Achats & approvisionnement' },
  { v: 'Remboursement', l: 'Remboursements' }, { v: 'Fiscal', l: 'Fiscal & parafiscal' },
  { v: 'Personnel', l: 'Personnel' }, { v: 'Suivi', l: 'Suivi journalier' }, { v: 'Divers', l: 'Divers' }];
export const STATUTS_TACHE: Opt[] = [{ v: 'APrevoir', l: 'À prévoir' }, { v: 'EnCours', l: 'En cours' }, { v: 'Termine', l: 'Terminé' }, { v: 'Bloque', l: 'Bloqué' }];
export const PRIORITES: Opt[] = [{ v: 'Basse', l: 'Basse' }, { v: 'Normale', l: 'Normale' }, { v: 'Haute', l: 'Haute' }, { v: 'Urgente', l: 'Urgente' }];
export const STATUTS_COMMANDE: Opt[] = [{ v: 'Demandee', l: 'Demandée' }, { v: 'Commandee', l: 'Commandée' }, { v: 'Livree', l: 'Livrée' }, { v: 'Payee', l: 'Payée' }, { v: 'Annulee', l: 'Annulée' }, { v: 'AValider', l: 'À valider (gérant)' }];
export const TYPES_REMB: Opt[] = [{ v: 'Desistement', l: 'Désistement' }, { v: 'TropPercu', l: 'Trop-perçu' }];
export const STATUTS_REMB: Opt[] = [{ v: 'Demandee', l: 'À valider' }, { v: 'Validee', l: 'Validée – à payer' }, { v: 'Rejetee', l: 'Rejetée' }, { v: 'Payee', l: 'Payée' }];
export const TYPES_OBLIG: Opt[] = [{ v: 'Fiscale', l: 'Fiscale' }, { v: 'Parafiscale', l: 'Parafiscale' }];
export const STATUTS_OBLIG: Opt[] = [{ v: 'APayer', l: 'À payer' }, { v: 'Payee', l: 'Payée' }];
export const STATUTS_POINTAGE: Opt[] = [{ v: 'Present', l: 'Présent' }, { v: 'Retard', l: 'Retard' }, { v: 'Absent', l: 'Absent' }, { v: 'Conge', l: 'Congé' }, { v: 'Maladie', l: 'Maladie' }];
export const TYPES_PAIE: Opt[] = [{ v: 'Salaire', l: 'Salaire' }, { v: 'Avance', l: 'Avance' }, { v: 'Prime', l: 'Prime' }];
export const STATUTS_MISSION: Opt[] = [{ v: 'Planifiee', l: 'Planifiée' }, { v: 'EnCours', l: 'En cours' }, { v: 'Terminee', l: 'Terminée' }, { v: 'Annulee', l: 'Annulée' }];
export const STATUTS_RECL: Opt[] = [{ v: 'Nouvelle', l: 'Nouvelle' }, { v: 'EnCours', l: 'En cours' }, { v: 'Traitee', l: 'Traitée' }, { v: 'Rejetee', l: 'Rejetée' }];

export function lab(list: Opt[], v: string) { return list.find(x => x.v === v)?.l ?? v; }
export function today() { return new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 10); }
export function monthNow() { return today().slice(0, 7); }

export const STATUTS_LOT: Opt[] = [{ v: 'Disponible', l: 'Disponible' }, { v: 'Reserve', l: 'Réservé' }, { v: 'Vendu', l: 'Vendu' }];
