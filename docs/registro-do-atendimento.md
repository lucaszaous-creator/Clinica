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

---

# Parte II — o ATENDIMENTO como sessão (parcela 74)

> **A parcela 73 respondeu O QUE SE ESCREVE. Esta responde COMO SE ATENDE.**

A cliente mandou o print do prontuário do iClinic, com a frase: *"veja como é quando o
médico/enfermeiro aperta para atender X paciente. Estamos MUITO atrás, muitos anos luz
atrás mesmo."*

## 8. As duas telas, medidas lado a lado

| iClinic | Nosso, antes desta parcela |
|---|---|
| **Finalizar atendimento + duração ao vivo** | **não existia** — formulário com "Salvar sessão" |
| Histórico de consultas | ✅ aba Prontuário |
| Tabela de acompanhamento | ✅ três abas (dor, medidas, avaliações) |
| Atendimento | ✅ |
| **Prescrições** | ❌ **fora do paciente** — item de menu separado |
| **Documentos e atestados** | ❌ **fora do paciente** — botão que abre janela |
| **Imagens e anexos** | ❌ **fora do paciente** — janela por sessão |
| Foto, idade, convênio, 1ª consulta | 🟡 iniciais + nome + uma linha |
| Últimos diagnósticos à vista | ❌ |
| Teleconsultas | ❌ e **fica** — ver §12 |

## 9. O atendimento não era um ESTADO

Num prontuário eletrônico o profissional **entra** no atendimento, o relógio corre e ele
**finaliza**. É essa diferença que faz a tela dizer, o tempo todo, que há uma pessoa na
sala e há quanto tempo ela está lá.

⚠️ **O carimbo do início existe desde a parcela 38** — `Agendamento.InicioAtendimentoEm`,
criado para o kanban do balcão — **e nenhuma tela do Consultório o lia.** É o defeito
recorrente do projeto na variante mais discreta de todas: nada falha, o dado está gravado,
e o efeito é apenas que ninguém sabe de nada.

O que entrou: `Agendamento.FimAtendimentoEm` (migration **aditiva**), a barra com o relógio
ao vivo e os botões **Iniciar** / **Finalizar atendimento**.

### 9.1 Finalizar NÃO era concluir — e passou a ser (parcela 95)

**O desenho original (parcela 61).** Concluir são **quatro fatos do mesmo ato** — a guia
nasce, o pacote debita, o insumo sai do estoque, o dinheiro entra no caixa — e **três deles
são do balcão**. O que o profissional afirmava ao finalizar era só *"terminei com esta
pessoa"*, e o `Status` seguia `Agendado` até o **Concluir** da Fila.

> O aviso que esta seção trazia, e que continua valendo palavra por palavra:
>
> *"Se alguém um dia fizer o encerramento marcar `Realizado` 'para simplificar', os três
> fatos do balcão deixam de acontecer **em silêncio**: o pacote não debita, o insumo não sai
> e o caixa fecha sem a sessão. Nada falha — o dia só não bate."*

**O que mudou, e por que o aviso não foi ignorado.** A direção pediu o fluxo de um clique
(*"ele clica em atender e faz o atendimento"*), e a medição mostrou que o argumento da
parcela 61 se sustenta para pacote, insumo e caixa e **não** para a GUIA, que é o fato do
atendimento. No caso mais comum — convênio, sem pacote, sem insumo — `TemDecisao` é falso e
o Concluir do balcão não abria janela nenhuma: era cerimônia.

O aviso acima foi endereçado, não contornado. Os três fatos do balcão continuam existindo e
ganharam **porta e pendência**:

| | Onde | Quando aparece |
|---|---|---|
| **Pacote** | botão de passo na raia FINALIZADO ("Debitar pacote") | sessão concluída, paciente COM pacote ativo, ainda não debitado |
| **Insumo e caixa** | menu "⋯" → "Fechar sessão (pacote, insumo, caixa)…" | toda sessão concluída |

