import { Component, inject, signal } from '@angular/core';
import { LanguageService, Language } from '../../core/services/language.service';

@Component({
  selector: 'app-language-switcher',
  template: `
    <div class="relative">
      <button
        (click)="isOpen.set(!isOpen())"
        class="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg text-sm text-gray-600 hover:bg-gray-100 transition-colors"
      >
        <span>{{ langService.currentLanguage().flag }}</span>
        <span class="hidden sm:inline">{{ langService.currentLanguage().name }}</span>
        <svg class="w-3.5 h-3.5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
          <path stroke-linecap="round" stroke-linejoin="round" d="M19.5 8.25l-7.5 7.5-7.5-7.5" />
        </svg>
      </button>

      @if (isOpen()) {
        <div
          class="absolute right-0 top-full mt-1 bg-white border border-gray-200 rounded-lg shadow-lg py-1 z-50 min-w-[140px]"
        >
          @for (lang of langService.languages; track lang.code) {
            <button
              (click)="selectLanguage(lang)"
              class="w-full flex items-center gap-2 px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 transition-colors"
              [class.bg-indigo-50]="lang.code === langService.currentLanguage().code"
              [class.text-indigo-700]="lang.code === langService.currentLanguage().code"
            >
              <span>{{ lang.flag }}</span>
              <span>{{ lang.name }}</span>
            </button>
          }
        </div>
      }
    </div>

    @if (isOpen()) {
      <div class="fixed inset-0 z-40" (click)="isOpen.set(false)"></div>
    }
  `,
})
export class LanguageSwitcherComponent {
  langService = inject(LanguageService);
  isOpen = signal(false);

  selectLanguage(lang: Language): void {
    this.langService.setLanguage(lang.code);
    this.isOpen.set(false);
  }
}
