import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { Conversation } from '../../core/services/chat.service';

@Component({
  selector: 'app-conversation-sidebar',
  imports: [FormsModule, TranslateModule],
  template: `
    <aside class="w-72 bg-[var(--color-sidebar-bg)] border-r border-[var(--color-border)] flex flex-col h-full">
      <!-- New chat button -->
      <div class="p-3">
        <button
          (click)="newConversation.emit()"
          class="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl border border-[var(--color-border)] bg-[var(--color-card-bg)] text-sm font-medium text-[var(--color-text)] hover:bg-[var(--color-hover)] transition-colors"
        >
          <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 4.5v15m7.5-7.5h-15" />
          </svg>
          {{ 'CHAT.SIDEBAR.NEW_CHAT' | translate }}
        </button>
      </div>

      <!-- Conversation list -->
      <div class="flex-1 overflow-y-auto px-3 pb-3 space-y-0.5">
        @if (isLoading()) {
          <div class="py-8 text-center text-xs text-[var(--color-text-secondary)]">{{ 'CHAT.SIDEBAR.LOADING' | translate }}</div>
        } @else if (conversations().length === 0) {
          <div class="py-8 text-center text-xs text-[var(--color-text-secondary)]">{{ 'CHAT.SIDEBAR.EMPTY' | translate }}</div>
        } @else {
          @for (convo of conversations(); track convo.id) {
            <div
              class="group relative flex items-center rounded-lg px-3 py-2.5 text-sm cursor-pointer transition-colors"
              [class]="convo.id === activeConversationId()
                ? 'bg-indigo-50 text-indigo-700 [html[data-theme=dark]_&]:bg-indigo-950 [html[data-theme=dark]_&]:text-indigo-300'
                : 'text-[var(--color-text)] hover:bg-[var(--color-hover)]'"
              (click)="onSelect(convo.id)"
            >
              @if (editingId() === convo.id) {
                <input
                  #renameInput
                  [(ngModel)]="editTitle"
                  (keydown.enter)="confirmRename(convo.id)"
                  (keydown.escape)="cancelRename()"
                  (blur)="confirmRename(convo.id)"
                  class="flex-1 text-sm bg-[var(--color-input-bg)] border border-[var(--color-primary)] rounded px-2 py-0.5 outline-none focus:ring-1 focus:ring-[var(--color-primary)] text-[var(--color-text)]"
                  (click)="$event.stopPropagation()"
                />
              } @else {
                <span class="flex-1 truncate">{{ convo.title }}</span>

                <!-- Action buttons -->
                <div class="hidden group-hover:flex items-center gap-0.5 shrink-0 ml-2">
                  <button
                    (click)="startRename($event, convo)"
                    class="p-1 rounded hover:bg-[var(--color-hover)] text-[var(--color-text-secondary)]"
                    [title]="'CHAT.SIDEBAR.RENAME' | translate"
                  >
                    <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M16.862 4.487l1.687-1.688a1.875 1.875 0 112.652 2.652L6.832 19.82a4.5 4.5 0 01-1.897 1.13l-2.685.8.8-2.685a4.5 4.5 0 011.13-1.897L16.863 4.487z" />
                    </svg>
                  </button>
                  <button
                    (click)="onDelete($event, convo.id)"
                    class="p-1 rounded hover:bg-[var(--color-hover)] text-[var(--color-text-secondary)] hover:text-[var(--color-error)]"
                    [title]="'CHAT.SIDEBAR.DELETE' | translate"
                  >
                    <svg class="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                      <path stroke-linecap="round" stroke-linejoin="round" d="M14.74 9l-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 01-2.244 2.077H8.084a2.25 2.25 0 01-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 00-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 013.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 00-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 00-7.5 0" />
                    </svg>
                  </button>
                </div>
              }
            </div>
          }
        }
      </div>
    </aside>
  `,
})
export class ConversationSidebarComponent {
  conversations = input.required<Conversation[]>();
  activeConversationId = input.required<string | null>();
  isLoading = input.required<boolean>();

  selectConversation = output<string>();
  newConversation = output<void>();
  renameConversation = output<{ id: string; title: string }>();
  deleteConversation = output<string>();

  editingId = signal<string | null>(null);
  editTitle = '';
  confirmingDeleteId = signal<string | null>(null);

  onSelect(id: string): void {
    if (this.editingId() === id) return;
    this.selectConversation.emit(id);
  }

  startRename(event: Event, convo: Conversation): void {
    event.stopPropagation();
    this.editingId.set(convo.id);
    this.editTitle = convo.title;
  }

  confirmRename(id: string): void {
    const title = this.editTitle.trim();
    if (title && title !== this.getConversationTitle(id)) {
      this.renameConversation.emit({ id, title });
    }
    this.editingId.set(null);
  }

  cancelRename(): void {
    this.editingId.set(null);
  }

  onDelete(event: Event, id: string): void {
    event.stopPropagation();
    if (this.confirmingDeleteId() === id) {
      this.deleteConversation.emit(id);
      this.confirmingDeleteId.set(null);
    } else {
      this.confirmingDeleteId.set(id);
      setTimeout(() => {
        if (this.confirmingDeleteId() === id) {
          this.confirmingDeleteId.set(null);
        }
      }, 3000);
    }
  }

  private getConversationTitle(id: string): string {
    return this.conversations().find((c) => c.id === id)?.title ?? '';
  }
}
