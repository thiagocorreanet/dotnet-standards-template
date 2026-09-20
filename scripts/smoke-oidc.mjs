import { readFileSync } from 'node:fs';
import { randomBytes, createHash } from 'node:crypto';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const env = Object.fromEntries(readFileSync(resolve(root, '.env'), 'utf8').trim().split('\n').filter(x => x && !x.startsWith('#')).map(x => { const i=x.indexOf('='); return [x.slice(0,i),x.slice(i+1)]; }));
const issuer = 'http://identity.localhost:8080/realms/modular-api';
const redirectUri = 'http://localhost:5173/callback';
const cookies = new Map();
async function identityFetch(uri, options = {}) {
  const url = new URL(uri);
  if (url.origin !== new URL(issuer).origin) throw new Error('Redirecionamento de identidade fora do ambiente local.');
  const host = url.host;
  url.host = '127.0.0.1:8080';
  const response = await fetch(url, { signal: AbortSignal.timeout(10000), ...options, redirect: 'manual',
    headers: { host, cookie: [...cookies].map(([k,v]) => k + '=' + v).join('; '), ...options.headers } });
  for (const cookie of response.headers.getSetCookie()) {
    const pair = cookie.split(';')[0], i = pair.indexOf('=');
    cookies.set(pair.slice(0,i), pair.slice(i+1));
  }
  return response;
}
function check(value, message) { if (!value) throw new Error(message); }
const verifier = randomBytes(48).toString('base64url');
const challenge = createHash('sha256').update(verifier).digest('base64url');
const state = randomBytes(24).toString('base64url');
const authorization = new URL(issuer + '/protocol/openid-connect/auth');
authorization.search = new URLSearchParams({
  client_id: 'modular-web', response_type: 'code', redirect_uri: redirectUri, scope: 'openid profile email',
  state, nonce: randomBytes(24).toString('base64url'), code_challenge: challenge, code_challenge_method: 'S256'
}).toString();
const page = await identityFetch(authorization);
const html = await page.text();
const action = html.match(/<form[^>]*action="([^"]+)"/i)?.[1]?.replaceAll('&amp;', '&');
check(page.status === 200 && action, 'Formulário de login não recebido; verifique o Keycloak.');
const login = await identityFetch(action, { method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ username: 'developer', password: env.DEV_ADMIN_PASSWORD, credentialId: '' }) });
const callback = new URL(login.headers.get('location') || issuer);
check(callback.origin + callback.pathname === redirectUri, 'Login não completou o fluxo de código.');
check(callback.searchParams.get('state') === state, 'State incorreto.');
check(callback.searchParams.get('iss') === issuer, 'Issuer de autorização incorreto.');
const tokenResponse = await identityFetch(issuer + '/protocol/openid-connect/token', {
  method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'authorization_code', client_id: 'modular-web', redirect_uri: redirectUri,
    code: callback.searchParams.get('code'), code_verifier: verifier })
});
check(tokenResponse.ok, 'Troca de código por token falhou.');
const tokens = await tokenResponse.json();
const headers = { authorization: 'Bearer ' + tokens.access_token };
const probeHeaders = {};
if (process.env.SMOKE_TRACEPARENT) {
  check(/^00-[a-f0-9]{32}-[a-f0-9]{16}-01$/.test(process.env.SMOKE_TRACEPARENT), 'Traceparent sintético inválido.');
  check(/^[a-zA-Z0-9_-]{1,64}$/.test(process.env.SMOKE_CORRELATION_ID || ''), 'Correlação sintética inválida.');
  probeHeaders.traceparent = process.env.SMOKE_TRACEPARENT;
  probeHeaders['X-Correlation-Id'] = process.env.SMOKE_CORRELATION_ID;
}
const query = process.env.SMOKE_PRIVATE_SENTINEL ? '?privacy_probe=' + encodeURIComponent(process.env.SMOKE_PRIVATE_SENTINEL) : '';
const me = await fetch('http://127.0.0.1:5761/api/v1/identity/users/me' + query,
  { headers: { ...headers, ...probeHeaders }, signal: AbortSignal.timeout(10000) });
check(me.ok, 'API rejeitou o access token real do Keycloak: ' + me.status);
const profile = await me.json();
check(profile.roles.includes('Administrator'), 'Perfil do client não foi mapeado.');
const outbox = await fetch('http://127.0.0.1:5761/api/v1/operations/outbox', { headers, signal: AbortSignal.timeout(10000) });
check(outbox.ok, 'Operação administrativa negada.');
const passwordGrant = await identityFetch(issuer + '/protocol/openid-connect/token', {
  method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ grant_type: 'password', client_id: 'modular-web', username: 'developer', password: env.DEV_ADMIN_PASSWORD })
});
check(!passwordGrant.ok, 'Password grant deveria estar desabilitado.');
const logout = await identityFetch(issuer + '/protocol/openid-connect/logout', {
  method: 'POST', headers: { 'content-type': 'application/x-www-form-urlencoded' },
  body: new URLSearchParams({ client_id: 'modular-web', refresh_token: tokens.refresh_token })
});
check(logout.ok, 'Logout do provedor falhou.');
console.log('PASS: Authorization Code + PKCE, audience, perfis do client, vínculo local, API protegida, password grant desabilitado e logout.');
// Tokens e senhas permanecem em memória e nunca são impressos nem persistidos.
