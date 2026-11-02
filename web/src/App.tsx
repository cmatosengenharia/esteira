import { useEffect, useState } from "react";
import { getToken, setToken } from "./api";
import { Entrar } from "./pages/Entrar";
import { Videos } from "./pages/Videos";
import { Novo } from "./pages/Novo";
import { Video } from "./pages/Video";
import { Metricas } from "./pages/Metricas";

// Roteamento por hash: #/videos, #/novo, #/video/<id>, #/metricas. Sem dependência de router.
function useHash() {
  const [hash, setHash] = useState(() => window.location.hash || "#/videos");
  useEffect(() => {
    const on = () => setHash(window.location.hash || "#/videos");
    window.addEventListener("hashchange", on);
    return () => window.removeEventListener("hashchange", on);
  }, []);
  return hash;
}

export function App() {
  const hash = useHash();
  const [token, setTok] = useState(getToken());
  const entrar = (t: string) => { setToken(t); setTok(t); window.location.hash = "#/videos"; };
  const sair = () => { setToken(null); setTok(null); };

  if (!token) return <Entrar onEntrar={entrar} />;

  const rota = hash.replace(/^#\/?/, "");
  const [tela, arg] = rota.split("/");
  const atual = (t: string) => (tela === t ? "link atual" : "link");

  return (
    <>
      <nav className="topo">
        <a className="marca" href="#/videos">Esteira</a>
        <a className={atual("videos")} href="#/videos">Vídeos</a>
        <a className={atual("novo")} href="#/novo">Novo vídeo</a>
        <a className={atual("metricas")} href="#/metricas">Semana</a>
        <span className="estado">
          <button className="btn" onClick={sair} type="button">Sair</button>
        </span>
      </nav>
      <main>
        {tela === "novo" && <Novo />}
        {tela === "video" && arg && <Video id={arg} />}
        {tela === "metricas" && <Metricas />}
        {(tela === "videos" || tela === "") && <Videos />}
      </main>
    </>
  );
}
