"""Guardas de configuração para publicar o portal clínico."""
from pathlib import Path
import re
import shlex


FLAG_CRIPTOGRAFIA = "CLINICA_CREDENCIAIS_CRIPTOGRAFIA_HABILITADA"


def incluir_flag_false_se_ausente(conteudo: bytes) -> bytes:
    """Acrescenta a desativação explícita sem sobrescrever configuração existente."""
    texto = conteudo.decode("utf-8")
    atribuicao = re.compile(rf"^\s*{re.escape(FLAG_CRIPTOGRAFIA)}\s*=", re.MULTILINE)
    if atribuicao.search(texto):
        return conteudo
    separador = b"\r\n" if b"\r\n" in conteudo else b"\n"
    novo = conteudo + (b"" if conteudo.endswith((b"\n", b"\r")) else separador)
    return novo + FLAG_CRIPTOGRAFIA.encode("ascii") + b"=false" + separador


def verificar_flag_desativada_no_processo(ambiente: bytes) -> None:
    """Verifica apenas a flag do processo sem imprimir nem retornar outras variáveis."""
    prefixo = FLAG_CRIPTOGRAFIA.encode("ascii") + b"="
    for entrada in ambiente.split(b"\0"):
        if entrada.startswith(prefixo):
            try:
                valor = entrada[len(prefixo):].decode("ascii")
            except UnicodeDecodeError as erro:
                raise ValueError("Valor não ASCII da flag no ambiente efetivo do serviço.") from erro
            _exigir_valor_desativado(valor, "ambiente efetivo do serviço")


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


def _verificar_arquivo_ambiente(conteudo: str, origem: str) -> bool:
    # O formato do systemd permite continuação de linha. Juntar antes de procurar
    # o nome evita que uma definição dividida escape à verificação.
    logicas = _linhas_logicas(conteudo, origem)
    encontrada = False
    padrao = re.compile(rf"^\s*{re.escape(FLAG_CRIPTOGRAFIA)}\s*=\s*(.*?)\s*$")
    for linha in logicas:
        texto = linha.strip()
        if not texto or texto.startswith(("#", ";")) or FLAG_CRIPTOGRAFIA not in linha:
            continue
        correspondencia = padrao.match(linha)
        if correspondencia is None:
            raise ValueError(f"Definição ambígua da cifra em {origem}.")
        _exigir_valor_desativado(correspondencia.group(1), origem)
        encontrada = True
    return encontrada


def verificar_criptografia_desativada(
    arquivo_principal: Path,
    unidade_systemd: str,
    conteudo_principal: str | None = None,
) -> None:
    """Falha fechada se qualquer fonte efetiva puder habilitar a cifra.

    Lê todos os EnvironmentFile listados pela unidade e as atribuições Environment=.
    Recusa PassEnvironment= para a flag, pois o valor herdado do gerenciador não pode
    ser confirmado sem consultar o ambiente do systemd.
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
        if diretiva in {"environment", "setenvironment"}:
            try:
                atribuicoes = shlex.split(valor, posix=True)
            except ValueError as erro:
                raise ValueError(f"{diretiva}= ambíguo na linha {numero}.") from erro
            for atribuicao in atribuicoes:
                if FLAG_CRIPTOGRAFIA in atribuicao:
                    nome, separador, conteudo = atribuicao.partition("=")
                    if nome != FLAG_CRIPTOGRAFIA or not separador:
                        raise ValueError(f"{diretiva}= ambíguo na linha {numero}.")
                    _exigir_valor_desativado(conteudo, f"{diretiva}= linha {numero}")
        elif diretiva == "passenvironment":
            try:
                variaveis = shlex.split(valor, posix=True)
            except ValueError as erro:
                raise ValueError(f"PassEnvironment= ambíguo na linha {numero}.") from erro
            if any(v == FLAG_CRIPTOGRAFIA or v.startswith(f"{FLAG_CRIPTOGRAFIA}=") for v in variaveis):
                raise ValueError(
                    f"PassEnvironment= transmite a flag de cifra sem valor verificável na linha {numero}."
                )
        elif diretiva.startswith("exec") and FLAG_CRIPTOGRAFIA in valor:
            raise ValueError(f"A linha Exec* altera a flag de cifra diretamente na linha {numero}.")
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
        conteudo = (
            conteudo_principal
            if caminho == arquivo_principal and conteudo_principal is not None
            else caminho.read_text(encoding="utf-8")
        )
        encontrada = _verificar_arquivo_ambiente(conteudo, str(caminho))
        if caminho == arquivo_principal and not encontrada:
            raise ValueError("O arquivo portal.env precisa definir a cifra explicitamente como false.")
    if any(
        linha.strip().lower().startswith("pamname=")
        for linha in _linhas_logicas(unidade_systemd, "unidade systemd")
    ):
        raise ValueError("PAMName= pode substituir a flag de cifra depois dos arquivos de ambiente.")
