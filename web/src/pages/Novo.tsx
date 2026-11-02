import { useState } from "react";
import { api } from "../api";

const REDES = ["YouTubeShorts", "YouTube", "Instagram", "TikTok"] as const;

export function Novo() {
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [redes, setRedes] = useState<string[]>(["YouTubeShorts"]);
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const alternar = (r: string) => setRedes((atual) => (atual.includes(r) ? atual.filter((x) => x !== r) : [...atual, r]));

  const enviar = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!arquivo) return;
    setOcupado(true); setErro(null);
    try {
      const job = await api.create(arquivo, redes.length ? redes : ["YouTubeShorts"]);
      window.location.hash = `#/video/${job.id}`;
    } catch (err) { setErro(err instanceof Error ? err.message : String(err)); setOcupado(false); }
  };

  return (
    <>
      <h1>Novo vídeo</h1>
      <section className="panel" style={{ maxWidth: 560 }}>
        <form onSubmit={enviar}>
          <div className="campo">
            <label htmlFor="arquivo">Vídeo bruto (mp4, mov, m4v, mkv, webm)</label>
            <input id="arquivo" type="file" accept=".mp4,.mov,.m4v,.mkv,.webm,video/*" onChange={(e) => setArquivo(e.target.files?.[0] ?? null)} required />
            {arquivo && <p className="legenda">{arquivo.name} · {(arquivo.size / 1e6).toFixed(1)} MB</p>}
          </div>
          <div className="campo">
            <label>Redes de destino</label>
            <div className="linha">
              {REDES.map((r) => (
                <label key={r} style={{ fontWeight: 400, display: "inline-flex", gap: 6, alignItems: "center", margin: 0 }}>
                  <input type="checkbox" checked={redes.includes(r)} onChange={() => alternar(r)} /> {r}
                </label>
              ))}
            </div>
            <p className="legenda">A esteira corta silêncios e erros de fala, legenda, gera o master 9:16 e até 3 shorts de 30–60 s.</p>
          </div>
          {erro && <p className="erro">{erro}</p>}
          <button className="btn primary" type="submit" disabled={!arquivo || ocupado}>{ocupado ? "Enviando…" : "Enviar para a esteira"}</button>
        </form>
      </section>
    </>
  );
}
