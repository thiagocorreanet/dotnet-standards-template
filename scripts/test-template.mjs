import { execFileSync } from 'node:child_process';
import { mkdtempSync, existsSync, readdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
if (!existsSync(resolve(root, '.template.config/template.json'))) throw new Error('Execute no repositório do template, não em um projeto gerado.');
const scratch = mkdtempSync(resolve(tmpdir(), 'modular-api-template-test-'));
const hive = resolve(scratch, 'hive');
function run(args, cwd = root) { execFileSync('dotnet', args, { cwd, stdio: 'inherit' }); }
run(['new', 'install', root, '--debug:custom-hive', hive]);
for (const example of [false, true]) {
  const name = example ? 'ExampleProof' : 'CoreProof';
  const output = resolve(scratch, name);
  run(['new', 'modular-api', '-n', name, '-o', output, '--includeExample', String(example), '--debug:custom-hive', hive]);
  for (const forbidden of ['.env', '.local', '.secrets', 'artifacts']) if (existsSync(resolve(output, forbidden))) throw new Error('Artefato privado exportado: ' + forbidden);
  const modules = readdirSync(resolve(output, 'api/src/modules'));
  if (modules.length !== (example ? 6 : 2)) throw new Error('Conjunto de módulos inesperado.');
  const results = resolve(output, 'artifacts/coverage/generated');
  run(['test', name + '.slnx', '--nologo', '--verbosity', 'quiet', '-clp:ErrorsOnly', '--logger', 'trx',
    '--collect:XPlat Code Coverage', '--settings', 'coverage.runsettings', '--results-directory', results], resolve(output, 'api'));
  execFileSync(process.execPath, [resolve(output, 'scripts/check-coverage.mjs'), results], { cwd: output, stdio: 'inherit' });
}
console.log('PASS: template genérico e exemplo. Diretório isolado preservado para inspeção: ' + scratch);
