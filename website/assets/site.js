document.documentElement.classList.add('js-motion');
const menuButton = document.querySelector('[data-menu-toggle]');
const menu = document.querySelector('[data-nav]');
if (menuButton && menu) {
  menuButton.addEventListener('click', () => {
    const open = menu.classList.toggle('is-open');
    menuButton.setAttribute('aria-expanded', String(open));
  });
}
const year = document.querySelector('[data-current-year]');
if (year) year.textContent = String(new Date().getFullYear());
const revealItems = document.querySelectorAll('.reveal');
if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
  const observer = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting) { entry.target.classList.add('is-visible'); observer.unobserve(entry.target); }
    }
  }, { threshold: 0.08 });
  revealItems.forEach(item => observer.observe(item));
} else revealItems.forEach(item => item.classList.add('is-visible'));

const releaseFeed = document.querySelector('[data-release-feed]');
if (releaseFeed) {
  fetch('https://api.github.com/repos/realTimexo/TXL-framework/releases?per_page=5', { headers: { Accept: 'application/vnd.github+json' } })
    .then(response => { if (!response.ok) throw new Error('GitHub Releases is unavailable'); return response.json(); })
    .then(releases => {
      if (!Array.isArray(releases) || releases.length === 0) return;
      const cards = releases.map(release => {
        const article = document.createElement('article');
        article.className = 'release-card';
        const header = document.createElement('header');
        const date = document.createElement('time');
        date.dateTime = release.published_at || release.created_at || '';
        date.textContent = date.dateTime ? new Date(date.dateTime).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' }) : 'Release';
        const badge = document.createElement('span');
        badge.className = 'release-badge';
        badge.textContent = release.tag_name || 'TXL';
        header.append(date, badge);
        const heading = document.createElement('h2');
        const link = document.createElement('a');
        link.href = release.html_url;
        link.textContent = release.name || release.tag_name || 'TXL release';
        heading.append(link);
        const notes = document.createElement('p');
        notes.textContent = (release.body || 'Release assets and notes are available on GitHub.').slice(0, 550);
        const assets = document.createElement('p');
        const assetNames = (release.assets || []).map(asset => asset.name).join(' · ');
        assets.textContent = assetNames ? `Downloads: ${assetNames}` : 'Open GitHub for release downloads.';
        article.append(header, heading, notes, assets);
        return article;
      });
      releaseFeed.replaceChildren(...cards);
    })
    .catch(() => { /* Keep the useful, crawler-visible release notes already in the HTML. */ });
}
