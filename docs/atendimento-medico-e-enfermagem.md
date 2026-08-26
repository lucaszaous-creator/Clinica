# X · Y · XY — o que o médico vê, o que a enfermagem vê, e o que os dois veem

> **A frase que decide o desenho inteiro: XY é a LEITURA. X e Y são as ESCRITAS.**

Parcela 72. Este documento é a referência do modelo de atendimento clínico do sistema —
quem alcança qual dado, por qual porta, e por quê. Ele é lido junto de
[`docs/permissoes-por-perfil.md`](permissoes-por-perfil.md) (a tabela de bits por perfil) e
de [`docs/conformidade-lgpd.md`](conformidade-lgpd.md) (o placar do compromisso).

> **Este documento responde QUEM VÊ.** O CONTEÚDO do ato — a consulta de enfermagem em cinco
> etapas (COFEN 358/2009) e os três eixos da sessão médica — está no documento irmão,
> [`docs/registro-do-atendimento.md`](registro-do-atendimento.md) (parcela 73).

---

## 1. O pedido, e o que ele revelou

A cliente pediu, com estas palavras:

> *"Quando o médico abre o cadastro do paciente através do módulo clínico para atender, ele
> enxerga X itens de atendimento; eu preciso que nessa tela que acabamos de ver a enfermagem
> enxergue Y e, caso as coisas de X e Y se completem, entreguem XY para ambos. Precisamos
> deixar profissional as telas e com TUDO aquilo que os médicos e enfermeiros enxergam. Além
> do mais as prescrições precisam ir para a ficha do paciente junto também com as infusões e
> evoluções, tanto a evolução da parte médica como da enfermagem."*

A medição do domínio respondeu antes do desenho. `PerfilAcesso.Profissional` e
`PerfilAcesso.Enfermagem` **já compartilhavam** `VerAgenda | VerFichaPaciente |
VerProntuario | ColherAssinaturaPaciente`. Ou seja:

> **Nenhuma permissão nova era necessária para entregar o XY. O que faltava era PORTA** — o
> defeito recorrente do projeto pela décima segunda vez.

E havia um argumento duro contra inventar bit: `Permissao` é `[Flags]` de **`int`**, gravada
como INTEIRO em bases de produção, e `RegistrarEvolucaoEnfermagem = 1 << 30`. **Sobra UM
bit** antes de o enum precisar virar `long` — o que muda o tipo da coluna numa base viva.
Bit novo aqui é orçamento escasso, não estilo.

---

## 2. O conjunto XY — o que os DOIS veem

Critério aplicado literalmente: **entra em XY o que muda o que aquela pessoa vai fazer nos
próximos minutos com este paciente.**

| # | Item | Onde aparece hoje |
|---|---|---|
| 1 | **Alergias e lista de problemas** | Atendimento (médico) · folha de execução, LINHA a linha · linha de contexto da tela da Enfermagem |
| 2 | **Medicação de uso contínuo** | Cabeçalho da folha de execução · linha de contexto da Enfermagem |
| 3 | **Evolução médica** (queixa · conduta · evolução · orientações) | Linha do tempo clínica, chip **Médica** — nas três portas |
| 4 | **Evolução de enfermagem** (sinais vitais, intercorrência, reação) | Linha do tempo, chip **Enfermagem** · alertas do Atendimento quando é intercorrência recente |
| 5 | **A aferição ANTERIOR dos sinais vitais** | Linha de contexto da Enfermagem, com a hora |
| 6 | **Folhas de infusão do paciente** (histórico e a de hoje) | Linha do tempo, chip **Infusões** · botão "Abrir folha de hoje" na Enfermagem |
| 7 | **Documentos clínicos vigentes** | Linha do tempo, chip **Documentos** (na ficha ele é falso — ver §5) |
| 8 | **Termo do procedimento pendente** | Botão condicional na Enfermagem · faixa do Atendimento · fila · ficha |
| 9 | **Anexos** (laudo, foto de lesão) | Botão da linha da sessão, sob `VerProntuario` |
| 10 | **Peso, com a data** | Linha de contexto da Enfermagem — é insumo da conferência da dose (mg/kg) |
| 11 | **Curva de dor (EVA) e escalas** | Aba própria do Consultório |
| 12 | **Quem está na clínica hoje** | A tela da Enfermagem ABRE com a fila do dia |

**A última pessoa entre a alergia e a veia é a técnica.** Foi esse argumento que decidiu a
maior parte desta lista.

---

## 3. O X do médico — o que só ele faz

