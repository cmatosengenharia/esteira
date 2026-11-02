import { useState } from "react";
import { api } from "../api";

/** Entrada de desenvolvimento: POST /dev/seed cria tenant e usuário e devolve o token. Login real entra depois do piloto. */
export function Entrar({ onEntrar }: { onEntrar: (token: string) => void }) {
  const [tenant, setTenant] = useState("dev");
  const [email, setEmail] = useState("dev@esteira.local");
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);

  const enviar = async (e: React.FormEvent) => {
    e.preventDefault();
    setOcupado(true); setErro(null);
    try { onEntrar((await api.seed(tenant.trim(), email.trim())).token); }
    catch (err) { setErro(err instanceof Error ? err.message : String(err)); }
    finally { setOcupado(false); }
  };

  return (
    <main style={{ maxWidth: 440 }}>
      <h1>Esteira</h1>
      <section className="panel">
        <h2>Entrar</h2>
        <p className="legenda">Ambiente de desenvolvimento: informe o espaço (tenant) e o e-mail. Se não existirem, são criados.</p>
        <form onSubmit={enviar}>
          <div className="campo"><label htmlFor="tenant">Espaço</label><input id="tenant" type="text" value={tenant} onChange={(e) => setTenant(e.target.value)} required /></div>
          <div className="campo"><label htmlFor="email">E-mail</label><input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required /></div>
          {erro && <p className="erro">{erro}</p>}
          <button className="btn primary" type="submit" disabled={ocupado}>{ocupado ? "Entrando…" : "Entrar"}</button>
        </form>
      </section>
    </main>
  );
}