⚠️ **A pendência é o PACOTE, e a distinção é o que a torna utilizável.** Fosse "ainda não
houve fechamento", o paciente de convênio sem pacote — a maioria do dia — ficaria com o
botão aceso para sempre, porque nele não há nada a fechar. O pacote é o único dos três que
o quadro sabe afirmar em lote (`AtendimentosComFechamentoAsync` + o selo "Pacote 9/10" que
a fila já carrega) e o que custa dinheiro quando escapa: sessão comprada atendida de graça.

⚠️ **O SERVIÇO continua com a divisão de sempre**: `EncerrarAtendimentoAsync` carimba
`FimAtendimentoEm` e não toca no `Status` — `Encerrar_NAO_conclui_o_atendimento` segue
verde, e é ele que garante que ninguém "simplifique" a conclusão para dentro do carimbo.
Quem encadeia os três passos é a TELA (§9.2), na ordem que decide o que sobra quando cada
um falha.

### 9.2 A ordem entre gravar, carimbar e concluir

Grava a sessão **primeiro**. É a hierarquia da parcela 65 aplicada aqui:

- gravação falhou → **o carimbo não acontece**. Mandar o recado de que o médico terminou
  enquanto a evolução não existe em lugar nenhum é falha exibida como sucesso;
- gravação passou e o carimbo falhou → vira **aviso**, e nunca desfaz o prontuário;
- carimbo passou e a **conclusão** falhou (parcela 95) → também vira aviso: o prontuário
  está escrito, o balcão sabe que a sala vagou, e a guia continua alcançável pelo Concluir
  da Fila. Cada um dos três desfechos tem frase própria — a exceção pode vir da permissão,
  do carimbo ou da conclusão, e a diferença entre elas é o que a pessoa faz em seguida.

Foi isso que fez `SalvarAsync` virar `TentarSalvarAsync` devolvendo `bool`.

### 9.3 O recado chega ao balcão

O cartão da fila ganha o selo **"Encerrado às 14h32"** e sobe para a frente da raia. É o par
do `ChamadoEm`, que atravessa no sentido contrário desde a parcela 38.

Até aqui a recepcionista descobria que o médico tinha terminado **quando o paciente aparecia
na frente dela**, e o cartão ficava em "Em atendimento" meia hora depois de a sala estar
vazia — o quadro do dia mentindo sobre quem está ocupado.

⚠️ **É SELO e não raia nova.** Uma coluna permanente para um estado que dura minutos é a
faixa vazia comendo a tela que o README condena desde a parcela 38. O que a recepcionista
precisa saber não é que existe uma coluna nova; é **qual cartão está pronto para fechar**.

### 9.4 As três regras menores

- **Encerrar sem ter começado é recusado** — fim sem começo produziria duração negativa e um
  cartão que sai da sala sem nunca ter entrado nela.
- **Encerrar de novo não reescreve a hora** (`??=`) — a razão do "chamar de novo": quem clica
  duas vezes precisa continuar vendo a hora em que terminou, e o segundo clique esconderia
  justamente o atendimento demorado. E movimento idempotente **não grava linha de trilha**.
- **Encerrar com a sessão em branco PERGUNTA, não impede.** O profissional pode escrever
  depois — registro que não se consegue salvar é registro que não acontece —, e é
  exatamente a dívida que este app existe para cobrar. A tela nomeia a consequência.

## 10. Três seções estavam FORA do paciente

Prescrever exigia **sair do paciente**, ir a um item de menu, escolher a pessoa de novo e
voltar. É a porta no lugar errado — o defeito que o projeto já corrigiu doze vezes **entre**
módulos — cometido agora dentro de um app só.

As sete seções, num **rail vertical**:

| # | Seção | Responde |
|---|---|---|
| 0 | Atendimento | o que se escreve agora |
| 1 | Histórico de sessões | o que já foi feito |
| 2 | Prescrições e documentos | o que sai no papel |
| 3 | Exames e anexos | o que chegou de volta |
| 4 | Evolução da dor | a curva |
| 5 | Medidas | os números seriados |
| 6 | Avaliações | as escalas |

