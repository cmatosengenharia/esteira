// Cliente da API da Esteira. O token vem do /dev/seed (Development) e fica no localStorage do navegador.
export type Output = { kind: "Master" | "Clip"; aspect: string; objectKey: string; localPath: string | null; durationSeconds: number; sizeBytes: number };
export type Timing = { stage: string; startedAt: string; finishedAt: string | null; durationMs: number | null };
export type Job = {
  id: string; stage: string; failureReason: string | null; originalFileName: string; createdAt: string; updatedAt: string;
  durationSeconds: number | null; fps: number | null; targets: string[]; styleTemplateId: string | null;
  budget: { maxLlmTokens: number; maxRenderMinutes: number };
  usage: { llmTokens: number; renderMinutes: number; transcribedMinutes: number };
  timings: Timing[]; outputs: Output[];
};
export type WeekBucket = { weekStart: string; completed: number; published: number; renderMinutes: number };
export type Weekly = { keyMetric: string; current: number; trend: number; weeks: WeekBucket[] };

const TOKEN_KEY = "esteira.token";
export const getToken = () => { try { return localStorage.getItem(TOKEN_KEY); } catch { return null; } };
export const setToken = (t: string | null) => { try { t ? localStorage.setItem(TOKEN_KEY, t) : localStorage.removeItem(TOKEN_KEY); } catch { /* sem storage */ } };

export class ApiError extends Error { constructor(public status: number, message: string) { super(message); } }

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  const token = getToken();
  if (token) headers.set("Authorization", `Bearer ${token}`);
  const res = await fetch(path, { ...init, headers });
  if (res.status === 401) { setToken(null); throw new ApiError(401, "Sessão expirada; entre de novo."); }
  if (!res.ok) {
    let msg = `${res.status} ${res.statusText}`;
    try { const body = await res.json(); if (body?.error) msg = body.error; } catch { /* corpo não é JSON */ }
    throw new ApiError(res.status, msg);
  }
  return (res.status === 204 ? (undefined as T) : await res.json()) as T;
}

export const api = {
  seed: (tenantSlug: string, email: string) =>
    request<{ tenantId: string; userId: string; token: string }>("/dev/seed", {
      method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ tenantSlug, email }),
    }),
  jobs: () => request<Job[]>("/jobs"),
  job: (id: string) => request<Job>(`/jobs/${id}`),
  create: (file: File, targets: string[]) => {
    const form = new FormData();
    form.append("file", file);
    form.append("targets", targets.join(","));
    return request<Job>("/jobs", { method: "POST", body: form });
  },
  weekly: (weeks = 4) => request<Weekly>(`/metrics/weekly?weeks=${weeks}`),
  /** Baixa uma saída com o bearer e devolve uma URL de blob para <video> ou <a download>. */
  outputBlob: async (jobId: string, index: number) => {
    const headers = new Headers();
    const token = getToken();
    if (token) headers.set("Authorization", `Bearer ${token}`);
    const res = await fetch(`/jobs/${jobId}/outputs/${index}`, { headers });
    if (!res.ok) throw new ApiError(res.status, `Não consegui baixar a saída (${res.status}).`);
    return URL.createObjectURL(await res.blob());
  },
};

export const STAGES = ["Uploaded", "Ingested", "Transcribed", "Planned", "Rendered", "CopyReady", "AwaitingApproval", "Approved", "Published"] as const;
export const STAGE_PT: Record<string, string> = {
  Uploaded: "Enviado", Ingested: "Ingerido", Transcribed: "Transcrito", Planned: "Planejado", Rendered: "Renderizado",
  CopyReady: "Copy pronta", AwaitingApproval: "Aguardando aprovação", Approved: "Aprovado", Published: "Publicado", Failed: "Falhou",
};
export const TERMINAL = new Set(["Rendered", "CopyReady", "AwaitingApproval", "Approved", "Published", "Failed"]);
export const fmtDur = (s: number | null | undefined) => s == null ? "—" : `${Math.floor(s / 60)}:${String(Math.round(s % 60)).padStart(2, "0")}`;
export const fmtBytes = (b: number) => b >= 1e6 ? `${(b / 1e6).toFixed(1)} MB` : `${Math.round(b / 1e3)} kB`;
export const fmtWhen = (iso: string) => new Date(iso).toLocaleString("pt-BR", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });
