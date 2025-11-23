import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { AuthService } from '../service/auth.service';

@Injectable({
  providedIn: 'root',
})
export class LoginGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean | UrlTree {
    const accessToken = this.authService.getAccessToken();

    // Nếu đã có token → chặn vào login, chuyển hướng về trang chính
    if (accessToken && accessToken !== 'false') {
      return this.router.createUrlTree(['/']);
    }

    return true; // chưa login → cho vào trang login
  }
}
