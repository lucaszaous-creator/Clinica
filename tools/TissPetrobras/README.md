# Consulta de elegibilidade na Saúde Petrobras (web service TISS)

Ferramenta de linha de comando, sem tela e sem banco: pergunta ao web service TISS da Saúde
Petrobras se uma carteira está ativa. Serve para **provar a conexão** (login, senha em MD5, hash e
formato da mensagem) antes de a consulta entrar num app. Nenhum app a chama, e ela não grava nada.

A decisão, as opções comparadas e o que falta estão em `docs/tiss-webservice-petrobras.md`.

## Uso

```sh
# Só montar e ver a mensagem (senha mascarada). Não envia nada e não pede credencial.
dotnet run --project tools/TissPetrobras -- elegibilidade --carteira 000000000000 --simular

# Consultar de verdade. NÃO há homologação no manual da operadora: isto é produção.
TISS_PETROBRAS_LOGIN=... TISS_PETROBRAS_SENHA=... TISS_PETROBRAS_CODIGO_PRESTADOR=... \
  dotnet run --project tools/TissPetrobras -- elegibilidade --carteira NUMERO --ocultar-nome
```

Código de saída: `0` elegível, `1` não elegível, `3` a operadora não avaliou (login, hash,
schema ou rede), `2` uso incorreto.

## Cuidados

- **Senha só por variável de ambiente**, nunca como argumento (argumento fica no histórico do
  terminal e na lista de processos) e nunca em arquivo do repositório. A ferramenta não a imprime.
- **A consulta leva o número da carteira à operadora** — o mesmo dado que a recepção já digita no
  Portal TISS. A resposta traz o nome do beneficiário: use `--ocultar-nome` ao copiar o resultado
  para chamado, chat ou PR.
- **Uma consulta por paciente atendido.** Não use para varrer carteiras: elegibilidade é
  verificação de atendimento, não pesquisa.
- O endereço e o registro ANS (422631) vêm do "Manual de Utilização Webservice TISS 4.01.00" da
  operadora e estão em `TissWebserviceCliente`.
