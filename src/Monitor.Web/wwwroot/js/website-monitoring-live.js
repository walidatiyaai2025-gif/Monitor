(() => {
    const root = document.querySelector('[data-website-live]');
    if (!root) return;

    const intervalMs = 15000;
    let timer = null;

    const schedule = () => {
        if (timer) window.clearTimeout(timer);
        timer = window.setTimeout(() => {
            if (document.visibilityState === 'visible') {
                window.location.reload();
                return;
            }
            schedule();
        }, intervalMs);
    };

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') schedule();
    });

    schedule();
})();
