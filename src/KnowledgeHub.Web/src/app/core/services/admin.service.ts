import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface AdminStats {
  totalUsers: number;
  totalDocuments: number;
  totalConversations: number;
  totalMessages: number;
  totalTokensUsed: number;
}

export interface AdminUser {
  id: string;
  email: string;
  displayName?: string;
  createdAt: string;
  isEnabled: boolean;
  documentCount: number;
  conversationCount: number;
  roles: string[];
}

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);

  stats = signal<AdminStats | null>(null);
  users = signal<AdminUser[]>([]);
  isLoadingStats = signal(false);
  isLoadingUsers = signal(false);

  loadStats(): void {
    this.isLoadingStats.set(true);
    this.http.get<AdminStats>(`${environment.apiUrl}/v1/admin/stats`).subscribe({
      next: (stats) => {
        this.stats.set(stats);
        this.isLoadingStats.set(false);
      },
      error: () => this.isLoadingStats.set(false),
    });
  }

  loadUsers(): void {
    this.isLoadingUsers.set(true);
    this.http.get<AdminUser[]>(`${environment.apiUrl}/v1/admin/users`).subscribe({
      next: (users) => {
        this.users.set(users);
        this.isLoadingUsers.set(false);
      },
      error: () => this.isLoadingUsers.set(false),
    });
  }

  toggleUser(id: string): void {
    this.http.put(`${environment.apiUrl}/v1/admin/users/${id}/toggle`, {}).subscribe({
      next: () => {
        this.users.update((users) =>
          users.map((u) => (u.id === id ? { ...u, isEnabled: !u.isEnabled } : u)),
        );
      },
    });
  }
}
