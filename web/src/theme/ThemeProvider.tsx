'use client'

import createCache from '@emotion/cache'
import { CacheProvider } from '@emotion/react'
import CssBaseline from '@mui/material/CssBaseline'
import { ThemeProvider as MuiThemeProvider } from '@mui/material/styles'
import { useState } from 'react'
import type { ReactNode } from 'react'
import { appTheme } from './AppTheme'

// Emotion styles are inserted on the client in P0. SSR style streaming is wired up with the
// design system at P4, where there is a real stylesheet budget to protect.
export function ThemeProvider({ children }: { readonly children: ReactNode }) {
  const [cache] = useState(() => createCache({ key: 'ostagar', prepend: true }))

  return (
    <CacheProvider value={cache}>
      <MuiThemeProvider theme={appTheme}>
        <CssBaseline />
        {children}
      </MuiThemeProvider>
    </CacheProvider>
  )
}
