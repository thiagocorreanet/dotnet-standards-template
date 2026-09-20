import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { resolve, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
if (!process.argv[2]) throw new Error('Informe um diretório novo contendo somente os resultados desta execução.');
const directory = resolve(process.argv[2]);
function reports(path) {
  return readdirSync(path, { withFileTypes: true }).flatMap(entry => {
    if (entry.name === 'In') return []; // TRX copia os mesmos anexos; não contar outra execução.
    const item = resolve(path, entry.name);
    return entry.isDirectory() ? reports(item) : entry.name === 'coverage.json' ? [item] : [];
  });
}
const inputs = reports(directory);
if (inputs.length !== 4) throw new Error('Esperados quatro relatórios, um por suíte. Recebidos: ' + inputs.length);
const files = new Map();
for (const input of inputs) {
  for (const [assembly, documents] of Object.entries(JSON.parse(readFileSync(input, 'utf8')))) {
    const module = basename(assembly).replace(/\.dll$/, '');
    for (const [document, classes] of Object.entries(documents)) {
      const file = document.replaceAll('\\', '/').split('/api/src/')[1];
      if (!file) throw new Error('Documento de cobertura fora de api/src: ' + document);
      const key = module + '/' + file;
      const entry = files.get(key) ?? { module, file, lines: new Map(), branches: new Map() };
      files.set(key, entry);
      for (const [type, methods] of Object.entries(classes)) {
        for (const [method, coverage] of Object.entries(methods)) {
          for (const [line, hits] of Object.entries(coverage.Lines ?? {}))
            entry.lines.set(line, (entry.lines.get(line) ?? false) || hits > 0);
          for (const branch of coverage.Branches ?? []) {
            const branchKey = JSON.stringify([type, method, branch.Line, branch.Offset, branch.EndOffset, branch.Path, branch.Ordinal]);
            entry.branches.set(branchKey, (entry.branches.get(branchKey) ?? false) || branch.Hits > 0);
          }
        }
      }
    }
  }
}
function summarize(entries) {
  const lines = entries.flatMap(e => [...e.lines.values()]);
  const branches = entries.flatMap(e => [...e.branches.values()]);
  const lineHits = lines.filter(Boolean).length, branchHits = branches.filter(Boolean).length;
  return { lines: lines.length, coveredLines: lineHits, branches: branches.length, coveredBranches: branchHits,
    linePercent: lines.length ? 100 * lineHits / lines.length : 0,
    branchPercent: branches.length ? 100 * branchHits / branches.length : 100 };
}
const entries = [...files.values()];
const summary = {
  total: summarize(entries),
  assemblies: Object.fromEntries([...new Set(entries.map(e => e.module))].sort().map(module => [module, summarize(entries.filter(e => e.module === module))])),
  files: Object.fromEntries(entries.map(e => [e.file, summarize([e])]))
};
writeFileSync(resolve(directory, 'summary.json'), JSON.stringify(summary, null, 2));
const policy = JSON.parse(readFileSync(resolve(root, 'api/coverage-policy.json'), 'utf8'));
const failures = [];
function check(name, actual, minimum) {
  if (!actual?.lines || actual.linePercent < minimum.linePercent || actual.branchPercent < minimum.branchPercent)
    failures.push(name + ': cobertura insuficiente ou ausente');
}
check('total', summary.total, policy.total);
for (const [file, minimum] of Object.entries(policy.files)) check(file, summary.files[file], minimum);
for (const [module, result] of Object.entries(summary.assemblies))
  console.log(module + ': linhas=' + result.linePercent.toFixed(2) + '% branches=' + result.branchPercent.toFixed(2) + '%');
console.log('Total: linhas=' + summary.total.linePercent.toFixed(2) + '% branches=' + summary.total.branchPercent.toFixed(2) + '%');
if (failures.length) throw new Error(failures.join('; '));
console.log('PASS: cobertura unificada por linha/branch, sem duplicar as quatro suítes; gates atendidos.');
