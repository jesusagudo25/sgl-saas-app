export type User = { id: string; firstName: string; lastName: string; email: string };
export type Session = { accessToken: string; expiresAt: string; user: User };
export type Organization = { id: string; name: string; slug: string; roleCode: string };
export type Member = User & { userId: string; roleCode: string; status: string };
export type Invitation = { id: string; organizationName: string; email: string; roleCode: string; expiresAt: string };
export type Client = { id: string; type: 'PERSON' | 'COMPANY'; displayName: string; identificationType: string; identificationNumber: string;
  email?: string; phone?: string; secondaryPhone?: string; address?: string; notes?: string; status: 'ACTIVE' | 'INACTIVE';
  firstName?: string; lastName?: string; legalName?: string; tradeName?: string; contactPerson?: string; createdAt: string; updatedAt: string };
export type CaseStatus = 'OPEN' | 'IN_PROGRESS' | 'SUSPENDED' | 'CLOSED';
export type CasePriority = 'LOW' | 'MEDIUM' | 'HIGH' | 'URGENT';
export type LegalCase = { id: string; clientId: string; clientName: string; caseNumber: string; title: string; description?: string;
  caseType: string; caseTypeId?: string; status: string; caseStatusId?: string; priority: CasePriority; responsibleMembershipId?: string; responsibleName?: string;
  openedAt: string; situationDate?: string; closedAt?: string; court?: string; courtId?: string; jurisdiction?: string; jurisdictionId?: string; counterparty?: string; opposingCounsel?: string;
  notes?: string; createdAt: string; updatedAt: string };
export type CaseOption = { id: string; name: string };
export type CaseStatusOption = CaseOption & { code: string; isOpen: boolean; isClosed: boolean; isInnocent: boolean; isGuilty: boolean };
export type CaseOptions = { clients: CaseOption[]; responsibleMemberships: CaseOption[]; caseStatuses: CaseStatusOption[]; caseTypes: CaseOption[]; courts: CaseOption[]; jurisdictions: CaseOption[] };
export type CatalogItem = CaseOption & { isActive: boolean; sortOrder: number; code?: string; isOpen?: boolean; isClosed?: boolean; isInnocent?: boolean; isGuilty?: boolean };
export type Page<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number };
export class ApiError extends Error { constructor(public status: number, message: string) { super(message); } }

let accessToken: string | null = null;
let organizationId: string | null = null;
let onSession: (session: Session | null) => void = () => {};
let refreshing: Promise<Session> | null = null;
let sessionVersion = 0;
let loggingOut = false;
export function configureSession(callback: typeof onSession) { onSession = callback; }
export function setSession(session: Session | null) { accessToken = session?.accessToken ?? null; onSession(session); }
export function clearSession() { sessionVersion++; setSession(null); setOrganization(null); }
export function setOrganization(id: string | null) { organizationId = id; }

async function parse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    const detail = problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.title;
    throw new ApiError(response.status, response.status === 403 ? 'Acceso denegado. Revisa tu organización y los permisos de tu cuenta.' :
      detail || (response.status === 429 ? 'Demasiados intentos. Espera un minuto.' : 'No se pudo completar la solicitud.'));
  }
  return response.status === 204 ? undefined as T : response.json();
}

export async function refreshSession(): Promise<Session> {
  if (loggingOut) throw new ApiError(401, 'Sesión cerrada.');
  if (!refreshing) {
    const version = sessionVersion;
    refreshing = fetch('/api/auth/refresh', { method: 'POST', credentials: 'include', headers: { 'X-CSRF': '1' } })
      .then(parse<Session>).then(session => {
        if (version !== sessionVersion) throw new ApiError(401, 'Sesión cerrada.');
        setSession(session); return session;
      }).catch(error => { if (version === sessionVersion) setSession(null); throw error; }).finally(() => { refreshing = null; });
  }
  return refreshing;
}

export async function beginLogout() {
  loggingOut = true;
  sessionVersion++;
  await refreshing?.catch(() => {});
  accessToken = null;
  organizationId = null;
}

export function finishLogout() {
  clearSession();
  loggingOut = false;
}

export async function api<T>(path: string, options: RequestInit = {}, auth = true, tenantId = organizationId): Promise<T> {
  const send = () => fetch(`/api${path}`, { ...options, credentials: 'include', headers: {
    ...(options.body ? { 'Content-Type': 'application/json' } : {}), 'X-CSRF': '1',
    ...(auth && accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
    ...(tenantId ? { 'X-Organization-Id': tenantId } : {}), ...options.headers
  } });
  let response = await send();
  if (auth && response.status === 401) {
    await refreshSession();
    response = await send();
    if (response.status === 401) clearSession();
  }
  return parse<T>(response);
}
export const post = <T,>(path: string, body?: unknown, auth = true, tenantId?: string | null) =>
  api<T>(path, { method: 'POST', ...(body === undefined ? {} : { body: JSON.stringify(body) }) }, auth, tenantId);
