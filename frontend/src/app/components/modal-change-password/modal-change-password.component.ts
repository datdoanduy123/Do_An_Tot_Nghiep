import { Component, Input } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ToastService } from '../../service/toast.service';
import { ChangePasswordRequest } from '../../models/user.model';
import { UserService } from '../../service/user.service';
import { CommonModule } from '@angular/common';
import { ToastComponent } from '../toast/toast.component';
import { ScreenLoadingComponent } from '../loading/screenLoading/screenLoading.component';
import { NzModalRef } from 'ng-zorro-antd/modal';
import { Router } from '@angular/router';

@Component({
  selector: 'app-modal-change-password',
  standalone: true, 
  imports: [CommonModule,ReactiveFormsModule,ToastComponent,ScreenLoadingComponent],
  templateUrl: './modal-change-password.component.html',
  styleUrls: ['./modal-change-password.component.css']
})
export class ModalChangePasswordComponent {
  form!: FormGroup;
  isSubmitting = false;
  constructor(
    private userService: UserService,
    private toastService: ToastService,
    private fb: FormBuilder,
     private modalRef: NzModalRef,  // inject NzModalRef để đóng modal
     private router : Router
  ) {
    this.form = this.fb.group({
      oldPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required],
    });
  }

  close() {
    this.modalRef.destroy();  // đóng modal
  }
  
  submit() {
    if (this.form.invalid) {
      this.toastService.Error('Vui lòng điền đầy đủ thông tin');
      return;
    }
    if (this.form.value.newPassword !== this.form.value.confirmPassword) {
      this.toastService.Error('Mật khẩu mới và xác nhận không khớp');
      return;
    }

    const req: ChangePasswordRequest = {
      oldPassword: this.form.value.oldPassword,
      newPassword: this.form.value.newPassword,
    };

    this.isSubmitting = true;
    this.userService.changePassword(req).subscribe({
      next: (res) => {
        this.isSubmitting = false;
        if (res.success) {
          this.toastService.Success(res.message || 'Đổi mật khẩu thành công');
          this.close();
          localStorage.removeItem('accessToken')
          this.router.navigate(['/login']);
        } else {
          this.toastService.Error(res.error || 'Đổi mật khẩu thất bại');
        }
      },
      error: (err) => {
        this.isSubmitting = false;
        console.error(err);
        this.toastService.Error('Không thể kết nối tới server');
      },
    });
  }
}
