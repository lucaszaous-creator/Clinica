# A guia nasce quando o horário entra no sistema

**Decisão da direção (ago/2026):** *"O confirmar presença não pode gerar guia. A guia
precisa nascer no momento em que a secretária coloca o atendimento no sistema — seja
avulso, seja pela agenda."* A clínica confia na carteira dela e não cobra no-show; o que
ela não aceita é **guia duplicada** nem **sessão sem guia**.

Este documento é a arquitetura. Nada aqui está implementado ainda; a ordem de entrega
está no §8.

---

## 1. Por que a duplicidade existe hoje (o mecanismo exato)

`AgendaService.ConfirmarPresencaAsync` faz DUAS gravações separadas:

1. `AtendimentoService.LancarAsync` grava o `Atendimento` + os `CodigoFaturamento`
   (primeiro `SaveChanges`);
2. o carimbo `ag.Status = Realizado; ag.AtendimentoId = …` vai num **segundo**
   `SaveChanges`.

Se o segundo falha — conflito de `xmin` entre as duas máquinas do balcão, queda de
conexão —, **as guias existem e o agendamento não sabe**: o cartão continua com
"Concluir" aceso, e o segundo clique gera outro jogo de guias. É o incidente de
12/08/2026 uma camada abaixo; a idempotência da parcela 65 não alcança porque a chave
dela é justamente o `AtendimentoId` que não chegou a ser gravado.

Remendar com transação resolveria o sintoma. A decisão da direção resolve a **causa**:
se a guia nasce quando o horário entra no sistema, **deixa de existir um segundo momento
de criação** — e o que não tem segundo momento não duplica.

## 2. O princípio

> O fato que gera a guia deixa de ser "o paciente veio" e passa a ser
> **"a secretária pôs o atendimento no sistema"**.

Consequências que a direção aceitou de antemão:

- a guia existe **antes** da sessão — e isso é o objetivo, não um efeito colateral: a
  secretária pode efetivar no portal do convênio com antecedência;
- sessão cancelada/falta fica com guia gerada e **não faturada** — a clínica não cobra
  no-show, então essa guia é suspensa automaticamente (§4.3), nunca esquecida no painel.

## 3. O desenho

### 3.1 A guia nasce no `AgendarAsync`, num grafo só

Toda porta de marcação já converge para `AgendaService.AgendarAsync` (normal, série,
encaixe, lista de espera, avulso — que desde a parcela 60 é um encaixe). Ali, com o
regime ligado (§3.5):

```
Agendamento + Atendimento + CodigoFaturamento[] + EventoAuditoria
   → UM SaveChanges (o grafo inteiro numa transação do EF)
```

O `Agendamento.Atendimento` é setado por **navegação**, então o EF insere tudo e amarra
as FKs numa transação só. Não existe estado intermediário observável: ou o horário nasce
com as guias e sabe disso, ou nada nasce. O `Numero` do atendimento
(`{ano}-{id:D6}`) precisa do Id e vai num segundo `SaveChanges` **cosmético**: se ele
falhar, nada duplica — a existência do `AtendimentoId` é o que impede recriação, e o
número é reparável.

### 3.2 `LancarAsync` se divide em CRIAÇÃO e PRESENÇA

O `LancarAsync` de hoje faz quatro coisas. Duas são da **criação** e duas são da
**presença**, e a divisão é o coração da mudança:

| Efeito | Momento certo | Por quê |
|---|---|---|
| Gerar `Atendimento` + códigos pelas regras | **Marcação** | É o pedido da direção |
| Atualizar `paciente.Categoria` | **Marcação** | Deriva das regras, viaja junto |
| Reabrir NCs do paciente ("cobre a guia AGORA") | **Presença** | O aviso só é verdade com o paciente na frente — dispará-lo numa marcação por telefone, semanas antes, cobraria a secretária por alguém que não está lá |
| Renovar a consulta do convênio | **Presença** | A consulta renova quando ACONTECE, não quando é marcada |

`AtendimentoService` ganha a separação explícita: `CriarAsync(…)` (a metade de cima, sem
`SaveChanges` próprio — quem salva é o chamador, para o grafo ir junto) e
`RegistrarPresencaAsync(atendimentoId, …)` (a metade de baixo). `LancarAsync` continua
existindo como a composição das duas — é o caminho do regime antigo e do fallback, e
some quando o último app antigo morrer.

