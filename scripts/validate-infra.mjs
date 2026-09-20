import { execFileSync } from 'node:child_process';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const fixture = mkdtempSync(resolve(tmpdir(), 'modular-api-config-'));
writeFileSync(resolve(fixture, 'telemetry_endpoint'), 'https://telemetry.example.test', { mode: 0o444 });
writeFileSync(resolve(fixture, 'telemetry_auth'), 'Bearer synthetic-validation-only', { mode: 0o444 });
execFileSync('openssl', ['req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-keyout', resolve(fixture, 'tls_key'), '-out', resolve(fixture, 'tls_cert'), '-days', '1', '-subj', '/CN=api.example.test'], { stdio: 'ignore' });
function docker(args) { execFileSync('docker', ['run', '--rm', '--network', 'none', ...args], { stdio: 'inherit' }); }
docker(['-v', root + '/infra/observability:/rules:ro', '--entrypoint', '/bin/promtool', 'prom/prometheus:v3.14.0', 'test', 'rules', '/rules/alerts.test.yaml']);
docker(['-v', root + '/infra/observability/collector.yaml:/etc/collector.yaml:ro', 'otel/opentelemetry-collector-contrib:0.161.0', 'validate', '--config=/etc/collector.yaml']);
docker(['-v', root + '/infra/production/collector.yaml:/etc/collector.yaml:ro', '-v', fixture + '/telemetry_endpoint:/run/secrets/telemetry_endpoint:ro', '-v', fixture + '/telemetry_auth:/run/secrets/telemetry_auth:ro', 'otel/opentelemetry-collector-contrib:0.161.0', 'validate', '--config=/etc/collector.yaml']);
docker(['-v', root + '/infra/production/Caddyfile:/etc/caddy/Caddyfile:ro', '-v', fixture + ':/run/secrets:ro', '-e', 'API_DOMAIN=api.example.test', 'caddy@sha256:834468128c7696cec0ceea6172f7d692daf645ae51983ca76e39da54a97c570d', 'caddy', 'validate', '--config', '/etc/caddy/Caddyfile', '--adapter', 'caddyfile']);
console.log('PASS: regras exercitadas, Collector produtivo e Caddy TLS validados. Certificado sintético em diretório temporário privado.');
