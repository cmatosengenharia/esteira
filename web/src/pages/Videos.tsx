import { useEffect, useState } from "react";
import { api, fmtDur, fmtWhen, type Job } from "../api";
import { Estagio } from "../components";

export function Videos() {
  const [jobs, setJobs] = useState<Job[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    let vivo = true;
    const carregar = () => api.jobs().then((j) => vivo && setJobs(j)).catch((e) => vivo && setErro(e.message));
    carregar();
    const t = setInterval(carregar, 5000);
    return () => { vivo = false; clearInterval(t); };
  }, []);

  return (
    <>
      <div className="linha" style={{ justifyContent: "space-between", marginBottom: 16 }}>
        <h1 style={{ margin: 0 }}>Vídeos</h1>
        <a className="btn primary" href="#/novo">Novo vídeo</a>
      </div>
      {erro && <section className="panel alerta"><h2>Não consegui carregar</h2><p className="erro">{erro}</p></section>}
      {jobs && jobs.length === 0 && <section className="panel"><h2>Nenhum vídeo ainda</h2><p className="legenda">Envie o primeiro bruto em "Novo vídeo". A esteira transcreve, corta, legenda e gera os shorts.</p></section>}
      {jobs && jobs.length > 0 && (
        <table>
          <thead><tr><th>Arquivo</th><th>Estado</th><th>Duração</th><th>Saídas</th><th>Enviado</th></tr></thead>
          <tbody>
            {jobs.map((j) => (
              <tr key={j.id} className="clicavel" onClick={() => (window.location.hash = `#/video/${j.id}`)}>
                <td><a href={`#/video/${j.id}`} onClick={(e) => e.stopPropagation()}>{j.originalFileName}</a></td>
                <td><Estagio stage={j.stage} /></td>
                <td className="mono">{fmtDur(j.durationSeconds)}</td>
                <td>{j.outputs.filter((o) => o.kind === "Master").length} master · {j.outputs.filter((o) => o.kind === "Clip").length} shorts</td>
                <td className="legenda">{fmtWhen(j.createdAt)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
