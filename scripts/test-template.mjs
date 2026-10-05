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
// Pacotes Shared.* num feed em pasta. Versão única por execução: o cache global do NuGet nunca devolve um pacote antigo.
const packagedVersion = /<VersionPrefix>([^<]+)<\/VersionPrefix>/.exec(readFileSync(resolve(root, 'api/src/shared/Directory.Build.props'), 'utf8'))[1];
const templateVersion = JSON.parse(readFileSync(resolve(root, '.template.config/template.json'), 'utf8')).symbols.sharedPackagesVersion.defaultValue;
if (packagedVersion !== templateVersion) throw new Error(`Versão dos pacotes (${packagedVersion}) difere do padrão do template (${templateVersion}).`);
const feed = resolve(scratch, 'feed');
const version = packagedVersion + '-templatetest.' + Date.now();
execFileSync(process.execPath, [resolve(root, 'scripts/pack-shared.mjs'), feed, version], { cwd: root, stdio: 'inherit' });
const packageArgs = ['--packageFeed', feed, '--sharedPackagesVersion', version];
const packaged = ['Kernel', 'Contracts', 'Data', 'Http', 'Messaging', 'Observability', 'WebHost'].map(l => 'Shared.' + l);
function textFiles(dir) {
  return readdirSync(dir).flatMap(entry => {
    if (['bin', 'obj', 'node_modules', 'artifacts', '.git'].includes(entry)) return [];
    const path = resolve(dir, entry);
    return statSync(path).isDirectory() ? textFiles(path) : /\.(cs|csproj|props|targets|config|json|md|ya?ml|slnx|mjs)$|Dockerfile$/.test(entry) ? [path] : [];
  });
}
function checkPackageConsumption(output, name) {
  for (const library of packaged)
    if (existsSync(resolve(output, 'api/src/shared', library))) throw new Error('Biblioteca copiada como código em vez de pacote: ' + library);
  if (!readFileSync(resolve(output, 'api/Directory.Build.props'), 'utf8').includes('>true</UseSharedPackages>')) throw new Error('UseSharedPackages não ligado no projeto gerado.');
  if (!readFileSync(resolve(output, 'api/nuget.config'), 'utf8').includes(feed)) throw new Error('nuget.config sem o feed informado.');
  if (readFileSync(resolve(output, 'api', name + '.slnx'), 'utf8').includes('Shared.Kernel')) throw new Error('Solução gerada ainda lista Shared.Kernel.');
  // O ID dos pacotes não pode entrar na troca de nome do projeto (sourceName).
  const leaked = textFiles(output).filter(file => readFileSync(file, 'utf8').includes(name + '.Shared.'));
  if (leaked.length) throw new Error('Prefixo dos pacotes trocado pelo nome do projeto em: ' + leaked.map(f => relative(output, f)).join(', '));
}
run(['new', 'install', root, '--debug:custom-hive', hive]);
for (const example of [false, true]) {
  const name = example ? 'ExampleProof' : 'CoreProof';
  const output = resolve(scratch, name);
  run(['new', 'modular-api', '-n', name, '-o', output, '--includeExample', String(example), ...packageArgs, '--debug:custom-hive', hive]);
  checkPackageConsumption(output, name);
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

// Geradores: projeto núcleo + módulo + um comando e uma consulta, sem nenhuma edição manual.
const generated = resolve(scratch, 'GeneratorProof');
const generatedApi = resolve(generated, 'api');
run(['new', 'modular-api', '-n', 'GeneratorProof', '-o', generated, ...packageArgs, '--debug:custom-hive', hive]);
run(['new', 'modular-module', '-n', 'InvoiceManagement', '--debug:custom-hive', hive], generatedApi);
run(['new', 'modular-usecase', '-n', 'CreateInvoice', '--module', 'InvoiceManagement', '--command', '--debug:custom-hive', hive], generatedApi);
run(['new', 'modular-usecase', '-n', 'GetInvoice', '--module', 'InvoiceManagement', '--debug:custom-hive', hive], generatedApi);
// Os post-actions usam continueOnError: confirme que cada registro aconteceu.
for (const file of ['GeneratorProof.slnx', 'src/hosts/Host.Api/Host.Api.csproj', 'tests/Tests.Unit/Tests.Unit.csproj', 'tests/Tests.Architecture/Tests.Architecture.csproj'])
  if (!readFileSync(resolve(generatedApi, file), 'utf8').includes('Module.InvoiceManagement')) throw new Error('Gerador de módulo não registrou o módulo em ' + file);
for (const file of ['src/modules/Module.InvoiceManagement/UseCases/CreateInvoice/CreateInvoiceAccessPolicy.cs',
  'src/modules/Module.InvoiceManagement/UseCases/GetInvoice/GetInvoiceEndpoint.cs', 'tests/Tests.Unit/InvoiceManagement/CreateInvoiceTests.cs'])
  if (!existsSync(resolve(generatedApi, file))) throw new Error('Gerador de caso de uso não criou ' + file);
run(['restore', 'GeneratorProof.slnx', '--nologo', '--verbosity', 'quiet'], generatedApi);
const build = execFileSync('dotnet', ['build', 'GeneratorProof.slnx', '--no-restore', '--nologo', '--verbosity', 'quiet'], { cwd: generatedApi, encoding: 'utf8' });
const generatedWarnings = build.split('\n').filter(line => /warning/.test(line) && /InvoiceManagement/.test(line));
if (generatedWarnings.length) throw new Error('Código gerado com avisos:\n' + [...new Set(generatedWarnings)].join('\n'));
run(['test', 'GeneratorProof.slnx', '--no-build', '--nologo', '--verbosity', 'quiet', '-clp:ErrorsOnly'], generatedApi);
console.log('PASS: template genérico, exemplo e geradores, consumindo pacotes ' + version + '. Diretório isolado preservado para inspeção: ' + scratch);
