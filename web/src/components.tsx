import { STAGE_PT } from "./api";

/** Rótulo de estado com glifo: cor nunca é a única pista (princípio 2 do Círculo). */
export function Estagio({ stage }: { stage: string }) {
  const nome = STAGE_PT[stage] ?? stage;
  if (stage === "Failed") return <span className="chip bad">✕ {nome}</span>;
  if (stage === "Published") return <span className="chip ok">✓ {nome}</span>;
  if (stage === "Rendered" || stage === "CopyReady" || stage === "Approved") return <span className="chip ok">✓ {nome}</span>;
  if (stage === "AwaitingApproval") return <span className="chip llm">! {nome}</span>;
  return <span className="chip jev">◆ {nome}</span>;
}
