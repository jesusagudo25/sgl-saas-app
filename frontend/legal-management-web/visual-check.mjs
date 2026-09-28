import { chromium } from '@playwright/test';

const id = '11111111-1111-1111-1111-111111111111';
const org = { id, name: 'Firma Legal de Panamá', slug: 'firma-legal', roleCode: 'ADMIN' };
const session = { accessToken: 'visual-check', expiresAt: new Date(Date.now() + 3600000).toISOString(), user: { id: 'user-1', firstName: 'Ana', lastName: 'Pérez', email: 'ana@example.test' } };
const client = { id, type: 'COMPANY', displayName: 'Estudio Legal del Istmo', identificationType: 'RUC', identificationNumber: '123456-1-123456',
  legalName: 'Estudio Legal del Istmo', tradeName: 'Istmo Legal', dv: '12', contactPerson: 'Juan López', contactPersonEmail: 'juan@example.test',
  email: 'contacto@example.test', phone: '2222-0000', secondaryPhone: '2222-0001', mobilePhone: '6000-0000',
  website: 'https://example.test', address: 'Avenida Balboa\nOficina 4', country: 'Panamá', provinceOrState: 'Panamá', city: 'Panamá',
  status: 'ACTIVE', createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() };
const legalCase = { id, clientId: id, clientName: client.displayName, caseNumber: 'EXP-2026-001', title: 'Proceso de prueba de responsabilidad contractual',
  caseType: 'Civil', caseTypeId: id, status: 'OPEN', caseStatusId: id, priority: 'HIGH', responsibleMembershipId: id,
  responsibleName: 'Ana Pérez', openedAt: new Date().toISOString(), situationDate: new Date().toISOString(),
  court: 'Juzgado Primero', courtId: id, jurisdiction: 'Panamá', jurisdictionId: id, counterparty: 'Contraparte',
  opposingCounsel: 'Abogado contrario', notes: 'Notas de prueba', createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() };
const options = { clients: [{ id, name: client.displayName }], responsibleMemberships: [{ id, name: 'Ana Pérez' }],
  caseStatuses: [{ id, name: 'Abierto', code: 'OPEN', isOpen: true, isClosed: false, isInnocent: false, isGuilty: false }],
  caseTypes: [{ id, name: 'Civil' }], courts: [{ id, name: 'Juzgado Primero' }], jurisdictions: [{ id, name: 'Panamá' }] };
