import { Component, inject, OnInit, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { ChatService } from '../../core/services/chat.service';
import { MessageListComponent } from './message-list.component';
import { ConversationSidebarComponent } from './conversation-sidebar.component';

@Component({
  selector: 'app-chat',
  imports: [FormsModule, TranslateModule, MessageListComponent, ConversationSidebarComponent],
  template: `
    <div class="h-full flex flex-col -m-4 md:-m-6">
      <div class="flex flex-1 overflow-hidden relative">
        <!-- Mobile overlay for conversation sidebar -->
        @if (showConversations()) {
          <div
            class="fixed inset-0 z-20 bg-black/50 md:hidden"
            (click)="showConversations.set(false)"
          ></div>
        }

        <!-- Conversation sidebar -->
        <div
          class="fixed inset-y-0 left-0 z-30 w-72 transition-transform duration-200 md:static md:z-auto md:translate-x-0"
          [class.translate-x-0]="showConversations()"
          [class.-translate-x-full]="!showConversations()"
        >
          <app-conversation-sidebar
            [conversations]="chatService.conversations()"
            [activeConversationId]="chatService.activeConversationId()"
            [isLoading]="chatService.isLoadingConversations()"
            (selectConversation)="onSelectConversation($event)"
            (newConversation)="onNewConversation()"
            (renameConversation)="onRenameConversation($event)"
            (deleteConversation)="onDeleteConversation($event)"
          />
        </div>

        <!-- Chat area -->
        <div class="flex-1 flex flex-col min-w-0">
          <!-- Header -->
          <div class="h-14 border-b border-[var(--color-border)] bg-[var(--color-card-bg)] flex items-center px-4 md:px-6 gap-3">
            <!-- Toggle conversations button (mobile) -->
            <button
              (click)="showConversations.update(v => !v)"
              class="md:hidden p-1.5 rounded-lg text-[var(--color-text-secondary)] hover:bg-[var(--color-hover)]"
              aria-label="Toggle conversations"
            >
              <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <path stroke-linecap="round" stroke-linejoin="round" d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5" />
              </svg>
            </button>
            <h2 class="text-lg font-semibold text-[var(--color-text)] truncate">
              {{ chatService.activeConversation()?.title || ('CHAT.NEW_CHAT' | translate) }}
            </h2>
          </div>

          <!-- Messages -->
          <div class="flex-1 overflow-hidden bg-[var(--color-bg-secondary)]">
            <app-message-list
              [messages]="chatService.messages()"
              [streamingContent]="chatService.streamingContent()"
              [streamingCitations]="chatService.streamingCitations()"
              [isStreaming]="chatService.isStreaming()"
              [isLoading]="chatService.isLoadingMessages()"
            />
          </div>

          <!-- Input -->
          <div class="border-t border-[var(--color-border)] bg-[var(--color-card-bg)] p-3 md:p-4">
            <div class="max-w-3xl mx-auto flex gap-2 md:gap-3">
              <textarea
                #inputField
                [(ngModel)]="inputText"
                (keydown.enter)="onEnterKey($event)"
                [placeholder]="'CHAT.INPUT_PLACEHOLDER' | translate"
                rows="1"
                class="flex-1 resize-none rounded-xl border border-[var(--color-input-border)] bg-[var(--color-input-bg)] px-3 md:px-4 py-3 text-sm text-[var(--color-text)] placeholder-[var(--color-text-secondary)] focus:border-[var(--color-primary)] focus:ring-1 focus:ring-[var(--color-primary)] outline-none transition-colors"
                [disabled]="chatService.isStreaming()"
              ></textarea>
              <button
                (click)="send()"
                [disabled]="chatService.isStreaming() || !inputText().trim()"
                class="shrink-0 rounded-xl bg-[var(--color-primary)] px-4 md:px-5 py-3 text-sm font-medium text-white hover:opacity-90 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
              >
                @if (chatService.isStreaming()) {
                  <span class="inline-flex items-center gap-1.5">
                    <span class="h-1.5 w-1.5 rounded-full bg-white animate-pulse"></span>
                    <span class="hidden sm:inline">{{ 'CHAT.THINKING' | translate }}</span>
                  </span>
                } @else {
                  {{ 'CHAT.SEND' | translate }}
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
  showConversations = signal(false);

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
  }

  onSelectConversation(id: string): void {
    this.chatService.loadMessages(id);
    this.showConversations.set(false);
  }

  onNewConversation(): void {
    this.chatService.createConversation();
    this.showConversations.set(false);
  }

  onRenameConversation(event: { id: string; title: string }): void {
    this.chatService.renameConversation(event.id, event.title);
  }

  onDeleteConversation(id: string): void {
    this.chatService.deleteConversation(id);
  }
}
