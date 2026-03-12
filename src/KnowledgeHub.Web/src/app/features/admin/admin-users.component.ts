import { Component, inject, OnInit } from '@angular/core';
import { AdminService, AdminUser } from '../../core/services/admin.service';
import { DatePipe } from '@angular/common';

@Component({
  selector: 'app-admin-users',
  imports: [DatePipe],
  template: `
    <div>
      <h2 class="text-2xl font-bold text-[var(--color-text)] mb-6">User Management</h2>

      @if (adminService.isLoadingUsers()) {
        <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-8 text-center">
          <p class="text-[var(--color-text-secondary)]">Loading users...</p>
        </div>
      } @else if (adminService.users().length === 0) {
        <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-8 text-center">
          <p class="text-[var(--color-text-secondary)]">No users found.</p>
        </div>
      } @else {
        <!-- Mobile card view -->
        <div class="block md:hidden space-y-3">
          @for (user of adminService.users(); track user.id) {
            <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-4">
              <div class="flex items-center justify-between mb-2">
                <span class="font-medium text-sm text-[var(--color-text)]">
                  {{ user.displayName || user.email }}
                </span>
                <span
                  class="px-2 py-0.5 text-xs font-medium rounded-full"
                  [class]="user.isEnabled ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'"
                >
                  {{ user.isEnabled ? 'Active' : 'Disabled' }}
                </span>
              </div>
              <p class="text-xs text-[var(--color-text-secondary)] mb-2">{{ user.email }}</p>
              <div class="flex items-center justify-between text-xs text-[var(--color-text-secondary)]">
                <span>{{ user.documentCount }} docs / {{ user.conversationCount }} chats</span>
                <button
                  (click)="adminService.toggleUser(user.id)"
                  class="px-3 py-1 text-xs font-medium rounded-lg transition-colors"
                  [class]="user.isEnabled
                    ? 'text-red-600 hover:bg-red-50 border border-red-200'
                    : 'text-green-600 hover:bg-green-50 border border-green-200'"
                >
                  {{ user.isEnabled ? 'Disable' : 'Enable' }}
                </button>
              </div>
            </div>
          }
        </div>

        <!-- Desktop table view -->
        <div class="hidden md:block bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] overflow-hidden">
          <table class="w-full">
            <thead class="bg-[var(--color-bg-secondary)] border-b border-[var(--color-border)]">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">Name</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">Email</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">Documents</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">Conversations</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">Joined</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">Status</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-[var(--color-text-secondary)] uppercase">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[var(--color-border)]">
              @for (user of adminService.users(); track user.id) {
                <tr class="hover:bg-[var(--color-hover)]">
                  <td class="px-6 py-4 text-sm font-medium text-[var(--color-text)]">
                    {{ user.displayName || '—' }}
                    @if (user.roles.includes('Admin')) {
                      <span class="ml-1.5 px-1.5 py-0.5 text-xs bg-indigo-100 text-indigo-700 rounded">Admin</span>
                    }
                  </td>
                  <td class="px-6 py-4 text-sm text-[var(--color-text-secondary)]">{{ user.email }}</td>
                  <td class="px-6 py-4 text-sm text-[var(--color-text-secondary)]">{{ user.documentCount }}</td>
                  <td class="px-6 py-4 text-sm text-[var(--color-text-secondary)]">{{ user.conversationCount }}</td>
                  <td class="px-6 py-4 text-sm text-[var(--color-text-secondary)]">{{ user.createdAt | date:'mediumDate' }}</td>
                  <td class="px-6 py-4">
                    <span
                      class="px-2 py-1 text-xs font-medium rounded-full"
                      [class]="user.isEnabled ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'"
                    >
                      {{ user.isEnabled ? 'Active' : 'Disabled' }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-right">
                    <button
                      (click)="adminService.toggleUser(user.id)"
                      class="text-sm font-medium transition-colors"
                      [class]="user.isEnabled ? 'text-red-600 hover:text-red-800' : 'text-green-600 hover:text-green-800'"
                    >
                      {{ user.isEnabled ? 'Disable' : 'Enable' }}
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class AdminUsersComponent implements OnInit {
  adminService = inject(AdminService);

  ngOnInit(): void {
    this.adminService.loadUsers();
  }
}
