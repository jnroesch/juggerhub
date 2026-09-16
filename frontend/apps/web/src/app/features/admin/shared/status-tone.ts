import { ChipTone } from '../../../shared/ui';
import { AccountStatus } from '../../../core/models/admin.models';

/**
 * The chip tone that stands for an account status. The three-armed colour ternary this
 * replaces was written out three times across the two admin screens that show a status
 * (GH #301) — three places for them to disagree about which colour "Suspended" is.
 */
export function accountStatusTone(status: AccountStatus): ChipTone {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Suspended':
      return 'warning';
    default:
      return 'danger';
  }
}
