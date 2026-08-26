# Módulo Consultório (`Clinica.Modulo.Clinico`)

As telas da máquina de **quem atende** — médico, fisioterapeuta, acupunturista — e, desde a
parcela 42, da **técnica de enfermagem** que executa a prescrição na sala de infusão.

É carregado por dois executáveis: o `Clinica.Clinico.exe` (Consultório), que carrega **só
ele**, e o `Clinica.Gerente.exe` (Gerente Geral), que carrega tudo. Ver
[`docs/arquitetura-multi-exe.md`](../../docs/arquitetura-multi-exe.md).

> **Este arquivo é o mapa do módulo e o registro do que cada parcela fez nele.**
> As regras de negócio não moram aqui — elas estão no `CLAUDE.md` (raiz) e em
> [`docs/features-por-modulo.md`](../../docs/features-por-modulo.md). Duplicá-las produziria
> duas verdades sobre o mesmo assunto, e é exatamente assim que uma delas envelhece.

---

## O que este módulo NÃO tem

Quase nenhuma lógica. Ele é uma casca de telas: **os serviços moram em
`Clinica.Application`** e os componentes reaproveitados moram no **shell**
(`Clinica.Desktop.Shell/Componentes`) — o mapa corporal, a emissão de documento clínico e o
seletor de paciente subiram para lá justamente porque a Recepção e o Consultório precisam
dos mesmos, e **nenhum módulo conhece os outros**.

Tela nova que marca ponto no corpo, emite documento ou escolhe paciente **usa esses
componentes**; não reescreva.

Os serviços que este módulo consome: `ConsultorioService`, `AvaliacaoClinicaService`,
`MedidaClinicaService`, `ProblemaPacienteService`, `ProntuarioService`,
`DocumentoClinicoService`, `PrescricaoService`, `PrescricaoInternaService`,
`ChecagemPrescricaoService`, `AssinaturaDePrescricaoService`, `ElegibilidadeService`,
`IndicadoresService`, `AgendaService`.

---

## A sidebar

Oito itens visíveis, agrupados por **tema** (não por módulo — ver parcela 7):

| Grupo | Item | Chave | Exige |
|---|---|---|---|
| GESTÃO | Meu dia | `ChaveMeuDia` | `VerAgenda` |
| GESTÃO | Sessões sem evolução | `ChaveRegistrosPendentes` | `VerProntuario` |
| GESTÃO | Minha semana | `ChaveMinhaSemana` | `VerAgenda` |
| GESTÃO | Sala de infusão | `ChaveSalaInfusao` | `ChecarPrescricao` |
| PACIENTE | Pacientes | `ChavePacientesDaClinica` | `VerProntuario` |
| PACIENTE | Prescrições | `ChavePrescricoes` | `VerProntuario` |
| PACIENTE | Prescrição de infusão | `ChavePrescricaoInfusao` | `Prescrever` |
| INTELIGÊNCIA | Meus números | `ChaveMeusNumeros` | `VerAgenda` |

E seis chaves **navegáveis fora do menu** (`Oculto = true`): `ChavePaciente`,
`ChaveAtendimento`, `ChaveProntuario`, `ChaveEvolucaoDor`, `ChaveMedidas`,
`ChaveAvaliacoes` — todas caem na `PacienteWorkspaceView`, cada uma na sua aba
(`ModuloClinico.AbaDe`).

> ⚠️ **Chave sem item declarado em `Itens` não abre NADA.** O shell navega procurando a
> chave e, sem achar, retorna `false` **em silêncio** — sem erro, sem log. Foi assim que a
> parcela 37 quebrou "Atender", os atalhos da carteira e o painel da direção de uma vez, com
> as três redes locais e os testes todos verdes. Hoje a **checagem 19** do
> `verificar-suite.py` cobre isso, e é autotestada contra aquela regressão.

**O módulo não declara `Inicial`**, e isso é deliberado: o `Clinica.Clinico.exe` carrega um
módulo só, então o primeiro item já é a abertura. Marcá-lo faria o Consultório vencer o
painel da direção dentro do Gerente — o defeito que a parcela 22 corrigiu.

---

## Duas coisas que se repetem aqui e vale conhecer antes de mexer

**`PacienteEmFoco` é singleton.** No consultório o paciente é **contexto**, não parâmetro de
tela: quem atende escolhe uma vez e passa vinte minutos entre quatro telas sobre a mesma
pessoa. Toda tela clínica abre no paciente em foco e trata a busca como atalho.

**Releitura periódica mora na View, não no ViewModel.** `MeuDiaView` e `SalaInfusaoView`
ligam e desligam um `DispatcherTimer` no `Loaded`/`Unloaded`. Ligar no VM manteria vivo um
timer por tela já trocada — o shell constrói uma nova a cada navegação. A releitura é
**silenciosa** (não acende "Carregando", não escreve erro na tela, mas registra no log):
quem está com um paciente na cadeira não pode ver a lista piscar em branco a cada minuto.

