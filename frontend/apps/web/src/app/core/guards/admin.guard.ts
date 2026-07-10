import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { RecognitionAdminService } from '../services/recognition-admin.service';

/**
 * Gates the /admin area to platform admins (feature 012). UX guard only — the server
 * `PlatformAdmin` policy enforces every admin operation regardless. Non-admins are sent home.
 */
export const adminGuard: CanActivateFn = () => {
  const admin = inject(RecognitionAdminService);
  const router = inject(Router);

  return admin.checkAccess().pipe(map((ok) => (ok ? true : router.parseUrl('/'))));
};
