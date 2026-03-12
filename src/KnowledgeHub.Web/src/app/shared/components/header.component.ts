import { Component, inject, output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { LanguageSwitcherComponent } from './language-switcher.component';
import { ThemeToggleComponent } from './theme-toggle.component';

@Component({
  selector: 'app-header',
  imports: [TranslateModule, LanguageSwitcherComponent, ThemeToggleComponent],
  template: `
    <header class="h-16 bg-[var(--color-header-bg)] border-b border-[var(--color-border)] flex items-center justify-between px-4 md:px-6">
      <div class="flex items-center gap-3">
        <!-- Hamburger menu (mobile only) -->
        <button
          (click)="toggleSidebar.emit()"
          class="md:hidden p-2 rounded-lg text-[var(--color-text-secondary)] hover:bg-[var(--color-hover)] transition-colors"
          aria-label="Toggle sidebar"
        >
          <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
          </svg>
        </button>
        <h1 class="text-xl font-bold text-[var(--color-primary)]">{{ 'APP.TITLE' | translate }}</h1>
      </div>
      <div class="flex items-center gap-2 md:gap-4">
        <app-theme-toggle />
        <app-language-switcher />
        @if (auth.currentUser()) {
          <span class="hidden sm:inline text-sm text-[var(--color-text-secondary)]">
            {{ auth.currentUser()?.displayName || auth.currentUser()?.email }}
          </span>
          <button
            (click)="auth.logout()"
            class="text-sm text-[var(--color-text-secondary)] hover:text-[var(--color-error)] transition-colors"
          >
            {{ 'HEADER.LOGOUT' | translate }}
          </button>
        }
      </div>
    </header>
  `,
})
export class HeaderComponent {
  auth = inject(AuthService);
  toggleSidebar = output<void>();
}
