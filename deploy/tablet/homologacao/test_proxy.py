import asyncio
import importlib.util
import pathlib
import unittest

spec=importlib.util.spec_from_file_location('proxy',pathlib.Path(__file__).with_name('safeid-proxy.py'))
proxy=importlib.util.module_from_spec(spec)
spec.loader.exec_module(proxy)

class FronteiraProxy(unittest.IsolatedAsyncioTestCase):
    async def test_destinos_nao_autorizados_nao_recebem_tunel(self):
        async with await asyncio.start_server(proxy.atender,'127.0.0.1',0) as server:
            port=server.sockets[0].getsockname()[1]
            for host in ['localhost:443','127.0.0.1:45432','pscsafeweb.safewebpss.com.br:80',
                         'pscsafeweb.safewebpss.com.br.atacante.test:443','usuario@pscsafeweb.safewebpss.com.br:443']:
                r,w=await asyncio.open_connection('127.0.0.1',port)
                w.write(f'CONNECT {host} HTTP/1.1\r\nHost: {host}\r\n\r\n'.encode());await w.drain()
                self.assertIn(b'403 Forbidden',await asyncio.wait_for(r.read(),2))
                w.close();await w.wait_closed()

    async def test_proxy_nao_aceita_http_sem_connect(self):
        async with await asyncio.start_server(proxy.atender,'127.0.0.1',0) as server:
            r,w=await asyncio.open_connection('127.0.0.1',server.sockets[0].getsockname()[1])
            w.write(b'GET https://pscsafeweb.safewebpss.com.br:443 HTTP/1.1\r\n\r\n');await w.drain()
            self.assertNotIn(b'200',await asyncio.wait_for(r.read(),2))
            w.close();await w.wait_closed()

if __name__=='__main__':unittest.main()
