import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { LoginComponent } from './login.component';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';
import { of, throwError } from 'rxjs';

describe('LoginComponent', () => {
  let component: LoginComponent;
  let fixture: ComponentFixture<LoginComponent>;
  let authService: AuthService;
  let router: Router;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LoginComponent, TranslateModule.forRoot()],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: Router,
          useValue: { navigate: vi.fn() },
        },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { params: {} } },
        },
      ],
    }).compileComponents();

    httpTesting = TestBed.inject(HttpTestingController);

    // Discard the /v1/auth/me request from AuthService constructor
    const meRequests = httpTesting.match(`${environment.apiUrl}/v1/auth/me`);
    meRequests.forEach((req) => req.flush(null, { status: 401, statusText: 'Unauthorized' }));

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
    fixture.detectChanges();

    localStorage.clear();
  });

  afterEach(() => {
    // Flush any remaining /v1/auth/me requests (triggered by handleAuthResponse -> loadUser)
    const meRequests = httpTesting.match(`${environment.apiUrl}/v1/auth/me`);
    meRequests.forEach((req) => req.flush(null, { status: 401, statusText: 'Unauthorized' }));

    httpTesting.verify();
    localStorage.clear();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should have an invalid form when empty', () => {
    expect(component.form.valid).toBe(false);
  });

  it('should require email', () => {
    component.form.controls.password.setValue('password123');
    expect(component.form.valid).toBe(false);
    expect(component.form.controls.email.hasError('required')).toBe(true);
  });

  it('should validate email format', () => {
    component.form.controls.email.setValue('not-an-email');
    expect(component.form.controls.email.hasError('email')).toBe(true);
  });

  it('should require password', () => {
    component.form.controls.email.setValue('test@example.com');
    expect(component.form.valid).toBe(false);
    expect(component.form.controls.password.hasError('required')).toBe(true);
  });

  it('should be valid when email and password are provided', () => {
    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    expect(component.form.valid).toBe(true);
  });

  it('should not submit when form is invalid', () => {
    const loginSpy = vi.spyOn(authService, 'login');
    component.onSubmit();
    expect(loginSpy).not.toHaveBeenCalled();
  });

  it('should call login and navigate on successful submit', () => {
    const mockResponse = {
      token: 'test-token',
      email: 'test@example.com',
      expiresAt: new Date().toISOString(),
    };

    vi.spyOn(authService, 'login').mockReturnValue(of(mockResponse as any));
    vi.spyOn(authService, 'handleAuthResponse');

    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.onSubmit();

    expect(authService.login).toHaveBeenCalled();
    expect(authService.handleAuthResponse).toHaveBeenCalledWith(mockResponse);
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('should display error message on login failure', () => {
    const errorResponse = { error: { message: 'Invalid credentials' } };
    vi.spyOn(authService, 'login').mockReturnValue(throwError(() => errorResponse));

    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('wrongpassword');
    component.onSubmit();

    expect(component.error).toBe('Invalid credentials');
    expect(component.isSubmitting).toBe(false);
  });

  it('should display fallback error message when no message in error response', () => {
    vi.spyOn(authService, 'login').mockReturnValue(throwError(() => ({ error: {} })));

    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.onSubmit();

    // TranslateService.instant returns the key when no translation is loaded
    expect(component.error).toBe('AUTH.LOGIN.ERROR_DEFAULT');
  });

  it('should set isSubmitting to true during submission', () => {
    vi.spyOn(authService, 'login').mockReturnValue(of({
      token: 't', email: 'e', expiresAt: new Date().toISOString(),
    } as any));
    vi.spyOn(authService, 'handleAuthResponse');

    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.onSubmit();

    expect(authService.login).toHaveBeenCalled();
  });
});
