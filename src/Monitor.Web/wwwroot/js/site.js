(() => {
  const clocks = document.querySelectorAll('[data-live-clock]');
  const countdowns = document.querySelectorAll('[data-countdown]');
  const snapshotAges = document.querySelectorAll('[data-snapshot-age]');
  const scanPhases = document.querySelectorAll('[data-scan-phase]');
  const phaseNames = [
    'Snapshot cache active',
    'Health state analyzing',
    'Incident correlation',
    'Estate view synchronized'
  ];

  document.documentElement.classList.add('js-live');

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
