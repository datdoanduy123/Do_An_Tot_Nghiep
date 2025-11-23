import { Injectable } from '@angular/core';
import {
  HttpInterceptor,
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpErrorResponse,
  HttpClient,
} from '@angular/common/http';
import { Observable, BehaviorSubject, throwError } from 'rxjs';
import { catchError, filter, switchMap, take, finalize } from 'rxjs/operators';
import { ToastService } from '../service/toast.service';
import { AuthService } from '../service/auth.service';
import { environment } from '../environment/environment';

@Injectable()
export class ErrorInterceptor implements HttpInterceptor {
  private isRefreshing = false;
  private refreshTokenSubject = new BehaviorSubject<string | null>(null);

  constructor(
    private http: HttpClient,
    private toastService: ToastService,
    private authService: AuthService
  ) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const accessToken = this.authService.getAccessToken();

    // Thêm token vào header nếu có
    const clonedReq = accessToken
      ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
      : req;

    return next.handle(clonedReq).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) {
          return this.handle401Error(clonedReq, next);
        }
        this.handleHttpError(error);
        return throwError(() => error);
      })
    );
  }

  private handleHttpError(error: HttpErrorResponse): void {
    switch (error.status) {
      case 400:
        this.toastService.Error('Yêu cầu không hợp lệ');
        break;
      case 404:
        this.toastService.Error('Không tìm thấy tài nguyên');
        break;
      case 500:
        this.toastService.Error('Lỗi máy chủ');
        break;
      default:
        this.toastService.Error(error.message);
    }
  }

  private handle401Error(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    if (!this.isRefreshing) {
      this.isRefreshing = true;
      this.refreshTokenSubject.next(null);

      const refreshToken = this.authService.getRefreshToken();
      if (!refreshToken) {
        this.authService.logout();
        return throwError(() => new Error('Hết phiên làm việc, vui lòng đăng nhập lại'));
      }

      // 🔥 Gửi refresh token qua Header (đúng với backend)
      return this.http
        .post<{ data: { accessToken: string; refreshToken: string } }>(
          `${environment.SERVICE_API}auth/refresh`,
          {}, // body rỗng
          {
            headers: { RefreshToken: refreshToken },
          }
        )
        .pipe(
          switchMap((res) => {
            const data = res.data;
            if (!data?.accessToken || !data?.refreshToken) {
              this.authService.logout();
              return throwError(() => new Error('Hết phiên làm việc, vui lòng đăng nhập lại'));
            }

            this.authService.setTokens(data.accessToken, data.refreshToken);
            this.refreshTokenSubject.next(data.accessToken);

            const cloned = req.clone({
              setHeaders: { Authorization: `Bearer ${data.accessToken}` },
            });
            return next.handle(cloned);
          }),
          catchError((err) => {
            this.authService.logout();
            const msg = err.error?.message || 'Hết phiên làm việc, vui lòng đăng nhập lại';
            this.toastService.Error(msg);
            return throwError(() => new Error(msg));
          }),
          finalize(() => {
            this.isRefreshing = false;
          })
        );
    } else {
      // Nếu đang refresh → đợi token mới
      return this.refreshTokenSubject.pipe(
        filter((token) => token !== null),
        take(1),
        switchMap((token) => {
          const cloned = req.clone({
            setHeaders: { Authorization: `Bearer ${token}` },
          });
          return next.handle(cloned);
        })
      );
    }
  }
}
