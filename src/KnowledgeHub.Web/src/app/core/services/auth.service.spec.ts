import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { AuthService, AuthResponse } from './auth.service';
import { environment } from '../../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate: vi.fn() } },
      ],
    });

    service = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);

    // Discard the /v1/auth/me request triggered by the constructor's loadUser()
    const meRequests = httpTesting.match(`${environment.apiUrl}/v1/auth/me`);
    meRequests.forEach((req) => req.flush(null, { status: 401, statusText: 'Unauthorized' }));

    localStorage.clear();
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should call login endpoint', () => {
    const loginRequest = { email: 'test@example.com', password: 'password123' };
    const mockResponse: AuthResponse = {
      token: 'test-token',
      email: 'test@example.com',
      displayName: 'Test User',
      expiresAt: new Date().toISOString(),
    };

    service.login(loginRequest).subscribe((res) => {
      expect(res.token).toBe('test-token');
    });

    const req = httpTesting.expectOne(`${environment.apiUrl}/v1/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(loginRequest);
    req.flush(mockResponse);
  });

  it('should call register endpoint', () => {
    const registerRequest = {
      email: 'new@example.com',
      password: 'password123',
      confirmPassword: 'password123',
      displayName: 'New User',
    };

    service.register(registerRequest).subscribe();

    const req = httpTesting.expectOne(`${environment.apiUrl}/v1/auth/register`);
    expect(req.request.method).toBe('POST');
    req.flush({ token: 'new-token', email: 'new@example.com', expiresAt: new Date().toISOString() });
  });

  it('should store token on handleAuthResponse', () => {
    const response: AuthResponse = {
      token: 'stored-token',
      email: 'test@example.com',
      expiresAt: new Date().toISOString(),
    };

    service.handleAuthResponse(response);

    expect(localStorage.getItem('auth_token')).toBe('stored-token');

    // Flush the /v1/auth/me request triggered by loadUser
    const meReq = httpTesting.expectOne(`${environment.apiUrl}/v1/auth/me`);
    meReq.flush({ id: '1', email: 'test@example.com', createdAt: new Date().toISOString(), roles: [] });
  });

  it('should clear token and navigate on logout', () => {
    localStorage.setItem('auth_token', 'some-token');

    service.logout();

    expect(localStorage.getItem('auth_token')).toBeNull();
    expect(service.currentUser()).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('should return token from getToken', () => {
    localStorage.setItem('auth_token', 'my-token');
    expect(service.getToken()).toBe('my-token');
  });

  it('should return null from getToken when no token stored', () => {
    expect(service.getToken()).toBeNull();
  });

  it('should set isLoading to true on login', () => {
    service.login({ email: 'test@example.com', password: 'pass' }).subscribe();

    expect(service.isLoading()).toBe(true);

    const req = httpTesting.expectOne(`${environment.apiUrl}/v1/auth/login`);
    req.flush({ token: 't', email: 'test@example.com', expiresAt: new Date().toISOString() });
  });
});