| Ato | Bit | Por que não é XY |
|---|---|---|
| Escrever a evolução médica (queixa, conduta, EVA, mapa corporal, modelos) | `EditarProntuario` | Conduta é decisão. Dar o bit para destravar outra coisa entrega a escrita do prontuário inteiro — o bit sobrecarregado que a parcela 49 gastou uma parcela para desfazer. |
| Prescrever e assinar (receita, atestado, pedido de exame, folha de infusão) | `Prescrever` | Ato privativo. |
| Suspender item da folha | `Prescrever` | É ato de quem prescreve, não de quem executa. |
| Emitir documento clínico | conforme a folha | Emitir é ato; **ver** o que foi emitido é XY. |
| Escalas clínicas (PHQ-9, GAD-7, Oswestry, Katz, FINDRISC) | `VerProntuario`, tela do Consultório | **Fora de Y por decisão, não por esquecimento** — ver §7. |

## 4. O Y da enfermagem — o que só ela faz

| Ato | Bit | Por que não é XY |
|---|---|---|
| Checar a execução (✓ / rodela + justificativa / retificação) | `ChecarPrescricao` | *A conferência vale porque foram duas pessoas.* O prescritor conferindo o que ele mesmo mandou fazer destrói o único controle de dupla checagem da clínica. |
| Escrever a evolução de enfermagem | `RegistrarEvolucaoEnfermagem` | O registro é assinado com nome **e COREN**. Médico escrevendo evolução de enfermagem é registro assinado com o conselho errado — pior que a lacuna que resolveria. |
| Assinar eletronicamente a execução | `ChecarPrescricao` | Responder pelo que se checou. |
| A fila da Sala de infusão | `ChecarPrescricao` | O médico lê a execução pela linha do tempo; ele não entra na fila da sala. |

⚠️ **O médico alcança a evolução de enfermagem sem receber bit nenhum e sem item novo na
sidebar dele** — pela linha do tempo, sob `VerProntuario`, que ele já tem.
`PermissoesFaturamentoTests.Medico_e_enfermagem_LEEM_o_mesmo_e_ESCREVEM_coisas_diferentes`
falha se alguém juntar os dois lados de volta.

---

## 5. A linha do tempo clínica — um componente, três portas

`LinhaDoTempoClinicaView` + `LinhaDoTempoClinicaViewModel` moram no **shell**, pela razão de
sempre: copiar a tela entre módulos daria três leituras do mesmo prontuário divergindo na
primeira correção — e o que elas mostram é dado de saúde, com regra de acesso por natureza.

Chips de seção **contados**, um marcado por vez:

> `Médica (12)` · `Enfermagem (31)` · `Infusões (4)` · `Documentos (7)`

⚠️ **Os chips desmarcados MOSTRAM a contagem.** É o número visível que faz a enfermeira
descobrir que há 12 sessões médicas para ler. Chip pré-marcado sem número ao lado deixaria a
entrega desligada justamente na tela de quem mais precisa dela.

### 5.1 Por que chips de SEÇÃO e não uma lista fundida

Duas razões, e cada uma sozinha decide:

1. **Os ids são POR TABELA.** A evolução de enfermagem nº 42 e a `Evolucao` nº 42 são
   registros de pacientes diferentes. Uma lista fundida cujo comando destrutivo recebesse só
   o id cancelaria o registro errado — **não estoura, não avisa**. O item carrega
   `Natureza` + `Id`, e é isso que permite fundir as listas no dia em que fizer sentido, sem
   reabrir o buraco. `ConjuntoClinicoTests.A_linha_de_enfermagem_42_nao_encosta_na_sessao_42`
   é a amarra.
2. **A ordenação cronológica é impossível hoje.** `Evolucao.Data` é `DateOnly`; a evolução
   de enfermagem tem data **e hora**. Ordenar a médica às 00:00 a poria antes de todas as
   aferições do dia, inclusive da reação que a motivou; ordenar por `CriadoEm` usaria quando
   o texto foi DIGITADO — e o módulo do Consultório existe inteiro porque isso acontece dias
   depois. **Ordenar um prontuário por uma hora que não existe é fabricar sequência de
   eventos num documento que responde em auditoria.**

### 5.2 Onde ela entra, e o que cada porta configura

| App / tela | Configuração | Por quê |
|---|---|---|
| **Recepção — ficha, aba Prontuário** | `MostrarDocumentos = false`, ação de abrir/cancelar a **sessão médica** | Os dois `Card` empilhados (sessões e enfermagem) viraram UMA superfície. O chip Documentos é falso aqui: a aba Documentos ao lado é a porta do papel e faz mais (emitir, termo, assinar, enviar) — duas listas do mesmo papel na mesma tela fazem a pessoa procurar a diferença que não existe. |
| **Consultório — aba Prontuário** | só **Enfermagem** e **Infusões**, sem ações | A lista rica de sessões daquela tela **não foi substituída**: ela tem busca no texto, contagem de anexos, marca de correção e os botões da linha. Trocá-la pela genérica tiraria capacidade de quem a usa todo dia. |
| **Consultório — Atendimento**, coluna direita | `Compacto = true`, Enfermagem e Infusões | Três linhas por seção. A coluna tem ~350 px de altura útil, e duas listas rolando dentro de um vão de três linhas é o proibido do README. |
| **Enfermagem** | tudo, abrindo no chip **Enfermagem** | Infundir sem saber a conduta da consulta de hoje é executar às cegas. |

