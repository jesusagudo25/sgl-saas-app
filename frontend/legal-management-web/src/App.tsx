import { useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { Link, Navigate, NavLink, Outlet, Route, Routes, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, ArrowRight, ArrowUpRight, Building2, CalendarDays, Check, ChevronDown, Eye, FileText, FolderOpen, LayoutDashboard, LogOut, Mail, Pencil, Plus, Scale, Search, Settings, ShieldCheck, Users } from 'lucide-react';
import { api, post, type Client, type Invitation, type Member, type Organization, type Page } from './api';
import { useAuth, useOrganizations } from './contexts';
import { CaseDetail, CaseForm, Cases } from './Cases';

const roleName: Record<string, string> = { ADMIN: 'Administrador', LAWYER: 'Abogado', ASSISTANT: 'Asistente', READONLY: 'Solo lectura' };
const errorText = (e: unknown) => e instanceof Error ? e.message : 'No se pudo completar la operación.';
function Brand() { return <Link to="/app" className="brand"><span className="brand-mark"><Scale size={24}/></span><span>SGL<span className="brand-caption">GESTIÓN LEGAL</span></span></Link>; }
function Alert({ children, success = false }: { children: ReactNode; success?: boolean }) {
  return children ? <div className={`alert ${success ? 'success' : ''}`} role={success ? 'status' : 'alert'}>{children}</div> : null;
}
function Loading() { return <div className="loading" role="status"><span className="spinner"/>Cargando tu espacio…</div>; }
function Field({ label, name, type = 'text', autoComplete, minLength, defaultValue, readOnly = false }: {
  label: string; name: string; type?: string; autoComplete?: string; minLength?: number; defaultValue?: string; readOnly?: boolean;
}) { return <label className="field">{label}<input name={name} type={type} required autoComplete={autoComplete} minLength={minLength}
  maxLength={type === 'password' ? 128 : 256} defaultValue={defaultValue} readOnly={readOnly}/></label>; }
function AuthLayout() {
  return <div className="auth-layout"><aside className="auth-story"><Brand/><div className="story-content"><span className="eyebrow">UN ESPACIO PARA TU FIRMA</span>
    <h1>La base de una<br/>práctica organizada.</h1><p>Tu equipo, conectado.<br/>Tu organización, en su propio espacio.</p>
    <div className="story-art"><div className="art-line"/><Scale size={112} strokeWidth={.7}/><span>CLARIDAD · CONFIANZA · COLABORACIÓN</span></div>
    </div><div className="story-footer"><ShieldCheck size={18}/> Acceso seguro para cada organización</div></aside>
    <main className="auth-main"><div className="mobile-brand"><Brand/></div><Outlet/><footer>SGL · Sistema de Gestión Legal</footer></main></div>;
}
function AuthForm({ mode }: { mode: 'login' | 'register' | 'forgot' | 'reset' }) {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const location = useLocation();
  const resetParams = new URLSearchParams(location.hash.slice(1));
  const next = params.get('next');
  const destination = next?.startsWith('/invitations/') && !next.includes('://') ? next : '/app';
  const suffix = next ? `?next=${encodeURIComponent(destination)}` : '';
  const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [busy, setBusy] = useState(false);
  const titles = { login: 'Bienvenido de nuevo', register: 'Crea tu cuenta', forgot: 'Recupera tu acceso', reset: 'Elige una nueva contraseña' };
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setSuccess(''); setBusy(true);
    const data = new FormData(event.currentTarget); const value = (key: string) => String(data.get(key) ?? '');
    try {
      if ((mode === 'register' || mode === 'reset') && value('password') !== value('confirm')) throw new Error('Las contraseñas no coinciden.');
      if (mode === 'login') { await login(value('email'), value('password'), data.has('remember')); navigate(destination, { replace: true }); }
      if (mode === 'register') {
        await post('/auth/register', { firstName: value('firstName'), lastName: value('lastName'), email: value('email'), password: value('password') }, false);
        await login(value('email'), value('password'), false); navigate(destination, { replace: true });
      }
      if (mode === 'forgot') {
        await post('/auth/forgot-password', { email: value('email') }, false);
        setSuccess('Si existe una cuenta con este correo, recibirás un enlace para recuperar tu contraseña.');
      }
      if (mode === 'reset') {
        await post('/auth/reset-password', { email: value('email'), token: resetParams.get('token') ?? params.get('token'), password: value('password') }, false);
        setSuccess('Contraseña actualizada. Ya puedes iniciar sesión.');
      }
    } catch (e) { setError(errorText(e)); } finally { setBusy(false); }
  }
  return <section className="auth-card" key={mode}><span className="eyebrow">TU ESPACIO LEGAL</span><h2>{titles[mode]}</h2>
    <p className="muted">{mode === 'login' ? 'Ingresa tus datos para continuar con tu organización.' : mode === 'register' ? 'Empieza con tu cuenta. Después, crea o únete a una organización.' : 'Te ayudamos a volver a tu espacio de trabajo.'}</p>
    <Alert>{error}</Alert><Alert success>{success}</Alert>
    <form onSubmit={submit}>
      {mode === 'register' && <div className="form-row"><Field label="Nombre" name="firstName" autoComplete="given-name"/><Field label="Apellido" name="lastName" autoComplete="family-name"/></div>}
      <Field label="Correo electrónico" name="email" type="email" autoComplete="email" defaultValue={mode === 'reset' ? resetParams.get('email') ?? params.get('email') ?? '' : undefined}/>
      {mode !== 'forgot' && <Field label="Contraseña" name="password" type="password" autoComplete={mode === 'login' ? 'current-password' : 'new-password'} minLength={mode === 'login' ? undefined : 10}/>}
      {(mode === 'register' || mode === 'reset') && <><p className="field-hint">Al menos 10 caracteres, con mayúscula, minúscula, número y símbolo.</p><Field label="Confirmar contraseña" name="confirm" type="password" autoComplete="new-password" minLength={10}/></>}
      {mode === 'login' && <div className="form-options"><label className="checkbox"><input type="checkbox" name="remember"/>Recordar sesión</label><Link to="/forgot-password">¿Olvidaste tu contraseña?</Link></div>}
      <button className="button full" disabled={busy}>{busy ? 'Un momento…' : mode === 'login' ? 'Iniciar sesión' : mode === 'register' ? 'Crear cuenta' : mode === 'forgot' ? 'Enviar enlace' : 'Guardar contraseña'}<ArrowRight size={17}/></button>
    </form>
    <p className="auth-link">{mode === 'login' ? <>¿Aún no tienes cuenta? <Link to={`/register${suffix}`}>Crear una cuenta</Link></> : <Link to={`/login${suffix}`}>Volver a iniciar sesión</Link>}</p>
    {mode === 'login' && <div className="invitation-note"><Mail size={20}/><p>¿Recibiste una invitación?<br/><span>Abre el enlace de tu correo para unirte a tu equipo.</span></p></div>}
  </section>;
}
function Protected() {
  const auth = useAuth();
  return auth.loading ? <Loading/> : auth.authenticated ? <Outlet/> : <Navigate to="/login" replace/>;
}
function OrganizationGuard() {
  const { activeOrganization, organizations, loading, error } = useOrganizations();
  if (loading) return <Loading/>;
  if (error) return <div className="center-page"><Alert>{error}</Alert><Link to="/select-organization">Volver al selector</Link></div>;
  return activeOrganization ? <Outlet/> : <Navigate to={organizations.length ? '/select-organization' : '/onboarding'} replace/>;
}
function OrganizationPage({ create = false }: { create?: boolean }) {
  const { organizations, selectOrganization, reload, loading } = useOrganizations();
  const { user, logout } = useAuth(); const navigate = useNavigate();
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function enter(org: Organization) { setBusy(true); setError(''); try { await selectOrganization(org); navigate('/app'); } catch (e) { setError(errorText(e)); } finally { setBusy(false); } }
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setBusy(true); setError('');
    try { const org = await post<Organization>('/organizations', { name: new FormData(e.currentTarget).get('name') });
      await reload(); await selectOrganization(org); navigate('/app');
    } catch (err) { setError(errorText(err)); } finally { setBusy(false); }
  }
  return <div className="organization-page"><header><Brand/><button className="text-button" onClick={() => logout().catch(e => setError(errorText(e)))}>Cerrar sesión <LogOut size={16}/></button></header>
    <main className="organization-content"><div className="large-icon"><Building2 size={28}/></div><span className="eyebrow">HOLA, {user?.firstName.toUpperCase()}</span>
      <h1>{create ? 'Dale un espacio a tu firma.' : '¿Dónde trabajamos hoy?'}</h1><p className="muted">{create ? 'Crea tu organización para empezar a colaborar con tu equipo.' : 'Selecciona una organización. Cada una tiene su propio equipo y permisos.'}</p>
      <Alert>{error}</Alert>{loading ? <Loading/> : create ? <form className="panel create-form" onSubmit={submit}><Field label="Nombre de la organización" name="name"/><p className="field-hint">Serás el administrador de esta organización.</p><button className="button full" disabled={busy}>Crear organización <ArrowRight size={17}/></button></form> :
      <div className="organization-list">{organizations.map(org => <div className="panel organization-item" key={org.id}><div className="org-avatar">{org.name.slice(0,2).toUpperCase()}</div><div><h3>{org.name}</h3><span className="muted">{roleName[org.roleCode]}</span></div><button className="button secondary" disabled={busy} onClick={() => enter(org)}>Entrar <ArrowRight size={16}/></button></div>)}</div>}
      {create ? <Link className="below-link" to="/select-organization">Volver a mis organizaciones</Link> : <Link className="button secondary" to="/onboarding"><Plus size={17}/>Crear nueva organización</Link>}
    </main></div>;
}
function Layout() {
  const { user, logout } = useAuth(); const { activeOrganization: org } = useOrganizations(); const [error, setError] = useState('');
  return <div className="app-layout"><aside className="sidebar"><Brand/><div className="sidebar-section">ESPACIO DE TRABAJO</div>
    <Link className="org-switch" to="/select-organization"><span className="org-avatar small">{org!.name.slice(0,2).toUpperCase()}</span><span>{org!.name}<small>Cambiar organización</small></span><ChevronDown size={16}/></Link>
    <nav><NavLink to="/app" end><LayoutDashboard size={19}/>Dashboard</NavLink><NavLink to="/app/clients"><Users size={19}/>Clientes</NavLink><NavLink to="/app/cases"><FolderOpen size={19}/>Casos</NavLink>
      {[[CalendarDays, 'Agenda'], [FileText, 'Documentos']].map(([Icon, name]) => { const I = Icon as typeof Users; return <button key={String(name)} disabled><I size={19}/>{String(name)}<span className="soon">Próximamente</span></button>; })}
      <NavLink to="/app/team"><Users size={19}/>Equipo</NavLink><button disabled><Settings size={19}/>Configuración</button></nav>
    <div className="sidebar-bottom"><ShieldCheck size={20}/><p>Tu organización.<br/><strong>Un espacio seguro.</strong></p></div></aside>
    <div className="app-body"><header className="topbar"><span>Mi espacio <span className="breadcrumb">/ {org!.name}</span></span><div className="user-menu"><span className="avatar">{user!.firstName[0]}{user!.lastName[0]}</span><div>{user!.firstName} {user!.lastName}<small>{roleName[org!.roleCode]}</small></div><button title="Cerrar sesión" aria-label="Cerrar sesión" className="icon-button" onClick={() => logout().catch(e => setError(errorText(e)))}><LogOut size={19}/></button></div></header>
    <main className="workspace"><Alert>{error}</Alert><Outlet key={org!.id}/></main><footer className="app-footer">SGL · Gestión Legal <span>Un buen comienzo para tu organización.</span></footer></div></div>;
}
function Dashboard() {
  const { user } = useAuth(); const { activeOrganization: org } = useOrganizations();
  return <><div className="page-heading"><div><span className="eyebrow">TU ESPACIO DE TRABAJO</span><h1>Bienvenido, {user!.firstName}.</h1><p className="muted">Todo empieza con un equipo bien conectado.</p></div><span className="status"><span/>Organización activa</span></div>
    <section className="welcome-panel"><div><span className="eyebrow">LISTOS PARA EMPEZAR</span><h2>La próxima etapa de tu firma<br/>empieza aquí.</h2><p>Ya tienes un espacio para tu organización.<br/>Reúne a tu equipo y construyan juntos lo que sigue.</p><Link className="button light" to="/app/team">{org!.roleCode === 'ADMIN' ? 'Organizar mi equipo' : 'Conocer a mi equipo'}<ArrowRight size={17}/></Link></div><div className="welcome-symbol"><Scale size={140} strokeWidth={.8}/></div></section>
    <div className="summary-grid"><section className="panel summary"><div className="small-icon"><Building2 size={21}/></div><span className="muted">Organización activa</span><h3>{org!.name}</h3><Link to="/select-organization">Cambiar organización <ArrowUpRight size={15}/></Link></section>
    <section className="panel summary"><div className="small-icon"><ShieldCheck size={21}/></div><span className="muted">Tu rol en este espacio</span><h3>{roleName[org!.roleCode]}</h3><p className="muted">Permisos definidos por tu membresía.</p></section></div>
    <section className="next-section"><span className="eyebrow">PRIMEROS PASOS</span><h2>Haz de este espacio tu lugar de trabajo</h2><div className="panel next-action"><div className="small-icon"><Users size={25}/></div><div><h3>El trabajo es mejor en equipo</h3><p className="muted">{org!.roleCode === 'ADMIN' ? 'Invita a los miembros de tu firma y asigna su rol.' : 'Consulta quiénes forman parte de tu organización.'}</p></div><Link className="button secondary" to="/app/team">Ir a equipo <ArrowRight size={16}/></Link></div></section>
    <div className="quiet-note"><Check size={17}/>Tu cuenta y tu organización están listas. Los módulos de gestión llegarán en próximas etapas.</div></>;
}
function Team() {
  const { activeOrganization: org } = useOrganizations(); const [members, setMembers] = useState<Member[]>([]);
  const [error, setError] = useState(''); const [success, setSuccess] = useState(''); const [busy, setBusy] = useState(false); const [loading, setLoading] = useState(true);
  useEffect(() => { let live = true; api<Member[]>(`/organizations/${org!.id}/members`).then(items => { if (live) setMembers(items); })
    .catch(e => { if (live) setError(errorText(e)); }).finally(() => { if (live) setLoading(false); }); return () => { live = false; }; }, [org!.id]);
  async function invite(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); const form = e.currentTarget; const data = new FormData(form); setError(''); setSuccess(''); setBusy(true);
    try { await post(`/organizations/${org!.id}/invitations`, { email: data.get('email'), roleCode: data.get('roleCode') });
      setSuccess('Invitación enviada. La persona recibirá un enlace para unirse a la organización.'); form.reset();
    } catch (err) { setError(errorText(err)); } finally { setBusy(false); }
  }
  return <><div className="page-heading"><div><span className="eyebrow">PERSONAS Y COLABORACIÓN</span><h1>Tu equipo</h1><p className="muted">Las personas que forman parte de {org!.name}.</p></div><span className="badge">{roleName[org!.roleCode]}</span></div><Alert>{error}</Alert><Alert success>{success}</Alert>
    {org!.roleCode === 'ADMIN' && <section className="panel invite-panel"><h2>Invita a un nuevo miembro</h2><p className="muted">Envía una invitación y define su rol dentro de esta organización.</p><form onSubmit={invite} className="invite-form"><Field name="email" label="Correo electrónico" type="email"/><label className="field">Rol<select name="roleCode" defaultValue="LAWYER">{Object.entries(roleName).map(([code,name]) => <option key={code} value={code}>{name}</option>)}</select></label><button className="button" disabled={busy}><Mail size={17}/>{busy ? 'Enviando…' : 'Enviar invitación'}</button></form></section>}
    <section className="panel members-panel"><div className="table-heading"><h2>Miembros de la organización</h2><span className="count">{members.length}</span></div>{loading ? <Loading/> : <div className="table-scroll"><table><thead><tr><th>Miembro</th><th>Correo electrónico</th><th>Rol</th><th>Estado</th></tr></thead><tbody>{members.map(m => <tr key={m.userId}><td><div className="member-name"><span className="avatar">{m.firstName[0]}{m.lastName[0]}</span>{m.firstName} {m.lastName}</div></td><td>{m.email}</td><td><span className="badge">{roleName[m.roleCode]}</span></td><td><span className="status"><span/>{m.status === 'ACTIVE' ? 'Activo' : 'Suspendido'}</span></td></tr>)}</tbody></table></div>}</section></>;
}
function Clients() {
  const [params, setParams] = useSearchParams(); const [page, setPage] = useState<Page<Client> | null>(null);
  const [error, setError] = useState(''); const [loading, setLoading] = useState(true);
  const query = params.toString();
  useEffect(() => { let live = true; setLoading(true); setError(''); api<Page<Client>>(`/clients${query ? `?${query}` : ''}`)
    .then(data => { if (live) setPage(data); }).catch(e => { if (live) setError(errorText(e)); })
    .finally(() => { if (live) setLoading(false); }); return () => { live = false; }; }, [query]);
  function filter(e: FormEvent<HTMLFormElement>) { e.preventDefault(); const data = new FormData(e.currentTarget); const next = new URLSearchParams();
    for (const key of ['search', 'type', 'status']) { const value = String(data.get(key) ?? '').trim(); if (value) next.set(key, value); } setParams(next); }
  return <><div className="page-heading"><div><span className="eyebrow">GESTIÓN DE CLIENTES</span><h1>Clientes</h1><p className="muted">Personas y empresas de tu organización.</p></div>
    <Link className="button" to="/app/clients/new"><Plus size={17}/>Nuevo cliente</Link></div><Alert>{error}</Alert>
    <form className="panel client-filters" onSubmit={filter}><label><Search size={17}/><input name="search" aria-label="Buscar clientes" placeholder="Nombre, identificación, correo o teléfono" defaultValue={params.get('search') ?? ''}/></label>
      <select name="type" aria-label="Filtrar por tipo" defaultValue={params.get('type') ?? ''}><option value="">Todos los tipos</option><option value="PERSON">Persona natural</option><option value="COMPANY">Persona jurídica</option></select>
      <select name="status" aria-label="Filtrar por estado" defaultValue={params.get('status') ?? ''}><option value="">Todos los estados</option><option value="ACTIVE">Activos</option><option value="INACTIVE">Inactivos</option></select>
      <button className="button secondary">Filtrar</button></form>
    <section className="panel clients-table"><div className="table-heading"><h2>Directorio</h2><span className="count">{page?.totalCount ?? 0}</span></div>{loading ? <Loading/> : !page?.items.length ? <div className="empty-state"><Users size={32}/><h3>No hay clientes para mostrar</h3><p>Agrega un cliente o modifica los filtros.</p></div> : <div className="table-scroll"><table><thead><tr><th>Nombre / Razón social</th><th>Tipo</th><th>Identificación</th><th>Correo</th><th>Teléfono</th><th>Estado</th><th>Última actualización</th><th>Acción</th></tr></thead><tbody>{page.items.map(client => <tr key={client.id}><td><strong>{client.displayName}</strong></td><td>{client.type === 'PERSON' ? 'Persona' : 'Empresa'}</td><td>{client.identificationType} · {client.identificationNumber}</td><td>{client.email || '—'}</td><td>{client.phone || '—'}</td><td><span className={`client-status ${client.status.toLowerCase()}`}>{client.status === 'ACTIVE' ? 'Activo' : 'Inactivo'}</span></td><td>{new Date(client.updatedAt).toLocaleDateString('es')}</td><td><Link className="icon-button" title="Ver cliente" to={`/app/clients/${client.id}`}><Eye size={17}/></Link></td></tr>)}</tbody></table></div>}
      {!!page?.totalPages && page.totalPages > 1 && <div className="pagination"><button disabled={page.page === 1} onClick={() => { const next = new URLSearchParams(params); next.set('page', String(page.page - 1)); setParams(next); }}>Anterior</button><span>Página {page.page} de {page.totalPages}</span><button disabled={page.page === page.totalPages} onClick={() => { const next = new URLSearchParams(params); next.set('page', String(page.page + 1)); setParams(next); }}>Siguiente</button></div>}</section></>;
}

