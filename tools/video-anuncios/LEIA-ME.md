# Gerador de vídeos de anúncio (modelo)

Modelo do gerador da rodada 3 da SemDor, aprovada pelo dono em 23/11/2026. O padrão completo está em `docs/padrao-de-video-anuncio.md`.

**Passo a passo:**
1. `locucao-com-tempos.py`: gera a voz (ElevenLabs) e os tempos de cada palavra.
2. `cenas.html` + `tempos.js`: as cenas, que são funções do tempo, e a legenda presa à voz.
3. `render.cjs`: Playwright/Chromium fotografa os quadros a 30 fps.
4. `fazer-videos.sh`: o ffmpeg monta o H.264, mistura a voz e normaliza o volume. Defina `NODE`, `FFMPEG` e `NODE_PATH` no ambiente.

**Fica fora do repositório:**
- fotos e fontes, que se baixam de novo pelas licenças em `LICENCAS.md`;
- áudios gerados;
- vídeos;
- a chave da ElevenLabs.
