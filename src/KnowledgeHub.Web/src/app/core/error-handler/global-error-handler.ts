import { ErrorHandler, Injectable, inject } from '@angular/core';
import { NotificationService } from '../services/notification.service';

@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private notificationService = inject(NotificationService);

  handleError(error: unknown): void {
    let message = 'An unexpected error occurred.';

    if (error instanceof Error) {
      // Filter out known benign errors
      if (error.message?.includes('ExpressionChangedAfterItHasBeenCheckedError')) {
        return;
      }
      message = error.message;
    }

    console.error('Unhandled error:', error);
    this.notificationService.error(message);
  }
}