---

## Registro das parcelas

### Parcela 36 — o Consultório nasce
O quinto executável. Responde a pergunta que nenhuma tela respondia: **o que eu atendi e
ainda não escrevi**. Trouxe `ConsultorioService`, as cinco escalas clínicas por
especialidade (PHQ-9, GAD-7, Oswestry, Katz, FINDRISC) e o `PacienteEmFoco`. Na 2ª rodada,
deixou de ser uma ilha: o que ele grava passou a ser lido pelo Gerente e pela Recepção.

### Parcela 37 — o prontuário que ele precisava
Anexos, busca no prontuário, medidas seriadas e lista de problemas. Três das lacunas não
pediam capacidade nova — pediam **porta no módulo certo**. Teve quatro rodadas de UI, e a
última é a lição cara: **lista → tela do item**, nunca mestre-detalhe espremido numa tela
só. Foi aqui que nasceram a `PacienteWorkspaceView` e o `ItemMenuModulo.Oculto`.

### Parcela 38 — o quadro e a chamada
"Meu dia" virou kanban de cinco colunas, as mesmas da fila do balcão, e ganhou o botão
"Chamar próximo" (`Agendamento.ChamadoEm`). A sincronização entre as duas telas é o
**banco** — nem fila, nem evento, nem um módulo conhecendo o outro. 2ª rodada: as quatro
faixas empilhadas que comiam metade da tela viraram uma linha de texto e um botão.

### Parcela 39 — a sidebar tinha três itens
Prescrições, Minha semana e Meus números. Nenhuma era capacidade nova: as três existiam e a
única porta estava no módulo de quem não as usa. A lição ficou registrada no `CLAUDE.md`:
ao procurar chamador em produção, **conte também quantos itens de menu o módulo tem**.

### Parcela 41 — botão aceso que não fazia nada
Os quatro botões de emitir voltavam de um `if (_pacienteId == 0) return;` **calados**,
enquanto o `IsEnabled` só olhava permissão. Virou regra geral — *guarda que volta em
silêncio é botão que não faz nada* — e a **checagem 21** passou a cobrar as duas metades.

