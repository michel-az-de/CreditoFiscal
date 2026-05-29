import http from 'k6/http';
import { check } from 'k6';
import { montarCredito } from './credito-modelo.js';

// stress: rampa ate 500 VUs em 5min, sustenta 1min, desce. Acha o ponto de quebra.
export const options = {
  stages: [
    { duration: '5m', target: 500 },
    { duration: '1m', target: 500 },
    { duration: '1m', target: 0 },
  ],
};

const baseUrl = __ENV.BASE_URL || 'http://api:8080';

export default function () {
  const corpo = JSON.stringify([
    montarCredito(`stress-${__VU}-${__ITER}-1`, `nfse-stress-${__VU}`),
    montarCredito(`stress-${__VU}-${__ITER}-2`, `nfse-stress-${__VU}`),
    montarCredito(`stress-${__VU}-${__ITER}-3`, `nfse-stress-${__VU}`),
  ]);

  const resposta = http.post(`${baseUrl}/api/creditos/integrar-credito-constituido`, corpo, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(resposta, {
    'status 202': function (r) {
      return r.status === 202;
    },
  });
}
