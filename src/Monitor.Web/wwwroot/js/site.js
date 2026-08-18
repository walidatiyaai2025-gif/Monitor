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

  const setupRefreshAllConnections = () => {
    const button = document.querySelector('[data-refresh-all-connections]');
    const status = document.querySelector('[data-refresh-all-status]');
    const runtime = document.querySelector('[data-refresh-all-runtime]');
    if (!button || !status || !runtime) return;

    const tokenInput = runtime.querySelector('input[name="__RequestVerificationToken"]');
    const registrationIds = Array.from(runtime.querySelectorAll('[data-refresh-registration-id]'))
      .map(input => input.value?.trim() ?? '')
      .filter(id => /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(id));

    const previousResult = window.sessionStorage.getItem('monitor.refreshAll.result');
    if (previousResult) {
      status.textContent = previousResult;
      window.sessionStorage.removeItem('monitor.refreshAll.result');
    }

    if (registrationIds.length === 0) {
      status.textContent = 'No active connections';
      button.disabled = true;
      return;
    }

    if (!tokenInput?.value) {
      status.textContent = 'Security token unavailable · reload page';
      button.disabled = true;
      return;
    }

    if (!previousResult) status.textContent = `${registrationIds.length} active connection(s)`;

    button.addEventListener('click', async () => {
      if (button.disabled) return;
      button.disabled = true;
      button.textContent = 'Refreshing…';

      let refreshed = 0;
      let retainedStale = 0;
      let skipped = 0;
      let throttled = 0;
      let failed = 0;
      let authorizationFailure = null;

      for (let index = 0; index < registrationIds.length; index += 1) {
        const id = registrationIds[index];
        status.textContent = `Refreshing ${index + 1} of ${registrationIds.length}…`;

        try {
          const body = new URLSearchParams({ __RequestVerificationToken: tokenInput.value });
          const response = await fetch(`/servers/${encodeURIComponent(id)}/refresh-snapshot`, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
              'Accept': 'application/json',
              'Content-Type': 'application/x-www-form-urlencoded;charset=UTF-8',
              'X-Requested-With': 'XMLHttpRequest'
            },
            body: body.toString()
          });

          if (response.status === 401 || response.status === 403) {
            authorizationFailure = response.status;
            break;
          }

          if (response.redirected) {
            authorizationFailure = 'redirect';
            break;
          }

          const contentType = response.headers.get('content-type')?.toLowerCase() ?? '';
          const isJson = contentType.includes('application/json');
          if (response.ok) {
            if (isJson) {
              refreshed += 1;
            } else {
              failed += 1;
            }
          } else if (response.status === 409) {
            skipped += 1;
          } else if (response.status === 429) {
            throttled += 1;
          } else if (response.status === 503 && isJson) {
            retainedStale += 1;
          } else {
            failed += 1;
          }
        } catch {
          failed += 1;
        }
      }

      if (authorizationFailure !== null) {
        const message = authorizationFailure === 403
          ? 'Administrator permission is required to refresh snapshots.'
          : 'Session expired or authentication is required before refreshing snapshots.';
        status.textContent = `${message} Partial result · refreshed ${refreshed} · stale retained ${retainedStale} · skipped ${skipped} · throttled ${throttled} · failed ${failed}`;
        button.textContent = 'Refresh unavailable';
        button.disabled = true;
        return;
      }

      const result = `Done · refreshed ${refreshed} · stale retained ${retainedStale} · skipped ${skipped} · throttled ${throttled} · failed ${failed}`;
      status.textContent = result;
      button.textContent = 'Refresh all again';
      button.disabled = false;

      if (refreshed > 0 || retainedStale > 0) {
        window.sessionStorage.setItem('monitor.refreshAll.result', result);
        status.textContent = `${result} · updating intelligence…`;
        window.setTimeout(() => window.location.reload(), 900);
      }
    });
  };

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

  setupRefreshAllConnections();
  tick();
  window.setInterval(tick, 1000);
})();