### 5.3 A coluna que não cabia

Antes de acrescentar informação ao Atendimento, foi preciso corrigir o leiaute:
`AtendimentoView` exigia `520 + 16 + 400 = 936 px`, e a janela mínima da suíte (960),
descontando o rail de 56 e as margens de 24+24 do workspace, deixa **856** — com o painel de
categoria fixado, **606**. O WPF honra o `MinWidth` e **corta o excesso à direita**: não há
rolagem horizontal ali. As duas colunas passaram a ser elásticas (`1.6*` / `*`).

---

## 6. O ponto único das naturezas clínicas

`CatalogoRegistroClinico` (em `Clinica.Domain/Prontuario/`) lista as **nove naturezas** do
registro clínico, com rótulo e permissão de leitura:

sessão médica · evolução de enfermagem · prescrição de infusão · documento emitido ·
avaliação · medida · problema anotado · anexo · mapa corporal

**Quatro leitores a consomem:**

1. `LinhaDoTempoClinica.Montar` (Application, função **pura**) — a linha do tempo;
2. `GuardaProntuarioService` — o "último registro" **e** a contagem de `SituacaoGuarda`;
3. `ExportacaoProntuarioService` — declara o arquivo CSV de cada natureza;
4. `TitularDadosService` — o art. 18, II da LGPD.

⚠️ **A regra que fica:** *entidade clínica nova entra em UMA lista, e quatro leitores a
consomem.* Sem isso, o que um esquecer aparece como **lista limpa**, indistinguível de "não
houve nada".

O defeito já tinha sido cometido **duas vezes** neste exato assunto, e os comentários do
código confessam: a folha de infusão ficou de fora da primeira versão da guarda; a lista de
problemas — onde moram as ALERGIAS — ficou de fora da primeira versão da exportação. Uma
terceira versão estava de pé: `SituacaoGuarda` contava CINCO naturezas enquanto o prazo de
20 anos era calculado sobre SETE, e o documento do art. 18, II cobria TRÊS de nove.

`ConjuntoClinicoTests` é **comportamental, não declarativo**: ele grava um registro de cada
natureza e exige que ele APAREÇA na contagem, no CSV e no texto do titular. Uma declaração
conferida contra outra declaração ficaria verde com as duas erradas do mesmo jeito. **Ele
falha no commit em que a próxima entidade clínica nascer.**

---

## 7. O que ficou de fora, e por quê

1. **Fundir as duas evoluções numa lista cronológica.** Bloqueada pela chave por tabela e
   pela ausência de hora na `Evolucao`. Volta à mesa quando `Evolucao` ganhar `Hora` —
   migration aditiva, outra parcela. O discriminador (`Natureza` + `Id`) já existe
   justamente para que aquela parcela não reabra o bug.
2. **Escalas clínicas (PHQ-9, GAD-7, Oswestry, Katz, FINDRISC) para a enfermagem.** É o
   único item em que o risco de SOBRAR vence: o item 9 do PHQ-9 é ideação suicida, e o
   escore é saúde mental identificada — o dado com maior distância entre risco e necessidade
   (art. 6º, III da LGPD). A técnica que infunde não precisa dele para não errar. E o alerta
   de item também não vai como contexto permanente: ideação suicida marcada em março
   acendendo em toda troca de curativo de agosto é o alerta que se aprende a fechar sem ler
   — e aí morre o de dipirona ao lado. Se a direção clínica quiser a escala aplicada
   **naquela passagem** chegando à sala, isso é decisão dela, por escrito.
3. **Dívida, glosa e cota do convênio na tela da enfermagem.** Ela não tem `VerFinanceiro`,
   `EditarAgenda` nem porta de autorização: não pode resolver nenhum. Alerta sem porta ensina
   a ignorar o alerta. O **termo** é a exceção *porque* ela tem o bit e a porta existe.
4. **Colher medida clínica (`MedidaClinica`) pela enfermagem.** Hoje a PA é gravada em
   `EvolucaoEnfermagem`, com hora, por decisão escrita. Destravar `MedidaClinica` daria
   **dois lugares para gravar a mesma aferição**, sem nada na tela dizendo qual. A ponte é de
   **LEITURA** — ver §8.
5. **Anexo na evolução de enfermagem** (foto de flebite, ferida). Hoje o anexo pertence à
   `Evolucao` (sessão médica). É **entidade**, e é outra parcela.
6. **Item "se necessário" (SOS) com N administrações.** Dipirona SOS às 14h e às 18h não tem
   onde ser registrada, e retificar apagaria a vigência da primeira, que é falso. **Decisão
   adiada, não esquecimento.**
