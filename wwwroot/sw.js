// Service Worker — PEÇAS AUTOMOTIVAS
// Estratégia:
//  • Estáticos (/css, /js, /lib, /fonts, /images, /uploads): stale-while-revalidate.
//  • Navegações (HTML): sempre rede; offline cai em /offline.html. Páginas nunca são cacheadas
//    (podem conter dados do usuário logado e tokens anti-CSRF).
//  • /api, /Account, /Admin, /Pedidos e qualquer método diferente de GET: ignorados (vão direto à rede).
'use strict';

const VERSAO = 'v1.0.0';
const CACHE_ESTATICO = `pecas-estatico-${VERSAO}`;
const PAGINA_OFFLINE = '/offline.html';

const PRECACHE = [
  PAGINA_OFFLINE,
  '/css/site.css',
  '/js/site.js',
  '/lib/lucide/lucide.min.js',
  '/fonts/barlow-latin-400-normal.woff2',
  '/fonts/barlow-latin-600-normal.woff2',
  '/fonts/barlow-condensed-latin-800-normal.woff2',
  '/images/icons/favicon.svg',
  '/images/icons/icon-192.png',
  '/manifest.json'
];

const PREFIXOS_ESTATICOS = ['/css/', '/js/', '/lib/', '/fonts/', '/images/', '/uploads/'];
const PREFIXOS_IGNORADOS = ['/api', '/account', '/admin', '/pedidos'];

self.addEventListener('install', (evento) => {
  evento.waitUntil(
    caches.open(CACHE_ESTATICO)
      .then((cache) => cache.addAll(PRECACHE))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (evento) => {
  evento.waitUntil(
    caches.keys()
      .then((chaves) => Promise.all(
        chaves.filter((c) => c.startsWith('pecas-') && c !== CACHE_ESTATICO).map((c) => caches.delete(c))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (evento) => {
  const { request } = evento;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  const caminho = url.pathname.toLowerCase();
  if (PREFIXOS_IGNORADOS.some((p) => caminho === p || caminho.startsWith(`${p}/`))) return;

  if (request.mode === 'navigate') {
    evento.respondWith(
      fetch(request).catch(async () => (await caches.match(PAGINA_OFFLINE)) ?? Response.error())
    );
    return;
  }

  if (PREFIXOS_ESTATICOS.some((p) => caminho.startsWith(p)) || caminho === '/manifest.json') {
    evento.respondWith(staleWhileRevalidate(evento, request));
  }
});

async function staleWhileRevalidate(evento, request) {
  const cache = await caches.open(CACHE_ESTATICO);
  const emCache = await cache.match(request);

  const rede = fetch(request)
    .then((resposta) => {
      if (resposta.ok && resposta.type === 'basic') cache.put(request, resposta.clone());
      return resposta;
    })
    .catch(() => undefined);

  if (emCache) {
    evento.waitUntil(rede);
    return emCache;
  }
  return (await rede) ?? Response.error();
}
