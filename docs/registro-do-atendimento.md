# O REGISTRO do atendimento — a consulta de enfermagem e a sessão médica

> **A frase que decide: a parcela 72 respondeu QUEM VÊ. Esta responde O QUE SE ESCREVE.**

Parcela 73. Documento irmão de
[`docs/atendimento-medico-e-enfermagem.md`](atendimento-medico-e-enfermagem.md) — aquele é o
mapa de acesso (X · Y · XY), este é o conteúdo do ato. Lido junto de
[`docs/conformidade-lgpd.md`](conformidade-lgpd.md).

---

## 1. A reprovação, e o que ela acertou

A cliente escreveu, ao abrir a entrega da parcela 72:

> *"Está muito CRU! Não é possível que em um atendimento de enfermagem e médico você me faça
> isso aí!"*

Foi a oitava reprovação do cliente no projeto, e a mais precisa de todas. A parcela 72
entregou **portas, linha do tempo e permissões** — e não encostou no **ato de atender**. O
que existia, medido antes de responder:

| O registro | O que ele tinha |
|---|---|
| Sessão médica (`Evolucao`) | queixa, EVA antes/depois, conduta, texto livre, orientações |
| Passagem de enfermagem (`EvolucaoEnfermagem`) | um texto livre, sinais vitais, hora, autor |

Ou seja: **um campo de texto**. O médico não tinha onde escrever história da doença atual,
exame físico nem hipótese diagnóstica — os três eixos do raciocínio clínico, sem os quais o
prontuário registra o que foi feito e não registra **por quê**. E a enfermagem não tinha
onde registrar o Processo de Enfermagem.

⚠️ **A segunda metade não é preferência de leiaute: é ilegalidade.** A
**COFEN 358/2009** torna o Processo de Enfermagem **obrigatório** e **registrado
formalmente** em cinco etapas, e a **Lei 7.498/1986, art. 11, I, "i"** faz da consulta de
enfermagem ato **privativo do Enfermeiro**. Um sistema que oferece à enfermagem uma caixa de
texto não está oferecendo pouco — está impedindo que ela cumpra a Resolução do próprio
conselho.

---

## 2. As cinco etapas, e por que elas moram numa entidade só

A COFEN 358/2009 enumera:

| Etapa | Onde mora |
|---|---|
| 1. Histórico de enfermagem (coleta de dados) | `EvolucaoEnfermagem.Historico` + `.ExameFisico` |
| 2. Diagnóstico de enfermagem | `List<DiagnosticoEnfermagem>` |
| 3. Planejamento (resultado esperado) | `DiagnosticoEnfermagem.ResultadoEsperado` |
| 4. Implementação (prescrição de enfermagem) | `List<CuidadoEnfermagem>` |
| 5. Avaliação | `EvolucaoEnfermagem.Avaliacao` |

⚠️ **A etapa 3 fica COLADA no diagnóstico dela**, e não numa lista à parte. É contra o
resultado esperado que a etapa 5 avalia; separá-los obrigaria quem lê a casar linha com
linha, e é assim que a avaliação vira impressão em vez de conclusão.

**Entidade NOVA para diagnóstico e cuidado, e não campos de texto na evolução.** São listas
ordenáveis de itens com estrutura própria (código, três partes da redação, frequência,
vínculo entre cuidado e diagnóstico) — enfiá-las em duas colunas de texto daria o mesmo
"muito cru" com mais letras. E são **filhas** da evolução, com `Cascade`: é a única cascata
clínica aceita no projeto, porque um diagnóstico não existe fora da consulta que o produziu
— e a consulta, essa sim, não se apaga.

### 2.1 ANOTAÇÃO × CONSULTA — a mesma entidade, dois atos

