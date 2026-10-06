import { chromium, expect } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';

const baseURL = process.env.SGL_VISUAL_URL || 'http://127.0.0.1:5173';
const id = '11111111-1111-1111-1111-111111111111';
const otherCase = '22222222-2222-2222-2222-222222222222';
const hearingId = '33333333-3333-3333-3333-333333333333';
const allDayId = '44444444-4444-4444-4444-444444444444';
const now = '2026-10-05T15:00:00Z';
const org = { id, name:'Firma Legal de Panamá', slug:'firma', roleCode:'ADMIN' };
const session = { accessToken:'visual-test', expiresAt:'2026-10-06T15:00:00Z', user:{id:'user',firstName:'Ana',lastName:'Pérez',email:'ana@example.test'} };
const legalCase = { id,clientId:id,clientName:'Cliente de prueba',caseNumber:'EXP-2026-001',title:'Proceso de responsabilidad contractual',caseType:'Civil',status:'OPEN',priority:'HIGH',openedAt:now };
const caseName = `${legalCase.caseNumber} · ${legalCase.title}`;
const options = {cases:[{id,name:caseName},{id:otherCase,name:'EXP-2026-002 · Caso independiente'}],responsibleMemberships:[{id,name:'Ana Pérez'}]};
const hearing = { id:hearingId,caseId:id,caseName,title:'Audiencia de responsabilidad contractual',description:'Revisar documentación y anexos antes de la audiencia.',eventType:'HEARING',status:'SCHEDULED',assignedMembershipId:id,assignedName:'Ana Pérez',startsAt:'2026-10-05T14:00:00Z',endsAt:'2026-10-05T15:00:00Z',allDay:false,location:'Juzgado Primero',meetingUrl:'https://example.test/meeting',createdByMembershipId:id,createdAt:now,updatedAt:now };
const allDay = {...hearing,id:allDayId,title:'Plazo de todo el día',eventType:'DEADLINE',allDay:true,startsAt:'2026-10-05T00:00:00Z',endsAt:'2026-10-05T00:00:00Z'};
const other = {...hearing,id:otherCase,caseId:otherCase,caseName:'EXP-2026-002 · Caso independiente',title:'Reunión de otro caso',eventType:'MEETING'};
const output = join(process.env.TEMP || '.', 'sgl-sprint4-visual'); await mkdir(output,{recursive:true});
const browser = await chromium.launch({headless:true});
const errors = [];

async function harness(timezoneId) {
  const context = await browser.newContext({timezoneId}); const page = await context.newPage();
  await page.clock.setFixedTime(new Date(now)); page.on('pageerror',e=>{ errors.push(e.message); console.error('PAGE ERROR:',e.message); });
  const state = { events:[{...hearing},{...allDay},{...other}], lastBody:null, failNextSave:false, queries:[] };
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()); const path = url.pathname.replace('/api',''); const method = route.request().method(); let data;
    if(path==='/auth/refresh') data=session;
    else if(path==='/organizations') data=[org];
    else if(path.startsWith('/organizations/')) data=org;
    else if(path===`/cases/${id}`) data=legalCase;
    else if(path==='/calendar/options') data=options;
    else if(path==='/calendar/events' && method==='GET') {
      state.queries.push(Object.fromEntries(url.searchParams));
      data=state.events.filter(event=>{
        for(const key of ['caseId','assignedMembershipId','eventType','status']) if(url.searchParams.get(key) && event[key]!==url.searchParams.get(key)) return false;
        const from=url.searchParams.get('from'),to=url.searchParams.get('to');
        if(event.allDay) return (!from || event.endsAt.slice(0,10)>=from.slice(0,10)) && (!to || event.startsAt.slice(0,10)<to.slice(0,10));
        return (!from || new Date(event.endsAt)>=new Date(from)) && (!to || new Date(event.startsAt)<new Date(to));
      });
    }
    else if(path.startsWith('/calendar/events')) {
      if(method==='GET') data=state.events.find(x=>x.id===path.split('/')[3]);
      else {
        state.lastBody=JSON.parse(route.request().postData());
        if(state.failNextSave) { state.failNextSave=false; await route.fulfill({status:400,contentType:'application/json',body:JSON.stringify({title:'Error de validación de prueba.'})}); return; }
        if(method==='POST') { data={...hearing,...state.lastBody,id:crypto.randomUUID(),caseName:options.cases.find(x=>x.id===state.lastBody.caseId)?.name}; state.events.push(data); }
        else { const eventId=path.split('/')[3]; data={...state.events.find(x=>x.id===eventId),...state.lastBody}; state.events=state.events.map(x=>x.id===eventId?data:x); }
      }
    }
    else throw new Error(`Unhandled API: ${method} ${path}`);
    await route.fulfill({status:method==='POST'?201:200,contentType:'application/json',body:JSON.stringify(data)});
  });
  return {context,page,state};
}

