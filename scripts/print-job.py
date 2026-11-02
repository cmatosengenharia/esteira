#!/usr/bin/env python3
"""Imprime o resumo de um job (JSON de GET /jobs/{id}) lido do stdin."""
import json
import sys

d = json.load(sys.stdin)
reason = f" - {d['failureReason']}" if d.get("failureReason") else ""
print(f"stage: {d['stage']}{reason}")
print("tempo por etapa:")
total = 0
for t in d["timings"]:
    ms = t["durationMs"] or 0
    total += ms
    print(f"  {t['stage']:<12} {ms:>8} ms")
print(f"  {'total':<12} {total:>8} ms")
u = d["usage"]
print(f"usage: llmTokens={u['llmTokens']} renderMinutes={u['renderMinutes']:.3f} transcribedMinutes={u['transcribedMinutes']:.3f}")
for o in d["outputs"]:
    kind = "saída" if o["kind"] == "Master" else "short"
    print(f"{kind} {o['aspect']}: {o['localPath']} ({o['durationSeconds']} s, {o['sizeBytes']} bytes)")