### Parcela 42 — prescrição de infusão, checagem de enfermagem e ICP-Brasil
*(PR [#90](https://github.com/lucaszaous-creator/Clinica/pull/90), mesclado em `6c3e216`.)*

A clínica faz infusão, e a folha que ela usa não é a receita: é prescrição de vários itens
*"destinada ao próprio consultório — o paciente não vai apresentar lá fora"*. O que faltava
era a **checagem da técnica**: ✓ com o horário quando administrou, **"rodela"** (horário
circulado) com justificativa quando não.

**O que entrou na parcela — e onde cada peça mora HOJE**

⚠️ Na parcela 48 a sala de infusão, a folha de execução e o seletor de certificado
**subiram para o shell** (`Clinica.Desktop.Shell/Componentes`), para a Recepção alcançar a
checagem sem instalar o app do médico — os dois módulos publicam a mesma chave
(`ChavesSuite.SalaInfusao`). Esta tabela reflete o endereço atual:

| Arquivo | Mora em | Papel |
|---|---|---|
| `PrescricaoInfusaoViewModel` + `PrescricaoInfusaoView` | **este módulo** | As folhas do paciente; abre no paciente em foco |
| `PrescricaoInternaEdicaoViewModel` + `PrescricaoInternaWindow` | **este módulo** | Escrever e assinar |
| `SalaInfusaoViewModel` + `SalaInfusaoView` | **shell** | A fila do dia, releitura de 1 min; só folhas **assinadas**; imprime a via e acha a folha pelo código impresso (parcela 61) |
| `FolhaExecucaoViewModel` + `FolhaExecucaoWindow` | **shell** | Checar item a item, retificar, suspender (quem prescreve), imprimir e encerrar |
| `EscolherCertificadoViewModel` + `EscolherCertificadoWindow` | **shell** | Escolher o e-CPF ICP-Brasil |

**Permissões novas**: `Prescrever` e `ChecarPrescricao` — separadas de propósito, porque é
serem duas pessoas que dá valor à conferência. E o perfil **`Enfermagem`**, sem o qual a
checagem seria assinatura de ninguém.

**Decisões que valem para o próximo que mexer aqui**

- A **hora é informada, nunca o relógio** — e hora futura é recusada. É a única regra do
  projeto com relógio injetado (`ChecagemPrescricaoService`), porque regra de segurança que
  não dá para testar apodrece sem ninguém notar.
- **Uma assinatura eletrônica só, a de quem prescreve.** A enfermeira confere e assina na
  via impressa — por isso a Prescrição sai com as colunas de checagem em branco. Ver a
  revisão de 06/08 abaixo.
- **A reimpressão devolve os bytes guardados**, nunca um PDF novo — a assinatura cobre uma
  faixa de bytes do arquivo.
- O **CPF sai de dentro do certificado** e é comparado com `Profissional.Cpf`. Sem isso, a
  assinatura provaria só que alguém com algum token assinou.

**Como a sessão correu, e o que ela custou de retrabalho**

1. A viabilidade foi provada **antes** de escrever a feature: QuestPDF → PdfSharp → PKCS#7,
   assinatura conferindo e 1 bit trocado sendo pego, tudo em Linux.
2. Descobriu-se ali que o PdfSharp **não faz atualização incremental** — duas assinaturas no
   mesmo PDF não existem. Isso levou ao desenho de duas folhas encadeadas pelo hash, que a
   revisão de 06/08 tornou desnecessário: com a enfermagem assinando no papel, sobra uma
   assinatura eletrônica só e a restrição deixa de existir.
3. Dois testes de conteúdo tiveram de ser reescritos: o QuestPDF embute fontes com
   subconjunto **CID**, então o "texto" dentro do PDF são IDs de glifo e procurar a string
   ali daria um teste que passa ou falha por acidente. Passaram a testar a **decisão**
   (`FraseDoNivel`, `AlergiasParaAFolhaAsync`) em vez do desenho.
4. Um parâmetro (URL da ACT) quase nasceu **sem tela para configurá-lo** — o defeito
   recorrente do projeto ao contrário. Ganhou campo em Configurações → Operação.

**Verificação**: 1146 testes (50 novos), `compilar-sombra` e `verificar-suite` verdes,
migration puramente aditiva, CI Windows verde (é ele que compila os XAML de verdade).

**O que ficou para a clínica providenciar**: certificado ICP-Brasil para quem prescreve, o
CPF preenchido em Equipe, e — se quiser data provada em vez de declarada — uma ACT
contratada.

### Parcela 42, revisão de 06/08 — a enfermagem assina no papel

O cliente corrigiu o fluxo depois de ver o desenho: *"apenas o médico vai prescrever e
assinar ali na hora e após impresso que a enfermeira irá verificar e assinar"*. Duas
assinaturas eletrônicas eram cerimônia a mais para a mesma garantia — e obrigavam a clínica
a comprar um e-CPF para a técnica.

**O que mudou**

- `ChecagemPrescricaoService.EncerrarAsync` não recebe mais `AssinaturaDocumento`: encerrar
  fecha a folha, não assina. `AssinaturaDePrescricaoService.EncerrarExecucaoAsync` saiu.
- A **Prescrição impressa** ganhou as colunas em branco (`Feito às`, `Visto`) e três linhas
  para o motivo do não realizado. É nela que a enfermeira escreve e assina.
- O **Registro de execução** deixou de ser documento assinado: é o espelho eletrônico do
  que foi checado, montado na hora (muda a cada item), com o rodapé dizendo que a autoria
  está no papel.
- `FolhaPrescricao` (enum novo) substituiu `PapelAssinatura` como seletor de "qual folha" —
  reusar "Executante" para isso passou a mentir sobre o que o parâmetro escolhe.
- `PrescricaoInterna.AssinaturaDoExecutante` saiu: seria sempre nula, e ler nulo sugere
  feature que não existe. O valor `PapelAssinatura.Executante` fica no enum (a coluna é
  texto, e tirá-lo pediria migration destrutiva para não ganhar nada) documentado como não
  gravado.

**O que NÃO mudou, e é o ponto**: a técnica continua registrando ✓ / rodela com hora e
justificativa na tela. Sem isso o circuito da reação alérgica virando alergia no prontuário
morreria — e é ele que faz a próxima prescrição acender o alerta.

**Nenhuma migration.** O esquema não mudou: só parou de escrever numa linha.

Dois testes novos guardam a decisão: `Encerrar_NAO_cria_segunda_assinatura_eletronica` e
`Registro_de_execucao_sai_sem_assinatura_eletronica`. Se um dia voltar um segundo
certificado, eles caem.

---

## Antes de todo push que mexa aqui

```bash
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
python3 tools/compilar-sombra.py     # o C# das telas WPF
python3 tools/verificar-suite.py     # XAML, chaves do design system, migration destrutiva
```

As três não cobrem o **compilador de marcação** (`MC*`) nem o empacotamento: isso é o CI
Windows, que roda em cada PR para a `main`. Tela nova é exatamente o caso em que vale
esperar o CI antes de mesclar.
