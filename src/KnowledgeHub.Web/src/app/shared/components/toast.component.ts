import { Component, inject } from '@angular/core';
import { NotificationService, Notification } from '../../core/services/notification.service';

@Component({
  selector: 'app-toast',
  template: `
    <div class="fixed top-4 right-4 z-50 flex flex-col gap-2 max-w-sm w-full pointer-events-none">
      @for (notification of notificationService.notifications(); track notification.id) {
        <div
          class="pointer-events-auto rounded-lg border px-4 py-3 shadow-lg flex items-start gap-3 animate-slide-in"
          [class]="getToastClasses(notification)"
        >
          <span class="shrink-0 mt-0.5" [innerHTML]="getIcon(notification.type)"></span>
          <p class="text-sm flex-1">{{ notification.message }}</p>
          <button
            (click)="notificationService.dismiss(notification.id)"
            class="shrink-0 p-0.5 rounded hover:opacity-70 transition-opacity"
          >
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    @keyframes slideIn {
      from {
        transform: translateX(100%);
        opacity: 0;
      }
      to {
        transform: translateX(0);
        opacity: 1;
      }
    }
    .animate-slide-in {
      animation: slideIn 0.3s ease-out;
    }
  `,
})
export class ToastComponent {
  notificationService = inject(NotificationService);

  getToastClasses(notification: Notification): string {
    const base = 'border';
    switch (notification.type) {
      case 'success':
        return `${base} bg-green-50 border-green-200 text-green-800`;
      case 'error':
        return `${base} bg-red-50 border-red-200 text-red-800`;
      case 'warning':
        return `${base} bg-yellow-50 border-yellow-200 text-yellow-800`;
      case 'info':
        return `${base} bg-blue-50 border-blue-200 text-blue-800`;
    }
  }

  getIcon(type: string): string {
    switch (type) {
      case 'success':
        return '<svg class="w-5 h-5 text-green-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M9 12.75L11.25 15 15 9.75M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg>';
      case 'error':
        return '<svg class="w-5 h-5 text-red-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" /></svg>';
      case 'warning':
        return '<svg class="w-5 h-5 text-yellow-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m9-.75a9 9 0 11-18 0 9 9 0 0118 0zm-9 3.75h.008v.008H12v-.008z" /></svg>';
      default:
        return '<svg class="w-5 h-5 text-blue-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M11.25 11.25l.041-.02a.75.75 0 011.063.852l-.708 2.836a.75.75 0 001.063.853l.041-.021M21 12a9 9 0 11-18 0 9 9 0 0118 0zm-9-3.75h.008v.008H12V8.25z" /></svg>';
    }
  }
}
