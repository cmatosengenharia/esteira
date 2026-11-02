import { useEffect, useState } from "react";
import { api, type Weekly } from "../api";

export function Metricas() {
  const [dados, setDados] = useState<Weekly | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  useEffect(() => { api.weekly(4).then(setDados).catch((e) => setErro(e.message)); }, []);

  return (
    <>
      <h1>Semana</h1>
      {erro && <section className="panel alerta"><h2>Não consegui carregar</h2><p className="erro">{erro}</p></section>}
      {dados && (
        <>
          <div className="linha" style={{ marginBottom: 16 }}>
            <div className="kpi"><span>Publicados nesta semana</span><b>{dados.current}</b></div>
            <div className="kpi"><span>Contra a semana anterior</span><b>{dados.trend > 0 ? `+${dados.trend}` : dados.trend}</b></div>
          </div>
          <table>
            <thead><tr><th>Semana de</th><th>Renderizados</th><th>Publicados</th><th>Minutos de render</th></tr></thead>
            <tbody>
              {[...dados.weeks].reverse().map((w) => (
                <tr key={w.weekStart}><td className="mono">{w.weekStart}</td><td>{w.completed}</td><td>{w.published}</td><td>{w.renderMinutes.toFixed(2)}</td></tr>
              ))}
            </tbody>
          </table>
          <p className="legenda" style={{ marginTop: 8 }}>Métrica-chave: {dados.keyMetric}. Todo número vem de um registro de job; nada é estimado.</p>
        </>
      )}
    </>
  );
}