⚠️ **Vertical, e não abas**: o `TabPanel` do WPF **espreme** as abas quando julga que a
régua não cabe — é o defeito da parcela 50, "Convê", "Prontu", "Documer" — e sete rótulos
quebrariam a régua em duas linhas mesmo com `WrapPanel`.

⚠️ **`AbaAtual` continua sendo o contrato.** As chaves de navegação de outros módulos caem
cada uma na sua seção (`ModuloClinico.AbaDe`), e trocar leiaute não pode quebrar navegação —
é literalmente a regressão da parcela 37, 4ª rodada.

⚠️ **A tela que vira seção perde o cabeçalho dela** (`MostrarCabecalho = false`). Não é
economia de pixel: o nome já está no crachá, e o **seletor de busca dela trocaria o
`PacienteEmFoco` por baixo das outras seis seções**, que continuariam mostrando o paciente
anterior. Duas listas de paciente na mesma tela é o mestre-detalhe que este desenho existe
para acabar.

### 10.1 "Exames e anexos" muda o EIXO, e é a lição que generaliza

Os anexos só se alcançavam **sessão a sessão**, dentro de uma janela aberta de uma linha do
prontuário. Isso responde *"o que tem nesta consulta"*. A pergunta de quem atende é outra, e
é a mesma que a parcela 37 já tinha nomeado ao trazer os anexos para o Consultório — **"eu
pedi a ressonância; ela chegou?"** —, e ela não se responde abrindo quarenta sessões uma por
uma.

> **Dado com leitor pode estar com a CHAVE errada.** É a variante de EIXO do defeito
> recorrente, e ela não aparece em teste nenhum: tudo funciona, só que ninguém consegue
> perguntar o que precisa.

A seção **não anexa**: anexar é ato da sessão, porque o arquivo pertence à consulta em que
ele foi discutido, e é esse vínculo que põe o laudo ao lado da conduta que ele motivou.

## 11. O crachá clínico

`CabecalhoClinicoPaciente` responde as quatro perguntas que se fazem antes de abrir a boca:
**idade** (a conduta de um paciente de 78 anos não é a de um de 30), **convênio** (decide o
que pode ser pedido), **desde quando** e **o que não se pode esquecer**.

⚠️ **A alergia sai do balde de "alertas" e vira atributo da pessoa.** Alerta é faixa que se
lê uma vez e se ignora nas quarenta sessões seguintes — é a razão pela qual este projeto
recusa alerta que dispara para todo mundo desde a parcela 26. No crachá ela fica ao lado do
nome enquanto o prontuário estiver aberto, e **entra mesmo dada por RESOLVIDA** (a regra da
parcela 37: "resolvida" numa alergia é quase sempre "não reagiu da última vez"). Só o
descarte, que exige motivo escrito, a cala.

Outras três decisões:

- **Os últimos diagnósticos são o primeiro leitor da `HipoteseDiagnostica`** que a parcela
  73 criou. Sem eles, ela seria mais um campo gravado sem leitor — o defeito recorrente
  cometido na parcela seguinte à que criou o dado. Saem **distintos e no máximo três**:
  repetir "lombalgia" nas oito últimas sessões gastaria a linha inteira dizendo uma coisa só.
- **A linha de identificação é montada no MODELO**, não no XAML, porque precisa PULAR o que
  não existe: sem data de nascimento não pode sair "· anos ·" com um vão no meio, e cadastro
  novo não tem "desde". Frase feita de bindings concatenados não sabe pular.
- **O total conta só o que ACONTECEU** (`RealizadoEm`): desde a parcela 70 a guia nasce no
  agendamento, então contar linhas de `Atendimento` somaria as sessões da semana que vem — o
  crachá diria "24 sessões" a quem teve 18.

## 12. O que NÃO foi feito, e por quê

