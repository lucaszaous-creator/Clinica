# Documentos ao paciente por e-mail e WhatsApp (out/2026)

"**Enviar ao paciente…**" existe em **todas** as portas onde um papel é emitido ou recuperado
(2ª via), e abre a **mesma janela** em todas elas:

| Porta | Onde o botão está |
|---|---|
| Central de Documentos (Recepção, Gerente e Consultório — Blazor) | menu "⋯" da linha |
| Aba Documentos da ficha (balcão) | botão "Enviar" da linha |
| Prescrições do Consultório — abre a mesma central de Documentos | menu "⋯" da linha |
| "Já saiu hoje" do atendimento (Blazor, pela ponte `IJanelasDoAtendimentoClinico`) | botão "Enviar…" ao lado da 2ª via |
| Fim da emissão de um documento novo | a janela abre sozinha: "Enviar agora ao paciente?" com "Agora não" |

A janela (`EnvioAoPacienteWindow`) tem três opções, nesta ordem:

1. **E-mail (recomendado)** — PRIMEIRA e já marcada. O endereço é digitado na hora, já
   preenchido com o da ficha quando houver (a clínica não colhe e-mail no cadastro), e conferido
   antes de fechar a janela. A caixinha **"Guardar este e-mail na ficha do paciente"** nasce
   desmarcada. Sai pelo servidor de saída da própria clínica, com o PDF anexado.
2. **WhatsApp (pelo CRM)** — com a frase *"Cada envio pelo WhatsApp é cobrado pela Meta — prefira o
   e-mail."* e o celular da ficha. Vai para a fila que o CRM envia pela API oficial da Meta.
3. **Abrir no WhatsApp deste computador** — o caminho de antes (wa.me + Ctrl+V), para quando o CRM
   está desligado.

Opção indisponível aparece **apagada com o motivo**, nunca some: o e-mail só fica indisponível
sem servidor SMTP configurado (e a frase diz onde configurar); o WhatsApp, sem celular válido na
ficha ou quando o mesmo papel já está na fila do CRM.

**O que pode sair**: receita, atestado, pedido de exame e declaração de comparecimento — com ou
sem assinatura digital — e qualquer outro documento clínico **assinado**. Cancelado não sai.
A **folha de infusão não sai** (decisão da direção): é folha interna de execução da enfermagem,
não papel do paciente, e nenhuma das telas dela tem o botão. O arquivo é sempre a **2ª
via**: os bytes guardados quando assinado (nunca se regenera um PDF assinado), a mesma montagem
da reimpressão quando não (`DocumentosClinicosPdfService.GerarAsync` com o prestador).

## 🧑‍💻 UX — quem atende

- **Uma janela, um clique para enviar**: o e-mail já vem marcado; quem atende confere o endereço
  com o paciente e clica "Enviar". Trocar para WhatsApp é um clique a mais, com o custo escrito ao
  lado.
- **Nada é automático por e-mail**: é sempre o clique de quem atende, a pedido do paciente.
- **O que aconteceu fica escrito na tela**: "Receita 2026/0012 enviado por e-mail para x@y. O
  servidor aceitou; e-mail não tem confirmação de leitura", "…na fila do CRM para o WhatsApp
  (11) 99999-0001… vale por 24 horas. Acompanhe em CRM → Envios do Clínico", ou o motivo (servidor
  recusou com a frase dele, sem celular válido, documento cancelado). No fim da emissão, o
  resultado vem num aviso, porque a janela de emissão fecha em seguida.
- **Segundo clique não duplica** o pedido na fila do CRM.
- **Atestado sem assinatura digital**: a janela avisa que, em arquivo, ele não substitui a via
  impressa e assinada (art. 13 da Lei 14.063/2020) e vai como cópia. Avisa, não impede. A
  mensagem que acompanha o papel sem certificado não diz "assinado".
- **As duas barreiras**: o botão só aparece com o estado E o bit do TIPO do documento
  (`OferecerEnviar`); o ato confere de novo (`Exigir`).

