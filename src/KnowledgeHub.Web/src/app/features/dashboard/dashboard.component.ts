import { Component, inject, OnInit, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { DocumentService, DocumentStats } from '../../core/services/document.service';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';
import { StatSkeletonComponent } from '../../shared/components/skeletons.component';

@Component({
  selector: 'app-dashboard',
  imports: [TranslateModule, FileSizePipe, StatSkeletonComponent],
  template: `
    <div>
      <h2 class="text-2xl font-bold text-[var(--color-text)] mb-6">{{ 'DASHBOARD.TITLE' | translate }}</h2>

      <!-- Stat cards -->
      @if (isLoading()) {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 md:gap-6 mb-8">
          @for (i of [1, 2, 3, 4]; track i) {
            <app-stat-skeleton />
          }
        </div>
      } @else {
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 md:gap-6 mb-8">
          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <p class="text-sm text-[var(--color-text-secondary)] mb-1">{{ 'DASHBOARD.WELCOME' | translate }}</p>
            <p class="text-lg font-semibold text-[var(--color-text)] truncate">
              {{ auth.currentUser()?.displayName || auth.currentUser()?.email }}
            </p>
          </div>
          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <p class="text-sm text-[var(--color-text-secondary)] mb-1">{{ 'DASHBOARD.DOCUMENTS' | translate }}</p>
            <p class="text-2xl font-bold text-[var(--color-primary)]">{{ stats()?.totalDocuments ?? 0 }}</p>
          </div>
          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <p class="text-sm text-[var(--color-text-secondary)] mb-1">Storage Used</p>
            <p class="text-2xl font-bold text-[var(--color-primary)]">{{ (stats()?.totalStorageBytes ?? 0) | fileSize }}</p>
          </div>
          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <p class="text-sm text-[var(--color-text-secondary)] mb-1">{{ 'DASHBOARD.CONVERSATIONS' | translate }}</p>
            <p class="text-2xl font-bold text-[var(--color-primary)]">—</p>
          </div>
        </div>
      }

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4 md:gap-6">
        <!-- Documents by status -->
        @if (stats(); as s) {
          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <h3 class="text-sm font-semibold text-[var(--color-text)] mb-4">Documents by Status</h3>
            @if (getStatusEntries(s).length === 0) {
              <p class="text-sm text-[var(--color-text-secondary)]">No documents yet</p>
            } @else {
              <div class="space-y-3">
                @for (entry of getStatusEntries(s); track entry.status) {
                  <div>
                    <div class="flex items-center justify-between mb-1">
                      <span class="text-sm text-[var(--color-text)]">{{ entry.status }}</span>
                      <span class="text-sm font-medium text-[var(--color-text-secondary)]">{{ entry.count }}</span>
                    </div>
                    <div class="w-full bg-[var(--color-bg-tertiary)] rounded-full h-2">
                      <div
                        class="h-2 rounded-full transition-all"
                        [style.width.%]="(entry.count / s.totalDocuments) * 100"
                        [class]="getStatusBarColor(entry.status)"
                      ></div>
                    </div>
                  </div>
                }
              </div>
            }
          </div>

          <!-- Recent uploads -->
          <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-6">
            <h3 class="text-sm font-semibold text-[var(--color-text)] mb-4">Recent Uploads</h3>
            @if (s.recentUploads.length === 0) {
              <p class="text-sm text-[var(--color-text-secondary)]">No recent uploads</p>
            } @else {
              <div class="space-y-3">
                @for (doc of s.recentUploads; track doc.id) {
                  <div class="flex items-center justify-between py-2 border-b border-[var(--color-border)] last:border-0">
                    <div class="min-w-0 flex-1">
                      <p class="text-sm font-medium text-[var(--color-text)] truncate">{{ doc.fileName }}</p>
                      <p class="text-xs text-[var(--color-text-secondary)]">{{ doc.fileSize | fileSize }}</p>
                    </div>
                    <span
                      class="shrink-0 ml-3 px-2 py-0.5 text-xs font-medium rounded-full"
                      [class]="getStatusBadgeClass(doc.status)"
                    >
                      {{ doc.status }}
                    </span>
                  </div>
                }
              </div>
            }
          </div>
        }
      </div>
    </div>
  `,
})
export class DashboardComponent implements OnInit {
  auth = inject(AuthService);
  private docService = inject(DocumentService);

  stats = signal<DocumentStats | null>(null);
  isLoading = signal(true);

  ngOnInit(): void {
    this.docService.getStats().subscribe({
      next: (stats) => {
        this.stats.set(stats);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  getStatusEntries(stats: DocumentStats): { status: string; count: number }[] {
    return Object.entries(stats.documentsByStatus).map(([status, count]) => ({ status, count }));
  }

  getStatusBarColor(status: string): string {
    const colors: Record<string, string> = {
      Uploaded: 'bg-blue-500',
      Processing: 'bg-yellow-500',
      Chunking: 'bg-orange-500',
      Embedding: 'bg-purple-500',
      Indexing: 'bg-cyan-500',
      Ready: 'bg-green-500',
      Failed: 'bg-red-500',
    };
    return colors[status] || 'bg-gray-500';
  }

  getStatusBadgeClass(status: string): string {
    const classes: Record<string, string> = {
      Uploaded: 'bg-blue-100 text-blue-700',
      Processing: 'bg-yellow-100 text-yellow-700',
      Ready: 'bg-green-100 text-green-700',
      Failed: 'bg-red-100 text-red-700',
    };
    return classes[status] || 'bg-gray-100 text-gray-700';
  }
}
