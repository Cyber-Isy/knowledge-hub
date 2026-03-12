import { Component, inject, OnInit } from '@angular/core';
import { DocumentService, Document } from '../../core/services/document.service';
import { FileSizePipe } from '../../shared/pipes/file-size.pipe';

@Component({
  selector: 'app-documents',
  imports: [FileSizePipe],
  template: `
    <div>
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold text-gray-900">Documents</h2>
      </div>

      <!-- Upload zone -->
      <div
        class="mb-8 border-2 border-dashed border-gray-300 rounded-xl p-8 text-center hover:border-indigo-400 transition-colors cursor-pointer"
        (click)="fileInput.click()"
        (dragover)="onDragOver($event)"
        (drop)="onDrop($event)"
      >
        <p class="text-gray-500 mb-2">Drag & drop files here or click to browse</p>
        <p class="text-sm text-gray-400">PDF, DOCX, TXT, MD — Max 10 MB</p>
        <input #fileInput type="file" class="hidden" accept=".pdf,.docx,.txt,.md" (change)="onFileSelected($event)" />
      </div>

      @if (isUploading) {
        <div class="mb-4 p-3 bg-indigo-50 border border-indigo-200 rounded-lg text-sm text-indigo-700">
          Uploading...
        </div>
      }

      <!-- Document list -->
      @if (docService.isLoading()) {
        <p class="text-gray-500">Loading documents...</p>
      } @else if (docService.documents().length === 0) {
        <div class="text-center py-12 text-gray-400">
          <p class="text-lg mb-1">No documents yet</p>
          <p class="text-sm">Upload your first document to get started</p>
        </div>
      } @else {
        <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
          <table class="w-full">
            <thead class="bg-gray-50 border-b border-gray-200">
              <tr>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Name</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Size</th>
                <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase">Status</th>
                <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-100">
              @for (doc of docService.documents(); track doc.id) {
                <tr class="hover:bg-gray-50">
                  <td class="px-6 py-4 text-sm font-medium text-gray-900">{{ doc.fileName }}</td>
                  <td class="px-6 py-4 text-sm text-gray-500">{{ doc.fileSize | fileSize }}</td>
                  <td class="px-6 py-4">
                    <span [class]="getStatusClass(doc.status)" class="px-2 py-1 text-xs font-medium rounded-full">
                      {{ doc.status }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-right">
                    <button (click)="deleteDoc(doc)" class="text-sm text-red-600 hover:text-red-800">Delete</button>
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
  isUploading = false;

  ngOnInit() {
    this.docService.loadDocuments();
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files?.[0]) {
      this.uploadFile(input.files[0]);
    }
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    if (event.dataTransfer?.files[0]) {
      this.uploadFile(event.dataTransfer.files[0]);
    }
  }

  private uploadFile(file: File) {
    this.isUploading = true;
    this.docService.upload(file).subscribe({
      next: () => {
        this.isUploading = false;
        this.docService.loadDocuments();
      },
      error: () => (this.isUploading = false),
    });
  }

  deleteDoc(doc: Document) {
    this.docService.delete(doc.id).subscribe(() => this.docService.loadDocuments());
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
