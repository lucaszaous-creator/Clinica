"""Guardas de configuração para publicar o portal clínico."""
from pathlib import Path
import re
import shlex


FLAG_CRIPTOGRAFIA = "CLINICA_CREDENCIAIS_CRIPTOGRAFIA_HABILITADA"

# A TRAVA DAS CREDENCIAIS CIFRADAS (publicação com --pular-hml): o portal publica com a cifra de
# credenciais DESLIGADA e sem a chave (CLINICA_CREDENCIAIS_CHAVE); um valor "enc:" que ele precise
# abrir quebraria a leitura depois do deploy. Por isso qualquer "enc:%" em "Configuracoes" recusa.
#
# A ÚNICA exceção são as chaves do CONECTOR TISS: quem as grava e as abre é só o conector, no servidor
# dele, com a chave dele (CredenciaisTissService.AbrirParaEnvioAsync). O portal e a /gestao nunca as
# abrem: a tela só pergunta se a senha EXISTE (ParaATelaAsync, sem decifrar). Hoje o conector cifra
# um só prefixo — a senha do web service; o login fica em claro. A lista é a MESMA do C#
# (CredenciaisTissService.PrefixosCifradosForaDoPortal), e um teste do C# falha se as duas divergirem
# ou se o serviço passar a cifrar uma chave fora dela.
PREFIXOS_CIFRADOS_DO_CONECTOR_TISS = ("TissSenha:",)

MENSAGEM_CREDENCIAIS_CIFRADAS = "A base contém credenciais protegidas; dispensa recusada sem exibir valores."

for _prefixo in PREFIXOS_CIFRADOS_DO_CONECTOR_TISS:
    # O prefixo entra no SQL literal: só letras, números e dois-pontos.
    if not re.fullmatch(r"[A-Za-z0-9]+:", _prefixo):
        raise ValueError("Prefixo de credencial do conector inválido.")


def consulta_credenciais_cifradas() -> str:
    """Conta os valores cifrados que o portal não pode ter, sem nunca ler o valor."""
    excecoes = "".join(
        f' AND substr("Chave", 1, {len(p)}) <> \'{p}\'' for p in PREFIXOS_CIFRADOS_DO_CONECTOR_TISS
    )
    return 'SELECT count(*) FROM "Configuracoes" WHERE "Valor" LIKE \'enc:%\'' + excecoes


def verificar_sem_credenciais_cifradas(executar_sql) -> None:
    """Recusa a publicação se houver credencial cifrada fora das do conector TISS. Não exibe valores."""
    if str(executar_sql(consulta_credenciais_cifradas())).strip() != "0":
        raise RuntimeError(MENSAGEM_CREDENCIAIS_CIFRADAS)



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


# --------------------------------------------------------------- faxina depois de publicar (nov/2026)
# Em 24/11/2026 o disco da VPS encheu (82 releases em /opt/clinica-tablet/releases e os pacotes que ficavam
# em /home/clinica-admin/tablet-stage) e o Postgres parou de gravar ("could not write init file"). A
# faxina roda SÓ depois de uma publicação SAUDÁVEL e nunca desfaz uma publicação: falhou, vira aviso.
MANTER_RELEASES = 5
SUFIXO_RUNTIME = '-runtime'


def releases_para_remover(releases: dict[str, float], manter: set[str], quantos: int = MANTER_RELEASES) -> list[str]:
    """Os releases que saem: fica o que está em `manter` (o current e o anterior) e os `quantos` mais novos
    (pela data). O `<nome>-runtime` acompanha o seu release: fica e sai junto com ele."""
    principais = {n: t for n, t in releases.items() if not n.endswith(SUFIXO_RUNTIME)}
    novos = sorted(principais, key=lambda n: (principais[n], n), reverse=True)[:quantos]
    ficam = set(manter) | set(novos)
    ficam |= {n + SUFIXO_RUNTIME for n in ficam}
    return sorted(n for n in releases if n not in ficam and (n.removesuffix(SUFIXO_RUNTIME) not in ficam))


