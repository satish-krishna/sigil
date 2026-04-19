# Capacitor Integration Patterns

Complete patterns for Ionic Capacitor native features.

## Platform Detection Service

```typescript
// ✅ CORRECT: Signals-based platform detection
import { Injectable, signal, computed, inject } from '@angular/core';
import { Capacitor } from '@capacitor/core';

@Injectable({ providedIn: 'root' })
export class PlatformService {
  isNative = signal(Capacitor.isNativePlatform());
  isIOS = signal(Capacitor.getPlatform() === 'ios');
  isAndroid = signal(Capacitor.getPlatform() === 'android');
  isWeb = signal(Capacitor.getPlatform() === 'web');

  supportsCamera = computed(() => this.isNative());
  supportsGeolocation = computed(() => true); // All platforms
  supportsPushNotifications = computed(() => this.isNative());
  supportsBiometrics = computed(() => this.isNative());

  constructor() {
    // Add platform class to body for styling
    document.body.classList.add(Capacitor.getPlatform());
  }
}

// Use in any component
supportsCamera = computed(() => this.platform.supportsCamera());
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
        source: CameraSource.Camera
      });

      this.photoUrl.set(image.webPath || null);
    } catch (error: any) {
      this.photoError.set(error.message || 'Failed to take photo');
      console.error('Camera error:', error);
    } finally {
      this.photoLoading.set(false);
    }
  }

  async selectFromGallery() {
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
        source: CameraSource.Photos
      });

      this.photoUrl.set(image.webPath || null);
    } catch (error: any) {
      this.photoError.set(error.message || 'Failed to select photo');
    } finally {
      this.photoLoading.set(false);
    }
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

// In component with Spartan-NG
@if (photoService.photoLoading()) {
  <div class="flex items-center gap-2 text-sm">
    <i class="fa-solid fa-spinner fa-spin"></i>
    <span>Taking photo...</span>
  </div>
}

@if (photoService.photoUrl()) {
  <img [src]="photoService.photoUrl()" class="w-full rounded-lg" />
}

<button hlmBtn (click)="photoService.takePicture()" class="w-full">
  <i class="fa-solid fa-camera mr-2"></i>
  Take Photo
</button>
```

## Geolocation

```typescript
// ✅ CORRECT: Geolocation with error handling
import { Geolocation } from '@capacitor/geolocation';

@Injectable({ providedIn: 'root' })
export class LocationService {
  currentLocation = signal<{lat: number, lng: number} | null>(null);
  locationError = signal<string | null>(null);
  locationLoading = signal(false);

  async getCurrentLocation() {
    this.locationLoading.set(true);
    this.locationError.set(null);

    try {
      const coordinates = await Geolocation.getCurrentPosition();

      this.currentLocation.set({
        lat: coordinates.coords.latitude,
        lng: coordinates.coords.longitude
      });
    } catch (error: any) {
      this.locationError.set(error.message || 'Failed to get location');
      console.error('Location error:', error);
    } finally {
      this.locationLoading.set(false);
    }
  }

  async watchLocation(callback: (location: {lat: number, lng: number}) => void) {
    try {
      return await Geolocation.watchPosition({}, (position) => {
        if (position.coords) {
          callback({
            lat: position.coords.latitude,
            lng: position.coords.longitude
          });
        }
      });
    } catch (error) {
      console.error('Watch location error:', error);
    }
  }
}

// In component with Spartan-NG
<button hlmBtn (click)="locationService.getCurrentLocation()" class="w-full">
  <i class="fa-solid fa-location-dot mr-2"></i>
  Get Location
</button>

@if (locationService.currentLocation(); as location) {
  <div hlmCard class="p-4 mt-4">
    <p class="text-sm font-medium">Current Location</p>
    <p class="text-sm text-muted-foreground">{{ location.lat }}, {{ location.lng }}</p>
  </div>
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
      // Request permission
      await PushNotifications.requestPermissions();

      // Register for push notifications
      await PushNotifications.register();

      // Listen for token registration
      PushNotifications.addListener('registration', (token) => {
        console.log('Push token:', token.value);
        this.pushToken.set(token.value);

        // Send token to backend
        this.sendTokenToBackend(token.value);
      });

      // Listen for incoming notifications
      PushNotifications.addListener('pushNotificationReceived', (notification) => {
        console.log('Push received:', notification);
        this.handleNotification(notification);
      });

      // Listen for notification taps
      PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
        console.log('Push tapped:', action);
        this.handleNotificationTap(action);
      });
    } catch (error: any) {
      this.notificationError.set(error.message);
      console.error('Push notification setup error:', error);
    }
  }

  private sendTokenToBackend(token: string) {
    // Make API call to register token
  }

  private handleNotification(notification: any) {
    // Show in-app notification or update UI
  }

  private handleNotificationTap(action: any) {
    // Navigate or perform action based on notification
  }
}
```

