import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

describe('authGuard', () => {
  let authService: AuthService;
  let router: Router;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: Router,
          useValue: {
            navigate: vi.fn(),
            createUrlTree: vi.fn().mockReturnValue({ toString: () => '/login' } as unknown as UrlTree),
          },
        },
      ],
    });

    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    httpTesting = TestBed.inject(HttpTestingController);

    // Discard the /v1/auth/me request from AuthService constructor
    const meRequests = httpTesting.match(`${environment.apiUrl}/v1/auth/me`);
    meRequests.forEach((req) => req.flush(null, { status: 401, statusText: 'Unauthorized' }));

    localStorage.clear();
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.clear();
  });

  it('should allow access when token exists', () => {
    localStorage.setItem('auth_token', 'valid-token');

    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as any, {} as any),
    );

    expect(result).toBe(true);
  });

  it('should redirect to /login when no token exists', () => {
    localStorage.removeItem('auth_token');

    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as any, {} as any),
    );

    expect(result).not.toBe(true);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
  });
});
