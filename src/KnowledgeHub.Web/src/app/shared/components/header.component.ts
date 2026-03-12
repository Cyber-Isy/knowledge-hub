import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-header',
  template: `
    <header class="h-16 bg-white border-b border-gray-200 flex items-center justify-between px-6">
      <div class="flex items-center gap-3">
        <h1 class="text-xl font-bold text-indigo-600">KnowledgeHub</h1>
      </div>
      <div class="flex items-center gap-4">
        @if (auth.currentUser()) {
          <span class="text-sm text-gray-600">{{ auth.currentUser()?.displayName || auth.currentUser()?.email }}</span>
          <button (click)="auth.logout()" class="text-sm text-gray-500 hover:text-red-600 transition-colors">
            Logout
          </button>
        }
      </div>
    </header>
  `,
})
export class HeaderComponent {
  auth = inject(AuthService);
}
