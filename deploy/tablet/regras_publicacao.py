"""Guardas de configuração para publicar o portal clínico."""
from pathlib import Path
import re
import shlex


FLAG_CRIPTOGRAFIA = "CLINICA_CREDENCIAIS_CRIPTOGRAFIA_HABILITADA"


def _exigir_valor_desativado(valor: str, origem: str) -> None:
    normalizado = valor.strip()
    if len(normalizado) >= 2 and normalizado[0] in "\"'" and normalizado[-1] == normalizado[0]:
        normalizado = normalizado[1:-1]
    if normalizado.lower() == "false":
        return
    if normalizado.lower() == "true":
        raise ValueError(f"A cifra de credenciais está ativa em {origem}.")
    raise ValueError(f"Não foi possível confirmar a cifra desativada em {origem}.")


def _linhas_logicas(conteudo: str, origem: str) -> list[str]:
    logicas: list[str] = []
    acumulada = ""
    for linha in conteudo.splitlines():
        parte = linha.rstrip()
        if parte.endswith("\\"):
            acumulada += parte[:-1]
            continue
        logicas.append(acumulada + (linha.lstrip() if acumulada else linha))
        acumulada = ""
    if acumulada:
        raise ValueError(f"Continuação incompleta em {origem}.")
    return logicas


def _verificar_arquivo_ambiente(conteudo: str, origem: str) -> None:
    # O formato do systemd permite continuação de linha. Juntar antes de procurar
    # o nome evita que uma definição dividida escape à verificação.
    logicas = _linhas_logicas(conteudo, origem)
    padrao = re.compile(rf"^\s*{re.escape(FLAG_CRIPTOGRAFIA)}\s*=\s*(.*?)\s*$")
    for linha in logicas:
        texto = linha.strip()
        if not texto or texto.startswith(("#", ";")) or FLAG_CRIPTOGRAFIA not in linha:
            continue
        correspondencia = padrao.match(linha)
        if correspondencia is None:
            raise ValueError(f"Definição ambígua da cifra em {origem}.")
        _exigir_valor_desativado(correspondencia.group(1), origem)


def verificar_criptografia_desativada(arquivo_principal: Path, unidade_systemd: str) -> None:
    """Falha fechada se qualquer fonte efetiva puder habilitar a cifra.

    Lê todos os EnvironmentFile listados pela unidade e as atribuições Environment=.
    Repetições são avaliadas individualmente: qualquer ``true`` ou valor ambíguo bloqueia.
    """
    arquivo_principal = arquivo_principal.resolve(strict=True)
    caminhos: list[Path] = []

    for numero, linha in enumerate(_linhas_logicas(unidade_systemd, "unidade systemd"), start=1):
        texto = linha.strip()
        if not texto or texto.startswith("#") or "=" not in texto:
            continue
        diretiva, valor = texto.split("=", 1)
        diretiva = diretiva.strip().lower()
        if diretiva == "environment":
            try:
                atribuicoes = shlex.split(valor, posix=True)
            except ValueError as erro:
                raise ValueError(f"Environment= ambíguo na linha {numero}.") from erro
            for atribuicao in atribuicoes:
                if FLAG_CRIPTOGRAFIA in atribuicao:
                    nome, separador, conteudo = atribuicao.partition("=")
                    if nome != FLAG_CRIPTOGRAFIA or not separador:
                        raise ValueError(f"Environment= ambíguo na linha {numero}.")
                    _exigir_valor_desativado(conteudo, f"Environment= linha {numero}")
        elif diretiva == "environmentfile":
            try:
                arquivos = shlex.split(valor, posix=True)
            except ValueError as erro:
                raise ValueError(f"EnvironmentFile= ambíguo na linha {numero}.") from erro
            for nome_arquivo in arquivos:
                caminho = Path(nome_arquivo.lstrip("-"))
                if not caminho.is_absolute():
                    raise ValueError(f"EnvironmentFile relativo na linha {numero}.")
                caminhos.append(caminho.resolve(strict=True))

    if arquivo_principal not in caminhos:
        raise ValueError("A unidade ativa não referencia o portal.env esperado.")
    for caminho in dict.fromkeys(caminhos):
        _verificar_arquivo_ambiente(caminho.read_text(encoding="utf-8"), str(caminho))
