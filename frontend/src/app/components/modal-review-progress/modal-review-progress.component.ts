import { SendRemindService } from './../../service/send-remind.service';
import { Component } from '@angular/core';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzInputModule } from 'ng-zorro-antd/input';
import { FormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { ToastService } from '../../service/toast.service';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { ReviewChildJobService } from '../../service/review-child-job.service';
@Component({
  selector: 'app-modal-review-progress',
  imports: [
    NzModalModule,
    NzInputModule,
    FormsModule,
    NzSelectModule,
    NzDividerModule,
    NzIconModule,
    NzButtonModule,
  ],
  templateUrl: './modal-review-progress.component.html',
  styleUrl: './modal-review-progress.component.css',
})
export class ModalReviewProgressComponent {
  userSelectedId: string | null = null;
  messageValue: string = '';
  isVisible = false;
  isOkLoading = false;
  accpectProgress = false;
  constructor(
    private toastService: ToastService,
    private reviewChildJobService: ReviewChildJobService
  ) {}
  showModal(id: string | null, accpectProgress: boolean): void {
    this.accpectProgress = accpectProgress;
    this.userSelectedId = id;
    this.isVisible = true;
  }

  handleOk(): void {
    this.isOkLoading = true;
    if (!this.userSelectedId) {
      this.toastService.Warning('Lỗi không có người nhắc nhở !');
      return;
    }
    setTimeout(() => {
      this.isOkLoading = true;

      const progressId = this.userSelectedId as string;

      if (this.accpectProgress) {
        // Accept report
        this.reviewChildJobService.acceptProgress(progressId).subscribe({
          next: () => {
            this.toastService.Success('Phê duyệt báo cáo thành công !');
            // refresh review list after accepting
            this.reviewChildJobService.triggerRefresh();
          },
          error: () => {
            this.toastService.Warning('Lỗi phê duyệt báo cáo !');
          },
        });
      } else {
        // Reject flow is not provided by backend yet
        this.toastService.Warning('Hiện chưa hỗ trợ từ chối báo cáo.');
      }

      this.isVisible = false;
      this.isOkLoading = false;
      return;
    }, 300);
  }

  handleCancel(): void {
    this.isVisible = false;
  }
}
