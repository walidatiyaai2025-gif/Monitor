(() => {
  const dbRows = Array.from(document.querySelectorAll('[data-db-row]'));
  const dbFilters = Array.from(document.querySelectorAll('[data-db-filter]'));
  const dbEmpty = document.querySelector('[data-db-empty]');

  const applyDbFilter = (filter) => {
    let visible = 0;
    dbRows.forEach(row => {
      const state = row.dataset.dbState || 'healthy';
      const show = filter === 'all' || filter === state || (filter === 'attention' && state !== 'healthy');
      row.hidden = !show;
      if (show) visible += 1;
    });
    if (dbEmpty) dbEmpty.classList.toggle('is-visible', visible === 0);
  };

  dbFilters.forEach(button => {
    button.addEventListener('click', () => {
      dbFilters.forEach(item => item.classList.remove('is-active'));
      button.classList.add('is-active');
      applyDbFilter(button.dataset.dbFilter || 'all');
    });
  });

  const freshnessNodes = Array.from(document.querySelectorAll('[data-health-age]'));
  if (freshnessNodes.length) {
    const startedAt = Date.now();
    window.setInterval(() => {
      const elapsed = Math.floor((Date.now() - startedAt) / 1000);
      freshnessNodes.forEach(node => {
        const base = Number(node.dataset.healthAge || '0');
        node.textContent = `${base + elapsed}s ago`;
      });
    }, 1000);
  }
})();
