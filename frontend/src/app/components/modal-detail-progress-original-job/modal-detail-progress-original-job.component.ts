import { ToastService } from './../../service/toast.service';
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ViecquanlyModel } from '../../models/viecquanly.model';
import { DetailProgressTaskParentModel } from '../../models/detail-progress.model';
import { DetailProgressService } from '../../service/detail-progress.service';
import { convertToVietnameseDate } from '../../helper/convertToVNDate';
import { ConvertStatusTask } from '../../constants/constant';
import { NzTableModule } from 'ng-zorro-antd/table';
// import { LoadingComponent } from '../loading/loading.component';

@Component({
  selector: 'app-modal-detail-progress-original-job',
  imports: [FormsModule, CommonModule, NzTableModule],
  templateUrl: './modal-detail-progress-original-job.component.html',
  styleUrl: './modal-detail-progress-original-job.component.css',
})
export class ModalDetailProgressOriginalJobComponent {
  @Input() viecGoc!: ViecquanlyModel;
  @Output() isShowModal = new EventEmitter<any>();
  detailProgressTaskParentModel?: DetailProgressTaskParentModel;
  isloading = true;
  totalItems: Number | null = null;
  pageSize: number | null = null;

  constructor(
    private detailProgressService: DetailProgressService,
    private toastService: ToastService
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  loadData() {
    this.isloading = true;
    this.detailProgressService
      .getFullProgressDetail(this.viecGoc.taskId.toString())
      .subscribe({
        next: (data) => {
          this.detailProgressTaskParentModel = data;
          this.totalItems = data.children.length;
          this.isloading = false;
        },
        error: (err) => {
          this.toastService.Warning(err.message || 'Lấy dữ liệu thất bại !');
          this.isloading = false;
        },
      });
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