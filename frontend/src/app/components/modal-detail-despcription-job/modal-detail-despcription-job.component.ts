import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';

@Component({
  selector: 'app-modal-detail-despcription-job',
  imports: [
    CommonModule, 
    NzModalModule,
    NzButtonModule
  ],
  templateUrl: './modal-detail-despcription-job.component.html',
  styleUrl: './modal-detail-despcription-job.component.css',
})
export class ModalDetailDespcriptionJobComponent {
  @Input() desp!: string;
  isVisible = false;
  isConfirmLoading = false;

  formatDescription(description: string): string {
    if (!description) return 'Không có nội dung';
    
    // Nếu là HTML content, trả về nguyên bản
    if (description.includes('<') && description.includes('>')) {
      return description;
    }
    
    // Nếu là plain text với \n, chuyển đổi thành <br>
    return description
      .replace(/\n/g, '<br>')
      .replace(/\r/g, '');
  }

  
  showModal(): void {
    this.isVisible = true;
  }

  handleOk(): void {
    this.isConfirmLoading = true;
    setTimeout(() => {
      this.isVisible = false;
      this.isConfirmLoading = false;
    }, 1000);
  }

  handleCancel(): void {
    this.isVisible = false;
  }
}
