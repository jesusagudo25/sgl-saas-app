import { useEffect, useState, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { api } from './api';
import './sprint5.css';
type Data = {activeCasesCount:number;overdueTasksCount:number;todayTasksCount:number;todayEventsCount:number;upcomingTasks:{id:string;caseId:string;title:string;dueAt?:string}[];upcomingEvents:{id:string;caseId?:string;title:string;startsAt:string;allDay:boolean}[];staleCases:{id:string;caseNumber:string;title:string;lastFollowUpAt?:string}[];latestFollowUps:{id:string;caseId:string;description:string;occurredAt:string}[];generatedAt:string;staleCaseDays:number;upcomingDays:number};
const date=(value?:string)=>value?new Date(value).toLocaleString('es-PA'):'Sin seguimiento';
function Block({title,children,empty}:{title:string;children:ReactNode[];empty:string}) {return <section className="panel dashboard-block"><h2>{title}</h2>{children.length?<ul>{children}</ul>:<p className="muted">{empty}</p>}</section>;}
export function Dashboard() {
  const [data,setData]=useState<Data|null>(null),[error,setError]=useState(''),[retry,setRetry]=useState(0);
  useEffect(()=>{let live=true;setError('');api<Data>(`/dashboard?timeZone=${encodeURIComponent(Intl.DateTimeFormat().resolvedOptions().timeZone)}`).then(x=>{if(live)setData(x);}).catch(e=>{if(live)setError(e instanceof Error?e.message:'No se pudo cargar el dashboard.');});return()=>{live=false;};},[retry]);
  return <><div className="page-heading"><div><span className="eyebrow">TU ESPACIO DE TRABAJO</span><h1>¿Qué debo atender hoy?</h1><p className="muted">La actividad diaria de tu organización.</p></div></div>
    {error?<div className="alert" role="alert">{error} <button className="button secondary" onClick={()=>setRetry(x=>x+1)}>Reintentar</button></div>:!data?<div className="loading" role="status">Cargando dashboard…</div>:<>
    <div className="dashboard-cards">{[['Casos activos',data.activeCasesCount],['Tareas vencidas',data.overdueTasksCount],['Tareas para hoy',data.todayTasksCount],['Eventos de hoy',data.todayEventsCount]].map(([label,value])=><section className="panel dashboard-card" key={label}><span>{label}</span><strong>{value}</strong></section>)}</div>
    <p className="muted">Próximos {data.upcomingDays} días · Zona horaria: {Intl.DateTimeFormat().resolvedOptions().timeZone}</p>
    <div className="dashboard-blocks"><Block title="Próximas tareas" empty="No hay tareas próximas.">{data.upcomingTasks.map(x=><li key={x.id}><Link to={`/app/cases/${x.caseId}?tab=Tareas`}>{x.title}</Link><time>{date(x.dueAt)}</time></li>)}</Block>
    <Block title="Próximos eventos" empty="No hay eventos próximos.">{data.upcomingEvents.map(x=><li key={x.id}><Link to={x.caseId?`/app/cases/${x.caseId}?tab=Agenda`:'/app/calendar'}>{x.title}</Link><time>{x.allDay?`${x.startsAt.slice(0,10)} · Todo el día`:date(x.startsAt)}</time></li>)}</Block>
    <Block title="Casos sin seguimiento reciente" empty="No hay casos pendientes de seguimiento.">{data.staleCases.map(x=><li key={x.id}><Link to={`/app/cases/${x.id}?tab=Seguimientos`}>{x.caseNumber} · {x.title}</Link><time>{date(x.lastFollowUpAt)}</time></li>)}</Block>
    <Block title="Últimos seguimientos" empty="Todavía no hay seguimientos.">{data.latestFollowUps.map(x=><li key={x.id}><Link to={`/app/cases/${x.caseId}?tab=Seguimientos`}>{x.description}</Link><time>{date(x.occurredAt)}</time></li>)}</Block></div><p className="muted">Sin seguimiento reciente: más de {data.staleCaseDays} días desde el último seguimiento o la creación del caso. Referencia operativa.</p></>}
    <section className="panel dashboard-block"><h2>Accesos rápidos</h2><div className="dashboard-shortcuts"><Link className="button" to="/app/cases/new">Nuevo caso</Link><Link className="button secondary" to="/app/clients/new">Nuevo cliente</Link><Link className="button secondary" to="/app/tasks/new">Nueva tarea</Link><Link className="button secondary" to="/app/calendar?new=1">Nuevo evento</Link></div></section></>;
}
