import {
  Component,
  input,
  signal,
  viewChild,
  ElementRef,
  effect,
} from '@angular/core';
import { ChatMessage, Citation } from '../../core/services/chat.service';
import { CitationCardComponent } from './citation-card.component';

@Component({
  selector: 'app-message-list',
  imports: [CitationCardComponent],
  template: `
    <div #scrollContainer class="h-full overflow-y-auto">
      <div class="max-w-3xl mx-auto py-6 px-4 space-y-6">
        @if (isLoading()) {
          <div class="flex justify-center py-12">
            <div class="flex items-center gap-2 text-gray-400">
              <span class="h-2 w-2 rounded-full bg-gray-300 animate-pulse"></span>
              <span class="h-2 w-2 rounded-full bg-gray-300 animate-pulse [animation-delay:150ms]"></span>
              <span class="h-2 w-2 rounded-full bg-gray-300 animate-pulse [animation-delay:300ms]"></span>
              <span class="text-sm ml-1">Loading messages...</span>
            </div>
          </div>
        } @else if (messages().length === 0 && !isStreaming()) {
          <div class="flex flex-col items-center justify-center py-24 text-gray-400">
            <svg class="w-12 h-12 mb-4 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
              <path stroke-linecap="round" stroke-linejoin="round" d="M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.087.16 2.185.283 3.293.369V21l4.076-4.076a1.526 1.526 0 011.037-.443 48.282 48.282 0 005.68-.494c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0012 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z" />
            </svg>
            <p class="text-lg font-medium mb-1">Start a conversation</p>
            <p class="text-sm">Ask a question about your uploaded documents</p>
          </div>
        } @else {
          @for (message of messages(); track message.id) {
            <div class="flex" [class]="message.role === 'user' ? 'justify-end' : 'justify-start'">
              <div
                [class]="message.role === 'user'
                  ? 'bg-indigo-600 text-white rounded-2xl rounded-br-md px-4 py-3 max-w-[80%]'
                  : 'bg-white border border-gray-200 text-gray-900 rounded-2xl rounded-bl-md px-4 py-3 max-w-[80%]'"
              >
                <div
                  class="text-sm leading-relaxed whitespace-pre-wrap break-words"
                  [class.prose-invert]="message.role === 'user'"
                  [innerHTML]="formatMessage(message.content)"
                ></div>

                @if (message.citations && message.citations.length > 0) {
                  <div class="mt-3 pt-3 border-t border-gray-100 space-y-2">
                    <p class="text-xs font-medium text-gray-500">Sources</p>
                    @for (citation of message.citations; track $index) {
                      <app-citation-card [citation]="citation" />
                    }
                  </div>
                }
              </div>
            </div>
          }

          <!-- Streaming message -->
          @if (isStreaming() && streamingContent()) {
            <div class="flex justify-start">
              <div class="bg-white border border-gray-200 text-gray-900 rounded-2xl rounded-bl-md px-4 py-3 max-w-[80%]">
                <div class="text-sm leading-relaxed whitespace-pre-wrap break-words" [innerHTML]="formatMessage(streamingContent())"></div>

                @if (streamingCitations().length > 0) {
                  <div class="mt-3 pt-3 border-t border-gray-100 space-y-2">
                    <p class="text-xs font-medium text-gray-500">Sources</p>
                    @for (citation of streamingCitations(); track $index) {
                      <app-citation-card [citation]="citation" />
                    }
                  </div>
                }
              </div>
            </div>
          }

          <!-- Typing indicator -->
          @if (isStreaming() && !streamingContent()) {
            <div class="flex justify-start">
              <div class="bg-white border border-gray-200 rounded-2xl rounded-bl-md px-4 py-3">
                <div class="flex items-center gap-1.5">
                  <span class="h-2 w-2 rounded-full bg-gray-400 animate-pulse"></span>
                  <span class="h-2 w-2 rounded-full bg-gray-400 animate-pulse [animation-delay:150ms]"></span>
                  <span class="h-2 w-2 rounded-full bg-gray-400 animate-pulse [animation-delay:300ms]"></span>
                </div>
              </div>
            </div>
          }
        }
      </div>
    </div>
  `,
})
export class MessageListComponent {
  messages = input.required<ChatMessage[]>();
  streamingContent = input.required<string>();
  streamingCitations = input.required<Citation[]>();
  isStreaming = input.required<boolean>();
  isLoading = input.required<boolean>();

  scrollContainer = viewChild.required<ElementRef<HTMLDivElement>>('scrollContainer');

  constructor() {
    effect(() => {
      // Track reactive dependencies to trigger scroll
      this.messages();
      this.streamingContent();
      this.isStreaming();

      // Schedule scroll after DOM update
      requestAnimationFrame(() => {
        const el = this.scrollContainer().nativeElement;
        el.scrollTop = el.scrollHeight;
      });
    });
  }

  formatMessage(content: string): string {
    if (!content) return '';

    let html = this.escapeHtml(content);

    // Bold: **text**
    html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');

    // Italic: *text*
    html = html.replace(/(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)/g, '<em>$1</em>');

    // Inline code: `code`
    html = html.replace(
      /`([^`]+)`/g,
      '<code class="px-1.5 py-0.5 bg-gray-100 text-gray-800 rounded text-xs font-mono">$1</code>',
    );

    // Code blocks: ```code```
    html = html.replace(
      /```(\w*)\n?([\s\S]*?)```/g,
      '<pre class="mt-2 mb-2 p-3 bg-gray-900 text-gray-100 rounded-lg overflow-x-auto text-xs font-mono"><code>$2</code></pre>',
    );

    // Headers
    html = html.replace(/^### (.+)$/gm, '<h4 class="font-semibold mt-2 mb-1">$1</h4>');
    html = html.replace(/^## (.+)$/gm, '<h3 class="font-semibold text-base mt-2 mb-1">$1</h3>');
    html = html.replace(/^# (.+)$/gm, '<h2 class="font-bold text-lg mt-2 mb-1">$1</h2>');

    // Bullet lists
    html = html.replace(/^- (.+)$/gm, '<li class="ml-4 list-disc">$1</li>');
    html = html.replace(/^(\d+)\. (.+)$/gm, '<li class="ml-4 list-decimal">$2</li>');

    return html;
  }

  private escapeHtml(text: string): string {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
}
