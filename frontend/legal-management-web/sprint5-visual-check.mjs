import { chromium, expect } from '@playwright/test';
import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';
const baseURL=process.env.SGL_VISUAL_URL||'http://127.0.0.1:5173';
const id='11111111-1111-1111-1111-111111111111',now='2026-10-18T15:00:00Z';
const org={id,name:'Firma Legal',slug:'firma',roleCode:'ADMIN'},session={accessToken:'visual',expiresAt:'2099-01-01T00:00:00Z',user:{id:'user',firstName:'Ana',lastName:'Pérez',email:'ana@example.test'}};
const legalCase={id,clientId:id,clientName:'Cliente',caseNumber:'EXP-001',title:'Expediente de prueba',caseType:'Civil',status:'OPEN',priority:'HIGH',openedAt:now};
const document={id,originalFileName:'Documento_de_prueba_con_nombre_muy_largo_'.repeat(4)+'.pdf',extension:'.pdf',contentType:'application/pdf',sizeBytes:1024,documentType:'EVIDENCE',notes:'Observaciones del expediente.',uploadedBy:'Ana Pérez',uploadedAt:now};
const empty={activeCasesCount:0,overdueTasksCount:0,todayTasksCount:0,todayEventsCount:0,upcomingTasks:[],upcomingEvents:[],staleCases:[],latestFollowUps:[],generatedAt:now,staleCaseDays:30,upcomingDays:7};
function dashboard(count){return {...empty,activeCasesCount:count,overdueTasksCount:count,todayTasksCount:count,todayEventsCount:count,upcomingTasks:Array.from({length:count},(_,i)=>({id:String(i),caseId:id,title:'Revisar documentación extensa '.repeat(3),dueAt:now})),upcomingEvents:Array.from({length:count},(_,i)=>({id:String(i),caseId:id,title:'Audiencia de revisión',startsAt:now,allDay:i%2===0})),staleCases:Array.from({length:count},(_,i)=>({id:String(i),caseNumber:'EXP-2026',title:'Caso sin seguimiento '.repeat(4)})),latestFollowUps:Array.from({length:count},(_,i)=>({id:String(i),caseId:id,description:'Descripción de seguimiento '.repeat(6),occurredAt:now}))};}
const output=join(process.env.TEMP||'.','sgl-sprint5-visual');await mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true});const context=await browser.newContext({timezoneId:'America/Panama'});const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));
let data=dashboard(2),documents=[document],fail=false,delay=false,uploadBody='';
await page.route('**/api/**',async route=>{
  const path=new URL(route.request().url()).pathname.replace('/api',''),method=route.request().method();let result;
  if(path==='/auth/refresh')result=session;else if(path==='/organizations')result=[org];else if(path.startsWith('/organizations/'))result=org;
  else if(path==='/dashboard') {if(delay)await new Promise(r=>setTimeout(r,700));if(fail){await route.fulfill({status:500,json:{title:'Error de prueba'}});return;}result=data;}
  else if(path===`/cases/${id}`)result=legalCase;
  else if(path===`/cases/${id}/documents`&&method==='GET'){if(delay)await new Promise(r=>setTimeout(r,700));result=documents;}
  else if(path===`/cases/${id}/documents`&&method==='POST') {uploadBody=route.request().postData();if(fail){await route.fulfill({status:400,json:{title:'Archivo inválido de prueba'}});return;}documents=[document];result=document;}
  else if(path.endsWith('/download')){await route.fulfill({status:200,contentType:'application/pdf',body:'%PDF-1.7 test'});return;}
  else if(path===`/cases/${id}/documents/${id}`&&method==='DELETE'){documents=[];await route.fulfill({status:204});return;}
  else if(path==='/calendar/events' || path.endsWith('/tasks') || path==='/cases/competences')result=[];
  else if(path==='/cases/options')result={clients:[],responsibleMemberships:[],caseStatuses:[],caseTypes:[],courts:[],jurisdictions:[]};
  else if(path==='/calendar/options')result={cases:[{id,name:'EXP-001'}],responsibleMemberships:[]};
  else throw new Error(`Unhandled API: ${method} ${path}`);
  await route.fulfill({status:method==='POST'?201:200,json:result});
});
async function check(width,name){const dimensions=await page.evaluate(()=>({scroll:document.documentElement.scrollWidth,view:innerWidth}));expect(dimensions.scroll,`${width} ${name} overflow`).toBeLessThanOrEqual(dimensions.view+1);const modal=page.locator('dialog[open]');if(await modal.count()){const box=await modal.evaluate(el=>({scroll:el.scrollWidth,client:el.clientWidth,left:el.getBoundingClientRect().left,right:el.getBoundingClientRect().right}));expect(box.scroll,`${name} modal overflow`).toBeLessThanOrEqual(box.client+1);expect(box.left).toBeGreaterThanOrEqual(0);expect(box.right).toBeLessThanOrEqual(width);}await page.screenshot({path:join(output,`${width}-${name}.png`),fullPage:true});}
try {
for(const width of [360,390,768,1024,1280,1440]){
 await page.setViewportSize({width,height:900});data=dashboard(2);await page.goto(`${baseURL}/app`);await expect(page.locator('.dashboard-card')).toHaveCount(4);await check(width,'dashboard');
 data=empty;await page.reload();await expect(page.getByText('No hay tareas próximas.')).toBeVisible();await check(width,'dashboard-empty');
 data=dashboard(10);await page.reload();await expect(page.locator('.dashboard-block li')).toHaveCount(40);await check(width,'dashboard-abundant');
 delay=true;await page.reload();await expect(page.getByRole('status').filter({hasText:'Cargando dashboard'})).toBeVisible();await check(width,'dashboard-loading');delay=false;await expect(page.locator('.dashboard-card')).toHaveCount(4);
 fail=true;await page.reload();await expect(page.getByRole('alert')).toContainText('Error de prueba');await check(width,'dashboard-error');fail=false;await page.getByRole('button',{name:'Reintentar'}).click();await expect(page.locator('.dashboard-card')).toHaveCount(4);
 documents=[document];await page.goto(`${baseURL}/app/cases/${id}?tab=Documentos`);await expect(page.locator('.document-row')).toHaveCount(1);await check(width,'documents-long-name');
 const download=page.waitForEvent('download');await page.getByRole('button',{name:'Descargar',exact:true}).click();expect((await download).suggestedFilename()).toBe(document.originalFileName);
 await page.getByRole('button',{name:'Subir documento',exact:true}).click();await page.locator('input[type=file]').setInputFiles({name:'prueba.pdf',mimeType:'application/pdf',buffer:Buffer.from('%PDF-1.7 test')});await page.locator('textarea[name=notes]').fill('Notas de prueba');await check(width,'upload');
 fail=true;await page.getByRole('button',{name:'Guardar documento'}).click();await expect(page.locator('dialog[open] [role=alert]')).toContainText('Archivo inválido');await check(width,'upload-error');fail=false;await page.getByRole('button',{name:'Guardar documento'}).click();await expect(page.locator('dialog[open]')).toHaveCount(0);expect(uploadBody).toContain('name="file"');expect(uploadBody).toContain('name="documentType"');
 await page.getByRole('button',{name:'Eliminar',exact:true}).click();await expect(page.locator('dialog[open]')).toBeVisible();await check(width,'delete-confirm');await page.getByRole('button',{name:'Confirmar eliminación'}).click();await expect(page.getByRole('heading',{name:'Sin documentos',exact:true})).toBeVisible();await check(width,'documents-empty');
 delay=true;await page.reload();await expect(page.getByRole('status').filter({hasText:'Cargando documentos'})).toBeVisible();await check(width,'documents-loading');delay=false;await expect(page.getByRole('heading',{name:'Sin documentos',exact:true})).toBeVisible();
}
await page.goto(baseURL+'/app');await expect(page.locator('.dashboard-card')).toHaveCount(4);
await page.getByRole('link',{name:'Nueva tarea',exact:true}).click();await page.getByLabel('Caso',{exact:true}).fill('EXP-001');await page.getByRole('button',{name:'Continuar'}).click();await expect(page.locator('dialog[open]')).toHaveAttribute('aria-label','Nueva tarea');
await page.goto(baseURL+'/app');await page.getByRole('link',{name:'Nuevo evento',exact:true}).click();await expect(page.locator('dialog[open]')).toBeVisible();
expect(errors).toEqual([]);console.log(`Sprint 5: seis anchuras verificadas. Capturas: ${output}`);
}finally{await browser.close();}
