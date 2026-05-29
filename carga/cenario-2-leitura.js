import http from 'k6/http';
import { check } from 'k6';

// leitura: 100 VUs por 1min. So consulta chaves que o cenario 1 garante (sem 404 por design).
// Pre-condicao: rodar depois do cenario 1 E da fila drenar (ver README), senao da 404.
export const options = {
  vus: 100,
  duration: '1m',
  thresholds: {
    http_req_failed: ['rate<0.005'],
  },
};

const baseUrl = __ENV.BASE_URL || 'http://api:8080';

export default function () {
  // 100 VUs sobre 50 chaves do cenario 1 (VU 1..50, iteracao 1, credito 1)
  const chave = ((__VU - 1) % 50) + 1;

  let resposta;
  if (__ITER % 10 < 7) {
    resposta = http.get(`${baseUrl}/api/creditos/nfse-${chave}`);
  } else {
    resposta = http.get(`${baseUrl}/api/creditos/credito/carga-${chave}-1-1`);
  }

  check(resposta, {
    'status 200': function (r) {
      return r.status === 200;
    },
  });
}