async function ready(page) { try { await expect(page.locator('.fc')).toBeVisible({timeout:15000}); await expect(page.locator('.legal-calendar .loading')).toHaveCount(0); } catch(error) { console.error('PAGE:',page.url(),await page.locator('body').innerText()); await page.screenshot({path:join(output,'failure.png'),fullPage:true}); throw error; } }
async function check(page,width,screen) {
  const size=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,view:innerWidth})); expect(size.scroll,`${width} ${screen} document overflow`).toBeLessThanOrEqual(size.view+1);
  const modal=page.locator('dialog[open]');
  if(await modal.count()) { const bounds=await modal.evaluate(el=>({scroll:el.scrollWidth,client:el.clientWidth,left:el.getBoundingClientRect().left,right:el.getBoundingClientRect().right})); expect(bounds.scroll).toBeLessThanOrEqual(bounds.client+1); expect(bounds.left).toBeGreaterThanOrEqual(0); expect(bounds.right).toBeLessThanOrEqual(width); }
  const selector=await modal.count() ? 'dialog[open] .field' : '.calendar-filters > .field';
  const overlap=await page.locator(selector).evaluateAll(fields=>fields.some((field,i)=>fields.slice(i+1).some(other=>{
    const a=field.getBoundingClientRect(),b=other.getBoundingClientRect(); return a.width && b.width && a.left<b.right-1 && a.right>b.left+1 && a.top<b.bottom-1 && a.bottom>b.top+1;
  }))); expect(overlap,`${width} ${screen} overlapping controls`).toBeFalsy();
  const calendarOverflow=await page.locator('.calendar-panel, .fc-view-harness').evaluateAll(els=>els.some(el=>el.scrollWidth>el.clientWidth+1)); expect(calendarOverflow,`${width} ${screen} calendar overflow`).toBeFalsy();
  const eventOverlap=await page.locator('.fc-timegrid-event').evaluateAll(events=>events.some((event,i)=>events.slice(i+1).some(other=>{
    const a=event.getBoundingClientRect(),b=other.getBoundingClientRect(); return a.width && b.width && a.left<b.right-1 && a.right>b.left+1 && a.top<b.bottom-1 && a.bottom>b.top+1;
  }))); expect(eventOverlap,`${width} ${screen} simultaneous events overlap`).toBeFalsy();
  await page.screenshot({path:join(output,`${width}-${screen}.png`),fullPage:true});
}

