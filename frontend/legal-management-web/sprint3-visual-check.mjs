import { chromium, expect } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';

const id = '11111111-1111-1111-1111-111111111111';
const root2 = '22222222-2222-2222-2222-222222222222';
const detail = '33333333-3333-3333-3333-333333333333';
const now = new Date().toISOString();
const org = { id, name: 'Firma Legal de Panamá', slug: 'firma', roleCode: 'ADMIN' };
const session = { accessToken: 'visual-test', expiresAt: now, user: { id: 'user', firstName: 'Ana', lastName: 'Pérez', email: 'ana@example.test' } };
const legalCase = { id, clientId:id, clientName:'Cliente de prueba', caseNumber:'EXP-2026-001', title:'Proceso de responsabilidad contractual', caseType:'Civil', status:'OPEN', priority:'HIGH', openedAt:now };
const options = { clients:[], caseStatuses:[{id,name:'Abierto'}], responsibleMemberships:[{id,name:'Ana Pérez'}], caseTypes:[],courts:[],jurisdictions:[] };
const competences = [{id,name:'Civil',isActive:true,sortOrder:0,parentId:null}, {id:root2,name:'Sin detalles',isActive:true,sortOrder:1,parentId:null}, {id:detail,name:'Responsabilidad contractual',parentId:id,isActive:true,sortOrder:0}];
const originalFollowUp = { id, occurredAt:now, status:'Abierto', competence:'Civil', competenceDetail:'Responsabilidad contractual', description:'Presentación de documentación judicial.\nSeguimiento de una descripción extensa para verificar su lectura en pantallas pequeñas.', createdBy:'Ana Pérez',createdAt:now };
const originalTask = { id,title:'Preparar documentación para audiencia', description:'Revisar contratos y anexos antes de la audiencia.',assignedMembershipId:id,assignedName:'Ana Pérez',priority:'URGENT',status:'PENDING',dueAt:new Date(Date.now()-3600000).toISOString(),createdAt:now };
const output = join(process.env.TEMP || '.', 'sgl-sprint3-visual'); await mkdir(output,{recursive:true});
const browser = await chromium.launch({headless:true});
const errors = [];
try {
  const page = await browser.newPage(); page.on('pageerror',e=>errors.push(e.message));
  let followUps = [], tasks = [], lastBody;
  await page.route('**/api/**', async route => {
    const url = new URL(route.request().url()); const path = url.pathname.replace('/api',''); const method = route.request().method();
    let data;
    if(path==='/auth/refresh') data=session;
    else if(path==='/organizations') data=[org];
    else if(path.startsWith('/organizations/')) data=org;
    else if(path==='/cases/options') data=options;
    else if(path==='/cases/competences') data=competences.filter(x=>x.parentId===(url.searchParams.get('parentId') || null));
    else if(path===`/cases/${id}`) data=legalCase;
    else if(path===`/cases/${id}/follow-ups`) { if(method==='POST') { lastBody=JSON.parse(route.request().postData()); data={...originalFollowUp,...lastBody,id:crypto.randomUUID()}; followUps.unshift(data); } else data=followUps; }
    else if(path.startsWith(`/cases/${id}/tasks`)) {
      if(method==='GET') data=tasks.filter(x=>!url.searchParams.get('status') || x.status===url.searchParams.get('status'));
      else { lastBody=JSON.parse(route.request().postData()); const taskId=path.split('/')[4];
        if(method==='POST') { data={...originalTask,...lastBody,id:crypto.randomUUID()}; tasks.push(data); }
        else { data={...tasks.find(x=>x.id===taskId),...lastBody}; tasks=tasks.map(x=>x.id===taskId?data:x); }
      }
    }
    else if(path==='/settings/competences') data=competences;
    else if(path.startsWith('/settings/')) data=[];
    else throw new Error(`Unhandled API ${method} ${path}`);
    await route.fulfill({status:method==='POST'?201:200,contentType:'application/json',body:JSON.stringify(data)});
  });
  async function check(width,screen) {
    const size=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,view:innerWidth})); expect(size.scroll,`${width} ${screen} document overflow`).toBeLessThanOrEqual(size.view+1);
    if(await page.locator('dialog[open]').count()) {
      const bounds=await page.locator('dialog[open]').evaluate(el=>({scroll:el.scrollWidth,client:el.clientWidth,left:el.getBoundingClientRect().left,right:el.getBoundingClientRect().right}));
      expect(bounds.scroll,`${width} ${screen} dialog overflow`).toBeLessThanOrEqual(bounds.client+1); expect(bounds.left).toBeGreaterThanOrEqual(0); expect(bounds.right).toBeLessThanOrEqual(width);
    }
    const overlap=await page.locator(await page.locator('dialog[open]').count() ? 'dialog[open] .field' : '.activity-filters>.field').evaluateAll(fields=>fields.some((field,i)=>fields.slice(i+1).some(other=>{
      const a=field.getBoundingClientRect(),b=other.getBoundingClientRect(); return a.width && b.width && a.left<b.right-1 && a.right>b.left+1 && a.top<b.bottom-1 && a.bottom>b.top+1;
    }))); expect(overlap,`${width} ${screen} overlapping controls`).toBeFalsy();
    await page.screenshot({path:join(output,`${width}-${screen}.png`),fullPage:true});
  }
  for(const width of [360,390,768,1024,1280,1440]) {
    followUps=[{...originalFollowUp}]; tasks=[{...originalTask},{...originalTask,id:root2,title:'Revisar anexos de la próxima audiencia',priority:'HIGH',dueAt:new Date(Date.now()+24*3600000).toISOString()}];
    await page.setViewportSize({width,height:900}); await page.goto(`http://127.0.0.1:5173/app/cases/${id}`); await page.getByRole('heading',{name:legalCase.title}).waitFor();
    await check(width,'case-detail');
    await page.getByRole('button',{name:'Seguimientos',exact:true}).click(); await page.getByText(originalFollowUp.description,{exact:true}).waitFor(); await check(width,'follow-ups');
    await page.getByRole('button',{name:'+ Agregar seguimiento',exact:true}).click(); await page.locator('dialog[open]').waitFor();
    await page.getByLabel('Estado *',{exact:true}).fill('Abierto'); await page.getByLabel('Competencia General *',{exact:true}).fill('Civil');
    await expect(page.locator('input[name="competenceDetailId"]')).toHaveValue(''); await expect(page.locator('datalist option[value="Responsabilidad contractual"]')).toHaveCount(1); await page.getByLabel('Competencia Detalle *',{exact:true}).fill('Responsabilidad contractual'); await expect(page.locator('input[name="competenceDetailId"]')).toHaveValue(detail);
    await expect(page.locator('input[name="competenceDetailId"]')).toHaveValue(detail);
    await page.getByLabel('Competencia General *',{exact:true}).fill('Sin detalles'); await expect(page.getByLabel('Competencia Detalle *',{exact:true})).toHaveValue('');
    await page.getByText('Esta competencia no tiene detalles activos.',{exact:false}).waitFor(); await expect(page.getByRole('button',{name:'Guardar',exact:true})).toBeDisabled();
    await page.getByLabel('Competencia General *',{exact:true}).fill('Civil'); await expect(page.locator('datalist option[value="Responsabilidad contractual"]')).toHaveCount(1); await page.getByLabel('Competencia Detalle *',{exact:true}).fill('Responsabilidad contractual'); await expect(page.locator('input[name="competenceDetailId"]')).toHaveValue(detail);
    await page.locator('textarea[name="description"]').fill('Nueva actuación judicial'); await check(width,'new-follow-up');
    await page.getByRole('button',{name:'Guardar',exact:true}).click(); await page.getByRole('status').filter({hasText:'Seguimiento registrado.'}).waitFor(); expect(lastBody.competenceDetailId).toBe(detail); expect(lastBody.occurredAt).toMatch(/Z$/);
    await page.getByRole('button',{name:'Tareas',exact:true}).click(); await page.getByText('⚠ Vencida',{exact:true}).waitFor(); await page.getByText('⚠ Próxima a vencer',{exact:true}).waitFor(); await check(width,'tasks');
    await page.getByRole('button',{name:'+ Agregar tarea',exact:true}).click(); await page.locator('dialog[open]').waitFor(); await page.locator('input[name="title"]').fill('Nueva tarea');
    await page.getByLabel('Responsable',{exact:true}).fill('Ana Pérez'); await page.locator('textarea[name="description"]').fill('Descripción usable en móvil'); await check(width,'new-task');
    await page.getByRole('button',{name:'Guardar',exact:true}).click(); await page.getByRole('status').filter({hasText:'Tarea guardada.'}).waitFor(); expect(lastBody.assignedMembershipId).toBe(id);
    await page.getByRole('button',{name:'Editar',exact:true}).first().click(); await expect(page.locator('input[name="title"]')).toHaveValue(originalTask.title); await page.locator('dialog[open] button').filter({hasText:'Cancelar'}).click();
    await page.getByRole('button',{name:'Completar',exact:true}).first().click(); await page.getByRole('button',{name:'Reabrir',exact:true}).waitFor(); await page.getByRole('button',{name:'Reabrir',exact:true}).click(); await expect(page.getByRole('button',{name:'Reabrir',exact:true})).toHaveCount(0);
    await page.getByRole('button',{name:'Cancelar',exact:true}).first().click(); await page.locator('.activity-filters select').first().selectOption('CANCELLED'); await expect(page.locator('.task-row')).toHaveCount(1); await page.locator('.activity-filters select').first().selectOption('COMPLETED'); await page.getByRole('heading',{name:'Sin tareas',exact:true}).waitFor();
    await page.goto('http://127.0.0.1:5173/app/settings'); await page.getByLabel('Tabla a administrar').selectOption('competences'); await page.getByText('Civil',{exact:true}).first().waitFor(); await check(width,'competences');
    await page.getByRole('button',{name:'Nuevo',exact:true}).click(); await page.locator('dialog[open]').waitFor(); await check(width,'new-competence');
    await page.getByRole('button',{name:'Cancelar',exact:true}).click();
    console.log(`PASS ${width}: case detail, follow-ups, dependent selectors, tasks, editing, transitions, competences, dialogs`);
  }
  expect(errors).toEqual([]); console.log(`Screenshots: ${output}`);
} finally { await browser.close(); }
