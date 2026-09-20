const endpoints = [
  'http://127.0.0.1:5761/health/ready',
  'http://127.0.0.1:8080/realms/modular-api/.well-known/openid-configuration',
  'http://127.0.0.1:3000/api/health',
  'http://127.0.0.1:9090/-/ready'
];
const deadline = Date.now() + 120000;
while (Date.now() < deadline) {
  const checks = await Promise.all(endpoints.map(async url => {
    try { return (await fetch(url, { signal: AbortSignal.timeout(2000) })).ok; }
    catch { return false; }
  }));
  if (checks.every(Boolean)) { console.log('PASS: API, Keycloak, Grafana e Prometheus prontos.'); process.exit(0); }
  await new Promise(resolve => setTimeout(resolve, 1000));
}
throw new Error('Stack local não ficou pronto em 120 segundos.');