- **Teleconsultas** — a clínica é presencial. Criar uma aba vazia com nome bonito é
  exatamente o "amador" que a reprovação apontava, e prometer o que o código não faz é a
  regra mais antiga do projeto.
- **Tags no paciente** — o iClinic tem; nós não. Antes de construir, falta saber o que a
  clínica marcaria com elas: tag sem uso combinado vira campo que ninguém preenche.
- **Sexta raia no kanban** para o atendimento encerrado — ver §9.3.

## 13. Dois defeitos meus, pegos na revisão do próprio diff

1. **`BooleanToVisibilityConverter` sobre um `BitmapImage` é `Collapsed` para sempre** — a
   lição da parcela 61 na terceira variante. A foto do paciente **nunca apareceria**, com
   XAML bem-formado, binding válido, nada lançando e as três redes verdes. Entrou
   `ObjetoParaVisibilidade`, que **não substitui** o `TextoParaVisibilidade`: string vazia
   não é nula. A pergunta que decide é *"o que significa 'não tem'?"* — para texto é o
   branco, para objeto é o nulo.
2. **`--` é ilegal dentro de comentário XML.** A linha de sublinhado do estilo de comentário
   deste projeto quebra o XAML inteiro. O `verificar-suite` pega na hora.

## 14. Como conferir que continua valendo

```bash
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~FimDoAtendimento"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~CabecalhoClinico"
```

---

# Parte III — a ANAMNESE, e o plano (parcela 75)

> A parcela 73 respondeu **o que se escreve**. A 74, **como se atende**.
> Esta responde **quem é esta pessoa**, e **o que vem pela frente**.

A cliente disse que o módulo clínico continuava cru para atendimento comparado ao iClinic —
*"eles dão mais opções no atendimento de campos e outros"*.

## 15. O buraco medido não era "mais campos na sessão"

Era que **a anamnese do PACIENTE não tinha onde morar**.

| | responde | onde estava |
|---|---|---|
| História da doença atual (parcela 73) | o EPISÓDIO — muda a cada queixa nova | na sessão ✓ |
| Antecedentes, família, hábitos | a PESSOA — vale para o tratamento inteiro | **em lugar nenhum** |

⚠️ Repetir isso em toda sessão foi medido e **recusado**: além de o profissional escrever
"idem" — pior que o campo vazio, porque parece registro —, a pergunta que ele faz na consulta
12 é *"o que eu já sei sobre este paciente?"*, e a resposta não pode depender de abrir a
sessão 1 e ler.

### 15.1 Por que não é o `ProblemaPaciente`

Aquele é uma **LISTA** e está certo para o que é item: "apendicectomia 2015", "alergia a
dipirona", "losartana 50mg". Esta é **NARRATIVA**, e há coisas que não viram item sem perder o
sentido — a história familiar, o padrão de sono, o contexto social. As duas convivem: **a
lista alerta, o texto explica.**

⚠️ **ALERGIA continua SÓ na lista de problemas**, e isso é decisão. Ela é o único dado clínico
que acende alerta em quatro telas e **RECUSA a assinatura de uma prescrição** (parcela 40). Um
campo de texto "alergias" aqui seria uma segunda verdade sobre a mesma coisa — e a que ninguém
lembraria de atualizar é justamente a que o alerta lê.

### 15.2 O que ela herda do prontuário

- **Não se apaga** — guarda de 20 anos (Lei 13.787/2018).
- **Alterar guarda o que ela dizia antes** (`VersaoAnamnese`): corrigir *"nega tabagismo"*
  para *"tabagista"* não pode apagar a informação de que a pessoa **havia negado** — que é
  exatamente o que uma perícia procura.
- **Anamnese em BRANCO é recusada.** Sem isso, um clique no Salvar criaria a linha, carimbaria
  "revisada hoje" e faria a ficha **afirmar** que ela foi colhida.

### 15.3 O que a TELA decide, e o serviço não

