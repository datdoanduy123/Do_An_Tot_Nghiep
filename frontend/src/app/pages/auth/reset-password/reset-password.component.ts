import { Component } from "@angular/core";
import { FormControl, FormGroup, FormsModule, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router, RouterModule } from "@angular/router";
import { CommonModule } from "@angular/common";
import { NzInputModule } from "ng-zorro-antd/input";
import { NzIconModule } from "ng-zorro-antd/icon";
import { ToastService } from "../../../service/toast.service";
import { AuthService } from "../../../service/auth.service";
import { ScreenLoadingComponent } from "../../../components/loading/screenLoading/screenLoading.component";
import { ToastComponent } from "../../../components/toast/toast.component";
import { SignalrService } from "../../../service/signalr/signalr.service";

@Component({
  selector: 'app-reset-password',
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
  templateUrl: './reset-password.component.html',
  styleUrls: ['./reset-password.component.css'],
})
export class ResetPasswordComponent {
  isLoading = false;
  passwordVisible = false;
  token = '';

  resetForm = new FormGroup({
    newPassword: new FormControl('', [
      Validators.required,
      Validators.minLength(6),
    ])
    // confirmPassword: new FormControl('', [
    //   Validators.required,
    //   Validators.minLength(6),
    // ])
  });

  constructor(
    private router: Router,
    private toastService: ToastService,
    private authService: AuthService,
    private route: ActivatedRoute,
    private _signalRService: SignalrService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
    if (params['token']) {
      this.token = decodeURIComponent(params['token']); 
      console.log('Token after decode:', this.token);
    }
  });
  }

  get newPassword() {
    return this.resetForm.get('newPassword');
  }

  get confirmPassword() {
    return this.resetForm.get('confirmPassword');
  }

  onSubmit(): void {
    if (this.resetForm.invalid) {
      this.toastService.Error('Vui lòng nhập đủ và đúng thông tin.');
      return;
    }

    
    this.isLoading = true;
    const password = this.newPassword?.value!;

    this.authService.resetPassword(this.token, password).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res.success) {
          this.toastService.Success(res.message || 'Đặt lại mật khẩu thành công.');
          this.router.navigate(['/login']);
        } else {
          this.toastService.Error(res.message || 'Có lỗi xảy ra.');
        }
      },
      error: () => {
        this.isLoading = false;
        this.toastService.Error('Không thể gửi yêu cầu, vui lòng thử lại sau.');
      }
    });
  }
}