A clínica registra muito mais **anotação** (a passagem pontual: "14h20 — paciente refere
melhora, PA 120/80") do que **consulta**. A anotação é da técnica; a consulta é privativa do
enfermeiro.

Quem distingue os dois é o **CONTEÚDO**, não um campo de tipo:

```csharp
public bool EhConsulta =>
    Diagnosticos.Count > 0 || Cuidados.Count > 0
    || !string.IsNullOrWhiteSpace(Historico) || !string.IsNullOrWhiteSpace(ExameFisico);
```

⚠️ **Um campo de tipo teria de ser preenchido, e seria preenchido errado** — a técnica
marcaria "consulta" por engano e a folha sairia cobrando cinco etapas de uma observação de
sinais vitais. Derivado do conteúdo, ele nunca mente: **quem escreveu um diagnóstico fez uma
consulta; quem escreveu uma linha fez uma anotação.**

### 2.2 A consulta incompleta AVISA, não impede

`EtapasEmFalta` devolve o que falta, e a tela e o papel escrevem. Não recusa, e a decisão é
deliberada:

- a consulta é escrita ao longo do turno — a avaliação (etapa 5) só existe **depois** de o
  cuidado ter sido prestado, e recusar salvar antes dela obrigaria a enfermeira a manter a
  janela aberta a tarde inteira ou a digitar tudo de novo no fim;
- **registro clínico que não se consegue salvar é registro que não acontece**, e a folha em
  branco é pior para a fiscalização do que a folha pela metade que se anuncia pela metade.

⚠️ A recusa aqui seria a **garantia aparente pelo avesso**: imprimir uma consulta incompleta
**sem dizer que está incompleta** é que seria enganar quem fiscaliza. Por isso o aviso sai na
tela **e** na via em papel.

---

## 3. O catálogo de diagnósticos e cuidados

`CatalogoEnfermagem`: **13 diagnósticos** e **19 cuidados**, em CÓDIGO — o mesmo desenho das
escalas clínicas e do motor de regras de convênio.

⚠️ **NANDA-I é licenciada e NÃO foi importada.** O catálogo é **a lista desta clínica**,
escrita com a redação em três partes que a NANDA popularizou (que é método, não texto
protegido), e cobre o que a casa faz: dor aguda e crônica, risco de infecção e de reação,
integridade da pele, perfusão, náusea, ansiedade, risco de queda, mobilidade, conhecimento
deficiente, adesão e desequilíbrio de volume. Importar a taxonomia sem licença seria pôr a
clínica num risco que o produto não pode criar por conta própria.

Três regras:

1. **É ATALHO, não lista fechada.** `Codigo` é anulável: escrever um diagnóstico à mão é
   legítimo e comum. Uma lista fechada faria a enfermeira parar de diagnosticar o que não
   está nela — e diagnóstico não escrito é cuidado não prestado.
2. **Aplicar COPIA, nunca aponta.** Mesma regra do protocolo do mapa corporal e do preço por
   convênio, e aqui ela não é desenho: é a **Lei 13.787/2018**. Corrigir a redação de um
   diagnóstico no catálogo hoje não pode reescrever o que a enfermeira registrou no mês
   passado.
3. **A busca é insensível a acento e caixa** (`Normalizar`): quem procura "infeccao" com o
   paciente na frente precisa achar "Risco de infecção".

E cada diagnóstico do catálogo já traz os **cuidados que costumam atendê-lo**
(`CuidadosDe`), porque a pergunta seguinte a "qual é o problema" é sempre "o que eu faço a
respeito" — e obrigar a procurar de novo, numa segunda lista, é o que faz a etapa 4 sair
vazia.

---

## 4. A sessão médica — os três eixos que faltavam

`Evolucao` ganhou `HistoriaDoencaAtual`, `ExameFisico`, `HipoteseDiagnostica` e `CidSessao`.
Todos **anuláveis**, e a tela os põe num `Expander` fechado por padrão
(`DetalharAtendimento`).

⚠️ **Fechado por padrão é decisão, não economia de pixel.** A sessão de acupuntura de
seguimento — a maioria absoluta do movimento da clínica — se registra em queixa, EVA e
conduta, como sempre foi. Abrir quatro campos vazios em toda sessão faria o profissional
aprender a rolar por cima deles, e no dia da consulta que PRECISA de anamnese eles já teriam
virado paisagem. **Quem abre o detalhe é quem tem o que escrever nele.**

O `CidSessao` reusa o `BuscaCidWindow` — que existia com **duas** montagens à mão
(`DocumentoEdicaoViewModel` e `ProblemaEdicaoViewModel`). Virou um ponto único
(`BuscaCidWindow.Perguntar`), pela razão de sempre: três montagens divergiriam na primeira
correção, e a terceira era esta.

⚠️ **A hipótese NÃO é a lista de problemas.** `ProblemaPaciente` é o que o paciente TEM
(persistente, com situação e alerta de alergia); `HipoteseDiagnostica` é o que se pensou
NAQUELA sessão, e as duas convivem sem se substituírem — a hipótese de terça pode estar
errada, e a lista de problemas não deve carregá-la.

---

## 5. O circuito — para onde o registro novo vai

A regra 8 do compromisso de conformidade: entidade clínica nova entra na **exportação** e na
**guarda**. Aqui não nasceu natureza nova (diagnóstico e cuidado são filhos da evolução de
enfermagem, que `CatalogoRegistroClinico` já conhece), mas os **campos** precisavam viajar:

| Destino | O que chega |
|---|---|
| Linha do tempo clínica (parcela 72) | `hipótese: …` na sessão; `CONSULTA DE ENFERMAGEM`, os títulos dos diagnósticos e a contagem de cuidados na passagem |
| Exportação do fornecedor (`ExportacaoProntuarioService`) | 4 colunas novas em `sessoes`, 4 em `enfermagem`, e **dois CSV novos** — `prontuario-enfermagem-diagnosticos.csv` e `-cuidados.csv` |
| Direito do titular, art. 18 II (`TitularDadosService`) | os três eixos médicos e as cinco etapas por extenso |
| **A via em PAPEL** (`PrescricaoInternaPdfService`) | o bloco das cinco etapas, com o que falta escrito |
| Relatório de evolução (`DocumentoClinicoService`) | HDA, exame físico e a hipótese **em texto** |

⚠️ **O papel é o item que não podia faltar**, e é o mais fácil de esquecer: a fiscalização do
COREN se faz no PRONTUÁRIO, que numa clínica é a via impressa e arquivada. **Consulta de
enfermagem que só existe na tela é consulta que a fiscalização não enxerga** — o defeito
recorrente do projeto na variante que custa multa.

⚠️ **E o CID da hipótese NÃO sai no relatório de evolução.** É a economia do CID da parcela
3, e vale aqui com mais razão: o relatório circula fora da clínica, o código é o que se lê
num campo de formulário sem ninguém ler a frase ao lado, e este documento não passa pela
autorização expressa que a receita e o atestado pedem. Quem precisa do código pede o
atestado.

---

## 6. O que ficou de fora, e por quê

- **Taxonomia NANDA-I / NIC / NOC** — licenciadas. Ver §3.
- **Diagnóstico de enfermagem com escala de prioridade** — a clínica ordena a lista à mão
  (`Ordem`), e prioridade numérica sem definição publicada seria um número que cada
  enfermeira interpretaria de um jeito.
- **Vínculo obrigatório cuidado → diagnóstico** — opcional de propósito: exigi-lo faria
  parar de registrar a hidratação e a orientação de alta, que não se encaixam em diagnóstico
  escrito nenhum.
- **Assinatura eletrônica da consulta de enfermagem** — a área da assinatura digital está
  **congelada** (ver [`docs/safeid-congelado.md`](safeid-congelado.md)); a autoria continua
  sendo o login com o COREN ao lado, como toda evolução.

---

## 7. Como conferir que continua valendo

```bash
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~ProcessoDeEnfermagem"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~ConjuntoClinico"

# As três redes locais, antes de todo push
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
python3 tools/compilar-sombra.py
python3 tools/verificar-suite.py
```

⚠️ **Antes de publicar a versão**: a exigência do COREN (parcela 72) **recusa** login de
enfermagem sem `Profissional` vinculado com `RegistroConselho`. Confira o cadastro da equipe
de enfermagem antes de subir, ou a sala descobre no primeiro registro do dia.