const catalog = { id, name: 'Abierto', code: 'OPEN', sortOrder: 1, isActive: true, isOpen: true, isClosed: false, isInnocent: false, isGuilty: false };
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname.replace('/api', '');
    let data;
    if (path === '/auth/refresh') data = session;
    else if (path === '/organizations') data = [org];
    else if (path.startsWith('/organizations/')) data = org;
    else if (path === '/clients') data = { items: [client], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 };
    else if (path === `/clients/${id}`) data = client;
    else if (path === '/cases/options') data = options;
    else if (path === '/cases') data = { items: [legalCase], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 };
    else if (path === `/cases/${id}`) data = legalCase;
    else if (path.startsWith('/settings/')) data = route.request().method() === 'GET'
      ? [catalog] : { ...catalog, isActive: path.endsWith('/status') ? JSON.parse(route.request().postData() || '{}').isActive : catalog.isActive };
    else data = {};
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(data) });
  });
  const routes = ['/app/clients', '/app/clients/new', `/app/clients/${id}/edit`,
    '/app/cases', '/app/cases/new', `/app/cases/${id}/edit`, `/app/cases/${id}`, '/app/settings'];
  for (const width of [360, 390, 768, 1024, 1280, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    for (const path of routes) {
      await page.goto(`http://127.0.0.1:5173${path}`);
      await page.waitForSelector('h1');
      await page.waitForTimeout(150);
      const size = await page.evaluate(() => ({ scroll: document.documentElement.scrollWidth, viewport: window.innerWidth }));
      if (size.scroll > size.viewport + 1) throw new Error(`Horizontal overflow at ${width} ${path}: ${JSON.stringify(size)}`);
      const overlapping = await page.locator('.filter-grid > .field').evaluateAll(fields => fields.some((field, i) =>
        fields.slice(i + 1).some(other => {
          const a = field.getBoundingClientRect(), b = other.getBoundingClientRect();
          return a.width > 0 && b.width > 0 && a.left < b.right - 1 && a.right > b.left + 1 && a.top < b.bottom - 1 && a.bottom > b.top + 1;
        })));
      if (overlapping) throw new Error(`Overlapping filters at ${width} ${path}`);
      if ((width === 360 || width === 1024) && (path === '/app/clients' || path === '/app/cases' || path === '/app/settings'))
        await page.screenshot({ path: `${process.env.TEMP}/sgl-${width}-${path.split('/').at(-1)}.png`, fullPage: true });
      if (path.endsWith('/clients') || path.endsWith('/cases')) {
        const expand = page.locator('.expand-button').first();
        await expand.click();
        if (await expand.getAttribute('aria-expanded') !== 'true') throw new Error(`Expansion failed at ${path}`);
        const details = await page.locator('.expanded-details dt:visible').allTextContents();
        if (path.endsWith('/clients')) {
          if (details.includes('Nombre / Razón social')) throw new Error('Client name repeated in details');
          if (width === 1024 && (details.includes('Identificación') || details.includes('Teléfono') || details.includes('Estado'))) throw new Error('Visible client fields repeated in details');
          if (width === 360 && !details.includes('Identificación')) throw new Error('Hidden client identification missing');
        } else {
          if (details.includes('Número') || details.includes('Título')) throw new Error('Case primary fields repeated');
          if (width === 1024 && (details.includes('Cliente') || details.includes('Estado'))) throw new Error('Visible case fields repeated');
          if (width === 360 && !details.includes('Cliente')) throw new Error('Hidden case client missing');
        }
        if (width === 360) await page.screenshot({ path: `${process.env.TEMP}/sgl-360-${path.split('/').at(-1)}-expanded.png`, fullPage: true });
      }
      if (path === '/app/settings') {
        await page.getByRole('button', { name: 'Nuevo' }).click();
        if (!await page.locator('dialog').evaluate(el => el.open)) throw new Error('Catalog modal failed');
        if (width === 360) await page.screenshot({ path: `${process.env.TEMP}/sgl-360-catalog-modal.png`, fullPage: true });
        if (width === 360) {
          await page.locator('dialog input[name="name"]').fill('Nuevo estado');
          await page.locator('dialog input[name="code"]').fill('NEW');
          await page.getByRole('button', { name: 'Guardar' }).click();
          await page.getByRole('status').filter({ hasText: 'Registro creado.' }).waitFor();
        } else await page.getByRole('button', { name: 'Cancelar' }).click();
        await page.getByRole('button', { name: 'Editar' }).first().click();
        if (!await page.locator('dialog input[name="name"]').inputValue()) throw new Error('Catalog edit did not preload values');
        if (width === 360) {
          await page.getByRole('button', { name: 'Guardar' }).click();
          await page.getByRole('status').filter({ hasText: 'Registro actualizado.' }).waitFor();
        } else await page.getByRole('button', { name: 'Cancelar' }).click();
      }
    }
    console.log(`PASS ${width}px: ${routes.length} screens`);
    if (width === 768) {
      const sidebarWidth = await page.locator('.sidebar').evaluate(el => el.getBoundingClientRect().width);
      if (sidebarWidth > 100) throw new Error('Tablet sidebar did not compact');
      await page.getByRole('button', { name: 'Expandir barra lateral' }).click();
      if (await page.locator('.sidebar').evaluate(el => el.getBoundingClientRect().width) < 150) throw new Error('Tablet sidebar did not expand');
      await page.getByRole('button', { name: 'Colapsar barra lateral' }).click();
      await page.evaluate(() => localStorage.removeItem('sgl-sidebar-collapsed'));
    }
  }
  await page.setViewportSize({ width: 390, height: 900 });
  await page.goto('http://127.0.0.1:5173/app/clients');
  await page.getByRole('button', { name: 'Abrir menú' }).click();
  if (!await page.locator('.sidebar').evaluate(el => el.classList.contains('open'))) throw new Error('Mobile sidebar failed');
  const navFont = await page.locator('.sidebar nav a').first().evaluate(el => Number.parseFloat(getComputedStyle(el).fontSize));
  if (navFont < 10) throw new Error('Mobile drawer labels are hidden');
  await page.getByRole('button', { name: 'Cerrar menú' }).first().click();
  console.log('PASS mobile drawer');
  await page.setViewportSize({ width: 1024, height: 900 });
  await page.goto('http://127.0.0.1:5173/app/clients');
  await page.getByRole('button', { name: 'Colapsar barra lateral' }).click();
  const sidebarWidth = await page.locator('.sidebar').evaluate(el => el.getBoundingClientRect().width);
  if (sidebarWidth > 100) throw new Error('Desktop sidebar did not collapse');
  console.log('PASS collapsible sidebar');
} finally { await browser.close(); }