### 3.3 `ConfirmarPresencaAsync` só carimba — e o fallback mata a duplicidade no regime antigo também

```
ConfirmarPresencaAsync(agendamentoId):
  se ag.AtendimentoId != null:            // regime novo
      ag.Status = Realizado
      atendimento.RealizadoEm = agora     // §5
      efeitos de PRESENÇA (NC + consulta) + EventoAuditoria
      → UM SaveChanges
  senão:                                  // regime antigo / janela de atualização
      criar Atendimento + códigos NO MESMO GRAFO do carimbo
      → UM SaveChanges (a correção da transação vale para o legado)
```

O fallback fica **para sempre**: são dois apps num banco só, e a janela em que um
atualizou e o outro não é o desenho (parcela 67). E ele também é a correção do item 1 da
fila — mesmo com o regime desligado, a criação e o carimbo passam a ser um único
`SaveChanges`.

De quebra, o ato que gera as guias ganha a linha de trilha que faltava (item 5 da fila):
`EventoAuditoria` no mesmo `SaveChanges` do carimbo.

### 3.4 O destino da guia segue o status do horário

Um método único (`AtendimentoService.RefletirStatusDoHorarioAsync`), chamado por
`CancelarAsync`/`MarcarFaltaAsync`/`RemarcarAsync` — as portas já são pontos únicos:

- **Cancelar / falta** → códigos `Aberto` do atendimento viram **`NaoAplicavel`**, com
  `RegistrarObservacaoPendencia("Sessão cancelada em dd/MM — falta/cancelamento")`.
  ⚠️ **Não é NC de propósito**: `LancarAsync`/presença REABRE as NCs do paciente quando
  ele volta — a guia da sessão que não aconteceu ressuscitaria como pendência fantasma
  na próxima marcação. `NaoAplicavel` já sai de `EstaPendente`, do painel e da rodada, e
  **não é valor novo de enum** (a mina da parcela 67 fica desarmada: o app antigo lê).
  Código já **Baixado** não se toca — a secretária efetivou antes e a sessão caiu; a
  tela avisa ("há guia já baixada deste horário — confira no portal") e a decisão é
  humana, como toda baixa.
- **Reabrir o horário** (remarcar uma falta/cancelamento) → códigos `NaoAplicavel` cujo
  convênio **gera guia** voltam a `Aberto` (o particular continua `NaoAplicavel`, que é
  o estado natural dele).
- **Remarcar a DATA** → `atendimento.Data` acompanha e as `DataPrevistaFaturamento` dos
  códigos `Abertos` **deslocam pelo delta de dias** (as regras derivam a prevista da
  data por deslocamentos fixos — +24h etc. —, então o delta preserva o desenho de todas,
  inclusive a inversão do BSV). Baixados não se tocam.
- **Mudar modalidade / especialidade / 1º código** → os códigos são **REGERADOS** pelas
  regras — somente quando TODOS estão `Aberto` e fora de lote; havendo baixa ou lote, a
  edição é **recusada com a explicação** (cancele e remarque, ou estorne primeiro). A
  regeneração substitui as linhas no mesmo `SaveChanges`, com `EventoAuditoria`
  escrevendo o que saiu e o que entrou.

### 3.5 A chave de ativação — por causa da janela de atualização

O risco real da virada: horário **marcado pelo app novo** (com guia) e presença
**confirmada pelo app velho** (cujo binário ainda cria no carimbo) = guia duplicada — e
o app de faturamento pode ficar dias aberto sem atualizar.

Por isso o regime novo nasce atrás de **`ParametrosService` → chave
"GuiaNoAgendamento", DESLIGADA por padrão**, com a caixinha em Configurações → Operação
dizendo com todas as letras: *"ligue depois de atualizar todas as máquinas"*. Com a
chave desligada, tudo se comporta como hoje **mais** a transação única do §3.3 — ou
seja, a duplicidade morre no dia 1, e a mudança de fluxo de trabalho acontece quando a
clínica decidir (e treinar a secretária).

### 3.6 O lançamento avulso — a porta PRINCIPAL de hoje, e o caso mais simples do desenho

