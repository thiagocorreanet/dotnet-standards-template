import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { randomBytes } from 'node:crypto';
import { execFileSync } from 'node:child_process';
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const env = Object.fromEntries(readFileSync(resolve(root, '.env'), 'utf8').trim().split('\n').map(x => { const i=x.indexOf('='); return [x.slice(0,i),x.slice(i+1)]; }));
const headers = { authorization: 'Basic ' + Buffer.from('operator:' + env.GRAFANA_PASSWORD).toString('base64') };
async function json(path) {
  const response = await fetch('http://127.0.0.1:3000/api/datasources/proxy/uid/' + path,
    { headers: { ...headers, accept: 'application/json' }, signal: AbortSignal.timeout(10000) });
  if (!response.ok) throw new Error('Destino de telemetria indisponível: ' + response.status);
  return response.json();
}
const query = expression => json('prometheus/api/v1/query?query=' + encodeURIComponent(expression));
const requests = 'sum(http_server_request_duration_seconds_count{http_route=~"/?api/v1/identity/users/me",http_request_method="GET",http_response_status_code="200"})';
const before = Number((await query(requests)).data?.result?.[0]?.value?.[1] ?? 0);
const traceId = randomBytes(16).toString('hex');
const correlationId = 'smoke-' + randomBytes(12).toString('hex');
const sentinel = 'private-synthetic-' + randomBytes(16).toString('hex');
const start = BigInt(Date.now()) * 1000000n;
execFileSync(process.execPath, [resolve(root, 'scripts/smoke-oidc.mjs')], {
  cwd: root, stdio: 'inherit', timeout: 90000,
  env: { ...process.env, SMOKE_TRACEPARENT: '00-' + traceId + '-' + randomBytes(8).toString('hex') + '-01',
    SMOKE_CORRELATION_ID: correlationId, SMOKE_PRIVATE_SENTINEL: sentinel }
});
const deadline = Date.now() + 120000;
let passed = false;
let checks = {};
let lastError;
while (Date.now() < deadline) {
  try {
    const [trace, logs, count, probes, collector] = await Promise.all([
      json('tempo/api/traces/' + traceId),
      json('loki/loki/api/v1/query_range?query=' + encodeURIComponent('{service_name="ModularApi.Api"} | CorrelationId="' + correlationId + '"') + '&start=' + start + '&limit=100'),
      query(requests), query('outbox_probe_age_seconds'), query('up{job="collector-internal"}')
    ]);
    const traceText = JSON.stringify(trace), logText = JSON.stringify(logs);
    if (traceText.includes(sentinel) || logText.includes(sentinel)) throw new Error('PrivacySentinelLeaked');
    const current = Number(count.data?.result?.[0]?.value?.[1] ?? 0);
    checks = {
      traceCorrelated: traceText.includes(correlationId) && traceText.includes('ModularApi.Api'),
      logCorrelated: logText.includes(correlationId) && logText.includes(traceId),
      counterAdvanced: current > before,
      probesFresh: probes.data?.result?.length > 0 && probes.data.result.every(x => Number(x.value[1]) < 30),
      collectorUp: collector.data?.result?.some(x => Number(x.value[1]) === 1) === true
    };
    passed = Object.values(checks).every(Boolean);
    if (passed) break;
  } catch (error) {
    if (error.message === 'PrivacySentinelLeaked') throw error;
    lastError = error.message;
    // Exportação assíncrona: a ausência inicial é esperada, com prazo total limitado.
  }
  await new Promise(resolve => setTimeout(resolve, 1000));
}
if (!passed) throw new Error('Telemetria da nova requisição não foi comprovada em 120s: ' + JSON.stringify(checks) + '; ' + (lastError ?? 'sem erro HTTP'));
console.log('PASS: nova requisição OIDC correlacionada no Tempo/Loki, contador HTTP avançou, sondas recentes e Collector monitorado; query sentinela ausente.');
