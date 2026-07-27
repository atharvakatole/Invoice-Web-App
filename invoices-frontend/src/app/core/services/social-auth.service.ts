import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

declare const google: any;
declare const AppleID: any;

/**
 * Lazily loads each provider's JS SDK and wraps their sign-in flows,
 * returning the raw token that should be sent to
 * AuthService.externalLogin(provider, token).
 *
 * Configure client/app IDs in src/environments/environment*.ts.
 */
@Injectable({ providedIn: 'root' })
export class SocialAuthService {
  private scriptsLoaded: Record<string, Promise<void> | undefined> = {};

  private loadScript(id: string, src: string): Promise<void> {
    const existing = this.scriptsLoaded[id];
    if (existing) return existing;

    const promise = new Promise<void>((resolve, reject) => {
      if (document.getElementById(id)) { resolve(); return; }
      const script = document.createElement('script');
      script.id = id;
      script.src = src;
      script.async = true;
      script.defer = true;
      script.onload = () => resolve();
      script.onerror = () => reject(new Error(`Failed to load script: ${src}`));
      document.head.appendChild(script);
    });

    this.scriptsLoaded[id] = promise;
    return promise;
  }

  // ===================== Google =====================

  /**
   * Renders the official Google "Sign in with Google" button into the
   * given element and resolves with the ID token once the user completes
   * sign-in.
   */
  async signInWithGoogle(buttonEl: HTMLElement): Promise<string> {
    if (!environment.googleClientId) {
      throw new Error('Google sign-in is not configured. Set googleClientId in environment.ts.');
    }

    await this.loadScript('google-identity-sdk', 'https://accounts.google.com/gsi/client');

    return new Promise((resolve, reject) => {
      google.accounts.id.initialize({
        client_id: environment.googleClientId,
        callback: (response: { credential: string }) => {
          if (response?.credential) resolve(response.credential);
          else reject(new Error('Google sign-in did not return a credential'));
        }
      });

      google.accounts.id.renderButton(buttonEl, {
        theme: 'outline',
        size: 'large',
        width: buttonEl.clientWidth || 320,
        text: 'continue_with'
      });
    });
  }

  // ===================== Facebook =====================

  async signInWithFacebook(): Promise<string> {
    if (!environment.facebookAppId) {
      throw new Error('Facebook sign-in is not configured. Set facebookAppId in environment.ts.');
    }

    await this.loadScript('facebook-jssdk', 'https://connect.facebook.net/en_US/sdk.js');

    await new Promise<void>((resolve) => {
      if (typeof (window as any).FB !== 'undefined') { resolve(); return; }
      (window as any).fbAsyncInit = () => {
        (window as any).FB.init({ appId: environment.facebookAppId, version: 'v19.0', xfbml: false });
        resolve();
      };
    });

    return new Promise((resolve, reject) => {
      (window as any).FB.login((response: any) => {
        if (response?.authResponse?.accessToken) {
          resolve(response.authResponse.accessToken);
        } else {
          reject(new Error('Facebook sign-in was cancelled'));
        }
      }, { scope: 'email,public_profile' });
    });
  }

  // ===================== Apple =====================

  async signInWithApple(): Promise<{ token: string; fullName?: string }> {
    if (!environment.appleClientId) {
      throw new Error('Apple sign-in is not configured. Set appleClientId in environment.ts.');
    }

    await this.loadScript('appleid-auth-sdk', 'https://appleid.cdn-apple.com/appleauth/static/jsapi/appleid/1/en_US/appleid.auth.js');

    AppleID.auth.init({
      clientId: environment.appleClientId,
      scope: 'name email',
      redirectURI: window.location.origin,
      usePopup: true
    });

    try {
      const res = await AppleID.auth.signIn();
      const idToken = res?.authorization?.id_token;
      if (!idToken) throw new Error('Apple sign-in did not return a token');

      let fullName: string | undefined;
      if (res?.user?.name) {
        const { firstName, lastName } = res.user.name;
        fullName = [firstName, lastName].filter(Boolean).join(' ') || undefined;
      }

      return { token: idToken, fullName };
    } catch (err: any) {
      if (err?.error === 'popup_closed_by_user') {
        throw new Error('Apple sign-in was cancelled');
      }
      throw new Error('Apple sign-in failed');
    }
  }
}
