import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router, ActivatedRoute } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { RegisterComponent } from './register.component';
import { AuthService } from '../../core/services/auth.service';
import { environment } from '../../../environments/environment';
import { of, throwError } from 'rxjs';

describe('RegisterComponent', () => {
  let component: RegisterComponent;
  let fixture: ComponentFixture<RegisterComponent>;
  let authService: AuthService;
  let router: Router;
  let httpTesting: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterComponent, TranslateModule.forRoot()],
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

    fixture = TestBed.createComponent(RegisterComponent);
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
    component.form.controls.confirmPassword.setValue('password123');
    expect(component.form.controls.email.hasError('required')).toBe(true);
  });

  it('should validate email format', () => {
    component.form.controls.email.setValue('invalid');
    expect(component.form.controls.email.hasError('email')).toBe(true);
  });

  it('should require minimum password length', () => {
    component.form.controls.password.setValue('short');
    expect(component.form.controls.password.hasError('minlength')).toBe(true);
  });

  it('should require confirm password', () => {
    expect(component.form.controls.confirmPassword.hasError('required')).toBe(true);
  });

  it('should be valid with all required fields', () => {
    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.form.controls.confirmPassword.setValue('password123');
    expect(component.form.valid).toBe(true);
  });

  it('should show error when passwords do not match', () => {
    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.form.controls.confirmPassword.setValue('different123');
    component.onSubmit();

    // TranslateService.instant returns the key when no translation is loaded
    expect(component.error).toBe('AUTH.REGISTER.PASSWORD_MISMATCH');
  });

  it('should not call register when passwords do not match', () => {
    const registerSpy = vi.spyOn(authService, 'register');

    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.form.controls.confirmPassword.setValue('different123');
    component.onSubmit();

    expect(registerSpy).not.toHaveBeenCalled();
  });

  it('should call register on valid submit with matching passwords', () => {
    const mockResponse = {
      token: 'test-token',
      email: 'test@example.com',
      expiresAt: new Date().toISOString(),
    };

    vi.spyOn(authService, 'register').mockReturnValue(of(mockResponse as any));
    vi.spyOn(authService, 'handleAuthResponse');

    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.form.controls.confirmPassword.setValue('password123');
    component.onSubmit();

    expect(authService.register).toHaveBeenCalled();
    expect(authService.handleAuthResponse).toHaveBeenCalledWith(mockResponse);
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('should display error on registration failure', () => {
    const errorResponse = { error: { errors: ['Email already exists'] } };
    vi.spyOn(authService, 'register').mockReturnValue(throwError(() => errorResponse));

    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.form.controls.confirmPassword.setValue('password123');
    component.onSubmit();

    expect(component.error).toBe('Email already exists');
    expect(component.isSubmitting).toBe(false);
  });

  it('should allow optional displayName', () => {
    component.form.controls.displayName.setValue('Test User');
    component.form.controls.email.setValue('test@example.com');
    component.form.controls.password.setValue('password123');
    component.form.controls.confirmPassword.setValue('password123');
    expect(component.form.valid).toBe(true);
  });
});
