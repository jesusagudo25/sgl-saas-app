import { lazy, Suspense } from 'react';

const CalendarView = lazy(() => import('./Calendar').then(module => ({ default: module.Calendar })));

export function Calendar(props: { caseId?: string; caseName?: string }) {
  return <Suspense fallback={<div className="loading" role="status"><span className="spinner"/>Cargando agenda…</div>}><CalendarView {...props}/></Suspense>;
}
