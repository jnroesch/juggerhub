/**
 * Shared UI primitives (feature 024) — the single, DESIGN.md-conformant source for
 * common building blocks. Import these instead of hand-assembling buttons, cards,
 * empty / loading / alert states, or page containers from raw Tailwind utilities.
 * See specs/024-ui-primitives/.
 *
 * ⚠ The shell imports from this barrel, so every module re-exported here lands in the
 * INITIAL bundle whether a first-paint screen uses it or not. `LowercaseInputDirective`
 * is deliberately NOT re-exported: it imports `@angular/forms` (~54 kB), which would drag
 * forms into the initial bundle for two lazy form screens. Import it by its own path.
 * Anything else that pulls a heavy dependency belongs outside this file for the same reason.
 */
export { ButtonDirective } from './button/button.directive';
export type { ButtonVariant, ButtonSize } from './button/button.directive';
export { IconComponent } from './icon/icon.component';
export type { IconSize } from './icon/icon.component';
export type { IconName } from './icon/icons';
export { CardComponent } from './card/card.component';
export type { CardPadding } from './card/card.component';
export { ChipDirective } from './chip/chip.directive';
export type { ChipTone } from './chip/chip.directive';
export { LoadingComponent } from './loading/loading.component';
export { AlertComponent } from './alert/alert.component';
export type { AlertTone } from './alert/alert.component';
export { EmptyStateComponent } from './empty-state/empty-state.component';
export { PageContainerComponent } from './page/page-container.component';
export type { PageWidth } from './page/page-container.component';
export { BumpDirective } from './bump/bump.directive';
export { RiseDirective, RiseScope } from './rise/rise.directive';
export { stepMotion } from './step-motion/step-motion';
export { LegalLinksComponent } from './legal-links/legal-links.component';
export type { LegalLinksVariant } from './legal-links/legal-links.component';
