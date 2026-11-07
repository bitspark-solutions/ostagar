'use strict'

module.exports = {
  meta: { name: 'eslint-plugin-garments', version: '0.0.0' },
  rules: {
    'no-direct-api-access': require('./no-direct-api-access.cjs'),
    'no-browser-storage': require('./no-browser-storage.cjs'),
    'no-hardcoded-api-origin': require('./no-hardcoded-api-origin.cjs'),
    'no-math-abs': require('./no-math-abs.cjs'),
    'no-hardcoded-currency-or-locale': require('./no-hardcoded-currency-or-locale.cjs'),
    'no-mock-in-production-path': require('./no-mock-in-production-path.cjs'),
    'no-inline-color-literal': require('./no-inline-color-literal.cjs'),
    'no-hardcoded-ui-string': require('./no-hardcoded-ui-string.cjs'),
    'one-component-per-file': require('./one-component-per-file.cjs'),
    'one-type-per-file': require('./one-type-per-file.cjs'),
    'file-name-matches-declaration': require('./file-name-matches-declaration.cjs'),
    'no-barrel-export': require('./no-barrel-export.cjs'),
  },
}
