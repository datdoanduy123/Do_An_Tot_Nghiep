import { Routes } from '@angular/router';
import { AuthGuard } from './guard/auth.guard';
import { LoginGuard } from './guard/login.guard';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [LoginGuard],
    loadComponent: () =>
      import('./pages/auth/login/loginPage.component').then(
        (c) => c.LoginPageComponent
      ),
  },
  {
    path: 'forgot-password',
    canActivate: [LoginGuard],
    loadComponent: () => 
        import('./pages/auth/forgot-password/forgotPassword.component').then(
          (c) => c.ForgotPasswordComponent 
        ),
    
  },
  {
    path:'reset-password',
    canActivate: [LoginGuard],
    loadComponent:() => 
      import('./pages/auth/reset-password/reset-password.component').then(
        (c)=> c.ResetPasswordComponent
      ),
  },
  {
    path: '',
    canActivate: [AuthGuard],

    loadChildren: () => import('./layout/main-routing').then((m) => m.routes),
  },
  {
    path: '**',
    loadComponent: () =>
      import('./components/PageNotFound/PageNotFound.component').then(
        (c) => c.PageNotFoundComponent
      ),
  },

];