- **Abre em modo de LEITURA.** É escrita uma vez e lida dezenas; seis caixas editáveis o tempo
  todo convidam à edição acidental de um registro que **versiona a cada gravação**.
- **O botão diz o ATO** ("Colher" × "Revisar"), porque a trilha os separa.
- **O motivo da revisão é OPCIONAL** — exigi-lo produziria trinta "atualização" por semana.
- **A coluna da direita mostra o que ela já disse.** Versionar sem mostrar seria o defeito
  recorrente com uma **perícia** como leitor faltando.
- **A idade dela aparece**: anamnese de três anos não está errada, está VELHA — e as duas
  coisas se tratam diferente.

## 16. O plano terapêutico

Três coisas que pareciam uma:

| | |
|---|---|
| **Conduta** | o que foi FEITO hoje — "6 pontos, eletro 2Hz, 20 min" |
| **Orientações** | o que o PACIENTE faz em casa — "compressa morna, evitar carga" |
| **Plano** | o que a CLÍNICA vai fazer — "10 sessões, 2x/semana, reavaliar a EVA em 4 semanas" |

Misturado à conduta, some; misturado à orientação, vira recado ao paciente. E é **a frase que
o convênio procura no relatório de evolução** — por isso ele entrou no relatório no mesmo
commit: sem isso nasceria gravado e o único papel que sai da clínica não o levaria.

Campo de **uma linha** de propósito: caixa grande convidaria a repetir a conduta, e campo
preenchido com o conteúdo do vizinho faz o relatório dizer a mesma coisa três vezes.

## 17. A checagem 38 — o rail e o índice de navegação

Pôr a Anamnese na posição 1 **empurrou os quatro índices de baixo** em `ModuloClinico.AbaDe`,
que é o contrato pelo qual a fila do dia e o painel da direção navegam.

⚠️ Nada disso quebra build: índice desatualizado abre a **seção errada**, e índice fora da
faixa o WPF ignora **em silêncio**. É a regressão da parcela 37 (4ª rodada) um nível abaixo —
ali a chave não achava destino; aqui ela acha o destino errado.

A checagem cobra rail e `TabControl` com o mesmo número de itens, e todo índice dentro da
faixa. Autotestada com casos sintéticos e **provada contra os dois defeitos reais** antes de
entrar.

## 18. O que a disciplina pegou, na própria escrita

Esta foi a primeira parcela sob a regra de **auditoria de linha** (`CLAUDE.md`, no topo). Ela
rendeu na primeira hora:

1. `ConjuntoClinicoTests` **reprovou cinco testes** no instante em que a natureza nova nasceu
   sem leitor — os quatro leitores foram cobertos no mesmo commit;
2. `IsReadOnly="{Binding Editando, Converter=BooleanToVisibility}"` — um `Visibility` numa
   propriedade `bool`, que o WPF trata como **falso sempre**: os campos ficariam editáveis o
   tempo todo, **sem erro nenhum**;
3. um conversor `InverterBooleano` que não existe;
4. `TextBlock` de dado do banco sem trimming (pego pelo `verificar-suite`);
5. `_anamneseId` gravado e nunca lido — campo morto, a regra 5 da lista cometida na parcela
   que a escreveu.

E **duas suspeitas minhas refutadas conferindo**, não deduzindo: a seção registra acesso sim
(pela janela de silêncio por origem do workspace), e `EstadoDaTela.Ativo` definido por `Style`
é seguro — a lição da parcela 58 é sobre a `Visibility`, que o controle atribui localmente.

## 19. O que NÃO foi feito, e por quê

- **Sinais vitais inline na sessão.** A coleta já tem porta na seção Medidas, e painel aberto
  para ato pontual é o que o `README` condena. O que falta ali é **VER** os últimos sinais
  enquanto se escreve — que é leitura, não campo.
- **Varrer texto livre na anonimização.** A anamnese pode conter o nome de familiares, e o
  art. 18 VI não o limpa — mas o texto das evoluções também não é limpo, e a decisão
  documentada é remover os campos de identificação e deixar o histórico sem dono
  identificável. Mudar isso é decisão de produto, não conserto.

