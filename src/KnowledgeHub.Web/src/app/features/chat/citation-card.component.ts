import { Component, input, signal } from '@angular/core';
import { Citation } from '../../core/services/chat.service';

@Component({
  selector: 'app-citation-card',
  template: `
    <div class="border border-gray-100 rounded-lg bg-gray-50 overflow-hidden">
      <button
        (click)="expanded.set(!expanded())"
        class="w-full flex items-center justify-between px-3 py-2 text-left hover:bg-gray-100 transition-colors"
      >
        <div class="flex items-center gap-2 min-w-0">
          <svg class="w-3.5 h-3.5 shrink-0 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
          </svg>
          <span class="text-xs font-medium text-gray-700 truncate">{{ citation().documentName }}</span>
        </div>
        <div class="flex items-center gap-2 shrink-0">
          <span class="text-xs px-1.5 py-0.5 rounded-full font-medium"
            [class]="getScoreClass(citation().relevanceScore)"
          >
            {{ (citation().relevanceScore * 100).toFixed(0) }}%
          </span>
          <svg
            class="w-3.5 h-3.5 text-gray-400 transition-transform"
            [class.rotate-180]="expanded()"
            fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"
          >
            <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
          </svg>
        </div>
      </button>

      @if (!expanded()) {
        <div class="px-3 pb-2">
          <p class="text-xs text-gray-500 line-clamp-2">{{ getPreview() }}</p>
        </div>
      }

      @if (expanded()) {
        <div class="px-3 pb-3 border-t border-gray-100">
          <p class="text-xs text-gray-600 mt-2 whitespace-pre-wrap leading-relaxed">{{ citation().chunkContent }}</p>
        </div>
      }
    </div>
  `,
})
export class CitationCardComponent {
  citation = input.required<Citation>();
  expanded = signal(false);

  getPreview(): string {
    const content = this.citation().chunkContent;
    return content.length > 150 ? content.substring(0, 150) + '...' : content;
  }

  getScoreClass(score: number): string {
    if (score >= 0.8) return 'bg-green-100 text-green-700';
    if (score >= 0.5) return 'bg-yellow-100 text-yellow-700';
    return 'bg-gray-100 text-gray-600';
  }
}
