import { Injectable, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export interface Language {
  code: string;
  name: string;
  flag: string;
}

@Injectable({
  providedIn: 'root',
})
export class LanguageService {
  private translate = inject(TranslateService);

  readonly languages: Language[] = [
    { code: 'en', name: 'English', flag: '🇬🇧' },
    { code: 'de', name: 'Deutsch', flag: '🇩🇪' },
    { code: 'fr', name: 'Français', flag: '🇫🇷' },
    { code: 'it', name: 'Italiano', flag: '🇮🇹' },
  ];

  currentLanguage = signal<Language>(this.languages[0]);

  init(): void {
    this.translate.addLangs(this.languages.map((l) => l.code));
    this.translate.setDefaultLang('en');

    const stored = localStorage.getItem('knowledgehub_lang');
    const browserLang = this.translate.getBrowserLang();
    const langCode = stored || (this.isSupported(browserLang) ? browserLang! : 'en');

    this.setLanguage(langCode);
  }

  setLanguage(code: string): void {
    const lang = this.languages.find((l) => l.code === code) || this.languages[0];
    this.currentLanguage.set(lang);
    this.translate.use(lang.code);
    localStorage.setItem('knowledgehub_lang', lang.code);
    document.documentElement.lang = lang.code;
  }

  private isSupported(lang: string | undefined): boolean {
    return !!lang && this.languages.some((l) => l.code === lang);
  }
}
