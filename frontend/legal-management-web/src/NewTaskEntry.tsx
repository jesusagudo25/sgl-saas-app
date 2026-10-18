import { useEffect, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { api, type CalendarOptions } from './api';
import { SearchableSelect } from './SearchableSelect';
export function NewTaskEntry() {
  const [options,setOptions]=useState<CalendarOptions>({cases:[],responsibleMemberships:[]}),[error,setError]=useState(''),[loading,setLoading]=useState(true);const navigate=useNavigate();
  useEffect(()=>{let live=true;api<CalendarOptions>('/calendar/options').then(x=>{if(live)setOptions(x);}).catch(e=>{if(live)setError(e.message);}).finally(()=>{if(live)setLoading(false);});return()=>{live=false;};},[]);
  function submit(e:FormEvent<HTMLFormElement>){e.preventDefault();const id=new FormData(e.currentTarget).get('caseId');if(id)navigate(`/app/cases/${id}?tab=Tareas&new=1`);}
  return <><div className="page-heading"><h1>Nueva tarea</h1></div><form className="panel dashboard-block" onSubmit={submit}>{error&&<div className="alert" role="alert">{error}</div>}<SearchableSelect required label="Caso" name="caseId" options={options.cases} loading={loading} error={error}/><button className="button" disabled={loading||!!error}>Continuar</button></form></>;
}
