> **27/10/2026 — o SafeID voltou** para receitas e documentos (sistema e portal), ao lado do A1; a infusão segue só com o A1. Ver [safeid-de-volta-out2026.md](safeid-de-volta-out2026.md).

> Registro histórico. A integração descrita foi retirada em 01/10/2026. Para o fluxo atual, consulte [assinatura A1](assinatura-a1.md) e [relatório de remoção](remocao-provedor-assinatura.md).

# ⛔ CONGELADO: SafeID e assinatura digital

**Decisão da direção, 14/08/2026. Não encoste em nada relativo ao SafeID e à assinatura
digital sem autorização expressa.**

Isto vale para pessoas e para agentes. Se você chegou aqui por uma tarefa que passa por
estes arquivos, **pare e pergunte antes**.

---

## ✅ Autorização expressa de 19/10/2026 — SÓ A CAMADA DE TELA

**A direção (dona da clínica) autorizou em 19/10/2026 converter para Blazor as TELAS desta
área**, e aceitou fazer ela mesma, depois, os testes de assinatura em homologação (cada
tentativa é cobrada). O alcance é estreito e está escrito para ninguém o alargar de raspão:

- ✅ **Pode**: trocar as janelas WPF por formulários Blazor — a escolha do certificado A1
  (`EscolherCertificadoWindow`), a coleta da assinatura do paciente (`ColetaDeTermo`,
  `AssinaturaPacienteWindow`, `EscolherTermoWindow`, `EscolherSessaoDoTermoWindow`), a
  prescrição de infusão (`PrescricaoInternaWindow`), a folha de execução
  (`FolhaExecucaoWindow`, `InfusaoExternaWindow`) e a evolução de enfermagem da folha
  (`EvolucaoEnfermagemWindow` e as views que ela hospedava).
- ✅ **Pode**: mover, SEM MUDAR COMPORTAMENTO, a lógica que morava num ViewModel e é parte do
  protocolo (a abertura e a conferência do A1, a ORDEM conferência → confirmação de alergia
  → certificado → assinatura) para classes fora da tela, onde o `dotnet test` alcança.
- ❌ **Não pode** (continua congelado, mesmo com esta autorização): `Assinatura/`, `SafeID/`,
  `AssinaturaDigitalService`, `CertificadoIcpBrasil`, `TitularDoCertificado`,
  `AssinaturaDeDocumentoClinicoService`, `AssinaturaDePrescricaoService`,
  `PublicacaoDocumentoService`, hash, PDF/PAdES, a revisão incremental, o tratamento do
  certificado, o fluxo de token, nem QUAIS bytes são assinados.

A regra que a conversão segue: **a tela Blazor chama os MESMOS serviços, com os MESMOS
argumentos, na MESMA ordem** que os ViewModels WPF chamavam. Onde a ordem era protocolo, ela
saiu do ViewModel para `src/Clinica.Application/Modelos/AssinaturaNaTela.cs` (movida linha a
linha) e há teste que a fixa (`AssinaturaNaTelaTests`). O roteiro do que a direção precisa
testar em homologação, caminho por caminho, está em
[`docs/safeid-telas-blazor-out2026.md`](safeid-telas-blazor-out2026.md).

## ✅ Autorização expressa de 24/11/2026 (2ª) — PUBLICAR, METADADOS ITI E RECEITUÁRIO DE CONTROLE ESPECIAL

**Contexto:** a receita 2026/0484, assinada com SafeID pelo portal e com assinatura PAdES válida, foi recusada na
farmácia por três motivos:
- tinha antibiótico, que pede o Receituário de Controle Especial;
- o QR no validador do gov.br pedia um código e nada servia, porque o portal assinava SEM publicar;
- o PDF não tinha os metadados de prescrição do ITI.

**Respostas do dono (24/11/2026):**
- sobre corrigir a receita (publicar, QR e metadados do ITI): *"Perfeito, então prossiga com isso"*;
- sobre a assinatura no Controle Especial: *"com a assinatura saindo nesse PDF de receituário também como o safeid
  porém precisará que a assinatura saia neste campo [ASSINATURA MÉDICO(A)] para não sobrescrever tão pouco
  exceder"*. Leitura (Jade): muda SÓ a posição e o tamanho da aparência visível no receituário; hash/PAdES iguais;
  UMA assinatura real de teste (cobrada), feita pela médica com a nossa orientação.

