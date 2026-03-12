import {
  Component,
  inject,
  OnInit,
  OnDestroy,
  signal,
  viewChild,
  ElementRef,
  afterNextRender,
  effect,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService, ChatMessage, Citation } from '../../core/services/chat.service';
import { MessageListComponent } from './message-list.component';
import { ConversationSidebarComponent } from './conversation-sidebar.component';

@Component({
  selector: 'app-chat',
  imports: [FormsModule, MessageListComponent, ConversationSidebarComponent],
  template: `
    <div class="h-full flex flex-col -m-6">
      <div class="flex flex-1 overflow-hidden">
        <!-- Conversation sidebar -->
        <app-conversation-sidebar
          [conversations]="chatService.conversations()"
          [activeConversationId]="chatService.activeConversationId()"
          [isLoading]="chatService.isLoadingConversations()"
          (selectConversation)="onSelectConversation($event)"
          (newConversation)="onNewConversation()"
          (renameConversation)="onRenameConversation($event)"
          (deleteConversation)="onDeleteConversation($event)"
        />

        <!-- Chat area -->
        <div class="flex-1 flex flex-col min-w-0">
          <!-- Header -->
          <div class="h-14 border-b border-gray-200 bg-white flex items-center px-6">
            <h2 class="text-lg font-semibold text-gray-900 truncate">
              {{ chatService.activeConversation()?.title || 'New Chat' }}
            </h2>
          </div>

          <!-- Messages -->
          <div class="flex-1 overflow-hidden">
            <app-message-list
              [messages]="chatService.messages()"
              [streamingContent]="chatService.streamingContent()"
              [streamingCitations]="chatService.streamingCitations()"
              [isStreaming]="chatService.isStreaming()"
              [isLoading]="chatService.isLoadingMessages()"
            />
          </div>

          <!-- Input -->
          <div class="border-t border-gray-200 bg-white p-4">
            <div class="max-w-3xl mx-auto flex gap-3">
              <textarea
                #inputField
                [(ngModel)]="inputText"
                (keydown.enter)="onEnterKey($event)"
                placeholder="Ask a question about your documents..."
                rows="1"
                class="flex-1 resize-none rounded-xl border border-gray-300 px-4 py-3 text-sm text-gray-900 placeholder-gray-400 focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none transition-colors"
                [disabled]="chatService.isStreaming()"
              ></textarea>
              <button
                (click)="send()"
                [disabled]="chatService.isStreaming() || !inputText().trim()"
                class="shrink-0 rounded-xl bg-indigo-600 px-5 py-3 text-sm font-medium text-white hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                @if (chatService.isStreaming()) {
                  <span class="inline-flex items-center gap-1.5">
                    <span class="h-1.5 w-1.5 rounded-full bg-white animate-pulse"></span>
                    Thinking
                  </span>
                } @else {
                  Send
                }
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
})
export class ChatComponent implements OnInit, OnDestroy {
  chatService = inject(ChatService);
  inputText = signal('');

  ngOnInit(): void {
    this.chatService.loadConversations();
    this.chatService.startConnection();
  }

  ngOnDestroy(): void {
    this.chatService.stopConnection();
  }

  onEnterKey(event: Event): void {
    const keyEvent = event as KeyboardEvent;
    if (!keyEvent.shiftKey) {
      keyEvent.preventDefault();
      this.send();
    }
  }

  async send(): Promise<void> {
    const text = this.inputText().trim();
    if (!text) return;
    this.inputText.set('');
    await this.chatService.sendMessage(text);

    // If this was the first message in a new conversation, reload conversations
    if (!this.chatService.activeConversationId()) {
      // The backend should return a conversation ID via SignalR
    }
  }

  onSelectConversation(id: string): void {
    this.chatService.loadMessages(id);
  }

  onNewConversation(): void {
    this.chatService.createConversation();
  }

  onRenameConversation(event: { id: string; title: string }): void {
    this.chatService.renameConversation(event.id, event.title);
  }

  onDeleteConversation(id: string): void {
    this.chatService.deleteConversation(id);
  }
}
