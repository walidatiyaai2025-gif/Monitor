(() => {
  const cards = [...document.querySelectorAll('[data-server-card]')];
  const filters = [...document.querySelectorAll('[data-estate-filter]')];
  const search = document.querySelector('[data-estate-search]');
  const empty = document.querySelector('[data-estate-empty]');

  if (cards.length) {
    let activeFilter = 'all';

    const apply = () => {
      const query = (search?.value ?? '').trim().toLowerCase();
      let visible = 0;

      cards.forEach(card => {
        const state = card.dataset.state ?? '';
        const name = card.dataset.name ?? '';
        const matchesState = activeFilter === 'all' || state === activeFilter;
        const matchesSearch = !query || name.includes(query);
        const show = matchesState && matchesSearch;
        card.hidden = !show;
        if (show) visible++;
      });

      empty?.classList.toggle('is-visible', visible === 0);
    };

    filters.forEach(button => {
      button.addEventListener('click', () => {
        activeFilter = button.dataset.estateFilter ?? 'all';
        filters.forEach(item => item.classList.toggle('is-active', item === button));
        apply();
      });
    });

    search?.addEventListener('input', apply);
    apply();
  }

  const ageElements = [...document.querySelectorAll('[data-snapshot-age]')];
  const ageBars = [...document.querySelectorAll('[data-snapshot-age-bar]')];
  if (ageElements.length || ageBars.length) {
    const started = Date.now();
    const tickAge = () => {
      const elapsed = Math.floor((Date.now() - started) / 1000);
      ageElements.forEach(element => {
        const initial = Number.parseInt(element.dataset.initialAge ?? '0', 10) || 0;
        element.textContent = `${initial + elapsed} sec ago`;
      });
      ageBars.forEach(bar => {
        const initial = Number.parseInt(bar.dataset.initialAge ?? '0', 10) || 0;
        const age = Math.min(initial + elapsed, 90);
        bar.style.width = `${Math.max(6, (age / 90) * 100)}%`;
      });
    };
    tickAge();
    window.setInterval(tickAge, 1000);
  }
})();
