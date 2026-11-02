'use client'

import { useState } from 'react'
import { useRouter } from 'next/navigation'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import CircularProgress from '@mui/material/CircularProgress'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { presentApiError } from '@/api/PresentApiError'
import { toApiError } from '@/api/ToApiError'
import { signIn } from '@/auth/SignIn'
import { designTokens } from '@/theme/Tokens'
import { useTranslations } from '@/i18n/UseTranslations'
import { ErrorState } from './ErrorState'
import type { FormEvent } from 'react'
import type { ApiErrorPresentation } from '@/api/ApiErrorPresentation'
import type { LoginCredentials } from '@/auth/LoginCredentials'
import type { MessageKey } from '@/i18n/MessageKey'

const dashboardPath = '/admin/dashboard'

const noCredentials: LoginCredentials = { tenantSlug: '', email: '', password: '' }

export function LoginPanel() {
  const t = useTranslations()
  const router = useRouter()
  const [credentials, setCredentials] = useState<LoginCredentials>(noCredentials)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [failure, setFailure] = useState<ApiErrorPresentation | null>(null)
  const [validationKey, setValidationKey] = useState<MessageKey | null>(null)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (
      credentials.tenantSlug.length === 0 ||
      credentials.email.length === 0 ||
      credentials.password.length === 0
    ) {
      setValidationKey('login.credentialsRequired')
      setFailure(null)
      return
    }

    setValidationKey(null)
    setFailure(null)
    setIsSubmitting(true)

    try {
      const signedIn = await signIn(credentials)

      if (signedIn) {
        router.push(dashboardPath)
      }
    } catch (error) {
      setFailure(presentApiError(toApiError(error)))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Box
      component="form"
      onSubmit={submit}
      sx={{
        display: 'grid',
        gap: designTokens.space.lg,
        width: '100%',
        maxWidth: designTokens.layout.formWidth,
      }}
    >
      <Box>
        <Typography variant="h1" component="h1">
          {t('login.title')}
        </Typography>
        <Typography variant="body2" sx={{ color: designTokens.color.textSecondary }}>
          {t('login.subtitle')}
        </Typography>
      </Box>

      <TextField
        label={t('login.tenant')}
        autoComplete="organization"
        value={credentials.tenantSlug}
        onChange={(event) => {
          setCredentials((current) => ({ ...current, tenantSlug: event.target.value.trim() }))
        }}
      />
      <TextField
        label={t('login.email')}
        type="email"
        autoComplete="username"
        value={credentials.email}
        onChange={(event) => {
          setCredentials((current) => ({ ...current, email: event.target.value }))
        }}
      />
      <TextField
        label={t('login.password')}
        type="password"
        autoComplete="current-password"
        value={credentials.password}
        onChange={(event) => {
          setCredentials((current) => ({ ...current, password: event.target.value }))
        }}
      />

      {validationKey !== null && (
        <Typography variant="body2" sx={{ color: designTokens.color.stateDanger }}>
          {t(validationKey)}
        </Typography>
      )}
      {failure !== null && <ErrorState messageKey={failure.messageKey} values={failure.values} />}

      <Box sx={{ display: 'flex', alignItems: 'center', gap: designTokens.space.md }}>
        <Button type="submit" variant="contained" disabled={isSubmitting}>
          {isSubmitting ? t('login.submitting') : t('login.submit')}
        </Button>
        {isSubmitting && <CircularProgress size={designTokens.space.lg} />}
      </Box>
    </Box>
  )
}
