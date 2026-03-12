import { Component } from '@angular/core';

@Component({
  selector: 'app-chat',
  template: `
    <div class="h-full flex flex-col">
      <h2 class="text-2xl font-bold text-gray-900 mb-6">Chat</h2>
      <div class="flex-1 flex items-center justify-center bg-white rounded-xl border border-gray-200">
        <div class="text-center text-gray-400">
          <p class="text-lg mb-1">Chat coming soon</p>
          <p class="text-sm">Upload documents first, then ask questions about them</p>
        </div>
      </div>
    </div>
  `,
})
export class ChatComponent {}
