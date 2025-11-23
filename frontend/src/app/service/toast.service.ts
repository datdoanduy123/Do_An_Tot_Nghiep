import { Injectable } from '@angular/core';
// import { ToastComponent } from '../../../component/toast/toast.component';
import { BehaviorSubject } from 'rxjs';

export interface Toast {
  id: number;
  title: string;
  message: string;
  type: 'success' | 'danger' | 'info' | 'warning';
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private toasts: Toast[] = [];
  private toastsSubject = new BehaviorSubject<Toast[]>([]);
  toasts$ = this.toastsSubject.asObservable();

  show(
    title: string,
    message: string,
    type: 'success' | 'danger' | 'info' | 'warning'
  ) {
    const id = Date.now();
    const newToast: Toast = { id, title, message, type };
    this.toasts.push(newToast);
    this.toastsSubject.next(this.toasts);

    setTimeout(() => {
      this.removeToast(id);
    }, 3300);
    return id;
  }

  Success(message: string) {
    this.show('Thành công', message, 'success');
  }
  Info(message: string) {
    this.show('Thông báo', message, 'info');
  }
  Warning(message: string) {
    this.show('Cảnh báo', message, 'warning');
  }

  Error(message: string) {
    this.show('Lỗi', message, 'danger');
  }
  ForFeature() {
    this.show('Thông báo', 'Tính năng đang phát triển !', 'info');
  }
  removeToast(id: number) {
    this.toasts = this.toasts.filter((toast) => toast.id !== id);
    this.toastsSubject.next(this.toasts);
  }
}
