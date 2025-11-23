import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./viecquanly-page.component').then(
        (c) => c.ViecquanlyPageComponent
      ),
  },
  {
    path: 'chitiet/:id',
    loadChildren: () =>
      import('./detail-viecquanly/detail-viecquanly-routing').then(
        (m) => m.routes
      ),
  },
  {
    path: 'review/:id',
    loadComponent: () =>
      import('./review-original-job/review-original-job.component').then(
        (c) => c.ReviewOriginalJobComponent
      ),
  },
];
