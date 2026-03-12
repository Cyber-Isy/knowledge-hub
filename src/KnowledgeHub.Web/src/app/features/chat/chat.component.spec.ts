import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ChatComponent } from './chat.component';
import { ChatService } from '../../core/services/chat.service';
import { environment } from '../../../environments/environment';

describe('ChatComponent', () => {
  let component: ChatComponent;
  let fixture: ComponentFixture<ChatComponent>;
  let chatService: ChatService;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChatComponent, TranslateModule.forRoot()],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);

    // Discard /v1/auth/me request from AuthService constructor
    const meRequests = httpTesting.match(`${environment.apiUrl}/v1/auth/me`);
    meRequests.forEach((req) => req.flush(null, { status: 401, statusText: 'Unauthorized' }));

    chatService = TestBed.inject(ChatService);

    // Stub startConnection to prevent actual SignalR connection
    vi.spyOn(chatService, 'startConnection').mockResolvedValue();
    vi.spyOn(chatService, 'stopConnection').mockResolvedValue();

    fixture = TestBed.createComponent(ChatComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.clear();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load conversations on init', () => {
    const loadSpy = vi.spyOn(chatService, 'loadConversations');

    fixture.detectChanges();

    expect(loadSpy).toHaveBeenCalled();

    // Flush conversations request
    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));
  });

  it('should start connection on init', () => {
    fixture.detectChanges();

    expect(chatService.startConnection).toHaveBeenCalled();

    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));
  });

  it('should stop connection on destroy', () => {
    fixture.detectChanges();

    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));

    fixture.destroy();

    expect(chatService.stopConnection).toHaveBeenCalled();
  });

  it('should send message and clear input', async () => {
    const sendSpy = vi.spyOn(chatService, 'sendMessage').mockResolvedValue();

    fixture.detectChanges();
    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));

    component.inputText.set('Hello, world!');
    await component.send();

    expect(sendSpy).toHaveBeenCalledWith('Hello, world!');
    expect(component.inputText()).toBe('');
  });

  it('should not send empty messages', async () => {
    const sendSpy = vi.spyOn(chatService, 'sendMessage').mockResolvedValue();

    fixture.detectChanges();
    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));

    component.inputText.set('   ');
    await component.send();

    expect(sendSpy).not.toHaveBeenCalled();
  });

  it('should load messages on conversation selection', () => {
    const loadMsgSpy = vi.spyOn(chatService, 'loadMessages');

    fixture.detectChanges();
    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));

    component.onSelectConversation('conv-123');

    expect(loadMsgSpy).toHaveBeenCalledWith('conv-123');

    // Flush the messages request
    const msgReqs = httpTesting.match(
      `${environment.apiUrl}/v1/chat/conversations/conv-123/messages`,
    );
    msgReqs.forEach((req) => req.flush([]));
  });

  it('should create new conversation', () => {
    const createSpy = vi.spyOn(chatService, 'createConversation');

    fixture.detectChanges();
    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));

    component.onNewConversation();

    expect(createSpy).toHaveBeenCalled();
  });

  it('should delete conversation', () => {
    const deleteSpy = vi.spyOn(chatService, 'deleteConversation');

    fixture.detectChanges();
    const convReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations`);
    convReqs.forEach((req) => req.flush([]));

    component.onDeleteConversation('conv-456');

    expect(deleteSpy).toHaveBeenCalledWith('conv-456');

    // Flush the delete request
    const delReqs = httpTesting.match(`${environment.apiUrl}/v1/chat/conversations/conv-456`);
    delReqs.forEach((req) => req.flush(null));
  });
});
