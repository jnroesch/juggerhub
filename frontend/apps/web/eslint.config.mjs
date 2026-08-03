import nx from '@nx/eslint-plugin';
import baseConfig from '../../eslint.config.mjs';

export default [
  ...nx.configs['flat/angular'],
  ...nx.configs['flat/angular-template'],
  ...baseConfig,
  {
    files: ['**/*.ts'],
    rules: {
      // Angular 22 made `OnPush` the default change detection strategy. The v22
      // `change-detection-eager` migration added an explicit
      // `ChangeDetectionStrategy.Eager` to 96 of our 99 components to preserve
      // pre-v22 rendering behavior, but angular-eslint 22 ships this rule as an
      // error in its recommended set — so Angular's own migration and its own
      // lint rule disagree by design.
      //
      // Disabled deliberately and temporarily. Adopting OnPush is a real
      // behavioral change that needs a per-component audit and e2e/manual QA;
      // it is tracked in #128, which also removes this override.
      '@angular-eslint/prefer-on-push-component-change-detection': 'off',
      '@angular-eslint/directive-selector': [
        'error',
        {
          type: 'attribute',
          prefix: 'jh',
          style: 'camelCase',
        },
      ],
      '@angular-eslint/component-selector': [
        'error',
        {
          type: 'element',
          prefix: 'jh',
          style: 'kebab-case',
        },
      ],
    },
  },
  {
    files: ['**/*.html'],
    // Override or add rules here
    rules: {},
  },
];
