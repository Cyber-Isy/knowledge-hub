import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, TranslateModule],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div class="w-full max-w-md bg-white rounded-xl shadow-lg p-8">
        <h2 class="text-2xl font-bold text-center text-gray-900 mb-2">{{ 'AUTH.REGISTER.TITLE' | translate }}</h2>
        <p class="text-center text-gray-500 mb-8">{{ 'AUTH.REGISTER.SUBTITLE' | translate }}</p>

        @if (error) {
          <div class="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">{{ error }}</div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-5">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">{{ 'AUTH.REGISTER.DISPLAY_NAME_LABEL' | translate }}</label>
            <input
              formControlName="displayName"
              type="text"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              [placeholder]="'AUTH.REGISTER.DISPLAY_NAME_PLACEHOLDER' | translate"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">{{ 'AUTH.REGISTER.EMAIL_LABEL' | translate }}</label>
            <input
              formControlName="email"
              type="email"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              [placeholder]="'AUTH.REGISTER.EMAIL_PLACEHOLDER' | translate"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">{{ 'AUTH.REGISTER.PASSWORD_LABEL' | translate }}</label>
            <input
              formControlName="password"
              type="password"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              [placeholder]="'AUTH.REGISTER.PASSWORD_PLACEHOLDER' | translate"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">{{ 'AUTH.REGISTER.CONFIRM_PASSWORD_LABEL' | translate }}</label>
            <input
              formControlName="confirmPassword"
              type="password"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              [placeholder]="'AUTH.REGISTER.CONFIRM_PASSWORD_PLACEHOLDER' | translate"
            />
          </div>

          <button
            type="submit"
            [disabled]="form.invalid || isSubmitting"
            class="w-full py-2.5 bg-indigo-600 text-white rounded-lg font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            {{ (isSubmitting ? 'AUTH.REGISTER.SUBMITTING' : 'AUTH.REGISTER.SUBMIT') | translate }}
          </button>
        </form>

        <p class="mt-6 text-center text-sm text-gray-500">
          {{ 'AUTH.REGISTER.HAS_ACCOUNT' | translate }}
          <a routerLink="/login" class="text-indigo-600 hover:text-indigo-700 font-medium">{{ 'AUTH.REGISTER.SIGN_IN' | translate }}</a>
        </p>
      </div>
    </div>
  `,
})
export class RegisterComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private translate = inject(TranslateService);

  form = this.fb.nonNullable.group({
    displayName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['', [Validators.required]],
  });

  error = '';
  isSubmitting = false;

  onSubmit() {
    if (this.form.invalid) return;

    const { password, confirmPassword } = this.form.getRawValue();
    if (password !== confirmPassword) {
      this.error = this.translate.instant('AUTH.REGISTER.PASSWORD_MISMATCH');
      return;
    }

    this.isSubmitting = true;
    this.error = '';

    this.auth.register(this.form.getRawValue()).subscribe({
      next: (res) => {
        this.auth.handleAuthResponse(res);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.error = err.error?.errors?.join(', ') || err.error?.message || this.translate.instant('AUTH.REGISTER.ERROR_DEFAULT');
        this.isSubmitting = false;
      },
    });
  }
}
