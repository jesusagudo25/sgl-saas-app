import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react';
import { api, beginLogout, configureSession, finishLogout, post, refreshSession, setOrganization, setSession, type Organization, type Session } from './api';

type AuthValue = { session: Session | null; user: Session['user'] | null; accessToken: string | null; authenticated: boolean; loading: boolean;
  login: (email: string, password: string, rememberMe: boolean) => Promise<void>; logout: () => Promise<void>; refresh: typeof refreshSession };
const AuthContext = createContext<AuthValue>(null!);
export const useAuth = () => useContext(AuthContext);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, update] = useState<Session | null>(null);
  const [loading, setLoading] = useState(true);
  useEffect(() => { configureSession(update); refreshSession().catch(() => {}).finally(() => setLoading(false));
    return () => configureSession(() => {}); }, []);
  async function login(email: string, password: string, rememberMe: boolean) {
    setSession(await post<Session>('/auth/login', { email, password, rememberMe }, false));
  }
  async function logout() {
    // Let an in-flight rotation settle, then revoke the cookie it produced.
    await beginLogout();
    try { await post('/auth/logout', undefined, false); }
    finally { finishLogout(); localStorage.removeItem('sgl.organization'); }
  }
  return <AuthContext.Provider value={{ session, user: session?.user ?? null, accessToken: session?.accessToken ?? null,
    authenticated: !!session, loading, login, logout, refresh: refreshSession }}>{children}</AuthContext.Provider>;
}

type OrganizationValue = { organizations: Organization[]; activeOrganization: Organization | null; loading: boolean; error: string;
  reload: () => Promise<Organization[]>; selectOrganization: (org: Organization) => Promise<void>; clearOrganization: () => void };
const OrganizationContext = createContext<OrganizationValue>(null!);
export const useOrganizations = () => useContext(OrganizationContext);
export function OrganizationProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const [organizations, setOrganizations] = useState<Organization[]>([]);
  const [activeOrganization, setActive] = useState<Organization | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadedForUser, setLoadedForUser] = useState<string | null>(null);
  const [error, setError] = useState('');
  const generation = useRef(0);
  function clearOrganization() { setActive(null); setOrganization(null); localStorage.removeItem('sgl.organization'); }
  async function reload() {
    const version = generation.current;
    const items = await api<Organization[]>('/organizations');
    if (version === generation.current) setOrganizations(items);
    return items;
  }
  async function selectOrganization(org: Organization) {
    const validated = await api<Organization>(`/organizations/${org.id}`, {}, true, org.id);
    setOrganization(validated.id); setActive(validated); localStorage.setItem('sgl.organization', validated.id);
  }
  useEffect(() => {
    const version = ++generation.current;
    setLoadedForUser(null);
    setOrganizations([]); setActive(null); setOrganization(null); setError('');
    if (!user) { setLoading(false); return; }
    setLoading(true);
    api<Organization[]>('/organizations').then(async items => {
      if (version !== generation.current) return;
      setOrganizations(items);
      const preferred = items.find(o => o.id === localStorage.getItem('sgl.organization')) ?? (items.length === 1 ? items[0] : undefined);
      if (preferred) {
        const validated = await api<Organization>(`/organizations/${preferred.id}`, {}, true, preferred.id);
        if (version === generation.current) { setActive(validated); setOrganization(validated.id); }
      }
    }).catch(e => { if (version === generation.current) setError(e.message); })
      .finally(() => { if (version === generation.current) { setLoadedForUser(user.id); setLoading(false); } });
    return () => { generation.current++; };
  }, [user?.id]);
  const initializing = loading || (!!user && loadedForUser !== user.id);
  return <OrganizationContext.Provider value={{ organizations, activeOrganization, loading: initializing, error, reload, selectOrganization, clearOrganization }}>
    {children}</OrganizationContext.Provider>;
}