7. **Dose/volume efetivamente administrados e lote/validade na checagem.** Infusão
   interrompida aos 40 mL de 100 mL é gravada como "Realizado", igual à que correu inteira.
   Adiado, e registrado como decisão — a folha impressa é o que responde por nós.

---

## 8. A curva de pressão vem de DUAS fontes

A PA está declarada em `CatalogoMedidas` desde a parcela 37, com as faixas publicadas — e a
série **nascia vazia e continuava vazia**. A razão é estrutural: a única porta de ESCRITA de
`MedidaClinica` está no app do MÉDICO, enquanto a pressão de verdade é aferida na
ENFERMAGEM, toda sessão, e vai para `EvolucaoEnfermagem`. **Curva vazia se lê como "este
paciente nunca teve a pressão aferida"**, que é falso.

`MedidaClinicaService.SerieAsync` passou a mesclar as duas fontes para a PA, com quatro
regras:

- **cada ponto DIZ de onde veio** (`PontoMedida.Procedencia`) — a curva teria dois pontos do
  mesmo dia sem dizer que um é de antes da consulta e o outro de meia hora depois da bomba,
  e a diferença entre os dois é justamente a leitura clínica;
- **a faixa sai da MESMA definição do catálogo**, nunca de uma segunda leitura de "pressão
  normal";
- **cancelada e retificada não entram** — registro desdito não é aferição (ele continua no
  prontuário, marcado, na linha do tempo);
- **dentro do mesmo dia a HORA desempata**.

Nota de dívida: `MedidaClinica` tem `ProfissionalId` e `CriadoPor` e **não tem
`AutorConselho`**. Se um dia a enfermagem escrever ali, a coluna aditiva entra no mesmo
commit.

---

## 9. As recusas clínicas que entraram junto

### 9.1 A alergia é conferida na ADMINISTRAÇÃO, não só na assinatura

`PrescricaoInternaService.AssinarAsync` confere a folha contra as alergias do paciente e
recusa sem confirmação escrita desde a parcela 42. A **execução não conferia nada**: as
únicas guardas de `ChecarAsync` eram "item já checado" e hora futura.

O caminho de dano é inteiro e concreto: a folha é assinada de manhã, sem alergia registrada;
o item 2 causa reação; **a própria técnica** grava a alergia pelo campo "Reação a registrar
como alergia"; e os itens 3, 4 e 5 seguem pendentes, com a folha na sala, sem **ninguém
reconferir**. O sistema tinha o dado — gravado por quem seria a vítima do silêncio — e não o
usava.

É a **quinta recusa do projeto**, e a única que impede dano ao paciente. Três decisões:

1. **Só na administração** (`Realizado`). Não administrar é o desfecho seguro; cobrar
   confirmação para a rodela treinaria a equipe a confirmar sem ler.
2. **Confere o ITEM, não a folha.** Repetir a resposta da folha inteira acenderia na linha
   do soro por causa da dipirona da linha de baixo.
3. **Avisa e exige confirmação — não impede.** Pode haver dessensibilização, o registro pode
   estar errado, e quem está com o paciente é quem decide. O que não pode é acontecer *sem
   alguém perceber*.

Vale igual na **retificação**: retificar de "não realizado" para "realizado" é administrar.

### 9.2 O COREN é obrigatório

O comentário da entidade já dizia que *"evolução de enfermagem sem o registro no conselho não
é evolução de enfermagem"* — e **não havia guarda em lugar nenhum**. Bastava um login sem
`Profissional` vinculado (o caso de toda técnica cadastrada sem ficha) para que **todo
registro daquela máquina entrasse no prontuário sem COREN, para sempre** — e sem conserto
barato, porque o campo é COPIADO no ato: corrigir exigiria retificar registro a registro, com
motivo, um por um. COFEN 429/2012.

`RegistrarAsync`, `RetificarAsync` e `ChecarAsync` recusam sem conselho, e **a frase nomeia o
conserto** (*"peça em Acessos para vincular o seu cadastro de profissional a este login"*),
pelo precedente do "Meu dia": sem `Profissional` vinculado, o app **diz** que está sem.

---

## 10. Os bloqueadores de conformidade que a rodada achou

