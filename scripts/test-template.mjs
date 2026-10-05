import { execFileSync } from 'node:child_process';
import { mkdtempSync, existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
if (!existsSync(resolve(root, '.template.config/template.json'))) throw new Error('Execute no repositório do template, não em um projeto gerado.');
const scratch = mkdtempSync(resolve(tmpdir(), 'modular-api-template-test-'));
const hive = resolve(scratch, 'hive');
function run(args, cwd = root) { execFileSync('dotnet', args, { cwd, stdio: 'inherit' }); }
function markdownFiles(dir) {
  return readdirSync(dir).flatMap(name => {
    if (['bin', 'obj', 'node_modules', 'artifacts', '.git'].includes(name)) return [];
    const path = resolve(dir, name);
    return statSync(path).isDirectory() ? markdownFiles(path) : name.endsWith('.md') ? [path] : [];
  });
}
// Links relativos em Markdown precisam apontar para arquivo ou pasta existente no projeto gerado.
function checkMarkdown(output) {
  const broken = [];
  for (const file of markdownFiles(output)) {
    const text = readFileSync(file, 'utf8');
    if (text.includes('<!--#if') || text.includes('<!--#endif')) broken.push(relative(output, file) + ': bloco condicional não processado');
    // Blocos de código podem conter "](...)" que não é link.
    for (const [, target] of text.replace(/```[\s\S]*?```/g, '').matchAll(/\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g)) {
      if (/^(https?:|mailto:|#)/.test(target)) continue;
      const path = decodeURIComponent(target.split('#')[0]);
      if (!existsSync(resolve(dirname(file), path))) broken.push(relative(output, file) + ' -> ' + target);
    }
  }
  if (broken.length) throw new Error('Links Markdown quebrados no projeto gerado:\n' + broken.join('\n'));
}
run(['new', 'install', root, '--debug:custom-hive', hive]);
for (const example of [false, true]) {
  const name = example ? 'ExampleProof' : 'CoreProof';
  const output = resolve(scratch, name);
  run(['new', 'modular-api', '-n', name, '-o', output, '--includeExample', String(example), '--debug:custom-hive', hive]);
  for (const forbidden of ['.env', '.local', '.secrets', 'artifacts']) if (existsSync(resolve(output, forbidden))) throw new Error('Artefato privado exportado: ' + forbidden);
  for (const history of ['docs/implementation-status.md', 'docs/technical-review.md', 'docs/corrections-review.md', 'docs/architecture-review.md', 'docs/reference'])
    if (existsSync(resolve(output, history))) throw new Error('Histórico do repositório de origem exportado: ' + history);
  checkMarkdown(output);
  const modules = readdirSync(resolve(output, 'api/src/modules'));
  if (modules.length !== (example ? 6 : 2)) throw new Error('Conjunto de módulos inesperado.');
  const results = resolve(output, 'artifacts/coverage/generated');
  run(['test', name + '.slnx', '--nologo', '--verbosity', 'quiet', '-clp:ErrorsOnly', '--logger', 'trx',
    '--collect:XPlat Code Coverage', '--settings', 'coverage.runsettings', '--results-directory', results], resolve(output, 'api'));
  execFileSync(process.execPath, [resolve(output, 'scripts/check-coverage.mjs'), results], { cwd: output, stdio: 'inherit' });
}
console.log('PASS: template genérico e exemplo. Diretório isolado preservado para inspeção: ' + scratch);
