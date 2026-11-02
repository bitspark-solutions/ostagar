import MockAdapter from 'axios-mock-adapter'
import { apiBaseUrl } from '@/api/ApiBaseUrl'
import { ApiError } from '@/api/ApiError'
import { apiClient } from '@/api/ApiClient'
import { accessTokenStore } from '@/api/AccessTokenStore'
import { toApiError } from '@/api/ToApiError'
import { signIn } from '@/auth/SignIn'

const TEST_ORIGIN = 'http://api.invalid'

describe('L12 single env-driven API client', () => {
  let transport: MockAdapter

  beforeEach(() => {
    process.env.NEXT_PUBLIC_API_BASE_URL = TEST_ORIGIN
    accessTokenStore.clear()
    transport = new MockAdapter(apiClient)
  })

  afterEach(() => {
    transport.restore()
    delete process.env.NEXT_PUBLIC_API_BASE_URL
  })

  it('refuses to guess an origin when the environment is not set', () => {
    delete process.env.NEXT_PUBLIC_API_BASE_URL

    expect(() => {
      apiBaseUrl()
    }).toThrow(ApiError)
  })

  it('rejects a relative origin instead of defaulting to a dev host', () => {
    process.env.NEXT_PUBLIC_API_BASE_URL = '/api'

    expect(() => {
      apiBaseUrl()
    }).toThrow(/absolute/)
  })

  it('sends the stored token on every subsequent request through the one interceptor', async () => {
    transport.onPost('/api/v1/auth/login').reply(200, { accessToken: 'first-token' })
    transport.onGet('/api/v1/ping').reply(200, {})

    await expect(signIn({ tenantSlug: 'acme', email: 'owner@factory.bd', password: 'issued-by-p1' })).resolves.toBe(true)
    await apiClient.get('/api/v1/ping')

    const login = transport.history.post[0]
    const followUp = transport.history.get[0]

    if (!login || !followUp) {
      throw new Error('Expected the mock transport to record both the login and the follow-up request')
    }

    expect(login.baseURL).toBe(TEST_ORIGIN)
    expect(followUp.headers?.Authorization).toBe('Bearer first-token')
  })

  it('reports "not signed in" when the server hands back no token', async () => {
    transport.onPost('/api/v1/auth/login').reply(200, {})

    await expect(signIn({ tenantSlug: 'acme', email: 'owner@factory.bd', password: 'issued-by-p1' })).resolves.toBe(false)
    expect(accessTokenStore.read()).toBeNull()
  })

  it('maps transport failures onto kinds the UI can translate', async () => {
    transport.onPost('/api/v1/auth/login').reply(401, {})

    const unauthorized = await signIn({ tenantSlug: 'acme', email: 'owner@factory.bd', password: 'wrong' }).catch((error: unknown) => error)

    expect(unauthorized).toBeInstanceOf(ApiError)
    expect((unauthorized as ApiError).kind).toBe('unauthorized')

    transport.onPost('/api/v1/auth/login').reply(500, {})

    const rejected = await signIn({ tenantSlug: 'acme', email: 'owner@factory.bd', password: 'x' }).catch((error: unknown) => error)

    expect((rejected as ApiError).status).toBe(500)
  })

  it('never invents a value for an error it cannot classify', () => {
    expect(toApiError(new Error('boom')).kind).toBe('network')
  })
})
