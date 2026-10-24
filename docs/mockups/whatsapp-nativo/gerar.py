#!/usr/bin/env python3
"""Monta o mockup a partir do CSS REAL da casa (tokens + clinica.css + moldura.css) mais o
conversas.css proposto e o corpo.html. Gera index.html (documento inteiro, para abrir e
fotografar) e artefato.html (sem doctype/head, para publicar como artefato).

Uso: python3 docs/mockups/whatsapp-nativo/gerar.py   (na raiz do repositório Clinica)
"""
import pathlib, re

RAIZ = pathlib.Path(__file__).resolve().parents[3]
AQUI = pathlib.Path(__file__).resolve().parent
UI = RAIZ / "src/Clinica.Ui.Blazor/Ativos/css"

tokens = "".join((RAIZ / "tokens" / n).read_text(encoding="utf-8") for n in
                 ("colors.css", "typography.css", "layout.css", "semantic.css", "web-extras.css"))
clinica = (UI / "clinica.css").read_text(encoding="utf-8")
clinica = re.sub(r'^@import url\("[^"]+"\);\s*$', "", clinica, flags=re.M)  # os @import não resolvem aqui: a tela é a única
moldura = (UI / "telas/moldura.css").read_text(encoding="utf-8")
chat = (UI / "telas/chat-equipe.css").read_text(encoding="utf-8")  # o corpo da moldura e a bolinha do chat da equipe moram aqui
conversas = (AQUI / "conversas.css").read_text(encoding="utf-8")
corpo = (AQUI / "corpo.html").read_text(encoding="utf-8")

# Os ícones: o MESMO traço do Componentes/Icone.razor (grade 24, 2 px, currentColor).
ICONES = {
    "abaixo": "<path d='m6 9 6 6 6-6'/>",
    "sino": "<path d='M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9'/><path d='M10.3 21a1.94 1.94 0 0 0 3.4 0'/>",
    "busca": "<circle cx='11' cy='11' r='7'/><path d='m20 20-3.5-3.5'/>",
    "atualizar": "<path d='M21 12a9 9 0 1 1-2.64-6.36'/><path d='M21 3v6h-6'/>",
    "mais": "<path d='M12 5v14M5 12h14'/>",
    "chat": "<path d='M21 12a8 8 0 0 1-11.6 7.2L4 21l1.8-5.4A8 8 0 1 1 21 12z'/>",
    "pessoa": "<circle cx='12' cy='8' r='4'/><path d='M4 21a8 8 0 0 1 16 0'/>",
    "transferir": "<path d='M4 7h13'/><path d='m14 4 3 3-3 3'/><path d='M20 17H7'/><path d='m10 14-3 3 3 3'/>",
    "check": "<path d='m5 12 5 5 9-10'/>",
    "ficha": "<rect x='4' y='3' width='16' height='18' rx='2'/><path d='M8 8h8M8 12h8M8 16h5'/>",
    "alerta": "<circle cx='12' cy='12' r='9'/><path d='M12 8v5M12 16h.01'/>",
    "info": "<circle cx='12' cy='12' r='9'/><path d='M12 11v5M12 8h.01'/>",
    "cal": "<rect x='3' y='5' width='18' height='16' rx='2'/><path d='M3 10h18M8 3v4M16 3v4'/>",
    "nota": "<path d='M4 4h12l4 4v12H4z'/><path d='M8 13h8M8 17h5'/>",
    "anexo": "<path d='m21 12-8.5 8.5a5 5 0 0 1-7-7L14 5a3.5 3.5 0 0 1 5 5l-8.5 8.5a2 2 0 0 1-3-3L15 8'/>",
    "modelo": "<rect x='4' y='4' width='16' height='16' rx='2'/><path d='M4 10h16M10 10v10'/>",
    "enviar": "<path d='m3 11 18-8-8 18-2-8z'/>",
    "lido": "<path d='m2 12 4 4 7-8'/><path d='m10 16 1 1 9-10'/>",
    "tick": "<path d='m4 12 5 5 11-11'/>",
    "ticks": "<path d='m2 12 4 4 7-8'/><path d='m10 16 1 1 9-10'/>",
    "relogio": "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
    "cadeado": "<rect x='5' y='11' width='14' height='10' rx='2'/><path d='M8 11V8a4 4 0 0 1 8 0v3'/>",
    "fechar": "<path d='M6 6l12 12M18 6 6 18'/>",
    "bolt": "<path d='M13 3 5 14h6l-1 7 8-11h-6z'/>",
    "esquerda": "<path d='m15 18-6-6 6-6'/>",
    "sms": "<rect x='3' y='5' width='18' height='14' rx='2'/><path d='M7 10h10M7 14h6'/>",
    "arquivo": "<path d='M6 3h8l4 4v14H6z'/><path d='M14 3v4h4'/>",
    "vazio": "<rect x='4' y='4' width='16' height='16' rx='3'/><path d='M4 13h4l2 3h4l2-3h4'/>",
    "coracao": "<path d='M12 20s-7-4.5-7-10a4 4 0 0 1 7-2.5A4 4 0 0 1 19 10c0 5.5-7 10-7 10z'/>",
}
def icone(m):
    nome = m.group(1)
    return ("<svg class='icone' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' "
            "stroke-linecap='round' stroke-linejoin='round' aria-hidden='true' focusable='false'>" + ICONES[nome] + "</svg>")
corpo = re.sub(r"\{\{icone:([a-z-]+)\}\}", icone, corpo)
import json
app = (AQUI / "app.js").read_text(encoding="utf-8")
corpo += "\n<script>\nconst ICONES = " + json.dumps(ICONES, ensure_ascii=False) + ";\n" + app + "\n</script>\n"

# A tela é de um app Windows claro: um tema só, de propósito, com fundo e cores explícitos.
css = ("/* ===== tokens/*.css (os MESMOS do app, linkados e nunca copiados no projeto) ===== */\n" + tokens +
       "\n/* ===== Ativos/css/clinica.css ===== */\n" + clinica +
       "\n/* ===== Ativos/css/telas/moldura.css ===== */\n" + moldura +
       "\n/* ===== Ativos/css/telas/chat-equipe.css ===== */\n" + chat +
       "\n/* ===== telas/conversas.css (proposta) ===== */\n" + conversas +
       "\n/* ===== só do mockup: a janela do app é a janela toda ===== */\n"
       ":root{color-scheme:light}html,body{height:100%}body{background:var(--fundo);color:var(--texto-primario)}\n"
       "[hidden]{display:none!important}\n"
       ".moldura{height:100%}\n")

TITULO = "WhatsApp na Recepção"
cabeca = f"<title>{TITULO}</title>\n<style>\n{css}</style>\n"
(AQUI / "artefato.html").write_text(cabeca + corpo, encoding="utf-8")
(AQUI / "index.html").write_text(
    '<!DOCTYPE html>\n<html lang="pt-BR">\n<head>\n<meta charset="utf-8" />\n'
    '<meta name="viewport" content="width=device-width, initial-scale=1" />\n' + cabeca + "</head>\n<body>\n" + corpo + "</body>\n</html>\n",
    encoding="utf-8")
print("ok:", (AQUI / "index.html").stat().st_size, "bytes")
