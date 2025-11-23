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

@Component({
  selector: 'app-modal-send-remind',
  imports: [
    NzModalModule,
    NzInputModule,
    FormsModule,
    NzSelectModule,
    NzDividerModule,
    NzIconModule,
    NzButtonModule,
  ],
  templateUrl: './modal-send-remind.component.html',
  styleUrl: './modal-send-remind.component.css',
})
export class ModalSendRemindComponent {
  userSelectedId: string | null = null;
  taskId: number | null = null; // Thêm taskId
  statusValue: string = '';
  messageValue: string = '';
  selectedTitle: string = '';
  isVisible = false;
  isOkLoading = false;
  listOfItem = ['Chậm tiến độ', 'Chưa nộp báo cáo'];
  index = 0;

  constructor(
    private toastService: ToastService,
    private sendRemindService: SendRemindService
  ) {}

  // Cập nhật showModal để nhận thêm taskId
  showModal(userId: string | null, taskId?: number): void {
    this.userSelectedId = userId;
    this.taskId = taskId || null;
    this.messageValue = ''; // Reset message
    this.isVisible = true;
  }

  handleOk(): void {
    this.isOkLoading = true;
    
    if (!this.userSelectedId) {
      this.toastService.Warning('Lỗi không có người nhắc nhở !');
      this.isOkLoading = false;
      return;
    }

    if (!this.messageValue.trim()) {
      this.toastService.Warning('Vui lòng nhập tin nhắn nhắc nhở !');
      this.isOkLoading = false;
      return;
    }

    setTimeout(() => {
      // Sử dụng API mới nếu có taskId, ngược lại dùng API cũ
      if (this.taskId) {
        this.sendRemindService.sendRemindToUser(
          this.taskId, 
          parseInt(this.userSelectedId!), 
          this.messageValue
        ).subscribe({
          next: () => {
            this.toastService.Success('Gửi nhắc nhở thành công !');
          },
          error: (err) => {
            this.toastService.Warning('Gửi nhắc nhở không thành công !');
          },
        });
      } else {
        // Fallback to old API
        const obj = {
          title: this.selectedTitle,
          message: this.messageValue,
          userIds: [this.userSelectedId],
          triggerTime: new Date().toISOString(),
        };
        this.sendRemindService.sendRemindUsers(obj).subscribe({
          next: () => {
            this.toastService.Success('Gửi nhắc nhở thành công !');
          },
          error: (err) => {
            this.toastService.Warning('Gửi nhắc nhở không thành công !');
          },
        });
      }

      this.isVisible = false;
      this.isOkLoading = false;
    }, 1000);
  }

  handleCancel(): void {
    this.isVisible = false;
  }

  addItem(input: HTMLInputElement): void {
    const value = input.value;
    if (this.listOfItem.indexOf(value) === -1) {
      this.listOfItem = [
        ...this.listOfItem,
        input.value || `New item ${this.index++}`,
      ];
    }
  }
}