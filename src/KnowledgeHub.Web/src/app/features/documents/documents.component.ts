import { Component, inject, OnInit } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { DocumentService, Document } from '../../core/services/document.service';
import { NotificationService } from '../../core/services/notification.service';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';
import { DocumentListSkeletonComponent } from '../../shared/components/skeletons.component';
import { EmptyStateComponent } from '../../shared/components/empty-state.component';

@Component({
  selector: 'app-documents',
  imports: [FileSizePipe, TranslateModule, DocumentListSkeletonComponent, EmptyStateComponent],
  template: `
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold text-[var(--color-text)]">{{ 'DOCUMENTS.TITLE' | translate }}</h2>
      </div>

      <!-- Upload zone -->
      <div
        class="mb-8 border-2 border-dashed border-[var(--color-border)] rounded-xl p-6 md:p-8 text-center hover:border-[var(--color-primary)] transition-colors cursor-pointer"
        (click)="fileInput.click()"
        (dragover)="onDragOver($event)"
        (drop)="onDrop($event)"
      >
        <svg class="w-10 h-10 mx-auto mb-3 text-[var(--color-text-secondary)]" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
        </svg>
        <p class="text-[var(--color-text-secondary)] mb-2">{{ 'DOCUMENTS.UPLOAD_HINT' | translate }}</p>
        <p class="text-sm text-[var(--color-text-secondary)] opacity-70">{{ 'DOCUMENTS.UPLOAD_FORMATS' | translate }}</p>
        <input #fileInput type="file" class="hidden" accept=".pdf,.docx,.txt,.md" multiple (change)="onFileSelected($event)" />
      </div>

      @if (isUploading) {
        <div class="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg text-sm text-blue-700">
          {{ 'DOCUMENTS.UPLOADING' | translate }}
        </div>
      }

      <!-- Document list -->
      @if (docService.isLoading()) {
        <app-document-list-skeleton />
      } @else if (docService.documents().length === 0) {
        <app-empty-state
          title="No documents yet"
          subtitle="Upload your first document to get started"
          actionLabel="Upload Document"
          (action)="fileInput.click()"
        />
      } @else {
        <!-- Mobile card view -->
        <div class="block md:hidden space-y-3">
          @for (doc of docService.documents(); track doc.id) {
            <div class="bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] p-4">
              <div class="flex items-start justify-between mb-2">
                <p class="text-sm font-medium text-[var(--color-text)] truncate flex-1 mr-2">{{ doc.fileName }}</p>
                <span [class]="getStatusClass(doc.status)" class="shrink-0 px-2 py-0.5 text-xs font-medium rounded-full">
                  {{ getStatusKey(doc.status) | translate }}
                </span>
              </div>
              <div class="flex items-center justify-between text-xs text-[var(--color-text-secondary)]">
                <span>{{ doc.fileSize | fileSize }}</span>
                <button (click)="deleteDoc(doc)" class="text-red-600 hover:text-red-800">{{ 'DOCUMENTS.DELETE' | translate }}</button>
              </div>
            </div>
          }
        </div>

        <!-- Desktop table view -->
        <div class="hidden md:block bg-[var(--color-card-bg)] rounded-xl border border-[var(--color-card-border)] overflow-hidden">
          <table class="w-full">
            <thead class="bg-[var(--color-bg-secondary)] border-b border-[var(--color-border)]">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">{{ 'DOCUMENTS.TABLE.NAME' | translate }}</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">{{ 'DOCUMENTS.TABLE.SIZE' | translate }}</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-[var(--color-text-secondary)] uppercase">{{ 'DOCUMENTS.TABLE.STATUS' | translate }}</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-[var(--color-text-secondary)] uppercase">{{ 'DOCUMENTS.TABLE.ACTIONS' | translate }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[var(--color-border)]">
              @for (doc of docService.documents(); track doc.id) {
                <tr class="hover:bg-[var(--color-hover)]">
                  <td class="px-6 py-4 text-sm font-medium text-[var(--color-text)]">{{ doc.fileName }}</td>
                  <td class="px-6 py-4 text-sm text-[var(--color-text-secondary)]">{{ doc.fileSize | fileSize }}</td>
                  <td class="px-6 py-4">
                    <span [class]="getStatusClass(doc.status)" class="px-2 py-1 text-xs font-medium rounded-full">
                      {{ getStatusKey(doc.status) | translate }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-right">
                    <button (click)="deleteDoc(doc)" class="text-sm text-red-600 hover:text-red-800">{{ 'DOCUMENTS.DELETE' | translate }}</button>
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
export class DocumentsComponent implements OnInit {
  docService = inject(DocumentService);
  private notificationService = inject(NotificationService);
  isUploading = false;

  ngOnInit() {
    this.docService.loadDocuments();
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.uploadFiles(Array.from(input.files));
      input.value = '';
    }
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.uploadFiles(Array.from(event.dataTransfer.files));
    }
  }

  private uploadFiles(files: File[]) {
    if (files.length === 1) {
      this.isUploading = true;
      this.docService.upload(files[0]).subscribe({
        next: () => {
          this.isUploading = false;
          this.notificationService.success(`"${files[0].name}" uploaded successfully.`);
          this.docService.loadDocuments();
        },
        error: (err) => {
          this.isUploading = false;
          this.notificationService.error(err.error?.message || 'Upload failed.');
        },
      });
      return;
    }

    this.isUploading = true;
    this.docService.uploadMultiple(files).subscribe({
      next: (result) => {
        this.isUploading = false;
        if (result.succeeded.length > 0) {
          this.notificationService.success(
            `${result.succeeded.length} of ${files.length} file(s) uploaded successfully.`,
          );
        }
        if (result.failed.length > 0) {
          const names = result.failed.map((f) => f.fileName).join(', ');
          this.notificationService.error(`Failed to upload: ${names}`);
        }
        this.docService.loadDocuments();
      },
      error: (err) => {
        this.isUploading = false;
        this.notificationService.error(err.error?.message || 'Batch upload failed.');
      },
    });
  }

  deleteDoc(doc: Document) {
    this.docService.delete(doc.id).subscribe({
      next: () => {
        this.notificationService.success(`"${doc.fileName}" deleted.`);
        this.docService.loadDocuments();
      },
      error: () => this.notificationService.error('Failed to delete document.'),
    });
  }

  getStatusKey(status: string): string {
    const keys: Record<string, string> = {
      Uploaded: 'DOCUMENTS.STATUS.UPLOADED',
      Processing: 'DOCUMENTS.STATUS.PROCESSING',
      Ready: 'DOCUMENTS.STATUS.READY',
      Failed: 'DOCUMENTS.STATUS.FAILED',
    };
    return keys[status] || status;
  }

  getStatusClass(status: string): string {
    const classes: Record<string, string> = {
      Uploaded: 'bg-blue-100 text-blue-700',
      Processing: 'bg-yellow-100 text-yellow-700',
      Ready: 'bg-green-100 text-green-700',
      Failed: 'bg-red-100 text-red-700',
    };
    return classes[status] || 'bg-gray-100 text-gray-700';
  }
}
