import { Component, inject, OnInit } from '@angular/core';
import { AdminService } from '../../core/services/admin.service';
import { StatSkeletonComponent } from '../../shared/components/skeletons.component';

@Component({
  selector: 'app-admin-dashboard',
  imports: [StatSkeletonComponent],
  template: `
    <div>
      <h2 class="text-2xl font-bold text-[var(--color-text)] mb-6">System Overview</h2>

      @if (adminService.isLoadingStats()) {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4 md:gap-6">
          @for (i of [1, 2, 3, 4, 5]; track i) {
            <app-stat-skeleton />
          }
        </div>
      } @else if (adminService.stats(); as stats) {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4 md:gap-6">
          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <div class="flex items-center gap-3 mb-2">
              <div class="p-2 rounded-lg bg-blue-100 text-blue-600">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M15 19.128a9.38 9.38 0 002.625.372 9.337 9.337 0 004.121-.952 4.125 4.125 0 00-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 018.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0111.964-3.07M12 6.375a3.375 3.375 0 11-6.75 0 3.375 3.375 0 016.75 0zm8.25 2.25a2.625 2.625 0 11-5.25 0 2.625 2.625 0 015.25 0z" />
                </svg>
              </div>
              <p class="text-sm text-[var(--color-text-secondary)]">Total Users</p>
            </div>
            <p class="text-3xl font-bold text-[var(--color-text)]">{{ stats.totalUsers }}</p>
          </div>

          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <div class="flex items-center gap-3 mb-2">
              <div class="p-2 rounded-lg bg-green-100 text-green-600">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
                </svg>
              </div>
              <p class="text-sm text-[var(--color-text-secondary)]">Total Documents</p>
            </div>
            <p class="text-3xl font-bold text-[var(--color-text)]">{{ stats.totalDocuments }}</p>
          </div>

          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <div class="flex items-center gap-3 mb-2">
              <div class="p-2 rounded-lg bg-purple-100 text-purple-600">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 8.511c.884.284 1.5 1.128 1.5 2.097v4.286c0 1.136-.847 2.1-1.98 2.193-.34.027-.68.052-1.02.072v3.091l-3-3c-1.354 0-2.694-.055-4.02-.163a2.115 2.115 0 01-.825-.242m9.345-8.334a2.126 2.126 0 00-.476-.095 48.64 48.64 0 00-8.048 0c-1.131.094-1.976 1.057-1.976 2.192v4.286c0 .837.46 1.58 1.155 1.951m9.345-8.334V6.637c0-1.621-1.152-3.026-2.76-3.235A48.455 48.455 0 0011.25 3c-2.115 0-4.198.137-6.24.402-1.608.209-2.76 1.614-2.76 3.235v6.226c0 1.621 1.152 3.026 2.76 3.235.577.075 1.157.14 1.74.194V21l4.155-4.155" />
                </svg>
              </div>
              <p class="text-sm text-[var(--color-text-secondary)]">Conversations</p>
            </div>
            <p class="text-3xl font-bold text-[var(--color-text)]">{{ stats.totalConversations }}</p>
          </div>

          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <div class="flex items-center gap-3 mb-2">
              <div class="p-2 rounded-lg bg-amber-100 text-amber-600">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.087.16 2.185.283 3.293.369V21l4.076-4.076a1.526 1.526 0 011.037-.443 48.282 48.282 0 005.68-.494c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z" />
                </svg>
              </div>
              <p class="text-sm text-[var(--color-text-secondary)]">Messages</p>
            </div>
            <p class="text-3xl font-bold text-[var(--color-text)]">{{ stats.totalMessages }}</p>
          </div>

          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <div class="flex items-center gap-3 mb-2">
              <div class="p-2 rounded-lg bg-rose-100 text-rose-600">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                  <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 13.5l10.5-11.25L12 10.5h8.25L9.75 21.75 12 13.5H3.75z" />
                </svg>
              </div>
              <p class="text-sm text-[var(--color-text-secondary)]">Tokens Used</p>
            </div>
            <p class="text-3xl font-bold text-[var(--color-text)]">{{ formatNumber(stats.totalTokensUsed) }}</p>
          </div>
        </div>
      }
    </div>
  `,
})
export class AdminDashboardComponent implements OnInit {
  adminService = inject(AdminService);

  ngOnInit(): void {
    this.adminService.loadStats();
  }

  formatNumber(value: number): string {
    if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
    if (value >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
    return value.toString();
  }
}
