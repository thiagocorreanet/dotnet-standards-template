import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const fixture = process.argv.includes('--fixture');
const envFile = fixture ? 'infra/production/environment.example' : '.env.production';
const env = { ...process.env };
if (fixture) for (const key of ['API_IMAGE', 'INGRESS_IMAGE', 'COLLECTOR_IMAGE']) env[key] = 'example.test/fixture@sha256:' + '0'.repeat(64);
const cfg = JSON.parse(execFileSync('docker', ['compose', '--env-file', envFile, '-f', 'compose.production.yaml', 'config', '--format', 'json'], { cwd: root, env, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }));
function require(value, message) { if (!value) throw new Error(message); }
const { api, migrate, collector, ingress } = cfg.services;
require(Object.keys(cfg.services).length === 4, 'Manifesto contém serviços não revisados.');
for (const [name, service] of Object.entries(cfg.services)) {
  require(/@sha256:[a-f0-9]{64}$/.test(service.image), 'Imagem sem digest: ' + name);
  require(service.read_only === true && service.cap_drop.includes('ALL'), 'Hardening ausente: ' + name);
  if (name !== 'ingress') require(!service.ports?.length, 'Porta interna publicada: ' + name);
}
require(ingress.ports.length === 1 && String(ingress.ports[0].published) === '443', 'Somente 443 deve ser publicada.');
require(api.environment.ASPNETCORE_ENVIRONMENT === 'Production', 'Ambiente incorreto.');
require(api.environment.Database__MigrateOnStartup === 'false' && api.environment.OpenApi__Enabled === 'false', 'Flags inseguras.');
require(api.environment.Oidc__RequireHttpsMetadata === 'true' && api.environment.Oidc__Authority.startsWith('https://'), 'OIDC deve usar HTTPS.');
require(api.environment.Cors__AllowedOrigins__0.startsWith('https://'), 'Frontend deve usar HTTPS.');
require(api.environment.ReverseProxy__KnownProxies__0 === ingress.networks.application.ipv4_address, 'Proxy confiável divergente.');
require(cfg.secrets['runtime-db'].file !== cfg.secrets['migrator-db'].file, 'Separe credenciais DML/DDL.');
if (!fixture) {
  for (const name of ['runtime-db', 'migrator-db']) {
    const secret = readFileSync(cfg.secrets[name].file, 'utf8');
    require(/SSL\s*Mode\s*=\s*VerifyFull/i.test(secret), 'PostgreSQL externo exige SSL Mode=VerifyFull: ' + name);
    require(!/Include\s*Error\s*Detail\s*=\s*true|Log\s*Parameters\s*=\s*true/i.test(secret), 'Detalhes sensíveis habilitados.');
  }
  const endpoint = readFileSync(cfg.secrets.telemetry_endpoint.file, 'utf8').trim();
  require(endpoint.startsWith('https://'), 'Backend OTLP externo deve usar TLS.');
}
console.log('PASS: manifesto de produção isolado, imagens fixas, porta 443, segredos separados, OIDC HTTPS e proxy explícito.' + (fixture ? ' Validação estática com valores sintéticos; não é deploy.' : ''));