try {
  const {context,page,state}=await harness('America/Panama');
  for(const width of [360,390,768,1024,1280,1440]) {
    state.events=[{...hearing},{...allDay},{...other}]; state.queries=[];
    await page.setViewportSize({width,height:900}); await page.goto(`${baseURL}/app/calendar`); await ready(page);
    await expect(page.locator('.calendar-event-card')).toHaveCount(3); await check(page,width,'month');
    expect((await page.locator('.fc-col-header-cell').allTextContents()).some(text=>/\d/.test(text))).toBeFalsy();
    await expect(page.locator('.fc-daygrid-day[data-date="2026-10-05"]')).toContainText(allDay.title);
    await page.getByRole('button',{name:'Periodo siguiente',exact:true}).click(); await page.getByRole('heading',{name:'Sin eventos en este periodo',exact:true}).waitFor();
    await page.getByRole('button',{name:'Periodo anterior',exact:true}).click(); await expect(page.locator('.calendar-event-card')).toHaveCount(3);
    await page.getByRole('button',{name:'Hoy',exact:true}).click();
    await page.getByRole('button',{name:'Semana',exact:true}).click(); await ready(page); await expect(page.locator('.fc-timeGridWeek-view')).toBeVisible(); await check(page,width,'week');
    await page.getByRole('button',{name:'Día',exact:true}).click(); await ready(page); await expect(page.locator('.fc-timeGridDay-view')).toBeVisible(); await check(page,width,'day');
    await page.getByRole('button',{name:'Nuevo evento',exact:true}).click(); await page.locator('dialog[open]').waitFor();
    await page.locator('input[name="title"]').fill('Nuevo evento de prueba'); await page.getByLabel('Caso',{exact:true}).fill(caseName); await page.getByLabel('Responsable',{exact:true}).fill('Ana Pérez');
    await page.getByLabel('Inicio',{exact:true}).fill('2026-10-05T11:00'); await page.getByLabel('Fin',{exact:true}).fill('2026-10-05T12:00');
    await page.locator('input[name="location"]').fill('Sala de reuniones'); await page.locator('input[name="meetingUrl"]').fill('https://example.test/nueva'); await page.locator('textarea[name="description"]').fill('Descripción extensa y usable en todas las anchuras.');
    await check(page,width,'new-event');
    if(width===360) { state.failNextSave=true; await page.getByRole('button',{name:'Guardar evento',exact:true}).click(); await page.getByRole('alert').filter({hasText:'Error de validación de prueba.'}).waitFor(); await expect(page.locator('input[name="title"]')).toHaveValue('Nuevo evento de prueba'); }
    await page.getByRole('button',{name:'Guardar evento',exact:true}).click(); await page.getByRole('status').filter({hasText:'Evento creado.'}).waitFor(); await ready(page);
    expect(state.lastBody.caseId).toBe(id); expect(state.lastBody.assignedMembershipId).toBe(id); expect(state.lastBody.startsAt).toBe('2026-10-05T16:00:00.000Z');
    await page.locator('.fc-event').filter({hasText:hearing.title}).first().click(); await page.locator('dialog[open]').waitFor();
    await expect(page.locator('input[name="title"]')).toHaveValue(hearing.title); await expect(page.getByLabel('Inicio',{exact:true})).toHaveValue('2026-10-05T09:00'); await expect(page.locator('input[name="meetingUrl"]')).toHaveValue(hearing.meetingUrl); await expect(page.locator('textarea[name="description"]')).toHaveValue(hearing.description);
    await page.locator('input[name="title"]').fill('Audiencia actualizada'); await check(page,width,'edit-event');
    await page.getByRole('button',{name:'Guardar evento',exact:true}).click(); await page.getByRole('status').filter({hasText:'Evento actualizado.'}).waitFor(); await ready(page);
    await page.locator('.calendar-event-card').filter({hasText:'Audiencia actualizada'}).click(); await page.getByRole('button',{name:'Completar evento',exact:true}).click(); await page.getByRole('status').filter({hasText:'Evento completado.'}).waitFor(); await ready(page);
    await page.locator('.calendar-event-card').filter({hasText:'Audiencia actualizada'}).click(); await page.getByRole('button',{name:'Cancelar evento',exact:true}).click(); await page.getByRole('status').filter({hasText:'Evento cancelado.'}).waitFor(); await ready(page);
    expect(state.events.find(x=>x.id===hearingId).status).toBe('CANCELLED');
    await page.getByLabel('Filtrar estado',{exact:true}).selectOption('CANCELLED'); await expect(page.locator('.calendar-event-card')).toHaveCount(1); await check(page,width,'cancelled-filter');
    await page.getByLabel('Filtrar estado',{exact:true}).selectOption(''); await page.getByLabel('Filtrar caso',{exact:true}).fill(caseName); await expect(page.locator('.calendar-event-card')).toHaveCount(3);
    await page.getByLabel('Filtrar responsable',{exact:true}).fill('Ana Pérez'); await page.getByLabel('Filtrar tipo',{exact:true}).selectOption('DEADLINE'); await expect(page.locator('.calendar-event-card')).toHaveCount(1);
    expect(state.queries.at(-1)).toMatchObject({caseId:id,assignedMembershipId:id,eventType:'DEADLINE'}); await page.getByLabel('Filtrar tipo',{exact:true}).selectOption('CALL'); await page.getByRole('heading',{name:'Sin eventos en este periodo',exact:true}).waitFor();
    await page.goto(`${baseURL}/app/cases/${id}`); await page.getByRole('heading',{name:legalCase.title,exact:true}).waitFor(); await page.getByRole('button',{name:'Agenda',exact:true}).click(); await ready(page);
    await expect(page.locator('.calendar-event-card')).toHaveCount(3); await expect(page.locator('.calendar-event-list')).not.toContainText(other.title); await check(page,width,'case-agenda');
    await page.getByRole('button',{name:'Nuevo evento',exact:true}).click(); await expect(page.getByLabel('Caso',{exact:true})).toHaveValue(caseName);
    await page.locator('input[name="title"]').fill('Evento contextual de todo el día'); await page.getByLabel('Todo el día',{exact:true}).check(); await expect(page.getByLabel('Inicio',{exact:true})).toHaveAttribute('type','date');
    await page.getByLabel('Inicio',{exact:true}).fill('2026-10-05'); await page.getByLabel('Fin',{exact:true}).fill('2026-10-07'); await check(page,width,'all-day-contextual');
    await page.getByRole('button',{name:'Guardar evento',exact:true}).click(); await page.getByRole('status').filter({hasText:'Evento creado.'}).waitFor(); await ready(page);
    expect(state.lastBody).toMatchObject({caseId:id,allDay:true,startsAt:'2026-10-05T00:00:00Z',endsAt:'2026-10-07T00:00:00Z'});
    await page.locator('.calendar-event-card').filter({hasText:'Evento contextual de todo el día'}).click(); await expect(page.getByLabel('Inicio',{exact:true})).toHaveValue('2026-10-05'); await expect(page.getByLabel('Fin',{exact:true})).toHaveValue('2026-10-07'); await page.getByRole('button',{name:'Cerrar',exact:true}).click();
    console.log(`PASS ${width}: month, week, day, navigation, creation, editing, status changes, filters, case agenda, all-day, overflow`);
  }
  await context.close();
  for(const timezone of ['Pacific/Kiritimati','Etc/GMT+12']) {
    const {context:zoneContext,page:zonePage,state:zoneState}=await harness(timezone); await zonePage.setViewportSize({width:390,height:900}); await zonePage.goto(`${baseURL}/app/calendar`); await ready(zonePage);
    await expect(zonePage.locator('.fc-daygrid-day[data-date="2026-10-05"]')).toContainText(allDay.title); await expect(zonePage.locator('.fc-daygrid-day[data-date="2026-10-04"]')).not.toContainText(allDay.title);
    await zonePage.locator('.calendar-event-card').filter({hasText:allDay.title}).click(); await expect(zonePage.getByLabel('Inicio',{exact:true})).toHaveValue('2026-10-05'); await expect(zonePage.getByLabel('Fin',{exact:true})).toHaveValue('2026-10-05');
    await zonePage.getByRole('button',{name:'Guardar evento',exact:true}).click(); await zonePage.getByRole('status').filter({hasText:'Evento actualizado.'}).waitFor(); expect(zoneState.lastBody.startsAt).toBe('2026-10-05T00:00:00Z'); expect(zoneState.lastBody.endsAt).toBe('2026-10-05T00:00:00Z');
    await check(zonePage,390,`timezone-${timezone.replaceAll('/','-').replaceAll('+','plus')}`); await zoneContext.close(); console.log(`PASS all-day civil date: ${timezone}`);
  }
  expect(errors).toEqual([]); console.log(`Screenshots: ${output}`);
} finally { await browser.close(); }