function ClientForm() {
  const { id } = useParams(); const editing = !!id; const navigate = useNavigate(); const [client, setClient] = useState<Client | null>(null);
  const [type, setType] = useState<'PERSON' | 'COMPANY'>('PERSON'); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  useEffect(() => { if (!id) return; api<Client>(`/clients/${id}`).then(value => { setClient(value); setType(value.type); }).catch(e => setError(errorText(e))); }, [id]);
  async function save(e: FormEvent<HTMLFormElement>) { e.preventDefault(); setBusy(true); setError(''); const data = new FormData(e.currentTarget);
    const body = Object.fromEntries(data.entries()); try { const saved = editing ? await api<Client>(`/clients/${id}`, { method: 'PUT', body: JSON.stringify(body) }) : await post<Client>('/clients', body); navigate(`/app/clients/${saved.id}`); }
    catch (err) { setError(errorText(err)); } finally { setBusy(false); } }
  if (editing && !client && !error) return <Loading/>;
  return <><div className="page-heading"><div><Link className="back-link" to={editing ? `/app/clients/${id}` : '/app/clients'}><ArrowLeft size={15}/>Volver</Link><h1>{editing ? 'Editar cliente' : 'Nuevo cliente'}</h1><p className="muted">Información de la persona o empresa.</p></div></div><Alert>{error}</Alert>
    <form className="panel client-form" onSubmit={save}><div className="form-section"><h2>Tipo e identificación</h2><div className="form-row"><label className="field">Tipo<select name="type" value={type} onChange={e => setType(e.target.value as typeof type)}><option value="PERSON">Persona natural</option><option value="COMPANY">Persona jurídica</option></select></label><Field label="Tipo de identificación" name="identificationType" defaultValue={client?.identificationType}/></div><Field label="Número de identificación" name="identificationNumber" defaultValue={client?.identificationNumber}/></div>
      <div className="form-section"><h2>{type === 'PERSON' ? 'Datos personales' : 'Datos de la empresa'}</h2>{type === 'PERSON' ? <div className="form-row"><Field label="Nombre" name="firstName" defaultValue={client?.firstName}/><Field label="Apellido" name="lastName" defaultValue={client?.lastName}/></div> : <><Field label="Razón social" name="legalName" defaultValue={client?.legalName}/><div className="form-row"><Field label="Nombre comercial" name="tradeName" defaultValue={client?.tradeName}/><Field label="Persona de contacto" name="contactPerson" defaultValue={client?.contactPerson}/></div></>}</div>
      <div className="form-section"><h2>Contacto</h2><div className="form-row"><Field label="Correo electrónico" name="email" type="email" defaultValue={client?.email}/><Field label="Teléfono" name="phone" defaultValue={client?.phone}/></div><div className="form-row"><Field label="Teléfono secundario" name="secondaryPhone" defaultValue={client?.secondaryPhone}/><Field label="Dirección" name="address" defaultValue={client?.address}/></div><label className="field">Notas<textarea name="notes" maxLength={2000} defaultValue={client?.notes}/></label></div>
      <div className="form-actions"><Link className="button secondary" to={editing ? `/app/clients/${id}` : '/app/clients'}>Cancelar</Link><button className="button" disabled={busy}>{busy ? 'Guardando…' : 'Guardar cliente'}</button></div></form></>;
}