def faxina_depois_de_publicar(base: Path, manter: set[str], pacote: Path, quantos: int = MANTER_RELEASES) -> list[str]:
    """Apaga os releases antigos de `base/releases` e o pacote já instalado. Devolve o que saiu."""
    import shutil
    pasta = base / 'releases'
    releases = {p.name: p.stat().st_mtime for p in pasta.iterdir() if p.is_dir() and not p.is_symlink()}
    saiu = []
    for nome in releases_para_remover(releases, manter, quantos):
        alvo = pasta / nome
        if alvo.resolve().parent != pasta.resolve():
            continue  # nunca sai da pasta de releases
        shutil.rmtree(alvo)
        saiu.append(nome)
    if pacote.is_file() and not pacote.is_symlink():
        pacote.unlink()
        saiu.append(pacote.name)
    return saiu


# ---- O papel da ponte do CRM (ponte_crm), reaplicado a cada deploy (nov/2026) ----
#
# Por que existe: o deploy/ponte-crm/papel-ponte-crm.sql era rodado À MÃO, uma vez. Tabela que o
# arquivo ganhou depois (EnviosPacienteCrm, LiberacoesDeTrava, AvisosDeLiberacao) ficava só com o
# SELECT dos privilégios padrão até alguém lembrar — e o primeiro "Enviar ao paciente pelo WhatsApp"
# parou em "queued" com 42501 no claim. Agora o deploy do portal reaplica o MESMO arquivo (sem a
# senha, que não muda) e reprova se faltar qualquer concessão da lista.

PAPEL_PONTE = "papel-ponte-crm.sql"
LINHA_SENHA_PONTE = "ALTER ROLE ponte_crm PASSWORD :senha;"

_PAR_DA_LISTA = re.compile(r"\['(\w+)','([^']+)'\]")
_GRANT_DE_COLUNA = re.compile(r'GRANT (\w+) \(([^)]*)\) ON public\."(\w+)"')


def papel_ponte_para_deploy(texto: str) -> str:
    """O arquivo do papel SEM a troca de senha (o deploy não sabe nem muda a senha da ponte)."""
    if texto.count(LINHA_SENHA_PONTE) != 1:
        raise ValueError("papel-ponte-crm.sql: a linha da senha mudou; confira antes de reaplicar.")
    return texto.replace(LINHA_SENHA_PONTE, "-- (senha: não muda no deploy)")


def concessoes_papel_ponte(texto: str) -> list[tuple[str, str, tuple[str, ...]]]:
    """(tabela, privilégio, colunas) que o arquivo concede à ponte — colunas vazias = a tabela toda."""
    saida: list[tuple[str, str, tuple[str, ...]]] = []
    for tabela, privs in _PAR_DA_LISTA.findall(texto):
        for priv in privs.split(","):
            m = re.fullmatch(r"\s*(\w+)\s*(?:\(([^)]*)\))?\s*", priv)
            if not m:
                raise ValueError(f"Privilégio ilegível para {tabela}: {priv!r}")
            colunas = tuple(c.strip().strip('"') for c in (m.group(2) or "").split(",") if c.strip())
            saida.append((tabela, m.group(1).upper(), colunas))
    for priv, colunas, tabela in _GRANT_DE_COLUNA.findall(texto):
        saida.append((tabela, priv.upper(), tuple(c.strip().strip('"') for c in colunas.split(","))))
    if not saida:
        raise ValueError("papel-ponte-crm.sql sem concessões legíveis.")
    return saida


def conferencia_papel_ponte(texto: str) -> str:
    """SQL que devolve UMA linha por concessão que falta (tabela existente); vazio = tudo concedido."""
    linhas = []
    for tabela, priv, colunas in concessoes_papel_ponte(texto):
        if not re.fullmatch(r"\w+", tabela) or priv not in ("SELECT", "INSERT", "UPDATE", "DELETE"):
            raise ValueError(f"Concessão inesperada: {tabela} {priv}")
        alvo = f"public.\"{tabela}\""
        if colunas:
            for coluna in colunas:
                if not re.fullmatch(r"\w+", coluna):
                    raise ValueError(f"Coluna inesperada: {coluna}")
                linhas.append(
                    f"SELECT '{priv} ({coluna}) em {tabela}' WHERE to_regclass('{alvo}') IS NOT NULL "
                    f"AND NOT has_column_privilege('ponte_crm', '{alvo}', '{coluna}', '{priv}')")
        else:
            linhas.append(
                f"SELECT '{priv} em {tabela}' WHERE to_regclass('{alvo}') IS NOT NULL "
                f"AND NOT has_table_privilege('ponte_crm', '{alvo}', '{priv}')")
    return "\nUNION ALL\n".join(linhas) + ";"
