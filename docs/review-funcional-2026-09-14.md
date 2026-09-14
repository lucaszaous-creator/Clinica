# Review funcional — Clínica — 14/09/2026

Repositório: `lucaszaous-creator/Clinica`. Base revisada: `main`, commit `61d5cd43fd2dcf32eb45962f69048ae99a16790f` (11/09/2026), idêntico ao checkout local e ao `origin/main` consultado.

## Parecer

Há falhas e fragilidades de integração compatíveis com o relato de atendimento pendente e necessidade aparente de lançar novamente. A guia depende da conclusão administrativa do atendimento; salvar a evolução, encerrar a sala e concluir a sessão são operações diferentes. A implementação mantém caminhos em que apenas parte disso acontece.

Lançar novamente não é a correção geral: pode criar outro atendimento, outras guias e outra pendência de prontuário. O caminho correto depende de identificar o horário, o atendimento e a evolução existentes.

Este documento contém inventário funcional dos cinco módulos e revisão aprofundada do circuito agenda → consultório → prontuário → guias → fechamento. Não representa homologação de todas as telas, auditoria integral de segurança ou reprodução de um caso da produção. Não foram consultados dados de pacientes, configuração do banco de produção nem versões instaladas nas estações. Nenhum código funcional foi alterado.

## Cinco módulos

| Módulo | Funcionalidades identificadas no código | Integração e limite da revisão |
|---|---|---|
| Recepção | Cadastro e ficha do paciente; agenda por profissional/sala, dia/semana, encaixes, remarcação, chegada, lista de espera; novo atendimento e guias; lançamentos e estornos; conciliação da agenda; retornos a marcar; particular e pacotes; documentos e termos; acessos à enfermagem e infusão. | Principal porta administrativa. Tem ações que apenas agendam e ações que já registram presença e geram guias. Essa diferença altera o comportamento do consultório. |
| Consultório | Meu dia/semana; atender, iniciar e finalizar; evolução e histórico, SOAP, EVA e mapa corporal; campos personalizados; avaliações e medidas; prontuários pendentes; prescrições, documentos, exames e anexos; enfermagem e execução de infusão; indicadores do profissional. | Salvar evolução não conclui a sessão. Finalizar encadeia gravações separadas. A detecção de prontuário pendente depende dos vínculos e do filtro de profissional. |
| Faturamento | Regras por convênio/modalidade; primeiro e segundo códigos, datas previstas, painel de pendências e não conformidades; baixa e consulta de guias; autorizações; lotes, XML/PDF TISS, retorno, glosa e recurso. | O painel cobra códigos que existem. Atendimento não lançado pode ficar sem código e, portanto, sem pendência nesse painel. Gerar código interno não é obter autorização/numero da guia no portal da operadora. |
| Financeiro | Caixa e fechamento; contas a pagar/receber; fluxo e resultado; recebíveis de cartão, inadimplência, conciliação de guias e extrato bancário; pacotes; estoque; repasses; taxas, impostos e categorias. | Guia não equivale a dinheiro recebido. Fechamento tem efeitos separados e pode terminar parcialmente. Revistos especialmente os efeitos ligados à sessão; demais funções inventariadas, sem homologação de ponta a ponta. |
| Gerente Geral | Agrega módulos e painéis; BI, metas, produção, resultado, rentabilidade, preços; campanhas/recall, retenção e origem; auditoria, acessos, guarda e documentos; configurações e importação. | Indicadores dependem do estado administrativo das sessões. Duplicatas ou sessões sem vínculo afetam a qualidade dos números. Não foi validada a configuração real dos perfis da clínica. |

Fontes do inventário: `src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs`, `src/Clinica.Modulo.Clinico/Modulo/ModuloClinico.cs`, `src/Clinica.Modulo.Financeiro/Modulo/ModuloFinanceiro.cs`, `src/Clinica.Modulo.Gerente/Modulo/ModuloGerente.cs`, serviços em `src/Clinica.Application/Servicos` e `docs/features-por-modulo.md`. A documentação tem comentários históricos contraditórios; os comportamentos abaixo foram conferidos nas implementações, não inferidos apenas dos comentários.

## Achados prioritários

