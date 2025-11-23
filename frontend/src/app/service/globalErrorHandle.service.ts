import { ErrorHandler, Injectable, Injector } from '@angular/core';
import { ToastService } from './toast.service';
import { HttpErrorResponse } from '@angular/common/http';

@Injectable({
  providedIn: 'root',
})
export class GlobalErrorService implements ErrorHandler {
  constructor(private injector: Injector) {}
  handleError(error: any): void {
    if (error instanceof HttpErrorResponse) {
    } else {
      const toastService = this.injector.get(ToastService);
      console.log('Global  error caught: ', error);
      toastService.Error(error.message);
    }
  }
}
