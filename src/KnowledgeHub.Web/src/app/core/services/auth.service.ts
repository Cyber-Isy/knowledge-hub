import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';

export interface User {
  id: string;
  email: string;
  displayName?: string;
  createdAt: string;
  roles: string[];
}

export interface AuthResponse {
  token: string;
  email: string;
  displayName?: string;
  expiresAt: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  confirmPassword: string;
  displayName?: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly TOKEN_KEY = 'auth_token';

  currentUser = signal<User | null>(null);
  isAuthenticated = computed(() => !!this.currentUser());
  isAdmin = computed(() => this.currentUser()?.roles?.includes('Admin') ?? false);
  isLoading = signal(false);

  constructor(
    private http: HttpClient,
    private router: Router,
  ) {
    this.loadUser();
  }

  login(request: LoginRequest) {
    this.isLoading.set(true);
    return this.http.post<AuthResponse>(`${environment.apiUrl}/v1/auth/login`, request);
  }

  register(request: RegisterRequest) {
    this.isLoading.set(true);
    return this.http.post<AuthResponse>(`${environment.apiUrl}/v1/auth/register`, request);
  }

  handleAuthResponse(response: AuthResponse) {
    localStorage.setItem(this.TOKEN_KEY, response.token);
    this.loadUser();
  }

  logout() {
    localStorage.removeItem(this.TOKEN_KEY);
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  private loadUser() {
    const token = this.getToken();
    if (!token) return;

    this.http.get<User>(`${environment.apiUrl}/v1/auth/me`).subscribe({
      next: (user) => {
        this.currentUser.set(user);
        this.isLoading.set(false);
      },
      error: () => {
        localStorage.removeItem(this.TOKEN_KEY);
        this.currentUser.set(null);
        this.isLoading.set(false);
      },
    });
  }
}
