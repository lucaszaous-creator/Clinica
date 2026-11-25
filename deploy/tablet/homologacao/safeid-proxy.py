"""CONNECT local com lista fechada de hosts. Não termina TLS nem registra tráfego.

O mesmo script atende a homologação (porta 18743, unit clinica-safeid-hml) e a produção
(porta 18744, unit ../clinica-safeid.service, copiado para /opt/clinica-safeid-proxy):
a porta vem do primeiro argumento, para a produção não depender de editar o arquivo.
"""
import asyncio
import ipaddress
import os
import re
import socket
import sys

SAFEWEB = {'pscsafeweb.safewebpss.com.br', 'pscsafeweb-homologacao.safewebpss.com.br'}


def hosts_extras(valor):
    """Hosts a mais (nov/2026: o armazenamento da PUBLICAÇÃO do documento assinado, ex. o R2). Lista fechada,
    vinda da unit (Environment=PROXY_HOSTS_EXTRAS=a.exemplo,b.exemplo): só nomes DNS completos, sem porta, sem
    curinga, sem IP. Valor inválido derruba a partida — melhor parar do que abrir um túnel para o lugar errado."""
    extras = set()
    for nome in filter(None, (h.strip().lower() for h in (valor or '').split(','))):
        if not re.fullmatch(r'(?=.{4,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}', nome):
            raise ValueError('PROXY_HOSTS_EXTRAS: host inválido: ' + nome)
        extras.add(nome)
    return extras


HOSTS = SAFEWEB | hosts_extras(os.environ.get('PROXY_HOSTS_EXTRAS'))
PORT = 18743
conexoes = set()

async def atender(reader, writer):
    remoto = None
    tarefa = asyncio.current_task()
    if len(conexoes) >= 24:
        writer.close()
        return
    conexoes.add(tarefa)
    try:
        cabecalho = await asyncio.wait_for(reader.readuntil(b'\r\n\r\n'), 5)
        if len(cabecalho) > 8192:
            return
        linha = cabecalho.split(b'\r\n', 1)[0].decode('ascii')
        metodo, destino, versao = linha.split(' ')
        host, porta = destino.rsplit(':', 1)
        if metodo != 'CONNECT' or host not in HOSTS or porta != '443' or versao not in {'HTTP/1.0', 'HTTP/1.1'}:
            writer.write(b'HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n')
            await writer.drain()
            return
        enderecos = await asyncio.wait_for(asyncio.get_running_loop().getaddrinfo(host, 443, type=socket.SOCK_STREAM), 5)
        publicos = [e[4][0] for e in enderecos if ipaddress.ip_address(e[4][0]).is_global]
        if not publicos:
            return
        entrada, remoto = await asyncio.wait_for(asyncio.open_connection(publicos[0], 443), 10)
        writer.write(b'HTTP/1.1 200 Connection Established\r\n\r\n')
        await writer.drain()
        async def copiar(origem, alvo):
            while bloco := await asyncio.wait_for(origem.read(65536), 60):
                alvo.write(bloco)
                await alvo.drain()
        fluxos = [asyncio.create_task(copiar(reader, remoto)), asyncio.create_task(copiar(entrada, writer))]
        try:
            await asyncio.wait(fluxos, timeout=300, return_when=asyncio.FIRST_COMPLETED)
        finally:
            for fluxo in fluxos:
                fluxo.cancel()
            await asyncio.gather(*fluxos, return_exceptions=True)
    except (Exception, asyncio.CancelledError):
        pass  # Nunca incluir dados de conexão, tokens ou requisições em logs.
    finally:
        conexoes.discard(tarefa)
        if remoto:
            remoto.close()
        writer.close()

async def main(porta=PORT):
    server = await asyncio.start_server(atender, '127.0.0.1', porta, limit=8192)
    async with server:
        await server.serve_forever()

if __name__ == '__main__':
    porta = int(sys.argv[1]) if len(sys.argv) > 1 else PORT
    assert 1024 < porta < 65536
    asyncio.run(main(porta))
