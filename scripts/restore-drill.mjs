import { execFileSync, spawn } from 'node:child_process';
import { randomBytes, randomUUID, createHash } from 'node:crypto';
import { mkdirSync, createWriteStream, createReadStream, writeFileSync } from 'node:fs';
import { pipeline } from 'node:stream/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const directory = resolve(root, 'artifacts/backups/' + new Date().toISOString().replaceAll(':', '-'));
mkdirSync(directory, { recursive: true, mode: 0o700 });
const name = 'modular-api-restore-' + randomUUID().slice(0, 8);
const image = 'postgres:17.11-alpine';
const compose = ['compose', '-f', 'compose.local.yaml', 'exec', '-T', 'postgres'];
const run = args => execFileSync('docker', args, { cwd: root, encoding: 'utf8', stdio: ['pipe', 'pipe', 'pipe'] }).trim();
const schemaQuery = "SELECT schemaname,tablename FROM pg_tables WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY 1,2";
const quote = name => '"' + name.replaceAll('"', '""') + '"';
function inventory(prefix, database) {
  const tables = run([...prefix, 'psql', '-U', 'postgres', '-d', database, '-Atc', schemaQuery]).split('\n').filter(Boolean);
  return Object.fromEntries(tables.map(row => { const [schema, table] = row.split('|'); return [row, Number(run([...prefix, 'psql', '-U', 'postgres', '-d', database, '-Atc', 'SELECT count(*) FROM ' + quote(schema) + '.' + quote(table)]))]; }));
}
function completed(child) { return new Promise((resolve, reject) => { child.on('error', reject); child.on('exit', code => code === 0 ? resolve() : reject(new Error('Ferramenta PostgreSQL falhou: ' + code))); }); }
const manifest = { at: new Date().toISOString(), image, databases: {} };
let created = false;
try {
  execFileSync('docker', ['run', '--detach', '--name', name, '--network', 'none', '-e', 'POSTGRES_PASSWORD', image],
    { env: { ...process.env, POSTGRES_PASSWORD: randomBytes(32).toString('hex') }, stdio: 'pipe' });
  created = true;
  let ready = false;
  for (let i = 0; i < 60; i++) {
    try { run(['exec', name, 'pg_isready', '-U', 'postgres']); ready = true; break; } catch { await new Promise(r => setTimeout(r, 1000)); }
  }
  if (!ready) throw new Error('Banco isolado não ficou pronto.');
  for (const database of ['app', 'identity_provider']) {
    const before = inventory(compose, database);
    const filename = resolve(directory, database + '.dump');
    const dump = spawn('docker', [...compose, 'pg_dump', '-U', 'postgres', '-Fc', database], { cwd: root, stdio: ['ignore', 'pipe', 'inherit'] });
    await Promise.all([completed(dump), pipeline(dump.stdout, createWriteStream(filename, { flags: 'wx', mode: 0o600 }))]);
    run(['exec', name, 'createdb', '-U', 'postgres', database]);
    const restore = spawn('docker', ['exec', '-i', name, 'pg_restore', '-U', 'postgres', '-d', database, '--no-owner', '--no-privileges', '--exit-on-error'], { stdio: ['pipe', 'ignore', 'inherit'] });
    await Promise.all([completed(restore), pipeline(createReadStream(filename), restore.stdin)]);
    const after = inventory(['exec', name], database);
    if (JSON.stringify(before) !== JSON.stringify(after)) throw new Error('Contagens mudaram: pare as escritas e repita o ensaio.');
    const digest = createHash('sha256');
    for await (const chunk of createReadStream(filename)) digest.update(chunk);
    manifest.databases[database] = { sha256: digest.digest('hex'), tables: after };
  }
  writeFileSync(resolve(directory, 'manifest.json'), JSON.stringify(manifest, null, 2), { flag: 'wx', mode: 0o600 });
  console.log('PASS: restore isolado de aplicação e Keycloak; contagens e checksums em ' + directory);
} finally {
  if (created) execFileSync('docker', ['rm', '--force', name], { stdio: 'ignore' });
}
// Ensaio local. Backups permanecem privados e sem criptografia; produção exige KMS/cópia externa.
// Somente o container temporário criado neste processo é removido. Nenhum volume de origem é apagado.
