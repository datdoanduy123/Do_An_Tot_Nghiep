import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./document-page.component').then((c) => c.DocumentPageComponent),
  },
  {
    path: 'assignJob',
    loadComponent: () =>
      import('./assign-job-page/assign-job-page.component').then(
        (c) => c.AssignJobPageComponent
      ),
  },
];
