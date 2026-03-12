import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-dashboard',
  template: `
    <div>
      <h2 class="text-2xl font-bold text-gray-900 mb-6">Dashboard</h2>
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div class="bg-white rounded-xl border border-gray-200 p-6">
          <p class="text-sm text-gray-500 mb-1">Welcome</p>
          <p class="text-lg font-semibold text-gray-900">
            {{ auth.currentUser()?.displayName || auth.currentUser()?.email }}
          </p>
        </div>
        <div class="bg-white rounded-xl border border-gray-200 p-6">
          <p class="text-sm text-gray-500 mb-1">Documents</p>
          <p class="text-2xl font-bold text-indigo-600">—</p>
        </div>
        <div class="bg-white rounded-xl border border-gray-200 p-6">
          <p class="text-sm text-gray-500 mb-1">Conversations</p>
          <p class="text-2xl font-bold text-indigo-600">—</p>
        </div>
      </div>
    </div>
  `,
})
export class DashboardComponent {
  auth = inject(AuthService);
}