⚠️ Enquanto a clínica usa o Amplimed como agenda, quase tudo entra pelo **Novo
atendimento** — a agenda é o caso geral do desenho, mas o avulso é o caso REAL do dia a
dia, e a arquitetura o trata de frente. Ele é a esteira inteira **colapsada num gesto**:
o horário entra no sistema já realizado (a secretária lança depois do trâmite, com a
sessão feita ou acontecendo).

Hoje, o clique dela dispara uma CORRENTE de três serviços e cinco `SaveChanges`:

```
AgendarAsync (encaixe)            → SaveChanges
RegistrarChegadaAsync (check-in)  → SaveChanges
ConfirmarPresencaAsync
  └ LancarAsync (guias)           → SaveChanges
  └ número do atendimento         → SaveChanges
  └ carimbo do agendamento        → SaveChanges
```

Cada vão entre eles é um meio-estado possível. O incidente de 12/08 (três encaixes com
chegada carimbada e `AtendimentoId` nulo) morava num desses vãos; a guia duplicada mora
no último — e a guarda de reaproveitamento (`ag.AtendimentoId is { }`) só funciona se o
carimbo chegou ao banco, que é exatamente o que falha.

No desenho novo, o MESMO clique monta **um grafo** — `Agendamento` (encaixe na hora
real, chegada carimbada) + `Atendimento` (com `RealizadoEm` carimbado: a sessão
aconteceu) + códigos + trilha — e grava em **um `SaveChanges`**. Os efeitos de PRESENÇA
(§3.2 — NC reaberta, consulta renovada) disparam JUNTO, porque no avulso o paciente
veio: a divisão criação × presença é por *"a sessão está sendo registrada como
realizada?"*, **não pela porta**. Ou existe tudo, ou não existe nada e o erro aparece na
tela — não sobra meio-estado para o segundo clique transformar em duplicata.

Para a secretária, **nada muda de aparência**: a mesma tela, os três passos numerados, a
prévia ("2 guias · a 2ª libera 09/08"), a conferência LANÇADOS HOJE. A janela de
fechamento (pacote/insumo/caixa) continua sendo o passo SEGUINTE e opcional (parcela
65), e cancelá-la não desfaz nada do que importa. E o avulso é **igual dos dois lados da
chave** (§3.5): a chave governa a guia de horário FUTURO; o avulso é sempre presente —
por isso a Fase 1 já entrega, para o fluxo atual da clínica, o comportamento final.

### 3.7 UMA porta de entrada: o Novo atendimento marca E lança; a agenda MOSTRA (decisão da direção)

A direção fechou a unificação pelo outro lado do que a primeira versão desta seção
propunha — e melhor: em vez de dois formulários com uma ponte, **UM formulário com a
pergunta "QUANDO?"**. O Novo atendimento passa a ser o único lugar onde um atendimento
NOVO entra no sistema, nos dois modos; a agenda passa a ser **visualização e gestão do
que já existe**.

```
[2] QUANDO
 (•) O paciente está aqui — lançar agora            ← o avulso de hoje (§3.6)
 ( ) Marcar dia e horário                           ← o agendamento
      Dia [22/08]  Hora [14:30]  Duração [30 min]
      Profissional [— sem profissional definido —▾]   Sala [Sala 2 ▾]
      ⚠ Sem profissional, a sessão não aparece no quadro do médico
        e fica fora do repasse — defina quando souber quem atende.
      ⚠ 14:30 já tem sessão da Dra. Paula (choque) — será um encaixe.
      ⚠ Agenda fechada neste período: Férias da Dra. Paula.
      [ ] Repetir semanalmente — [10] sessões (série)
```

Modalidade, especialidade, consulta, BSV (termo), elegibilidade e a prévia das guias já
moram na tela — valem para os DOIS modos. No modo "marcar", o Salvar grava o grafo do
§3.1 (horário + atendimento + guias, chave ligada) e **pergunta se quer imprimir o
comprovante de agendamento** (`AgendaPdfService.ComprovanteAsync`, parcela 29 — já
existe; falta só a pergunta).

O que muda de lugar, e o que NÃO muda:

- **O vão livre da agenda continua clicável** — mas passa a abrir o Novo atendimento
  pré-preenchido (dia, hora, profissional da coluna), em vez do formulário antigo. A
  agenda "que mostra" não pode perder o gesto de apontar o horário com o dedo.