function ClientDetail() {
  const { id } = useParams(); const [client, setClient] = useState<Client | null>(null); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  useEffect(() => { api<Client>(`/clients/${id}`).then(setClient).catch(e => setError(errorText(e))); }, [id]);
  async function toggle() { if (!client) return; setBusy(true); setError(''); try { const next = client.status === 'ACTIVE' ? 'INACTIVE' : 'ACTIVE';
    setClient(await api<Client>(`/clients/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status: next }) })); } catch (e) { setError(errorText(e)); } finally { setBusy(false); } }
  if (!client && !error) return <Loading/>;
  return <><Alert>{error}</Alert>{client && <><div className="page-heading"><div><Link className="back-link" to="/app/clients"><ArrowLeft size={15}/>Clientes</Link><h1>{client.displayName}</h1><p className="muted">{client.identificationType} · {client.identificationNumber}</p></div><div className="heading-actions"><button className="button secondary" onClick={toggle} disabled={busy}>{client.status === 'ACTIVE' ? 'Desactivar' : 'Activar'}</button><Link className="button" to={`/app/clients/${id}/edit`}><Pencil size={16}/>Editar</Link></div></div>
    <div className="client-detail-grid"><section className="panel detail-card"><h2>Información general</h2><dl><dt>Tipo</dt><dd>{client.type === 'PERSON' ? 'Persona natural' : 'Persona jurídica'}</dd><dt>Estado</dt><dd><span className={`client-status ${client.status.toLowerCase()}`}>{client.status === 'ACTIVE' ? 'Activo' : 'Inactivo'}</span></dd>{client.type === 'COMPANY' && <><dt>Razón social</dt><dd>{client.legalName}</dd><dt>Nombre comercial</dt><dd>{client.tradeName || '—'}</dd><dt>Persona de contacto</dt><dd>{client.contactPerson || '—'}</dd></>}</dl></section>
    <section className="panel detail-card"><h2>Contacto</h2><dl><dt>Correo</dt><dd>{client.email || '—'}</dd><dt>Teléfono</dt><dd>{client.phone || '—'}</dd><dt>Teléfono secundario</dt><dd>{client.secondaryPhone || '—'}</dd><dt>Dirección</dt><dd>{client.address || '—'}</dd></dl></section><section className="panel detail-card full-detail"><h2>Notas</h2><p>{client.notes || 'Sin notas.'}</p><small>Actualizado el {new Date(client.updatedAt).toLocaleString('es')}</small></section></div></>}</>;
}
function InvitationPage() {
  const { token } = useParams(); const { user, loading, logout } = useAuth(); const { reload, selectOrganization } = useOrganizations(); const navigate = useNavigate();
  const [invitation, setInvitation] = useState<Invitation | null>(null); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  useEffect(() => { let live = true; api<Invitation>(`/invitations/${token}`, {}, false).then(i => { if (live) setInvitation(i); }).catch(e => { if (live) setError(errorText(e)); }); return () => { live = false; }; }, [token]);
  async function accept() { setBusy(true); setError(''); try { const org = await post<Organization>(`/invitations/${token}/accept`); await reload(); await selectOrganization(org); navigate('/app'); } catch (e) { setError(errorText(e)); } finally { setBusy(false); } }
  const suffix = `?next=${encodeURIComponent(`/invitations/${token}`)}`;
  return <section className="auth-card"><div className="large-icon"><Mail size={28}/></div><span className="eyebrow">UNA INVITACIÓN PARA TI</span><h2>Tu equipo te espera.</h2><Alert>{error}</Alert>{!invitation || loading ? (!error && <Loading/>) : <><p className="muted">Te invitaron a formar parte de</p><div className="panel invitation-details"><h3>{invitation.organizationName}</h3><span className="badge">{roleName[invitation.roleCode]}</span><p>{invitation.email}</p><small className="muted">Válida hasta {new Date(invitation.expiresAt).toLocaleString('es')}</small></div>{user ? user.email.toUpperCase() === invitation.email.toUpperCase() ? <button className="button full" onClick={accept} disabled={busy}>Aceptar invitación <ArrowRight size={17}/></button> : <><p>Esta invitación es para otra cuenta. Inicia sesión con {invitation.email}.</p><button className="button" onClick={() => logout().catch(e => setError(errorText(e)))}>Cambiar de cuenta</button></> : <><Link className="button full" to={`/register${suffix}`}>Crear cuenta y continuar <ArrowRight size={17}/></Link><p className="auth-link">¿Ya tienes cuenta? <Link to={`/login${suffix}`}>Iniciar sesión</Link></p></>}</>}</section>;
}
export default function App() {
  return <Routes><Route element={<AuthLayout/>}><Route path="/login" element={<AuthForm key="login" mode="login"/>}/><Route path="/register" element={<AuthForm key="register" mode="register"/>}/><Route path="/forgot-password" element={<AuthForm key="forgot" mode="forgot"/>}/><Route path="/reset-password" element={<AuthForm key="reset" mode="reset"/>}/><Route path="/invitations/:token" element={<InvitationPage/>}/></Route>
    <Route element={<Protected/>}><Route path="/onboarding" element={<OrganizationPage create/>}/><Route path="/select-organization" element={<OrganizationPage/>}/><Route element={<OrganizationGuard/>}><Route path="/app" element={<Layout/>}><Route index element={<Dashboard/>}/><Route path="clients" element={<Clients/>}/><Route path="clients/new" element={<ClientForm/>}/><Route path="clients/:id" element={<ClientDetail/>}/><Route path="clients/:id/edit" element={<ClientForm/>}/><Route path="cases" element={<Cases/>}/><Route path="cases/new" element={<CaseForm/>}/><Route path="cases/:id" element={<CaseDetail/>}/><Route path="cases/:id/edit" element={<CaseForm/>}/><Route path="team" element={<Team/>}/></Route></Route></Route>
    <Route path="/" element={<Navigate to="/app" replace/>}/><Route path="*" element={<div className="center-page"><h1>Página no encontrada</h1><Link to="/app">Volver a mi espacio</Link></div>}/></Routes>;
}