## Platform-Specific Styling

```scss
// ✅ CORRECT: Platform-specific CSS

// All platforms share common styles
.event-list {
  padding: 1rem;
  display: grid;
  gap: 1rem;
}

// Web-only styles
body.web {
  .desktop-sidebar {
    width: 300px;
    display: block;
  }

  .mobile-only {
    display: none;
  }
}

// iOS styles (handle safe areas for notch)
body.ios {
  .status-bar {
    padding-top: env(safe-area-inset-top);
  }

  .bottom-nav {
    padding-bottom: env(safe-area-inset-bottom);
  }

  // iOS button styling
  sl-button::part(base) {
    border-radius: 10px;
  }
}

// Android styles (Material Design)
body.android {
  .container {
    padding: 16px;
  }

  // Android button styling
  button[hlmBtn] {
    border-radius: 4px;
  }
}

// Mobile-first responsive (applies to both iOS and Android)
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

## Component with Platform Awareness

```typescript
// ✅ CORRECT: Component adapts to platform
@Component({
  selector: 'app-event-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div hlmCard class="p-6">
      <h2 class="text-2xl font-bold mb-4">{{ event.name }}</h2>

      <!-- Show native actions on mobile -->
      @if (platform.isNative()) {
        <div class="space-y-2 mb-4">
          <button hlmBtn variant="outline" class="w-full" (click)="shareEvent()">
            <i class="fa-solid fa-share mr-2"></i>
            Share
          </button>

          <button hlmBtn variant="outline" class="w-full" (click)="navigateToLocation()">
            <i class="fa-solid fa-map mr-2"></i>
            Navigate
          </button>

          @if (platform.supportsCamera()) {
            <button hlmBtn variant="outline" class="w-full" (click)="takePhoto()">
              <i class="fa-solid fa-camera mr-2"></i>
              Take Photo
            </button>
          }
        </div>
      }

      <!-- Show web actions on desktop -->
      @if (platform.isWeb()) {
        <button hlmBtn variant="outline" class="w-full" (click)="copyLink()">
          <i class="fa-solid fa-link mr-2"></i>
          Copy Link
        </button>
      }
    </div>
  `,
})
export class EventDetailComponent {
  @Input() event!: Event;

  protected platform = inject(PlatformService);
  private photoService = inject(PhotoService);

  async shareEvent() {
    if (!this.platform.isNative()) return;

    const { Share } = await import('@capacitor/share');
    await Share.share({
      title: this.event.name,
      text: this.event.description,
      url: window.location.href,
      dialogTitle: 'Share Event',
    });
  }

  async navigateToLocation() {
    if (!this.event.latitude || !this.event.longitude) return;

    const { Share } = await import('@capacitor/share');
    const mapsUrl = `https://maps.google.com/?q=${this.event.latitude},${this.event.longitude}`;

    if (this.platform.isNative()) {
      const { App } = await import('@capacitor/app');
      await App.openUrl({ url: mapsUrl });
    } else {
      window.open(mapsUrl);
    }
  }

  async takePhoto() {
    await this.photoService.takePicture();
  }

  copyLink() {
    navigator.clipboard.writeText(window.location.href);
  }
}
```

## Testing Capacitor Features

```typescript
describe('PhotoService', () => {
  let service: PhotoService;
  let platform: PlatformService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [PhotoService, PlatformService],
    });

    service = TestBed.inject(PhotoService);
    platform = TestBed.inject(PlatformService);
  });

  it('should use web fallback on web platform', async () => {
    spyOn(platform, 'supportsCamera').and.returnValue(false);
    await service.selectFromGallery();

    expect(service.photoUrl()).not.toBeNull();
  });

  it('should call Camera plugin on native', async () => {
    // Mock Camera plugin
    const cameraSpy = spyOn(Camera, 'getPhoto').and.returnValue(
      Promise.resolve({ webPath: 'path/to/image' })
    );

    await service.takePicture();

    expect(cameraSpy).toHaveBeenCalled();
  });
});
```

## DO's and DON'T's

### DO ✅

- Always check platform support before using plugins
- Provide web fallbacks
- Use signals for async state
- Test on real devices
- Handle permissions properly
- Provide user feedback (loading states)

### DON'T ❌

- Never assume Capacitor plugins are available
- Never ignore error handling
- Never use hardcoded platform checks
- Never forget platform-specific styling
- Never mix web and mobile code without guards