### 1. [P1] Finalização parcial perde a possibilidade de repetir o próprio comando

**Cenário:** médico inicia, escreve e finaliza; a evolução salva e `EncerrarAtendimentoAsync` grava `FimAtendimentoEm`. Depois disso, a escolha do convênio é cancelada ou a etapa de geração/confirmação falha.

**Resultado:** o prontuário pode estar salvo e o fim registrado, mas o agendamento permanece `Agendado` e pode não ter atendimento/guias. `DescreverSessao` define `EmAtendimento = false` quando existe fim; `FinalizarSessaoAsync` exige `EmAtendimento`. Assim, repetir o mesmo botão não retoma o passo faltante. Há saída pela recepção ou reabertura, mas não uma retomada direta e segura da conclusão no comando original.

**Evidência:** `src/Clinica.Modulo.Clinico/ViewModels/PacienteWorkspaceViewModel.cs:461-464,593-596,650-703,737-740`; `src/Clinica.Application/Servicos/AgendaService.cs:791-815`.

**Correção recomendada:** conclusão retomável pelo mesmo agendamento; permitir completar a etapa pendente sem recriar a evolução, reabrir artificialmente a sessão ou gerar outro atendimento. Tornar “encerrado, falta concluir/gerar guia” um estado operacional explícito e persistente. Preservar o registro clínico já salvo.

### 2. [P1] Lançar pelo balcão pode concluir a sessão antes de o médico atender

**Cenário:** a secretária usa “Lançar e gerar as guias” sobre um horário existente, ou cria lançamento avulso, antes da consulta.

**Resultado:** `LancarNoHorarioAsync` e `LancarAvulsoAsync` chamam `ConfirmarNucleoAsync`, que marca `Realizado` e preenche `RealizadoEm`. O consultório entende “Sessão concluída”; iniciar/finalizar exige `Agendado`. O médico ainda pode escrever o prontuário, mas o fluxo normal de iniciar e encerrar já foi invalidado pelo lançamento administrativo.

**Evidência:** `src/Clinica.Application/Servicos/AgendaService.cs:1100-1140,1197-1282,1046-1050`; `src/Clinica.Modulo.Clinico/ViewModels/PacienteWorkspaceViewModel.cs:461-472`; `src/Clinica.Modulo.Clinico/ViewModels/MeuDiaViewModel.cs:692-697`. O teste `FluxoAtenderTests.Concluir_antes_de_encerrar_deixa_o_encerramento_impossivel` explicita a recusa dessa ordem.

**Correção recomendada:** separar lançamento/guia antecipada de realização clínica e tornar clara a escolha na recepção. Já existe o regime “guia no agendamento”, que cria atendimento/guias mantendo o horário aberto; ele deve ser considerado no desenho, sem ativá-lo indiscriminadamente na produção. Não modificar datas nem declarações de realização para apenas gerar guias.

### 3. [P2] Evolução de quem cobriu outro profissional pode aparecer como pendente

**Cenário:** horário atribuído ao profissional A, atendimento escrito pelo profissional B, sem mudar a atribuição da agenda.

**Resultado:** a evolução é gravada com o profissional logado, B. A lista de A busca as evoluções filtrando `ProfissionalId == A` ou nulo antes de verificar o vínculo com o horário. Mesmo que a evolução de B esteja vinculada ao agendamento certo, ela não participa da consulta; a sessão realizada aparece sem evolução para A. Isso pode estimular nova escrita ou lançamento desnecessário.

**Evidência:** `src/Clinica.Desktop.Shell/Componentes/FolhaDaSessaoViewModel.cs:84-87,673-675`; `src/Clinica.Application/Servicos/ConsultorioService.cs:165-179,303-309`; `src/Clinica.Infrastructure/ClinicaRepositorio.cs:2963-2972`.

**Correção recomendada:** verificar existência de evolução pelo identificador da sessão independentemente do autor; preservar a autoria e as permissões de leitura do conteúdo separadamente. Testar cobertura/substituição de profissional.

### 4. [P2] Sessão encerrada sem guias fica fora dos dois radares principais

**Cenário:** a finalização parcial do achado 1 atravessa a virada do dia, com status ainda `Agendado` e sem código gerado.