O alcance vale **só** para estes três pontos; o resto continua congelado:

1. **Publicar depois de assinar, em todos os caminhos.**
   - O portal (SafeID/A1, Consultório e gestão) passa a registrar a `PublicacaoDocumentoService`: o token nasce
     ANTES do PDF e o arquivo sobe depois de assinar.
   - A fila `PublicarPendentesAsync` (no portal, a cada 2 min) sobe o que falhou.
   - A saída do portal ganha o host do armazenamento no proxy restrito (`PROXY_HOSTS_EXTRAS`).
   - O assinado SEM token não muda: o QR dele foi selado apontando para o validador, e é preciso assinar de novo.
2. **Metadados de prescrição do ITI:** só com fonte oficial (Guia de Orientações aos Desenvolvedores do VALIDAR).
3. **Receituário de Controle Especial** (modelo ITI v2.0): documento novo, assinável com SafeID/A1. A aparência
   visível da assinatura fica no campo "ASSINATURA MÉDICO(A)"; só a posição e o tamanho mudam, e hash/PAdES
   continuam iguais.
   - **O que mudou no motor (nov/2026), e só isto:** `AreaAssinatura` ganhou dois campos opcionais —
     `OutrasPaginas` (o mesmo bloco visível desenhado também na 2ª via, ANTES de assinar, então coberto pelo hash)
     e `AjustarFonte` (a letra encolhe até 0,55 para as quatro linhas caberem no campo de 160 × 44 pt). Sem eles
     (todos os outros documentos), o desenho é o de sempre, byte a byte. `CarimboDeAssinatura` passou de `private`
     a `internal` só para o teste medir a escala. CMS, ByteRange, carimbo de tempo, certificado e token: intocados.
   - **Metadados (ponto 2) — `MetadadosDePrescricaoIti`:** os OIDs vêm do guia oficial do VALIDAR
     (validar.iti.gov.br/guia-desenvolvedor.html): no cap. V, 2.16.76.1.12.1.1 = "Prescrição de medicamento" e
     2.16.76.1.4.2.2.1/.2/.3 = CFM número/UF/especialidade; no cap. I, item 3, eles "devem ser incluídos como
     metadados". **O guia NÃO diz o lugar no PDF nem o formato do valor.** Decisão de 25/11 (Jade, opção a), na
     leitura literal: uma entrada do dicionário **Info** por OID, com o OID como chave. O valor é o rótulo do guia
     para o tipo e o dado do prescritor (número, UF por sigla, especialidade) para os do CFM. Os do CFM só vão
     quando o conselho é CRM. Tudo isso é gravado ANTES de assinar, fica coberto pelo hash e não muda a validade
     da assinatura. Na primeira assinatura real, conferir se o validador reconhece a prescrição; se não
     reconhecer, a mudança fica só nesta classe.

## ✅ Autorização expressa de 24/11/2026 — ASSINAR NA GESTÃO WEB

**Pergunta feita ao dono:** *"Autoriza levar a assinatura digital (SafeID e A1) para a Gestão do portal? O motor da
assinatura não muda; a Gestão passa a usar os mesmos serviços do Consultório, e é preciso 1 assinatura real de
teste, que é cobrada."*

**Resposta do dono (24/11/2026):** *"Autorizo, na Gestão"*.

O alcance:

- ✅ **Pode**: a gestão web (`/gestao`) assinar receitas e documentos clínicos chamando, pelo navegador, as MESMAS
  rotas da API do portal que o Consultório usa, na MESMA ordem e com a mesma sessão e o mesmo CSRF:
  - `.../atendimentos/0/documento/{id}/safeid` → Safeweb **na mesma aba** → `/safeid/retorno` → de volta à
    `/gestao/?assinatura=` → confirmação explícita "Assinar e arquivar" → `/safeid/{id}/concluir`;
  - `.../atendimentos/0/documento/{id}/a1` → senha → `/a1/{id}/assinar`.

  Peças: `Gestao.Web/Hospedagem/AssinaturaNaGestao.cs`, `Gestao.Web/wwwroot/assinatura-na-gestao.js`, a volta no
  `clinica-site` (`clinico.js`, `safeid.js`) e a aba "Para assinar" de Clínico → Prescrições.
