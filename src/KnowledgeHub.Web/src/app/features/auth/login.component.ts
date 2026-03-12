import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, TranslateModule],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-50 px-4">
      <div class="w-full max-w-md bg-white rounded-xl shadow-lg p-8">
        <h2 class="text-2xl font-bold text-center text-gray-900 mb-2">{{ 'AUTH.LOGIN.TITLE' | translate }}</h2>
        <p class="text-center text-gray-500 mb-8">{{ 'AUTH.LOGIN.SUBTITLE' | translate }}</p>

        @if (error) {
          <div class="mb-4 p-3 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">{{ error }}</div>
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" class="space-y-5">
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">{{ 'AUTH.LOGIN.EMAIL_LABEL' | translate }}</label>
            <input
              formControlName="email"
              type="email"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              [placeholder]="'AUTH.LOGIN.EMAIL_PLACEHOLDER' | translate"
            />
          </div>

          <div>
            <label class="block text-sm font-medium text-gray-700 mb-1">{{ 'AUTH.LOGIN.PASSWORD_LABEL' | translate }}</label>
            <input
              formControlName="password"
              type="password"
              class="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 outline-none"
              [placeholder]="'AUTH.LOGIN.PASSWORD_PLACEHOLDER' | translate"
            />
          </div>

          <button
            type="submit"
            [disabled]="form.invalid || isSubmitting"
            class="w-full py-2.5 bg-indigo-600 text-white rounded-lg font-medium hover:bg-indigo-700 disabled:opacity-50 transition-colors"
          >
            {{ (isSubmitting ? 'AUTH.LOGIN.SUBMITTING' : 'AUTH.LOGIN.SUBMIT') | translate }}
          </button>
        </form>

        <p class="mt-6 text-center text-sm text-gray-500">
          {{ 'AUTH.LOGIN.NO_ACCOUNT' | translate }}
          <a routerLink="/register" class="text-indigo-600 hover:text-indigo-700 font-medium">{{ 'AUTH.LOGIN.SIGN_UP' | translate }}</a>
        </p>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private auth = inject(AuthService);
  private router = inject(Router);
  private translate = inject(TranslateService);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  error = '';
  isSubmitting = false;

  onSubmit() {
    if (this.form.invalid) return;
    this.isSubmitting = true;
    this.error = '';

    this.auth.login(this.form.getRawValue()).subscribe({
      next: (res) => {
        this.auth.handleAuthResponse(res);
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.error = err.error?.message || this.translate.instant('AUTH.LOGIN.ERROR_DEFAULT');
        this.isSubmitting = false;
      },
    });
  }
}
