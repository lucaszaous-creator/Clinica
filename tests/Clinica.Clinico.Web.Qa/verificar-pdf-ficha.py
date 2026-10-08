"""Confere o PDF sintético emitido pelo botão da sessão na QA WebView2."""
from pathlib import Path
import sys
import pymupdf

arquivo = Path(sys.argv[1] if len(sys.argv) > 1 else
               "artifacts/clinico-web/Ficha-do-atendimento-2026-0001.pdf")
with pymupdf.open(arquivo) as documento:
    texto = "\n".join(pagina.get_text() for pagina in documento)
    assert documento.page_count > 0, "Ficha emitida sem páginas"
    assert "Paciente fictícia de demonstração" in texto, "Paciente ausente do PDF"
    assert "Evolução sintética para validação da interface." in texto, "Evolução ausente do PDF"
    arquivo.with_suffix(".texto-qa.txt").write_text(texto, encoding="utf-8")
    print(f"OK PDF da ficha: {documento.page_count} páginas, paciente e evolução preservados")
