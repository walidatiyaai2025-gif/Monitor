const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const baseUrl = process.env.MONITOR_VISUAL_BASE_URL || 'http://127.0.0.1:5087';
const username = process.env.MONITOR_VISUAL_ADMIN_USERNAME;
const password = process.env.MONITOR_VISUAL_ADMIN_PASSWORD;
const outputDir = process.env.MONITOR_VISUAL_OUTPUT_DIR || 'artifacts/website-monitoring-visual';
if (!username || !password) throw new Error('Ephemeral visual-admin credentials are required.');
fs.mkdirSync(outputDir, { recursive: true });

async function assertPage(page) {
  await page.goto(`${baseUrl}/websites`, { waitUntil: 'networkidle' });
  await page.getByRole('heading', { name: 'Website Monitoring', exact: true }).waitFor();
  const monitoring = page.locator('[aria-label="Website monitoring summary"]');
  if (!await monitoring.getByText('OFF', { exact: true }).isVisible()) throw new Error('Website Monitoring must remain disabled for visual acceptance.');
  const checkNow = page.getByRole('button', { name: 'Check now' });
  if (await checkNow.count() && !await checkNow.first().isDisabled()) throw new Error('Check now must be disabled while Website Monitoring is disabled.');
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, reducedMotion: 'no-preference' });
    const page = await context.newPage();
    await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
    await page.getByLabel('Username').fill(username);
    await page.getByLabel('Password').fill(password);
    await Promise.all([
      page.waitForLoadState('networkidle'),
      page.getByRole('button', { name: /Enter Command Center/ }).click()
    ]);

    await page.goto(`${baseUrl}/websites`, { waitUntil: 'networkidle' });
    if (await page.getByText('No website targets are registered yet.').isVisible()) {
      await page.getByLabel('Name').fill('Browser evidence target');
      await page.getByLabel('URL').fill('https://example.invalid/health');
      await Promise.all([
        page.waitForLoadState('networkidle'),
        page.getByRole('button', { name: 'Save target' }).click()
      ]);
    }

    await assertPage(page);
    await page.getByText('Browser evidence target', { exact: true }).waitFor();
    await page.screenshot({ path: path.join(outputDir, 'website-monitoring-wide-1440.png'), fullPage: true });

    await page.setViewportSize({ width: 390, height: 844 });
    await assertPage(page);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    if (overflow > 1) throw new Error(`390px viewport has ${overflow}px page-level horizontal overflow.`);
    await page.screenshot({ path: path.join(outputDir, 'website-monitoring-responsive-390.png'), fullPage: true });

    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await assertPage(page);
    const reduced = await page.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches);
    if (!reduced) throw new Error('Reduced-motion browser emulation was not active.');
    await page.screenshot({ path: path.join(outputDir, 'website-monitoring-reduced-motion.png'), fullPage: true });

    await context.close();
  } finally {
    await browser.close();
  }
})().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
