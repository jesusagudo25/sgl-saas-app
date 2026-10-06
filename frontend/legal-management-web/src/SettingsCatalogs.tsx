import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Pencil, Plus, X } from 'lucide-react';
import { api, post, type CatalogItem } from './api';
import { SearchableSelect } from './SearchableSelect';

const labels = { 'case-statuses': 'Estados de caso', 'case-types': 'Tipos de caso', courts: 'Tribunales / Juzgados', jurisdictions: 'Jurisdicciones', competences: 'Competencias' } as const;
type Key = keyof typeof labels;

export function SettingsCatalogs() {
  const [kind, setKind] = useState<Key>('case-statuses');
  const [items, setItems] = useState<CatalogItem[]>([]);
  const [editing, setEditing] = useState<CatalogItem | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const dialogRef = useRef<HTMLDialogElement>(null);
  async function load() {
    setLoading(true);
    try { setItems(await api<CatalogItem[]>(`/settings/${kind}`)); }
    catch (e) { setError(e instanceof Error ? e.message : 'No se pudo cargar el catálogo.'); }
    finally { setLoading(false); }
  }
  useEffect(() => { setSearch(''); setError(''); setSuccess(''); void load(); }, [kind]);
  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;
    if (formOpen && !dialog.open) dialog.showModal();
    if (!formOpen && dialog.open) dialog.close();
  }, [formOpen]);
  function open(item: CatalogItem | null) { setEditing(item); setError(''); setFormOpen(true); }
  function close() { if (!busy) { setFormOpen(false); setEditing(null); } }
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); setBusy(true); setError(''); setSuccess('');
    const d = new FormData(e.currentTarget);
    const body = kind === 'case-statuses'
      ? { name: d.get('name'), code: d.get('code'), sortOrder: Number(d.get('sortOrder')), isOpen: d.has('isOpen'), isClosed: d.has('isClosed'), isInnocent: d.has('isInnocent'), isGuilty: d.has('isGuilty') }
      : { name: d.get('name'), sortOrder: Number(d.get('sortOrder')), ...(kind === 'competences' ? { parentId: d.get('parentId') || null } : {}) };
    try {
      const saved = editing
        ? await api<CatalogItem>(`/settings/${kind}/${editing.id}`, { method: 'PUT', body: JSON.stringify(body) })
        : await post<CatalogItem>(`/settings/${kind}`, body);
      if (saved.isActive !== d.has('isActive'))
        await api(`/settings/${kind}/${saved.id}/status`, { method: 'PATCH', body: JSON.stringify({ isActive: d.has('isActive') }) });
      setFormOpen(false); setEditing(null);
      setSuccess(editing ? 'Registro actualizado.' : 'Registro creado.');
      await load();
    } catch (x) { setError(x instanceof Error ? x.message : 'No se pudo guardar.'); }
    finally { setBusy(false); }
  }
  async function toggle(item: CatalogItem) {
    if (item.isActive && !window.confirm(`¿Desactivar “${item.name}”?`)) return;
    setBusy(true); setError(''); setSuccess('');
    try {
      await api(`/settings/${kind}/${item.id}/status`, { method: 'PATCH', body: JSON.stringify({ isActive: !item.isActive }) });
      setSuccess(item.isActive ? 'Registro desactivado.' : 'Registro activado.');
      await load();
    } catch (e) { setError(e instanceof Error ? e.message : 'No se pudo actualizar.'); }
    finally { setBusy(false); }
  }
  const visible = items.filter(item => `${item.name} ${item.code ?? ''}`.toLocaleLowerCase().includes(search.toLocaleLowerCase()));
  return <><div className="page-heading"><div><span className="eyebrow">CONFIGURACIÓN</span><h1>Tablas administrables</h1><p className="muted">Catálogos propios de tu organización.</p></div></div>
    {!formOpen && error && <div className="alert catalog-feedback" role="alert">{error}</div>}
    {success && <div className="alert success catalog-feedback" role="status">{success}</div>}
    <div className="panel catalog-selector"><label className="field">Tabla a administrar<select value={kind} onChange={e => setKind(e.target.value as Key)}>{Object.entries(labels).map(([key, label]) => <option key={key} value={key}>{label}</option>)}</select></label></div>
    <section className="panel catalog-list"><div className="table-heading"><h2>{labels[kind]}</h2><span className="count">{items.length}</span><button className="button catalog-new" onClick={() => open(null)}><Plus size={16}/>Nuevo</button></div>
      <label className="field catalog-search">Buscar<input value={search} onChange={e => setSearch(e.target.value)} placeholder="Nombre o código"/></label>
      <div className="catalog-header"><span>Nombre</span><span>{kind === 'competences' ? 'Competencia general' : 'Código'}</span><span>Estado</span><span>Acciones</span></div>
      {loading ? <div className="loading" role="status"><span className="spinner"/>Cargando catálogo…</div> : visible.length === 0 ? <div className="empty-state"><h3>{items.length ? 'Sin coincidencias' : 'Todavía no hay registros'}</h3><p>{items.length ? 'Prueba otra búsqueda.' : 'Crea el primer registro de esta tabla.'}</p></div> : visible.map(item => <div className="catalog-row" key={item.id}><strong>{item.name}</strong><span>{kind === 'competences' ? (items.find(x => x.id === item.parentId)?.name || 'General') : item.code || '—'}</span><span className={`client-status ${item.isActive ? 'active' : 'inactive'}`}>{item.isActive ? 'Activo' : 'Inactivo'}</span><div className="catalog-row-actions"><button className="button secondary" disabled={busy} onClick={() => open(item)}><Pencil size={15}/>Editar</button><button className="button secondary" disabled={busy} onClick={() => toggle(item)}>{item.isActive ? 'Desactivar' : 'Activar'}</button></div></div>)}
    </section>
    <dialog ref={dialogRef} className="catalog-dialog" onClose={() => setFormOpen(false)} onCancel={e => { if (busy) e.preventDefault(); }} aria-label={editing ? 'Editar catálogo' : 'Nuevo catálogo'}>
      <div className="catalog-dialog-heading"><h2>{editing ? `Editar ${labels[kind].toLocaleLowerCase()}` : `Nuevo registro: ${labels[kind]}`}</h2><button type="button" className="icon-button" aria-label="Cerrar" onClick={close}><X size={20}/></button></div>
      {formOpen && error && <div className="alert" role="alert">{error}</div>}
      <form key={`${kind}-${editing?.id ?? 'new'}`} className="catalog-modal-form" onSubmit={save}><label className="field">Nombre<input required maxLength={240} name="name" defaultValue={editing?.name} autoFocus/></label>
        {kind === 'competences' && <SearchableSelect label="Competencia general (vacío para crear raíz)" name="parentId" value={editing?.parentId} options={items.filter(x => !x.parentId && (x.isActive || x.id === editing?.parentId) && x.id !== editing?.id)}/>}
        {kind === 'case-statuses' && <><label className="field">Código<input required maxLength={80} name="code" defaultValue={editing?.code}/></label><fieldset className="catalog-flags"><legend>Clasificación del estado</legend>{([['isOpen','Cuenta como caso activo'],['isClosed','Cuenta como caso cerrado'],['isInnocent','Resultado inocente'],['isGuilty','Resultado culpable']] as const).map(([key,label]) => <label key={key}><input type="checkbox" name={key} defaultChecked={!!editing?.[key]}/>{label}</label>)}</fieldset></>}
        <label className="field">Orden<input name="sortOrder" type="number" defaultValue={editing?.sortOrder ?? 0}/></label>
        <label className="catalog-active"><input type="checkbox" name="isActive" defaultChecked={editing?.isActive ?? true}/>Activo</label>
        <div className="filter-actions"><button type="button" className="button secondary" onClick={close}>Cancelar</button><button className="button" disabled={busy}>{busy ? 'Guardando…' : 'Guardar'}</button></div></form>
    </dialog></>;
}
