import { Injectable } from '@angular/core';
import {
  CanActivate,
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  Router,
} from '@angular/router';

import { AuthService } from '../service/auth.service';

@Injectable({
  providedIn: 'root',
})
export class AuthGuard implements CanActivate {
  constructor(private router: Router, private authService: AuthService) {}

  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): boolean {
    const accessToken = this.authService.getAccessToken();

    let url: string = state.url;
    if (accessToken === null || accessToken === '' || accessToken === 'false') {
      this.router.navigate(['/login']);
      return false;
    } else {
      return true;
    }
  }
}