| O que estava errado | O estrago |
|---|---|
| `CancelarAsync(id, Operador)` **posicional** na ficha, com o login caindo na vaga do MOTIVO | `MotivoCancelamento = "ana.silva"`, `CanceladaPor = null`, auditoria com `Operador = "?"` — e a única recusa do serviço nunca disparava. Derrubava os pontos **1** e **6** do compromisso de conformidade. |
| Rótulo "Excluir", diálogo *"APAGAR a sessão? os anexos vão junto"*, snackbar *"sessão EXCLUÍDA"* | Nada é apagado desde a parcela 52, e os anexos não vão a lugar nenhum. |
| Seis botões da ficha com **só** a barreira que impede | `Exigir` LANÇA: a enfermeira, que tem `VerProntuario` e não `EditarProntuario`, levava `InvalidOperationException` até a rede do Dispatcher. `PodeEditarProntuario` existia no VM, documentada como "a metade VISÍVEL", sem um leitor no XAML. |
| Card "Evolução da dor" sem barreira | `SemMedidaEva` nasce `true`: a região ficava de pé com quatro travessões e "Nova sessão" aceso para Financeiro e Faturista — que se lê como tela quebrada. |
| Aba Documentos da ficha sem **Assinar** e **Enviar** | `PodeAssinar` e `PodeEnviar` eram calculados e nunca lidos; a tela irmã do mesmo módulo tinha os quatro botões. Sem assinatura o arquivo não vale. |
| `VerAnexosAsync` do Consultório sem bit nenhum | A janela abre LAUDO. As ações de dentro dela já exigiam o bit certo; a porta, não. |
| `AnexosAsync(e.Id)` dentro do `foreach` na ficha | Quarenta sessões = quarenta viagens ao Neon por recarga, numa tela que o balcão abre com o paciente na frente. |
| Tela da Enfermagem sem guarda de LEITURA | Montava a carteira inteira e carregava a evolução sem conferir `VerFichaPaciente` nem `VerProntuario` uma vez. |
| `OrigemAcessoProntuario.SalaInfusao` sem um único escritor | A janela de silêncio da trilha é **por origem**: gravar `ProntuarioClinico` fundia o acesso da enfermagem com o de quem abriu o prontuário clínico de verdade, apagando a distinção que uma investigação procura. |

---

## 11. O que a revisão adversarial do próprio diff achou

A parcela foi auditada por duas leituras independentes **depois** de estar verde — 1740
testes, três redes locais e o CI nos três jobs. Elas acharam onze defeitos, e **nenhum
quebrava nada**. Vale o registro porque as famílias se repetem:

| Defeito | Por que passou |
|---|---|
| `Secao = SecaoInicial` no construtor | Propriedade `init` é atribuída **depois** do corpo do construtor. A tela da Enfermagem abria em "Médica" — e as outras duas portas funcionavam por acidente, porque restringem as seções visíveis |
| A HORA nunca aparecia | `TextoParaVisibilidade` faz `value as string`; um `TimeOnly?` encaixotado devolve `null` → `Collapsed` para sempre |
| Selo INTERCORRÊNCIA, realce e `RegistradoEm` perdidos | A troca da lista pelo componente genérico não conferiu o que a antiga MOSTRAVA |
| Autor virou "quem DIGITOU" | `CriadoPor` no lugar de `Profissional?.Rotulo` — a linha continuou existindo e passou a responder outra pergunta |
| "Enviar" sem nenhuma barreira | Comando portado sem `Exigir`, com a metade visível em estado puro |
| "Assinar" com as barreiras discordando | Botão pelo bit do TIPO, comando com `Prescrever` fixo |
| Portão de natureza engolindo o comparecimento | O `PermissaoVer` do catálogo era teto onde precisava ser piso |
| Guarda em 1 + N×2 consultas | O laço por sessão, num serviço que a tela varre paciente a paciente |
| COREN recusado no clique, botão aceso | Recusa nova de serviço sem a metade visível |
| Art. 18 II sem anexos de sessão cancelada | `DoPacienteAsync` devolve só as vigentes |
| Curva mesclada, tabela não | A ponte entrou só em `SerieAsync` |

⚠️ **E a auditoria em workflow devolveu `sobreviventes: []`** — não porque nada sobreviveu,
mas porque 26 dos 28 agentes falharam no limite de uso e nenhum cético votou. O script
tratava "sem voto" como "refutado". É a lição da parcela 66 cobrada dentro da própria
ferramenta de achar defeitos: **resultado vazio se investiga, nunca se lê como aprovação.**

---

## 12. Como conferir que continua valendo

```bash
# Os testes que fixam este documento
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~ConjuntoClinico"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~ConferenciaNaExecucao"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~PressaoDeDuasFontes"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~PermissoesFaturamento"

# As três redes locais, antes de todo push
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
python3 tools/compilar-sombra.py
python3 tools/verificar-suite.py
```

---

## 13. O POSTO da enfermagem: ver todos os pacientes e ATENDER (parcela 88)

A cliente voltou com duas frases:

> *"Os enfermeiros podem ver todos os pacientes e clicar em atender, em vez de ver só os
> pacientes dele. E quando clicado em atender teremos seções de texto livre para escrever
> sobre o atendimento e campos de evolução. Precisamos deixar isso profissional no módulo
> Clínico."*

A parcela 72 entregou **quem vê**; esta entrega **por onde se entra e onde se escreve**.

### 13.1 A primeira frase tinha um mecanismo, e ele era invisível

Três fatos verdadeiros, que somados produziam o defeito:

