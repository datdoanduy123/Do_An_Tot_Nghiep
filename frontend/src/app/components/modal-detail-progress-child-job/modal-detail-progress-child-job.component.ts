import { ToastService } from './../../service/toast.service';
import { DetailProgressTaskChildModel } from './../../models/detail-progress.model';
import { DetailProgressService } from './../../service/detail-progress.service';
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { LoadingComponent } from '../loading/loading.component';
import { convertToVietnameseDate } from '../../helper/convertToVNDate';
import { ConvertStatusTask } from '../../constants/constant';

@Component({
  selector: 'app-modal-detail-progress-child-job',
  imports: [CommonModule, LoadingComponent],
  templateUrl: './modal-detail-progress-child-job.component.html',
  styleUrl: './modal-detail-progress-child-job.component.css',
})
export class ModalDetailProgressChildJobComponent {
  @Input() taskId!: string;
  @Output() isShowModal = new EventEmitter<any>();
  detailProgressTaskChildModel?: DetailProgressTaskChildModel;
  isloading = true;
  constructor(
    private detailProgressService: DetailProgressService,
    private toastService: ToastService
  ) {}
  ngOnInit() {
    this.detailProgressService.detailProgressTaskChild(this.taskId).subscribe({
      next: (data) => {
        this.detailProgressTaskChildModel = data;
        this.isloading = false;
      },
      error: (err) => {
        this.toastService.Warning(err.message ?? 'Lấy dữ liệu thất bại !');
        this.isloading = false;
      },
    });
    console.log(this.detailProgressTaskChildModel?.completionRate);
  }
  convertDateTime(datetime: string): string {
    return convertToVietnameseDate(datetime);
  }
  convertStatusText(status: string | null): string {
    return (
      ConvertStatusTask[status as keyof typeof ConvertStatusTask] ||
      'Không xác định'
    );
  }
  onClose() {
    this.isShowModal.emit(false);
  }
}
