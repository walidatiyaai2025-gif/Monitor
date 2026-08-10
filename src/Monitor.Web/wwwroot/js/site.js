(() => {
  const clocks = document.querySelectorAll('[data-live-clock]');
  const countdowns = document.querySelectorAll('[data-countdown]');

  const tick = () => {
    const now = new Date();
    const time = now.toLocaleTimeString([], { hour12: false });
    clocks.forEach(clock => { clock.textContent = time; });

    const seconds = 30 - (now.getSeconds() % 30);
    countdowns.forEach(counter => { counter.textContent = `${seconds}s`; });
  };

  tick();
  window.setInterval(tick, 1000);
})();
