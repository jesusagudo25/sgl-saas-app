import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import FullCalendar from '@fullcalendar/react';
import dayGridPlugin from '@fullcalendar/daygrid';
import timeGridPlugin from '@fullcalendar/timegrid';
import esLocale from '@fullcalendar/core/locales/es';
import type { DatesSetArg } from '@fullcalendar/core';
import { ChevronLeft, ChevronRight, Plus, X } from 'lucide-react';
import { api, post, type CalendarEvent, type CalendarOptions } from './api';
import { SearchableSelect } from './SearchableSelect';
import './calendar.css';

const types: Record<string, string> = { HEARING: 'Audiencia', MEETING: 'Reunión', DEADLINE: 'Plazo', CALL: 'Llamada', VISIT: 'Visita', OTHER: 'Otro' };
const statuses: Record<string, string> = { SCHEDULED: 'Programado', COMPLETED: 'Completado', CANCELLED: 'Cancelado' };
const views = { dayGridMonth: 'Mes', timeGridWeek: 'Semana', timeGridDay: 'Día' };
const plugins = [dayGridPlugin, timeGridPlugin];
const calendarViews = { dayGridMonth: { dayHeaderFormat: { weekday: 'short' as const } } };
const errorText = (e: unknown) => e instanceof Error ? e.message : 'No se pudo completar la operación.';
const localValue = (value: string) => { const d = new Date(value); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0,16); };
const nextCivilDate = (value: string) => { const d = new Date(`${value.slice(0,10)}T00:00:00Z`); d.setUTCDate(d.getUTCDate()+1); return d.toISOString().slice(0,10); };
const eventPeriod = (event: CalendarEvent) => event.allDay
  ? `${event.startsAt.slice(0,10)}${event.endsAt.slice(0,10) !== event.startsAt.slice(0,10) ? ` — ${event.endsAt.slice(0,10)}` : ''} · Todo el día`
  : `${new Date(event.startsAt).toLocaleString('es-PA')} — ${new Date(event.endsAt).toLocaleString('es-PA')}`;

