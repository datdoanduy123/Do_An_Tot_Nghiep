import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./viecduocgiao-page.component').then(
        (c) => c.ViecduocgiaoPageComponent
      ),
  },
  {
    path: 'chitiet/:id',
    loadComponent: () =>
      import('./detail-viecduocgiao/detail-viecduocgiao.component').then(
        (c) => c.DetailViecduocgiaoComponent
      ),
  },
 
];
