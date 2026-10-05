// Empacota as bibliotecas Shared.* genéricas (as de api/src/shared com IsPackable) em um diretório.
// Uso: node scripts/pack-shared.mjs <diretório-de-saída> [versão]
// Sem versão, usa a VersionPrefix de api/src/shared/Directory.Build.props. Não publica nada.
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, existsSync, mkdirSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const [output, version] = process.argv.slice(2);
if (!output) throw new Error('Informe o diretório de saída dos pacotes.');
const shared = resolve(root, 'api/src/shared');
const projects = readdirSync(shared, { withFileTypes: true })
  .filter(entry => entry.isDirectory())
  .map(entry => resolve(shared, entry.name, entry.name + '.csproj'))
  .filter(project => existsSync(project) && !/<IsPackable>\s*false\s*<\/IsPackable>/.test(readFileSync(project, 'utf8')));
if (projects.length === 0) throw new Error('Nenhuma biblioteca empacotável em api/src/shared.');
mkdirSync(resolve(output), { recursive: true });
for (const project of projects) {
  const args = ['pack', project, '-c', 'Release', '-o', resolve(output), '--nologo', '-v', 'q'];
  if (version) args.push('-p:Version=' + version);
  execFileSync('dotnet', args, { cwd: root, stdio: 'inherit' });
}
console.log(`PASS: ${projects.length} pacote(s) em ${resolve(output)}${version ? ' com versão ' + version : ''}.`);