## 🛠️ DX — quem mantém

- **Uma definição só**: `AcoesDoDocumento.EnviarAsync` (com `aposEmitir` para o fim da
  emissão), sobre a `EnvioAoPacienteWindow`. As listas já usavam `AcoesDoDocumento` e o
  `MenuDoDocumento`; o "já saiu hoje" e o fim da emissão passaram a usar também.
  `PortasDoEnvioAoPacienteTests` lista as portas e falha se uma delas deixar de passar pelo ponto
  único.
- **A regra mora na Application** (com teste):
  - `DestinosDoEnvio.Opcoes` / `.Padrao` — as três opções, a ordem (e-mail primeiro), qual vem
    marcada, o motivo de cada indisponível e a frase do custo do WhatsApp. A janela só desenha.
  - `EnvioAoPacienteService` — o que pode sair, a fila do CRM, o e-mail (endereço digitado,
    `guardarNaFicha` → `PacienteService.DefinirEmailAsync` **depois** do aceite do servidor), o
    caminho local, e a **trilha com QUEM, por qual CANAL e PARA ONDE** em toda saída
    (`DocumentoEnviadoPorEmail`, `DocumentoEmailRecusado`, `DocumentoNaFilaWhatsAppCrm`,
    `DocumentoAbertoNoWhatsAppLocal`; `EmailGuardadoNoEnvio` quando a ficha muda).
  - `FilaEnviosCrm` + `FilaEnviosCrmService` — o protocolo que o `ClinicalDispatchWorker` do CRM
    já consumia: listar **sem** texto nem PDF (projeção por expressão), reservar (protocolo 2;
    repetível pelo mesmo CRM; reserva de outro CRM vence em 10 min), **autorização final de uso
    único** (a segunda vira `uncertain`; corrida resolvida pela coluna `Versao`, sem transação à
    mão — compatível com a estratégia de repetição), relato sem regressão (`PodeIr`), validade de
    24 h, e documento/prescrição cancelados depois do pedido bloqueando até na autorização final.
    O PDF sai da linha quando o pedido sai da fila.
  - `TextoDoEnvio` — legenda, assunto e corpo do e-mail (curto, sem nada clínico).
- **A ponte só traduz HTTP**: `ClinicDispatchBridge` + `RotasDaFilaDeEnvios` (GET
  `/crm/v1/dispatches?consumer=`, POST `/{id}/claim`, `/{id}/submit`, `/{id}/report`) atrás da
  mesma rede, chave e tradução de recusa das outras rotas. `ClinicContracts.cs` **não mudou**.
- **E-mail com anexo**: `IEnviadorDeEmail.EnviarComAnexoAsync` (método novo com corpo padrão que
  recusa — os enviadores falsos existentes continuam compilando); `EnviadorSmtp` implementa os
  dois. O lembrete por e-mail não mudou.
- **Migração aditiva** `20261019194147_EnviosPacienteCrm`: tabela nova, FKs para `Pacientes`,
  `DocumentosClinicos` e `PrescricoesInternas` com `Restrict` (nada clínico apaga em cascata),
  datas `timestamp without time zone` na hora da clínica (`HoraDaClinica`).
- **Papel restrito da ponte**: `deploy/ponte-crm/papel-ponte-crm.sql` ganhou **só** `UPDATE` em
  `EnviosPacienteCrm` (quem insere é o desktop).
- **Testes**: `EnvioAoPacienteTests` (canais e padrão, fila, e-mail, endereço digitado, guardar na
  ficha, trilha, protocolo, corrida de autorização, matriz), `PortasDoEnvioAoPacienteTests`
  (cada porta pelo ponto único), `PonteClinicDispatchTests` (as quatro rotas num servidor de
  verdade, com a estratégia que repete), `PontePapelRestritoTests.Fila_de_envios_ao_paciente` (o
  fluxo como `ponte_crm` — **só roda com `CLINICA_TESTES_POSTGRES`**).