- ❌ **Não pode**: nada muda no motor. `Assinatura/`, `SafeID/`, `SafeIdTabletService`, `A1TabletService`,
  `PublicacaoDocumentoService`, hash, PDF/PAdES, certificado e token continuam congelados. A infusão (prescrição,
  folha de execução) segue só no Consultório, com o A1. A anamnese segue no aplicativo do computador.
- 🧪 **Teste real**: UMA assinatura de teste, feita pela própria médica, guiada, depois do deploy. O CI usa só
  falsos (`AssinaturaNaGestaoTests`).

---

## Por que

Em 14/08/2026 a assinatura em nuvem passou a funcionar depois de **sete rodadas** de
correção, todas em produção, com a clínica testando entre uma e outra. Os defeitos estavam
empilhados uns sobre os outros e cada um só apareceu depois de o anterior sair do caminho:

| | defeito |
|---|---|
| 1 | `raw_signature` fora do base64 padrão |
| 2 | `TrimEnd('0')` comia o byte `0x00` do PKCS#7 |
| 3 | assinava o **hash de nada** — o `RangedStream` chega sem posição |
| 4 | CMS em **BER indefinido** recusado pelo recorte |
| 5 | BER embutido no PDF em vez de DER (PAdES exige DER) |
| 6 | seis colunas de data **com fuso**, que o Npgsql recusa |
| 7 | publicação derrubando a assinatura que já tinha dado certo |

Três coisas tornam esta área diferente de qualquer outra do sistema:

1. **Cada tentativa de assinatura é COBRADA pelo PSC.** Depurar aqui gasta dinheiro da
   clínica, e não há como "testar de novo" de graça.
2. **É a única parte do sistema com valor jurídico afirmado no rodapé.** Um erro aqui não
   produz uma tela feia: produz um documento que parece oficial e não é — a *garantia
   aparente* que o projeto recusa desde a parcela 3.
3. **O teste da casa usa o leitor da casa.** Os 1582 testes rodam contra certificado
   autoassinado em memória e banco SQLite. Eles não enxergam e-CPF real, cadeia ICP-Brasil,
   `timestamp with time zone` nem o formato que a Safeweb realmente emite. **Verde aqui não
   quer dizer funciona.**

---

## O que está congelado

### Núcleo — não se toca

```
src/Clinica.Application/Assinatura/SafeID/          (a pasta inteira)
src/Clinica.Application/Assinatura/AssinaturaDigitalService.cs
src/Clinica.Application/Assinatura/CertificadoIcpBrasil.cs
src/Clinica.Application/Assinatura/TitularDoCertificado.cs
src/Clinica.Application/Servicos/AssinaturaDeDocumentoClinicoService.cs
src/Clinica.Application/Servicos/AssinaturaDePrescricaoService.cs
src/Clinica.Desktop.Shell/Componentes/EscolherCertificado*
tests/Clinica.Tests/SafeIDTests.cs
tests/Clinica.Tests/AssinaturaDigitalTests.cs
tests/Clinica.Tests/AssinaturaEmNuvemFimAFimTests.cs
tests/Clinica.Tests/AssinaturaDocumentoClinicoTests.cs
```

### Fronteira — também não se toca, e o motivo é o mesmo

A **publicação** dispara *de dentro* da assinatura, e foi ela que derrubou o documento
assinado na rodada 7. Mexer nela é mexer no caminho da assinatura.

```
src/Clinica.Application/Servicos/PublicacaoDocumentoService.cs
src/Clinica.Domain/PublicacaoDocumento.cs
src/Clinica.Infrastructure/ArmazenamentoS3.cs
```

### Fora do congelamento

- **A assinatura do PACIENTE** (`AssinaturaDoPacienteService`; a tela é o `AssinaturaDoPacienteForm` Blazor desde out/2026, antes `AssinaturaPacienteWindow`) —
  é assinatura eletrônica **simples**, feita a dedo na tela, e não usa certificado nem PSC.
  Outro assunto, outro risco.
- As **telas que CHAMAM** a assinatura (`DocumentoEdicaoViewModel`, `PrescricoesClinicas
  ViewModel`, a Sala de Infusão…) continuam livres para mudar de leiaute — **desde que não
  mudem o que é passado para o serviço nem a ordem das chamadas.**

