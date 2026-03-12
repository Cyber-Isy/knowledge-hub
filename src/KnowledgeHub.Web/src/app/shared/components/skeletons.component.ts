import { Component } from '@angular/core';

@Component({
  selector: 'app-document-list-skeleton',
  template: `
    <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
      <div class="bg-gray-50 border-b border-gray-200 px-6 py-3 flex gap-6">
        @for (w of ['40%', '15%', '15%', '10%']; track $index) {
          <div class="h-3 bg-gray-200 rounded animate-pulse" [style.width]="w"></div>
        }
      </div>
      @for (i of [1, 2, 3, 4, 5]; track i) {
        <div class="px-6 py-4 border-b border-gray-100 flex items-center gap-6">
          <div class="h-4 bg-gray-200 rounded animate-pulse w-2/5"></div>
          <div class="h-4 bg-gray-200 rounded animate-pulse w-16"></div>
          <div class="h-5 bg-gray-200 rounded-full animate-pulse w-16"></div>
          <div class="h-4 bg-gray-200 rounded animate-pulse w-12 ml-auto"></div>
        </div>
      }
    </div>
  `,
})
export class DocumentListSkeletonComponent {}

@Component({
  selector: 'app-chat-message-skeleton',
  template: `
    <div class="space-y-6 py-6 px-4 max-w-3xl mx-auto">
      @for (i of [1, 2, 3]; track i) {
        <div [class]="i % 2 !== 0 ? 'flex justify-end' : 'flex justify-start'">
          <div
            class="rounded-2xl px-4 py-3 max-w-[70%] space-y-2"
            [class]="i % 2 !== 0 ? 'bg-indigo-100' : 'bg-gray-100'"
          >
            <div class="h-3 bg-gray-300/50 rounded animate-pulse w-full"></div>
            <div class="h-3 bg-gray-300/50 rounded animate-pulse w-4/5"></div>
            @if (i % 2 === 0) {
              <div class="h-3 bg-gray-300/50 rounded animate-pulse w-3/5"></div>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class ChatMessageSkeletonComponent {}

@Component({
  selector: 'app-stat-skeleton',
  template: `
    <div class="bg-white rounded-xl border border-gray-200 p-6 space-y-3">
      <div class="h-3 bg-gray-200 rounded animate-pulse w-24"></div>
      <div class="h-7 bg-gray-200 rounded animate-pulse w-16"></div>
    </div>
  `,
})
export class StatSkeletonComponent {}
