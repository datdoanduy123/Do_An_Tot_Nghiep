import { ReviewChildJobComponent } from './review-child-job/review-child-job.component';
import { DetailViecquanlyComponent } from './detail-viecquanly.component';
import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./detail-viecquanly.component').then(
        (c) => c.DetailViecquanlyComponent
      ),
  },
  {
    path: 'review/:id',
    loadComponent: () =>
      import('./review-child-job/review-child-job.component').then(
        (c) => c.ReviewChildJobComponent
      ),
  },
];
