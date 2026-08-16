(() => {
  const clocks = document.querySelectorAll('[data-live-clock]');
  const countdowns = document.querySelectorAll('[data-countdown]');
  const snapshotAges = document.querySelectorAll('[data-snapshot-age]');
  const scanPhases = document.querySelectorAll('[data-scan-phase]');
  const sidebar = document.querySelector('[data-sidebar]');
  const navToggle = document.querySelector('[data-nav-toggle]');
  const phaseNames = [
    'Snapshot cache active',
    'Health state analyzing',
    'Incident correlation',
    'Estate view synchronized'
  ];

  document.documentElement.classList.add('js-live');

  const closeNavigation = () => {
    if (!sidebar || !navToggle) return;
    sidebar.classList.remove('is-open');
    navToggle.setAttribute('aria-expanded', 'false');
  };

  if (sidebar && navToggle) {
    navToggle.addEventListener('click', () => {
      const open = !sidebar.classList.contains('is-open');
      sidebar.classList.toggle('is-open', open);
      navToggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    });

    sidebar.addEventListener('keydown', event => {
      if (event.key === 'Escape') {
        closeNavigation();
        navToggle.focus();
      }
    });

    sidebar.querySelectorAll('a').forEach(link => {
      link.addEventListener('click', () => {
        if (window.matchMedia('(max-width: 860px)').matches) closeNavigation();
      });
    });

    window.matchMedia('(min-width: 861px)').addEventListener('change', event => {
      if (event.matches) closeNavigation();
    });
  }

  const tick = () => {
    const now = new Date();
    const time = now.toLocaleTimeString([], { hour12: false });
    clocks.forEach(clock => { clock.textContent = time; });

    const seconds = 30 - (now.getSeconds() % 30);
    countdowns.forEach(counter => { counter.textContent = `${seconds}s`; });

    const elapsed = Math.floor(Date.now() / 1000);
    snapshotAges.forEach(age => {
      const initial = Number.parseInt(age.dataset.snapshotAge ?? '0', 10);
      const base = Number.isNaN(initial) ? 0 : initial;
      const localGrowth = elapsed % 30;
      age.textContent = `${base + localGrowth} sec`;
    });

    const phase = phaseNames[Math.floor(now.getTime() / 4000) % phaseNames.length];
    scanPhases.forEach(label => { label.textContent = phase; });
  };

  tick();
  window.setInterval(tick, 1000);
})();
