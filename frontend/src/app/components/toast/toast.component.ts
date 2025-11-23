import { Component, inject, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { CommonModule, NgClass } from '@angular/common';
import { Toast, ToastService } from '../../service/toast.service';

@Component({
  selector: 'app-toast',
  standalone: true,
  templateUrl: './toast.component.html',
  styleUrls: ['./toast.component.css'],
  imports: [CommonModule, NgClass],
})
export class ToastComponent implements OnInit {
  private toastService = inject(ToastService);
  toasts: Toast[] = [];
  private subscription!: Subscription;

  ngOnInit(): void {
    this.subscription = this.toastService.toasts$.subscribe((toasts) => {
      this.toasts = toasts;
    });
  }

  removeToast(id: number) {
    this.toastService.removeToast(id);
  }

  ngOnDestroy() {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }
}
