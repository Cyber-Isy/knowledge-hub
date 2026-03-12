import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HeaderComponent } from './header.component';
import { SidebarComponent } from './sidebar.component';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, HeaderComponent, SidebarComponent],
  template: `
    <div class="h-screen flex flex-col bg-[var(--color-bg)]">
      <app-header (toggleSidebar)="toggleSidebar()" />
      <div class="flex flex-1 overflow-hidden relative">
        <!-- Mobile overlay -->
        @if (sidebarOpen()) {
          <div
            class="fixed inset-0 z-30 bg-black/50 md:hidden"
            (click)="sidebarOpen.set(false)"
          ></div>
        }

        <app-sidebar [isOpen]="sidebarOpen()" (closeSidebar)="sidebarOpen.set(false)" />

        <main class="flex-1 overflow-y-auto p-4 md:p-6 bg-[var(--color-bg-secondary)]">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
})
export class LayoutComponent {
  sidebarOpen = signal(false);

  toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }
}
