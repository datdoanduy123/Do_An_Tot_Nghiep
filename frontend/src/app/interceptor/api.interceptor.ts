import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse,
  HttpClient,
} from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, throwError, switchMap, catchError } from 'rxjs';
import { ToastService } from '../service/toast.service';
import { environment } from '../environment/environment';

@Injectable()
export class ApiInterceptor implements HttpInterceptor {
  private isRefreshing = false;
  private refreshTokenRequest: Observable<any> | null = null;

  constructor(private toastService: ToastService, private http: HttpClient) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const excludedUrl = ['/login', '/refresh'];
    if (excludedUrl.some((url) => req.url.includes(url))) {
      return next.handle(req);
    }

    const token = localStorage.getItem('accessToken');
    const cloned = token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

    return next.handle(cloned).pipe(
      catchError((error) => {
        if (error instanceof HttpErrorResponse && error.status === 401) {
          return this.handle401Error(req, next);
        }
        return throwError(() => error);
      })
    );
  }

  private handle401Error(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    if (!this.isRefreshing) {
      this.isRefreshing = true;

      const refreshToken = localStorage.getItem('refreshToken');
      if (!refreshToken) {
        this.logout();
        return throwError(() => new Error('No refresh token'));
      }

      // 🔥 Gọi API refresh gửi token qua header
      this.refreshTokenRequest = this.http
        .post<any>(
          `${environment.SERVICE_API}auth/refresh`,
          {},
{ headers: { RefreshToken: refreshToken || '' } }
        )
        .pipe(
          switchMap((res) => {
            this.isRefreshing = false;
            this.refreshTokenRequest = null;

            localStorage.setItem('accessToken', res.data.accessToken);
            localStorage.setItem('refreshToken', res.data.refreshToken);

            // ✅ Gửi lại request cũ với accessToken mới
            const cloned = req.clone({
              setHeaders: { Authorization: `Bearer ${res.data.accessToken}` },
            });
            return next.handle(cloned);
          }),
          catchError((err) => {
            this.isRefreshing = false;
            this.logout();
            return throwError(() => err);
          })
        );

      return this.refreshTokenRequest;
    }

    // Nếu đang refresh → chờ request refresh hoàn tất
    return this.refreshTokenRequest!;
  }

  private logout() {
    localStorage.clear();
    window.location.href = '/login';
  }
}
