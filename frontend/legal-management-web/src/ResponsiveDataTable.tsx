import { Fragment, useState, type ReactNode } from 'react';
import { ChevronDown, ChevronRight } from 'lucide-react';

export type DetailField = { label: string; value: ReactNode };

export function ResponsiveDataRow({ id, cells, details }: { id: string; cells: ReactNode[]; details: DetailField[] }) {
  const [expanded, setExpanded] = useState(false); const detailId = `row-details-${id}`;
  return <Fragment><tr className={expanded ? 'data-row expanded' : 'data-row'}>
    <td className="expand-cell"><button type="button" className="expand-button" aria-expanded={expanded} aria-controls={detailId}
      aria-label="Mostrar detalles" onClick={() => setExpanded(x => !x)}>
      {expanded ? <ChevronDown size={18}/> : <ChevronRight size={18}/>}</button></td>
    {cells.map((cell, index) => <td key={index}>{cell}</td>)}</tr>
    {expanded && <tr className="expanded-details"><td colSpan={cells.length + 1}><dl id={detailId}>{details.map((detail, index) =>
      <div key={index}><dt>{detail.label}</dt><dd>{detail.value || '—'}</dd></div>)}</dl></td></tr>}
  </Fragment>;
}
