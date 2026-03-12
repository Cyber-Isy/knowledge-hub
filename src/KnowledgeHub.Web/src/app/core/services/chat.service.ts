import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

export interface ChatMessage {
  id: string;
  conversationId: string;
  role: 'user' | 'assistant';
  content: string;
  citations?: Citation[];
  createdAt: string;
}

export interface Citation {
  documentId: string;
  documentName: string;
  chunkContent: string;
  relevanceScore: number;
}

export interface Conversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
}

interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class ChatService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);
  private hubConnection: signalR.HubConnection | null = null;

  conversations = signal<Conversation[]>([]);
  activeConversationId = signal<string | null>(null);
  messages = signal<ChatMessage[]>([]);
  streamingContent = signal<string>('');
  streamingCitations = signal<Citation[]>([]);
  isStreaming = signal(false);
  isLoadingMessages = signal(false);
  isLoadingConversations = signal(false);

  activeConversation = computed(() => {
    const id = this.activeConversationId();
    return this.conversations().find((c) => c.id === id) ?? null;
  });

  async startConnection(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    const token = this.authService.getToken();

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chat', {
        accessTokenFactory: () => token ?? '',
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveSources', (sources: { documentChunkId: string; fileName: string; score: number }[]) => {
      this.streamingCitations.set(
        sources.map((s) => ({
          documentId: s.documentChunkId,
          documentName: s.fileName,
          chunkContent: '',
          relevanceScore: s.score,
        })),
      );
    });

    this.hubConnection.on('StreamCompleted', (messageId: string) => {
      const content = this.streamingContent();
      const citations = this.streamingCitations();

      const assistantMessage: ChatMessage = {
        id: messageId,
        conversationId: this.activeConversationId() ?? '',
        role: 'assistant',
        content,
        citations: citations.length > 0 ? citations : undefined,
        createdAt: new Date().toISOString(),
      };

      this.messages.update((msgs) => [...msgs, assistantMessage]);
      this.streamingContent.set('');
      this.streamingCitations.set([]);
      this.isStreaming.set(false);

      // Refresh conversations to get updated titles
      this.loadConversations();
    });

    try {
      await this.hubConnection.start();
    } catch {
      // Connection failed — will retry on next send
    }
  }

  async stopConnection(): Promise<void> {
    if (this.hubConnection) {
      await this.hubConnection.stop();
      this.hubConnection = null;
    }
  }

  loadConversations(): void {
    this.isLoadingConversations.set(true);
    this.http.get<PagedResult<Conversation>>(`${environment.apiUrl}/v1/chat/conversations`).subscribe({
      next: (result) => {
        this.conversations.set(result.items);
        this.isLoadingConversations.set(false);
      },
      error: () => this.isLoadingConversations.set(false),
    });
  }

  loadMessages(conversationId: string): void {
    this.isLoadingMessages.set(true);
    this.activeConversationId.set(conversationId);
    this.http
      .get<PagedResult<ChatMessage>>(`${environment.apiUrl}/v1/chat/conversations/${conversationId}/messages`)
      .subscribe({
        next: (result) => {
          this.messages.set(result.items);
          this.isLoadingMessages.set(false);
        },
        error: () => this.isLoadingMessages.set(false),
      });
  }

  async sendMessage(content: string): Promise<void> {
    if (this.isStreaming() || !content.trim()) return;

    await this.startConnection();

    // Create a conversation first if we don't have one
    let conversationId = this.activeConversationId();
    if (!conversationId) {
      try {
        const conversation = await this.http
          .post<Conversation>(`${environment.apiUrl}/v1/chat/conversations`, {})
          .toPromise();
        if (conversation) {
          conversationId = conversation.id;
          this.activeConversationId.set(conversationId);
          this.loadConversations();
        }
      } catch {
        return;
      }
    }

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      conversationId: conversationId ?? '',
      role: 'user',
      content: content.trim(),
      createdAt: new Date().toISOString(),
    };

    this.messages.update((msgs) => [...msgs, userMessage]);
    this.isStreaming.set(true);
    this.streamingContent.set('');
    this.streamingCitations.set([]);

    try {
      if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
        // Use SignalR streaming (server-to-client)
        this.hubConnection.stream('StreamMessage', conversationId, content.trim()).subscribe({
          next: (token: string) => {
            this.streamingContent.update((c) => c + token);
          },
          error: (err: unknown) => {
            const errorMsg = err instanceof Error ? err.message : 'Unknown error';
            const errorMessage: ChatMessage = {
              id: crypto.randomUUID(),
              conversationId: conversationId ?? '',
              role: 'assistant',
              content: `Error: ${errorMsg}`,
              createdAt: new Date().toISOString(),
            };
            this.messages.update((msgs) => [...msgs, errorMessage]);
            this.isStreaming.set(false);
          },
          complete: () => {
            // StreamCompleted event from hub will handle the rest
          },
        });
      } else {
        // Fallback to REST if SignalR not connected
        this.http
          .post<{ message: ChatMessage; sources: Citation[] }>(
            `${environment.apiUrl}/v1/chat/conversations/${conversationId}/messages`,
            { content: content.trim() },
          )
          .subscribe({
            next: (response) => {
              this.messages.update((msgs) => [...msgs, response.message]);
              this.isStreaming.set(false);
            },
            error: () => this.isStreaming.set(false),
          });
      }
    } catch {
      this.isStreaming.set(false);
    }
  }

  createConversation(): void {
    this.activeConversationId.set(null);
    this.messages.set([]);
    this.streamingContent.set('');
    this.isStreaming.set(false);
  }

  renameConversation(id: string, title: string): void {
    this.http.put(`${environment.apiUrl}/v1/chat/conversations/${id}`, { title }).subscribe({
      next: () => {
        this.conversations.update((convos) =>
          convos.map((c) => (c.id === id ? { ...c, title } : c)),
        );
      },
    });
  }

  deleteConversation(id: string): void {
    this.http.delete(`${environment.apiUrl}/v1/chat/conversations/${id}`).subscribe({
      next: () => {
        this.conversations.update((convos) => convos.filter((c) => c.id !== id));
        if (this.activeConversationId() === id) {
          this.createConversation();
        }
      },
    });
  }
}
