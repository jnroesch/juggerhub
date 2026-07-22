/**
 * Shared UI primitives (feature 024) — the single, DESIGN.md-conformant source for
 * common building blocks. Import these instead of hand-assembling buttons, cards,
 * empty / loading / alert states, or page containers from raw Tailwind utilities.
 * See specs/024-ui-primitives/.
 */
export { ButtonDirective } from './button/button.directive';
export type { ButtonVariant, ButtonSize } from './button/button.directive';
export { IconComponent } from './icon/icon.component';
export type { IconName } from './icon/icons';
export { CardComponent } from './card/card.component';
export { LoadingComponent } from './loading/loading.component';
export { AlertComponent } from './alert/alert.component';
export type { AlertTone } from './alert/alert.component';
export { EmptyStateComponent } from './empty-state/empty-state.component';
export { PageContainerComponent } from './page/page-container.component';
export type { PageWidth } from './page/page-container.component';
