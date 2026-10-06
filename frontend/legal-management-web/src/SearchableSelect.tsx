import { useEffect, useId, useRef, useState } from 'react';

export function SearchableSelect({ label, name, options, value, required = false, clearLabel = 'Sin seleccionar', loading = false, error = '', onChange }: {
  label: string; name: string; options: { id: string; name: string }[]; value?: string; required?: boolean; clearLabel?: string; loading?: boolean; error?: string; onChange?: (id: string) => void;
}) {
  const list = useId(); const [text, setText] = useState('');
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => { input.current?.setCustomValidity(text && !options.some(x => x.name === text) ? 'Selecciona un resultado válido de la lista.' : ''); }, [text, options]);
  useEffect(() => { const selected = options.find(x => x.id === value); if (selected) setText(selected.name); else if (!value) setText(''); }, [value, options]);
  const match = options.find(x => x.name === text); const filtered = text ? options.filter(x => x.name.toLocaleLowerCase().includes(text.toLocaleLowerCase())) : options;
  return <label className="field searchable-select">{label}<input ref={input} list={list} value={text} required={required} disabled={loading} placeholder={loading ? 'Cargando…' : 'Buscar…'}
    onChange={e => { setText(e.target.value); onChange?.(options.find(x => x.name === e.target.value)?.id ?? ''); }} aria-label={label} aria-describedby={`${list}-state`} aria-invalid={!!error}/><input type="hidden" name={name} value={match?.id ?? ''}/>
    <datalist id={list}>{!required && <option value="">{clearLabel}</option>}{options.map(x => <option key={x.id} value={x.name}/>)}</datalist>
    <span id={`${list}-state`} className={`searchable-state ${error ? 'error' : ''}`} aria-live="polite">{loading ? <><span className="mini-spinner"/>Cargando opciones</> : error ? error : !filtered.length ? 'Sin resultados' : match ? 'Resultado seleccionado' : `${filtered.length} resultado${filtered.length === 1 ? '' : 's'} disponible${filtered.length === 1 ? '' : 's'}`}</span></label>;
}
