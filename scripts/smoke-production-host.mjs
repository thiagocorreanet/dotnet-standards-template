import { execFileSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { request } from 'node:http';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
function docker(args) { return execFileSync('docker', args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim(); }
const compose = JSON.parse(docker(['compose', '-f', 'compose.local.yaml', 'config', '--format', 'json']));
const postgres = docker(['compose', '-f', 'compose.local.yaml', 'ps', '-q', 'postgres']);
const inspected = JSON.parse(docker(['inspect', postgres]))[0];
const network = Object.keys(inspected.NetworkSettings.Networks)[0];
const name = 'modular-api-production-probe-' + randomUUID().slice(0, 8);
function status(uri) {
  return new Promise((resolve, reject) => {
    const req = request(uri, { headers: { Host: 'api.example.test' }, timeout: 3000 }, response => {
      response.resume(); resolve(response.statusCode);
    });
    req.on('error', reject); req.on('timeout', () => req.destroy(new Error('Request timeout'))); req.end();
  });
}
let created = false;
try {
  docker(['run', '-d', '--name', name, '--network', network, '--read-only', '--tmpfs', '/tmp', '--cap-drop', 'ALL',
    '-p', '127.0.0.1::8080', '-e', 'ASPNETCORE_ENVIRONMENT=Production', '-e', 'AllowedHosts=api.example.test',
    '-e', 'Oidc__Authority=https://identity.example.test/realms/probe', '-e', 'Oidc__Audience=modular-api',
    '-v', root + '/.local/runtime-db:/run/secrets/ConnectionStrings__ModularApi:ro', compose.services.api.image]);
  created = true;
  const inspect = () => JSON.parse(docker(['inspect', name]))[0];
  const port = inspect().NetworkSettings.Ports['8080/tcp'][0].HostPort;
  let ready = false;
  for (let i=0;i<60;i++) {
    try { if (await status('http://127.0.0.1:' + port + '/health/ready') === 200) { ready=true; break; } } catch {}
    await new Promise(r => setTimeout(r,1000));
  }
  if (!ready) throw new Error('Host produtivo não ficou pronto.');
  docker(['exec', name, 'dotnet', 'Host.Api.dll', 'healthcheck']);
  for (const path of ['/scalar', '/scalar/v1', '/scalar/scalar.js', '/scalar/scalar.aspnetcore.js', '/scalar/favicon.svg', '/swagger/index.html', '/openapi/v1.json', '/openapi/v1.yaml']) {
    if (await status('http://127.0.0.1:' + port + path) !== 404) throw new Error('Documentação exposta em produção.');
  }
  console.log('PASS: imagem em Production, runtime DML, migrations aplicadas, healthcheck com Host permitido e OpenAPI desabilitada.');
} finally { if (created) docker(['rm', '--force', name]); }
// Prova local do host; não testa TLS externo, login produtivo nem HA.
