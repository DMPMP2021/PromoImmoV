import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards';
import { ShellComponent, HomeRedirectComponent } from './layout/shell.component';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login.component').then(m => m.LoginComponent) },
  { path: 'contact', loadComponent: () => import('./pages/contact.component').then(m => m.ContactComponent) },
  {
    path: '', component: ShellComponent, canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', component: HomeRedirectComponent },
      { path: 'dashboard', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/dashboard.component').then(m => m.DashboardComponent) },
      { path: 'recette', canActivate: [roleGuard('Recette', 'Gerant')], loadComponent: () => import('./pages/recette.component').then(m => m.RecetteComponent) },
      { path: 'budgets', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/budgets.component').then(m => m.BudgetsComponent) },
      { path: 'clients', canActivate: [roleGuard('Gerant', 'Recette', 'Remboursement')], loadComponent: () => import('./pages/clients.component').then(m => m.ClientsComponent) },
      { path: 'clients/:id', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/client-historique.component').then(m => m.ClientHistoriqueComponent) },
      { path: 'contrats', canActivate: [roleGuard('Gerant', 'Recette')], loadComponent: () => import('./pages/contrats.component').then(m => m.ContratsComponent) },
      { path: 'chantiers', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/chantiers.component').then(m => m.ChantiersComponent) },
      { path: 'materiels', canActivate: [roleGuard('Gerant', 'Materiel')], loadComponent: () => import('./pages/materiels.component').then(m => m.MaterielsComponent) },
      { path: 'materiels/:id', canActivate: [roleGuard('Gerant', 'Materiel')], loadComponent: () => import('./pages/materiel-detail.component').then(m => m.MaterielDetailComponent) },
      { path: 'technique', canActivate: [roleGuard('Gerant', 'Technique')], loadComponent: () => import('./pages/technique.component').then(m => m.TechniqueComponent) },
      { path: 'achats', canActivate: [roleGuard('Gerant', 'Achats')], loadComponent: () => import('./pages/achats.component').then(m => m.AchatsComponent) },
      { path: 'remboursements', canActivate: [roleGuard('Gerant', 'Remboursement')], loadComponent: () => import('./pages/remboursements.component').then(m => m.RemboursementsComponent) },
      { path: 'fiscal', canActivate: [roleGuard('Gerant', 'Fiscal')], loadComponent: () => import('./pages/fiscal.component').then(m => m.FiscalComponent) },
      { path: 'personnel', canActivate: [roleGuard('Gerant', 'Personnel')], loadComponent: () => import('./pages/personnel.component').then(m => m.PersonnelComponent) },
      { path: 'suivi', canActivate: [roleGuard('Gerant', 'Suivi')], loadComponent: () => import('./pages/suivi.component').then(m => m.SuiviComponent) },
      { path: 'divers', canActivate: [roleGuard('Gerant', 'Achats')], loadComponent: () => import('./pages/divers.component').then(m => m.DiversComponent) },
      { path: 'reclamations', canActivate: [roleGuard('Gerant', 'Recette')], loadComponent: () => import('./pages/reclamations.component').then(m => m.ReclamationsComponent) },
      { path: 'rentabilite', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/rentabilite.component').then(m => m.RentabiliteComponent) },
      { path: 'impayes', canActivate: [roleGuard('Gerant', 'Recette')], loadComponent: () => import('./pages/impayes.component').then(m => m.ImpayesComponent) },
      { path: 'lots', canActivate: [roleGuard('Gerant', 'Recette')], loadComponent: () => import('./pages/lots.component').then(m => m.LotsComponent) },
      { path: 'securite', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/securite.component').then(m => m.SecuriteComponent) },
      { path: 'compte', loadComponent: () => import('./pages/compte.component').then(m => m.CompteComponent) },
      { path: 'taches/:service', loadComponent: () => import('./pages/taches.component').then(m => m.TachesComponent) },
      { path: 'notifications', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/notifications.component').then(m => m.NotificationsComponent) },
      { path: 'utilisateurs', canActivate: [roleGuard('Gerant')], loadComponent: () => import('./pages/utilisateurs.component').then(m => m.UtilisateursComponent) }
    ]
  },
  { path: '**', redirectTo: '' }
];
