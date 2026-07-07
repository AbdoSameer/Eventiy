import { Injectable, signal } from '@angular/core';
import { AuthResponse } from '../../core/models/auth.model';

@Injectable({ providedIn: 'root' })
export class SessionStorageService {
  private readonly storageKey = 'auth_user';

  save(response: AuthResponse): void {
    localStorage.setItem(this.storageKey, JSON.stringify(response));
  }

  load(): AuthResponse | null {
    try {
      const stored = localStorage.getItem(this.storageKey);
      return stored ? (JSON.parse(stored) as AuthResponse) : null;
    } catch {
      this.clear();
      return null;
    }
  }

  clear(): void {
    localStorage.removeItem(this.storageKey);
  }
}
