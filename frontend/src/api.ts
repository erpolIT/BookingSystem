export async function mutateApi(url: string, options: RequestInit = {}): Promise<Response> {
  // Tokens are tied to the current identity. Fetch a fresh one after any login/logout.
  const tokenResponse = await fetch('/api/auth/csrf', {
    credentials: 'same-origin', cache: 'no-store', signal: AbortSignal.timeout(30000),
  })
  if (!tokenResponse.ok) throw new Error('Could not obtain CSRF token')
  const data: unknown = await tokenResponse.json()
  if (!data || typeof data !== 'object' || !('token' in data) || typeof data.token !== 'string')
    throw new Error('Invalid CSRF token response')
  const headers = new Headers(options.headers)
  headers.set('X-CSRF-TOKEN', data.token)
  return fetch(url, {
    ...options, method: options.method ?? 'POST', headers, credentials: 'same-origin',
    signal: options.signal ?? AbortSignal.timeout(30000),
  })
}
