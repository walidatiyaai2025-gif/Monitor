(() => {
  const authInputs = Array.from(document.querySelectorAll('input[name="AuthenticationMode"]'));
  const sqlFields = document.querySelector('[data-sql-auth-fields]');
  if (!authInputs.length || !sqlFields) return;

  const credentialInputs = Array.from(sqlFields.querySelectorAll('input'));
  const update = () => {
    const selected = authInputs.find(input => input.checked)?.value;
    const sqlLogin = selected === '1';
    sqlFields.classList.toggle('is-hidden', !sqlLogin);
    sqlFields.setAttribute('aria-hidden', sqlLogin ? 'false' : 'true');
    credentialInputs.forEach(input => { input.disabled = !sqlLogin; });
  };

  authInputs.forEach(input => input.addEventListener('change', update));
  update();
})();
