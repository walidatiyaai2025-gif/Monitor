(() => {
  const auth = document.querySelector('[data-lab-auth]');
  const secretField = document.querySelector('[data-lab-secret-field]');
  if (!auth || !secretField) return;

  const update = () => {
    const sqlLogin = auth.value === 'SqlLogin';
    secretField.classList.toggle('is-hidden', !sqlLogin);
    const input = secretField.querySelector('input');
    if (input) input.disabled = !sqlLogin;
  };

  auth.addEventListener('change', update);
  update();
})();
