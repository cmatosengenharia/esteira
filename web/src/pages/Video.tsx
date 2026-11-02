import { useEffect, useState } from "react";
import { api, fmtBytes, fmtDur, fmtWhen, STAGES, STAGE_PT, TERMINAL, type Job } from "../api";
import { Estagio } from "../components";

export function Video({ id }: { id: string }) {
  const [job, setJob] = useState<Job | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let vivo = true; let t: number | undefined;
    const carregar = async () => {
      try {
        const j = await api.job(id);
        if (!vivo) return;
        setJob(j);
        if (!TERMINAL.has(j.stage)) t = window.setTimeout(carregar, 3000);
      } catch (e) { if (vivo) setErro(e instanceof Error ? e.message : String(e)); }
    };
    carregar();
    return () => { vivo = false; if (t) clearTimeout(t); };
  }, [id]);

  if (erro) return <section className="panel alerta"><h2>Não consegui carregar</h2><p className="erro">{erro}</p></section>;
  if (!job) return <p className="legenda">Carregando…</p>;

  const feitas = new Set(["Uploaded", ...job.timings.filter((t) => t.finishedAt).map((t) => t.stage)]); // Uploaded é o estado inicial: sempre feito
  const atualIdx = STAGES.indexOf(job.stage as (typeof STAGES)[number]);
  const masters = job.outputs.map((o, i) => ({ o, i })).filter((x) => x.o.kind === "Master");
  const shorts = job.outputs.map((o, i) => ({ o, i })).filter((x) => x.o.kind === "Clip");

  return (
    <>
      <div className="linha" style={{ justifyContent: "space-between", marginBottom: 16 }}>
        <div>
          <h1 style={{ margin: 0 }}>{job.originalFileName}</h1>
          <p className="legenda" style={{ margin: "4px 0 0" }}>enviado {fmtWhen(job.createdAt)} · {fmtDur(job.durationSeconds)} · {job.fps ? `${Math.round(job.fps)} fps` : ""} · <code>{job.id}</code></p>
        </div>
        <Estagio stage={job.stage} />
      </div>

      {job.stage === "Failed" && (
        <section className="panel alerta" style={{ marginBottom: 16 }}>
          <h2>✕ A esteira parou nesta etapa</h2>
          <p className="erro">{job.failureReason}</p>
        </section>
      )}

      <p className="secao">Etapas</p>
      <div className="etapas">
        {STAGES.slice(0, 5).map((s, i) => {
          const t = job.timings.find((x) => x.stage === s && x.finishedAt);
          const cls = job.stage === "Failed" && i === atualIdx + 1 ? "falhou" : feitas.has(s) ? "feita" : i === atualIdx + 1 ? "atual" : "";
          return (
            <div key={s} className={`etapa ${cls}`}>
              <b>{feitas.has(s) ? "✓ " : cls === "atual" ? "◆ " : cls === "falhou" ? "✕ " : ""}{STAGE_PT[s]}</b>
              <span>{t?.durationMs != null ? `${(t.durationMs / 1000).toFixed(1)} s` : cls === "atual" ? "em andamento" : "—"}</span>
            </div>
          );
        })}
      </div>

      <p className="secao">Master</p>
      {masters.length === 0 && <p className="legenda">{TERMINAL.has(job.stage) ? "Sem master." : "Aguardando o render…"}</p>}
      <div className="grid">
        {masters.map(({ o, i }) => <Saida key={i} jobId={job.id} index={i} titulo={`Master ${o.aspect}`} dur={o.durationSeconds} bytes={o.sizeBytes} />)}
      </div>

      <p className="secao">Shorts <span className="selo">{shorts.length}</span></p>
      {shorts.length === 0 && masters.length > 0 && <p className="legenda">Nenhum short: o vídeo cortado precisa ter pelo menos 45 s.</p>}
      <div className="grid">
        {shorts.map(({ o, i }, n) => <Saida key={i} jobId={job.id} index={i} titulo={`Short ${n + 1}`} dur={o.durationSeconds} bytes={o.sizeBytes} />)}
      </div>

      <p className="secao">Custo</p>
      <div className="linha">
        <div className="kpi"><span>Minutos de render</span><b>{job.usage.renderMinutes.toFixed(2)}</b></div>
        <div className="kpi"><span>Minutos transcritos</span><b>{job.usage.transcribedMinutes.toFixed(2)}</b></div>
        <div className="kpi"><span>Tokens de LLM</span><b>{job.usage.llmTokens}</b></div>
      </div>
    </>
  );
}

function Saida({ jobId, index, titulo, dur, bytes }: { jobId: string; index: number; titulo: string; dur: number; bytes: number }) {
  const [url, setUrl] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const baixar = async () => {
    setOcupado(true); setErro(null);
    try { setUrl(await api.outputBlob(jobId, index)); } catch (e) { setErro(e instanceof Error ? e.message : String(e)); } finally { setOcupado(false); }
  };
  return (
    <section className="panel">
      <h2>{titulo}</h2>
      <p className="legenda">{fmtDur(dur)} · {fmtBytes(bytes)}</p>
      {url ? (
        <>
          <video src={url} controls style={{ width: "100%", maxHeight: 420, background: "#000", borderRadius: 8 }} />
          <div className="linha" style={{ marginTop: 8 }}><a className="btn" href={url} download={`${titulo.replace(/\s+/g, "-").toLowerCase()}.mp4`}>Salvar arquivo</a></div>
        </>
      ) : (
        <button className="btn" type="button" onClick={baixar} disabled={ocupado}>{ocupado ? "Baixando…" : "Ver e baixar"}</button>
      )}
      {erro && <p className="erro">{erro}</p>}
    </section>
  );
}
