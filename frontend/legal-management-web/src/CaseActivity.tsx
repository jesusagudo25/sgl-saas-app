import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api, post, type CaseTask, type FollowUp, type CatalogItem, type CaseOptions } from './api';
import { SearchableSelect } from './SearchableSelect';
import './case-activity.css';

const statuses: Record<string, string> = { PENDING: 'Pendiente', IN_PROGRESS: 'En progreso', COMPLETED: 'Completada', CANCELLED: 'Cancelada' };
const priorities: Record<string, string> = { LOW: 'Baja', MEDIUM: 'Media', HIGH: 'Alta', URGENT: 'Urgente' };
const message = (e: unknown) => e instanceof Error ? e.message : 'No se pudo completar la operación.';
const date = (value?: string) => value ? new Date(value.endsWith('Z') || /[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`) : null;
const displayDate = (value?: string) => date(value)?.toLocaleString('es-PA') ?? 'Sin vencimiento';
const localDate = (value?: string) => { const d = value ? date(value)! : new Date(); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0,16); };
const iso = (value: FormDataEntryValue | null) => value ? new Date(String(value)).toISOString() : null;
type Options = Pick<CaseOptions, 'caseStatuses' | 'responsibleMemberships'>;

export function CaseActivity({ caseId, mode }: { caseId: string; mode: 'follow-ups' | 'tasks' }) {
  const tasks = mode === 'tasks'; const base = `/cases/${caseId}/${mode}`;
  const [followUps, setFollowUps] = useState<FollowUp[]>([]); const [items, setItems] = useState<CaseTask[]>([]);
  const [options, setOptions] = useState<Options>({ caseStatuses: [], responsibleMemberships: [] });
  const [generals, setGenerals] = useState<CatalogItem[]>([]); const [details, setDetails] = useState<CatalogItem[]>([]);
  const [general, setGeneral] = useState(''); const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState(''); const [optionsLoading, setOptionsLoading] = useState(true); const [optionsError, setOptionsError] = useState('');
  const [open, setOpen] = useState(false); const [editing, setEditing] = useState<CaseTask | null>(null);
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false); const [error, setError] = useState(''); const [success, setSuccess] = useState('');
  const [filters, setFilters] = useState({ status: '', priority: '', assignedMembershipId: '', dueFrom: '', dueTo: '' });
  const dialog = useRef<HTMLDialogElement>(null);
  const loadVersion = useRef(0);
  const query = new URLSearchParams(Object.entries(filters).filter(([,v]) => v).map(([k,v]) => [k, k.startsWith('due') ? new Date(v).toISOString() : v])).toString();
  const currentRequest = useRef({ base, query, tasks });
  currentRequest.current = { base, query, tasks };
  async function load() {
    const version = ++loadVersion.current; const request = currentRequest.current; setLoading(true);
    try { if (request.tasks) { const data = await api<CaseTask[]>(`${request.base}?${request.query}`); if (version === loadVersion.current) setItems(data); }
      else { const data = await api<FollowUp[]>(request.base); if (version === loadVersion.current) setFollowUps(data); } }
    catch (e) { if (version === loadVersion.current) setError(message(e)); } finally { if (version === loadVersion.current) setLoading(false); }
  }
  useEffect(() => { void load(); return () => { loadVersion.current++; }; }, [base, query]);
  useEffect(() => { let live = true; setOptionsLoading(true);
    Promise.all([api<CaseOptions>('/cases/options'), api<CatalogItem[]>('/cases/competences')]).then(([o,g]) => { if (live) { setOptions(o); setGenerals(g); } })
      .catch(e => { if (live) setOptionsError(message(e)); }).finally(() => { if (live) setOptionsLoading(false); }); return () => { live = false; };
  }, [caseId]);
  useEffect(() => { let live = true; setDetails([]); setDetailError('');
    if (!general) { setDetailLoading(false); return; } setDetailLoading(true);
    api<CatalogItem[]>(`/cases/competences?parentId=${general}`).then(x => { if (live) setDetails(x); }).catch(e => { if (live) setDetailError(message(e)); })
      .finally(() => { if (live) setDetailLoading(false); }); return () => { live = false; };
  }, [general]);
  useEffect(() => { if (open && !dialog.current?.open) dialog.current?.showModal(); else if (!open) dialog.current?.close(); }, [open]);
  function show(item: CaseTask | null = null) { setEditing(item); setGeneral(''); setError(''); setOpen(true); }
  function close() { if (!busy) setOpen(false); }
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); const d = new FormData(e.currentTarget); setError(''); setSuccess('');
    const body = tasks ? { title: d.get('title'), description: d.get('description'), assignedMembershipId: d.get('assignedMembershipId') || null,
      priority: d.get('priority'), status: d.get('status'), dueAt: iso(d.get('dueAt')) } : { caseStatusId: d.get('caseStatusId'), competenceId: d.get('competenceId'),
      competenceDetailId: d.get('competenceDetailId'), description: d.get('description'), occurredAt: iso(d.get('occurredAt')) };
    if (!tasks && (!d.get('caseStatusId') || !d.get('competenceId') || !d.get('competenceDetailId'))) { setError('Selecciona un estado, una competencia general y un detalle válidos.'); return; }
    if (tasks && d.get('assignedMembershipId') === '' && e.currentTarget.querySelector<HTMLInputElement>('[aria-label="Responsable"]')?.value) { setError('Selecciona un responsable válido o deja el campo vacío.'); return; }
    setBusy(true);
    try { if (editing) await api(`${base}/${editing.id}`, { method: 'PUT', body: JSON.stringify(body) }); else await post(base, body);
      setOpen(false); setSuccess(tasks ? 'Tarea guardada.' : 'Seguimiento registrado.'); await load();
    } catch (err) { setError(message(err)); } finally { setBusy(false); }
  }
  async function change(item: CaseTask, status: string) {
    setBusy(true); setError(''); setSuccess(''); try { await api(`${base}/${item.id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }); setSuccess('Estado actualizado.'); await load(); }
    catch(e) { setError(message(e)); } finally { setBusy(false); }
  }
  function dueLabel(item: CaseTask) { if (!item.dueAt || ['COMPLETED','CANCELLED'].includes(item.status)) return ''; const remaining = date(item.dueAt)!.getTime() - Date.now(); return remaining < 0 ? 'Vencida' : remaining < 48 * 3600000 ? 'Próxima a vencer' : ''; }
  return <section className="panel activity-panel"><div className="table-heading"><h2>{tasks ? 'Tareas' : 'Seguimientos'}</h2><button className="button" onClick={() => show()}>+ {tasks ? 'Agregar tarea' : 'Agregar seguimiento'}</button></div>
    {!open && error && <div className="alert" role="alert">{error}</div>}{success && <div className="alert success" role="status">{success}</div>}
    {tasks && <div className="activity-filters"><label className="field">Estado<select value={filters.status} onChange={e => setFilters({...filters,status:e.target.value})}><option value="">Todos</option>{Object.entries(statuses).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label>
      <label className="field">Prioridad<select value={filters.priority} onChange={e => setFilters({...filters,priority:e.target.value})}><option value="">Todas</option>{Object.entries(priorities).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label>
      <SearchableSelect label="Filtrar responsable" name="filterAssigned" options={options.responsibleMemberships} onChange={v => setFilters({...filters,assignedMembershipId:v})}/>
      <label className="field">Vence desde<input type="datetime-local" value={filters.dueFrom} onChange={e => setFilters({...filters,dueFrom:e.target.value})}/></label><label className="field">Vence hasta<input type="datetime-local" value={filters.dueTo} onChange={e => setFilters({...filters,dueTo:e.target.value})}/></label></div>}
    {loading ? <div className="loading" role="status">Cargando…</div> : (tasks ? items.length : followUps.length) === 0 ? <div className="empty-state"><h3>{tasks ? 'Sin tareas' : 'Sin seguimientos'}</h3><p>{tasks ? 'Crea una tarea o ajusta los filtros.' : 'Registra el primer seguimiento del caso.'}</p></div> : tasks ?
      <div className="activity-list">{items.map(item => <article className="activity-row task-row" key={item.id}><div><strong>{item.title}</strong>{item.description && <p className="activity-description">{item.description}</p>}<span>Responsable: {item.assignedName || 'Sin asignar'}</span></div><div><span>Prioridad: {priorities[item.priority]}</span><span>Estado: {statuses[item.status]}</span></div><div><span>{displayDate(item.dueAt)}</span>{dueLabel(item) && <strong className="due-warning">⚠ {dueLabel(item)}</strong>}{item.completedAt && <small>Completada: {displayDate(item.completedAt)}</small>}</div>
        <div className="activity-actions"><button className="button secondary" disabled={busy} onClick={() => show(item)}>Editar</button>{item.status !== 'IN_PROGRESS' && <button className="button secondary" disabled={busy} onClick={() => change(item,'IN_PROGRESS')}>{item.status === 'COMPLETED' ? 'Reabrir' : 'En progreso'}</button>}{item.status !== 'COMPLETED' && <button className="button secondary" disabled={busy} onClick={() => change(item,'COMPLETED')}>Completar</button>}{item.status !== 'CANCELLED' && <button className="button secondary" disabled={busy} onClick={() => change(item,'CANCELLED')}>Cancelar</button>}</div></article>)}</div> :
      <><div className="follow-up-header"><span>Fecha / Hora</span><span>Estado / Competencia</span><span>Descripción</span><span>Registrado por</span></div><div className="activity-list">{followUps.map(item => <article className="activity-row follow-up-row" key={item.id}><time>{displayDate(item.occurredAt)}</time><div><strong>{item.status}</strong><span>{item.competence}</span><span>{item.competenceDetail}</span></div><p className="activity-description">{item.description}</p><div><span>{item.createdBy}</span><small>Registrado: {displayDate(item.createdAt)}</small></div></article>)}</div></>}
    <dialog ref={dialog} className="catalog-dialog activity-dialog" aria-label={tasks ? editing ? 'Editar tarea' : 'Nueva tarea' : 'Nuevo seguimiento'} onClose={() => setOpen(false)} onCancel={e => { if (busy) e.preventDefault(); }}><div className="catalog-dialog-heading"><h2>{tasks ? editing ? 'Editar tarea' : 'Nueva tarea' : 'Nuevo seguimiento'}</h2><button className="icon-button" disabled={busy} type="button" aria-label="Cerrar" onClick={close}>×</button></div>
      {open && <form className="catalog-modal-form" key={editing?.id ?? 'new'} onSubmit={save}>{error && <div className="alert" role="alert">{error}</div>}
        {tasks ? <><label className="field">Título *<input autoFocus required name="title" maxLength={240} defaultValue={editing?.title}/></label><SearchableSelect label="Responsable" name="assignedMembershipId" value={editing?.assignedMembershipId} options={editing?.assignedMembershipId && !options.responsibleMemberships.some(x => x.id === editing.assignedMembershipId) ? [...options.responsibleMemberships,{id:editing.assignedMembershipId,name:editing.assignedName || 'Responsable inactivo (reasignar)'}] : options.responsibleMemberships} loading={optionsLoading} error={optionsError}/>
          <div className="form-row"><label className="field">Prioridad *<select name="priority" defaultValue={editing?.priority ?? 'MEDIUM'}>{Object.entries(priorities).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label><label className="field">Estado<select name="status" defaultValue={editing?.status ?? 'PENDING'}>{Object.entries(statuses).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label></div><label className="field">Fecha / hora de vencimiento<input name="dueAt" type="datetime-local" defaultValue={editing?.dueAt ? localDate(editing.dueAt) : ''}/></label></> :
          <><SearchableSelect required label="Estado *" name="caseStatusId" options={options.caseStatuses} loading={optionsLoading} error={optionsError}/><label className="field">Fecha / Hora *<input required name="occurredAt" type="datetime-local" defaultValue={localDate()}/></label><SearchableSelect required label="Competencia General *" name="competenceId" options={generals} loading={optionsLoading} error={optionsError} onChange={setGeneral}/><SearchableSelect key={general} required label="Competencia Detalle *" name="competenceDetailId" options={details} loading={detailLoading} error={detailError}/>{general && !detailLoading && !details.length && <p className="muted">Esta competencia no tiene detalles activos. Un administrador debe configurarlos para registrar el seguimiento.</p>}</>}
        <label className="field">Descripción{!tasks && ' *'}<textarea name="description" required={!tasks} maxLength={4000} rows={5} defaultValue={editing?.description}/></label><div className="filter-actions"><button type="button" className="button secondary" disabled={busy} onClick={close}>Cancelar</button><button className="button" disabled={busy || optionsLoading || !!optionsError || (!tasks && (detailLoading || !details.length))}>{busy ? 'Guardando…' : 'Guardar'}</button></div></form>}
    </dialog></section>;
}
