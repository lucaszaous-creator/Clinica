# Web service TISS da Saúde Petrobras — primeiro passo: elegibilidade

**Pedido da direção (11/11/2026):** parar de digitar no Portal TISS da Saúde Petrobras o que o
sistema já sabe. Hoje a secretária abre o portal, marca "Não" em "Você possui o Número da Guia do
Prestador?", preenche a guia de consulta inteira, salva, copia o número que o portal gerou e cola
no sistema ("dar baixa"). O alvo é a baixa no sistema registrar a guia na operadora.

Este documento cobre o **primeiro passo**, que não muda tela nenhuma: provar a conexão com o web
service da operadora por uma consulta que não cobra nada — a **verificação de elegibilidade**.

## O que a operadora publica

"Manual de Utilização Webservice TISS 4.01.00" (Saúde Petrobras, conectividade CRC):

- 12 serviços SOAP em `https://webservices.crc.com.br/tiss/ws/40100/`, entre eles
  `tissVerificaElegibilidade`, `tissLoteGuias`, `tissSolicitacaoStatusProtocolo`,
  `tissSolicitacaoDemonstrativoRetorno`, `tissRecursoGlosa` e `tissCancelaGuia`;
- login e senha **os mesmos da conectividade** do Portal TISS, com a senha em MD5;
- **só endereços de produção**: o manual não cita homologação.

Conferido em 11/11/2026, sem credencial: os WSDL de 4.01.00 respondem; o caminho equivalente com
`40300` devolve 404. Uma mensagem de elegibilidade com login fictício voltou `LoginInvalido`,
a falha do próprio padrão TISS: o serviço leu a mensagem e recusou o login. É bom sinal para o
formato, mas **não prova** que hash e versão passam — não se sabe em que ordem ele confere.

✅ **Resultado do teste (11/11/2026, 19:22):** com o login da clínica e a carteira de um paciente
Petrobras, a operadora respondeu **elegível** e devolveu o nome do beneficiário. Uma consulta, em
produção, pela ferramenta `tools/TissPetrobras`. Isso prova, para a **elegibilidade**: o login e a
senha da conectividade valem no web service (senha em MD5 hexadecimal minúsculo), o código do
prestador é o CNPJ só com dígitos, o hash em ISO-8859-1 é aceito e o serviço responde em 4.01.00.

⚠️ **Continua em aberto:** a operadora avisou que não aceita 4.01.00 desde 01/08/2026. A
elegibilidade em 4.01.00 passou; **o envio de lote de guias ainda não foi testado** e pode ter
regra diferente. Nenhum texto do sistema afirma que a Petrobras aceita o envio de guias.

## As opções para o primeiro teste

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| **A. Elegibilidade por ferramenta de linha de comando** (esta) | Nenhuma tela muda; ninguém na clínica percebe | Duas classes novas sem dependência, testadas contra o XSD oficial; nada no banco | Não cobra, não cria guia; uma consulta leva a carteira à operadora, como o portal já faz |
| B. Status de um protocolo já enviado | Igual | Igual, mas exige um protocolo de lote por XML, e a Petrobras hoje é digitada guia a guia | Não envolve paciente, mas não havia protocolo para consultar |
| C. Enviar logo um lote de uma guia | Seria a primeira guia sem digitação | Exige completar a guia antes (executante, CBO, valor, tipo de consulta) e mexer no gerador que fatura a clínica | É cobrança real em produção, sem ensaio: erro vira glosa ou duplicidade |
| D. Robô no portal | Nenhuma | Frágil a cada mudança de tela; o login tem código de verificação | Canal não oficial; risco de bloqueio do acesso da clínica |

**Recomendação: A.** É a única que testa login, hash e formato em produção sem efeito financeiro
e sem tocar no faturamento que roda na clínica. A direção escolheu testar com um paciente real.

## O que entrou

- `TissElegibilidadeWs` (Application): monta `pedidoElegibilidadeWS` (cabeçalho, login, senha em
  MD5, hash) e o envelope SOAP; lê a resposta em **três estados** — elegível, não elegível e
  **não avaliado** (login, hash, schema, rede, glosa da mensagem). Falha nunca vira "não elegível".
- `TissWebserviceCliente` (Application): o POST. Só HTTPS; lê a falha no corpo do HTTP 500, como
  o SOAP a devolve; decodifica pela declaração do XML (a operadora responde em ISO-8859-1).
