"""Consolida contratos e respostas reais do Jev; não converte inventário em cobertura."""
import hashlib
import json
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2]
PASTA = RAIZ / "artifacts/mapa-botoes"
mapa = json.loads((PASTA / "mapa-jev.json").read_text(encoding="utf-8"))
detalhes = json.loads((PASTA / "mapa-jev-detalhes.json").read_text(encoding="utf-8"))
por_id = {a["id"]: a for a in detalhes["acoes"]}

# Resultado definido pela implementação lida, especialmente quando a classificação
# probabilística do Jev é ambígua ou incorreta (Cobrar não dá baixa na dívida).
precisos = {
    "Parabenizar": "Abre WhatsApp com telefone e saudação preenchidos; não envia mensagem nem vende algo.",
    "VenderPacote": "Abre venda com o paciente escolhido; confirmar grava o pacote; cancelar preserva o agendamento.",
    "ReceberDivida": "Abre cobrança do paciente escolhido e recarrega elegibilidade após fechar; abertura não quita dívida.",
    "Conferir": "Consulta documento pelo código; mostra titular/situação ou informa código vazio/inexistente; não emite novo documento.",
    "ColherTermo": "Abre coleta do termo pendente do paciente e sessão corretos; recarrega pendências ao voltar.",
    "Enfermagem.ColherTermo": "Abre coleta do termo pendente do paciente e sessão corretos; recarrega pendências ao voltar.",
    "Cobrar": "Abre WhatsApp com a mensagem de cobrança do paciente; não envia sozinho e não registra recebimento.",
    "Simular": "Informa valor/parcela inválidos; com dados válidos calcula bruto/descontos/líquido sem gravar lançamento."
}
acoes = []
for a in mapa["acoes"]:
    d = por_id.get(a["id"])
    item = {k: a.get(k) for k in ("id", "tela", "tipo", "comando", "rotulo", "permissao", "guarda", "visivel")}
    item["jev"] = {k: a["jev"].get(k) for k in ("choice", "confidence")}
    item["pedido_sha256"] = a["pedido_sha256"]
    item["resultado_esperado"] = precisos[a["comando"]] if d else a["resultado_esperado"]
    item["verificacao"] = "Contrato validado; execução individual não atestada por este inventário. Consultar cenários e limites no relatório."
    if d:
        item["analise_com_fonte"] = {"fonte": d["fonte"], "jev": d["jev"], "pedido_sha256": d["pedido_sha256"],
            "interpretacao": "Resultado esperado revisado no código; a escolha probabilística não determina aprovação."}
    acoes.append(item)
logs = {}
for nome in ("qa-financeiro-botoes", "qa-recepcao-botoes", "qa-faturamento-botoes", "qa-ferramentas-botoes",
             "qa-gerente-botoes", "qa-clinico-botoes", "qa-infusao-botoes", "testes-botoes", "verificar-suite-botoes", "compilar-sombra-botoes"):
    p = RAIZ / "artifacts" / (nome + ".log")
    if p.exists():
        dados = p.read_bytes()
        logs[nome] = {"sha256": hashlib.sha256(dados).hexdigest(), "trecho_final": "\n".join(dados.decode("utf-8-sig", errors="replace").splitlines()[-8:])}
saida = {"modelo": "jev-1.13.0", "escopo": "Ações registradas: páginas, seções, linhas e formulários dos cinco módulos. Componentes próprios e ferramentas globais têm testes separados.",
    "limite": "Mapeamento e classificação não comprovam 986 cliques nem todas as combinações de estado/perfil. Jev recebeu código, não executou a aplicação. Integrações externas reais não foram acionadas.",
    "quantidade": len(acoes), "evidencias_de_cenarios": logs, "acoes": acoes}
cabecalho = {k: v for k, v in saida.items() if k != "acoes"}
texto = json.dumps(cabecalho, ensure_ascii=False, indent=2).rstrip()[:-1]
texto += ',"acoes":[\n' + ',\n'.join(json.dumps(a, ensure_ascii=False, separators=(",", ":")) for a in acoes) + '\n]}\n'
(RAIZ / "docs/design-system/mapa-botoes-jev-pr245.json").write_text(texto, encoding="utf-8")
print(f"Mapa consolidado: {len(acoes)} ações; {len(logs)} logs de cenários.")