---

## O que "não encostar" quer dizer, na prática

- ❌ Não refatore, não renomeie, não "limpe" e não reorganize estes arquivos.
- ❌ Não mexa na ordem das operações da assinatura. Ela **não é livre**: o token nasce antes
  do PDF, o QR entra antes da assinatura, a assinatura sela os bytes, a publicação vem
  depois. Cada inversão já custou uma rodada.
- ❌ Não mexa em `TamanhoReservado`, no recorte ASN.1, na normalização para DER nem no
  cálculo do hash.
- ❌ Não apague nem "simplifique" as recusas (hash vazio, PKCS#7 inválido, BER não
  convertível). Cada uma existe porque a alternativa era um documento inválido saindo calado.
- ❌ Não troque a mensagem de erro por uma mais bonita. **Elas carregam a evidência de
  propósito** — foi a mensagem com os bytes em hexa que resolveu a rodada 6 no primeiro
  relato, depois de três rodadas de adivinhação.
- ✅ Pode LER à vontade, e deve: os comentários explicam por que cada linha está ali.

---

## Se algo parecer errado

1. **Não corrija por dedução.** Foi assim que as rodadas 1, 2 e 3 chegaram à clínica: eu li
   o código, deduzi a causa a partir da mensagem e mandei a correção. Cada uma era um
   defeito real, e nenhuma era a causa.
2. **Peça o log.** `Diagnostico.Registrar` grava a exceção inteira, com a inner, em
   `<pasta da instalação>\logs` (Configurações → "Abrir pasta de logs"). Ele já tinha a
   resposta da rodada 7 antes de qualquer hipótese.
3. **Peça o commit do build.** Metade da confusão de 14/08 foi não saber se o exe da clínica
   tinha as correções anteriores — e uma vez a resposta estava na própria frase de erro, que
   já não existia mais no código.
4. **Reproduza antes de corrigir.** O experimento que achou o defeito da rodada 3 tem trinta
   linhas e devia ter sido a primeira coisa.
5. **Teste em HOMOLOGAÇÃO.** Configurações → SafeID tem o ambiente da Safeweb, e o plano
   gratuito tem 50 assinaturas. É a mesma pergunta sem custo.

---

## Prova de campo — o que JÁ rodou na clínica

> ✅ **A assinatura de prescrição com e-CPF real, pelo SafeID, foi testada e funciona**
> (ago/2026). Deixou de ser a maior incógnita deste documento.

Isso exercitou, de uma vez, o que os testes não alcançavam:

- **e-CPF real**, em vez do certificado autoassinado em memória;
- **`exigirCadeiaConfiavel: true`** com cadeia **ICP-Brasil** de verdade;
- **a publicação no S3** de um documento real — ela estourava antes de rodar uma vez;
- e o circuito inteiro que as rodadas 3 a 8 da parcela 67 corrigiram às cegas: o
  `raw_signature`, o recorte do PKCS#7 em BER, o hash do conteúdo coberto e a normalização
  para DER.

⚠️ **O que isso NÃO significa.** Continua valendo o resto deste documento: cada tentativa é
COBRADA, o valor jurídico é afirmado no rodapé, e "verde nos testes" segue não querendo
dizer "funciona" — foi preciso chegar à clínica para provar. Mudança aqui continua exigindo
autorização expressa.

## O que continua sem prova de campo

Registrado para não ser confundido com "funciona":

- **Carimbo do tempo (ACT RFC 3161)** — a ACT configurada **não é aplicada** no caminho de
  nuvem: o `CarimbadoraDeTempo` só chega ao assinador local. O rodapé escreve "data
  declarada", que é honesto, mas a configuração é ignorada em silêncio. **Em aberto,
  aguardando decisão da direção.**
- **LTV / PAdES-LT** — sem ele, o PDF assinado deixa de se validar sozinho quando o
  certificado expira. Nunca foi implementado, e o rodapé não o promete.

---

## Referências

- `docs/safeid-assinatura-em-nuvem.md` — a integração, endpoint a endpoint
- `docs/prescricao-eletronica-conformidade.md` — o que a lei exige de cada documento
- `CLAUDE.md`, parcela 67 — as lições rodada a rodada, com o porquê de cada decisão
