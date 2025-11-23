import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from "@angular/forms";
import { AuthService } from "../../../service/auth.service";
import { ToastService } from "../../../service/toast.service";
import { CommonModule } from "@angular/common";
import { Router, RouterModule } from "@angular/router";
import { ToastComponent } from "../../../components/toast/toast.component";
import { NzInputModule } from "ng-zorro-antd/input";
import { ScreenLoadingComponent } from "../../../components/loading/screenLoading/screenLoading.component";
import { NzIconModule } from "ng-zorro-antd/icon";
import { Component } from "@angular/core";
import { SignalrService } from "../../../service/signalr/signalr.service";

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ToastComponent,
    ScreenLoadingComponent,
    NzInputModule,
    NzIconModule
  ],
  templateUrl: './forgotPassword.component.html',
  styleUrls: ['./forgotPassword.component.css'],
})
export class ForgotPasswordComponent {
  isLoading = false;

  forgotForm: FormGroup = new FormGroup({
    userName: new FormControl('', [Validators.required]),
  });

  constructor(
    private router: Router,
    private toastService: ToastService,
    private authService: AuthService,
    private _signalRService: SignalrService
  ) {}

  get userName() {
    return this.forgotForm.get('userName');
  }

  onSubmit() {
    if (!this.forgotForm.valid) {
      this.toastService.Error('Vui lòng nhập tên đăng nhập');
      return;
    }

    this.isLoading = true;
    const username = this.userName?.value;

    this.authService.forgotPassword(username).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          this.toastService.Success(res.message || 'Đã gửi email đặt lại mật khẩu.');
        } else {
          this.toastService.Error(res.message || 'Có lỗi xảy ra.');
        }
      },
      error: () => {
        this.isLoading = false;
        this.toastService.Error('Không thể gửi yêu cầu, vui lòng thử lại sau.');
      },
    });
  }
}
