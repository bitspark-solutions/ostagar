import coreWebVitals from 'eslint-config-next/core-web-vitals'
import garments from './rules/OstagarPlugin.cjs'

const lawRules = {
  'garments/no-direct-api-access': ['error', { allow: ['src/api/'] }],
  'garments/no-browser-storage': ['error', { allow: ['src/api/'] }],
  'garments/no-hardcoded-api-origin': ['error', { allow: ['src/api/ApiBaseUrl.ts'] }],
  'garments/no-math-abs': 'error',
  'garments/no-hardcoded-currency-or-locale': ['error', { allow: ['src/i18n/'] }],
  'garments/no-mock-in-production-path': 'error',
  'garments/no-inline-color-literal': ['error', { allow: ['src/theme/Tokens.ts'] }],
  'garments/no-hardcoded-ui-string': 'error',
  'garments/one-component-per-file': 'error',
  'garments/one-type-per-file': 'error',
  'garments/file-name-matches-declaration': 'error',
  'garments/no-barrel-export': 'error',
}

export default [
  ...coreWebVitals,
  {
    files: ['**/*.{ts,tsx}'],
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
    },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: { garments },
    rules: lawRules,
  },
  {
    files: ['src/components/**/*.tsx', 'src/i18n/**/*.tsx'],
    rules: { 'react/no-unescaped-entities': 'off' },
  },
  {
    files: ['scripts/**/*.mjs', 'rules/**/*.cjs', 'eslint.config.mjs'],
    rules: {
      '@typescript-eslint/no-require-imports': 'off',
      'import/no-anonymous-default-export': 'off',
    },
  },
]
