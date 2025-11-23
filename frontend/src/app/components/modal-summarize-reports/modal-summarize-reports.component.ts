import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';

@Component({
  selector: 'app-modal-summarize-reports',
  imports: [CommonModule, NzButtonModule],
  templateUrl: './modal-summarize-reports.component.html',
  styleUrl: './modal-summarize-reports.component.css',
})
export class ModalSummarizeReportsComponent {
  uploadedFiles: File[] = [];
  isLoadingButton = false;
  onFileSelected(event: Event): void {
    this.isLoadingButton = true;
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      const file = input.files[0];

      // Lưu file vào danh sách
      this.uploadedFiles.push(file);

      // Nếu cần gửi FormData:
      // const formData = new FormData();
      // formData.append('file', file);

      // console.log('Selected file:', file);
    }
    this.isLoadingButton = false;
  }

  removeFile(index: number): void {
    this.uploadedFiles.splice(index, 1);
  }
}
