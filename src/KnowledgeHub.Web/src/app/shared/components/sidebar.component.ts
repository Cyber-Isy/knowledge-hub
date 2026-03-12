import { Component, inject, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, TranslateModule],
  template: `
    <aside
      class="fixed inset-y-0 left-0 z-40 w-64 bg-[var(--color-sidebar-bg)] border-r border-[var(--color-border)] flex flex-col pt-16 transition-transform duration-200 ease-in-out md:static md:z-auto md:pt-0 md:translate-x-0"
      [class.translate-x-0]="isOpen()"
      [class.-translate-x-full]="!isOpen()"
    >
      <nav class="flex-1 p-4 space-y-1">
        @for (item of navItems; track item.path) {
          <a
            [routerLink]="item.path"
            routerLinkActive="bg-indigo-50 text-indigo-700 border-indigo-200 [html[data-theme=dark]_&]:bg-indigo-950 [html[data-theme=dark]_&]:text-indigo-300 [html[data-theme=dark]_&]:border-indigo-800"
            (click)="closeSidebar.emit()"
            class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[var(--color-text)] hover:bg-[var(--color-hover)] transition-colors border border-transparent"
          >
            <span>{{ item.icon }}</span>
            <span>{{ item.labelKey | translate }}</span>
          </a>
        }

        @if (auth.isAdmin()) {
          <div class="mt-4 pt-4 border-t border-[var(--color-border)]">
            <p class="px-3 mb-2 text-xs font-semibold uppercase text-[var(--color-text-secondary)]">Admin</p>
            @for (item of adminNavItems; track item.path) {
              <a
                [routerLink]="item.path"
                routerLinkActive="bg-indigo-50 text-indigo-700 border-indigo-200 [html[data-theme=dark]_&]:bg-indigo-950 [html[data-theme=dark]_&]:text-indigo-300 [html[data-theme=dark]_&]:border-indigo-800"
                (click)="closeSidebar.emit()"
                class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[var(--color-text)] hover:bg-[var(--color-hover)] transition-colors border border-transparent"
              >
                <span>{{ item.icon }}</span>
                <span>{{ item.label }}</span>
              </a>
            }
          </div>
        }
      </nav>
    </aside>
  `,
})
export class SidebarComponent {
  auth = inject(AuthService);
  isOpen = input(false);
  closeSidebar = output<void>();

  navItems = [
    { path: '/dashboard', labelKey: 'SIDEBAR.DASHBOARD', icon: '📊' },
    { path: '/documents', labelKey: 'SIDEBAR.DOCUMENTS', icon: '📄' },
    { path: '/chat', labelKey: 'SIDEBAR.CHAT', icon: '💬' },
  ];

  adminNavItems = [
    { path: '/admin/dashboard', label: 'Dashboard', icon: '⚙️' },
    { path: '/admin/users', label: 'Users', icon: '👥' },
  ];
}