1. As cinco telas de lista do Consultório — o dia, a semana, a carteira, a dívida de
   prontuário e os números — filtravam por `SessaoUsuario.Atual.ProfissionalId`. (A
   **carteira** saiu desse recorte na 3ª rodada — §13.6; as outras quatro continuam nele,
   porque a agenda tem dono.)
2. A enfermeira **precisa** de um `Profissional` vinculado: é dele que sai o **COREN**
   copiado em cada registro (parcela 72), e `IdentificacaoExecutante.Exigir` recusa sem ele.
3. **A enfermagem não tem agenda própria.** Os horários pertencem a quem consulta; ela passa
   por todos eles — a frase da parcela 71, *"todo paciente precisa passar pela enfermagem"*.

> ⚠️ **Cadastrá-la CERTO fazia as cinco telas dela abrirem VAZIAS.** Nada falhava: sem erro,
> sem log, sem exceção. E tela vazia se lê como sistema quebrado, não como "esta lista não é
> para você".

A decisão mora no **domínio**, porque é decisão — e projeto WPF não compila no `dotnet test`:

| Função (`PerfisAcesso`, `Clinica.Domain`) | Responde |
|---|---|
| `EscreveComoEnfermagem(efetivas)` | escreve por **Y** (`ChecarPrescricao \| RegistrarEvolucaoEnfermagem`) e **não** por **X** (`EditarProntuario \| Prescrever`) |
| `ProfissionalDaListaDoPosto(efetivas, profissionalId)` | de quem é a lista — `null` = a clínica inteira |
| `MotivoDaListaDoPosto(efetivas, profissionalId)` | **por quê**, e são dois motivos diferentes |

`PostoClinico` (módulo Clínico) é só o adaptador que lê `SessaoUsuario.Atual`.

**Por que pelos BITS e não pelo texto do conselho:** `Profissional.RegistroConselho` é campo
livre — "COREN-SP 999999", "Coren SP 12345", "coren/sp 12345". Procurar "COREN" nele daria
uma regra que erra no dia em que alguém digitar diferente, **e erra em silêncio**.

⚠️ **Quem tem OS DOIS lados responde `false`.** O Gerente Geral recebe `Todas`, inclusive o
bit da enfermagem, e ele **tem** agenda própria: devolver-lhe a clínica inteira na carteira
esconderia justamente os pacientes dele. A regra é "escreve **SÓ** por Y".

⚠️ **E a FRASE importa tanto quanto o filtro.** *"Peça à direção para ligar o seu usuário ao
seu cadastro"* é verdade para quem não tem vínculo e **mentira** para a enfermeira, que está
vinculada. Instrução errada com cara de instrução certa manda o suporte procurar um defeito
que não existe — é a irmã de "falha exibida como sucesso".

### 13.2 "Atender" é uma palavra só, e leva a duas seções

`PostoClinico.ChaveDoAtendimento()` bifurca o destino:

| Quem clica | Cai em |
|---|---|
| quem consulta | `ChaveAtendimento` — a sessão em **S-O-A-P** |
| quem executa | `ChaveAtendimentoEnfermagem` — a passagem nas **cinco etapas da COFEN** |

As duas caem na **mesma** tela do paciente: mesmo crachá, mesmo rail, mesmas seções de
leitura. O que muda é a seção que abre.

> ⚠️ Sem a bifurcação, a técnica cairia no formulário do médico — onde `Salvar` tem
> `IsEnabled="{Binding PodeEditarProntuario}"` e ela não tem `EditarProntuario`. Seria o
> **botão que não faz nada** da parcela 41, com uma tela inteira em volta.