## 20. Como conferir que continua valendo

```bash
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~AnamneseDoPaciente"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~AnamneseSobrevive"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~ConjuntoClinico"
python3 tools/verificar-suite.py    # a checagem 38, com autoteste
```

---

# Parte IV — a FOLHA ÚNICA (set/2026, mockup 01)

## 21. O pedido, e o que ele derrubou

A direção olhou a tela de atendimento e escreveu: *"São coisas não tão profissionais que
eram aceitáveis no começo do projeto. Hoje em dia não são aceitas e nem devem ser aceitas.
Pode diminuir a quantidade de campos tanto para o médico quanto para a enfermeira e deixar
campos de texto livre para escrever o que quiser durante a sessão, é melhor segundo eles.
Lembrando que precisamos também dos campos de imprimir a sessão do atendimento tanto
quanto salvar."*

Foram desenhados **cinco mockups** (`docs/mockups/atendimento-cinco-desenhos.html`) e a
direção aprovou o **01 — Folha única**. As outras telas do Consultório vieram na mesma
língua num segundo desenho (`docs/mockups/prontuario-folha-unica.html`), também aprovado.

O que foi MEDIDO antes de desenhar, e que justifica o tamanho da mudança:

| | antes | depois |
|---|---|---|
| campos na tela do médico | 12, em quatro abas | 1 (a folha) + 4 números na tira |
| campos na tela da enfermagem | 16 | 1 (a folha) + a tira de sinais vitais |
| altura do campo de escrever da enfermagem | **90 px de teto** | o que sobra da tela |
| botões na barra do médico | 4 — e o 4º saía CORTADO a 1366 px | 4, com o Imprimir no rodapé |
| cartões empilhados em Exames e anexos | 3 | 1 superfície com régua de chips |
| botões por linha em Prescrições | 6 | 1 + o "⋯" |
| itens do rail | 9 numa lista corrida | 10 em três grupos |

## 22. A regra que governa a redução

**Reduzir a TELA nunca é reduzir o REGISTRO.** Nenhuma coluna foi apagada: os doze campos
da sessão continuam no banco (guarda de 20 anos, Lei 13.787/2018), continuam saindo no
relatório do convênio separados por assunto, e continuam editáveis — atrás da linha
"Detalhar em campos separados…" ao pé da folha.

⚠️ **E a linha ANUNCIA quantos a sessão aberta já tem** (`CamposDetalhados`,
`SeloDetalhe`). Sem esse selo, a sessão de 27/08 — que tem hipótese, conduta e exame
preenchidos — sumiria de VISTA sem sumir do banco, que é o pior dos dois mundos e o defeito
recorrente do projeto cometido pela própria reforma que o corrige.

## 23. Por que o detalhe é JANELA, e não um bloco recolhido

Um bloco recolhido na própria tela cresce com o dado e disputa altura com a folha, e **filho
ancorado que não cabe é DECEPADO** (parcela 79, nesta mesma tela). Do outro lado da clínica
a consulta da COFEN já abre em janela desde a parcela 88, 5ª rodada — o mesmo gesto, o mesmo
desenho, e duas telas irmãs que se parecem.

A janela edita o **MESMO ViewModel** e **não grava nada**: quem grava é o "Salvar sessão" da
tela de trás, e o rodapé dela escreve isso. Duas cópias do mesmo registro dariam duas
verdades sobre a mesma sessão.

## 24. A promessa do mockup que o código NÃO cumpre

O desenho trazia, no rodapé, *"salva sozinha a cada pausa"*. **A folha não salva sozinha, e
é decisão:** cada gravação de uma evolução que já existe cria uma `VersaoEvolucao` (parcela
52), e salvar a cada pausa encheria o prontuário de dezenas de versões por sessão — o
registro passaria a mentir sobre quantas vezes ele foi corrigido.

