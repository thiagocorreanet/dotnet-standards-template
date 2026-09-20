import { execFileSync } from 'node:child_process';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
let ready = false;
for (let attempt = 0; attempt < 120; attempt++) {
  try {
    const response = await fetch('http://127.0.0.1:8080/realms/modular-api/.well-known/openid-configuration', { signal: AbortSignal.timeout(2000) });
    if (response.ok) { ready = true; break; }
  } catch {}
  await new Promise(resolve => setTimeout(resolve, 1000));
}
if (!ready) throw new Error('Keycloak local não ficou pronto em dois minutos.');
// Somente fixture local: Keycloak pode gerar outro ID ao importar um realm.
// Produção provisiona o subject verificado via administração do IdP, nunca por e-mail.
const subject = execFileSync('docker', ['compose', '-f', 'compose.local.yaml', 'exec', '-T', 'postgres',
  'psql', '-U', 'postgres', '-d', 'identity_provider', '-Atc',
  "SELECT id FROM user_entity WHERE username='developer' AND realm_id=(SELECT id FROM realm WHERE name='modular-api')"],
  { cwd: root, encoding: 'utf8' }).trim();
if (!subject || subject.includes('\n')) throw new Error('Identidade local única não encontrada; aguarde o import do Keycloak.');
execFileSync('docker', ['compose', '-f', 'compose.local.yaml', '--profile', 'setup', 'run', '--rm', 'bootstrap'],
  { cwd: root, env: { ...process.env, DEV_ADMIN_SUBJECT: subject }, stdio: 'inherit' });