- **A SÉRIE vem junto** para o modo "marcar" (`AgendarSerieAsync`, com o resultado
  pulado-e-dito de sempre) — deixá-la para trás faria "marcar" morar em dois lugares de
  novo.
- **Editar o horário EXISTENTE continua na janela do horário da agenda** (remarcar,
  cancelar, falta, comprovante, reabrir). Marcar NOVO e mexer no EXISTENTE são atos
  diferentes; uma definição por ATO. O formulário antigo da agenda se aposenta só da
  metade "novo".
- **Choque e bloqueio criticam ANTES do clique com a MESMA regra que recusa no serviço**
  (`AgendaService` ganha a leitura de crítica reutilizável) — a regra do número da guia,
  aplicada ao horário.
- ⚠️ **"Profissional aleatório" NÃO é sorteio.** Deixar sem profissional é permitido e
  avisado (o aviso acima); o sistema **nunca escolhe sozinho** — profissional define
  repasse (dinheiro) e o quadro de quem atende, e um sorteio pagaria a pessoa errada. O
  horário órfão cai na coluna "Sem profissional" da agenda, onde é adotado depois.

E o aviso de duplicidade vira o aviso da **CAPA**, em dois momentos (parcela 51 — a
tela critica a cada escolha, não só no confirmar):

1. **Ao escolher o paciente**: a região de avisos mostra "já tem atendimento HOJE" com a
   capa (número, modalidade, quem lançou, hora) e **o status de cada guia** — baixada,
   aberta, N.A. É o que mata o gesto de 12/08: a secretária vê que a guia já existe e já
   está com a faturista ANTES de pensar em lançar de novo.
2. **No clique de Lançar**: a pergunta da parcela 65 deixa de ser sim/não cego e mostra a
   mesma capa. Continua **pergunta, nunca recusa** — sessão de manhã + consulta à tarde é
   caso legítimo.

⚠️ A leitura da capa mora num **ponto único**
(`AtendimentoService.CapaDoDiaAsync(pacienteId, dia)` — capa, modalidade, autor, guias
com status), consumido pelas três portas: Novo atendimento, formulário da agenda (aviso
quando a data escolhida já tem sessão do paciente) e Fila. Escrita na tela, a segunda
porta esqueceria — alerta que existe numa porta só é o defeito de novo.

