import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  APP_INITIALIZER,
  ErrorHandler,
  inject,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideTranslateService, TranslateLoader } from '@ngx-translate/core';
import { TranslateHttpLoader, provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { LanguageService } from './core/services/language.service';
import { GlobalErrorHandler } from './core/error-handler/global-error-handler';

function initializeLanguage(): () => void {
  const langService = inject(LanguageService);
  return () => langService.init();
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideTranslateService({
      defaultLanguage: 'en',
      loader: { provide: TranslateLoader, useClass: TranslateHttpLoader },
    }),
    provideTranslateHttpLoader({ prefix: './i18n/', suffix: '.json' }),
    {
      provide: APP_INITIALIZER,
      useFactory: initializeLanguage,
      multi: true,
    },
    {
      provide: ErrorHandler,
      useClass: GlobalErrorHandler,
    },
  ],
};
