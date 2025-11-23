import { Status } from '../../constants/constant';
import { UpdateProgressService } from '../../service/update-progress.service';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ToastService } from '../../service/toast.service';
import { FormsModule, NgForm } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { NzButtonModule } from 'ng-zorro-antd/button';

@Component({
  selector: 'app-modal-update-progress',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzButtonModule],
  templateUrl: './modal-update-progress.component.html',
  styleUrl: './modal-update-progress.component.css',
})
export class ModalUpdateProgressComponent {
  @Output() closePopup = new EventEmitter<boolean>();
  selectedFileName: string = '';
  @Input() taskId!: number;
  isLoading = false;
  statusOptions = [
    { label: 'Đang thực hiện', value: Status.DangThucHien },
    { label: 'Hoàn thành', value: Status.HoanThanh },
    { label: 'Tạm dừng', value: Status.TamDung },
  ];
  form = {
    kienNghi: '',
    ketQua: '',
    feedback: '',
    file: '',
    status: Status.DangThucHien,
  };

  constructor(
    private route: ActivatedRoute,
    private updateProgressService: UpdateProgressService,
    private toastService: ToastService
  ) {}

  // ngOnInit() {
  //   this.route.params.subscribe((params) => {
  //     this.taskId = +params['id'];
  //   });
  // }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) {
      this.form.file = file;
      this.selectedFileName = file.name;
    }
  }
  onSubmit(form: NgForm) {
    if (
      form.invalid ||
      !this.form.kienNghi ||
      !this.form.ketQua ||
      !this.form.feedback ||
      !this.form.file
    ) {
      this.toastService.Warning('Vui lòng nhập đầy đủ các trường !');
      return;
    }
    const formData = new FormData();
    formData.append('Proposal', this.form.kienNghi);
    formData.append('Result', this.form.ketQua);
    formData.append('Feedback', this.form.feedback);
    formData.append('ReportFile', this.form.file);
    formData.append('Status', this.form.status);
    this.isLoading = true;

    this.updateProgressService
      .updateProgress(this.taskId.toString(), formData)
      .subscribe({
        next: () => {
          this.toastService.Success('Cập nhật tiến độ thành công !');
          this.updateIsShowPopup();
          this.isLoading = false;
        },
        error: (err) => {
          this.toastService.Error(
            err.message ?? 'Cập nhật tiến độ không thành công!'
          ),
            (this.isLoading = false);
        },
      });
  }

  updateIsShowPopup() {
    this.closePopup.emit(false);
  }
}