A porta na lista é o botão **Atender** de cada linha da tela da Enfermagem, e o clique na
linha faz a MESMA coisa — dois resultados para o mesmo alvo fariam a pessoa procurar a
diferença que não existe. Os dois modos da lista viraram **chips exclusivos** ("Na clínica
hoje" × "Todos os pacientes"), porque dois botões iguais não dizem em qual dos dois você
está.

⚠️ **O caminho de volta é parte da feature.** A tela da Enfermagem é do **shell** e é
publicada por DOIS módulos: no `Clinica.Recepcao.exe` o módulo Clínico não está carregado, e
`NavegacaoSuite.Ir` devolveria `false` **em silêncio**. Então se pergunta antes, com
`Existe`, e o painel da própria tela continua sendo a resposta onde não há posto clínico —
com tudo o que ele já tinha.

### 13.3 A seção nova: `Atendimento de enfermagem`

Posição **1** do rail, logo depois do Atendimento (`ModuloClinico.SecoesDoPaciente`, checagem
38 casa a lista do C# com os rótulos do XAML, posição por posição).

O desenho é o do Atendimento do médico, **de propósito**: escrever à esquerda, reler à
direita; rodapé ancorado fora do scroll; avisos numa superfície só, uma linha por aviso.
Dois desenhos para o mesmo ato no mesmo sistema é o que faz alguém achar que abriu outro
programa (a reprovação da parcela 47).

A coluna da direita é a **imagem espelhada**: o médico relê a enfermagem e a infusão; a
enfermagem relê a **sessão médica** e a infusão.

> ⚠️ **O compositor NÃO foi reescrito.** `Passagem` é o MESMO `EvolucaoEnfermagemViewModel`
> da janela da sala de infusão e da ficha da Recepção. As regras caras moram lá — hora
> INFORMADA, hora futura recusada, retificação que preserva a data do fato, alergia que
> entra na lista de problemas no mesmo `SaveChanges`. O XAML da seção declara
> `DataContext="{Binding Passagem}"` justamente para ficar idêntico ao da janela.

**A janela continua existindo, e não é dívida.** A folha de execução é MODAL e
`PodeMexer => PodeChecar && EmExecucao`: folha encerrada apaga o painel dela inteiro. A
janela existe para o caso que a justifica — a reação meia hora depois da última bomba —, e
uma seção do workspace não é alcançável de dentro de um modal. **A seção é a quinta porta,
não a substituta.**

O **plano de cuidados do dia** (etapa 4) virou componente do shell
(`PlanoDeCuidadosViewModel`), usado pela tela da Enfermagem **e** pela seção: duas
definições de "o que falta executar hoje" divergiriam na primeira correção.

### 13.4 O que a rodada achou de quebrado no caminho

| Defeito | Mecanismo |
|---|---|
| **O "se necessário" era descartado na gravação** | `AplicarProcesso` copia o cuidado campo a campo e `SeNecessario` ficou de fora desde a parcela 76 — o "lugar 3" da lista de conferência. `CuidadoDoDia.Pendente` é `!SeNecessario && Vigentes.Count == 0`, então **todo cuidado condicional ficava eternamente aguardando** e o contador da sala apontava para nada — exatamente o que o comentário do campo diz existir para impedir. |
| **`EvolucaoEnfermagem.AgendamentoId` sem leitor** | Gravado desde a parcela 71, preservado na retificação, **e nenhuma consulta, tela ou papel o lia**. Sem leitor, dizer "a passagem fica ligada a esta sessão" seria promessa que o código não cumpre. Ganhou o primeiro: o selo **DESTA SESSÃO** na lista de passagens. |
| **Guarda com a mensagem errada** | O "Chamar próximo" mandava a enfermeira consertar um vínculo que já existe. A frase passou a sair do ponto único. |

### 13.5 O que ficou de fora, e por quê

- **A barra de atendimento (Iniciar/Finalizar/Reabrir) fica desabilitada para a enfermagem**
  — `PodeMoverFila` é `EditarAgenda | MovimentarFila`, e `PerfilAcesso.Enfermagem` não tem
  nenhum dos dois. É correto: mover a fila é ato do balcão e de quem consulta. A barra
  continua mostrando o **estado** da sessão, que é o que ela precisa ler.
- **`Finalizar` não grava a passagem de enfermagem**, e é decisão: a evolução dela é
  append-only, várias por passagem, cada uma com a hora do FATO. Gravá-la de carona criaria
  uma passagem com hora que ninguém confirmou. O que não pode é sumir calada — então a tela
  **pergunta**, com a consequência escrita.
- **Nenhum bit novo.** `RegistrarEvolucaoEnfermagem = 1 << 30` é o último positivo do
  `[Flags] int`; e, como na parcela 72, **não era preciso**: faltava porta.
- **A altura da seção** é o ponto a vigiar. A janela declara `MinHeight 670` porque a
  consulta COFEN aberta pede muito; dentro do workspace o compositor divide altura com o
  crachá, a barra e o rodapé. As abas são o filho que **PREENCHE** (`MinHeight="100"`, nunca
  `Height` fixo) e cada uma rola por dentro — mas numa janela muito baixa, com a consulta
  aberta, elas encolhem antes de tudo. É a família da checagem 36.

### 13.6 A correção: vale para médicos também — e "meu paciente" não existe

A cliente voltou duas vezes, e a segunda apagou a premissa da primeira.

**(a)** *"A tarefa acima se refere a enfermeiros e médicos também, eu mencionei somente
enfermeiros."* O buraco do lado de quem consulta era **simétrico e real**:
`MeusPacientesAsync(profissionalId)` devolvia só quem **ele** já atendeu, então

- o paciente de **primeira consulta**,
- o do **colega** que ele está cobrindo,
- e o que o **balcão acabou de cadastrar**

eram **inalcançáveis do Consultório**. Não havia segunda porta: a busca da tela filtra em
memória o que já veio, e o que veio era a carteira dele. A primeira resposta foram dois chips
(*Meus pacientes* × *Todos os pacientes*), com a carteira como padrão.

**(b)** *"Não existe 'meu paciente', todos atendem todos."* E aí a resposta (a) estava
errada pela raiz: **o recorte não precisava de um segundo clique, precisava deixar de
existir.** Dois chips para uma distinção que a clínica não faz é oferecer uma escolha
inventada — e a que abre por padrão seria justamente a lista **mais estreita**, escondendo
o paciente do colega de quem foi chamado para cobri-lo.

**O que ficou:**

| Antes | Agora |
|---|---|
| `ConsultorioService.MeusPacientesAsync(profissionalId, …)` | `ConsultorioService.PacientesAsync(termo, limite, comDor)` — **sem parâmetro de profissional** |
| `IClinicaRepositorio.PacientesDoProfissionalAsync(profissionalId, …)` | `PacientesAtendidosAsync(limite)` — a clínica inteira, de quem veio por último ao mais antigo |
| record `PacienteDoProfissional` | record **`PacienteDaCarteira`** |
| Dois chips + linha de motivo | Nada — a lista é uma só, e o título da tela é **"Pacientes"** |
| `MeusPacientesView(Model)`, `ChaveMeusPacientes` | `PacientesDaClinicaView(Model)`, `ChavePacientesDaClinica` — **o VALOR da chave (`consultorio-pacientes`) não muda**: é contrato de navegação entre módulos |
| Abas da Recepção: *Todos* × *Meus pacientes* | *Cadastro* × *Em tratamento* — o rótulo passou a dizer a PERGUNTA de cada aba, já que as duas listam todo mundo |

⚠️ **Sem termo, a lista é quem a clínica JÁ ATENDEU — e isso não é o recorte de volta.** É a
leitura que a tela existe para dar: sessões, última visita e a queda da dor. O cadastro
inteiro (inclusive quem nunca veio) chega pela **busca**, que casa nome OU CPF.

⚠️ **A busca vai ao SQL, nunca à memória.** A lista vem cortada no teto (200), e filtrar em
memória o que veio cortado faz a busca responder *"não existe"* para todo paciente além dele
— a resposta errada mais cara que uma busca de paciente pode dar, porque leva a **cadastrar a
pessoa de novo** (o CPF duplicado da parcela 57). **E o teto é DITO** no resumo: lista
cortada que se anuncia como a da clínica é corte silencioso.

⚠️ **O resultado da busca não pode mentir sobre as sessões.** `BuscarPacientesAsync` devolve
ficha, não histórico — fabricar `Sessoes = 0` para quem tem quarenta esvaziaria a coluna que
é o assunto da tela, com cara de dado. Daí `SessoesDosPacientesAsync(ids)`, que responde pelo
MESMO critério da lista (`Status == Realizado`); duas contagens de "quantas sessões este
paciente teve" divergiriam na primeira correção.

**E abrir pela lista passou a amarrar o horário de hoje.** Isso era aceitável enquanto ela
era só a dele — o caminho normal é "Meu dia", que já traz o agendamento. Com a lista sendo a
da clínica, ela virou o caminho de quem cobre o colega, e sem o vínculo a evolução nasceria
**solta**: a sessão ficaria em *"Sessões sem evolução"* para sempre, mesmo depois de escrita.
`EntregaDoPaciente.AoPostoAsync` (shell) é o ponto único que a Enfermagem e a lista de
pacientes compartilham — e as **três** portas da linha (Atender, Dor, Avaliações) passam por
ele, porque as três caem na mesma tela e o rail troca de seção sem trocar de paciente.

**O que NÃO mudou, e por quê:** "Meu dia", "Minha semana", "Meus números" e "Sessões sem
evolução" continuam recortados por profissional. A diferença não é conceitual, é de natureza
do dado: **paciente não tem dono; HORÁRIO tem.** A agenda é fato de marcação, e
`PodeChamarProximo` depende disso — na lista da clínica inteira o primeiro da fila pode ser
paciente de outro profissional, e o clique cego anunciaria um nome para a sala do colega. A
autoria também não mudou: quem atendeu, quem assinou e o conselho de cada registro continuam
sendo de quem fez.

### 13.7 Como conferir

```bash
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~PostoDaEnfermagem"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~ProcessoDeEnfermagem"
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~ConsultorioTests"
```

`PostoDaEnfermagemTests.O_dia_da_enfermeira_e_o_da_clinica_inteira` é o que carrega o
arquivo: ele monta a clínica real e pergunta o dia **dos dois lados** contra o serviço de
verdade. Provar só o predicado deixaria passar o defeito, que estava no casamento entre a
regra e o filtro — e elo partido ali não vira erro, vira **lista vazia**.

E `A_lista_de_pacientes_e_a_da_clinica_para_os_dois_lados` é a amarra do item (b): se alguém
devolver um filtro por profissional ao serviço, o paciente do médico some da lista de quem
não é ele — e sumir não estoura nada. Do lado de `ConsultorioTests`,
`A_busca_alcanca_alem_do_teto` fixa que o cadastro inteiro continua alcançável mesmo com a
lista cortada.