- `tools/TissPetrobras`: a ferramenta. `--simular` mostra a mensagem com a senha mascarada e não
  envia; sem ele, consulta em produção. Senha só por variável de ambiente.
- `tests/Clinica.Tests/Tiss/xsd-4.01.00/`: os XSD oficiais do padrão (baixados do próprio web
  service da operadora), e `TissElegibilidadeWsTests` valida a mensagem contra eles.

O que **não** entrou, de propósito: nenhuma tela, nenhuma migration, nenhuma mudança no
`TissExportService` nem no `ElegibilidadeService` (a conferência interna continua igual), nenhum
lugar para guardar a credencial e nenhum registro no banco.

## 🧑‍💻 UX

Nada muda para quem atende: não há botão, aviso nem tela. A consulta é rodada por quem implanta,
num terminal. Quando ela entrar no balcão (próximo passo), a regra já está escrita: a recepção
precisa distinguir "a operadora disse que não" de "não consegui perguntar" — os dois têm textos
diferentes em `RespostaElegibilidadeTiss.Resumo`, e o segundo nunca pode travar o atendimento.

## 🛠️ DX

- Duas classes e uma ferramenta; nenhuma dependência nova (só `HttpClient` e `System.Xml` da BCL).
- A regra mora num lugar: `TissElegibilidadeWs`. O transporte não conhece a transação.
- Testa-se sem rede e sem credencial: 24 testes, incluindo validação no XSD oficial (com a prova
  de que o XSD reprova mensagem errada) e uma rede de mentira para o transporte.
- O hash daqui é em **ISO-8859-1**, a regra do padrão. O `TissExportService` calcula o do lote em
  UTF-8: com texto só ASCII os dois coincidem, com acento não. **Não foi alterado** — ele fatura a
  clínica hoje — e fica registrado como ponto a conferir antes de o lote ir por web service.
- Os XSD de 4.01.00 agora estão no repositório (pasta de testes). O `TissValidador` continua lendo
  o XSD de `%APPDATA%`; ligar um ao outro é o próximo ganho barato.

## 🤝 CX

- **Custo:** nenhum serviço pago; a consulta de elegibilidade não gera cobrança nem guia.
- **Risco em produção:** nenhum app instalado muda. O risco é o da própria consulta: errar o
  login repetidas vezes pode bloquear o acesso da clínica ao portal — por isso a sonda de formato
  foi feita com login fictício, e a consulta real é uma por vez.
- **Dado pessoal:** a consulta envia à operadora o número da carteira, e ela devolve o nome do
  beneficiário. É a mesma troca que a recepção já faz no Portal TISS, com a mesma operadora, para
  a mesma finalidade (faturar o atendimento). A ferramenta não grava a resposta. O ponto 10 do
  compromisso de conformidade pede decisão para **dado clínico novo** em serviço externo: aqui não
  vai dado clínico (nem procedimento, nem CID, nem prontuário) e o destinatário não é novo; ainda
  assim, a decisão de ligar isto numa tela fica registrada como pendente da direção.
- **Credencial:** hoje não é guardada em lugar nenhum do sistema. Antes de qualquer tela, é
  preciso decidir onde ela mora (cifrada, lida só no servidor) — não entra neste passo.

## Lote, situação do protocolo e cancelamento (nov/2026)

Três transações do padrão 4.01.00 ganharam o par "monta/lê" no molde da elegibilidade — só texto, sem rede, cada pedido validado no XSD oficial (`tissWebServicesV4_01_00.xsd`):

