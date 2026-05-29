import http from 'k6/http';
import { check } from 'k6';
import { montarCredito } from './credito-modelo.js';

// pico: rampa ate 50 VUs, sustenta 2min, desce. Mede latencia sob carga sustentada.
export const options = {
  stages: [
    { duration: '30s', target: 50 },
    { duration: '2m', target: 50 },
    { duration: '30s', target: 0 },
  ],
};

const baseUrl = __ENV.BASE_URL || 'http://api:8080';

export default function () {
  const corpo = JSON.stringify([
    montarCredito(`carga-${__VU}-${__ITER}-1`, `nfse-${__VU}`),
    montarCredito(`carga-${__VU}-${__ITER}-2`, `nfse-${__VU}`),
    montarCredito(`carga-${__VU}-${__ITER}-3`, `nfse-${__VU}`),
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
