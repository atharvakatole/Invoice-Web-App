import { describe, expect, it } from 'vitest';
import { environment } from './environment.prod';

describe('production environment', () => {
  it('uses the deployed production API instead of the local development API', () => {
    expect(environment.production).toBe(true);
    expect(environment.apiUrl).toBe('https://invoice-web-app-api.onrender.com/api');
    expect(environment.apiUrl).not.toContain('localhost');
  });
});