- **`TissLoteGuiasWs`** (`ENVIO_LOTE_GUIAS`, serviço `tissLoteGuias`): embrulha em SOAP o `loteGuiasWS` — cabeçalho com `loginSenhaPrestador`, `loteGuias` (número do lote + guias) e o `hash` em ISO-8859-1. As guias entram prontas; a de consulta conferida na baixa sai de `TissExportService.GuiaConsultaConferida`. Recusa antes do envio o lote vazio, com mais de 100 guias, com tipos misturados ou com número de guia repetido. A resposta (`protocoloRecebimentoWS`) vira `Recebido` em três estados: `true` com o protocolo, `false` quando a operadora devolve `mensagemErro` no lugar do protocolo, `null` quando não se sabe (falha de login, schema, hash, rede, página de erro).
- **`TissStatusProtocoloWs`** (`SOLIC_STATUS_PROTOCOLO`, serviço `tissSolicitacaoStatusProtocolo`): pergunta pelo número do protocolo e lê a situação do lote (1 Recebido … 9 Cancelado), os valores e as guias. **O padrão não traz situação por guia**: cada guia volta com os números, os valores processado/glosado/liberado e as glosas.
- **`TissCancelaGuiaWs`** (`CANCELA_GUIA`, serviço `tissCancelaGuia`): cancela guias de faturamento (`tipoGuia` 2) ou o lote inteiro — nunca os dois no mesmo pedido. O recibo vira `Cancelado`: `true` só com tudo no status 1; `false` com alguma recusa (não cancelado, guia/lote/protocolo inexistente, `mensagemErro`); `null` enquanto estiver "em processamento" (4) ou sem resposta legível.

**Onde volta o número da guia na operadora** (`numeroGuiaOperadora`, opcional no XSD nos dois lugares):
- no envio: `recebimentoLote/protocoloRecebimento/detalheProtocolo/dadosGuiasProtocolo/dadosGuias`;
- na situação: `situacaoProtocolo/situacaoDoProtocolo/lote/detalheLote/guiasTISS/guiasMedicas/guias`.

Em código é `GuiaNoProtocoloTiss.NumeroGuiaOperadora`, `null` quando a operadora não manda. A chave garantida para casar resposta e guia é o `numeroGuiaPrestador`, gerado por `NumeroGuiaPrestadorTiss` ("C" + Id do código de faturamento; reenvio da mesma guia ganha sufixo, "C1234R2") e lido de volta por `TentarLer`.

**Transporte:** `TissWebserviceCliente.EnviarAsync(endereço, envelope)` serve às três (e à elegibilidade): só HTTPS, bytes em ISO-8859-1, corpo lido também no HTTP 500. Devolve `RespostaBrutaTissWs`; quem interpreta é o `LerResposta` da transação.

**O que NÃO está provado** (nada disto foi enviado à operadora; só a elegibilidade foi):
- que a Saúde Petrobras aceita o lote pelo web service como aceita a elegibilidade, e que devolve o protocolo de forma síncrona;
- que ela devolve `numeroGuiaOperadora` — no XSD é opcional;
- que ela aceita `numeroGuiaPrestador` com letra ("C1234"); no portal é ela quem numera. Se exigir só dígitos, muda `NumeroGuiaPrestadorTiss`;
- se o CNPJ vai em `codigoPrestadorNaOperadora` também no lote (na elegibilidade foi aceito assim);
- o que ela faz com reenvio do mesmo número de guia ou de lote, e se o cancelamento responde na hora ou fica "em processamento";
- que ela aceita o cancelamento por web service para guia já em análise;
- o formato real das respostas: as dos testes foram escritas a partir do XSD (e validadas nele), não capturadas da operadora;
- o demonstrativo de retorno: há só o endereço como constante, sem classe de mensagem.

⚠️ Lote sem resposta (rede caiu depois do POST) fica `Recebido = null`: **pode ter chegado**. Não reenviar no automático; a consulta de situação pede o número do protocolo, que nesse caso não se tem — confirmar com a operadora.

**Quem chama estas mensagens** é o conector no servidor (`src/Clinica.Tiss.Conector`), a partir da fila
`TransacoesTiss`: ver `docs/tiss-conector.md`, "As três frentes ligadas". Lá estão o caminho de uma
guia do balcão à baixa, as duas chaves que seguram o envio e a baixa em lote das guias recebidas.

## O que vem depois (nesta ordem)

1. ~~Rodar a consulta com o login da clínica e um paciente Petrobras.~~ Feito em 11/11/2026: elegível.
2. Completar a guia de consulta do XML com o que a tela do portal exige e o gerador não envia:
   profissional executante (conselho, número, UF, CBO), indicação de acidente, regime de
   atendimento, tipo de consulta e valor.
3. Enviar **uma** guia real por `tissLoteGuias`, de uma consulta que não foi digitada no portal,
   e conferir no portal. Confirmar se a operadora aceita o número de guia do prestador (no portal
   a clínica sempre marca "Não") e onde ela devolve o número dela.
4. Só então: o botão de baixa, a guarda da credencial e a fila de envio.