## 🤝 CX — a clínica

- **Custo**: o e-mail é o padrão porque não custa nada por envio (sai do servidor que a clínica
  já paga); cada envio pelo WhatsApp oficial iniciado pela clínica é cobrado pela Meta, e a
  janela diz isso no lugar da decisão.
- **Parece profissional**: o documento chega do e-mail ou do número oficial da clínica (com o
  modelo aprovado `semdor_documento` fora da janela de 24 h), não do celular de alguém.
- **Risco**: nada sai sem configuração; o envio pelo CRM nunca é repetido automaticamente quando
  o resultado é incerto; o pedido vence em 24 h; o PDF não fica guardado em duplicata depois de
  enviado; **ligar a fila de documentos não desliga o lembrete das 06:00 nem a mensagem de
  marcação do CRM** (a centralização virou uma chave separada no CRM).
- **O dia de quem atende**: "manda a receita no meu e-mail" se resolve na própria tela onde o papel
  foi emitido, sem abrir outro programa.

## Como ligar

1. **Release dos cinco apps PRIMEIRO** (ela aplica a migração `EnviosPacienteCrm` na abertura).
   Só depois:
2. **Rodar de novo** `deploy/ponte-crm/papel-ponte-crm.sql` como `postgres` na VPS 1 (concede o
   `UPDATE` em `EnviosPacienteCrm`; rodar de novo é seguro).
3. **Publicar a ponte nova** (VPS 1). Ponte nova antes da migração devolveria erro nas rotas da fila.
4. **CRM** (branch `claude/documentos-crm`, VPS 2), variáveis:
   - `CRM_CLINICAL_DISPATCH_ENABLED=true`
   - `CRM_CLINICAL_DOCUMENT_TEMPLATE=semdor_documento` (opcional `CRM_CLINICAL_DOCUMENT_LANGUAGE`, padrão `pt_BR`)
   - `CRM_PUBLIC_URL` — já configurada (o provedor busca o PDF por link temporário)
   - **não** ligar `CRM_CLINICAL_CENTRALIZED_CONTACTS` (padrão desligado) — é ela, e não mais a de
     cima, que cala o lembrete e o recall do CRM.
   - `CRM_CLINICAL_SENDER` só se houver mais de uma caixa de WhatsApp no CRM.
5. **E-mail**: nada no CRM. Basta o servidor de saída da clínica em Gerente → Configurações →
   Lembretes por e-mail (o mesmo do lembrete).

## O que NÃO foi feito, e por quê

- **Situação do envio na linha do documento** ("entregue 10:41"): o resultado é dito no clique e o
  acompanhamento fica em CRM → Envios do Clínico; mostrar o estado em cada lista seria uma leitura
  nova por linha em várias telas.
- **E-mail pelo CRM**: o documento sai do servidor da clínica, direto do desktop — as credenciais
  SMTP não são copiadas para o CRM e o documento não passa por mais um servidor.
- **Envio automático por e-mail**: não existe, por decisão.
- **Folhas financeiras (recibo, orçamento)**: fora do escopo do pedido (são outro PDF e outro
  serviço); continuam com a 2ª via.
- **A janela de envio é WPF**, aberta pelo hospedeiro também a partir das telas Blazor — como as
  outras janelas dos atos do documento (assinar, emitir). Uma versão HTML na tela Blazor seria uma
  segunda definição.
- **Nenhum teste com o Postgres de verdade foi rodado nesta entrega** (sem Postgres no ambiente):
  `PontePapelRestritoTests.Fila_de_envios_ao_paciente` e a suíte com `CLINICA_TESTES_POSTGRES`
  precisam rodar antes do merge. As janelas não foram abertas no Windows nem fotografadas; a
  compilação das telas e o build da solução passaram.
- **Homologação com a Meta** (modelo `semdor_documento` aprovado de verdade, recibo real) e com o
  SMTP real da clínica: pendente.
