---
name: capacitor
description: |
  Master Ionic Capacitor mobile integration for Siora projects. Provides patterns for platform detection, Camera, Geolocation, Push Notifications, and platform-specific styling. Use this skill when: (1) Detecting platform (iOS, Android, Web); (2) Accessing native features (Camera, GPS); (3) Implementing push notifications; (4) Using platform-specific CSS; (5) Testing Capacitor features; (6) Building cross-platform mobile/web apps
---

# Siora Capacitor Integration Patterns

Ionic Capacitor provides native platform integration with web fallbacks.

## Quick Start: Platform Detection

```typescript
// ✅ CORRECT: Signals-based platform detection
import { Injectable, signal, computed } from '@angular/core';
import { Capacitor } from '@capacitor/core';

@Injectable({ providedIn: 'root' })
export class PlatformService {
  isNative = signal(Capacitor.isNativePlatform());
  isIOS = signal(Capacitor.getPlatform() === 'ios');
  isAndroid = signal(Capacitor.getPlatform() === 'android');
  isWeb = signal(Capacitor.getPlatform() === 'web');

  supportsCamera = computed(() => this.isNative());
  supportsPushNotifications = computed(() => this.isNative());

  constructor() {
    // Add platform class to body for styling
    document.body.classList.add(Capacitor.getPlatform());
  }
}
```

## Camera Plugin

```typescript
// ✅ CORRECT: Camera with web fallback
import { Camera, CameraResultType, CameraSource } from '@capacitor/camera';

@Injectable({ providedIn: 'root' })
export class PhotoService {
  private platform = inject(PlatformService);

  photoUrl = signal<string | null>(null);
  photoLoading = signal(false);
  photoError = signal<string | null>(null);

  async takePicture() {
    if (!this.platform.supportsCamera()) {
      return this.selectFileFromWeb();
    }

    this.photoLoading.set(true);
    this.photoError.set(null);

    try {
      const image = await Camera.getPhoto({
        quality: 90,
        allowEditing: false,
        resultType: CameraResultType.Uri,
        source: CameraSource.Camera,
      });

      this.photoUrl.set(image.webPath || null);
    } catch (error: any) {
      this.photoError.set(error.message || 'Failed to take photo');
    } finally {
      this.photoLoading.set(false);
    }
  }

  async selectFromGallery() {
    // Similar implementation with CameraSource.Photos
  }

  private selectFileFromWeb(): Promise<void> {
    return new Promise((resolve) => {
      const input = document.createElement('input');
      input.type = 'file';
      input.accept = 'image/*';

      input.onchange = (e: any) => {
        const file = e.target.files[0];
        const reader = new FileReader();

        reader.onload = () => {
          this.photoUrl.set(reader.result as string);
          resolve();
        };

        reader.readAsDataURL(file);
      };

      input.click();
    });
  }
}
```

## Geolocation

```typescript
// ✅ CORRECT: Geolocation with error handling
import { Geolocation } from '@capacitor/geolocation';

@Injectable({ providedIn: 'root' })
export class LocationService {
  currentLocation = signal<{ lat: number; lng: number } | null>(null);
  locationError = signal<string | null>(null);
  locationLoading = signal(false);

  async getCurrentLocation() {
    this.locationLoading.set(true);
    this.locationError.set(null);

    try {
      const coordinates = await Geolocation.getCurrentPosition();

      this.currentLocation.set({
        lat: coordinates.coords.latitude,
        lng: coordinates.coords.longitude,
      });
    } catch (error: any) {
      this.locationError.set(error.message || 'Failed to get location');
    } finally {
      this.locationLoading.set(false);
    }
  }

  async watchLocation(callback: (location: { lat: number; lng: number }) => void) {
    try {
      return await Geolocation.watchPosition({}, (position) => {
        if (position.coords) {
          callback({
            lat: position.coords.latitude,
            lng: position.coords.longitude,
          });
        }
      });
    } catch (error) {
      console.error('Watch location error:', error);
    }
  }
}
```

## Push Notifications

```typescript
// ✅ CORRECT: Push notifications setup
import { PushNotifications } from '@capacitor/push-notifications';

@Injectable({ providedIn: 'root' })
export class PushNotificationService {
  private platform = inject(PlatformService);

  pushToken = signal<string | null>(null);
  notificationError = signal<string | null>(null);

  async setup() {
    if (!this.platform.supportsPushNotifications()) {
      return;
    }

    try {
      await PushNotifications.requestPermissions();
      await PushNotifications.register();

      PushNotifications.addListener('registration', (token) => {
        console.log('Push token:', token.value);
        this.pushToken.set(token.value);
        this.sendTokenToBackend(token.value);
      });

      PushNotifications.addListener('pushNotificationReceived', (notification) => {
        this.handleNotification(notification);
      });

      PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
        this.handleNotificationTap(action);
      });
    } catch (error: any) {
      this.notificationError.set(error.message);
    }
  }

  private sendTokenToBackend(token: string) {
    // Make API call to register token
  }

  private handleNotification(notification: any) {
    // Show in-app notification or update UI
  }

  private handleNotificationTap(action: any) {
    // Navigate based on notification
  }
}
```

## Platform-Specific Styling

```scss
// ✅ CORRECT: Platform-specific CSS

// Web-only
body.web {
  .desktop-sidebar {
    display: block;
  }
  .mobile-only {
    display: none;
  }
}

// iOS (safe areas for notch)
body.ios {
  .status-bar {
    padding-top: env(safe-area-inset-top);
  }
  .bottom-nav {
    padding-bottom: env(safe-area-inset-bottom);
  }
}

// Android (Material Design)
body.android {
  .container {
    padding: 16px;
  }
}

// Both mobile platforms
body.ios,
body.android {
  .desktop-sidebar {
    display: none;
  }
  .mobile-menu {
    display: block;
  }
}
```

## Complete Reference

See [capacitor-patterns.md](references/capacitor-patterns.md) for detailed patterns including:

- Platform-aware components
- Share functionality
- App linking
- Testing Capacitor features
- DO's and DON'Ts checklist

## Key Principles

✅ **DO**

- Always check platform support before using plugins
- Provide web fallbacks
- Use signals for async state
- Test on real devices
- Handle permissions properly

❌ **DON'T**

- Never assume Capacitor plugins are available
- Never ignore error handling
- Never use hardcoded platform checks
- Never forget platform-specific styling
