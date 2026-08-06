# Prescrição e documentos clínicos: o que a lei exige, e o que o sistema faz

Parcela 43. Este documento é o mapa legal do que sai da clínica em papel ou em arquivo —
receita, atestado, declaração de comparecimento e pedido de exame (feature 07) — e o que o
sistema garante em cada caso. Ele existe porque "está dentro da lei?" tem respostas
diferentes para o papel e para o arquivo, e confundir as duas é o jeito mais rápido de
entregar ao paciente um documento que a farmácia recusa.

## O resumo, para quem só quer a resposta

| Situação | Vale? | O que o sistema faz |
|---|---|---|
| Receita **impressa e assinada à caneta** | **Sim, sempre valeu** | Imprime com todos os campos do art. 35 e avisa o que falta |
| Receita **em arquivo**, assinada com ICP-Brasil | **Sim** | Assina, guarda os bytes e diz no rodapé onde conferir |
| Atestado **impresso e assinado à caneta** | **Sim** | Idem |
| Atestado **em arquivo** sem certificado | **Não** (art. 13 da Lei 14.063/2020) | A tela avisa antes de emitir |
| Receita de **controle especial** em arquivo | **Fora de escopo** | O sistema não emite; depende do SNCR da ANVISA |
| Folha de infusão (execução interna) | **Sim** | Assinada desde a parcela 42; é ato interno, dispensado pela própria lei |

## As três normas que mandam aqui

### 1. Lei 14.063/2020, art. 13 — o que EXIGE assinatura qualificada

> Receituário de medicamentos sujeitos a controle especial e **atestado médico** em meio
> eletrônico só são válidos com **assinatura eletrônica qualificada** (ICP-Brasil).
> *Não se aplica aos atos internos do ambiente hospitalar.*

Duas consequências para nós:

- **O atestado em arquivo é o único documento da feature 07 que a assinatura não melhora —
  ela o faz existir.** Sem certificado, um atestado em PDF não é um atestado com defeito, é
  um arquivo sem valor jurídico. Por isso `ConformidadeDocumentoClinico` o marca, e a marca
  SOME quando a folha vai ser assinada: em papel, com a caneta da médica, ele sempre valeu.
- **A folha de infusão da parcela 42 é justamente o caso dispensado** (ato interno). Ela é
  assinada mesmo assim, o que é entregar mais do que a lei pede — e foi feito primeiro, o
  que é a ironia que esta parcela corrige.

### 2. Lei 14.063/2020, art. 14 — o resto

Os demais documentos assinados por profissional de saúde valem com assinatura **avançada
ou qualificada**. Receita simples, pedido de exame, declaração de comparecimento e
relatório de evolução caem aqui.

O sistema usa a **qualificada** de qualquer jeito, e a razão é prática, não jurídica: a
qualificada é a que o farmacêutico e o RH conseguem conferir sozinhos, num validador
público, sem cadastro em plataforma nenhuma. Uma assinatura avançada exigiria que a outra
ponta aceitasse o nosso mecanismo — e nós não temos ponta nenhuma do lado de fora.

### 3. Lei 5.991/1973, art. 35 — o CONTEÚDO da receita

É a norma que decide se a farmácia pode **aviar** a receita, e ela vale igual no papel e no
arquivo. Exige: vernáculo, sem abreviaturas, legível; **nome e endereço residencial do
paciente**; **modo de usar** do medicamento; data e assinatura do profissional; endereço do
consultório; e o **número de inscrição no conselho** do prescritor.

O sistema imprimia receita desde a parcela 3 e **não tinha onde guardar o endereço do
paciente** — a clínica descobria a exigência na farmácia, com o paciente na fila. Agora:

- `Paciente.Endereco` existe, com campo no cadastro da Recepção e o rótulo dizendo para que
  serve (campo sem explicação fica em branco);
- o endereço sai impresso **só na receita** — num atestado ele iria para a mão do RH sem
  nenhuma exigência que o justifique, e a economia é a mesma do CID;
- `ConformidadeDocumentoClinico.Conferir` lista o que falta, **na tela, enquanto se
  escreve**, com o fundamento escrito ao lado.

## Onde o sistema AVISA e onde ele IMPEDE

A regra da casa é avisar: quem decide é quem assina. Há uma exceção, e ela é a **assinatura
digital**.

Assinar sela os bytes. Corrigir o endereço depois exige cancelar o documento e emitir
outro, e a essa altura o arquivo já está com o paciente — com toda a aparência de oficial.
Um PDF criptograficamente impecável de uma receita que a farmácia não pode aviar é
exatamente o objeto que este projeto se recusa a produzir desde a parcela 3: uma garantia
aparente. Por isso `AssinaturaDeDocumentoClinicoService` **recusa** assinar documento com
exigência impeditiva por cumprir, e a recusa lista o que falta, item a item.

