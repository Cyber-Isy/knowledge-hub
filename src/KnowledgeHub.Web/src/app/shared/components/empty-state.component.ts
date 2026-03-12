import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  template: `
    <div class="flex flex-col items-center justify-center py-16 text-gray-400">
      <div class="mb-4" [innerHTML]="icon()"></div>
      <p class="text-lg font-medium mb-1 text-gray-500">{{ title() }}</p>
      <p class="text-sm text-gray-400">{{ subtitle() }}</p>
      @if (actionLabel()) {
        <button
          (click)="action.emit()"
          class="mt-4 px-4 py-2 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-700 transition-colors"
        >
          {{ actionLabel() }}
        </button>
      }
    </div>
  `,
})
export class EmptyStateComponent {
  icon = input(
    '<svg class="w-12 h-12 text-gray-300" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5"><path stroke-linecap="round" stroke-linejoin="round" d="M2.25 13.5h3.86a2.25 2.25 0 012.012 1.244l.256.512a2.25 2.25 0 002.013 1.244h3.218a2.25 2.25 0 002.013-1.244l.256-.512a2.25 2.25 0 012.013-1.244h3.859m-19.5.338V18a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18v-4.162c0-.224-.034-.447-.1-.661L19.24 5.338a2.25 2.25 0 00-2.15-1.588H6.911a2.25 2.25 0 00-2.15 1.588L2.35 13.177a2.25 2.25 0 00-.1.661z" /></svg>',
  );
  title = input('Nothing here yet');
  subtitle = input('');
  actionLabel = input('');
  action = output<void>();
}