**Resultado:** `RegistrosPendentesAsync` só cobra horários `Realizado`; o faturamento só busca códigos existentes. A conciliação alcança horários abertos vencidos, porém sua carência padrão é de dois dias e o corte é exclusivo. Existe recuperação, mas não um alerta imediato e específico de “médico terminou, guia não foi gerada”.

**Evidência:** `src/Clinica.Application/Servicos/ConsultorioService.cs:154-179`; `src/Clinica.Application/Servicos/PendenciaService.cs:31-41`; `src/Clinica.Application/Servicos/ConciliacaoAgendaService.cs:126-163`; `src/Clinica.Infrastructure/ClinicaRepositorio.cs:2227-2235`.

**Correção recomendada:** fila própria de sessões com fim registrado e conclusão/atendimento faltante, sem aguardar carência de agendamento não resolvido. Ausência de código deve poder gerar alerta administrativo.

### 5. [P2] Evolução avulsa/importada pode ser atribuída ao horário errado

**Cenário:** mesmo paciente possui dois horários no dia, um antigo parado e um atendimento real; há uma evolução sem `AgendamentoId`.

**Resultado:** `EvolucaoDoHorario` distribui evoluções avulsas pela ordem dos horários. O horário parado pode “ficar” com a evolução na leitura e a sessão realizada continuar pendente. Não há exclusão do texto; há associação inferida insuficiente. Um novo lançamento aumenta a ambiguidade.

**Evidência:** `src/Clinica.Application/Servicos/ConsultorioService.cs:303-331`; `docs/conciliacao-da-agenda.md`. A atribuição é uma heurística explícita, não prova de que houve duas consultas.

**Correção recomendada:** preferir vínculo explícito e oferecer conciliação assistida quando houver ambiguidade. A função existente “Já foi lançada — encerrar” resolve horários duplicados elegíveis; não equivale a uma ferramenta geral de edição dos vínculos de qualquer evolução.

### 6. [P2] Fechamento financeiro não tem a mesma proteção de repetição das guias

**Cenário:** duas estações abrem o fechamento da mesma sessão antes de qualquer uma terminar e confirmam caixa/insumos.

**Resultado:** o atendimento é reaproveitado, mas `ConcluirAsync` executa novamente lançamentos e movimentos de estoque. Os serviços inserem novas linhas e não possuem chave da operação de fechamento. O bloqueio `Concluida` protege uma instância da janela, não duas máquinas. Isto pode duplicar receita ou consumo, mesmo sem duplicar a guia.

**Evidência:** `src/Clinica.Application/Servicos/FechamentoSessaoService.cs:311-390`; `src/Clinica.Application/Servicos/FinanceiroService.cs:100-174`; `src/Clinica.Application/Servicos/EstoqueService.cs:304-361`; `src/Clinica.Modulo.Recepcao/ViewModels/FechamentoSessaoViewModel.cs:331-364`. O mapeamento de lançamentos não tem unicidade por operação de fechamento; a unicidade de recorrência é outra regra (`ClinicaDbContext.cs:1636-1654`).

**Correção recomendada:** identificar cada operação e cada etapa executada para permitir retomada sem repetir efeitos. Não impor unicidade simples por atendimento: pagamentos parciais e vários insumos podem ser legítimos. Cenário identificado por leitura, ainda sem execução concorrente local.

## Como interpretar “pendente”

| Situação | Significado | Caminho de regularização |
|---|---|---|
| Evolução salva, horário aberto | Salvar o texto não concluiu a sessão. | Finalizar no contexto do horário; se já encerrado, concluir o mesmo horário pela recepção. |
| Encerrado, sem atendimento/guias | O último passo não terminou. | Recepção → Agenda → Dia → Concluir no mesmo horário, corrigindo a causa apresentada. |
| Realizado, evolução pendente | Pode faltar escrita, vínculo, ou a leitura estar filtrando outro autor. | Conferir evolução e seus vínculos antes de escrever ou lançar novamente. |
| Atendimento e guias já existem, horário antigo parado | Duplicidade entre agenda e lançamento. | Recepção → Agenda → Conciliar agenda → “Já foi lançada — encerrar”, quando disponível e quando for realmente a mesma sessão. |
| Não há atendimento lançado | Falta lançamento efetivo. | Lançar pelo horário correto, preservando sua data. |
| Guia existe e está pendente de baixa | Falta etapa do faturamento, não outra consulta. | Regularizar a guia existente no faturamento. O segundo código usa sua própria data prevista. |