No lugar da promessa, o rodapé diz o que houve: **"Última gravação às 14h37"**
(`UltimaGravacao`, limpa na troca de paciente). Prometer na tela o que o código não faz é a
garantia aparente que este projeto recusa desde a parcela 3 — inclusive quando a promessa
está num mockup aprovado.

## 25. Onde o desenho aprovado NÃO foi seguido, e por quê

Três desvios, todos declarados:

1. **O Histórico não FUNDIU as duas listas.** O mockup desenhava uma linha do tempo única
   com sessão médica, enfermagem, infusão e documento na mesma tabela. A lista rica de
   sessões tem busca no texto, contagem de anexos, marca de correção e os botões da linha —
   e os ids são **por tabela**, então um "cancelar" na linha errada cancelaria o registro de
   outra pessoa, sem estourar nada (o defeito que `PacientesView.xaml` documenta desde a
   parcela 71). A tela funde a **vista** — uma superfície, a busca no topo, as sessões
   preenchendo, a linha do tempo ancorada embaixo com teto —, nunca as listas.
2. **"Salvar e finalizar" continuou sendo dois atos.** O mockup punha um botão só. Finalizar
   carimba `FimAtendimentoEm` e avisa o balcão que a sala vagou; se todo Salvar finalizasse,
   salvar no meio da consulta mandaria o recado errado. O Finalizar continua na faixa da
   sessão, onde já mora — e ele **já salva a evolução antes de carimbar** (parcela 74).
3. **Os chips de Medidas e Avaliações não trazem CONTAGEM.** O desenho mostrava "Peso · 6" e
   "Oswestry · nunca aplicada". Contar por tipo exige uma consulta que hoje não existe, e
   inventar o número seria pior que não mostrá-lo. Fica como pendência.

## 26. As tabelas laterais viraram ABAS (a 9ª reprovação do cliente)

> *"Essas tabelas laterais não me agradam! Seria melhor criar uma aba para elas e
> deixá-las profissionais!"* — a direção, set/2026, com o print das duas telas já
> redesenhadas.

As duas telas de atendimento — a do médico e a da enfermagem — eram **duas colunas**:
escrever à esquerda (`1.6*`, mínimo 440 px) e reler numa faixa à direita (`*`, mínimo
320 px). A faixa não cabia no que ela precisava dizer:

| o que a faixa mostrava | como saía em 320 px |
|---|---|
| a sessão passada, com sete campos | seis frases cortadas em pontos diferentes, cada uma com o rótulo dentro do texto (`"Queixa: lombalgia há três mes…"`) — fragmentos, não um registro |
| enfermagem e infusões | modo COMPACTO, três linhas por seção, num vão de ~200 px em que o estado vazio ocupava quase toda a altura |
| as passagens (enfermagem) | um cartão com moldura por passagem, dentro de um cartão — retângulo dentro de retângulo |

A régua de leiaute do `README.md` responde sozinha: **quantas perguntas esta tela
responde?** Três — *o que eu escrevo agora*, *o que veio antes* e *o que o outro lado
registrou* —, e pergunta a mais é **aba**, não caixa menor. Foi a mesma correção da central
de documentos (parcela 82), onde encolher os cartões não resolveu porque a pergunta
estrutural ainda não tinha sido feita.

**O desenho que ficou**, igual nas duas telas:

| aba | médico | enfermagem |
|---|---|---|
| 1 | A sessão de hoje | A passagem de hoje |
| 2 | Sessões anteriores | Passagens do paciente |
| 3 | Enfermagem e infusões | Conduta médica e infusões |

⚠️ **A barra de ações, os avisos, o plano de cuidados e o RODAPÉ ficam FORA das abas**,
ancorados no `DockPanel` de cima. Trocar de aba não pode esconder o botão que grava nem a
mensagem que ele escreve: botão que some quando alguém vai reler a sessão passada é a
gravação que não acontece.

