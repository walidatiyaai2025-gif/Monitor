(() => {
  const authMode = document.querySelector('[data-connection-auth-mode]');
  const secretField = document.querySelector('[data-secret-reference-field]');
  if (!authMode || !secretField) return;

  const update = () => {
    const sqlLogin = authMode.value === 'SqlLogin';
    secretField.classList.toggle('is-hidden', !sqlLogin);
    const input = secretField.querySelector('input');
    if (input) input.disabled = !sqlLogin;
  };

  authMode.addEventListener('change', update);
  update();
})();
