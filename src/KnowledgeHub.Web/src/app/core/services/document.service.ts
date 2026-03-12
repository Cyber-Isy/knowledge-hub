import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
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

@Injectable({ providedIn: 'root' })
export class DocumentService {
  documents = signal<Document[]>([]);
  isLoading = signal(false);

  constructor(private http: HttpClient) {}

  loadDocuments() {
    this.isLoading.set(true);
    this.http.get<Document[]>(`${environment.apiUrl}/documents`).subscribe({
      next: (docs) => {
        this.documents.set(docs);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  upload(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<Document>(`${environment.apiUrl}/documents/upload`, formData);
  }

  delete(id: string) {
    return this.http.delete(`${environment.apiUrl}/documents/${id}`);
  }

  getDownloadUrl(id: string): string {
    return `${environment.apiUrl}/documents/${id}/download`;
  }
}
