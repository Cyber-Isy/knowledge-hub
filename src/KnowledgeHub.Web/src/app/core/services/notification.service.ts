import { Injectable, signal } from '@angular/core';

export type NotificationType = 'success' | 'error' | 'warning' | 'info';

export interface Notification {
  id: string;
  type: NotificationType;
  message: string;
  autoDismiss: boolean;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  notifications = signal<Notification[]>([]);

  show(message: string, type: NotificationType = 'info', autoDismiss = true): void {
    const notification: Notification = {
      id: crypto.randomUUID(),
      type,
      message,
      autoDismiss,
    };

    this.notifications.update((n) => [...n, notification]);

    if (autoDismiss) {
      setTimeout(() => this.dismiss(notification.id), 5000);
    }
  }

  success(message: string): void {
    this.show(message, 'success');
  }

  error(message: string): void {
    this.show(message, 'error');
  }

  warning(message: string): void {
    this.show(message, 'warning');
  }

  info(message: string): void {
    this.show(message, 'info');
  }

  dismiss(id: string): void {
    this.notifications.update((n) => n.filter((item) => item.id !== id));
  }
}
