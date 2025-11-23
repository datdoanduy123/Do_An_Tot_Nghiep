import { Component, inject, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ToastComponent } from '../../../components/toast/toast.component';
import { ToastService } from '../../../service/toast.service';

declare var google: any;

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    ToastComponent,
  ],
  templateUrl: './registerPage.component.html',
  styleUrls: ['./registerPage.component.css'],
})
export class RegisterPageComponent {
  authForm: FormGroup = new FormGroup(
    {
      userName: new FormControl('', [Validators.required]),
      password: new FormControl('', [
        Validators.required,
        Validators.minLength(6),
      ]),
      confirmPassword: new FormControl('', [
        Validators.required,
        Validators.minLength(6),
      ]),
      email: new FormControl('', [Validators.required, Validators.email]),
      name: new FormControl('', [Validators.required]),
    },
    { validators: passwordMatchValidator() }
  );
  constructor(private router: Router, private toastService: ToastService) {}

  ngOnInit(): void {}

  get userName() {
    return this.authForm.get('userName');
  }
  get password() {
    return this.authForm.get('password');
  }
  get confirmPassword() {
    return this.authForm.get('confirmPassword');
  }
  get email() {
    return this.authForm.get('email');
  }
  get name() {
    return this.authForm.get('name');
  }

  onSubmit() {
    this.toastService.Info('Tính năng này đang phát triển');
    // this.authRepo
    //   .login({ username: this.username, password: this.password })
    //   .pipe(
    //     catchError((error) => {
    //       this.toastService.Error('Đăng nhập không thành công!');
    //       return throwError(() => error);
    //     })
    //   )
    //   .subscribe((res) => {
    //     this.router.navigate(['/login'], { replaceUrl: true });
    //     this.authService.saveDataLocalStorage(res);
    //   });
    // }
  }
  navigateToPage() {
    this.router.navigate(['/login'], { replaceUrl: true });
  }
}

export function passwordMatchValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const password = control.get('password')?.value;
    const confirmPassword = control.get('confirmPassword')?.value;

    if (password && confirmPassword && password !== confirmPassword) {
      return { passwordMismatch: true };
    }
    return null;
  };
}
