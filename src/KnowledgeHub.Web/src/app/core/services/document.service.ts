import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface Document {
  id: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  status: string;
  errorMessage?: string;
  createdAt: string;
  updatedAt: string;
}

export interface DocumentStats {
  totalDocuments: number;
  totalStorageBytes: number;
  documentsByStatus: Record<string, number>;
  recentUploads: Document[];
}

@Injectable({ providedIn: 'root' })
export class DocumentService {
  private readonly baseUrl = `${environment.apiUrl}/v1/documents`;

  documents = signal<Document[]>([]);
  isLoading = signal(false);

  constructor(private http: HttpClient) {}

  loadDocuments() {
    this.isLoading.set(true);
    this.http.get<{ items: Document[] }>(`${this.baseUrl}`).subscribe({
      next: (result) => {
        this.documents.set(result.items);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  getStats(): Observable<DocumentStats> {
    return this.http.get<DocumentStats>(`${this.baseUrl}/stats`);
  }

  upload(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<Document>(`${this.baseUrl}/upload`, formData);
  }

  delete(id: string) {
    return this.http.delete(`${this.baseUrl}/${id}`);
  }

  getDownloadUrl(id: string): string {
    return `${this.baseUrl}/${id}/download`;
  }
}