É a quarta recusa do projeto, junto da divergência do fechamento de caixa, da rodela sem
justificativa e do descarte de problema sem motivo.

## O balcão da farmácia consegue conferir? Consegue, de graça — e é este o endereço

A primeira leitura do problema concluiu que faltava construir um **portal público de
validação**, e que isso seria o item caro. Estava errado: o portal existe, é do governo, e
é feito exatamente para isto.

**[assinaturadigital.iti.gov.br](https://assinaturadigital.iti.gov.br)** — o *Validador de
Documentos Digitais em Saúde*, do ITI **com apoio do CFM e do CFF**. Ele **não é** o
validador genérico de assinaturas (`validar.iti.gov.br`), e a diferença é justamente a
pergunta do farmacêutico:

| Pergunta do balcão | Validador genérico | Validador de saúde |
|---|---|---|
| O arquivo foi alterado depois de assinado? | sim | sim |
| A assinatura é de quem o documento diz? | sim | sim |
| **Quem assinou é prescritor com registro ATIVO?** | não | **sim** |
| Registrar a dispensação | não | **sim** (farmacêutico com certificado) |

É o endereço que os CRFs mandam usar, aceita **PDF**, é gratuito e não exige cadastro para
conferir. Por isso o documento assinado sai com **três coisas** que a folha de papel não
tem: o endereço escrito por extenso, um **QR** que leva direto à
[página do farmacêutico](https://assinaturadigital.iti.gov.br/farmaceutico/) e um bloco
**"PARA O FARMACÊUTICO"** com o passo a passo — porque um PDF assinado que chega sem uma
palavra sobre como verificá-lo é recusado por precaução, e o farmacêutico está certo em
recusá-lo: pela orientação dos CRFs, farmácia que não consegue verificar **não é obrigada
a dispensar**.

**O que o QR faz e o que ele não faz.** Ele leva ao validador — não carrega o documento.
Quem hospeda a receita num endereço público é uma plataforma (Memed, Mevo, a do CFM), e nós
não hospedamos nada; prometer que o QR "abre a receita" seria mentir sobre o que ele faz.
Na prática o balcão escaneia, cai na página certa e envia o arquivo que o paciente trouxe.

**Se a clínica quiser zero fricção**, o caminho é a integração gratuita com Mevo ou Memed:
lá a receita nasce hospedada, o QR resolve para o documento e a farmácia da rede a lê sem
upload nenhum. Ver `docs/apis-integracao-prescricao.md`.

**A via impressa de um documento assinado é uma CÓPIA.** A assinatura vive nos bytes do
arquivo, não na tinta: imprimir deixa a garantia para trás. O rodapé diz isso em vermelho,
e é por isso que assinar **salva e abre o arquivo** em vez de mandar para a impressora.

## O que o sistema NÃO faz, e por quê

- **Receituário de controle especial em meio eletrônico.** Depende do SNCR da ANVISA, cuja
  documentação técnica saiu em junho/2026 e cujo prazo é 30/09/2026. Não está implementado
  e, de propósito, `ConformidadeDocumentoClinico.ExigeAssinaturaQualificada` não o lista —
  citá-lo daria a entender que o sistema o cobre.
- **Detectar antimicrobiano.** A orientação dos conselhos é que receita de **antimicrobiano**
  em meio eletrônico também seja assinada com ICP-Brasil (RDC 20/2011 + retenção na
  farmácia). O sistema **não sabe** se o que foi escrito é antimicrobiano — o campo é texto
  livre, sem base de medicamentos —, então não há como avisar automaticamente. A regra
  prática para a clínica: **receita de antimicrobiano que sai em arquivo, assine**. Em papel,
  assinada à caneta e retida na farmácia, segue como sempre foi.
- **LTV / PAdES-LT** (embutir CRL/OCSP para o arquivo continuar verificável depois de o
  certificado expirar). Anunciar sem implementar seria a mesma mentira do carimbo escaneado.
- **Integração de API com PSC em nuvem** (VIDaaS, Bird ID). `CertificadoIcpBrasil` lê o
  **repositório do Windows**, e é isso que o sistema usa.

  Isto **não** deixa a médica de fora do SafeID: o **SafeID Desktop** existe exatamente
  para intermediar o certificado em nuvem com aplicativos que não são integrados ao SafeID
  — instalado o programa, o certificado aparece no repositório do Windows, e ao assinar
  chega uma notificação no celular para autorizar com o PIN. Ou seja, os dois caminhos da
  clínica funcionam hoje: o certificado instalado na máquina e o SafeID com o Desktop.

  Espere a assinatura demorar o tempo da autorização no celular — é o mesmo comportamento
  do PIN de um token A3, e é do fluxo, não do sistema.

  A integração direta por API (OAuth do PSC, sem programa intermediário) continua não
  implementada; ver a seção de certificados em `docs/apis-integracao-prescricao.md`.
- **Base de medicamentos / autocomplete.** O campo é texto livre por decisão, e é por isso
  que a conferência de alergia compara por palavra inteira.
- **Certificação SBIS/CFM (S-RES).** Não é requisito para prescrever nem para assinar; é
  requisito para o prontuário eletrônico substituir o arquivo de papel (NGS2).

## Carimbo do tempo

Opcional, configurado em Configurações → Operação (uma URL de ACT RFC 3161). Sem ele a
assinatura é PAdES-B: **válida**, com a data declarada pelo relógio de quem assinou — e o
rodapé escreve exatamente isso, em vez de fingir precisão que a via não tem.

## Onde isso está no código

| O quê | Onde |
|---|---|
| As exigências da lei, por tipo de documento | `Domain/ConformidadeDocumentoClinico.cs` |
| Assinar, guardar e conferir o documento | `Application/Servicos/AssinaturaDeDocumentoClinicoService.cs` |
| O certificado é de quem assina (CPF de dentro dele) | `Application/Assinatura/TitularDoCertificado.cs` |
| PKCS#7 no PDF, carimbo do tempo, conferência | `Application/Assinatura/AssinaturaDigitalService.cs` |
| Rodapé, faixa da assinatura e endereço na receita | `Application/Servicos/DocumentosClinicosPdfService.cs` |
| Escolha do certificado (as duas portas) | `Desktop.Shell/Componentes/EscolherCertificadoWindow.xaml` |
| Emissão com assinatura | `Desktop.Shell/Componentes/DocumentoEdicaoViewModel.cs` |
| Assinar depois | Prescrições da Recepção e do Consultório |
| Testes | `tests/Clinica.Tests/AssinaturaDocumentoClinicoTests.cs` |

## Fontes

- [Lei 14.063/2020 — texto na base da ANVISA](https://anvisalegis.datalegis.net/action/ActionDatalegis.php?acao=detalharAto&tipo=LEI&numeroAto=00014063&seqAto=000&valorAno=2020&orgao=NI&nomeTitulo=codigos&desItem=&desItemFim=&cod_modulo=310&cod_menu=9882)
- [Lei 5.991/1973, art. 35](https://www.planalto.gov.br/ccivil_03/leis/l5991.htm) ·
  [alteração da validade nacional (Anfarmag)](https://anfarmag.org.br/conteudos/validade-da-receita-em-todo-o-territorio-nacional-alteracao-da-lei-no-5-991-1973/)
- [CRF-RS — orientação ao farmacêutico sobre prescrição eletrônica](https://crfrs.org.br/noticias/orientacao.ao.farmaceutico.sobre.prescricao.eletronica) ·
  [CRF-SP — dispensação de receitas com assinatura digital](http://www.crfsp.org.br/orienta%C3%A7%C3%A3o-farmac%C3%AAutica/641-fiscalizacao-parceira/farm%C3%A1cia/11248-prescri%C3%A7%C3%A3o-eletr%C3%B4nica-4.html)
- [ITI — Validador de Documentos Digitais em Saúde](https://assinaturadigital.iti.gov.br) ·
  [página do farmacêutico](https://assinaturadigital.iti.gov.br/farmaceutico/) ·
  [ITI — o que o validador garante](https://www.gov.br/iti/pt-br/assuntos/noticias/indice-de-noticias/validador-de-prescricoes-e-atestados-medicos-digitais-garante-seguranca-na-relacao-medico-paciente-e-farmaceuticos) ·
  [CRF-RJ — passo a passo para validar](https://crf-rj.org.br/noticias/4093-passo-a-passo-como-validar-uma-receita-digital-assinada-com-certificado-icp-brasil.html) ·
  [Validar genérico de assinaturas](https://validar.iti.gov.br)
- [ANVISA — SNCR: documentação técnica da API](https://www.gov.br/anvisa/pt-br/assuntos/noticias-anvisa/anvisa-publica-documentacao-tecnica-para-integracao-de-sistemas-de-prescricao-eletronica-ao-sncr) ·
  [prazo prorrogado para 30/09/2026](https://www.gov.br/anvisa/pt-br/assuntos/noticias-anvisa/2026/sncr-anvisa-inicia-etapa-de-integracao-com-sistemas-de-prescricao-eletronica-e-amplia-prazo-para-implementacao)
