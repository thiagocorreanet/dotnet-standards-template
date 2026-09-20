import { mkdirSync, existsSync, writeFileSync, readFileSync, readdirSync } from 'node:fs';
import { randomBytes, randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const target = resolve(root, '.local');
if (existsSync(resolve(root, '.env'))) throw new Error('.env já existe; não será sobrescrito. Preserve as credenciais dos volumes existentes.');
mkdirSync(target, { recursive: true, mode: 0o700 });
const secret = () => randomBytes(24).toString('hex');
const values = {
  COMPOSE_PROJECT_NAME: readdirSync(resolve(root, 'api')).find(x => x.endsWith('.slnx')).replace('.slnx', '').toLowerCase().replace(/[^a-z0-9-]/g, '-') + '-local',
  POSTGRES_PASSWORD: secret(), MIGRATOR_PASSWORD: secret(), RUNTIME_PASSWORD: secret(),
  IDENTITY_DB_PASSWORD: secret(), KEYCLOAK_ADMIN_PASSWORD: secret(), GRAFANA_PASSWORD: secret(),
  DEV_ADMIN_PASSWORD: secret(), DEV_ADMIN_SUBJECT: randomUUID()
};
const env = Object.entries(values).map(([key, value]) => key + '=' + value).join('\n') + '\n';
writeFileSync(resolve(root, '.env'), env, { flag: 'wx', mode: 0o600 });
function output(name, value) { writeFileSync(resolve(target, name), value, { flag: 'wx', mode: 0o444 }); }
output('runtime-db', 'Host=postgres;Database=app;Username=api_runtime;Password=' + values.RUNTIME_PASSWORD + ';Include Error Detail=false');
output('migrator-db', 'Host=postgres;Database=app;Username=api_migrator;Password=' + values.MIGRATOR_PASSWORD + ';Include Error Detail=false');
output('grafana-password', values.GRAFANA_PASSWORD);
const realm = JSON.parse(readFileSync(resolve(root, 'infra/keycloak/realm.json'), 'utf8'));
realm.verifyEmail = false; // Ambiente isolado de desenvolvimento; produção exige SMTP e verificação.
realm.requiredActions = []; // O smoke local não automatiza configuração de MFA.
realm.users = [{
  id: values.DEV_ADMIN_SUBJECT, username: 'developer', enabled: true, emailVerified: true,
  firstName: 'Development', lastName: 'Account', email: 'developer@example.test',
  credentials: [{ type: 'password', value: values.DEV_ADMIN_PASSWORD, temporary: false }],
  clientRoles: { 'modular-api': ['Administrator'] }
}];
output('realm-local.json', JSON.stringify(realm, null, 2));
console.log('Ambiente local criado. Credenciais em .env (0600); não versionar. Usuário da API: developer.');
