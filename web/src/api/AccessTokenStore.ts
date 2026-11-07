const accessTokenKey = 'ostagar.accessToken'

export class AccessTokenStore {
  private readStorage(): Storage | null {
    return typeof window === 'undefined' ? null : window.localStorage
  }

  public read(): string | null {
    return this.readStorage()?.getItem(accessTokenKey) ?? null
  }

  public write(token: string): void {
    this.readStorage()?.setItem(accessTokenKey, token)
  }

  public clear(): void {
    this.readStorage()?.removeItem(accessTokenKey)
  }
}

export const accessTokenStore = new AccessTokenStore()
