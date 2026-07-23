const USER_KEY = 'userKey'
const USER_NAME = 'userName'

export const userKey = () => localStorage.getItem(USER_KEY)
export const userName = () => localStorage.getItem(USER_NAME)
export const isLoggedIn = () => Boolean(userKey())

export async function api(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...options.headers }
  const key = userKey()
  if (key) headers['X-User-Key'] = key

  const res = await fetch(path, { ...options, headers })
  if (!res.ok) {
    const body = await res.json().catch(() => null)
    throw new Error(body?.Error ?? body?.error ?? `${res.status} ${res.statusText}`)
  }
  if (res.status === 204) return null
  return res.json()
}

export const get = (path) => api(path)
export const post = (path, body) => api(path, { method: 'POST', body: JSON.stringify(body) })

export async function login(email) {
  const user = await post('/api/user/login', { Email: email })
  localStorage.setItem(USER_KEY, user.userKey)
  localStorage.setItem(USER_NAME, user.name)
  return user
}

export function logout() {
  localStorage.removeItem(USER_KEY)
  localStorage.removeItem(USER_NAME)
}
