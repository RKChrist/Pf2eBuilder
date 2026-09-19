// Deliberately inert in development. A caching worker here would serve yesterday's build back
// to whoever is testing today's, which is the hardest kind of bug to notice.
self.addEventListener('fetch', () => { });
