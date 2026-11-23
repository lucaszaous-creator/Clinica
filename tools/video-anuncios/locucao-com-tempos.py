#!/usr/bin/env python3
"""Gera a locução (ElevenLabs) e o JSON com o tempo de cada palavra, que alimenta a legenda cinética.

Uso: ELEVENLABS_API_KEY=... python3 locucao-com-tempos.py roteiros.json pasta-de-saida [voice_id]
roteiros.json: {"C1-historia": "texto falado...", ...}
A chave vem do ambiente (cofre do OpenBot): nunca grave a chave no repositório nem no log.
Escreva no texto a PRONÚNCIA da marca (ex.: "Clínica Sem Dor"); na tela vale a grafia oficial ("SemDor").
Padrão: docs/padrao-de-video-anuncio.md.
"""
import base64, json, os, sys, urllib.request

VOZ_PADRAO = "WPgqgCVa3mD38JuyODU6"  # Gabriel (pt-BR, biblioteca ElevenLabs; exige plano pago)

def main():
    chave = os.environ["ELEVENLABS_API_KEY"]
    roteiros = json.load(open(sys.argv[1], encoding="utf-8")); saida = sys.argv[2]
    voz = sys.argv[3] if len(sys.argv) > 3 else VOZ_PADRAO
    os.makedirs(saida, exist_ok=True)
    for nome, texto in roteiros.items():
        corpo = json.dumps({"text": texto, "model_id": "eleven_multilingual_v2", "language_code": "pt",
                            "voice_settings": {"stability": 0.42, "similarity_boost": 0.8, "style": 0.4,
                                               "use_speaker_boost": True, "speed": 1.05}}).encode()
        req = urllib.request.Request(
            f"https://api.elevenlabs.io/v1/text-to-speech/{voz}/with-timestamps?output_format=mp3_44100_128",
            data=corpo, headers={"xi-api-key": chave, "Content-Type": "application/json"})
        d = json.load(urllib.request.urlopen(req, timeout=120))
        open(os.path.join(saida, f"{nome}.mp3"), "wb").write(base64.b64decode(d["audio_base64"]))
        a = d.get("normalized_alignment") or d["alignment"]
        palavras, atual, ini, fim = [], "", 0.0, 0.0
        for c, s, e in zip(a["characters"], a["character_start_times_seconds"], a["character_end_times_seconds"]):
            if not c.strip():
                if atual: palavras.append({"palavra": atual, "inicio": round(ini, 3), "fim": round(fim, 3)}); atual = ""
                continue
            if not atual: ini = s
            atual += c; fim = e
        if atual: palavras.append({"palavra": atual, "inicio": round(ini, 3), "fim": round(fim, 3)})
        json.dump({"texto": texto, "voz": voz, "palavras": palavras},
                  open(os.path.join(saida, f"{nome}-tempos.json"), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        print(f"{nome}: {palavras[-1]['fim']:.1f}s, {len(palavras)} palavras")

if __name__ == "__main__":
    main()