As travas contra duplicidade são CAMADAS, e nenhuma sozinha basta: atomicidade (§3.6 —
mata a acidental), aviso da capa (mata a "por não ver"), pergunta informada (mata a "por
descuido" sem travar o legítimo), choque de horário da agenda (mata a do agendar).

## 4. O que NÃO muda — e por quê já estava pronto

1. **O painel de pendências e a rodada bloqueante**: `CodigoFaturamento.EstaPendente`
   **já** exige `DataPrevistaFaturamento <= hoje`. Guia de sessão futura não é pendência
   por construção — ela aparece na **Consulta de guias** (onde a secretária a efetiva
   cedo, que é o objetivo) e só entra no painel quando a data chega.
2. **As quatro portas da baixa** e a `RegraNumeroGuia`: intocadas. Baixa antecipada
   passa de acidente a feature.
3. **Os quatro fatos do Finalizar** (pacote, insumo, caixa): continuam no Concluir da
   Fila. Só a GUIA muda de momento.
4. **`Avulso_e_agendado_produzem_os_mesmos_fatos`**: continua verde — as duas portas
   continuam sendo a mesma esteira, só que a guia nasce um passo antes nas duas.

## 5. O que muda de SIGNIFICADO: `Atendimento`

Hoje, `Atendimento` significa "a sessão aconteceu". No regime novo significa "a sessão
está registrada". Todo leitor que quis dizer **aconteceu** precisa de âncora nova:

- **Migration aditiva**: `Atendimento.RealizadoEm (timestamp without time zone, null)` +
  backfill `UPDATE … SET RealizadoEm = LancadoEm` (toda linha existente é sessão que
  aconteceu — a lição do `defaultValue` da parcela 60 aplicada a dado).
- `ConfirmarPresencaAsync` carimba `RealizadoEm`; cancelar/falta o anula.
- **Inventário de leitores a reancorar** (grep por `Atendimentos` no repositório, cada
  um conferido como a parcela 69 fez com os leitores de agendamento):
  produtividade/BI, `RentabilidadeConvenioService` (período do atendimento),
  `OrigemPacientesService` ("estreou" = primeiro atendimento **realizado**),
  `RetencaoPacienteService` ("cancelado não é visita"), custo por sessão,
  `ConsultorioService` (pendência de evolução é de sessão realizada — já filtra por
  `StatusAgendamento.Realizado`, confere), elegibilidade/cota.
  O que **não** se reancora: `CodigosDoPacienteNoMesAsync` no contexto das regras —
  marcar 10 sessões do mês deve enxergar as anteriores ao gerar a próxima, e isso é
  desejado.

## 6. Riscos, e como cada um morre

| Risco | Resposta |
|---|---|
| Guia duplicada por falha entre criação e carimbo | Deixa de existir segundo momento de criação (§3.1); no legado, grafo único (§3.3) |
| App velho confirmando horário do app novo | Chave de ativação (§3.5) |
| Guia de sessão futura alarmando o faturista | `EstaPendente` já filtra por data (§4.1) |
| Sessão cancelada virando pendência eterna | Suspensão automática em `NaoAplicavel` com motivo (§3.4) |
| NC fantasma reaberta pela próxima marcação | Cancelamento NÃO usa NC (§3.4) |
| Valor novo de enum quebrando o app velho | Nenhum valor novo — `NaoAplicavel` já existe (§3.4) |
| Indicadores contando sessão que não houve | `RealizadoEm` + inventário de leitores (§5) |
| Guia baixada de sessão que caiu | Não se toca; aviso na tela; decisão humana (§3.4) |

## 7. Plano de testes

- **Atomicidade sem Postgres**: decorator de `IClinicaRepositorio` que CONTA os
  `SalvarAsync` — `Marcar_gera_horario_atendimento_e_guias_em_UM_SaveChanges` e
  `Fallback_legado_cria_no_carimbo_em_UM_SaveChanges`. É a forma testável da transação;
  o teste contra Postgres real (xmin) continua na meta do CI, mas sai do caminho
  crítico: conflito agora significa "releia — o `AtendimentoId` já está lá".
- `Confirmar_presenca_de_horario_com_guia_so_carimba_e_nao_cria_nada`.
- `Cancelar_suspende_as_guias_abertas` / `Reabrir_devolve` / `Falta_idem` /
  `Particular_continua_NaoAplicavel_ao_reabrir`.
- `Remarcar_data_desloca_as_previstas_dos_abertos_e_nao_toca_baixado`.
- `Mudar_modalidade_regenera_quando_intocada_e_recusa_quando_baixada_ou_em_lote`.
- `NC_do_paciente_reabre_na_PRESENCA_e_nao_na_marcacao`.
- `Consulta_renova_na_PRESENCA_e_nao_na_marcacao`.
- `Guia_de_sessao_futura_nao_e_pendencia_nem_entra_na_rodada`.
- `Indicador_X_nao_conta_sessao_nao_realizada` (um por leitor reancorado do §5).
- `Avulso_e_agendado_produzem_os_mesmos_fatos` — continua, nos DOIS regimes da chave.

## 8. Ordem de entrega

1. **Fase 1 — a transação (vale nos dois regimes, ganha imediato):** o Novo atendimento
   (§3.6) e o Concluir da Fila viram operações ATÔMICAS — encaixe + chegada +
   atendimento + guias + carimbo num grafo/`SaveChanges` único, com trilha. É a porta
   que a clínica usa HOJE, o dia inteiro, enquanto a agenda ainda mora no Amplimed:
   mata a duplicidade e o encaixe fantasma sem mudar um clique do fluxo de trabalho.
2. **Fase 2 — o regime novo atrás da chave:** §3.1, §3.2, §3.4, §3.5, migration do §5.
3. **Fase 3 — os leitores reancorados** (§5), um a um, cada qual com teste.
4. **Fase 4 — o resto da nota 9 da Recepção** (fila da parcela 69): espera média que
   conta falta, bloqueio que não vê a sessão que invade (a consulta de origem corta por
   `DataHora` — o `ColideCom` de baixo nunca vê a sessão das 11h30), profissional
   desativado sumindo da grade, reconferência de elegibilidade ao trocar a data.
