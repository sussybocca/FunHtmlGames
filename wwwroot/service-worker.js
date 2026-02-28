const CACHE_NAME = 'funhtmlgames-v1';
const urlsToCache = [
  '/',
  '/index.html',
  '/css/app.css',
  '/js/fileHandler.js',
  '/_framework/blazor.webassembly.js',
  '/_framework/blazor.webassembly.js.gz'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then(cache => cache.addAll(urlsToCache))
  );
});

self.addEventListener('fetch', event => {
  event.respondWith(
    caches.match(event.request)
      .then(response => response || fetch(event.request))
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(
      keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))
    ))
  );
});