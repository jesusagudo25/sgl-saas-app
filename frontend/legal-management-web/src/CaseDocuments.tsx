import { useEffect, useRef, useState, type FormEvent } from 'react';
import { api } from './api';
import './sprint5.css';

type Document = { id: string; originalFileName: string; extension: string; contentType: string; sizeBytes: number; documentType: string; notes?: string; uploadedBy: string; uploadedAt: string };
const types: Record<string,string> = { PLEADING:'Escrito', RESOLUTION:'Resolución', EVIDENCE:'Prueba', CONTRACT:'Contrato', IDENTIFICATION:'Identificación', CORRESPONDENCE:'Correspondencia', OTHER:'Otro' };
const message = (e: unknown) => e instanceof Error ? e.message : 'No se pudo completar la operación.';
export function CaseDocuments({caseId}: {caseId:string}) {
  const base = `/cases/${caseId}/documents`;
  const [items,setItems] = useState<Document[]>([]), [loading,setLoading] = useState(true), [busy,setBusy] = useState(false);
  const [error,setError] = useState(''), [success,setSuccess] = useState(''), [open,setOpen] = useState(false), [removing,setRemoving] = useState<Document|null>(null);
  const dialog = useRef<HTMLDialogElement>(null);
  async function load() { setLoading(true); try { setItems(await api<Document[]>(base)); } catch(e) {setError(message(e));} finally {setLoading(false);} }
  useEffect(()=>{ let live=true; api<Document[]>(base).then(x=>{if(live)setItems(x);}).catch(e=>{if(live)setError(message(e));}).finally(()=>{if(live)setLoading(false);}); return ()=>{live=false;}; },[base]);
  useEffect(()=>{ if(open || removing) dialog.current?.showModal(); else dialog.current?.close(); },[open,removing]);
  function close() { if(!busy) {setOpen(false);setRemoving(null);} }
  async function save(e:FormEvent<HTMLFormElement>) {
    e.preventDefault();setBusy(true);setError('');setSuccess('');
    try { await api(base,{method:'POST',body:new FormData(e.currentTarget)});setOpen(false);setSuccess('Documento subido.');await load(); }
    catch(e){setError(message(e));}finally{setBusy(false);}
  }
  async function remove() {
    if(!removing)return;setBusy(true);setError('');setSuccess('');
    try {await api(`${base}/${removing.id}`,{method:'DELETE'});setRemoving(null);setSuccess('Documento eliminado.');await load();}
    catch(e){setError(message(e));}finally{setBusy(false);}
  }
  async function download(item:Document) {
    setBusy(true);setError('');
    try {const blob=await api<Blob>(`${base}/${item.id}/download`,{},true,undefined,true);const url=URL.createObjectURL(blob);const a=document.createElement('a');a.href=url;a.download=item.originalFileName;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
    catch(e){setError(message(e));}finally{setBusy(false);}
  }
  return <section className="panel documents-panel"><div className="table-heading"><h2>Documentos</h2><button className="button" onClick={()=>{setError('');setOpen(true);}}>Subir documento</button></div>
    {!open && !removing && error && <div className="alert" role="alert">{error}</div>}{success && <div className="alert success" role="status">{success}</div>}
    {loading ? <div className="loading" role="status">Cargando documentos…</div> : !items.length ? <div className="empty-state"><h3>Sin documentos</h3><p>Sube el primer documento del caso.</p></div> : <div className="document-list">{items.map(item=><article className="document-row" key={item.id}>
      <div><strong>{item.originalFileName}</strong>{item.notes && <p>{item.notes}</p>}</div><div><span className="muted">Tipo</span><span>{types[item.documentType]}</span></div><div><span className="muted">Tamaño</span><span>{(item.sizeBytes/1024).toLocaleString('es',{maximumFractionDigits:1})} KB</span></div><div><span>{item.uploadedBy}</span><time>{new Date(item.uploadedAt).toLocaleString('es-PA')}</time></div>
      <div className="document-actions"><button className="button secondary" disabled={busy} onClick={()=>download(item)}>Descargar</button><button className="button secondary" disabled={busy} onClick={()=>{setError('');setRemoving(item);}}>Eliminar</button></div></article>)}</div>}
    <dialog ref={dialog} className="catalog-dialog document-dialog" aria-label={removing?'Eliminar documento':'Subir documento'} onCancel={e=>{if(busy)e.preventDefault();}} onClose={()=>{setOpen(false);setRemoving(null);}}>
      <div className="catalog-dialog-heading"><h2>{removing?'Eliminar documento':'Subir documento'}</h2><button className="icon-button" aria-label="Cerrar" disabled={busy} onClick={close}>×</button></div>
      {error && <div className="alert" role="alert">{error}</div>}
      {removing ? <div className="catalog-modal-form"><p>¿Eliminar {removing.originalFileName} del caso?</p><p className="muted">Dejará de estar disponible. Se conservará su historial de auditoría.</p><div className="filter-actions"><button className="button secondary" disabled={busy} onClick={close}>Cancelar</button><button className="button" disabled={busy} onClick={remove}>{busy?'Eliminando…':'Confirmar eliminación'}</button></div></div> : open && <form className="catalog-modal-form" onSubmit={save}>
        <label className="field">Archivo *<input autoFocus required type="file" name="file" accept=".pdf,.doc,.docx,.xls,.xlsx,.txt,.csv,.jpg,.jpeg,.png"/></label><label className="field">Tipo *<select required name="documentType">{Object.entries(types).map(([v,n])=><option value={v} key={v}>{n}</option>)}</select></label><label className="field">Observaciones<textarea name="notes" maxLength={4000} rows={4}/></label><div className="filter-actions"><button type="button" className="button secondary" disabled={busy} onClick={close}>Cancelar</button><button className="button" disabled={busy}>{busy?'Subiendo…':'Guardar documento'}</button></div></form>}
    </dialog></section>;
}