⚠️ **O que a coluna aberta dava — reler ENQUANTO se escreve — não se perdeu por inteiro.**
A folha ganhou uma linha quieta (`ResumoSessaoAnterior.ContextoDaUltima`): *"Última sessão
em 27/08/2026 · EVA 8 → 3 · ↩ Voltar em 02/09/2026 — reavaliar a EVA"*. É a resposta para
*"por que este paciente está aqui hoje"*, e ela não pode custar um clique. O resto está na
aba ao lado. Sem essa linha, trocar a coluna pela aba teria custado justamente o campo que
a parcela 77 existiu para pôr na tela.

### 26.1 O que "profissional" mudou de fato

- **O rótulo saiu de dentro do texto.** `ResumoSessaoAnterior` devolvia frases prontas
  (`"Conduta: agulhamento lombar"`); agora devolve pares `Rotulo`/`Valor`
  (`CampoDaSessaoAnterior`), e a aba desenha uma **coluna de rótulos alinhada**
  (`SharedSizeGroup`) com o valor ao lado, que é como se lê um prontuário no papel. O valor
  **quebra a linha** em vez de ser cortado — a largura agora existe.
- **A data virou âncora**, numa coluna própria compartilhada entre as linhas: sem o
  `SharedSizeGroup`, a linha com "EVA não medida" ficava mais larga que a com "EVA 7 → 4" e
  a régua saía em escada.
- **Cada sessão é uma LINHA com um traço embaixo**, nunca um cartão: retângulo dentro de
  retângulo é a colcha de retalhos que o README proíbe. Vale igual para as passagens da
  enfermagem, que eram cartões com moldura e raio.
- **A linha do tempo deixou de ser COMPACTA.** O corte em três itens por seção existia
  porque a coluna tinha ~350 px de altura útil; numa aba inteira ele esconderia a quarta
  aferição da tarde e o resumo diria "3 de 12", que é a tela pedindo desculpa por um limite
  que não precisa mais existir.
- **Cinco sessões anteriores, não três.** O três era a altura da coluna, não uma decisão
  clínica. Não é "todas" de propósito: o prontuário inteiro — com busca no texto, anexos e
  correções — é a seção **Histórico**, e duas telas respondendo à mesma pergunta é o que faz
  alguém procurar a diferença que não existe.

### 26.2 O defeito que a mudança de modelo revelou

`RepetirUltima` comparava com `"—"` — um sentinela que o modelo **deixou de produzir na
parcela 77**, quando o campo vazio passou a ser `string.Empty`. A comparação era portanto
sempre verdadeira, e o que ia para o campo `Conduta` era o texto **já rotulado**:

```
Conduta = "Conduta: agulhamento lombar, 20 min."
```

Gravado assim no prontuário e impresso assim no relatório do convênio; no segundo clique,
`"Conduta: Conduta: …"`. Nada estourava. Só apareceu porque o rótulo saiu de dentro do
texto e o compilador passou a exigir que alguém dissesse **qual campo** estava sendo lido.

Junto veio a segunda metade: sem conduta escrita, o botão dizia *"trazida para a tela"*
tendo trazido **nada** — e depois da folha única esse é o caso normal, porque a sessão é
escrita na folha e não no campo Conduta. Agora ele repete a conduta quando ela existe, o
texto da folha quando não existe, **nunca por cima do que já está escrito**, e diz o que
trouxe. Quando não há o que trazer, ele **diz isso** em vez de afirmar um sucesso que não
houve.

## 27. Como conferir que continua valendo

```bash
python3 tools/compilar-sombra.py      # o C# das dez telas WPF
python3 tools/verificar-suite.py      # XAML, chaves, o rail e a checagem 38
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
```

E, na tela: abrir uma sessão ANTIGA (com queixa e conduta preenchidas) e conferir que o selo
da linha do detalhe diz quantos campos ela tem. Se ele não disser, o dado continua no banco e
sumiu da vista — que é exatamente o que a parte 22 existe para impedir.