export function Calendar({ caseId, caseName }: { caseId?: string; caseName?: string }) {
  const calendar = useRef<FullCalendar>(null); const dialog = useRef<HTMLDialogElement>(null);
  const [range, setRange] = useState({ from: '', to: '', title: '' }); const [view, setView] = useState('dayGridMonth');
  const [events, setEvents] = useState<CalendarEvent[]>([]); const [options, setOptions] = useState<CalendarOptions>({ cases: [], responsibleMemberships: [] });
  const [filters, setFilters] = useState({ caseId: '', assignedMembershipId: '', eventType: '', status: '' });
  const [loading, setLoading] = useState(true); const [optionsLoading, setOptionsLoading] = useState(true); const [optionsError, setOptionsError] = useState('');
  const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [busy, setBusy] = useState(false);
  const [open, setOpen] = useState(false); const [selected, setSelected] = useState<CalendarEvent | null>(null);
  const [allDay, setAllDay] = useState(false); const [starts, setStarts] = useState(''); const [ends, setEnds] = useState(''); const [revision, setRevision] = useState(0);
  const [small, setSmall] = useState(() => window.matchMedia('(max-width: 600px)').matches);
  useEffect(() => { const media = window.matchMedia('(max-width: 600px)'); const update = () => setSmall(media.matches); media.addEventListener('change',update); return () => media.removeEventListener('change',update); }, []);
  useEffect(() => { let live = true; setOptionsLoading(true); setOptionsError('');
    api<CalendarOptions>('/calendar/options').then(x => { if (live) setOptions(x); }).catch(e => { if (live) setOptionsError(errorText(e)); })
      .finally(() => { if (live) setOptionsLoading(false); }); return () => { live = false; };
  }, [caseId]);
  const query = new URLSearchParams(Object.entries({ ...filters, caseId: caseId || filters.caseId, from: range.from, to: range.to }).filter(([,v]) => v)).toString();
  useEffect(() => { if (!range.from) return; let live = true; setLoading(true); setError(''); setEvents([]);
    api<CalendarEvent[]>(`/calendar/events?${query}`).then(x => { if (live) setEvents(x); })
      .catch(e => { if (live) setError(errorText(e)); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [query, revision, range.from]);
  useEffect(() => { if (open && !dialog.current?.open) dialog.current?.showModal(); else if (!open) dialog.current?.close(); }, [open]);
  const datesSet = useCallback((info: DatesSetArg) => {
    setView(info.view.type); setRange(previous => previous.from === info.startStr && previous.to === info.endStr && previous.title === info.view.title ? previous : { from: info.startStr, to: info.endStr, title: info.view.title });
  }, []);
  const calendarEvents = useMemo(() => events.map(event => ({ id: event.id, title: `${event.title} · ${statuses[event.status]}`,
    start: event.allDay ? event.startsAt.slice(0,10) : event.startsAt,
    end: event.allDay ? nextCivilDate(event.endsAt) : event.endsAt, allDay: event.allDay,
    classNames: [`calendar-status-${event.status.toLowerCase()}`] })), [events]);

  function show(event: CalendarEvent | null = null) {
    setSelected(event); setAllDay(event?.allDay ?? false); setError('');
    const now = new Date(); now.setSeconds(0,0);
    setStarts(event ? event.allDay ? event.startsAt.slice(0,10) : localValue(event.startsAt) : localValue(now.toISOString()));
    setEnds(event ? event.allDay ? event.endsAt.slice(0,10) : localValue(event.endsAt) : localValue(new Date(now.getTime()+3600000).toISOString()));
    setOpen(true);
  }
  function close() { if (!busy) setOpen(false); }
  function toggleAllDay(value: boolean) {
    setAllDay(value); setStarts(value ? starts.slice(0,10) : `${starts}T09:00`); setEnds(value ? ends.slice(0,10) : `${ends}T10:00`);
  }
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setError(''); setSuccess('');
    if (!starts || !ends || ends < starts) { setError('El fin debe ser igual o posterior al inicio.'); return; }
    const d = new FormData(e.currentTarget);
    const body = { title: d.get('title'), caseId: caseId || d.get('caseId') || null, eventType: d.get('eventType'), status: d.get('status'),
      assignedMembershipId: d.get('assignedMembershipId') || null, startsAt: allDay ? `${starts}T00:00:00Z` : new Date(starts).toISOString(),
      endsAt: allDay ? `${ends}T00:00:00Z` : new Date(ends).toISOString(), allDay, location: d.get('location'), meetingUrl: d.get('meetingUrl'), description: d.get('description') };
    setBusy(true);
    try { if (selected) await api(`/calendar/events/${selected.id}`, { method: 'PUT', body: JSON.stringify(body) }); else await post('/calendar/events',body);
      setOpen(false); setSuccess(selected ? 'Evento actualizado.' : 'Evento creado.'); setRevision(x => x+1);
    } catch (err) { setError(errorText(err)); } finally { setBusy(false); }
  }
  async function changeStatus(status: string) {
    if (!selected) return; setBusy(true); setError(''); setSuccess('');
    try { const saved = await api<CalendarEvent>(`/calendar/events/${selected.id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) });
      setSelected(saved); setOpen(false); setSuccess(status === 'COMPLETED' ? 'Evento completado.' : 'Evento cancelado.'); setRevision(x => x+1);
    } catch(e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  const eventCases = useMemo(() => selected?.caseId && !options.cases.some(x => x.id === selected.caseId) ? [...options.cases,{id:selected.caseId,name:selected.caseName || 'Caso del evento'}] : options.cases, [options.cases, selected?.caseId, selected?.caseName]);
  const members = useMemo(() => selected?.assignedMembershipId && !options.responsibleMemberships.some(x => x.id === selected.assignedMembershipId)
    ? [...options.responsibleMemberships,{ id: selected.assignedMembershipId, name: `${selected.assignedName || 'Responsable'} (inactivo: reasignar)` }] : options.responsibleMemberships, [options.responsibleMemberships, selected?.assignedMembershipId, selected?.assignedName]);
  return <div className="legal-calendar">{!caseId && <div className="page-heading"><div><span className="eyebrow">AGENDA JURÍDICA</span><h1>Agenda y calendario</h1><p className="muted">Audiencias, reuniones y plazos de tu organización.</p></div></div>}
    {!open && error && <div className="alert" role="alert">{error}</div>}{success && <div className="alert success" role="status">{success}</div>}
    <section className="panel calendar-panel"><div className="calendar-toolbar"><div className="calendar-navigation"><button className="button secondary" aria-label="Periodo anterior" onClick={() => calendar.current?.getApi().prev()}><ChevronLeft size={18}/></button><button className="button secondary" onClick={() => calendar.current?.getApi().today()}>Hoy</button><button className="button secondary" aria-label="Periodo siguiente" onClick={() => calendar.current?.getApi().next()}><ChevronRight size={18}/></button></div><h2 aria-live="polite">{range.title || 'Agenda'}</h2><button className="button calendar-new" onClick={() => show()}><Plus size={16}/>Nuevo evento</button><div className="calendar-views" role="group" aria-label="Vista del calendario">{Object.entries(views).map(([key,name]) => <button key={key} className={`button ${view === key ? '' : 'secondary'}`} aria-pressed={view === key} onClick={() => calendar.current?.getApi().changeView(key)}>{name}</button>)}</div></div>
      <div className="calendar-filters">{!caseId && <SearchableSelect label="Filtrar caso" name="filterCaseId" value={filters.caseId} options={options.cases} loading={optionsLoading} error={optionsError} onChange={value => setFilters(x => ({...x,caseId:value}))}/>}
        <SearchableSelect label="Filtrar responsable" name="filterMemberId" value={filters.assignedMembershipId} options={options.responsibleMemberships} loading={optionsLoading} error={optionsError} onChange={value => setFilters(x => ({...x,assignedMembershipId:value}))}/>
        <label className="field">Tipo<select aria-label="Filtrar tipo" value={filters.eventType} onChange={e => setFilters(x => ({...x,eventType:e.target.value}))}><option value="">Todos</option>{Object.entries(types).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label><label className="field">Estado<select aria-label="Filtrar estado" value={filters.status} onChange={e => setFilters(x => ({...x,status:e.target.value}))}><option value="">Todos</option>{Object.entries(statuses).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label></div>
      <p className="calendar-timezone muted">Zona horaria: {Intl.DateTimeFormat().resolvedOptions().timeZone}. Los eventos de todo el día mantienen su fecha.</p>
      {loading && <div className="loading" role="status"><span className="spinner"/>Cargando eventos…</div>}
      <FullCalendar ref={calendar} plugins={plugins} views={calendarViews} locale={esLocale} initialView="dayGridMonth" timeZone="local" headerToolbar={false} datesSet={datesSet}
        events={calendarEvents} eventClick={info => { const event = events.find(x => x.id === info.event.id); if (event) show(event); }}
        height={view === 'dayGridMonth' ? 'auto' : 620} dayMaxEvents={small ? 2 : 3} nowIndicator firstDay={1}
        dayHeaderFormat={small ? {weekday:'narrow',day:'numeric'} : {weekday:'short',day:'numeric'}}
        eventTimeFormat={{hour:'2-digit',minute:'2-digit',hour12:false}} slotLabelFormat={{hour:'2-digit',minute:'2-digit',hour12:false}}
        slotDuration="01:00:00" scrollTime="08:00:00" eventMinHeight={24} slotEventOverlap={false} />
      {!loading && !error && events.length === 0 && <div className="empty-state calendar-empty"><h3>Sin eventos en este periodo</h3><p>Crea un evento o ajusta los filtros.</p></div>}
      {events.length > 0 && <div className="calendar-event-list"><h3>Eventos del periodo</h3>{events.map(event => <button type="button" className={`calendar-event-card calendar-status-${event.status.toLowerCase()}`} key={event.id} onClick={() => show(event)}><strong>{event.title}</strong><span>{types[event.eventType]} · {statuses[event.status]}</span><span>{eventPeriod(event)}</span><span>{event.caseName || 'Sin caso'} · {event.assignedName || 'Sin responsable'}</span>{event.location && <span>{event.location}</span>}</button>)}</div>}
    </section>
    <dialog ref={dialog} className="catalog-dialog calendar-dialog" aria-label={selected ? 'Editar evento' : 'Nuevo evento'} onClose={() => setOpen(false)} onCancel={e => { if (busy) e.preventDefault(); }}><div className="catalog-dialog-heading"><h2>{selected ? 'Editar evento' : 'Nuevo evento'}</h2><button className="icon-button" disabled={busy} type="button" aria-label="Cerrar evento" onClick={close}><X size={20}/></button></div>
      {open && <form className="catalog-modal-form" key={selected?.id || 'new'} onSubmit={save}>{error && <div className="alert" role="alert">{error}</div>}
        <label className="field">Título *<input autoFocus required name="title" maxLength={240} defaultValue={selected?.title}/></label>
        {caseId ? <label className="field">Caso<input readOnly value={caseName || options.cases.find(x => x.id === caseId)?.name || 'Caso actual'}/></label> : <SearchableSelect label="Caso" name="caseId" value={selected?.caseId} options={eventCases} loading={optionsLoading} error={optionsError}/>}
        <div className="form-row"><label className="field">Tipo *<select required aria-label="Tipo de evento" name="eventType" defaultValue={selected?.eventType || 'HEARING'}>{Object.entries(types).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label><label className="field">Estado<select aria-label="Estado del evento" name="status" defaultValue={selected?.status || 'SCHEDULED'}>{Object.entries(statuses).map(([v,n]) => <option key={v} value={v}>{n}</option>)}</select></label></div>
        <SearchableSelect label="Responsable" name="assignedMembershipId" value={selected?.assignedMembershipId} options={members} loading={optionsLoading} error={optionsError}/>
        <label className="calendar-all-day"><input name="allDay" type="checkbox" checked={allDay} onChange={e => toggleAllDay(e.target.checked)}/>Todo el día</label>
        <div className="form-row"><label className="field">Inicio *<input required aria-label="Inicio" type={allDay ? 'date' : 'datetime-local'} value={starts} onChange={e => setStarts(e.target.value)} name="startsAt"/></label><label className="field">Fin *<input required aria-label="Fin" type={allDay ? 'date' : 'datetime-local'} min={starts} value={ends} onChange={e => setEnds(e.target.value)} name="endsAt"/></label></div>
        {allDay && <p className="muted">La fecha de fin es el último día incluido en el evento.</p>}
        <label className="field">Ubicación<input name="location" maxLength={500} defaultValue={selected?.location}/></label><label className="field">URL de reunión<input type="url" name="meetingUrl" placeholder="https://" maxLength={2048} defaultValue={selected?.meetingUrl}/></label><label className="field">Descripción<textarea name="description" maxLength={4000} rows={4} defaultValue={selected?.description}/></label>
        {selected && <div className="calendar-status-actions">{selected.status !== 'COMPLETED' && <button className="button secondary" type="button" disabled={busy} onClick={() => changeStatus('COMPLETED')}>Completar evento</button>}{selected.status !== 'CANCELLED' && <button className="button secondary" type="button" disabled={busy} onClick={() => changeStatus('CANCELLED')}>Cancelar evento</button>}</div>}
        <div className="filter-actions"><button className="button secondary" type="button" disabled={busy} onClick={close}>Cerrar</button><button className="button" disabled={busy || optionsLoading || !!optionsError}>{busy ? 'Guardando…' : 'Guardar evento'}</button></div>
      </form>}
    </dialog>
  </div>;
}
