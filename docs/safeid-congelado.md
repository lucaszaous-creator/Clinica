# ⛔ CONGELADO: SafeID e assinatura digital

**Decisão da direção, 14/08/2026. Não encoste em nada relativo ao SafeID e à assinatura
digital sem autorização expressa.**

Isto vale para pessoas e para agentes. Se você chegou aqui por uma tarefa que passa por
estes arquivos, **pare e pergunte antes**.

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

- **A assinatura do PACIENTE** (`AssinaturaDoPacienteService`, `AssinaturaPacienteWindow`) —
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