Na conciliação, o serviço exige o mesmo paciente e o mesmo dia. Não aplicar o vínculo entre dias diferentes nem marcar falta/cancelamento apenas para esconder uma sessão que aconteceu. Horário “Realizado” sem `AtendimentoId` aparece numa lista específica, mas não tem correção automática nessa tela.

## Proteções que já existem

- Confirmação de presença grava horário, atendimento, códigos e vínculos de evoluções do horário no mesmo commit do banco.
- Confirmação reutiliza atendimento pré-existente no regime de guia no agendamento.
- `RegistrarAtendimentoAsync` reutiliza sessão já realizada, evitando nova guia numa chamada repetida sobre o mesmo horário.
- Cadastro com convênio “a definir” é recusado na montagem do atendimento.
- Recepção procura horários abertos do paciente no dia e avisa sobre capas já existentes.
- Conciliação substitui o horário antigo pela sessão existente, sob validação, sem criar outro atendimento.
- Prontuário preserva vínculos quando uma tela os omite e mantém versão anterior na edição.

Essas proteções não resolvem todos os achados: operações isoladas corretas não garantem que a sequência usada nas duas estações chegue ao fim.

## Validação obtida e limitações

- Repositório e branch confirmados pelo GitHub e Git; checkout funcional limpo antes do relatório.
- Executado localmente `python tools/verificar-suite.py`: aprovado, 177 XAML, 9 projetos e 127 construtores de ViewModel. Emitiu cinco avisos de migrations declaradas como alterações conscientes; nenhum erro.
- CI do commit revisado: [execução 34607608423](https://github.com/lucaszaous-creator/Clinica/actions/runs/34607608423), concluída com sucesso. Logs: 2.529 testes aprovados na execução SQLite e 2.529 na execução do job PostgreSQL; zero falhas e zero ignorados em cada execução. Também passaram compilação da web, compilação-sombra do C# WPF e checagens estáticas.
- Não rodei novamente a suíte .NET local: esta estação dispõe do comando/runtime, mas não tem SDK .NET instalado. Não confundir o CI consultado com teste executado agora nesta máquina.
- Última release do Consultório consultada: `clinico-v1.2.26`, de 10/09/2026, commit `ff17f47cb869b1672db301e1abdbe60883583964`. Os arquivos `PacienteWorkspaceViewModel.cs`, `AgendaService.cs` e `ConsultorioService.cs` não diferem entre esse commit e a base revisada. Não foi verificado qual versão a clínica efetivamente instalou.
- Os testes examinados de fluxo chamam serviços; não reproduzem todos os cliques, falhas entre escopos e transições da janela. Verde no CI não constitui prova contra os achados acima.

## Ordem recomendada de correção e homologação

1. Tornar a conclusão retomável no mesmo horário e dar visibilidade imediata às sessões encerradas sem guias.
2. Separar a geração antecipada de guia da conclusão clínica nos lançamentos do balcão.
3. Corrigir existência/vínculo da evolução independentemente da autoria, mantendo o controle de acesso.
4. Conciliar registros antigos com evidência, sem geração em massa de atendimentos ou alterações em prontuários por suposição.
5. Proteger os efeitos do fechamento contra repetição e concorrência.
6. Homologar em banco de teste e duas estações: fluxo normal; salvar sem finalizar; falha após salvar; falha após encerrar; retomada; convênio não definido; guia antecipada; profissional substituto; dois horários no dia; evolução importada; virada de dia; concorrência no fechamento.

Para atribuir um incidente concreto a um destes caminhos, faltam a versão dos dois apps, a tela/rotulo exato da pendência, os identificadores de agendamento/atendimento/evolução e os eventos de auditoria correspondentes. Não é necessário relançar o paciente para fazer esse diagnóstico.
