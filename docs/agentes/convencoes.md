# Convenções e lições de implementação

Movido do `CLAUDE.md` em out/2026 (o arquivo passava de 9.700 linhas e era carregado inteiro em toda
sessão). O texto é o MESMO, sem reescrita: design system, WPF/XAML, Blazor, EF/Npgsql, checagens do
`verificar-suite`, assinatura digital, impressão, CRM e as rodadas de UI, cada uma com o defeito que a
originou. Antes de escrever uma tela, uma consulta ou uma checagem, procure aqui a lição da área.

## Índice (185 tópicos, na ordem do arquivo)

Procure a palavra da área (`grep -n "palavra" docs/agentes/convencoes.md`) e leia o tópico inteiro antes de mexer nela.

- ⛔ TELA, BARRA OU BOX NOVO SEGUE O DESIGN SYSTEM — SEMPRE
- Degradação silenciosa tem que deixar rastro.
- A sidebar da suíte é agrupada por TEMA, não por módulo
- O circuito entre os módulos é testado de PONTA A PONTA
- Cobertura é medida, não estimada.
- Duas barreiras locais contra erro de compilação WPF, e elas se dividem por linguagem.
- Tamanho de tela tem TETO e PISO, e o piso é o que corta.
- Gráfico é desenhado com os tokens, sem biblioteca
- CSV para o Excel em português
- Escolher paciente é um componente só
- CPF do paciente não se repete, e a recusa mora na ESCRITA
- GUIA NÃO É ATENDIMENTO — e o sistema materializava a pendência como HORÁRIO
- `EstadoDaTela` com `Visibility` AMARRADA: o binding morre e o vazio vaza pela tela
- `SharedSizeGroup` sem `Grid.IsSharedSizeScope` não alinha NADA
- A AGENDA era uma pilha de cartões; agenda é LINHA DO TEMPO
- `WrapPanel` que nunca dobra a linha
- Kanban se ARRASTA — e o arrasto não pode ser o único caminho
- Quem LANÇOU o atendimento e o agendamento
- A Agenda saiu do perfil Faturista
- DUAS PORTAS, UMA ESTEIRA — e a diferença entre elas não tinha sido decidida, tinha
- ATENDIMENTO QUE ENTRA NO SISTEMA JÁ GERA GUIA — a unificação da parcela 60 pendurou a
- Mover XAML entre projetos quebra o `xmlns`, e só o CI acusa
- A venda de pacote subiu para o shell
- O PARTICULAR não é um convênio: é a ausência de um
- Migration de coluna `bool` não-anulável: o EF gera `defaultValue: false`, e quase nunca
- Porta e CONTEÚDO são duas permissões, e uma sem a outra não resolve
- A rodada bloqueante é DIRIGIDA a quem fatura, e a dispensa é um BIT — não o contrário
- Coluna de formulário feita de `StackPanel` irmão desalinha quando o rótulo quebra
- Clicar no dado COPIA o dado
- A rede não varria o faturamento na checagem de CHAVE
- O WPF não formata na cultura da máquina — `StringFormat` é en-US por padrão
- Componente sem `Template` no design system não fica neutro: fica com o tema do
- `EstadoDaTela` sem `Itens` nem `Vazio` liga a sobreposição PARA SEMPRE
- Filtro na Conciliação, e por que ele é por OPERADORA e não por família
- A sidebar não estava desorganizada: estava CHEIA
- Varredura do Gerente: o enum vazava onde a checagem não olha, e a permissão tinha
- O MESMO ATO com DUAS regras: a metade sem regra é a que ninguém confere
- Trocar de paciente rápido mistura os dados de DOIS pacientes na tela de conformidade
- A mensagem de ÊXITO invisível não é uma tela: é o padrão do arquivo ao lado
- A checagem que existe para pegar o defeito passava por cima dele
- Varredura de permissão: conte os IRMÃOS, não os comandos
- Ver o número e DECIDIR sobre ele são bits diferentes
- Teto posto no elemento errado não encolhe o conteúdo: ele o DECEPA
- Três respostas para a mesma pergunta se leem como sobreposição
- Coluna elástica ao lado de coluna FIXA dá o excesso todo para a elástica
- A checagem 20 tinha um SEGUNDO ponto cego: o enum da camada de APLICAÇÃO
- O termo que o PACIENTE assina, e a pergunta que decidiu o desenho inteiro
- A porta do termo na FICHA, e as dezesseis coisas que uma revisão adversarial achou
- Checagem que cobre UM sentido de um erro simétrico está metade cega
- "Regra com exceção que ninguém vai exercer" tem prazo de validade — e ele acabou antes
- Tela do paciente: o modelo da MAQUININHA, e por que ela não é um tablet
- Um procedimento exige VÁRIOS termos, e a chave antiga apagava o primeiro em silêncio
- Rascunho revisável não é termo de fábrica
- A saída CONSCIENTE da checagem 18
- A dispensa da checagem 18 tem de ser por OPERAÇÃO, não por ARQUIVO
- `AlterColumn` nunca disparou a checagem 18 — o EF gera a forma GENÉRICA
- Autoteste que REIMPLEMENTA a lógica não testa nada
- Idempotência ancorada em campo EDITÁVEL não é idempotência
- Declaração de termo é redigida para que "Não" seja um SINAL
- Valor NOVO de enum é a única coisa que o app velho não consegue ler do banco
- Quem diz onde o DER termina é o CABEÇALHO dele, nunca o enchimento
- Decodificar prova que veio base64; não prova que veio uma ASSINATURA
- A assinatura em nuvem assinava o hash de NADA — e o defeito morava no vão entre dois
- O CMS não vem em DER: vem em BER com comprimento INDEFINIDO
- Aceitar o formato do fornecedor não é o mesmo que EMITIR o formato da norma
- BER aninhado, medido em vez de suposto
- "See the inner exception for details" é instrução para o PROGRAMADOR impressa na cara
- Data COM fuso: o defeito que o SQLite não pode pegar
- Comentário que promete o que o código não faz é pior que comentário nenhum
- A 2ª assinatura: a enfermagem sela o REGISTRO DE EXECUÇÃO, nunca a mesma folha
- Auditoria Recepção → Consultório: nenhum dos achados QUEBRAVA nada
- A premissa que ficou seis parcelas de pé porque ninguém mediu a CONCLUSÃO dela
- A navegação que só o TESTE preenche
- A folha só fica assinável no instante em que ela SOME da lista
- Varredura do módulo CLÍNICO: a barreira que falta é sempre na tela que mais custa
- O QR da folha e o "Ler QR Code" do validador são dois PROTOCOLOS — o mesmo texto
- O "Ler QR Code" do validador gov.br é um CONTRATO aberto — e entrar nele não exige
- AUDITORIA DA AGENDA ANTES DE PRODUÇÃO — dois módulos, e nenhum dos achados quebrava
- A VARREDURA DA RECEPÇÃO DEPOIS DA AGENDA — três achados, nenhum bloqueador, e todos
- A VARREDURA DO FINANCEIRO — mais limpo que a Recepção estava, e o achado grave era o
- A VARREDURA DO GERENTE E DO CONSULTÓRIO — nenhum grave, e o melhor achado estava no
- A RODADA "TODAS AS NOTAS EM 8" DO CONSULTÓRIO
- A GUIA NASCE QUANDO O ATENDIMENTO ENTRA NO SISTEMA — e a duplicidade morreu de
- O carimbo da assinatura saía no ALTO da folha: o eixo Y do `/Rect` é medido do PÉ da
- O carimbo do prescritor NUNCA apareceu — e eram DOIS defeitos somados, cada um
- Carimbo visível é dado do BANCO numa caixa de largura FIXA — logo, precisa de corte
- Conteúdo de página HERDA o estado gráfico de quem desenhou antes — e quem equilibra a
- Resolvedor de fonte que descarta o `bold` faz o título nascer sem hierarquia
- `/Size` do trailer calculado À MÃO desencontra na primeira linha nova — e quem recusa é
- A folha selada pela MÉDICA não pode mostrar a execução — e a que mostra tem de ser
- O ESCOPO da autorização em nuvem é escolhido pelo ATO, e errá-lo só aparece na SEGUNDA
- A EVOLUÇÃO DE ENFERMAGEM, e por que ela não é a `Evolucao` com outro autor
- X · Y · XY — a LEITURA que os dois compartilham, a ESCRITA que os separa
- Entidade clínica nova entra em UMA lista, e quatro leitores a consomem
- A alergia é conferida na ADMINISTRAÇÃO, não só na assinatura
- A curva de PRESSÃO vem de DUAS fontes, e cada ponto diz de onde veio
- Comentário que promete o que o código não faz, terceira ocorrência
- Propriedade `init` NÃO existe ainda dentro do construtor
- `TextoParaVisibilidade` sobre qualquer coisa que não seja `string` é `Collapsed` para
- Ao substituir uma lista por um componente genérico, liste o que a antiga MOSTRAVA
- Portão de acesso por NATUREZA tem de ser o PISO quando a natureza tem regra por ITEM
- Comando novo que faz DADO DE SAÚDE SAIR nasce com as duas barreiras
- Recusa nova de serviço precisa da metade VISÍVEL, senão ela chega depois do trabalho
- Resultado vazio de workflow é para ser investigado, nunca lido como aprovação
- "Está muito CRU!" — a parcela 72 entregou QUEM VÊ e não encostou no ATO DE ATENDER
- "Estamos anos luz atrás" — o atendimento não era um ESTADO, era um formulário
- Três seções do prontuário estavam FORA do paciente
- A alergia é ATRIBUTO DA PESSOA, não alerta do dia
- `BooleanToVisibilityConverter` sobre um `BitmapImage` é `Collapsed` para sempre
- `--` é ilegal DENTRO de comentário XML
- Método de repositório SEM chamador em teste é código que ninguém executou
- `Task.WhenAll` sobre o MESMO `IClinicaRepositorio` é um defeito, não uma otimização
- Carimbo novo da fila entra no bloco que a REMARCAÇÃO limpa
- `DispatcherTimer` ligado no ViewModel mantém viva cada tela já trocada
- Antes de aceitar uma leitura, meça o que ela TRAZ
- Campo novo de prontuário entra em TRÊS lugares no mesmo commit — e nenhum quebra o
- Teste que reenvia a ENTIDADE RASTREADA não testa versionamento
- `ToQueryString()` contra o Npgsql é a rede que faltava para tradução de consulta
- `git add -A` commita o que você não revisou — e a suíte local pode rodar contra uma
- Quem RETIFICA precisa receber tudo o que quem REGISTRA recebe
- O flag que decide a PERGUNTA não é o que decide a GRAVAÇÃO
- Voltar etapa que consome o clique sem mover o cartão é botão que não faz nada
- Estilo local sem `BasedOn` apaga o controle inteiro, e o diálogo rotulado "desisti" era o
- Relógio INJETADO com vazamento: o carimbo que a auditoria compara escapou dele
- "Árvore limpa" não prova que a árvore é a que você empurrou
- O roteiro preenchia MENOS DA METADE da consulta, e os sinais vitais paravam na
- A enfermeira prescrevia o cuidado e nada registrava que ele foi FEITO
- A conferência na escrita pega muito, e a RELEITURA do próprio diff ainda pega três
- As duas fichas eram UMA rolagem só, e a do médico escondia metade do registro atrás de
- A checagem 24 acusava `Text` LITERAL por causa de um binding no vizinho
- Os itens novos das duas fichas, e o lugar por onde um campo de prontuário some sem
- A busca no prontuário existia DUAS vezes, e as duas já tinham divergido
- Três derivadas nascidas sem leitor, no commit seguinte ao que documentou isso
- O painel das últimas sessões mostrava 4 dos 12 campos — e o que faltava era a razão da
- A FICHA DO ATENDIMENTO: o motor existia, faltava a PORTA — e faltava a ENFERMAGEM
- A FICHA DESENHADA: o mapa corporal e a curva da dor no papel
- O NOME DA TABELA SAI DO `DbSet`, NUNCA DA CLASSE — e migration aqui é escrita à mão
- A RODADA DOS PRINTS DO CONSULTÓRIO: quatro defeitos de leiaute, e três eram regras já
- FILHO ANCORADO QUE NÃO CABE É DECEPADO — e o que ficava fora da dobra era o botão de
- A ALERGIA NO TERMO DO BSV: o destaque vermelho antes da assinatura, e por que ele NÃO
- "PRESSÃO CAIU" SAIU COMO ALERGIA — e a causa era o RÓTULO de um campo
- O TERMO PELO WHATSAPP: o paciente assina no PRÓPRIO celular, na sala de espera
- NO `workers.dev` O HOSTNAME É O WORKER — e o desenho de "dois workers, duas rotas"
- O CSP ESCRITO À MÃO BLOQUEOU O PRÓPRIO POST DA PÁGINA — e o erro culpava a internet
- A CENTRAL DE DOCUMENTOS ENTERRAVA A PRÓPRIA LISTA — 700 px de cartões de emitir e "o
- A CENTRAL DE DOCUMENTOS EM DUAS ABAS — e a correção anterior tinha parado no meio
- Quando o texto APROVADO chega, o botão do rascunho vira SUBSTITUIÇÃO — a recusa "já
- Os três da fila da parcela 69 que não dependiam de ninguém: espera de falta, bloqueio
- A VALIDAÇÃO COMPLETA DA RECEPÇÃO — "tudo funcionando perfeitamente" pedido pela
- O KANBAN NOVO DA FILA — o redesenho que a cliente pediu comparando com o Smart
- A ENFERMAGEM NÃO TEM AGENDA PRÓPRIA — e cadastrá-la CERTO esvaziava as cinco telas
- "Atender" é uma palavra só, e precisa levar à seção de escrita de QUEM clicou
- A cópia campo a campo do serviço engoliu o "se necessário" por doze parcelas
- Dado gravado sem leitor, na variante que faz o COMENTÁRIO mentir
- A MESMA lacuna do lado de quem CONSULTA — e a lista da clínica é o segundo clique, não
- "MEU PACIENTE" NÃO EXISTE — e a resposta certa não era um segundo clique, era apagar o
- O CATÁLOGO ERA UMA CAIXINHA DE 240 px, e o que não cabia nele era o que ele sabe
- O compositor COFEN existia DUAS vezes, e esta seria a primeira divergência
- Tag de tipo da casa SEM PREFIXO: a terceira variante da família, e a que as duas
- "O pop out é da CONSULTA, não do catálogo" — o pedido que eu li um degrau abaixo
- Checagem que não enxerga dentro de controle da casa acusa o que está certo
- PROVA DE CAMPO — o que deixou de ser suposição em ago/2026.
- A AGENDA DEPOIS DA ABERTURA: as seis melhorias, e o que a lista pegou na escrita
- A AGENDA DO DIA VIROU LISTA, E A "FILA DO DIA" DEIXOU DE SER ITEM
- ALTURA DE LINHA FIXA NÃO ENCOLHE CONTEÚDO: ELA O DECEPA — e a agenda do dia não
- O CRONÔMETRO DO ATENDIMENTO — e o que "respeitando o design system" significa quando a
- OS BOTÕES DE FILA SAÍRAM — e a conta que eles sustentavam saiu junto, em vez de virar
- A SIDEBAR GANHOU O GRUPO ATENDIMENTO E OS GRUPOS RECOLHÍVEIS
- O INTERVALO DA RELEITURA SEGUE QUEM RECEBE, NÃO QUEM MUDA
- A CONFERÊNCIA DE CHOQUE EXISTE PARA IMPEDIR QUE SE CRIE UMA SOBREPOSIÇÃO — reconferi-la
- CORRIGIR O QUE A SESSÃO É, SEM SAIR DA AGENDA DO DIA
- A AGENDA AVISA E NÃO IMPEDE — nenhum choque recusa
- "CHEGOU NO LOCAL" VOLTOU, e só ele
- O PARTICULAR NÃO EXISTIA — e por isso o fluxo dele não tinha por onde começar
- A CENTRAL DE DOCUMENTOS NÃO ASSINAVA NEM ENVIAVA — três portas para o mesmo papel, cada
- A MESMA ESCOLHA COM DUAS REGRAS, e a do lugar mais importante era um ACIDENTE
- A PRÉVIA AFIRMAVA UMA COBRANÇA QUE O FINALIZAR NÃO IA FAZER
- "QUAL PACOTE DEBITA" ESTAVA ESCRITO TRÊS VEZES
- A VENDA DE PACOTE TINHA UMA PORTA, E ELA PERGUNTAVA O QUE A TELA JÁ SABIA
- Três armadilhas de XAML que a releitura do próprio diff pegou
- Classe de tela Blazor nova leva o PREFIXO da tela: `entrada`, `saida` e `dica` já são do comum
- Arquivo novo da prévia que uma SEMENTE usa entra também no `ValidarLayoutWindows.csproj`
- `textarea` herda a fonte da casa pela regra COMUM, não por remendo de tela
- Parâmetro `string` de componente Razor recebe expressão com `@`; sem ela recebe o TEXTO
- Semente com data relativa a "hoje" respeita a JORNADA de quem tem agenda protegida

---


- **⛔ TELA, BARRA OU BOX NOVO SEGUE O DESIGN SYSTEM — SEMPRE** (decisão da direção,
  ago/2026, ao aprovar o handoff dos painéis; a referência é `docs/design-system/` e o
  espelho de tokens conferido no CI). As cinco metades da regra:
  1. **Valor visual sai de TOKEN, nunca de número solto.** `FontSize` numérico novo, cor
     em hexadecimal nova e raio/espaçamento inventado são dívida no ato — o mockup e o
     kit foram gerados DOS tokens do repositório, e um valor fora deles é o primeiro
     ponto em que as duas coisas param de bater.
  2. **Antes de desenhar, procure o componente que já existe**: `CardKpi` (anatomia
     fixa — `CardKpi.Icone` à esquerda do rótulo NA MESMA LINHA, o `BotaoMenuKpi` ("⋯")
     à direita dela, `CardKpi.Rotulo` 13, `CardKpi.Valor` 28, `BarraDado` de 6 px só
     onde a fração é real, e `CardKpi.Delta` como ÚLTIMA linha onde há período; o
     ESTILO tem MinHeight 88 — a altura fixa é decisão POR TELA de painel, com a conta
     da pilha escrita ao lado do `Height`), `ItemBarraRotulada`, `GraficoLinha`,
     `Badge.*`, `ChipFiltro`, `EstadoDaTela`, `Card`. Desenho próprio para uma pergunta
     que um componente já responde é a segunda definição que diverge na primeira
     correção.
  3. **Iconografia é Segoe Fluent (`FonteIcones`), com glifo SEMÂNTICO consistente**: a
     mesma métrica leva o mesmo glifo em todo o sistema (E896 entrada · E898 saída ·
     E7BA vencido/falta · E73E baixado/atendido · E823 pendente/previsto · E7C3 guia —
     a tabela completa está em `docs/design-system/tokens.md` §Iconografia). E **a cor do
     ícone não é escolha do ícone: ela repete a que a tela deu ao NÚMERO** (Acento no
     neutro; semântica só onde o valor é pintado; Acento.Tint no cartão de acento).
  4. **Componente ou correção do shell é PORTADO ao design system do faturamento no
     MESMO commit** — os dois não se referenciam (o débito permanente da Fase 4), e a
     cópia que fica para trás é onde a capacidade some (parcelas 61, 75, 90).
  5. **A regra de leiaute do `README.md` continua sendo o portão** — o design system diz
     COMO cada peça se desenha; as três perguntas do README dizem SE a peça cabe ali.
- Ao adicionar um **instrumento de avaliação**: nova classe em `Domain/Avaliacoes/`
  implementando `IInstrumentoAvaliacao`, uma linha em `RegistroInstrumentos`, as
  especialidades declaradas e o fluxograma coberto em `InstrumentosAvaliacaoTests` — a
  mesma convenção do convênio, e pela mesma razão: peso errado num item produz escore que
  continua parecendo válido.
- Ao adicionar uma **medida clínica**: nova entrada em `CatalogoMedidas`, com unidade,
  piso/teto de PLAUSIBILIDADE (não de normalidade) e as faixas publicadas, e cobertura em
  `MedidasClinicasTests`. Não precisa de migration — o tipo é um código, e o que a colheita
  grava são as colunas que já existem. Faixa que depende do sexo vai em `FaixasMasculino` /
  `FaixasFeminino`; sem leitura publicada, deixe `Faixas` vazio em vez de inventar "normal".
- Ao adicionar um convênio fixo: nova classe em `Domain/Regras/`, registrar em `RegistroRegras`,
  adicionar ao enum `Convenio`, cobrir o fluxograma com testes em `RegrasFaturamentoTests`.
- Ao adicionar uma **permissão**: bit novo no fim do enum `Permissao` (nunca reaproveite um bit —
  ele é gravado como INTEIRO em bases de produção), rótulo em `PerfisAcesso.Rotular`, entrada nos
  perfis padrão que já faziam aquilo, e as **duas barreiras** em todo comando alcançado.
  `PermissoesFaturamentoTests` cobre bit único, rótulo em português e o padrão de cada perfil — é
  ele que impede uma atualização de tirar em silêncio o que a pessoa fazia ontem.
- Toda tela que escreve trata as exceções e nunca derruba o app (`DispatcherUnhandledException`
  como última rede). O **feedback tem dois canais, e a escolha não é livre**:
  - **Mensagem inline** (propriedade `Mensagem` + `MensagemEhErro` no VM, desenhada perto da
    ação) — é o padrão para formulário: validação, erro de gravação e confirmação que precisa
    ficar na tela enquanto o usuário corrige. Usado em 13 dos 18 VMs.
  - **Snackbar** (`ISnackbarService`) — só para confirmação passageira de ação que não tem
    lugar natural na tela (ex.: salvar em Configurações). Some em 4s, então nunca para erro
    que exija correção.
- **Degradação silenciosa tem que deixar rastro.** O projeto degrada de propósito em vez de
  derrubar o app (aviso que não carregou, foto que não abriu, update que falhou) — e isso é
  certo. Mas todo `catch` desses grava `LogErros.Registrar(contexto, ex)` (Desktop) ou
  `Diagnostico.Registrar` (Application/Infrastructure, com o sink ligado no `App.OnStartup`).
  O log é um `.txt` por mês em `<pasta da instalação>\logs` (fora da pasta versionada do
  Velopack, para sobreviver às atualizações), rotacionado em 2 MB e expurgado em 90 dias;
  Configurações → Clínica/prestador tem o caminho e o botão "Abrir pasta de logs".
  Só ficam mudos: o próprio logger, decodificação de quadro da webcam (30x/s) e cancelamento
  esperado. **Falha nunca pode ser exibida como sucesso** — se a checagem não rodou, a tela
  mostra um terceiro estado ("não verificado"), como o painel faz com a rodada de pendências.
- **A sidebar da suíte é agrupada por TEMA, não por módulo** (parcela 7). `ItemMenuModulo`
  tem duas coisas que só parecem uma: `Grupo` (`GrupoSidebar` — GESTÃO · PACIENTE ·
  FINANCEIRO · INTELIGÊNCIA) é **onde o item aparece**, e `ModuloNome` é **quem sabe
  construir a tela**. Antes o cabeçalho era o nome do módulo carregado, e o Gerente — que
  carrega os três — via "Recepção / Financeiro / Direção": uma sidebar que explica a
  arquitetura para quem só quer saber onde mexe no paciente. Item novo declara o `Grupo`.
  Quando um item da proposta cobre vários assuntos (é o caso de "Faturamento (TISS)"), use
  **sub-abas** dentro dele em vez de criar entradas novas — a proposta tem um item ali.
  A abertura do app é o item com `Inicial = true` (parcela 22), não o primeiro da lista;
  navegação entre módulos passa por `NavegacaoSuite` + `ChavesSuite` — chave que só um
  módulo usa continua sendo `const` do módulo dono, porque não é contrato de ninguém.
- **O circuito entre os módulos é testado de PONTA A PONTA** (`CircuitoCompletoTests`,
  parcela 33). O resto da suíte testa trechos — o fechamento sozinho, a conciliação
  sozinha, a glosa sozinha —, e cada um pode passar com o circuito partido: o que liga um
  módulo ao outro aqui **não é chamada de método, é chave estrangeira**. Se
  `LancamentoFinanceiro.CodigoFaturamentoId` deixasse de ser preenchido, nenhum teste de
  unidade falharia e a guia nunca sairia da conciliação. Os quatro circuitos cobertos são
  os da parcela 27: Recepção → Faturamento → Financeiro (a guia sai da lista por **ter
  receita**, não por alguém marcá-la), a **glosa que volta** (cancelada a receita, a guia
  reaparece sozinha), a **NC que reabre quando o paciente volta** e o **painel da direção**,
  montado com os onze serviços à mão — que é o que prova que o grafo do Gerente fecha sem
  o DI resolver nada por baixo. O painel é o teste certo para o fim do circuito porque
  ele **não calcula nada**: elo partido não vira erro, vira número ZERADO, e zero por
  defeito é indistinguível de zero porque o dia foi fraco.
- **Cobertura é medida, não estimada.** `dotnet test --collect:"XPlat Code Coverage"` (o
  `coverlet` já está no `.csproj`). A leitura útil é a da **camada de aplicação sem as
  migrations** — elas têm milhares de linhas e afundam o número global para ~21%. Hoje:
  **91,6%**, com 26 métodos sem execução (eram 39). A varredura por "método com zero
  execução" é o que achou `RemarcarEmLoteAsync`, `ApuracaoMensalAsync` e os dois PDFs do
  faturamento — 323 linhas de desenho aprovadas no CI que nunca haviam rodado uma vez.
  **Serviço testado não é serviço executado**, e é assim que se descobre a diferença.
- **Duas barreiras locais contra erro de compilação WPF, e elas se dividem por linguagem.**
  - `tools/compilar-sombra.py` compila **o C#** dos sete projetos WPF. Ele recompila os
    mesmos `.cs` num projeto `net8.0` comum que referencia as *reference assemblies* do
    WPF baixadas do NuGet, e substitui o compilador de marcação — a etapa que o SDK de
    Linux não tem — por um gerador próprio de `.g.cs`: para cada XAML com `x:Class`, emite
    a parte `partial` com `InitializeComponent()` e um campo por `x:Name`. **`x:Name`
    dentro de `Style`/`ControlTemplate`/`DataTemplate` NÃO vira campo** (o WPF resolve por
    `FindName` em runtime) — gerar campo ali inventaria erro que o CI não tem. Ele nasceu
    de quatro erros reais numa noite só (`CS0019` de `DayOfWeek % 7`, `CS0117` de membro
    inexistente, `CS1503` de argumento posicional caindo no parâmetro errado e `CS0579` de
    `[RelayCommand]` órfão), e é **autotestado contra os quatro**.
  - `tools/verificar-suite.py` cobre **o XAML e o que é textual**: chaves do design system,
    pack URIs, dicionário que usa token sem mesclá-lo (quebra em runtime, não no build),
    **aridade de `new XViewModel(...)` escrito à mão** (checagem 7), **membro `required`
    não inicializado** (checagem 8), **variável de padrão colidindo com outra declaração do
    mesmo método** (checagem 9) e **tipo público duplicado no mesmo projeto e namespace**
    (checagem 12, `CS0101`). A 9 tem uma assimetria que engana: `is { } f` entra no escopo
    do MÉTODO, enquanto `foreach (var f in ...)` entra num escopo próprio — por isso dois
    `foreach` com o mesmo nome são irmãos e legais, e a checagem parte só das variáveis de
    padrão.

  **Rode as duas antes de todo push.** O que sobra para o CI é o compilador de marcação de
  verdade (`MC*`) e o empacotamento — não invente heurística para isso.
- **Tamanho de tela tem TETO e PISO, e o piso é o que corta.** A checagem 15 olha o teto
  (janela que nasce maior que o monitor de 1366×768 do balcão); a **16** olha o piso, e é
  o lado que o usuário alcança sozinho: janela redimensionável sem `MinWidth` encolhe até
  o mínimo do WPF e não há leiaute que resista. A diferença é que o teto tem conserto —
  arrasta-se a borda — e o **piso não**: quem encolheu demais não sabe qual largura
  devolve a tela ao normal. A **17** cobre o texto: `StackPanel` horizontal dá a cada
  filho a largura que ele PEDE e nunca dobra a linha, e `WrapPanel` dobra ENTRE os filhos
  mas nunca DENTRO de um — então `{Binding Mensagem}` sem `TextWrapping` sai pela direita
  e **não se lê justamente o texto que explica por que a ação falhou**. As duas valem
  também no faturamento congelado, pela razão da 15 (nada a ver com arquitetura, o usuário
  sente todo dia) e por uma segunda: os três casos reais estavam lá, e checagem que não
  alcança o lugar onde o defeito estava é checagem que passa sozinha.
  Quando o conteúdo tem um piso REAL, sobe-se o mínimo em vez de espremer: as figuras do
  mapa corporal são `Canvas` de 220×460 casados com `PontoMapaItem.LarguraFigura`, a mesma
  const que posiciona o marcador e converte o clique de volta — encolher a figura
  espalharia os pontos em silêncio, então quem cede é a janela (`EvolucaoWindow`, mínimo
  960). Quando o piso é gratuito, ele some: largura fixa de campo dentro de barra vira
  coluna elástica (`RetornoLoteWindow`), que de quebra acaba com o vão morto ao lado dele
  quando a janela está maximizada.
- **Gráfico é desenhado com os tokens, sem biblioteca** (`Controls/Graficos.cs`,
  `Componentes/Graficos.xaml`). Os cinco apps se auto-atualizam por Velopack e uma
  dependência de UI nova é risco desproporcional. Duas regras do desenho: **valor nulo
  interrompe a linha** (um mês sem horário fechado desenhado como 0% inventaria um mês
  perfeito) e **o eixo ancora no zero** (escala que começa no menor valor transforma
  variação de 2% num despencar visual). A fração das barras vem **normalizada do
  ViewModel** — o DataTemplate não enxerga os irmãos da série.
- **CSV para o Excel em português** (`Componentes/ExportacaoCsv.cs`): separador **ponto e
  vírgula** e **BOM UTF-8**. Com vírgula o arquivo abre com tudo numa coluna só; sem BOM,
  "Ocupação" vira "OcupaÃ§Ã£o". Métrica sem base de cálculo exporta "—", igual à tela —
  trocar por 0 faria a planilha calcular média sobre um número que não existe.
- **Escolher paciente é um componente só**: `SeletorPacienteViewModel` (VM) +
  `ItemPacienteSeletor` (`Styles/Componentes/Pacientes.xaml`). Ele já resolve limite no SQL
  (`BuscarPacientesAsync(termo, limite)` — nunca `Take()` depois de materializar), agrupamento
  das teclas e descarte de resposta fora de ordem. Tela nova que escolhe paciente usa ele; não
  reescreva a busca. A **listagem** de Pacientes é outra coisa (linha com ações) e usa o mesmo
  VM com `limite: null` + `Refinar`.
- `docs/atualizacoes.md` documenta o mecanismo de auto-update; `docs/design-system/` documenta
  tokens, componentes, atalhos e acessibilidade da UI.
- **CPF do paciente não se repete, e a recusa mora na ESCRITA** (`PacienteService`,
  parcela 57): duas fichas da mesma pessoa partem o histórico em dois — metade dos
  atendimentos, das guias e do prontuário fica em cada uma. Não é fraude nem erro de
  digitação: é a mesma pessoa cadastrada de novo por quem não achou a ficha antiga, e por
  isso a mensagem **diz o nome de quem já tem aquele CPF** — é o que transforma um erro
  numa instrução ("é este aqui, abra a ficha dele").
  **Não é índice único**, e a razão é a mesma que já vale para o CPF do profissional
  (parcela 45): a migration roda no `MigrateAsync` da ABERTURA do app, inclusive do
  faturamento em produção, e a criação do índice falharia se a base já tivesse duplicata —
  quem não abriria seria o sistema que fatura. E ela TEM chance de ter: até aqui nada
  impedia.
  ⚠️ **A regra é simples de propósito: CPF de OUTRA ficha é recusado na criação e na
  edição.** Houve uma versão que abria exceção para a ficha antiga já duplicada, para não
  travar a correção do telefone dela; a direção dispensou — as duplicatas que já existem
  serão apagadas direto no banco (Neon), e daí em diante só precisa existir o impedimento.
  **Regra com exceção que ninguém vai exercer é código a mais para manter e mais uma
  resposta possível para a mesma pergunta.** O efeito colateral fica fixado em teste em vez
  de descoberto no balcão: enquanto a limpeza não acontece, a ficha duplicada não salva.
  Dois cuidados que o código não conta sozinho: a comparação **ignora máscara nos dois
  lados** (a coluna aceita 30 caracteres e guarda o que foi digitado; a base tem linhas
  anteriores à normalização, com "123.456.789-00", e comparar o texto cru deixaria passar
  justamente o duplicado que já existe) e a limpeza acontece **no banco**, com o `replace`
  do SQL, porque carregar a carteira inteira a cada Salvar seria uma varredura completa
  numa base remota. **CPF em branco continua sendo o caso normal** — criança, paciente
  cadastrado pela carteirinha, quem chegou sem documento —, e vazio vira NULO para dois
  documentos "" não serem iguais.

- **GUIA NÃO É ATENDIMENTO — e o sistema materializava a pendência como HORÁRIO**
  (parcela 58; a cliente mandou a foto de uma paciente sem agendamento ocupando a fila do
  dia e a agenda). `AgendaService.ConfirmarPresencaAsync` criava, ao confirmar a presença,
  um `Agendamento` de verdade (`OrigemAgendamento.RetornoSugerido`) na data prevista do 2º
  código, às 9h, "para não esquecer de obtê-lo".
  O 2º código é obtido +24h depois **pela SECRETÁRIA, no sistema do convênio** — o paciente
  não volta para nada. Materializá-lo como horário punha na fila do balcão e na agenda dos
  MÉDICOS uma pessoa que não tem hora marcada e não vai aparecer.
  ⚠️ **E não era ruído visual.** O cartão fantasma vinha com "Chegou / Entrou / Falta /
  Cancelar": um clique em Entrou → Finalizar lança um atendimento NOVO e gera guias NOVAS
  para uma sessão que nunca aconteceu — faturamento inventado a partir de uma pendência de
  faturamento. É a inversão exata do que o produto existe para fazer.
  **Não faltava lembrete, sobrava um no lugar errado**: o 2º código já nasce como
  `CodigoFaturamento` com `DataPrevistaFaturamento`, aparece no painel de pendências com
  semáforo, entra na rodada bloqueante ao vencer o prazo e é mostrado ao balcão pelo
  `PainelRecepcaoService` junto dos pacientes do dia.
  O valor do enum FICA (é gravado como texto e há linhas assim em produção; apagá-lo
  quebraria a leitura delas), e o selo "Retorno do 2º código" segue na tela justamente
  para a clínica reconhecer e limpar as que sobraram.
  A lição, que é a mais cara desta lista: **ao ligar dois módulos, pergunte se o que
  atravessa é o FATO ou só o lembrete dele.** Pendência de faturamento é fato do
  faturamento; ela se mostra onde se resolve, e não vira agenda de quem atende.

- **`EstadoDaTela` com `Visibility` AMARRADA: o binding morre e o vazio vaza pela tela**
  (parcela 58 — o cliente mandou a foto de "Nenhuma sessão registrada" escrito por cima da
  lista de pacientes CHEIA, no Prontuário e nas Prescrições da Recepção). O componente
  decide a própria `Visibility` em `Recalcular()`, atribuindo um valor **local** — e em WPF
  valor local atribuído por código **substitui o binding**. A tela ligava a visibilidade a
  "estou mostrando o prontuário"; na primeira mudança de `Itens`, `Carregando` ou
  `NaoVerificado` o `Recalcular` sobrescrevia e o binding deixava de existir. Daí em diante
  o vazio aparecia quando a LISTA estava vazia, e não quando a tela dele estava aberta.
  A saída é `Ativo`, que entra no CÁLCULO em vez de brigar com ele; e o componente volta
  para dentro da região a que pertence, como último filho do Grid dela — na raiz da tela
  ele cobre tudo, que é o defeito que a PR #113 já tinha corrigido noutras telas. Virou a
  **checagem 30**.
- **`SharedSizeGroup` sem `Grid.IsSharedSizeScope` não alinha NADA** (parcela 58): cada
  linha de uma lista é um Grid próprio, então sem o escopo as larguras são resolvidas POR
  LINHA — a linha que tem o selo "Carteirinha vencida" fica com a última coluna mais larga
  e empurra o telefone daquela linha para a esquerda. A lista deixa de ter colunas e vira
  uma pilha de linhas que por acaso se parecem. O `ItemPacienteLinha` trazia o aviso
  escrito no próprio comentário ("o escopo é declarado por quem monta a lista") e **três
  das quatro telas que o usam esqueceram**. Contrato que depende de alguém lembrar é o que
  a **checagem 31** existe para substituir.
  ⚠️ A checagem nasceu CEGA e o autoteste é que mostrou: ela procurava
  `IsSharedSizeScope` no texto do arquivo, e o **comentário que explica a regra** satisfazia
  a busca — a tela ficava desalinhada com a checagem verde. É o inverso da lição da
  checagem 19 (lá a prosa fazia disparar, aqui fazia silenciar), e o silêncio é pior:
  ninguém percebe uma checagem que passou. **Toda checagem que procura uma marca no texto
  tem de tirar os comentários antes.**

- **A AGENDA era uma pilha de cartões; agenda é LINHA DO TEMPO** (parcela 58 — *"o iClinic
  está dando um banho na gente nessa questão de fila/agenda"*). A da Recepção empilhava os
  horários numa coluna por profissional, contíguos: o das 9h colado no das 15h. Ela
  respondia **"quais horários existem"**, e a pergunta do balcão com o paciente na frente é
  outra — **"quando cabe?"**. Numa pilha, ela só se responde lendo cartão por cartão; numa
  grade, o **vazio tem tamanho**, e é isso que a torna legível de longe.
  As decisões:
  (a) **O vão livre é CLICÁVEL e abre o formulário já na hora e na coluna clicadas**
  (`ProfissionalPreferidoId` — um ID, não a entidade, porque a lista de profissionais é
  carregada do banco DEPOIS do construtor, e atribuir a entidade de fora abriria o combo em
  branco justamente na coluna que a pessoa apontou). Redigitar a hora que se acabou de
  apontar com o dedo é onde ela sai errada.
  (b) **A janela de horas é a padrão da clínica ESTICADA pelo que existe no dia** — nunca
  00:00–23:59, que daria 48 faixas vazias para rolar antes do primeiro paciente; e uma
  sessão às 6h30 puxa a grade para cima em vez de ficar de fora.
  (c) **A sessão de uma hora marca a faixa seguinte como CONTINUAÇÃO**, senão a meia hora
  de dentro dela apareceria vaga e a recepção marcaria por cima.
  (d) **Cancelado e falta ficam apagados, nunca sumindo** (a regra da folha do dia, parcela
  29) — e o vão deles volta a ser oferecido, porque o horário de fato vagou.
  (e) **Colunas de largura IGUAL** (`UniformGrid`), que é o que mantém cabeçalho e faixas no
  mesmo prumo sem uma única `SharedSizeGroup`; o cabeçalho rola JUNTO, porque congelá-lo
  exigiria dois `ScrollViewer` sincronizados à mão — mais peça para desalinhar do que valor.
  (f) **Os sete botões saíram do cartão** e foram para a janela do horário. Sete botões em
  cada um de quarenta cartões é uma tela em que o olho para de distinguir o frequente do
  raro — e não caberiam em 46 px. A janela **não executa nada**: devolve a INTENÇÃO, e quem
  age é a ViewModel, para as sete ações continuarem com um dono só (permissão,
  recarregamento e erro no mesmo lugar).
  (g) **A lista de espera saiu da faixa lateral de 320 px** e virou botão com a contagem no
  rótulo. Ela se consulta quando um horário VAGA; a agenda é o que se olha o tempo todo.
  E o "quem chamar para as 14h" ABRE a janela em vez de repintar um painel que ninguém
  estava olhando.
  ⚠️ **A janela de cima passou a ser dona do próximo modal** (`Dono()` = a janela ATIVA, não
  `MainWindow`): com a lista de espera aberta, o formulário nasceria ATRÁS dela, e a
  recepção concluiria que o clique não fez nada.

- **`WrapPanel` que nunca dobra a linha** (parcela 58, checagem 32): a barra da agenda tem
  nove controles. Num `Grid` de coluna `Width="Auto"` — ou docado num `DockPanel`, ou dentro
  de um `StackPanel` horizontal — o `WrapPanel` é medido com largura **infinita**: ele
  alinha tudo numa linha só e empurra o título para fora da tela. O `Auto` engana
  especialmente, porque a intenção declarada ("ocupa o que precisar") é exatamente o que
  impede a quebra. A saída é fazê-lo o filho que **PREENCHE** e encostá-lo com
  `HorizontalAlignment`. Nenhuma rede pegava — XAML bem-formado, nada lança —, e o defeito
  **não reproduz na máquina de quem escreve**: só na largura errada.

- **Kanban se ARRASTA — e o arrasto não pode ser o único caminho** (parcela 58): a fila
  ganhou arrastar-e-soltar entre as cinco raias, com realce da raia de destino (alvo de
  soltar sem realce é alvo que se erra, e errar aqui manda o paciente para a coluna do
  lado). Três regras:
  **as transições legais são EXATAMENTE as dos botões** — não uma segunda regra escrita no
  arrasto, porque duas definições de "para onde este cartão pode ir" divergem na primeira
  correção e a de baixo é a que ninguém lembra de ajustar; **para trás anda um passo por
  vez**, porque `VoltarEtapaAsync` apaga UM carimbo de hora e apagar três de uma vez para
  atender a um arrasto longo inventaria uma linha do tempo que não aconteceu; e
  **movimento impossível não é silêncio** — a tela diz por que não deu (a lição da parcela
  41 aplicada ao gesto).
  O handler de `PreviewMouseLeftButtonDown` **não marca o evento como tratado** e o arrasto
  só começa depois do limiar do sistema: sem isso o clique nunca chegaria aos botões do
  cartão, e o quadro perderia a metade que funciona para quem não arrasta nada.
  ⚠️ **O menu "⋯" e o arrasto moram em CÓDIGO, não em XAML**: um `ContextMenu` declarado
  vive num `Popup`, fora da árvore visual da tela, e os comandos precisariam de
  `PlacementTarget.Tag` para chegar ao ViewModel — binding que erra o caminho falha em
  RUNTIME, calado, que é a categoria que nenhuma rede local pega. Em código, o
  `compilar-sombra` pega.

- **Quem LANÇOU o atendimento e o agendamento** (parcela 58): a direção pediu para ver de
  quem é cada lançamento. A trilha de auditoria responde "quem fez isso?" desde a parcela
  21 — mas ela é outra tela, filtrada por período, e a pergunta que se faz olhando a agenda
  é sobre **aquela linha, agora**. `Agendamento.CriadoPor/CriadoEm` e
  `Atendimento.LancadoPor/LancadoEm` são migration **aditiva** (quatro `AddColumn`), e o
  operador chega da TELA (`SessaoUsuario.Atual.Operador`): o serviço não lê a sessão, pela
  razão de sempre — é o chamador que sabe quem está logado, e no balcão duas pessoas
  dividem a máquina.
  **Nulo é decisão**: linha anterior à parcela guarda `null`, nunca `""` nem o usuário do
  Windows, e a tela escreve *"marcado antes de o sistema passar a registrar quem lança"* —
  em branco não se distingue de "não carregou".
  A confirmação de presença leva **quem CONCLUIU**, não quem marcou o horário semanas
  atrás: são dois atos e duas pessoas, e o segundo é o que gera as GUIAS.

- **A Agenda saiu do perfil Faturista** (parcela 58): a direção pediu *"retire a Agenda do
  módulo faturamento ou permita granular que o faturista possa ou não abrir um
  agendamento"*, e a razão é a mesma que derrubou o agendamento fantasma do 2º código —
  horário aberto do lado do faturamento aparece na fila do balcão e na agenda de quem
  atende. A escolha foi **granular, não amputação**: `PerfilAcesso.Faturista` perdeu
  `EditarAgenda` e **manteve `VerAgenda`**, porque conferir o dia é parte de faturá-lo, e
  tirar as duas juntas seria tirar capacidade de quem a usava (a regra 3 do bloco do
  faturamento). Devolver é um clique em Acessos.
  As **duas barreiras** entraram no `AgendaViewModel` do faturamento no mesmo commit —
  `PodeAgendar` nos botões e `Exigir` em `AbrirCadastroAsync`, que é o ponto único por onde
  Novo, Remarcar e "agendar na faixa" passam. Bit sem guarda seria só uma caixinha na tela
  de Acessos.

- **DUAS PORTAS, UMA ESTEIRA — e a diferença entre elas não tinha sido decidida, tinha
  sido esquecida** (parcela 60). Havia dois caminhos para criar atendimento, e do lado do
  faturamento eles eram idênticos — a guia nascia certa —, o que é exatamente por que
  ninguém notou. Em volta dela, não: **pela agenda** (Fila → Finalizar) o
  `FechamentoSessaoService` fazia QUATRO coisas — a guia nasce, o pacote debita, o insumo
  sai do estoque, o dinheiro entra no caixa; **pelo avulso** acontecia UMA.
  O custo era invisível e diário: paciente com pacote de dez sessões lançado pelo avulso
  consumia sessão **sem debitar** (a clínica atendia de graça, e é o que o `PacoteService`
  existe para impedir), e o particular que pagou no balcão não aparecia no caixa — o mês
  fechava com uma diferença que não tinha nome. Não havia uma linha de comentário
  explicando a escolha porque **não houve escolha**.
  A saída não foi escolher uma das duas telas: foi ver que **quem chega sem horário está
  pedindo um ENCAIXE**, e encaixe a agenda já sabia fazer. "Novo atendimento" continua no
  menu (decisão da direção) e por dentro faz `AgendarAsync(hora real, encaixe: true)` →
  `RegistrarChegadaAsync` → **a MESMA `FechamentoSessaoWindow` da Fila**. Não há uma
  segunda tela de decisão: duas telas para a mesma pergunta divergem na primeira correção.
  ⚠️ **O efeito estrutural é a amarra**: `AtendimentoService.LancarAsync` ficou com **UM
  ÚNICO CHAMADOR** em todo o sistema (`AgendaService.ConfirmarPresencaAsync`). Ponto único
  deixou de ser documentação e virou o que o compilador mostra. `registrarNaAgenda` morreu
  — e com ele o **agendamento fantasma às 9h fixo**, sem profissional, que o avulso criava
  porque `Atendimento` só guarda `DateOnly` e não havia hora para copiar; ele aparecia na
  grade num horário em que ninguém foi atendido e CONTAVA na ocupação do dia. Não era o
  fantasma da parcela 58 (aquele nascia `Agendado` e tinha o botão "Entrou", que fabricava
  guia): era ruído na grade e um número de ocupação errado — a mesma família, sem o
  estrago.
  **O que a unificação custou, e por que valeu**: `Agendamento.PrimeiroCodigo` (migration
  aditiva). A escolha de qual código o convênio libera primeiro é da tela, e precisava
  atravessar o horário para chegar ao motor — sem a coluna, unificar teria custado a
  feature. De quebra ela ficou disponível também no agendamento normal.
  **Cancelar o fechamento não desfaz o encaixe**: o horário fica com o check-in carimbado,
  e o paciente aparece na Fila em "Na recepção". É a verdade — ele chegou —, e apagar o
  registro sumiria com o único sinal de que há alguém no balcão esperando.
  `Avulso_e_agendado_produzem_os_mesmos_fatos` é a amarra em teste: ele falha se alguém
  abrir uma terceira porta que pule algum dos fatos, que é como a segunda foi aberta.
- **ATENDIMENTO QUE ENTRA NO SISTEMA JÁ GERA GUIA — a unificação da parcela 60 pendurou a
  guia no clique errado** (parcela 65; o cliente achou em produção, no dia seguinte). Ao
  unificar a esteira, a guia deixou de nascer no lançamento e passou a nascer na
  **confirmação da `FechamentoSessaoWindow`**. Quem fechasse aquela janela ficava com o
  horário na agenda, o paciente marcado como presente e **nenhuma guia** — e, como o
  encaixe tinha sido criado, a tela parecia ter funcionado.
  Em **12/08/2026** a mesma paciente foi lançada **três vezes em 71 segundos** (encaixes
  161, 162 e 163, todos com `ChegadaEm` carimbado e `AtendimentoId` nulo): a recepcionista
  não via guia nenhuma e tentava de novo. Zero guias, três cartões na fila para uma sessão.
  A mensagem que explicava existia — inline, numa tela que a pessoa já dava por concluída.
  **Ninguém tenta três vezes em um minuto se a mensagem chegou.**
  A regra que a direção fixou é uma só e vale para as DUAS portas: **registrou o
  atendimento, a guia nasce e vai para o faturamento**. Pacote, insumo e caixa são o passo
  SEGUINTE e não podem condicionar nem desfazer a guia — é a MESMA hierarquia que o
  `ConcluirAsync` já aplicava entre os quatro fatos (só o atendimento derruba a operação;
  os outros três viram aviso), agora estendida ao **momento** em que cada um acontece.
  `FechamentoSessaoService.RegistrarAtendimentoAsync` é o ponto único, e é **idempotente
  por agendamento**: quem já tem `AtendimentoId` é reaproveitado, nunca reconfirmado —
  dois cliques no Finalizar não viram duas guias.
  ⚠️ **A recusa de "agendamento já realizado" teve de SAIR do `PrepararAsync`.** Com a guia
  antes da janela, `Realizado` virou o caso NORMAL da tela, e a recusa derrubaria
  justamente a janela que veio depois do lançamento correto. Quem impede a duplicidade é o
  reaproveitamento do atendimento e o `AtendimentoJaConsumiuPacoteAsync` — nunca uma
  exceção na cara de quem clicou duas vezes procurando a guia que ele não viu.
  **A janela só abre quando há o que decidir** (`RegistroAtendimento.TemDecisao`: pacote,
  dinheiro ou insumo). Para o paciente de convênio sem pacote não há uma única pergunta a
  responder ali, e pedir confirmação de uma tela vazia é como se ensina alguém a fechar
  janela sem ler — que é a causa raiz do dia 12.
  **E o segundo clique passou a custar caro**, então ganhou pergunta: antes de criar o
  encaixe, a tela confere se o paciente já tem atendimento naquele dia e pergunta. É
  PERGUNTA e não recusa — o paciente pode ter a sessão da manhã e a consulta da tarde, e
  recusar travaria o balcão num caso legítimo que a recepcionista não teria como contornar.
  A lição, que vale para a próxima unificação: **ao juntar dois fluxos, pergunte em que
  momento cada fato passa a existir — e não deixe o fato IRREVERSÍVEL depender do passo
  OPCIONAL.** A guia é o que a clínica não pode perder; o pacote e o caixa se resolvem
  depois, por outra tela. Inverter essa ordem não quebra teste nenhum: os 1512 continuavam
  verdes, porque cada um deles chamava `ConcluirAsync` — o caminho que ninguém questiona é
  o que os testes exercitam.
- **Mover XAML entre projetos quebra o `xmlns`, e só o CI acusa** (parcela 60, checagem
  33): `clr-namespace:X;assembly=Y` manda o WPF procurar o namespace X **dentro** do
  assembly Y. Quando Y é o próprio projeto do arquivo, o compilador de marcação não acha
  nada e recusa (`MC3074`, `MC3072`). É o que acontece ao mover uma tela de um projeto para
  outro: o `xmlns` continua nomeando o assembly de ORIGEM, que agora é o de DESTINO.
  Foi assim que a tela de Pacotes, ao subir do Financeiro para o shell, derrubou o build.
  ⚠️ **Nenhuma rede local pegava**: o XML é bem-formado, o `compilar-sombra` **não lê o
  corpo** do XAML (ele só gera o `.g.cs` a partir de `x:Class` e `x:Name`) e o C# compila.
  O defeito existe só para o compilador de MARCAÇÃO, que roda no Windows — sete minutos de
  CI por um `;assembly=` que sobra. Dentro do próprio projeto a forma certa é
  `clr-namespace:…` **sem** o sufixo.
- **A venda de pacote subiu para o shell** (parcela 60): a tela existe desde a parcela 4 e
  a única porta estava no app do FINANCEIRO — mas quem vende dez sessões ao paciente é a
  RECEPÇÃO, no balcão, com ele na frente. Décima primeira ocorrência do defeito recorrente,
  e ela bloqueava justamente o caso que motivou o Particular. A tela não foi copiada:
  **subiu** (`Componentes/PacotesView`), como a sala de infusão na parcela 48, e os dois
  módulos publicam a **MESMA chave** — a dedupe do `ShellViewModel` faz o Gerente, que
  carrega os dois, mostrar uma linha só.
  ⚠️ O item passou a exigir `Permissao.VenderPacote`, bit **próprio**, e não
  `VerFinanceiro`: dar o financeiro ao balcão abriria junto o caixa, a conciliação e as
  contas a pagar. Vender um pacote é combinar um preço com o paciente; ler o dinheiro da
  clínica é outra coisa — o mesmo corte que a parcela 49 fez entre ficha e prontuário.
- **O PARTICULAR não é um convênio: é a ausência de um** (`ConvenioCadastro.GeraGuia`,
  parcela 60). O enum `Convenio` não tem "sem convênio", então o paciente que vem sem plano
  não tinha onde ser cadastrado — e as duas saídas eram ruins de jeitos diferentes:
  (a) cadastrá-lo sob um convênio qualquer faz o motor gerar guia com data prevista, que
  entra no painel de pendências, vence o prazo e abre a **rodada BLOQUEANTE** — travando a
  tela de quem fatura por uma guia que nunca vai a operadora nenhuma, porque não há
  operadora; (b) não cadastrar o atendimento, e aí a sessão não existe em lugar nenhum:
  nem guia, nem prontuário, nem caixa.
  A saída é um sinalizador no CADASTRO, e **o código continua nascendo** — marcado
  `NaoAplicavel`. Não é preciosismo: `EstaPendente` já ignora esse status, então o
  particular sai das pendências e da rodada **sem uma linha de código nova**; o invariante
  "não há atendimento sem código" é o que prova que `AtendimentoService.LancarAsync`
  continua sendo ponto único; e o registro da sessão (modalidade, especialidade, data) é o
  que alimenta os indicadores — sumir com ele faria a clínica medir só o convênio.
  ⚠️ **O sinalizador mora no `ConvenioCadastro`, não em `ConfiguracaoRegraGenerica`**, pela
  mesma razão que pôs o formato do número da guia lá: aquela configuração só é lida pela
  família Personalizado, e o campo tem de valer para **qualquer** família. Dentro dela
  seria uma caixinha que não faz nada num convênio embutido — o campo morto que a parcela
  49 tirou da tela de Taxas.
  ⚠️ **E quem aplica é o `AtendimentoService`, não as regras.** São seis regras com vários
  ramos cada, e a modalidade Consulta delega para `RegraConsultaAvulsa`, que monta o código
  por fora: um ramo esquecido produziria guia pendente para um particular, e ela só
  apareceria **dez dias depois**, travando a rodada. Pós-processar no ponto por onde a
  PRÉVIA e o LANÇAMENTO passam é o único jeito que não tem como ser esquecido — e é o que
  garante que a tela não prometa "nenhuma guia" e o serviço grave uma.
  **Código fora do catálogo é presumido FATURÁVEL** (`CatalogoConvenios.GeraGuia` devolve
  `true`): o erro nesse sentido é uma guia a mais para conferir; no sentido contrário seria
  o sistema parar de gerar guia para um convênio de verdade, em silêncio, e só descoberto
  no fim do mês.
- **Migration de coluna `bool` não-anulável: o EF gera `defaultValue: false`, e quase nunca
  é isso que você quer** (parcela 60). O gerador não sabe o que a coluna significa — viu um
  `bool` e pôs o default da linguagem. Aplicada assim, a coluna `GeraGuia` teria desligado
  a guia de **todos os convênios já cadastrados** na primeira abertura depois da
  atualização: o app abriria, os atendimentos continuariam nascendo, e as guias
  simplesmente parariam de existir. **Confira o `defaultValue` de toda coluna nova
  não-anulável, e pergunte o que as linhas JÁ GRAVADAS valem** — é isso que o default
  precisa dizer, não o que o tipo devolve por omissão.
- **Porta e CONTEÚDO são duas permissões, e uma sem a outra não resolve** (parcela 59 — a
  direção viu a recepcionista abrindo os documentos e pediu a permissão granular). A
  central de documentos pedia `VerFichaPaciente`, que todo perfil de balcão tem: a
  recepção alcançava as dez folhas, inclusive relatório de evolução e anamnese.
  **A porta sozinha seria o defeito do bit sobrecarregado de novo** (parcela 49), agora
  numa tela: as dez folhas não são a mesma coisa — receituário, atestado, pedido de exame,
  relatório de evolução e anamnese carregam dado de saúde (art. 5º, II); **declaração de
  comparecimento e termo de consentimento não**, e os dois saem do balcão o dia inteiro.
  Um bit só obrigaria a direção a escolher entre a recepcionista lendo a evolução de todo
  mundo e a recepcionista sem o recibo que ela emite dez vezes por dia.
  Daí as duas metades: `Permissao.VerDocumentos` fecha a SEÇÃO, e cada folha declara o que
  exige (`FolhaCatalogo.PermissaoVer` / `PermissaoEmitir`).
  ⚠️ **O acesso NÃO segue a `NaturezaFolha`**, e a distinção é o ponto: a natureza diz de
  que lado da clínica a folha vem (é o que agrupa os cartões), e sete são "do
  atendimento". Amarrar o acesso a ela tiraria da recepção dois papéis que ela entrega
  todo dia para proteger um dado que eles não carregam.
  ⚠️ **A regra mora no CATÁLOGO porque são TRÊS portas**: a central, o Receituário da
  Recepção e a aba Documentos da ficha emitem os mesmos papéis. Corrigir só a que o
  cliente apontou deixaria a correção cosmética — bastaria clicar no item ao lado para ler
  as mesmas receitas. É o defeito recorrente do projeto na variante que mais engana: a que
  **parece** coberta.
  Três decisões menores que valem além desta tela: **cartão que a pessoa não alcança SOME,
  não fica apagado** — "sem permissão" ao lado de "Relatório de evolução" anuncia que
  existe um relatório daquele paciente, que é justamente o que não se quer contar; **a
  lista do que já saiu passa pelo mesmo filtro dos cartões**, senão a tela esconderia o
  botão de emitir receita e mostraria "Receituário 2026/0012 — Maria Silva" logo abaixo; e
  **folha sem acesso declarado nasce FECHADA** (`Permissao.Nenhuma` faz `Pode` LIBERAR, e
  um papel novo nasceria aberto para todo mundo sem ninguém notar até vazar).
  `SessaoUsuario.Efetivas` existe para filtrar LISTA por acesso sem repetir a regra do
  "sem sessão autenticada, libera" — lida como `Permissoes` cru, a central abriria sem um
  único cartão fora do login, e tela vazia se lê como defeito.
- **A rodada bloqueante é DIRIGIDA a quem fatura, e a dispensa é um BIT — não o contrário**
  (`Permissao.DispensarRodadaPendencias`, parcela 57): a trava de 10 dias abria para
  qualquer um que pudesse baixar OU marcar NC, e o Gerente Geral recebe `Todas` — então a
  direção entrava no faturamento para CONFERIR e caía numa fila de guias que ela não vai
  resolver. Travar quem entra para olhar faz a conferência simplesmente não acontecer.
  ⚠️ **O bit é uma DISPENSA, e essa inversão é a decisão.** `PerfilAcesso.Gerente =>
  Todas` percorre `Enum.GetValues`, então um bit com o sentido direto ("está sujeito à
  rodada") chegaria LIGADO à direção justamente por ela ter tudo — e um bit novo que
  precisasse ser subtraído de `Todas` transformaria "todas" em "todas menos", que é a
  porta para a próxima exceção. Como dispensa, o perfil que tem tudo é dispensado sozinho
  e o `Faturista`, que não tem, continua travado.
  **Dispensa não é cegueira**: quem tem o bit continua vendo o banner de rodada vencida no
  painel e o botão "Rodar pendências". Esconder o aviso junto faria a direção deixar de
  saber que há guia vencida — o oposto do que a rodada existe para garantir. Por isso a
  checagem mora na ABERTURA (`App.MostrarRodadaSeVencidaAsync`) e não dentro do
  `RodadaPendenciasFluxo`, que é compartilhado com o botão do painel: o que se desliga é a
  janela que TRANCA, nunca a capacidade de rodar.
  ⚠️ **Tensão que ficou de pé e é decisão da direção**: o `Faturista` não tem
  `MarcarNaoConformidade` (parcela 49), e a janela bloqueante exige uma decisão POR GUIA —
  baixa ou NC. Quem não tem o número da guia e não pode marcar NC fica sem saída para
  aquela linha. Antes isso se diluía porque a direção também era travada e resolvia; agora
  a trava é só dele. O conserto de um clique existe e é o que a granularidade serve:
  conceder `MarcarNaoConformidade` ao Faturista em Acessos.

- **Coluna de formulário feita de `StackPanel` irmão desalinha quando o rótulo quebra**
  (parcela 57 — a cliente reprovou o "fora de esquadro" da tela de novo paciente). Duas
  colunas lado a lado, cada uma um `StackPanel` com rótulo em cima e campo embaixo, ficam
  alinhadas **enquanto os dois rótulos couberem numa linha**. Basta a janela estreitar ou
  o rótulo crescer — "Validade da carteirinha" ao lado de "Nº da carteirinha" — para um
  deles quebrar em duas linhas, empurrar o campo daquela coluna 17px para baixo e deixar a
  linha inteira torta. O defeito depende do texto E da largura, que é o que o torna difícil
  de reproduzir e fácil de reintroduzir.
  A correção é estrutural: rótulo e campo em **LINHAS de `Grid`**, com a linha do rótulo
  em `SharedSizeGroup` — as duas colunas reservam a altura do MAIOR rótulo e os campos
  começam sempre na mesma altura. ⚠️ O nome do grupo é um **identificador** (sem ponto,
  sem espaço): ele é validado em RUNTIME e derruba a tela inteira — a lição da parcela 50,
  hoje cobrada pela checagem 27.
  Na mesma tela, dois vizinhos do mesmo tipo: o **`DatePicker` sem `Template` no design
  system do FATURAMENTO** (o mesmo defeito da parcela 56 no da suíte, portado agora — dois
  design systems que não se referenciam significam corrigir duas vezes), e **botão ancorado
  num `DockPanel` sem `VerticalAlignment="Center"`**, que estica junto quando a mensagem ao
  lado quebra em três linhas. A recusa de CPF repetido é exatamente uma mensagem dessas: a
  regra nova tornou visível um defeito de leiaute que já estava lá.

- **Clicar no dado COPIA o dado** (`Copiavel`, `CelulaCopiavel`, parcela 57): a clínica
  não vive só neste sistema — a guia é efetivada no PORTAL da operadora, e para isso a
  secretária retipa nome, telefone, carteirinha e número da guia do outro lado. Retipar é
  onde nasce o erro que o produto inteiro combate: o **"O" no lugar do zero** que a
  `RegraNumeroGuia` existe para pegar, o dígito a menos no telefone. Copiar não erra.
  O telefone **já estava no modelo de pendência** desde que o botão do WhatsApp existe —
  o sistema o TINHA e não o MOSTRAVA, e quem precisava do número para o portal abria a
  ficha do paciente noutra tela. É a variante do defeito recorrente do projeto em que o
  dado tem leitor, mas só um, e não o da tarefa.
  As decisões: **nome e telefone na MESMA célula**, e não em duas colunas — as pendências
  já disputam a largura, e foi a coluna de paciente espremida entre vizinhas mais largas
  (1,2* e 1,4* contra 1*) que cortava o nome; **o estilo carrega a afordância** (mão e
  sublinhado no hover), porque recurso que não se anuncia ninguém descobre; **a dica
  mostra o valor inteiro**, que é o que devolve o nome longo a quem só vê o começo dele;
  **célula vazia não se anuncia como copiável**, senão a pessoa clica num traço e conclui
  que quebrou; e o **`TextBlock` ganha fundo transparente ao ser ligado**, porque sem
  pintura o WPF só aceita clique em cima das LETRAS — sem isso o clique ao lado do texto
  não copiaria nada e o recurso pareceria funcionar "às vezes".
  A cópia **tenta três vezes**: a área de transferência é recurso ÚNICO da máquina e fica
  bloqueada enquanto outro programa a segura (o navegador e o próprio portal fazem isso o
  tempo todo). E **falha nunca aparece como sucesso** — dizer "copiado" e a pessoa colar o
  conteúdo anterior no portal é pior do que não ter o recurso, porque ela cola sem
  conferir. A confirmação aparece NO LUGAR DO CLIQUE, não numa barra no rodapé: quem copia
  a terceira célula da linha está olhando para ela.
- **A rede não varria o faturamento na checagem de CHAVE** (parcela 57): chave inexistente
  é `ResourceReferenceKeyNotFoundException` na montagem da tela — erro de runtime puro, que
  é exatamente o grupo que o `arvores_com_faturamento` existe para alcançar desde a parcela
  51. Ficar de fora deixou passar quatro `CellTemplate="{StaticResource …}"` apontando para
  uma chave ainda não declarada: XAML bem-formado, `compilar-sombra` verde,
  `verificar-suite` verde, e a coluna sairia **vazia** na tela de quem fatura. Só a metade
  das CHAVES foi estendida — não a de `FontSize` numérico e cor em hexadecimal, que é a
  dívida antiga que faria a checagem gritar trinta vezes. Medido antes: **zero** chaves
  pendentes no faturamento, então a extensão não custou nada.
  ⚠️ De quebra, a lista de "estilos que já resolvem" da checagem 24 **não seguia o
  `BasedOn`**: estilo que herda o corte do pai aparecia como dívida sem ser. O ponto cego é
  traiçoeiro porque a reclamação é PLAUSÍVEL — quem a lê acrescenta o `TextTrimming`
  repetido na tela e segue, e a checagem continua cega para o próximo caso.

- **O WPF não formata na cultura da máquina — `StringFormat` é en-US por padrão**
  (parcela 56; o cliente viu **"August/2026"** no cabeçalho da Conciliação). O que engana
  é que só METADE da tela erra: o que a ViewModel formata em C# (`valor.ToString("C")`)
  sai em pt-BR pela cultura da máquina, e o que o XAML formata sai em inglês, porque
  binding usa `FrameworkElement.Language` — que nasce `en-US` e ignora a thread. Daí
  "August/2026" logo acima de uma coluna de "R$ 0,00". A correção é um `OverrideMetadata`
  no `SuiteApp`, e não uma linha por binding: são ~30 `StringFormat` de data e moeda
  espalhados, e o próximo que alguém escrever também precisa nascer certo.
- **Componente sem `Template` no design system não fica neutro: fica com o tema do
  SISTEMA OPERACIONAL** (parcela 56 — os campos "De"/"Até" da Auditoria destoavam dos
  vizinhos). O `DatePicker` tinha estilo desde a parcela 7, com só uns `Setter` e um
  comentário que dizia "usa o TextBox estilizado internamente". **Não usa**: o campo de
  texto de um `DatePicker` é um `DatePickerTextBox`, tipo próprio que o estilo implícito
  de `TextBox` não alcança. Sem `Template`, o WPF desenhava a moldura 3D, os cantos retos
  e o botão de calendário do Aero, ao lado dos campos planos da suíte. É a MESMA história
  do `TabControl` na parcela 55 — controle usado em 30 telas e nunca estilizado. O
  **calendário do pop-up ficou de fora de propósito**: retemplá-lo exige `CalendarItem`,
  `CalendarButton` e `CalendarDayButton`, cujos gatilhos só falham em RUNTIME e nenhuma
  rede local compila XAML; o campo, que é o que fica na tela, está resolvido.
- **`EstadoDaTela` sem `Itens` nem `Vazio` liga a sobreposição PARA SEMPRE** (parcela 56 —
  o cliente mandou a foto do "Nada por aqui" escrito por cima do paciente que a tela tinha
  acabado de achar). O componente resolve `(Vazio ?? Vazia(Itens))`, e `Vazia(null)` é
  **verdadeiro**: quem o declara só com `Carregando` e `NaoVerificado` ganha o estado vazio
  permanente por cima de uma tela que funciona por baixo. As duas saídas legítimas são
  `Itens` (o caminho normal, quando há lista) e `Vazio` (tela composta, ou tela que não é
  lista nenhuma e portanto nunca está vazia). Nenhuma rede pegava — XAML bem-formado,
  propriedades existentes, binding válido, nada lança —, e virou a **checagem 29**.
- **Filtro na Conciliação, e por que ele é por OPERADORA e não por família** (parcela 56):
  a tela abria com 53 guias numa lista corrida e nada para estreitá-la. Quem concilia não
  lê a lista — tem o **demonstrativo de uma operadora** na mão e precisa achar as guias
  que estão nele, uma a uma. O filtro de convênio casa pelo **nome resolvido**
  (`CatalogoConvenios.Nome(codigo, familia)`), ao contrário da consulta de guias do
  faturamento, que filtra por família: lá a pergunta é "o que vem sendo feito", aqui é
  "onde estão as guias deste papel", e filtrar por família juntaria "Sul América" e
  "Unimed Costa do Sol" — as duas respondem a `Convenio.Personalizado` — devolvendo as
  guias de quem não está no demonstrativo. A lista de convênios sai **do mês carregado**,
  não do catálogo: oferecer opção que não tem guia daria filtro que só leva a resultado
  vazio. Três regras que o código não conta sozinho: (a) o filtro **reaproveita as
  instâncias** de `LinhaConciliacao`, porque a linha guarda o VALOR DIGITADO e recriá-la
  apagaria o que a pessoa acabou de teclar em cinco guias ao ela estreitar a lista para
  achar a sexta; (b) o resumo e o estado vazio **dizem que está filtrado** — "12 de 53
  guia(s)" e "nenhuma bate com o filtro" existem porque um filtro esquecido que responda
  "nenhuma guia esperando receita" faz a clínica dar o mês por conciliado com 53
  pendentes (a lição da lista de espera da parcela 25); (c) `Convenios.Clear()` faz o
  `ComboBox` devolver **nulo** pelo binding — a mesma armadilha da prévia do Novo
  atendimento na parcela 50 —, então a remontagem da lista roda sob guarda.

- **A sidebar não estava desorganizada: estava CHEIA** (`ItemMenuModulo.Abas`,
  `TelaComAbas`, rail + painel, parcela 55). A direção reclamou de "muitas abas dentro das
  categorias" no Gerente, e a medição explicou por quê: o `Clinica.Gerente.exe` carrega os
  quatro módulos e a dedupe do `ShellViewModel` casa por CHAVE, então a sidebar tinha
  **46 itens** — a 36px por item mais os cabeçalhos, **1.824px de menu para 610px de
  tela**. Um terço visível, o resto atrás de rolagem sem marca de onde se está. O
  `MenuRecolhido` (248↔56px) não ajudava: recolher não mostra um item a mais, só troca
  rótulo por ícone na mesma lista rolante.
  A contagem achou mais três coisas: **dois itens "Prescrições"** lado a lado em PACIENTE
  (`prescricoes` e `consultorio-prescricoes` — chaves diferentes, então a dedupe não
  pegava), **oito glifos repetidos** entre os itens, e o **"Painel da direção" em 9º** no
  primeiro grupo, sendo a tela de abertura.
  O modelo escolhido pelo cliente foi **rail de 56px + painel de categoria**, e ele só é
  viável junto da **consolidação em sub-abas** — 46 itens viraram 24, e o maior painel
  (FINANCEIRO, 8) cabe inteiro em 768px. O rail sozinho deixaria um flyout de 16 linhas por
  cima do conteúdo; a consolidação sozinha ainda pediria 996px. **Nenhuma das duas metades
  bastava, e é isso que a medida mostra.**
  As decisões, e a razão de cada uma:
  (a) **A aba carrega a CHAVE, não a tela** (`AbaMenu(Rotulo, Chave)`), e quem resolve é o
  shell. É a indireção que permite um item compor telas de **módulos diferentes** —
  "Relatórios / BI" é publicado pela Direção e inclui duas telas do Financeiro e uma do
  Consultório — sem que nenhum módulo passe a conhecer o outro, que é a regra da suíte.
  (b) ⚠️ **A tela que vira aba CONTINUA sendo um item.** `NavegacaoSuite.Ir(chave)` procura
  na lista de itens e **devolve false em silêncio** quando não acha; o shell passou a
  resolver a chave de uma sub-tela abrindo **o item pai já na aba certa**, então toda
  navegação entre módulos continua valendo. É literalmente a regressão da 4ª rodada da
  parcela 37, que passou pelas três redes — agora com a **checagem 28** cobrando que a
  chave de cada `AbaMenu` seja item declarado de algum módulo.
  (c) ⚠️ **Quem esconde a sub-tela é o PAI, e só onde o pai existe** — não é uma marca
  nela. Um item composto é declarado por UM módulo, mas compõe telas de vários: no
  `Clinica.Financeiro.exe`, que não carrega a Direção, "Relatórios / BI" não existe, e
  "Resultado do mês" e "Produção" voltam a ser itens de menu comuns. Esconder por decreto
  teria feito duas telas sumirem do único app onde alguém as usa todo dia — o defeito
  recorrente do projeto cometido pela própria correção dele. `Oculto` continua sendo outra
  coisa: a tela que nunca aparece sozinha (as cinco clínicas, que só existem com paciente).
  (d) **O clique FIXA o painel, e é a metade que torna o hover utilizável.** Painel que só
  existe enquanto o mouse está em cima é **alvo móvel**: para ir do ícone até o oitavo item
  o mouse atravessa a borda entre os dois e, num percurso diagonal, sai da zona por um
  instante. Daí os 180ms de intenção para abrir, os **320ms de folga do "corredor"** antes
  de fechar, e o alfinete dentro do próprio painel — mandar a pessoa de volta ao ícone para
  prender a lista é o movimento que este modelo cobra caro.
  (e) **Glifo único por item visível**, agora por obrigação: numa lista com rótulo, oito
  desenhos repetidos passam; num rail, o ícone é a única identificação.
  (f) **A busca indexa o rótulo das ABAS** e diz o caminho ("Fechamento de caixa — em
  Caixa"). Consolidação que tira telas do Ctrl+F troca um problema de rolagem por um pior.
  (g) **A abertura vem primeira no grupo dela** — a ordem dentro do grupo é a de
  carregamento dos módulos, e o dono da abertura é a Direção, que carrega por último: sem a
  exceção, o "Painel" abria o app e aparecia no FIM de GESTÃO. É a desordem que a parcela
  22 corrigiu, um nível abaixo.
  (h) **Uma aba não é aba, é a tela**: sobrando uma só, o shell mostra a tela direto.
  E duas que NÃO foram feitas: "Faturamento (TISS)" **não virou aba de nada** (ele já é uma
  tela de cinco abas por dentro, e pendurá-lo sob outra régua daria abas dentro de abas, o
  que é pior do que os dois itens que se economizaria); e o `Clinica.Desktop` **não foi
  tocado** — ele tem o shell dele, e a Fase 4 segue cancelada.
  ⚠️ A lição de rede: a **checagem 19 disparou no COMENTÁRIO** que explicava a regra, porque
  ela casava `Ir("literal")` sem tirar comentário. Checagem que reclama de prosa é checagem
  que alguém desliga — e aí ela para de pegar o defeito de verdade. Virou `_sem_comentarios`
  (que PULA as strings, para `"https://…"` não virar comentário).

- **Varredura do Gerente: o enum vazava onde a checagem não olha, e a permissão tinha
  UMA barreira** (parcela 54). A checagem 20 só examina `ComboBox`, e por isso não via o
  caminho mais comum do defeito da parcela 41 — **interpolação em `$"..."` dentro do
  ViewModel**. Sete pontos escreviam o identificador na tela: `"ConsultaEspecialidade"` na
  linha da guia em Pendências, NC, Glosas e Tabela de preço; `"ClinicaDaDor"` na
  especialidade; e o **código** do convênio na frase que a direção lê em Rentabilidade
  ("rende mais: UnimedIntercambio" — o defeito da parcela 50 de novo, agora em texto
  montado). `RotulosEnum.De` e `CatalogoConvenios.Nome` já resolviam tudo; as telas não os
  usavam. A lição: **ao procurar enum vazando, procure a INTERPOLAÇÃO, não só o binding** —
  `{p.Tipo}` dentro de uma string é invisível para qualquer checagem de XAML.
  `Especialidade` ganhou rótulo porque é o único cujo humanizador não salva: ele devolve
  "Clinica da dor" sem acento, e isso sai impresso em relatório que vai para fora.
  Do lado da permissão, **`UsuarioEdicaoViewModel` criava usuário, definia senha e alterava
  permissões sem um único `Exigir`** — e é a tela onde a segunda barreira vale mais, porque
  quem mexe em permissão **pode conceder permissão a si mesmo**. A parcela 51 derrubou essa
  suposição no `AcessosViewModel` ("só se chega por ali") e ela ficou de pé na janela que
  ele abre. Campanhas tinha o mesmo buraco em quatro caminhos de escrita.
  **O que a varredura conferiu e NÃO era defeito**, para a próxima não refazer: coluna de
  300–330 px é a coluna de **Ações** em tabela de largura inteira, não faixa lateral (o
  falso positivo da parcela 49); `StackPanel` horizontal sem `VerticalAlignment` só estica
  botão quando o pai é linha de `Grid` — dentro de painel vertical não há defeito; e
  `LotesGerencialViewModel` ser somente leitura é **decisão** (o número do lote é sequência,
  e dois apps gerando em paralelo produzem lotes duplicados que a operadora recusa semanas
  depois).

- **O MESMO ATO com DUAS regras: a metade sem regra é a que ninguém confere** (parcela 64,
  auditoria de prontidão do módulo do GERENTE). A tela "Quem parou de vir" abre o WhatsApp
  com um convite para retomar as sessões. É **recall** — comunicação ativa da clínica —, e
  o projeto decidiu na parcela 5 que recall só sai com `ComunicacaoEMarketing` vigente:
  `CampanhaService.GerarRecallAsync` recusa quem não consentiu e ainda **conta** quantos
  ficaram de fora. A tela mandava a mesma mensagem para as mesmas pessoas **sem perguntar
  nada**, porque a lista vinha de outro serviço (`RetencaoPacienteService`, que responde
  "quem sumiu" e não tem por que saber de consentimento).
  Não é a mesma coisa que capacidade sem porta: aqui há DUAS portas para o mesmo ato, uma
  com regra e outra sem — e a sem regra é justamente a que ninguém lembra de conferir,
  porque a existência da outra dá a sensação de que o assunto está coberto. É primo do que
  a parcela 61 corrigiu na fila (`EditarAgenda` de um lado, `VerAgenda` do outro), com o
  agravante de o lado frouxo aqui não ter regra NENHUMA, e de a garantia ser a que a
  cliente está auditando.
  As decisões: a leitura de consentimento é **em lote** (`PacientesComConsentimentoVigente
  Async`, a MESMA da campanha — nunca uma segunda definição), e quem não consentiu
  **continua na lista, contado, com o motivo escrito na linha** e o botão apagado. Sumir
  com ele seria pior: some da lista, some da cabeça, e a tarefa que destrava o telefonema
  (colher o consentimento no balcão) deixaria de existir para a direção.
  ⚠️ E o teste não podia morar no ViewModel (WPF não compila no projeto de teste): o que
  `RetencaoConsentimentoTests` fixa é que os **dois lados perguntam a mesma coisa e recebem
  a mesma resposta**, revogação incluída. **Ao achar o mesmo ato em duas telas, escreva o
  teste que compara as duas** — não o que prova que a que você acabou de arrumar funciona.

- **Trocar de paciente rápido mistura os dados de DOIS pacientes na tela de conformidade**
  (parcela 64): `GuardaProntuarioViewModel` fazia duas idas ao banco em sequência (a guarda
  e a trilha de leitura) disparadas pelo `Selecionado` do seletor, **sem contador de
  geração** — e o seletor é uma BUSCA, então trocar de pessoa várias vezes é o uso normal
  de quem investiga um acesso indevido. Num banco remoto a leitura velha responde depois da
  nova: a guarda de um paciente sob o nome de outro, ou a trilha da Maria listada na ficha
  do João. A regra da parcela 60 já valia para "toda tela que dispara leitura a cada tecla
  ou clique"; o que esta parcela acrescenta é **onde doer mais**: numa tela que existe para
  responder auditoria, a resposta errada tem exatamente a mesma cara da certa. A vizinha
  (`AuditoriaViewModel`), com o mesmo seletor, já tinha o contador — **duas telas com o
  mesmo componente e só uma com a guarda é o sinal de que a varredura da 60 passou por
  alto**.

- **A mensagem de ÊXITO invisível não é uma tela: é o padrão do arquivo ao lado** (parcela
  64). A parcela 62 achou o defeito em cinco janelas da Recepção — `<Border AlertaPerigo
  Visibility="{Binding MensagemEhErro}">` esconde junto a mensagem que zera o booleano — e
  o corrigiu lá. As **20 telas do Gerente** estavam todas no padrão antigo, com oito
  mensagens que nunca apareceram: o estado vazio das Campanhas ("Gere uma rodada acima para
  começar"), "Nenhum usuário cadastrado", "Nenhum horário na agenda neste período" e — a
  pior — **"Exportação gravada em {destino}"**, na tela que exporta o prontuário da clínica
  inteira: a direção clicava, esperava, e a tela não dizia nada nem onde havia gravado.
  A correção é a mesma frase da 62 — **quem decide se aparece é o texto; quem decide a cor
  é a gravidade** —, e a lição é sobre o ALCANCE: quando um defeito de padrão é corrigido
  numa tela, **procure o mesmo par de linhas nos outros módulos antes de dar a parcela por
  fechada**. Quatro destas telas já usavam `AlertaAviso` em vez de `AlertaPerigo`, o que
  mostra que alguém percebeu que a mensagem era informativa e não percebeu que ela nunca ia
  aparecer.

- **A checagem que existe para pegar o defeito passava por cima dele** (parcela 64,
  checagem 20). A tela de preço por convênio oferecia **"ClinicaDaDor"** no seletor de
  especialidade — o defeito da parcela 41, na tela do Gerente, com a checagem verde. A
  causa é um caractere: a coleção é `IReadOnlyList<Especialidade?>` (anulável porque o nulo
  é a opção "todas"), e a expressão da checagem só casava `<Especialidade>`. O WPF chama
  `ToString()` igual nos dois casos.
  Alargá-la para `<Tipo?>` custou **zero ruído** — a varredura achou UMA ocorrência em toda
  a suíte, que era o próprio defeito. A lição: **checagem cega é pior que checagem ausente,
  porque ela responde "está limpo"**; e a hora de medir o ruído de um alargamento é ANTES
  de decidir não fazê-lo. Autotestada nos dois sentidos (dispara com o `ItemTemplate`
  removido, cala com ele posto), pela regra da checagem 34.

- **Varredura de permissão: conte os IRMÃOS, não os comandos** (parcela 64). `Dispensar
  Async` era o único dos quatro comandos de escrita de `CampanhasViewModel` sem `Exigir` —
  e é o que faz o contato **sumir da fila sem que ninguém tenha falado com o paciente**, a
  mesma família de `MarcarNaoConformidade` no faturamento. Nas exportações CSV faltavam as
  duas: a trilha de auditoria (que leva nome de paciente desde a parcela 52) e a lista de
  sumidos (nome e TELEFONE) saíam para arquivo sem a segunda barreira, num módulo onde
  todas as outras escritas a tinham. **Quando três comandos vizinhos têm a guarda e um não,
  o que está errado é o um** — e o CSV conta como saída de dado, que foi a lição da parcela
  60 aplicada ao export clínico.

- **Ver o número e DECIDIR sobre ele são bits diferentes** (`Permissao.DefinirMetas`,
  parcela 64): a tela de Metas exigia `VerIndicadores` — o bit de LER o BI — para criar e
  para APAGAR o alvo do mês. É o bit sobrecarregado da parcela 49 de novo, num par que
  parece o mesmo assunto e não é: o realizado é FATO, e a meta é a DECISÃO da direção
  sobre o fato. Enquanto os dois moraram no mesmo bit, dar acesso de leitura aos números a
  alguém ("o financeiro pode ver") entregava junto o poder de apagar as metas do ano — e
  meta apagada não deixa buraco visível: o painel volta a comparar com o mês anterior, que
  responde "melhorou?" e nunca "chegamos onde a gente disse que ia chegar?".
  ⚠️ Diferente da parcela 49, **esta separação não tira nada de ninguém**: nenhum perfil
  padrão além do Gerente tinha `VerIndicadores`, e o Gerente recebe `Todas` — o bit novo
  chega ligado a quem já definia meta ontem. O que ele acrescenta é a possibilidade de
  conceder o BI sem conceder o alvo, que é o pedido da direção na 49 aplicado ao lugar
  onde ainda não estava. `Ver_indicadores_nao_da_o_direito_de_definir_meta` falha se
  alguém os juntar de volta, inclusive pelo caminho discreto (acrescentar o bit ao padrão
  de um perfil que só deveria ler).

- **Teto posto no elemento errado não encolhe o conteúdo: ele o DECEPA** (parcela 64 — o
  cliente mandou a foto do mapa corporal com "Repetir a anterior" e "Limpar" meio visíveis,
  encavalados no resumo). O `MaxHeight="260"` estava no `DockPanel` do painel inteiro, e
  não no `ScrollViewer` da lista de pontos, que é o único filho que cresce sem limite.
  Protocolo, campo de observações e a linha de ações somam mais de 260 px sozinhos — então
  o `StackPanel` do topo era cortado, e o que ficava fora do corte eram justamente os dois
  botões e a frase "Nenhum ponto marcado". A pergunta que decide onde o teto vai: **qual
  filho cresce com o DADO?** É nele. Os de altura conhecida não podem ser cortados.
- **Três respostas para a mesma pergunta se leem como sobreposição** (parcela 64, tela
  "Quem me deve"): o resumo ao lado do combo dizia "Nenhuma conta de paciente vencida", uma
  faixa verde `AlertaSucesso` repetia a MESMA frase, e o `EstadoDaTela` dizia "Ninguém
  devendo" com desenho. O cliente descreveu como faixa sobreposta no lugar errado, e era
  isso mesmo: o `EstadoDaTela` estava na RAIZ da tela, cobrindo KPIs, filtro e a coluna de
  envelhecimento, e caía por cima da faixa verde. Duas correções, e as duas são regras
  velhas: **"um estado vazio por pergunta"** (parcela 37) e **a sobreposição pertence à
  REGIÃO cujo vazio ela explica, nunca à página** (parcela 58, que já a tinha corrigido
  noutras telas e não nesta).
- **Coluna elástica ao lado de coluna FIXA dá o excesso todo para a elástica** (parcela 64,
  Conciliação): `Paciente` era `*` e `Convênio` 170 px fixos, então numa tela larga o nome
  do paciente ganhava meio palmo de branco enquanto "Unimed Costa do Sol Intercâmbio" saía
  truncado ao lado — e o número da guia colava nele. Quando DUAS colunas têm conteúdo de
  tamanho imprevisível, as duas são estrela e o que se escolhe é a PROPORÇÃO (`2*` e
  `1.3*`, com piso); fixa fica só para o que tem tamanho conhecido — data, número, campo de
  digitar. E célula de tabela precisa de respiro: sem margem, "Unimed Costa do
  Sol Inte…37034962" se lê como uma coisa só.
- **A checagem 20 tinha um SEGUNDO ponto cego: o enum da camada de APLICAÇÃO** (parcela 64
  — o cliente viu "MaisAntigo" e "MaiorValor" no seletor de "Quem me deve"). A função que
  monta a lista de enums varria só `src/Clinica.Domain`, e `OrdemInadimplencia` mora em
  `Clinica.Application/Servicos` — o WPF chama `ToString()` sem se importar com a camada em
  que o enum nasceu. Custo de alargar, medido ANTES: uma ocorrência em toda a suíte, que
  era o próprio defeito. É o mesmo desfecho do ponto cego do enum anulável, na mesma
  parcela, e a lição se repete de propósito: **quando uma checagem responde "está limpo",
  pergunte primeiro o que ela não olha** — e meça o ruído antes de decidir não alargar.

- **O termo que o PACIENTE assina, e a pergunta que decidiu o desenho inteiro** (parcela
  66): a cliente pediu "algo como o SmartDocs" para o BSV — o consentimento do
  procedimento e a **declaração de jejum**. O pedido chegou como um item só e são **dois
  documentos de naturezas opostas**, e é essa separação que resolve tudo. O consentimento
  do procedimento ganha em ser lido em casa, com calma. A declaração de jejum afirma
  "**ESTOU** em jejum": assinada na véspera ela é uma declaração sobre o FUTURO, e o valor
  dela é ser sobre o presente. **Isso derruba o SmartDocs como resposta ao caso trazido** —
  o link no celular resolve o primeiro documento e é inadequado justamente para o segundo,
  que foi o que motivou o pedido. Daí a assinatura no BALCÃO, onde o paciente do BSV já
  está: `InkCanvas` é de fábrica no WPF, não exige e-CPF, não exige internet e não cria
  deployable novo. **A pergunta que decide não é "que tecnologia o concorrente usa", é
  "sobre QUANDO este documento afirma alguma coisa".**
  ⚠️ **O paciente NÃO assina com certificado, e isso não é limitação.** Termo de
  consentimento é documento ENTRE AS PARTES (MP 2.200-2/2001, art. 10, §2º) — a Lei
  14.063/2020 chama isso de assinatura SIMPLES. Exigir e-CPF do paciente seria inviável e
  desnecessário. **O que dá valor a ela é EVIDÊNCIA**: quem, quando, diante de quem, com
  que documento conferido, e o SHA-256 do que ele tinha na frente — declarações respondidas
  incluídas, senão o selo deixaria de fora justamente a parte que se contesta ("eu nunca
  disse que estava em jejum"). E o rodapé escreve "assinatura eletrônica simples", jamais
  "digital": é a regra do carimbo escaneado, da parcela 3.
  **A ordem das duas assinaturas é amarra técnica, não preferência**: o PDF não se assina
  incrementalmente (a restrição que a parcela 42 já encontrou), então o traço do paciente
  tem de estar DENTRO dos bytes ANTES do selo ICP-Brasil do profissional. Colher depois
  produziria, em silêncio, um arquivo cujo selo não fecha — a garantia aparente que este
  projeto recusa. `ColherAsync` RECUSA documento já assinado eletronicamente.
  **"Não estou em jejum" NÃO impede, e é decisão**: o termo é emitido do mesmo jeito, com a
  resposta escrita, e acende alerta VERMELHO pelo `ElegibilidadeService`. Bloquear
  produziria o desfecho pior — ninguém emite o termo, o procedimento acontece assim mesmo e
  não sobra registro nenhum. Quem decide adiar é quem faz o procedimento.
  **Validade POR SESSÃO, sem campo de prazo** (decisão da clínica): a chave de "já
  assinou?" é a DATA. Um "vale por N dias" existiria para nunca ser usado, e regra com
  exceção que ninguém exerce é código a mais e mais uma resposta possível para a mesma
  pergunta (a lição do CPF duplicado, parcela 57).
  **O TEXTO é da clínica, não nosso**: `ModeloDocumento` com as declarações como
  `ItemModelo`, escrito em Configurações → "Escrever termos…". Não há termo de fábrica —
  um texto de consentimento embutido seria o sistema opinando sobre risco clínico. E
  aplicar **COPIA**, como o protocolo do mapa corporal; aqui não é desenho, é a Lei
  13.787/2018: referência viva faria corrigir uma palavra hoje reescrever o que um
  paciente assinou no mês passado. Por isso `DocumentoClinico.ModeloOrigemId` é
  PROCEDÊNCIA — e casar por MODELO, não por tipo, é o que permite dois procedimentos no
  mesmo dia exigirem dois termos sem um cobrir o outro.
  ⚠️ **`TipoDocumentoClinico.TermoProcedimento` não reaproveita o `Consentimento`**, que é o
  termo **LGPD** montado das finalidades: seria o bit sobrecarregado da parcela 49 num
  papel — sem como conceder um sem o outro, e a segunda via de um sairia com o texto do
  outro. E a folha nova nasce **fechada** em `FolhaCatalogo` (a regra da parcela 59).
  **A leitura tem DOIS caminhos e UMA definição**: a fila lê 30 cartões em 3 consultas
  (`DoDiaAsync`) e a ficha lê um (`SituacaoDoDiaAsync`), e os dois resolvem pela MESMA
  função privada. Duas definições de "falta assinar" divergiriam na primeira correção, e a
  que ninguém lembraria de ajustar é a do quadro — onde o erro aparece como **cartão
  limpo**, indistinguível de termo em dia. `Leitura_em_lote_da_fila_concorda_com_a_leitura_
  por_paciente` é a amarra.
  **A porta fica na fila do balcão**, no "⋯" do cartão: o alerta do check-in diz que falta
  assinar, e alerta sem porta no mesmo app é pior que alerta nenhum (parcela 48). A janela
  mora no **shell**, como o mapa corporal — ela é apresentada em três lugares e copiá-la
  daria três telas divergindo na primeira correção.
  **Lição de teste**: `Os_sete_documentos_geram_PDF` cobrava `TipoDocumentoInfo.Todos` e
  falhou ao nascer o oitavo tipo — foi a rede que impediu um documento sem PDF de passar no
  build e só quebrar na frente de quem fosse imprimi-lo. **Asserção contra a lista COMPLETA
  de um enum é o que faz o tipo novo cobrar a própria cobertura.**

- **A porta do termo na FICHA, e as dezesseis coisas que uma revisão adversarial achou
  numa parcela que já estava verde** (parcela 66, 2ª rodada). O cliente pediu para colher o
  termo dentro da ficha do paciente — a porta nasceu na fila, e a ficha é onde a
  recepcionista já está com a pessoa na frente. Virou uma seção no TOPO da aba Documentos
  (não aba nova: a ficha já responde seis perguntas), e a decisão que a governa é a mesma
  da entidade: **ela só mostra o dia de hoje**. Sem procedimento marcado, a seção troca de
  frase em vez de sumir, e explica que o termo é colhido no dia — colher "para amanhã"
  produziria um papel que o balcão não veria como cumprido amanhã, que é pior do que não
  ter porta, porque a pessoa acreditaria ter resolvido.
  ⚠️ **A lição maior não é a porta: é que a parcela estava com 1531 testes verdes, três
  redes locais verdes, e tinha DEZESSEIS defeitos reais.** Nenhum deles quebrava build ou
  teste. Vale a pena listar as famílias, porque elas se repetem:
  (a) **Dado de saúde sem barreira na tela nova** — a seção da ficha era lida por
  Financeiro e Faturista, que têm `VerFichaPaciente` e não têm `VerProntuario`. A lista de
  documentos ao lado já filtrava por acesso; a seção nova, não. **Ao acrescentar região a
  uma tela existente, copie a barreira da região vizinha.**
  (b) **Carga que falha deixa a tela do paciente ANTERIOR** — o `catch` logava e saía, e
  as linhas de quem já tinha saído continuavam ali com o botão aceso apontando para o
  `DocumentoId` dele. Um clique assinaria o termo de outra pessoa. **Limpe ANTES do await,
  não depois**, e ponha terceiro estado.
  (c) **O selo que regera o arquivo sem a parte selada** — `AssinaturaDeDocumentoClinico
  Service` gerava o PDF para assinar SEM passar o traço do paciente, e como a reimpressão
  devolve os bytes guardados, o termo selado perderia a assinatura do paciente PARA SEMPRE.
  É a inversão exata da regra que a própria parcela documentou.
  (d) **Formulário incompleto gravado como cumprido** — declaração sem resposta virava
  "Assinado hoje" sem alerta nenhum. Recusar em branco não contradiz o "avisa, mas não
  impede": aquilo vale para o CONTEÚDO da resposta ("não estou em jejum" é registrado e o
  procedimento segue sendo decisão de quem o faz); o que se impede é o campo vazio.
  (e) **Rota que cai no `default:`** — a folha nova na central caía na janela genérica de
  documento, que não conhece o tipo: papel numerado sem modelo de origem e sem declarações.
  A saída foi a forma que o RECIBO já usava (`ExigenciaFolha.ProcedimentoDoDia`: o cartão
  LEVA até onde se colhe, e o rótulo diz "Abrir a ficha" em vez de "Emitir"), mais uma
  recusa no construtor da janela genérica — **a segunda barreira para a próxima porta**.
  (f) **Mensagem de erro que manda fazer o que a tela não faz** — "troque o modelo da
  exigência que existe" e não havia por onde. `ExigirAsync` passou a TROCAR. É seguro
  porque aplicar COPIA: o termo assinado guarda o texto lido e o `ModeloOrigemId` antigo.
  (g) **`NULL` não é único no PostgreSQL** — o índice `(Modalidade, ModalidadeCodigo)`
  ficava inerte justamente no caso NORMAL (código nulo = família inteira), e dois cliques
  concorrentes criariam duas exigências, fazendo o paciente assinar o mesmo papel duas
  vezes. Família passou a gravar **string vazia**.
  (h) **Hash gravado que ninguém recalcula é um número** — `ConteudoIntacto` existia sem
  chamador em produção, e o rodapé afirmava o selo. Agora o PDF **recalcula na impressão** e
  a segunda via de um termo alterado sai dizendo que não prova o que foi assinado. A
  montagem do selo desceu para a ENTIDADE, porque quem precisa dela são dois (o serviço que
  grava e o PDF que confere) e duas montagens divergiriam.
  (i) **Alerta em data futura é alerta impossível de atender** — a conferência do termo
  roda também no formulário de agendamento, que pergunta pela data MARCADA. Marcar um BSV
  para o mês que vem acendia vermelho sem ter o que fazer. Restrito a hoje.
  (j) **Dado calculado que nenhuma tela mostra** — `CartaoFila.TemTermoPendente` era
  computado para todo cartão e só aparecia dentro do menu "⋯". Virou selo.
  (k) **`IsEnabled` de bloco apagando a permissão de outro ato** — o "⋯" seguia
  `PodeEditarAgenda`, e a técnica de enfermagem, que tem `ColherAssinaturaPaciente` e não
  tem o da agenda, não alcançava o único caminho para colher. O bloco perdeu o `IsEnabled`;
  quem decide item a item é o menu, e o botão de PASSO ficou com a permissão que é dele.
  (l) **Alerta sem porta no app de quem o lê** — o Consultório recebia "falta o termo" com
  o paciente na sala e não tinha botão. Ganhou a faixa com a MESMA janela do shell.
  ⚠️ E a **lição de método**: o script da revisão tinha um bug meu (passei promessas a
  `parallel`, que espera thunks), então a fase de verificação morreu e o workflow devolveu
  `{confirmados: [], descartados: []}` — **vazio por defeito, indistinguível de "nada
  encontrado"**. É o defeito recorrente do projeto cometido na própria ferramenta de achar
  defeitos. Os achados estavam no `journal.jsonl` o tempo todo. **Resultado vazio de
  workflow é para ser investigado no journal, nunca lido como aprovação.**

- **Checagem que cobre UM sentido de um erro simétrico está metade cega** (parcela 66, 3ª
  rodada — o CI reprovou a PR). A `ModelosTermoWindow` do Gerente declarou
  `xmlns:ctrl="clr-namespace:Clinica.Desktop.Controls"` **sem** `;assembly=`, e o tipo mora
  no shell: `MC3074`. A **checagem 33** existe exatamente para esta família e não viu,
  porque ela nasceu na parcela 60 pegando o `;assembly=` que **SOBRA** (tela movida entre
  projetos) — e este é o mesmo erro pelo avesso, o `;assembly=` que **FALTA**.
  Nenhuma rede local pegava, pela razão de sempre: o XML é bem-formado, o
  `compilar-sombra` não lê o corpo do XAML e o C# compila. Virou a **checagem 33-B**, que
  casa cada `clr-namespace:X` sem sufixo contra os namespaces que o projeto DECLARA nos
  `.cs` dele — projeto sem `.cs` lido responde "não sei" e cala, como a 34. Autotestada
  contra o caso real e contra os dois legítimos (shell e faturamento, que declaram
  `Clinica.Desktop.Controls` cada um no seu — o débito permanente da parcela 7).
  A regra que fica, e que vale para toda checagem futura: **ao escrever uma rede para um
  erro que tem dois sentidos, cubra os dois no mesmo commit.** O sentido que você deixar de
  fora é o que a próxima pessoa vai cometer — aqui foram seis parcelas até alguém tentar o
  outro lado.

- **"Regra com exceção que ninguém vai exercer" tem prazo de validade — e ele acabou antes
  de a feature chegar à clínica** (parcela 66, 3ª rodada). A validade do termo nasceu POR
  SESSÃO, sem campo de prazo, com o argumento (bom, e escrito) de que um "vale por N dias"
  existiria para nunca ser usado. A cliente exerceu a exceção **antes do primeiro uso**: ela
  quer colher a assinatura quando o paciente aparece — inclusive na consulta em que ele vem
  tirar dúvidas, semanas antes — e emitir pela central de documentos, sem esperar o dia.
  E ela tem razão pelo argumento que o próprio desenho já continha: **o dia do procedimento
  é justamente quando ninguém tem tempo de ler o termo.**
  O que a distinção original acertou, e que sobreviveu: as DECLARAÇÕES moram dentro do termo,
  e nem toda declaração sobrevive à antecedência — "estou em jejum" assinado na semana
  passada é afirmação sobre o futuro. Por isso a validade virou **escolha por procedimento**
  (`ExigenciaTermoProcedimento.SoValeNoDiaDoProcedimento`, desmarcada por padrão) em vez de
  desaparecer: a clínica escreve o consentimento longo sem prazo e, se quiser, um termo
  curto só com o jejum marcado como "a cada sessão". Os dois convivem porque a exigência é
  por **MODELO**, não por tipo.
  ⚠️ **Seja qual for a validade, RECUSA e papel pendente contam só no DIA.** Recusa é decisão
  de um momento, não estado permanente: herdá-la faria um "não" de três semanas atrás calar
  o pedido no dia do procedimento — e o paciente pode ter mudado de ideia, tanto que veio
  fazer. E o papel emitido e nunca assinado carrega a DATA da emissão: reusá-lo faria a
  assinatura de hoje nascer com data velha, que numa exigência "só no dia" não contaria
  nunca.
  De quebra, as portas viraram QUATRO (ficha, fila, Consultório, central) e passaram a
  entrar por um ponto único — `ColetaDeTermo.Abrir`, no shell. Cada uma montava a janela por
  conta própria (escopo, ViewModel, dono, recarga), e quatro montagens divergem na primeira
  correção; o que elas colhem é a prova de que o paciente consentiu. Quando o modelo não vem
  decidido por um procedimento marcado, o ponto único PERGUNTA qual termo é — que é o
  caminho da coleta avulsa.
  A lição, e ela é sobre método: **"não construa a exceção que ninguém vai exercer" é uma
  boa regra para código especulativo, e não para uma decisão que o cliente ainda não tomou.**
  Quando a exceção depende de como a clínica trabalha, pergunte antes de fechá-la.

- **Tela do paciente: o modelo da MAQUININHA, e por que ela não é um tablet** (parcela 66,
  5ª rodada — *"há um dispositivo para assinar via touchscreen … quero que o dispositivo
  ligue na hora aparecendo o lugar certo para o paciente assinar"*). A cliente escolheu
  **duas telas: ela controla, ele só assina**, e é o desenho da maquininha do cartão — não
  um espelho da janela da recepcionista. São dois conteúdos diferentes: aqui as declarações
  com Sim/Não, o documento conferido e o Confirmar; lá o texto GRANDE, as respostas como
  ela as marcou e a área de assinar.
  ⚠️ **A área de assinar SOME desta janela quando há a do paciente.** Duas áreas ativas
  permitiriam a recepcionista assinar pelo paciente sem querer — e o termo diria que ele
  assinou. No lugar dela fica a linha que explica a ausência: campo que some sem explicação
  se lê como defeito.
  ⚠️ **A resposta marcada aqui muda a tela dele NO MESMO INSTANTE**, e isso não é conforto:
  o selo do termo é o SHA-256 do que o paciente tinha na frente, declarações incluídas. Uma
  tela atrasada faria a evidência afirmar algo que não é verdade.
  **O que se grava é o NOME do dispositivo (`\\.\DISPLAY2`), nunca o índice na lista.** O
  índice muda quando alguém desliga um cabo ou o Windows reordena as telas depois de um
  reinício — e a tela do paciente viraria a da recepcionista, com o termo em tela cheia por
  cima do trabalho dela, sem ninguém ter mexido em nada. Tela gravada e ausente responde
  **"não há segunda tela"**, jamais "use a primeira que aparecer", e a configuração escreve
  o terceiro estado: silêncio faria a clínica concluir que a feature quebrou quando o que
  houve foi um cabo solto.
  **Sem segunda tela, TUDO continua numa janela só, e isso não é modo degradado**: é o modo
  de quem tem um monitor. A clínica pode ficar meses sem comprar o touch, e a feature não
  pode esperar por isso — foi o que decidiu o `ParametrosService` devolver **nulo** para a
  chave vazia, em vez de uma string que o sistema procuraria como nome de monitor.
  **O botão "Testar" é obrigatório, não conforto**: as telas se chamam `\\.\DISPLAY1` e
  `\\.\DISPLAY2` e ninguém sabe qual é qual pelo nome. Sem ele, o primeiro a descobrir que o
  termo abriu no monitor errado seria o PACIENTE, vendo o próprio nome e o procedimento dele
  numa tela virada para a sala de espera. O exemplo não leva dado de paciente nenhum, pela
  razão do arquivo de teste da publicação (parcela 53).
  **Por que MONITOR e não tablet**: um tablet é outro computador — exigiria servidor web,
  rede confiável no balcão e um caminho de volta para o traço, que é a Fase 2 inteira. Um
  monitor touch é só mais uma tela do Windows: o traço nasce dentro do mesmo processo que
  grava o termo, sem rede entre a caneta e o banco. A especificação do que comprar está em
  `docs/termo-assinado-pelo-paciente.md` §3.8.
  Detalhes que o código não conta sozinho: a janela dele é `Topmost`, sem barra de título e
  **dona desta** (painel órfão no balcão é convite para alguém assinar o termo de outra
  pessoa); o monitor é **impedido de dormir** enquanto o termo está no ar
  (`SetThreadExecutionState`), senão a proteção de tela apaga o documento no meio da leitura
  de quem lê devagar; e a enumeração de monitores é **P/Invoke puro** (`EnumDisplayMonitors`),
  sem WinForms, em pixels FÍSICOS — num arranjo de DPIs diferentes, posicionar em unidades
  do WPF põe a janela na tela errada.

- **Um procedimento exige VÁRIOS termos, e a chave antiga apagava o primeiro em silêncio**
  (parcela 67). A cliente pediu os termos do BSV prontos, e ao criá-los apareceu o defeito:
  `ExigenciaTermoProcedimento` tinha chave única `(Modalidade, ModalidadeCodigo)`, então
  amarrar o **segundo** papel TROCAVA o primeiro. O BSV precisa de dois, e a razão é a mesma
  que desenhou a parcela 66 inteira: o **consentimento** vale a partir da assinatura (lido
  com calma, dias antes) e a **declaração de jejum** não se herda (é sobre o dia). São
  validades OPOSTAS e não cabem no mesmo papel.
  O mais revelador é que **a leitura sempre soube devolver vários** — `Resolver` percorre as
  exigências e o balcão lê uma LISTA, com o comentário dizendo "é o que permite dois
  procedimentos no mesmo dia exigirem dois termos sem um cobrir o outro". Quem não deixava
  era a ESCRITA. **Quando um lado do sistema fala no plural e o outro no singular, o que
  está errado é quase sempre o singular** — e o erro não aparece: a exigência antiga
  simplesmente some da lista.
  A chave passou a incluir `ModeloDocumentoId`. Repetir a MESMA amarração continua não
  recusando (a lição da 2ª rodada da 66 — mensagem de erro que manda fazer o que a tela não
  faz), só que agora ela atualiza a VALIDADE; trocar de modelo é ligar o novo e desligar o
  antigo, dois cliques **visíveis** — e visível é o ponto, porque a troca automática
  acontecia sem a lista mostrar o que sumiu.
- **Rascunho revisável não é termo de fábrica** (parcela 67): a regra da parcela 66 é "o
  texto é da clínica, não nosso — texto embutido seria o sistema opinando sobre risco
  clínico", e ela continua valendo. O que a prática mostrou é que **a lista nascer vazia não
  fez ninguém escrever um termo do zero no meio do expediente — fez o BSV continuar
  acontecendo sem termo nenhum.** O que a regra proíbe é o texto que o sistema aplica
  SOZINHO; o que passou a existir é um rascunho que alguém **pede, lê e edita**: botão em
  Configurações (nada em migration, nada na abertura), nome com "(rascunho — revisar)" e
  primeira linha do corpo mandando o responsável técnico conferir e apagar o aviso ao
  aprovar. Criar o modelo **não exige nada de ninguém** enquanto a amarração não for feita, e
  recriar é recusado — sobrescrever apagaria a revisão que a clínica já fez, que é o trabalho
  que o botão existe para começar.
- **A saída CONSCIENTE da checagem 18** (parcela 67): alargar uma chave única é `DropIndex` +
  `CreateIndex`, e não perde dado — toda linha que passava na chave antiga passa na nova. Mas
  a regra não podia virar "`DropIndex` pode": o mesmo drop usado para ESTREITAR quebra a
  clínica no dia seguinte, e **a diferença entre os dois casos não está na operação, está na
  intenção de quem a escreveu**. Por isso a exceção é DECLARADA (`MIGRATION-NAO-ADITIVA-
  CONSCIENTE: <razão>` no arquivo) e o preço dela é escrever a razão; marca sem razão não
  vale, porque a razão É a exceção e não um interruptor. E ela **nunca fica silenciosa**:
  vira aviso em toda execução, inclusive no CI — exceção que some da saída é exceção que
  ninguém revisa, e a próxima pessoa a copiar a migration como modelo precisa ver que ali
  houve uma decisão, não uma permissão. Autotestada nos três cenários (sem marca, com marca
  e razão, marca vazia).

- **A dispensa da checagem 18 tem de ser por OPERAÇÃO, não por ARQUIVO** (parcela 67, 2ª
  rodada — o achado mais grave da revisão adversarial do próprio diff). A saída consciente
  nasceu certa na intenção e errada no alcance: a marca valia para o arquivo INTEIRO, então
  bastava um `DropIndex` inofensivo declarado para um `DropColumn` acrescentado DEPOIS, na
  mesma migration, passar junto — e a ferramenta ainda imprimia, como justificativa dele, a
  frase que falava do índice e afirmava "nenhuma linha se perde". **Garantia falsa no log do
  CI é pior do que checagem nenhuma**, e o caminho é o realista: a migration marcada é
  justamente a que a próxima pessoa copia como modelo. A marca passou a NOMEAR o que cobre
  (`MIGRATION-NAO-ADITIVA-CONSCIENTE(DropIndex): razão`); o que não estiver na lista continua
  erro. A regra geral: **exceção declarada delimita o que dispensa, senão ela dispensa o
  vizinho.**
- **`AlterColumn` nunca disparou a checagem 18 — o EF gera a forma GENÉRICA** (parcela 67,
  2ª rodada). A busca era `.AlterColumn(` e o EF emite `migrationBuilder.AlterColumn<string>(`.
  A operação **mais** destrutiva da lista (encolher `maxLength`, tornar coluna NOT NULL) era
  letra morta desde que a checagem nasceu, e ninguém notou porque ela só cala — não erra.
  Casar `Op(` **ou** `Op<`. A lição: **quando uma checagem procura uma chamada de método,
  confira como a ferramenta que gera o código a escreve de verdade**, não como você a
  escreveria.
- **Autoteste que REIMPLEMENTA a lógica não testa nada** (parcela 67, 2ª rodada): o da saída
  consciente repetia a leitura da marca linha a linha em vez de chamar a função da checagem.
  Ele ficaria verde exatamente quando a checagem quebrasse, porque a cópia dentro dele não
  quebra junto — é a variante mais discreta do defeito recorrente do projeto, aplicada à
  rede que existe para pegá-lo. **Autoteste chama o código que roda.**
- **Idempotência ancorada em campo EDITÁVEL não é idempotência** (parcela 67, 2ª rodada): o
  botão dos termos do BSV guardava-se contra o segundo clique comparando o NOME do modelo —
  e o nome é justamente o que o desenho pede que mude, porque a marca "(rascunho — revisar)"
  mora nele para ser apagada na aprovação. Renomeado, o segundo clique criava outro par e
  ligava mais quatro exigências, que a chave alargada da mesma parcela deixa CONVIVER: o BSV
  passaria a cobrar quatro papéis, dois deles o rascunho não revisado, e assinar um par não
  zeraria o outro. **A pergunta certa não era "este texto já existe?", era "o BSV já está
  configurado?"** — a guarda olha a EXIGÊNCIA. E, de quebra, isso a tornou RESUMÍVEL: são
  seis gravações contra banco remoto sem transação, e o segundo clique agora completa o que
  faltou em vez de recusar tudo por causa da metade que ficou.
- **Declaração de termo é redigida para que "Não" seja um SINAL** (parcela 67, 2ª rodada):
  responder "Não" acende alerta VERMELHO no balcão e no consultório, então (a) declaração
  cujo "Não" é NORMAL dilui o alerta — havia um "estou acompanhado, se a clínica exigir",
  que faria metade dos pacientes acender vermelho e treinaria todo mundo a ignorar o do
  jejum; e (b) declaração NEGATIVA ("não tive febre") torna a resposta ambígua, porque "Não"
  vira dupla negação. Todas afirmativas e incondicionais. E o **`Detalhe` sai IMPRESSO na via
  do paciente**: instrução para a equipe ali ("confira com o paciente quantas horas") produz
  um documento que fala do leitor na terceira pessoa.
- **Valor NOVO de enum é a única coisa que o app velho não consegue ler do banco
  compartilhado** (parcela 67, 3ª rodada — o cliente mandou a foto da tela de Prescrições
  morta, com *"Cannot convert string value 'TermoProcedimento' from the database to any
  value in the mapped 'TipoDocumentoClinico' enum"* no lugar da lista). Os cinco apps se
  auto-atualizam por Velopack, **um canal por app**, e dividem UM banco: a janela em que o
  Consultório já atualizou e a Recepção não é o DESENHO, não o acidente.
  Coluna nova e tabela nova atravessam essa janela sem incidente — o EF só pede as colunas
  que conhece. **Valor de enum, não**: o `HasConversion<string>()` chama `Enum.TryParse` e,
  ao não achar o nome, LANÇA. E não falha a LINHA, falha a **CONSULTA** — a clínica perde a
  tela inteira por causa de um registro, com uma frase em inglês que não diz a ninguém que
  o que falta é atualizar o programa. São 76 `HasConversion<string>()` no contexto; cada um
  é a mesma mina.
  `ConversorEnumTolerante<TEnum>` tem **duas metades, e uma sem a outra é pior que nada**:
  **ler é tolerante** (o nome desconhecido vira o sentinela, a linha aparece e a tela abre)
  e **escrever é RECUSADO**. Sem a segunda, o app velho leria "não sei o que é isto" e no
  primeiro Salvar gravaria o sentinela **por cima do tipo verdadeiro** — apagando em
  silêncio o que a versão nova registrou. Registro clínico não se apaga (Lei 13.787/2018),
  e apagar por conversão é a forma mais discreta de fazê-lo. A recusa mora no CONVERSOR
  porque a escrita tem muitas portas (emitir, cancelar, salvar modelo) e uma que esquecesse
  bastaria — a razão de sempre.
  ⚠️ **Cair no `default` do enum seria pior do que estourar.** Um termo de procedimento
  apareceria como "Receita": mentir sobre um registro de prontuário, em silêncio, é o
  desfecho que este projeto recusa desde a parcela 3. O sentinela `Desconhecido` diz o que
  há **e o que fazer** ("Tipo não reconhecido — atualize o sistema"), fica **fora de
  `TipoDocumentoInfo.Todos`** (não é papel nenhum) e **não imprime** sem os bytes guardados
  — a folha sairia com cabeçalho, número e assinatura e sem o miolo, que é a garantia
  aparente de novo.
  O conversor foi aplicado a `TipoDocumentoClinico` — o enum que quebrou, que CRESCE (sete
  tipos até a 65, oito na 66) e que os cinco apps leem. Enum que ganhar valor novo e for
  lido por outro app entra nele; enum estável não precisa, e sentinela espalhado por todos
  teria a blast radius conhecida do `Enum.GetValues` dos catálogos.
- **Quem diz onde o DER termina é o CABEÇALHO dele, nunca o enchimento** (parcela 67, 4ª
  rodada — a clínica levou *"A assinatura foi produzida mas não conferiu: … (ASN1 corrupted
  data)"* na primeira assinatura em nuvem que passou do decode). O `/Contents` do PDF é
  dimensionado para o pior caso ANTES de assinar (32 KB) e completado com zeros à direita;
  na volta, `LerConteudoAssinatura` cortava o enchimento com `TrimEnd('0')` — que tira
  **caracteres '0', não bytes zero**. Uma assinatura terminada em `0x00` perdia o último
  byte: os dois caracteres sumiam, o comprimento continuava PAR e o remendo de nibble ímpar
  (`if (hexa.Length % 2 == 1) hexa += "0"`) não repunha nada.
  ⚠️ **O comentário ao lado do defeito já dizia a solução** — "que é DER e sabe onde
  termina" — e o código cortava por enchimento assim mesmo. Agora `RecortarDer` lê tag e
  comprimento e fatia exatamente.
  A aritmética é o que explica por que isto chegou à clínica: o último byte de um CMS é o
  último byte da assinatura RSA, ou seja é **sorteado** — uma folha a cada 256. Raro o
  bastante para nunca cair num teste (o `Assinatura_confere_no_arquivo_intacto` passava
  255 vezes em 256, e ninguém viu a 256ª), frequente o bastante para acontecer em produção.
  **Teste que depende de um byte aleatório não é teste, é sorteio** — o novo fixa o caso
  com um DER construído à mão que termina em `0x00`.
  E nada disso é do SafeID: pega token e nuvem igual. Apareceu ali porque a assinatura
  ICP-Brasil **nunca tinha rodado fora dos testes** (a parcela 53 já dizia isso por
  escrito), e a nuvem foi o primeiro caminho a chegar ao cliente.
- **Decodificar prova que veio base64; não prova que veio uma ASSINATURA** (parcela 67, 4ª
  rodada). A 3ª rodada tornou o `raw_signature` tolerante a base64url, enchimento e
  armadura PEM — e o que sai dali continuava indo **calado** para o `/Contents` sem ninguém
  perguntar se era um PKCS#7. Quando não era, o erro só aparecia na conferência, como "ASN1
  corrupted data": uma frase que não distingue **três** causas diferentes — o PSC devolveu
  outro formato, o arquivo foi adulterado, ou nós lemos errado de volta. Foram duas rodadas
  de diagnóstico às cegas por isso.
  `AssinadorSafeID.ExigirPkcs7` decodifica com o MESMO `SignedCms` que a conferência vai
  usar depois (é isso que faz a checagem valer: o que passa aqui passa lá) e a recusa diz o
  **começo dos bytes em hexa** — um CMS abre em `30 82`, e o que vier diferente nomeia a
  causa de imediato. Ela mora no ASSINADOR e não no `ClienteSafeID` porque aquele é
  transporte: entrega o que o PSC mandou. Aqui é onde a assinatura vira PDF, e é o único
  caminho pelo qual uma assinatura em nuvem chega a um documento da clínica.
  A lição de método, que vale além desta integração: **quando uma correção torna uma
  entrada mais TOLERANTE, ela precisa vir junto da conferência do que a entrada deveria
  ser.** Tolerância sem conferência só empurra a falha para mais longe da causa — e o
  segundo erro é sempre mais caro de diagnosticar que o primeiro.
- **A assinatura em nuvem assinava o hash de NADA — e o defeito morava no vão entre dois
  testes que passavam** (parcela 67, 5ª rodada; três mensagens de erro diferentes em três
  dias até alguém REPRODUZIR o caminho em vez de deduzi-lo). O `RangedStream` que o
  PDFsharp entrega ao `IDigitalSigner` chega **sem posição**: `Position` nem getter
  utilizável tem (lança `NullReferenceException`), `CanSeek` **lança** em vez de devolver
  false — o que derruba `CopyTo` —, e **ler antes de posicionar devolve ZERO bytes, calado**.
  `AssinadorSafeID` fazia `SHA256.HashDataAsync(conteudoCoberto)` direto, então o que subia
  para o PSC era `e3b0c442…b7852b855` — o SHA-256 da cadeia vazia, **a mesma constante em
  toda folha da clínica**. O PSC assinava esse hash corretamente e devolvia um PKCS#7
  impecável: nenhum erro em lugar nenhum até a conferência dizer que o documento "foi
  alterado", porque a assinatura de fato cobre outra coisa. **Assinatura válida sobre
  conteúdo nenhum é a garantia aparente na forma mais perigosa que este projeto já
  produziu** — e a única que sai da clínica com valor jurídico afirmado no rodapé.
  ⚠️ **Nenhum teste podia pegar, e é isso que a lição tem de mudar.** Havia teste para a
  montagem da requisição, para a leitura da resposta, para o recorte do PKCS#7 e para a
  conferência com certificado LOCAL. Não havia um só que assinasse um PDF **pelo assinador
  de nuvem** e depois o CONFERISSE — o circuito inteiro. Cada peça verde, o produto delas
  quebrado. É o `CircuitoCompletoTests` da parcela 33 aplicado a outro assunto: **quando um
  caminho é montado de peças testadas, o teste que falta é sempre o do CAMINHO.**
  A rede que ficou junto da correção: assinar o hash da cadeia vazia é **recusado**. Não é
  paranoia — é a única forma de a próxima versão do PDFsharp (ou outro caminho de
  salvamento) não devolver o defeito em silêncio.
  ⚠️ E a lição de MÉTODO, que custou três rodadas ao cliente: **eu diagnostiquei por
  inferência três vezes seguidas.** Cada correção era um defeito real e provado — o
  `raw_signature` que não era base64 padrão, o `TrimEnd('0')` que comia o byte `0x00` — e
  nenhuma era ESTA. Ler o código e deduzir a causa a partir da mensagem de erro é o que
  parece diligência e é o que faz o cliente testar de novo. **Quando a mensagem muda a cada
  rodada, pare de ler e REPRODUZA o caminho.** O experimento que resolveu tem trinta linhas
  e devia ter sido a primeira coisa.
- **O CMS não vem em DER: vem em BER com comprimento INDEFINIDO** (parcela 67, 6ª rodada —
  e foi a mensagem-com-evidência da rodada anterior que resolveu em UMA tentativa). A tela
  trouxe `o cabeçalho DER não é legível (começa em 308006092A864886, 32768 bytes
  disponíveis)`, e o hexa diz tudo: `30` SEQUENCE, **`80` comprimento indefinido**, `06 09`
  OID de nove bytes, `2A 86 48 86 F7 0D 01 07 02` = `1.2.840.113549.1.7.2` = signedData.
  Um PKCS#7 perfeito — só que em **BER**, que é sobre o que o CMS é definido (RFC 5652),
  e não em DER.
  O `RecortarDer` da 4ª rodada lia o cabeçalho À MÃO e **recusava explicitamente** o `0x80`,
  com um comentário meu dizendo que "existe em BER, não em DER" — a premissa certa e a
  conclusão errada: eu tratei como defeito o formato que o fornecedor usa. Agora quem conta
  os bytes é o `AsnDecoder` do próprio .NET em modo BER, que percorre a estrutura e acha o
  `00 00` do fim. **Parser de ASN.1 escrito à mão é onde se erra justamente o caso do
  fornecedor** — e o `SignedCms.Decode` sempre aceitou os dois (foi por isso que o
  `ExigirPkcs7` passou e a mensagem dizia "produzida").
  ⚠️ **A lição de método, e é a que fecha a série**: as rodadas 3, 4 e 5 foram diagnóstico
  por inferência e custaram três testes ao cliente. A rodada 5 acrescentou UMA coisa — a
  frase passou a nomear a causa e a imprimir os primeiros bytes — e a 6ª foi resolvida no
  primeiro relato, sem hipótese nenhuma. **Mensagem de erro que carrega a evidência não é
  conforto: é o que substitui a próxima rodada de adivinhação.** Quando um caminho novo
  encosta em formato de terceiro, a evidência entra JUNTO com o código, não depois de ele
  falhar.
- **Aceitar o formato do fornecedor não é o mesmo que EMITIR o formato da norma** (parcela
  67, 7ª rodada — achado ao responder "temos 100% de certeza?", e a resposta honesta era
  não). A rodada anterior fez o nosso `Conferir` aceitar o CMS em BER indefinido que o
  SafeID devolve. Isso resolve metade: **PDF assinado exige DER** (ISO 32000-1 e
  PAdES/ETSI EN 319 142). Embutir o BER produziria um arquivo que o NOSSO validador aprova
  e que o Adobe e o validador do ITI podem recusar — e quem descobriria é o farmacêutico,
  com a receita na mão. É a **garantia aparente virada do avesso**: em vez de o sistema
  mentir para a clínica, ele diria a verdade para a clínica e produziria um arquivo que o
  mundo lá fora não lê.
  `AssinadorSafeID` passou a **normalizar para DER** (`SignedCms.Decode` + `Encode`) antes
  de devolver os bytes ao PDFsharp. Medido antes de decidir: o reencode devolve **o DER
  byte a byte idêntico** ao que o PSC teria produzido, e `CheckSignature` continua
  passando — a assinatura cobre os atributos assinados, não a codificação de fora.
  O `RecortarAsn1` continua aceitando BER de propósito: é o que mantém legível qualquer
  folha assinada ANTES desta correção.
  ⚠️ A pergunta que produziu isto vale mais que a correção: **"e se o nosso lado estiver
  certo e o de fora não?"** Toda integração que aceita o formato de um terceiro tem essa
  segunda metade, e ela não aparece em teste nenhum da casa — por definição, o teste da
  casa usa o leitor da casa.
- **BER aninhado, medido em vez de suposto** (parcela 67, 8ª rodada — a cliente perguntou
  se havia 100% de certeza, e cada tentativa de assinatura é COBRADA pelo PSC, então a
  resposta tinha de vir de experimento e não de leitura). Um HSM emite o CMS com
  comprimento indefinido nos ENVELOPES de fora e DER no miolo assinado. Medido em
  profundidade crescente: **1 a 4 passam inteiras** — o recorte acha o fim exato dentro do
  enchimento de 32 KB, o `SignedCms` decodifica, `CheckSignature` confere e a normalização
  devolve o DER **byte a byte idêntico**. A profundidade 4 já entra na lista de
  certificados, além do que qualquer PSC produz.
  Só o caso patológico (indefinido até o certificado embutido, que nenhum PSC reescreve)
  derruba o **reencode** — e a leitura continua correta mesmo ali.
  ⚠️ **O experimento achou um defeito meu na MENSAGEM, não no formato.** Decode e encode
  estavam no mesmo `try`, então a falha do reencode sairia com a frase "não são um PKCS#7"
  sobre bytes que SÃO um PKCS#7 válido e conferido. É a mesma família de erro que custou
  seis rodadas a esta integração: **mensagem plausível e errada manda procurar o defeito no
  lugar errado**, e é pior que mensagem nenhuma. Os dois passos ganharam `catch` separados.
  A recusa, nesse caso, é deliberada: o CMS é válido mas está em BER, e embuti-lo produziria
  um arquivo que a NOSSA conferência aprova e o Adobe/ITI podem recusar. A frase diz isso e
  lembra que a via em papel continua valendo.
- **"See the inner exception for details" é instrução para o PROGRAMADOR impressa na cara
  do usuário** (parcela 67, 9ª rodada — a clínica assinou um documento com sucesso e levou,
  no lugar da confirmação, *"An error occurred while saving the entity changes. See the
  inner exception for details."*). `ClinicaRepositorio.SalvarAsync` traduz o que reconhece
  (duplicidade, vínculo quebrado, sem conexão) e devolvia `null` para o resto — e `null`
  faz a exceção subir **como o EF a escreveu**. A frase não diz o que houve, não diz o que
  fazer, e esconde justamente a linha que resolve: a do banco, com a coluna, a restrição e
  o valor.
  Agora o caso não classificado leva `ex.GetBaseException().Message` junto ("O banco
  respondeu: …"). Não é elegante e é muito melhor que uma tela que não informa nada — e a
  causa já ia para o log desde sempre, o que significa que a informação existia e só não
  chegava a quem precisava dela.
  É a MESMA lição que a assinatura em nuvem custou seis rodadas para ensinar, agora noutra
  camada: **mensagem de erro que carrega a evidência substitui a próxima rodada de
  adivinhação.** Quando uma tradução de erro tem um caminho "não sei o que é isto", esse
  caminho é o que vai aparecer na clínica — e é o que precisa dizer mais, não menos.
- **Data COM fuso: o defeito que o SQLite não pode pegar** (parcela 67, 9ª rodada — a
  clínica assinou um documento com SUCESSO e levou *"An error occurred while saving the
  entity changes"* na publicação). O sistema grava hora de PAREDE (`DateTime.Now`,
  `Kind=Local`), e o Npgsql **RECUSA** isso numa coluna `timestamp with time zone` ("only
  UTC is supported"). O padrão do provedor para `DateTime` é justamente **com** fuso — então
  esquecer o `HasColumnType("timestamp without time zone")` numa propriedade nova **não
  quebra nada** até alguém tentar gravá-la em produção.
  Eram **SEIS** colunas, e a publicação era só a que apareceu: as outras cinco são da
  parcela 52 — cancelar evolução, anexo, avaliação e medida, e versionar evolução. Ou seja,
  **as garantias de prontuário imutável que a auditoria da cliente exigiu estavam todas
  quebradas no Postgres desde que nasceram**, e a clínica só não descobriu porque ninguém
  tinha cancelado um registro clínico ainda.
  ⚠️ **Os 1580 testes não podiam pegar, e isso é estrutural**: eles rodam em SQLite, que
  guarda data como texto e não liga para o `Kind`. É a mesma família do `xmin` (concorrência
  otimista, "só no Npgsql — testes rodam em SQLite e ficam de fora"), só que aqui o buraco
  não estava documentado. `DatasSemFusoTests` fecha por outro caminho: lê o **MODELO** do EF
  e cobra `timestamp without time zone` de toda propriedade `DateTime`. Ela falha no commit
  em que alguém esquecer, que é meses antes de a clínica esbarrar.
  **Ao acrescentar propriedade `DateTime`, declare o tipo da coluna.** O provedor não erra
  a favor.
- **Comentário que promete o que o código não faz é pior que comentário nenhum** (parcela
  67, 9ª rodada, o segundo defeito do mesmo log). `AssinaturaDeDocumentoClinicoService`
  dizia, por escrito, *"publica DEPOIS de gravar … falhar aqui não desfaz nada — vira frase
  no resultado"* — e **não havia `try`**. A exceção da publicação subia por cima de tudo, e
  a tela dizia que o documento NÃO tinha sido assinado, quando ele tinha e estava gravado.
  É o pior desfecho possível: quem lê "não foi assinado" **emite de novo**, e ficam dois
  documentos do mesmo ato — exatamente o que o comentário de `DocumentoEdicaoViewModel`
  avisa duas telas acima. `ResultadoPublicacao.Falhou` já existia; faltava alguém chamá-lo.
  A lição de leitura: **quando um comentário descreve um comportamento de degradação,
  confira se existe o `catch` que o realiza.** Intenção escrita não é intenção executada, e
  o comentário faz o revisor seguinte parar de procurar.
- **A 2ª assinatura: a enfermagem sela o REGISTRO DE EXECUÇÃO, nunca a mesma folha**
  (parcela 68 — a direção reverteu, para quem pedir, a decisão de ago/2026 de que a
  execução se assina só à caneta). O pedido era "um campo de 2ª assinatura onde, quando
  marcado, a enfermeira também possa assinar digitalmente" — e o desenho inteiro sai de
  duas restrições que já estavam escritas: em PDF **não se assina incrementalmente**
  (dois documentos encadeados, um por signatário — o caminho que o comentário da
  `AssinaturaDocumento` já apontava), e o **motor congelado não se toca** (o
  `AssinaturaDigitalService` e o SafeID são reusados tal e qual; o que nasceu foi
  orquestração por cima: `AssinarExecucaoAsync` no orquestrador e no serviço de domínio).
  As decisões: **o campo é por FOLHA, de quem prescreve** (`ExigeAssinaturaEletronicaDa
  Execucao` — regra global travaria a sala inteira no dia em que o certificado de uma
  técnica vencesse); **a assinatura é no ENCERRAMENTO**, porque o registro de execução
  muda a cada item checado e assinar antes selaria um arquivo que ainda ia mudar — a
  razão de a prescritora não assinar rascunho, aplicada ao outro papel; **o CPF do
  certificado confere contra QUEM ESTÁ ASSINANDO** (a enfermeira logada, via
  `UsuarioSistema → Profissional`), nunca contra o prescritor — sem isso o e-CPF da
  médica assinaria a execução da técnica; e **assinada, a segunda via da execução passa a
  devolver os bytes GUARDADOS** (até aqui ela era sempre montada na hora, e continua
  sendo nas folhas do regime do papel — que não mudou em nada para quem não marcou o
  campo, e há teste fixando as duas coisas).
  O bit da ação é `ChecarPrescricao`, não um novo: assinar a execução é responder pelo
  que se checou — o mesmo trabalho, selado. E o botão "Assinar execução" só EXISTE na
  folha encerrada que aguarda a assinatura: botão apagado permanente para o caso que não
  existe seria a caixinha morta da parcela 49.

- **Auditoria Recepção → Consultório: nenhum dos achados QUEBRAVA nada** (parcela 68). O
  fluxo entre os dois módulos foi varrido inteiro — lógica, erros e leiaute — e o que a
  varredura devolveu tem a assinatura de sempre: build verde, 1601 testes verdes, três
  redes locais verdes, e quem descobre é a recepcionista ou o médico com o paciente na
  frente. As famílias, e o que cada uma ensina:
  **O vínculo da evolução com o horário se perdia nas DUAS pontas, e o resultado era o
  mesmo — duas evoluções do mesmo atendimento.** Na ESCRITA: a janela de evolução da
  Recepção nunca carregou `AgendamentoId`/`AtendimentoId`, e o `ProntuarioService` os
  copiava **sem condição** — editar pelo balcão uma sessão escrita no consultório
  DESLIGAVA a evolução do horário, e o consultório voltava a cobrar o registro. Na
  LEITURA: o cartão do Meu dia casa por `AgendamentoId` **e cai para paciente + data**,
  e a tela de Atendimento só olhava o `AgendamentoId` — o cartão dizia "Ver registro", o
  formulário abria EM BRANCO e o Salvar criava a segunda.
  As correções são as duas metades da mesma regra: **nulo quer dizer "o chamador não
  sabe", nunca "desligue"** (o serviço passou a preservar o vínculo com `?? destino.…`,
  onde vale para toda porta), e o casamento sessão × evolução virou **uma definição só**
  (`ConsultorioService.EvolucaoDoHorario`, pública e estática). Para a segunda, o dia do
  horário teve de viajar até o posto (`PacienteEmFoco.DataDoHorario`): presumir hoje daria
  a resposta errada justamente onde a pergunta importa — a dívida de prontuário e a Minha
  semana abrem horário de OUTROS dias.
  ⚠️ **A lição de método**: as duas telas foram escritas com meses de distância, e a que
  ficou para trás é sempre a que ninguém releria. **Quando duas telas respondem à mesma
  pergunta sobre o mesmo dado, o teste que falta é o que compara as DUAS** — é a mesma
  lição da parcela 64 (recall com regra num lado e sem regra no outro), aplicada a uma
  leitura em vez de a uma permissão.
  **O menu "⋯" da fila prometia por escrito o que não fazia.** O XAML diz "o bloco não
  segue `PodeEditarAgenda`; quem decide item a item é o próprio menu" — e o construtor do
  menu montava os itens **só pelo estado do cartão**, sem um `Pode` sequer. Os três atos
  pedem bits diferentes (`ColherAssinaturaPaciente`, `EditarAgenda | MovimentarFila`,
  `EditarAgenda` estrito), então o item aparecia aceso e a recusa só chegava depois do
  clique. **Comentário que descreve uma barreira é lugar para conferir se ela existe** —
  a mesma leitura que achou o `catch` ausente da publicação na parcela 67.
  **A cor mentia sobre o desfecho.** A barra do Novo atendimento pintava a mensagem de
  vermelho FIXO, e é o mesmo campo que recebe "Atendimento registrado — 2 guia(s) no
  faturamento": a cor dizia "falhou" enquanto o texto dizia "registrado", e quem lê a cor
  primeiro lança de novo — o gesto que produziu os três encaixes em 71 segundos da parcela
  65. Três estados, porque o meio existe de verdade: a guia nasceu e o pacote/caixa não
  **não é erro** (só o atendimento derruba a operação, parcela 6) **nem é sucesso limpo**.
  E a confirmação "Consulta de X renovada" era escrita ANTES do `RecarregarAsync`, que
  começa zerando a mensagem: **só o sucesso sumia**, porque o erro volta antes de recarregar.
  **A regra do dono da janela existia como método privado SEM CHAMADOR, enquanto 49
  lugares repetiam `MainWindow` à mão.** A parcela 58 documentou que o dono é a janela
  ATIVA — com um modal aberto, a próxima nasce ATRÁS dele e quem clicou conclui que o
  botão não fez nada — e a regra ficou numa ViewModel só, sem uso. Virou `JanelaDona.Atual()`
  no shell. **Método sem chamador ao lado de vizinhos que repetem a linha à mão é sinal de
  regra que foi escrita e não foi aplicada** — não de código morto a remover.
  **E o `EstadoDaTela` na RAIZ da página, de novo.** Em Equipe ele cobria as três colunas;
  em Documentos, os cartões e o filtro — nas duas com um estado vazio inline repetindo a
  mesma frase logo abaixo. É a parcela 58 e a 64 pela terceira vez, e a regra não muda:
  **a sobreposição pertence à REGIÃO cujo vazio ela explica, e há UM estado vazio por
  pergunta.**

- **A premissa que ficou seis parcelas de pé porque ninguém mediu a CONCLUSÃO dela**
  (parcela 68, 2ª rodada — a cliente descreveu o fluxo real: *"o médico prescreve e assina,
  a infusão vai pra enfermagem e ela também assina a prescrição que já foi assinada pelo
  médico"*). Desde a parcela 42 o projeto afirmava, por escrito e em quatro lugares, que
  **"duas assinaturas no mesmo PDF não existem, porque o PDFsharp reescreve o arquivo ao
  salvar"** — e foi essa frase que desenhou a 1ª rodada desta parcela, em que a enfermagem
  selava OUTRO documento (o registro de execução).
  Medido antes de mexer: a primeira metade está **certa** — assinar por cima do assinado
  devolve um arquivo cujo prefixo mudou, e a assinatura de quem assinou primeiro deixa de
  fechar. A **conclusão** é que estava errada: a limitação é da BIBLIOTECA, não do formato.
  O PDF prevê múltiplas assinaturas exatamente para este caso, por **atualização
  incremental** — a revisão nova é anexada ao fim e os bytes já assinados não se tocam. A
  assinatura da médica cobre 0..N; a da enfermeira cobre 0..M, com M > N; as duas fecham
  porque nenhuma teve um byte alterado.
  ⚠️ **A lição não é sobre PDF.** É que **quando uma limitação de ferramenta vira decisão
  de desenho, é preciso escrever qual das duas metades foi medida** — a restrição ou a
  conclusão que se tirou dela. Esta ficou seis parcelas sem ninguém tentar o caminho que o
  formato já oferecia, e o desenho errado tinha chegado a ter teste verde e PR aberto.
  As decisões do `RevisaoIncrementalPdf`: ele **não calcula assinatura nenhuma** — quem
  produz o PKCS#7 continua sendo o mesmo `IDigitalSigner` do token e do SafeID, sem uma
  linha de diferença, e é isso que permitiu mexer aqui sem encostar no motor congelado;
  a aparência usa **Helvetica**, uma das 14 fontes-padrão do PDF, para não depender do que
  a outra ferramenta embutiu; e a forma do arquivo de entrada é **conferida, não
  adivinhada** — xref em fluxo, `/Annots` por referência indireta ou ausência de
  `/AcroForm` **recusam com a frase escrita**, porque um meio-parser que "dá um jeito"
  produziria o arquivo que a NOSSA conferência aprova e o Adobe recusa (a garantia
  aparente virada do avesso, a lição da 7ª rodada da parcela 67).
  **E o `Conferir` passou a valer pela PIOR das assinaturas.** Ele lia o primeiro
  `/ByteRange` e parava ali: num documento de duas, responderia "íntegro" a um arquivo cuja
  segunda não fecha — falha exibida como sucesso, no papel que prova quem mandou e quem
  executou.
  ⚠️ **O teste que decide não é o nosso.** Cada tentativa de assinatura no SafeID é
  COBRADA, então a prova tinha de fechar de graça e **fora do nosso código**: o arquivo foi
  validado no **pyhanko**, que devolveu `intact=True valid=True` nas duas, com a da médica
  em `coverage=ENTIRE_REVISION mod=FORM_FILLING` e a da enfermeira em `ENTIRE_FILE
  mod=NONE` — o retrato exato de um PDF corretamente assinado duas vezes. Os testes gravam
  o arquivo quando `CLINICA_DUMP_PDF` aponta uma pasta, justamente para essa conferência
  externa continuar barata.
  ⚠️ E um defeito que só apareceu por rodar num PDF de outro produtor: **o espaço entre
  `/Type` e o nome é OPCIONAL** — o QuestPDF escreve `/Type /Page` e o PDFsharp escreve
  `/Type/Page`. Casar o texto cru funcionava por sorte, e a falha teria sido "o PDF não tem
  páginas legíveis" num arquivo cheio delas.

- **A navegação que só o TESTE preenche** (parcela 68, 3ª rodada — o bloqueador que uma
  auditoria adversarial achou no código recém-enviado, com 1608 testes verdes).
  `AssinarExecucaoAsync` lia os bytes da médica por `AssinaturaDoPrescritor?.Arquivo?
  .Conteudo`. `ObterPrescricaoInternaAsync` faz `.Include(p => p.Assinaturas)` e **não** faz
  `.ThenInclude(a => a.Arquivo)`, e o projeto não usa lazy loading — em produção, onde cada
  operação abre escopo próprio, a navegação chega **nula** e a 2ª assinatura falhava
  **sempre**, na primeira tentativa da enfermeira, que nesta área é COBRADA pelo PSC.
  ⚠️ **Os testes não podiam pegar, e a razão é a que mais se repete neste projeto**: eles
  compartilham UM `DbContext` entre gravar o `ArquivoAssinado` e reler a prescrição, e o
  **relationship fixup** do EF preenche a navegação a partir do change tracker mesmo sem
  `ThenInclude`. Ou seja, o teste montava um mundo que produção nunca vê. É o
  `CircuitoCompletoTests` da parcela 33 outra vez: cada peça verde, o produto quebrado.
  A regra que fica: **bytes de arquivo assinado se buscam por
  `ObterArquivoAssinadoAsync(arquivoId)`, nunca pela navegação** — é o que todo leitor
  legítimo já fazia, e a linha nova foi a única a confiar no grafo. E, mais geral: **teste
  de serviço que lê navegação precisa de um `DbContext` NOVO**, senão ele prova o fixup do
  EF em vez do `Include` do repositório. `Assina_com_ESCOPO_SEPARADO_como_em_producao` fixa
  isso, e foi verificado que ele FALHA no código antigo.

- **A folha só fica assinável no instante em que ela SOME da lista** (parcela 68, 4ª
  rodada — as quatro pendências que a auditoria adversarial confirmou). A 2ª assinatura da
  enfermagem só é possível depois de ENCERRAR a folha, e encerrar era exatamente o que a
  tirava da fila da Sala de infusão. Somadas, as quatro faziam a pendência desaparecer:
  1. a fila é travada em **`p.Data == hoje`** — folha de ontem não aparecia de jeito nenhum;
  2. **"Mostrar encerradas" nasce DESMARCADO** — encerrada some da lista padrão no mesmo dia;
  3. **não existia consulta, contador nem selo** de "aguardando a 2ª assinatura":
     `AguardaAssinaturaDaExecucao` era lido só DENTRO da janela de uma folha já aberta, então
     descobrir qual ficou pendente exigia abrir uma por uma;
  4. no dia seguinte a única volta era **digitar o código impresso** — que só existe se
     alguém imprimiu o papel.
  É o **"alerta sem porta" na pior variante** (parcela 48): o que a pessoa precisa
  reencontrar é justamente o que a lista esconde. E a mensagem ainda prometia que "o botão
  Assinar execução fica nesta folha" sem dizer como voltar a ela — comentário/《promessa》
  que o código não cumpre, a armadilha da parcela 67.
  A correção é uma consulta **sem filtro de data** (`PrescricoesInternasAguardandoAssinatura
  Async`: encerrada + exige assinatura + sem a do Executante), unida à fila de hoje e
  publicada de uma vez em lista local — **entre o `Clear()` e o último `Add` não pode haver
  await**, e aqui são DUAS leituras (a lição da parcela 62). O selo vermelho diz o que
  falta, a data aparece quando a folha não é de hoje, e o contador da assinatura é
  **separado** do de itens aguardando: um se resolve administrando, o outro com o
  certificado — somá-los daria um número que não diz o que fazer.
  ⚠️ E a pendência **SAI da lista assim que ela assina**: pendência que não some é
  pendência que ensina a ignorar a lista. O regime do papel (campo desmarcado) nunca entra,
  senão toda folha encerrada da história viraria cobrança eterna.

- **Varredura do módulo CLÍNICO: a barreira que falta é sempre na tela que mais custa**
  (parcela 68, 5ª rodada). Três achados, e os três da família "duas barreiras":
  (a) **A prescrição de infusão tinha só a metade VISÍVEL** — `SalvarRascunho` e `Assinar`
  se protegiam por `IsEnabled`/`PodePrescrever` e não chamavam `Exigir` em lugar nenhum.
  Atalho de teclado e corrida de carregamento passam direto, e o ato é gerar e selar uma
  prescrição médica. É o mesmo defeito que a parcela 51 corrigiu no Acessos e a 54 no
  cadastro de usuário: a tela onde ele mais custa é a última em que alguém olha.
  (b) **Enviar documento clínico ao paciente não tinha guarda** — emitir, assinar e
  cancelar tinham; enviar, não. É dado de saúde SAINDO, que é o que a parcela 60 passou a
  cobrar no export. **Três comandos vizinhos guardados e um não: o errado é o um** (a
  lição da parcela 64, aplicada de novo).
  (c) **A direção não via a pendência que a sala passou a ver.**
  `AssuntoDirecao.InfusaoAguardando` conta só folhas `Assinada` com item sem checagem, e a
  folha ENCERRADA aguardando a 2ª assinatura fica de fora por construção — o alerta novo
  (`InfusaoSemAssinatura`) contradizia, por omissão, o argumento que criou o bloco na
  parcela 61: *a sala vê a própria fila, a direção é quem vê a soma*. Os dois alertas são
  **separados**, nunca somados: um se resolve administrando o que falta, o outro com o
  certificado de quem executou.
  ⚠️ E a varredura teve um **falso positivo meu que vale registrar**: contar `Exigir(` por
  arquivo acusou `MeuDiaViewModel` de ter 11 comandos e nenhuma guarda. Ele usa
  `ExigirAlgum(` — o par de `PodeAlgum` que a parcela 61 criou —, e as quatro escritas
  estão guardadas. **Varredura textual de barreira tem de conhecer TODAS as formas da
  barreira**, senão ela acusa o arquivo que está certo e ninguém confia mais nela.

- **O QR da folha e o "Ler QR Code" do validador são dois PROTOCOLOS — o mesmo texto,
  lido por dois contratos** (parcela 68, 6ª rodada — a clínica mandou o print: o validador
  LEU o QR e pediu "Insira o código"). O nosso QR carrega a URL do PDF publicado. A câmera
  do celular trata isso como "abra no navegador" e o arquivo abre; o botão "Colar URL" do
  validador trata como endereço de arquivo, baixa e valida. Já o botão **"Ler QR Code" do
  próprio validador** trata o conteúdo como endereço da API de uma plataforma CADASTRADA:
  ele chama a URL pedindo `application/validador-iti+json` com um `_secretCode`, e o guia
  do desenvolvedor diz que esse fluxo **não aceita documento sem código/senha**. Um bucket
  S3 não fala esse dialeto e não tem código: **nenhum código digitado ali funciona, por
  desenho** — não é o QR, não é o tamanho, não é o link.
  A evidência estava no print desde o início: **ele LEU o QR ("QR Code escaneado") e ainda
  pediu código — logo nunca tentou baixar o arquivo.** Quando o cliente pergunta "por que
  A funciona e B não com o MESMO dado", a resposta é B tratar o dado por outro CONTRATO.
  ⚠️ E a lição de método custou três rodadas: a primeira hipótese (código de plataforma
  cadastrada) estava CERTA — e eu a recuei por não ter fonte, troquei pela hipótese do
  tamanho do QR (medível, e errada) e só fechei quando li o guia oficial. **Hipótese certa
  sem fonte continua sendo hipótese; a resposta estava em procurar o CONTRATO do outro
  lado (guia do desenvolvedor), não em medir o nosso.**
  O que era nosso no defeito: a folha dizia "Escaneie para baixar · validar em …" sem
  dizer COM O QUÊ escanear nem POR QUAL botão validar — a dois centímetros do NOSSO código
  de conferência, numa tela que pede um código. O rodapé agora nomeia o caminho que
  funciona (câmera → colar link ou enviar o arquivo) e desarma o errado ("o Ler QR Code de
  lá pede um código que esta folha não tem"); o código de conferência diz que é interno; e
  a folha de infusão parou de afirmar que o código "confere integridade" — ele LOCALIZA a
  folha; quem prova integridade é a assinatura (a regra da parcela 3, que quase escapou por
  uma palavra).

- **O "Ler QR Code" do validador gov.br é um CONTRATO aberto — e entrar nele não exige
  cadastro** (parcela 68, 7ª rodada — a cliente colou o Guia de Orientações aos
  Desenvolvedores do VALIDAR, que a rede deste ambiente bloqueava). O que o guia fecha:
  depois de ler o QR, o validador chama a URL com
  `?_format=application/validador-iti+json&_secretCode=<código>` e espera um JSON com a
  URL do PDF (`{"version":"1.0.0","prescription":{"signatureFiles":[{"url":"…"}]}}`);
  401 para código errado, 404 para inexistente. O `_secretCode` é definido como **"0 a 64
  caracteres"** — vazio é contrato válido. Qualquer URL que responda isso entra no fluxo;
  não há credenciamento.
  O desenho que isso destrava: um **Worker no MESMO domínio do QR** (o CNAME da parcela
  53, que existe exatamente para pôr coisas na frente do balde sem trocar a URL selada nos
  PDFs) entrega o PDF a navegador e o JSON ao validador, conferindo o `_secretCode` contra
  o **código de conferência impresso na folha** — que a publicação passou a gravar como
  METADADO do objeto (`codigo`), para a borda responder sem banco. Como a URL não muda,
  **as receitas já assinadas e impressas entram no fluxo também**. O Worker pronto está em
  `tools/worker-validar-iti.js`; o roteiro, em `docs/validar-pelo-qr-code.md`; o rodapé só
  muda DEPOIS de o Worker existir — mudar antes imprimiria promessa que o endereço não
  cumpre.
  ⚠️ **E o Capítulo VI do guia toca no nosso arquivo de duas assinaturas**: o VALIDAR
  recomenda a 1ª assinatura declarar `DocMDP` (P=2 permite formulário e novas assinaturas)
  e avisa que, sem isso, PDF com atualização incremental "poderá" sair como "Assinatura
  Indeterminada". Afeta só a prescrição de infusão (interna, art. 13 — não vai à
  farmácia); declarar DocMDP mexe em como o PDFsharp escreve a 1ª assinatura, que é área
  congelada. Registrado como decisão futura, não tomada de raspão.

- **AUDITORIA DA AGENDA ANTES DE PRODUÇÃO — dois módulos, e nenhum dos achados quebrava
  nada** (parcela 69). A direção pediu o estado da Agenda na Recepção e no Consultório
  antes de subir. O ponto de partida era o de sempre: **build verde, 1612 testes verdes,
  as três redes locais verdes** — e a varredura devolveu um bloqueador que impede a
  recepcionista de fechar um feriado e um buraco que faz o paciente do balcão sumir do
  app do médico. As famílias, e o que cada uma ensina:

  ⚠️ **DUAS PORTAS PARA O MESMO ATO, DOIS BITS DIFERENTES — e a de fora era a mais larga.**
  "Fechar agenda…" na barra da Agenda pedia `EditarAgenda` (o comentário ao lado diz, com
  todas as letras, "o MESMO bit do Novo horário"), e o Salvar da janela pedia
  `GerenciarEquipe`. `PerfilAcesso.Recepcao` tem o primeiro e não tem o segundo: a
  recepcionista atravessava a porta, escolhia o 25/12, escrevia "Natal" e levava *"Seu
  acesso não permite fechar a agenda"* no clique final. Feriado e férias simplesmente não
  entravam no sistema pelo balcão.
  Não é a parcela 41 ("guarda que volta em silêncio") nem a 51 ("tela com uma barreira
  só"): as duas barreiras existiam e **discordavam sobre que ato era aquele**. A regra
  que fecha o assunto: **quando uma janela é aberta por mais de uma tela, a guarda do
  Salvar é a UNIÃO dos bits das portas** (`ExigirAlgum`), nunca o de uma delas — senão a
  porta que ficou de fora vira um corredor sem saída, e quem a atravessa descobre isso
  depois de fazer o trabalho todo.

  ⚠️ **O ATENDIMENTO AVULSO NASCIA SEM PROFISSIONAL, e é assim que o paciente some do app
  do médico.** "Novo atendimento" — a porta de quem chega sem hora marcada — chamava
  `AgendarAsync` sem `profissionalId`, e a tela nem perguntava quem ia atender. O
  encaixe nascia órfão, e órfão não aparece em lugar nenhum do Consultório: "Meu dia" e
  "Minha semana" filtram por `ProfissionalId`, e o **repasse também** — ele lê quem
  atendeu do AGENDAMENTO, porque `Atendimento` não guarda profissional. O médico atendia
  alguém que o app dele nunca mostrou, e não era pago por aquela sessão.
  Nada falhava: a guia nascia certa, a tela dizia "Atendimento registrado — 2 guia(s) no
  faturamento", e o balcão via o paciente na Fila (que não filtra por profissional). É o
  **elo partido da parcela 33 na forma mais cara**: não vira erro, vira ausência — e
  ausência é indistinguível de "hoje não teve".
  A lição para a próxima porta: **ao criar um caminho novo que grava um agendamento,
  liste quem LÊ agendamento e confira se o registro novo satisfaz o filtro de cada um.**
  São três leitores com o mesmo filtro (quadro do dia, semana do profissional, repasse) e
  um sem filtro (a fila do balcão) — e é justamente o sem filtro que faz o defeito passar
  despercebido no teste de mesa.
  ⚠️ O seletor **não impede**: sem profissional escolhido a sessão é lançada assim mesmo,
  porque a guia é o que a clínica não pode perder (a hierarquia da parcela 65). O que ele
  faz é dizer, ao lado do campo, o que a lacuna custa — e é isso que transforma um buraco
  invisível numa decisão de quem está no balcão.

  ⚠️ **"EMPURRAR AS SESSÕES" APAGAVA A ESPECIALIDADE E A VARIANTE DA MODALIDADE.**
  `RemarcarEmLoteAsync` (o botão que desloca as trinta sessões de umas férias) chama
  `RemarcarAsync` **só com a data nova**, porque é só isso que ele muda. Do outro lado, a
  atribuição era incondicional: `ag.ModalidadeCodigo = modalidadeCodigo ?? modalidade.
  ToString()` trocava "Acupuntura (domiciliar)" por "Acupuntura", e a especialidade da
  consulta ia a nulo. O empurrão respondia *"30 sessão(ões) empurradas"* e o estrago
  aparecia semanas depois, **uma paciente por vez**, na guia que nascia errada.
  É a regra da parcela 68 — **nulo quer dizer "o chamador não sabe", nunca "desligue"** —
  no lugar onde ela ainda não tinha sido aplicada. Quem MUDA a modalidade é quem informa
  o código dela, e só nesse caso a especialidade que vem junto é autoridade (inclusive
  para limpar). A correção mora no SERVIÇO e não no chamador, pela razão de sempre: são
  quatro portas para remarcar, e a próxima também não vai saber os códigos.

  ⚠️ **REMARCAR LEVAVA JUNTO OS CARIMBOS DA FILA.** A etapa do kanban é DERIVADA de
  `ChegadaEm`/`ChamadoEm`/`InicioAtendimentoEm`, não é coluna no banco — e `RemarcarAsync`
  nunca os tocou. O paciente que fez check-in às 9h e pediu para remarcar aparecia, na
  quinta-feira, já na raia "Na recepção" **antes de sair de casa**, com a espera contada
  desde terça (a espera vai da chegada até agora). Se ele tinha entrado na sala antes de a
  sessão ser interrompida, aparecia em "Em atendimento".
  Só se limpa quando a DATA muda: remarcar mexendo em sala, duração ou observação é ajuste
  do horário de hoje, e apagar o check-in de quem já está sentado no balcão seria destruir
  o fato pelo caminho errado. **Estado DERIVADO de carimbo de hora tem de ser revisto em
  toda escrita que muda o que o carimbo significa** — e "mudou de dia" é a maior delas.

  ⚠️ **`EstadoDaTela` CONGELAVA A RESPOSTA NO INSTANTE DO BINDING — e quase toda tela
  escapou por acidente.** O componente decide o estado num callback de
  *DependencyProperty*, e as telas amarram `Itens` a uma `ObservableCollection` que **nunca
  é reatribuída** (elas fazem `Clear()` e `Add()` na mesma instância). Isso dispara
  `CollectionChanged`, que o componente não ouvia. `Recalcular` rodava uma vez, com a lista
  ainda vazia, e a resposta ficava ali para sempre.
  O que salvou a maioria foi um acidente: elas também amarram `Carregando`, que vira true e
  depois false a cada leitura — e é esse callback vizinho que reavaliava a lista. As três
  que amarraram **só** `Itens` ficaram com "não há nada aqui" escrito por cima do conteúdo
  cheio: a **lista de espera da agenda**, as **autorizações do convênio** na ficha do
  paciente e a **busca de CID**. Nenhuma rede pega — XAML válido, binding válido, nada
  lança.
  A lição é maior que o componente: **contrato que só funciona porque uma propriedade IRMÃ
  costuma mudar junto é contrato que ninguém consegue lembrar.** A assinatura de
  `INotifyCollectionChanged` resolve no ponto único por onde toda tela passa, e as telas
  seguintes nascem certas sem saber que a regra existe.

  ⚠️ **CANCELADO E FALTA FICAVAM NA RAIA "AGUARDANDO" DO MÉDICO SEM UMA MARCA.** Ficarem na
  coluna é decisão certa e documentada (a regra da folha do dia: quem lê às 14h precisa
  saber que as 15h vagaram). O que faltava era o selo — e ele **existia calculado**
  (`LinhaSessao.Situacao`, via `Rotular`) e o XAML nunca o leu. Dado calculado sem leitor,
  na variante em que o estrago não é *não ler* e sim **ler errado**: o médico contava cinco
  pessoas por vir e duas tinham desmarcado.
  E a tela IRMÃ, no mesmo módulo, já marcava: "Minha semana" tem `ForaDaFila` e escreve
  "Não aconteceu". É a lição das parcelas 64 e 68 pela terceira vez — **quando duas telas
  respondem à mesma pergunta sobre o mesmo dado, a que está errada é a que ninguém releu**,
  e o teste que falta é o que compara as duas.

  ⚠️ **O BIT QUE NOMEIA O ATO NÃO GUARDAVA A PORTA QUE O EXECUTA.** `LancarAtendimento`
  existe para a direção poder tirar de alguém a criação de atendimento — e a caixinha dele
  em Acessos diz "criar o atendimento — e, com ele, as guias". O **Concluir da Fila**, que
  é onde a guia de fato nasce desde a parcela 65, pedia só `EditarAgenda`. Desmarcar
  aquela caixinha fechava a tela de Novo atendimento e não fechava **nada**: a mesma pessoa
  seguia gerando guia pela porta que a clínica usa o dia inteiro.
  A causa é a parcela 65 vista do lado do acesso: **quando um ATO muda de lugar, a
  permissão dele não vai junto sozinha.** Ao mover o momento em que um fato passa a
  existir, releia quem guarda o novo momento. Nenhum perfil padrão perdeu nada aqui — só
  `Recepcao` tem os dois bits, e o Gerente tem todos: o que mudou foi o bit passar a valer.

  ⚠️ **O APP CONGELADO FAZIA CERTO E O MÓDULO ATIVO FAZIA ERRADO.** A visão de semana da
  Recepção montava as sete colunas chamando `DoDiaAsync` **em laço** — sete idas em fila
  indiana a um banco REMOTO —, mais uma oitava desperdiçada (o dia escolhido era buscado
  no começo da carga e o ramo da semana nunca o abria). `AgendaService.NoPeriodoAsync`
  existe para isso desde sempre, e o único que a usava era a agenda do **faturamento
  congelado**; `ConsultorioService.DaSemanaAsync` já tinha o comentário dizendo por quê.
  A lição: **antes de escrever um laço de leitura, procure o método que faz o período de
  uma vez — e olhe no app que ninguém edita.** Código congelado não é código errado; muitas
  vezes é onde a decisão certa foi tomada primeiro e ficou.

  ⚠️ **O SEGUNDO BLOQUEADOR, e é o mais grave dos dois: A FILA E O PAINEL DO BALCÃO NUNCA
  VIAM O QUE A OUTRA MÁQUINA GRAVOU.** Serviço registrado como `AddScoped` — e o
  `DbContext` junto — pedido ao provedor **RAIZ** vive no ESCOPO RAIZ, isto é, pela vida
  inteira do aplicativo. E o shell resolve toda tela da raiz: `SuiteApp` passa
  `host.Services` ao `ShellViewModel`, que o entrega a `IModuloApp.CriarTela`. O
  `FilaViewModel` e o `PainelViewModel` recebiam `AgendaService`,
  `PainelRecepcaoService`, `TermoProcedimentoService` e `RelacionamentoService` **por
  construtor** — logo, o mesmo `DbContext` da abertura do app até o fim do expediente.
  A consulta da agenda é RASTREADA, e o EF **não sobrescreve valores de entidade já
  rastreada**: reler o dia no mesmo contexto devolve o `ChamadoEm` que ele já tinha — nulo.
  Ou seja: **o médico clica em "Chamar próximo" e o balcão não vê**. Justamente a
  sincronização que a parcela 38 existe para garantir, e que este arquivo descreve como "os
  dois leem a mesma linha, e é isso que faz os dois quadros nunca divergirem". A releitura
  de um minuto — construída na parcela 62 para fazer o recado CHEGAR — relia pelo contexto
  que já tinha a resposta velha. Falta marcada na outra máquina, remarcação e as cinco
  contagens do painel ficavam congeladas no número da abertura, a manhã inteira. **Só o que
  era clicado NESTA máquina aparecia — o que faz o quadro parecer perfeito para quem está
  usando.**
  Agravante: `DbContext` não aceita duas operações ao mesmo tempo. A batida do relógio
  caindo em cima de um clique vira *"A second operation was started on this context
  instance"* — erro em inglês, no balcão, com o paciente na frente.
  ⚠️ **`ValidateScopes` não pega**: `Host.CreateDefaultBuilder()` só o liga no ambiente
  **Development**; em produção a resolução passa calada. Por isso a rede virou a
  **checagem 37**, que casa o construtor de todo ViewModel resolvido em `CriarTela` contra
  os tipos registrados como `AddScoped`/`AddDbContext`.
  A regra: **tela de vida longa abre ESCOPO por operação, e não recebe serviço Scoped no
  construtor.** `AgendaViewModel` e `MeuDiaViewModel` já faziam assim — e é exatamente por
  isso que a grade e o quadro do médico atualizavam e a fila não. **Quando uma tela
  atualiza e a irmã não, a diferença não está na tela: está em como ela pede os serviços.**
  ⚠️ E eram **NOVE** ViewModels, não dois — os outros sete no Financeiro e no Gerente
  (Caixa, Conciliação, Estoque, Pacotes, Plano de contas, Produção, Repasses). **Todos os
  nove foram corrigidos**, e a lista de dívida da checagem 37 está VAZIA: o conjunto
  continua no código como caminho de volta, para tela nova nascer cobrada sem ninguém
  precisar afrouxar nada.
  O Caixa era o que doía mais: um recebimento lançado na outra máquina não aparecia, e o
  fechamento do dia conferia a gaveta contra um número velho.
  Duas coisas que a correção dos sete ensinou, e que a próxima vai reencontrar:
  **(a) o serviço não vai sozinho — o sub-VM vai junto.** Cada tela grande abre uma janela
  de edição (`CategoriaEdicaoViewModel`, `ItemEstoqueEdicaoViewModel`,
  `MovimentoEstoqueViewModel`, `RegraRepasseViewModel`, `PacoteCatalogoEdicaoViewModel`,
  `PacoteVendaViewModel`, `ConsumosPacoteViewModel`, `LancamentoEdicaoViewModel`,
  `CobrancaPixViewModel`) e passava o serviço já resolvido adiante. Corrigir só a tela de
  fora deixaria a janela com o contexto da abertura do app — e é NELA que se grava.
  **(b) antes de trocar, pergunte se alguma ENTIDADE atravessa o escopo.** Carregar num
  escopo e salvar noutro deixa a entidade destacada, e o EF a trataria como nova. Aqui não
  havia nenhum caso — todas as escritas passam ID ou objeto NOVO (`SalvarItemAsync(new
  ItemEstoque { Id = … })`), e `GuiaSemLancamento` é record, não entidade —, mas isso foi
  CONFERIDO caso a caso, não presumido. É a pergunta que decide se o refactor é mecânico
  ou se precisa de desenho.
  De quebra, três `Clear()` com `await` no meio apareceram no caminho (parcela 62) e foram
  corrigidos junto: `RegraRepasseViewModel`, `PacotesViewModel` e `PacoteVendaViewModel`.

  ⚠️ **A CONSULTA CHAMADA DA LISTA DE ESPERA NASCIA SEM ESPECIALIDADE.** O formulário do
  balcão EXIGE a especialidade ("Consulta precisa de especialidade") — e
  `ListaEsperaService.ChamarAsync` **não tinha o parâmetro**. A recepcionista escolhia
  "Consulta / Psiquiatria", salvava sem erro nenhum, e o horário nascia sem ela; como o
  atendimento herda a especialidade do AGENDAMENTO na confirmação da presença, a guia saía
  sem a informação que a operadora cobra. **Campo que a TELA exige e o SERVIÇO não recebe é
  um campo que só existe para o usuário** — e a distância entre a validação e o descarte é
  de duas telas, então ninguém as lê juntas.

  ⚠️ **FALTA MARCADA POR ENGANO NÃO TINHA VOLTA NO BALCÃO.** "Marcar falta" e "Cancelar
  horário" são dois botões vermelhos lado a lado, e o clique errado some com o cartão do
  quadro na hora — porque o bloco inteiro de ações da janela do horário estava sob
  `Visibility={Binding EmAberto}`. A única porta para reabrir ficava no app de
  **FATURAMENTO**: outro programa, de outra pessoa. `RemarcarAsync` já traz o horário de
  volta (`Status = Agendado`); o que faltava era a porta — a variante da parcela 48 ("alerta
  sem porta") aplicada a um ERRO em vez de a um alerta, que é pior, porque o erro é de quem
  está olhando a tela.
  Na mesma janela, os seis botões apareciam **acesos** para quem só LÊ a agenda — enfermagem,
  financeiro e faturista têm `VerAgenda` — e a recusa chegava depois do clique. O vizinho já
  fazia certo desde sempre (o vão livre da grade e os botões da barra têm `IsEnabled`); era
  esta janela que não tinha. E o `IsEnabled` é **por botão, não no bloco**: "Confirmar pelo
  WhatsApp" e "Comprovante" são leitura, e apagá-los tiraria de quem só lê uma coisa que ele
  fazia ontem.

  ⚠️ **A RELEITURA DE FUNDO DO CONSULTÓRIO COBRIA O QUADRO CHEIO — e as três telas irmãs do
  balcão já faziam certo.** O `catch` do "Meu dia" acendia `NaoVerificado` **sem olhar se a
  carga era silenciosa**, e ele liga a sobreposição do `EstadoDaTela`: uma engasgada do banco
  na batida de um minuto escrevia "vazia por falha de leitura" por cima de um quadro com
  pacientes, sem ninguém ter clicado em nada. O comentário logo abaixo prometia o contrário
  ("a tela segue com o quadro do minuto anterior") havia parcelas — comentário que descreve
  degradação sem o código que a realiza é o defeito da parcela 67.
  Agenda, Fila e Painel do balcão saem do `catch` sem tocar em nada quando a carga é
  silenciosa. **Quando três irmãs fazem igual e uma faz diferente, a diferente é a que
  ninguém releu** — e o reset de `NaoVerificado` na ENTRADA tem de ser guardado junto, senão
  a batida silenciosa que também falha limpa o aviso da carga anterior.

  ⚠️ **E O RECADO ERA APAGADO NO MESMO INSTANTE EM QUE ERA ESCRITO.** "Fulano foi chamado — a
  recepção já está vendo o aviso" é a única confirmação de que o recado atravessou os dois
  módulos, e as quatro ações do quadro escreviam a mensagem e chamavam `CarregarAsync()` — a
  carga que a PESSOA pede, que começa zerando `Mensagem`. O comentário da própria carga já
  dizia que a recarga de fundo não apaga o recado da última ação; o que faltava era **as
  chamadas usarem a sobrecarga silenciosa**. Regra: **ação que escreve um recado e recarrega
  em seguida recarrega em SILÊNCIO** — senão a tela desfaz o que a ação acabou de dizer.

  ⚠️ **"MEUS PACIENTES" FAZIA 1 + 200 CONSULTAS A UM BANCO REMOTO PARA CALCULAR DOIS
  NÚMEROS.** A carteira do consultório lia o prontuário INTEIRO de cada paciente, um por um,
  para extrair a primeira e a última EVA — texto da evolução, conduta e orientações vinham
  junto, e eram descartados. É uma das duas portas do módulo, e ficava dezenas de segundos em
  "Montando a sua carteira…", repetindo a espera a cada volta à tela.
  Virou `ParesDeEvaDosPacientesAsync`: uma consulta, três colunas. **Antes de escrever um
  laço com `await` dentro, pergunte quantas linhas ele pode ter no pior caso** — 200 é o
  limite padrão desta tela, e estava no próprio parâmetro do método.

  ⚠️ **O QUE NÃO FOI CORRIGIDO, e por que a decisão é essa.** `ConfirmarPresencaAsync`
  grava em DOIS `SaveChanges`: o primeiro (dentro de `LancarAsync`) persiste o atendimento
  e as guias, o segundo carimba `Status`/`AtendimentoId` no agendamento. Falhando o
  segundo — conflito de `xmin` entre as duas máquinas do balcão, queda de conexão —, **as
  guias existem e o agendamento não sabe**: a recepcionista não vê guia nenhuma, o cartão
  continua com "Concluir" aceso, e o segundo clique gera outro jogo de guias. É o incidente
  de 12/08 (parcela 65) uma camada abaixo, e a idempotência daquela parcela não alcança,
  porque a chave dela é justamente o `AtendimentoId` que não chegou a ser gravado.
  Não corrigi, e isso é decisão: juntar os quatro `SaveChanges` numa transação mexe no
  caminho que **gera as guias** e que o faturamento em produção também percorre, e este
  ambiente não tem Postgres para reproduzir o conflito (os testes rodam em SQLite, onde
  `xmin` não existe — o mesmo buraco que a parcela 67 documentou para as datas com fuso).
  A regra do projeto é anterior a mim: **não corrija por dedução, reproduza antes.** Fica
  escrito aqui como o primeiro item da próxima parcela, com o roteiro: transação única em
  `ConfirmarPresencaAsync` e teste contra Postgres de verdade, não contra SQLite.

  **A fila restante, medida e priorizada** (confirmada na refutação, não corrigida nesta
  parcela — cada uma com o motivo de ter ficado):
  1. ~~Horário sem profissional some do quadro do médico~~ — **pago na parcela 95**: marcar
     EXIGE quem vai atender (`AgendaService.AgendarAsync`), e só o encaixe passa sem dono.
     A decisão que aqui esperava a clínica foi tomada por ela ("cai na agenda do médico
     respectivo"). Set/2026 acrescentou o outro lado da pergunta — a JORNADA do
     profissional e a busca da próxima vaga com ele.
  2. **O "+" não volta no vão de um horário cancelado** (`CelulaAgenda.Livre` conta os
     cartões sem olhar `ForaDoDia`). A correção esbarra numa escolha de leiaute: o cartão
     cinza é desenhado por cima do botão, então liberar o vão exige ou pôr o "+" na frente
     (e perder o clique que abre o cancelado) ou mudar o desenho da célula. O caminho de
     volta agora existe pela janela ("Reabrir este horário"); a decisão de leiaute é do
     cliente.
  3. ~~A espera média do painel conta quem chegou e virou FALTA~~ — **pago na validação
     da parcela 86**: a falta ficou de fora da média como o cancelado.
  4. ~~Bloqueio de parte do dia não enxerga a sessão que COMEÇA antes dele~~ — **pago na
     parcela 86**: a consulta-base do `MarcadosDentroAsync` abre no começo do DIA e quem
     decide é o `ColideCom` (o filtro certo estava sobre a consulta errada).
  5. ~~`ConfirmarPresencaAsync` não deixa linha na trilha~~ — **pago na parcela 70**: a
     trilha `PresencaConfirmada` entra no MESMO commit atômico do `ConfirmarNucleoAsync`.
  6. **Trocar a DATA no formulário não reconfere elegibilidade** (carteirinha, cota,
     consulta a renovar) — **metade paga**: o Novo atendimento reconfere tudo ao trocar a
     data (parcela 70); o FORMULÁRIO DA AGENDA (o fallback, e o caminho da lista de
     espera) só reconfere conflitos e a consulta — a elegibilidade completa continua
     presa ao `AoTrocarPaciente`.
  7. **Horário de profissional (ou sala) DESATIVADO some da grade** — a coluna só é montada
      para os ATIVOS, e o horário com dono inativo não cai em "Sem profissional": não existe
      coluna para ele. O resumo continua contando sobre a lista inteira, então o cabeçalho
      diz "12 horário(s)" e a grade desenha 11; o vão fica clicável e a recepção marca outra
      pessoa por cima, enquanto o paciente segue na Fila e na folha impressa do dia. A caixa
      da tela de equipe diz "Ativo (aparece na agenda)", prometendo o contrário do que
      acontece. É diferente do item 1: aqui o `ProfissionalId` existe — quem sumiu foi a
      coluna.
  (O item que estava aqui — os sete ViewModels restantes com serviço Scoped no construtor —
  saiu da fila: foram corrigidos na mesma parcela, e a checagem 37 fecha o assunto. Quatro
  outros saíram na rodada "todas as notas em 8" do Consultório, mais abaixo: a autoria da
  fila, as observações que não chegavam, a evolução avulsa cobrindo duas sessões e o
  Atender sem conferir `VerProntuario`. O 6 saiu na parcela 70, junto da unificação. Os
  itens **3, 4 e 7** foram pagos na parcela 85 — ver a lição abaixo; o **1** na 95. Restam
  o **5** (a transação única + trilha do `ConfirmarPresencaAsync`, que exigem Postgres de
  verdade) e o **2**, que espera a decisão de leiaute do cliente.)

- **A VARREDURA DA RECEPÇÃO DEPOIS DA AGENDA — três achados, nenhum bloqueador, e todos
  da mesma família** (parcela 69, continuação). Com a Agenda fechada, a pergunta virou
  "o que mais no balcão promete e não cumpre?". A varredura foi por eixo (mensagem de
  êxito invisível, EstadoDaTela na raiz, método órfão, propriedade sem leitor, texto de
  tela que promete mecanismo), e o resultado tem as duas metades: o que achou e o que
  CONFERIU E ESTAVA CERTO — sem a segunda, a próxima varredura refaz esta.

  ⚠️ **A ORIGEM DO PACIENTE ERA PERGUNTADA A TODO CADASTRO E NINGUÉM SOMAVA.** O balcão
  pergunta "como conheceu a clínica?" desde que o campo existe, e a resposta era lida em
  UM lugar: a própria ficha, uma pessoa por vez ("Indicação de Maria"). Nenhum serviço,
  relatório ou tela agregava — a direção não tinha como responder "quantos vieram por
  indicação neste ano?", que é a única razão de a pergunta ser feita. O detalhe que dói:
  o comentário do rótulo em `RotulosEnum` já dizia *"é um dos poucos campos que a direção
  lê agrupado num relatório"* — **o rótulo foi preparado para um relatório que nunca
  existiu**. Virou `OrigemPacientesService` + a tela "De onde vêm os pacientes" no grupo
  Marketing/Recall do Gerente. As decisões:
  (a) **"Estreou no período" = o PRIMEIRO atendimento caiu no período** — `Paciente` não
  tem data de cadastro, e a estreia é o fato mais honesto disponível: cadastro sem
  atendimento é intenção, atendimento é a clínica trabalhando. Quem já vinha e continuou
  vindo não estreou. A tela ESCREVE a definição: número cuja definição só existe no
  código é um número que cada leitor interpreta de um jeito.
  (b) **"Não perguntado" é linha de primeira classe, ordenada junto das outras** — quando
  ela encabeça a tabela, o achado do relatório é o balcão ter parado de perguntar, e o
  selo "colher no cadastro" transforma o número em tarefa. Escondê-la no rodapé faria a
  direção decidir anúncio sobre uma amostra que ninguém está colhendo.
  (c) **Quem indica agrupa por nome NORMALIZADO** (trim + maiúsculas fora): "maria silva"
  e "Maria Silva " são a mesma pessoa digitada por duas recepcionistas, e separá-las
  esconderia justamente a maior indicadora da clínica.
  A lição que generaliza: **quando um campo é PERGUNTADO no balcão, procure quem RESPONDE
  com ele.** Campo que só a ficha individual lê é pergunta feita de graça — e rótulo
  preparado "para o relatório" é promessa que se confere como as outras.

  ⚠️ **"CANCELAR O RESTO DA SÉRIE" CONFIRMAVA ÀS CEGAS — com a prévia pronta e sem
  chamador.** O diálogo dizia "cancelar TODAS as sessões ainda marcadas?" sem dizer
  QUANTAS nem QUAIS; a contagem só aparecia no snackbar, DEPOIS do estrago. E
  `AgendaService.DaSerieAsync` — "sessões de uma série, na ordem em que acontecem" —
  existia sem um único chamador em produção: a leitura que responde a pergunta do diálogo
  estava pronta desde que a série nasceu. Agora a prévia vem antes (quantas e as datas,
  até dez), série sem sessão em aberto ganha aviso em vez de silêncio, e falha na leitura
  IMPEDE a pergunta — confirmar "todas" sem saber quantas é exatamente o que a correção
  existe para acabar. **Ação destrutiva em LOTE diz o tamanho do lote ANTES do clique**;
  "todas" não é número.

  ⚠️ **NÃO EXISTIA TROCAR A PRÓPRIA SENHA.** `AcessoService.TrocarSenhaAsync` — o método
  que CONFERE a senha atual antes de aceitar a nova — estava órfão. O único caminho de
  troca era o forçado (a direção emite provisória com "deve trocar", o login cobra a
  definitiva): quem desconfiasse que alguém viu a sua senha precisava pedir à direção,
  que escolhia a nova e a ENTREGAVA — **trocando um segredo comprometido por um que já
  nasce compartilhado**. Entrou `TrocaSenhaWindow` ao lado do "Trocar usuário" — nos DOIS
  apps, porque é o débito permanente da Fase 4 (a lição da parcela 60: a mesma ação nos
  dois lados, e a cópia que faltar é onde a capacidade some). Sem permissão a exigir: a
  prova de posse é a senha atual, e é o serviço que a confere — validar na tela seria a
  segunda definição da mesma regra. A lição: **fluxo de segurança com só a metade
  administrativa não está completo** — o reset pela direção existia desde a parcela 5, e
  a metade voluntária ficou 60+ parcelas sem porta porque o fluxo forçado dava a
  impressão de assunto coberto.

  **CONFERIDO E LIMPO — para a próxima varredura não refazer**: os 13 candidatos a
  "mensagem de êxito invisível" eram falsos positivos (todos só escrevem ERRO no campo; a
  parcela 62 já tinha limpado); nenhum `EstadoDaTela` na raiz de página; a tela de
  Retorno confere `TemConsentimento` pela MESMA leitura da campanha e escreve o motivo na
  linha; a ficha→CRM lê `ContatoCampanha` de verdade, com contador de geração; o circuito
  autorização→aviso de cota fecha. E `TracoAsync` era código morto (terceira definição de
  uma leitura que dois serviços já fazem direto no repositório) — método órfão continua
  sendo SINTOMA: dos 15 órfãos reais, três eram feature faltando e o resto era duplicata.

- **A VARREDURA DO FINANCEIRO — mais limpo que a Recepção estava, e o achado grave era o
  mais antigo de todos** (parcela 69, continuação). Os mesmos eixos da varredura do
  balcão, aplicados ao módulo do dinheiro. O que achou, o que ensina, e o que estava
  CERTO — na ordem de sempre.

  ⚠️ **O ESTOQUE NÃO TINHA EXTRATO — e o motivo escrito da perda não tinha leitor.** "O
  saldo é a SOMA dos movimentos" é regra desde a parcela 4, a perda EXIGE motivo escrito
  (única recusa do serviço) e o acerto de inventário grava direção, observação e
  `CriadoPor` (parcela 30) — e `MovimentosAsync`, o extrato de um item, ficou órfão desde
  que nasceu: tudo gravado, nada lido. A clínica exigia justificativa da funcionária e a
  guardava num lugar que nem a direção alcançava — a trilha da parcela 21 de novo, na
  versão do estoque. E no dia em que o saldo não bate, a única pergunta útil ("QUAIS
  movimentos produziram este número?") não tinha tela. Virou o botão "Extrato" na linha
  do item: todos os movimentos com o saldo APÓS cada um, o motivo da perda em destaque,
  a direção do acerto escrita e quem fez.
  Três decisões que vieram junto, e as duas primeiras são maiores que a tela:
  (a) **A recusa da perda sem motivo subiu para `MovimentarAsync`** — ela morava SÓ no
  wrapper `PerderAsync`, que nenhuma tela chama; a janela genérica entra pelo método
  genérico, e a única barreira era a validação da TELA. É o defeito recorrente vestido de
  validação (a regra do número da guia): quem valida na tela cobre uma porta. O teste
  novo FALHA no código antigo.
  (b) **O sinal do movimento desceu para o DOMÍNIO** (`MovimentoEstoque.Delta`/`DeltaDe`),
  porque agora há DOIS somadores — o saldo do repositório e o extrato — e duas cópias da
  conta divergiriam exatamente no AJUSTE, o movimento raro que ninguém testa de cabeça.
  De quebra, isso removeu o par `Sinal`/`QuantidadeComSinal`: dizia "−1 para tudo que não
  é entrada", erraria o ajuste PARA CIMA, e nunca doeu por nunca ter tido um chamador —
  **propriedade de domínio errada sem chamador é uma mina, não um detalhe: o primeiro uso
  a detona.** `O_saldo_oficial_e_a_soma_dos_Deltas_sao_o_mesmo_numero` fixa que o extrato
  não pode desmentir a tela que ele existe para explicar.
  (c) O extrato acumula NA ORDEM CRONOLÓGICA e exibe do mais recente — a última linha TEM
  de bater com o saldo da lista de trás.

  ⚠️ **A FAIXA DA ALÍQUOTA ÚNICA SUMIA PELA CONDIÇÃO ERRADA — e a frase ao lado mentia
  junto.** O serviço cai no fallback quando não há tributo VIGENTE NO DIA
  (`ApurarAsync`); a tela escondia a faixa quando havia tributo CADASTRADO
  (`Tributos.Count` no DataTrigger). Na janela entre as duas condições — tributos
  cadastrados com vigência futura, ou todos expirados — a alíquota única continuava
  valendo no cálculo com o campo dela INVISÍVEL, e a frase `OrigemDaCarga` dizia "Nenhum
  tributo cadastrado" com a lista cheia. O detalhe que dói: o MESMO ViewModel calculava a
  frase com a condição certa (`ValendoAgora`) e a visibilidade com a errada, a dez linhas
  de distância. **Campo invisível que faz efeito é pior que o campo morto que a parcela
  49 tirou desta mesma tela** — e a lição é: quando a tela mostra/esconde algo POR CAUSA
  de uma regra de serviço, a condição da visibilidade é A MESMA do serviço, nunca uma
  aproximação ("cadastrado" não é "vigente").

  ⚠️ **O CSV DA INADIMPLÊNCIA SAÍA COM NOME + DÍVIDA DO PACIENTE SEM A SEGUNDA BARREIRA.**
  O `Receber` da mesma tela tinha o `Exigir`; o `Exportar` não — a mesma lacuna que a
  parcela 64 fechou nos exports do Gerente, sobrevivendo no módulo vizinho. **Quando um
  defeito de padrão é corrigido num módulo, os exports dos OUTROS módulos entram na mesma
  varredura** (é a lição da 64 sobre alcance, cobrada de novo). O FluxoCaixa ganhou o
  mesmo tratamento (agregados, sem dado pessoal — mas a regra "CSV é saída de dado" não
  tem exceção por conteúdo).

  ⚠️ **"CÓDIGO COPIADO" DO PIX NUNCA APARECIA.** A janela nasceu DEPOIS das limpezas das
  parcelas 62/64 e repetiu o padrão que elas mataram: o sucesso da cópia escrevia
  `MensagemEhErro = false` e a única superfície de mensagem era
  `Visibility="{Binding MensagemEhErro}"`. Quem copiava não via confirmação — e clicava
  de novo, ou copiava à mão sem precisar. Correção pelo padrão canônico (quem decide se
  aparece é o TEXTO; quem decide a cor é a GRAVIDADE). A lição é sobre recorrência:
  **padrão corrigido em N telas volta na tela N+1 se só as telas foram corrigidas** — o
  par `AlertaPerigo`+`MensagemEhErro` continua sendo o que a mão escreve primeiro.

  **Menores da mesma rodada**: cinco `Clear()` com await no meio (Contas 2×, Resultado,
  Taxas 2×) — cargas de abertura, janela pequena, corrigidos pelo padrão da 62; e
  `Resolvida` no ExtratoBanco, calculada e nunca lida, removida.

  **CONFERIDO E LIMPO — para a próxima varredura não refazer**: menu × `CriarTela` fecha
  (os 3 "sem caso" são grupos com abas, resolvidos pelo shell); nenhum `EstadoDaTela` na
  raiz nem sem gatilho; dos 21 arquivos com o padrão de `Border` de mensagem, só o Pix
  escrevia êxito; "Sem categoria aparece, nunca some" cumprida (Fluxo e Resultado); a
  reabertura do caixa fica marcada COM o motivo lido na tela; `DesfazerConfirmacaoAsync`
  tem porta; "Só no banco" e "Só no sistema" ambos no extrato OFX. **E dois falsos
  positivos meus que valem regra de varredura**: `ValorRotulo` parecia órfão porque o
  leitor é um template COMPARTILHADO no shell (`ItemBarraRotulada`) — scan de leitor que
  só olha o XAML do módulo não enxerga template compartilhado; e `EntrarAsync`/
  `PerderAsync`/`CustoDoAtendimentoAsync` pareciam duplicatas e são WRAPPERS de uma
  definição só (delegam a `MovimentarAsync`/`Precificar`) — **wrapper fino não é segunda
  definição; a pergunta é onde mora a REGRA, não quantas assinaturas existem.**

- **A VARREDURA DO GERENTE E DO CONSULTÓRIO — nenhum grave, e o melhor achado estava no
  vizinho** (parcela 69, fim da rodada). Os dois módulos mais auditados do projeto
  (Gerente: 54/64; Consultório: 61/68 e a auditoria da Agenda) saíram os mais limpos —
  as varreduras anteriores pagaram. Os quatro achados, e o que cada um ensina:

  ⚠️ **DUAS DEFINIÇÕES DO TOTAL DO BADGE — e o teste fixava a que ninguém executa.**
  `PendenciaService.TotalPendenciasAsync` dizia, no próprio comentário, ser "o total para
  o badge do topo" — e estava ÓRFÃO: o badge real era uma segunda conta, escrita à mão no
  `DashboardViewModel`, com um comentário jurando usar "o mesmo critério" e CITANDO o
  método que não chamava. As duas contas eram iguais hoje; a primeira mudança em qualquer
  uma divergiria em silêncio — e `PendenciasFaturamentoTests` fixava o lado morto. O
  critério virou UMA função (`TotalParaBadge`, estática) que os dois lados chamam, e o
  teste passou a exercitar o que a tela usa. A lição: **comentário que cita um método como
  "o critério" é promessa que se confere com grep — se o método está órfão, o comentário
  é a segunda definição se anunciando.**

  ⚠️ **A TELA MANDAVA COMPRAR TABLET — e a decisão documentada é MONITOR.** A parcela 66
  gastou uma seção inteira em "por que MONITOR e não tablet" (tablet é outro computador;
  monitor touch é só mais uma tela do Windows), a especificação de compra está em
  `docs/termo-assinado-pelo-paciente.md` §3.8 — e TRÊS textos de tela diziam "tablet":
  a configuração do termo no Gerente, a instrução que o PACIENTE lê na janela de
  assinatura e a descrição da folha na central de documentos. Texto de tela que contradiz
  a decisão de arquitetura induz a clínica a comprar o hardware errado — e o texto novo da
  janela de assinatura lembra o modo de UMA tela ("ou com o mouse"), que é o modo real de
  quem ainda não comprou o touch. **Quando uma decisão renomeia o mundo (tablet→monitor),
  os textos de tela entram no mesmo grep da decisão.**

  **Menores**: `MinhaSemana` e `AnexosSessao` limpavam a coleção antes do await (o item
  que a fila da parcela 69 já listava — pago); `TemAssinatura` na linha da lista de
  infusão, calculada e nunca lida (a `Situacao` textual já diz "assinada"), removida.

  **CONFERIDO E LIMPO — para a próxima varredura não refazer**: menu × `CriarTela` fecha
  nos DOIS (as cinco chaves "sem caso" do Consultório caem no `or` múltiplo do
  workspace — padrão que o scan de regex simples não vê); nenhum `EstadoDaTela` raso ou
  sem gatilho; TODAS as mensagens de êxito dos dois módulos aparecem (o Gerente foi limpo
  na 64, e as janelas novas do Consultório já nasceram no padrão certo — a primeira
  geração de telas que nasceu imune ao defeito); `PoliticaBackupService` roda na abertura
  do Gerente; o `AlertaDeItem` do PHQ-9 chega à tela de Avaliações; a fila de pendências
  do Gerente critica o número da guia linha a linha com a `RegraNumeroGuia` (parcela 51
  cumprida); "quem não consentiu aparece CONTADO" cumprida nas Campanhas. **Falsos
  positivos com lição**: `EhVerde`/`Urgencia`/`FormatoGuia` pareciam órfãos e alimentam
  DataTriggers e o fluxo de baixa (o verde é o DEFAULT do estilo — propriedade que define
  o caso-padrão não aparece em trigger nenhum); `Enunciado` alimenta `Rotulo`;
  `Correcoes` alimenta `Retificada`/`CorrecoesTexto` — **scan de leitor precisa seguir
  UMA derivação interna antes de acusar.**

- **A RODADA "TODAS AS NOTAS EM 8" DO CONSULTÓRIO** (parcela 69, encerramento — o
  diagnóstico pessimista deu notas de 4 a 8 por dimensão, e a direção mandou: *"Faça
  todas as notas baterem 8!"*). Tudo o que segurava cada nota foi pago de uma vez; as
  lições, na ordem do que ensinam:

  ⚠️ **A EVOLUÇÃO AVULSA CASA COM NO MÁXIMO UMA SESSÃO DO DIA.** Com duas sessões do
  mesmo paciente no mesmo dia (manhã e tarde — quase sempre especialidades diferentes),
  uma evolução sem `AgendamentoId` dava as DUAS por escritas: a segunda sumia da
  cobrança, e abrir qualquer uma na tela de Atendimento CONTINUAVA o mesmo texto,
  fundindo duas sessões num registro só. `EvolucaoDoHorario` agora recebe as sessões
  IRMÃS do dia (de TODOS os profissionais, antes do filtro — sem conhecê-las não há como
  saber a vez de cada uma na fila da avulsa) e distribui cronologicamente: avulsas na
  ordem em que foram escritas, sessões na ordem em que aconteceram; cancelada não
  disputa; quem tem evolução própria não disputa. É escolha determinística sobre um dado
  que não diz de quem é — e **a que erra, erra para o lado de COBRAR, nunca de calar**.
  A tela de Atendimento busca as irmãs (`SessoesDoPacienteNoDiaAsync`) porque ela conhece
  o horário chamado e não as vizinhas dele.

  ⚠️ **A FILA GANHOU AUTORIA — e o movimento que APAGA escreve o que apagou.** Os cinco
  movimentos (chegada, chamada, desfazer, entrada, voltar) recebem `operador`
  OBRIGATÓRIO (parâmetro com default deixaria todo chamador antigo compilando sem
  autoria — o compilador é quem acha os call sites) e gravam `EventoAuditoria` no mesmo
  `SaveChanges`. Duas regras que não são detalhe: **movimento idempotente que não mudou
  nada não grava linha** (trilha com duplicata a cada clique é trilha que ninguém lê), e
  **`VoltarEtapaAsync`/`DesfazerChamadaAsync` escrevem NA TRILHA o carimbo que apagaram,
  com o valor** — depois do apagamento ele não existe em mais lugar nenhum, e "quem
  desfez e o que dizia" é a pergunta de qualquer conferência.

  ⚠️ **A MINHA SEMANA VIROU A GRADE DO BALCÃO — e o montador mora na APPLICATION.** A
  pilha de cartões por dia era o desenho que a parcela 58 condenou com a frase que
  decide ("numa grade, o vazio TEM tamanho"), sobrevivendo na tela irmã da mesma
  pergunta. As regras da grade são AS MESMAS do balcão de propósito (janela padrão
  esticada, continuação, cancelado marcado sem cobrir, bloqueio escrito na célula) —
  duas grades da mesma agenda com regras diferentes divergiriam sobre o mesmo horário; a
  janela padrão (7h–20h) subiu para o DOMÍNIO (`Agendamento.AberturaPadraoGrade`) pela
  mesma razão. E `GradeSemana.Montar` é função PURA (recebe o relógio) na Application,
  porque a camada de tela do WPF não compila nos testes: **tudo o que decide um desenho
  precisa morar onde o `dotnet test` alcança** — a grade nasceu com sete testes, coisa
  que nenhuma tela do projeto teve. A diferença deliberada: a célula livre NÃO tem
  clique de marcar — quem marca é o balcão; aqui o vão é informação.

  ⚠️ **"CHAMAR PRÓXIMO" NO MODO SEM VÍNCULO CHAMAVA O PACIENTE DE OUTRO PROFISSIONAL.**
  Sem `Profissional` ligado ao login a tela mostra a clínica inteira — e "o primeiro da
  recepção" podia ser de qualquer colega: o clique cego anunciava um nome para a sala
  errada. O botão fica DESLIGADO nesse modo, com a explicação na tela e a segunda
  barreira no comando; **chamar pelo CARTÃO continua liberado**, porque ali a escolha é
  de quem leu o nome antes de clicar. E **a fila corre só HOJE**: os botões de movimento
  somem num dia passado/futuro e o arrasto recusa DIZENDO — chamar alguém de ontem
  carimbaria hora num horário morto e a tela afirmaria "a recepção já está vendo o
  aviso" sobre uma fila que só relê o dia corrente (afirmação falsa com cara de
  confirmação).

  **O resto da rodada, em uma linha cada**: as OBSERVAÇÕES do horário chegaram ao cartão
  do Meu dia (`SessaoDoDia.Observacoes` — onze campos e nenhum era o recado do balcão);
  férias/feriado apareceram (célula da semana + linha "Agenda fechada neste dia" no Meu
  dia — dia fechado aparecia como dia VAZIO, que se lê como "ninguém marcou"); a batida
  de 1 min parou de reler 30 dias de pendências (a recarga silenciosa relê só o quadro —
  quem escreve evolução está NESTA máquina); "Atender", a dívida e o "Ver a lista" dos
  Meus números ganharam as duas barreiras de `VerProntuario` (`NavegacaoSuite.Ir`
  devolve false em silêncio para quem não tem o bit do destino); o botão "Excluir" de
  medidas/avaliações virou "Cancelar…" (**o ato sempre foi cancelar com motivo — o
  RÓTULO é que mentia sobre o que o registro clínico não faz**); a frase da tendência da
  dor parou de sair duplicada (quando ela preocupa, só o alerta a diz — um estado por
  pergunta); a lista de problemas virou LINHA DENSA (cada problema tinha cinco linhas e
  QUATRO botões empilhados ≈ 140 px — três problemas empurravam o histórico, que é o
  assunto da aba, para fora da vista; detalhe foi para a dica, ação virou fileira, nada
  foi tirado); o "N sem evolução" da semana diz "NESTA semana" (o badge do Meu dia conta
  a fila de trabalho de 30 dias — dois números com a mesma frase se leem como o mesmo
  número errado); e o cabeçalho do Atendimento diz a DATA do horário quando não é hoje
  (a dívida e a semana abrem dias passados, e "da agenda de hoje" mentia sobre a data a
  que o registro ia ficar ligado).

  **O que ficou de fora, e é decisão**: a trilha do `ConfirmarPresencaAsync` (item 5 da
  fila) — mexer nele encosta no caminho que gera as guias, e vai junto da transação única
  com teste em Postgres de verdade, que este ambiente não reproduz.

- **A GUIA NASCE QUANDO O ATENDIMENTO ENTRA NO SISTEMA — e a duplicidade morreu de
  desenho, não de aviso** (parcela 70; o mapa completo está em
  `docs/guia-no-agendamento.md`, e é lá que se atualiza). O pedido da direção: *"o
  confirmar presença não pode gerar guia! A guia precisa nascer no momento em que a
  secretaria coloca o atendimento no sistema, seja avulso ou agenda"* — e depois:
  *"unificar tudo em um lugar só"*. As decisões que não são óbvias pelo código:
  **O clique virou UM SaveChanges.** `LancarAsync` se dividiu em CRIAÇÃO
  (`MontarAsync`, não grava) × PRESENÇA (`PrepararPresencaAsync` encena NCs no mesmo
  commit; `ConcluirPresencaAsync` renova consulta depois, falha vira aviso), e
  `ConfirmarNucleoAsync` pendura o atendimento no agendamento pela NAVEGAÇÃO — o EF
  grava horário + atendimento + guias + carimbo + trilha numa transação. Ou existe
  tudo, ou nada: o incidente de 12/08 (três encaixes em 71s) morava nos vãos entre
  cinco SaveChanges. `AtomicidadeDoLancamentoTests` prova com um DbContext que falha no
  N-ésimo save e afirma ESTADO, não mensagem — dá para provar atomicidade no SQLite.
  **O regime novo mora atrás de uma chave** (`ChaveGuiaNoAgendamento`, nasce DESLIGADA):
  os cinco apps se atualizam por canais separados e dividem um banco — um binário velho
  confirmando presença de um horário que o novo já marcou COM guia duplicaria; a chave
  se liga depois de todos atualizarem, e ligar dispara o backfill de `RealizadoEm`.
  **`Atendimento.RealizadoEm` é o que separa "marcado" de "aconteceu"**: leitor que
  quer dizer "sessão realizada" filtra por ele (retenção, origem, estreia); a COTA
  conta os ATIVOS (realizado OU guia aberta/baixada) — é assim que a 11ª sessão de uma
  autorização de 10 avisa NA MARCAÇÃO, não na glosa. Cancelar/faltar suspende as guias
  abertas (`NaoAplicavel` + marca `MarcaSuspensao` na observação — NUNCA NC, que a
  volta do paciente reabriria, e NUNCA valor novo de enum, a mina da parcela 67);
  reabrir devolve SÓ as marcadas; baixada não se toca — vira aviso. Remarcar de data
  desloca as previstas; de modalidade, regera (a velha fica "Substituída") ou RECUSA se
  algo já saiu da clínica (baixa, lote, NC).
  **A tela pergunta QUANDO** (o §3.7): "o paciente está aqui — lançar agora" × "marcar
  dia e horário", no MESMO formulário — modalidade, prévia, elegibilidade e capa servem
  aos dois modos. A agenda MOSTRA e mexe no existente; criar mora no Novo atendimento, e
  o clique no vão da grade chega lá pré-preenchido (`PreenchimentoNovoAtendimento`,
  singleton de UM pedido que consumir LIMPA — pedido órfão pré-preencheria a abertura
  de amanhã com o clique de ontem). **Sem profissional não é sorteio**: marca, avisa o
  custo (fora do Meu dia e do repasse) e nunca escolhe sozinho — repasse é dinheiro.
  **A pergunta de duplicidade ficou INFORMADA** (`CapasDoDiaAsync`, ponto único): número
  do atendimento, modalidade, quem lançou e o placar das baixas, no aviso ao escolher o
  paciente E na pergunta do clique — nas DUAS portas de criação (Novo atendimento e o
  formulário da agenda, que sobrevive para lista de espera/remarcar/fallback).
  **Permissão acompanhou o momento do fato** (a lição da parcela 69 aplicada de novo):
  com a chave ligada, marcar CRIA guias — o Salvar exige `EditarAgenda` E
  `LancarAtendimento`; e quem tem só `EditarAgenda` não perdeu nada, porque o
  redirecionamento da agenda cai no formulário antigo quando `NavegacaoSuite.Ir`
  devolve false (regra 3 do faturamento: a unificação não tira capacidade).
  ⚠️ **A auditoria do próprio diff achou o leitor que o inventário da Fase 3 pulou — e
  era o de DINHEIRO.** O alimentador do REPASSE (`AgendamentosComAtendimentoAsync`)
  filtrava só "tem atendimento": com a chave ligada, a sessão MARCADA — e a cancelada,
  que mantém o `AtendimentoId` com as guias suspensas — entraria na regra "valor por
  atendimento", pagando sessão que ninguém deu. O filtro virou `Status == Realizado`
  (no regime antigo não muda nada: `AtendimentoId` só nascia junto do `Realizado`), com
  teste que confirma a presença e vê o valor aparecer. Os outros dois achados da mesma
  auditoria: o formulário de agendamento do FATURAMENTO sem nenhuma `Exigir` (a cópia
  que ficou para trás, parcela 60 — ganhou as duas guardas, inclusive a do duplo bit
  com a chave ligada) e a tela QUANDO reconferindo cota/consulta/elegibilidade ao
  trocar a DATA (o item 6 da fila da parcela 69, que a unificação tornou cotidiano) —
  com a elegibilidade completa (dívida, glosa, pacote, termo) que o formulário
  substituído já mostrava: **porta unificada não pode mostrar MENOS que a porta que
  aposentou.**
  ⚠️ **A auditoria do GERENTE sob o regime novo — conferido e limpo, para a próxima
  varredura não refazer** (parcela 70, depois do achado do repasse): todo leitor do
  Gerente que responde "quantas sessões" filtra certo — `IndicadoresService` (Atendidos/
  produtividade/ocupação/série mensal) e `RelacionamentoService` (padrão de falta) por
  `Status == Realizado`; `CompletudeProntuario` divide evoluções por Atendidos
  (Realizado); retenção e origem/estreia por `RealizadoEm` (Fase 3); metas/orçamento/
  painel por dinheiro ou pelo serviço dono; a fila de pendências do Gerente pelo
  `EstaPendente`, que exige a data prevista alcançada. E os relatórios por CÓDIGO
  (`RelatorioService.PorConvenio`, rentabilidade) **contam a guia marcada quando o
  período pedido inclui dias futuros — e isso é decisão, não defeito**: a guia existe e
  pode ser baixada antecipadamente; filtrar pela data esconderia justamente a baixada
  antecipada, que é falha exibida como sucesso. Período já encerrado sai exato (a
  cancelada vira `NaoAplicavel` e o `CodigosNoPeriodoAsync` já a exclui). A chave em
  Configurações do Gerente tem as duas barreiras (`GerenciarUsuarios` no item e no
  `ExecutarAsync`), grava só quando MUDOU (ligar dispara o backfill, e repeti-lo a cada
  Salvar escreveria a tabela inteira à toa) e o aviso na tela diz a ordem: atualizar os
  cinco apps ANTES de ligar.
- **O carimbo da assinatura saía no ALTO da folha: o eixo Y do `/Rect` é medido do PÉ da
  página** (parcela 68, 8ª rodada — a clínica mandou o print: o carimbo do prescritor por
  cima do título "PRESCRIÇÃO DE EXECUÇÃO INTERNA", e o rodapé com o espaço reservado
  vazio logo abaixo). A fórmula era `AlturaPagina - MargemPagina - AlturaRodape + 2`, ou
  seja a distância a partir do TOPO — e o retângulo vai para o `/Rect` da anotação, que no
  PDF tem origem no canto INFERIOR esquerdo. **Medido antes de corrigir**: assinei um PDF
  pedindo `Y=640` e o arquivo saiu com `/Rect [40 640 280 686]` — o PDFsharp grava o que
  recebe, sem converter. A conta certa é a do próprio rodapé:
  `MargemPagina + AlturaRodape - AlturaFaixaAssinatura + 2`.
  ⚠️ **Saiu assim em TODO documento assinado até 20/08/2026** — receita, atestado,
  prescrição —, nos DOIS serviços de PDF, desde a parcela 42. Nenhuma rede pegava e nenhum
  teste olhava: os testes de assinatura provavam que o arquivo **fecha**, nunca onde o
  carimbo **cai**. `CarimboNoRodapeTests` fixa o invariante (o retângulo cabe dentro da
  faixa do rodapé, medida do pé) e **inclui a prova de que a fórmula antiga falharia** —
  teste de posição que não reprova a posição errada não prova nada.
  Duas consequências que o Y errado escondia e que a correção destapou: o carimbo passa a
  cair **em cima da linha "Assinatura e carimbo"** (por isso `GerarPrescricaoAsync` ganhou
  `paraAssinaturaEletronica`, como o de documentos clínicos já tinha — folha que vai ser
  selada não desenha a linha de caneta), e o **2º carimbo cairia sobre o nome do
  prescritor** (o rodapé passou a reservar DOIS espaços quando a folha pede a assinatura
  da execução; cada carimbo estreita para 240, porque 250+10+250 dá 510 contra 510,24
  úteis e o QuestPDF recusa a linha sem folga — a largura sai de `AreaDaAssinatura` e do
  rodapé pelo MESMO par de constantes).
  A lição geral: **quando um valor atravessa a fronteira para uma biblioteca de terceiro,
  o sistema de coordenadas dela é premissa a MEDIR, não a deduzir do nome do campo.**

- **O carimbo do prescritor NUNCA apareceu — e eram DOIS defeitos somados, cada um
  bastando sozinho** (parcela 68, 9ª rodada — a clínica assinou as duas vias e disse que
  "as DUAS assinaturas digitais não saíram na folha conforme tem que ser"). Reproduzido
  antes de corrigir: o PDF gerado pelo teste foi RENDERIZADO, e a folha saía com **um
  carimbo só**, o da enfermagem.
  1. **A aparência do widget era recortada.** O PDFsharp entrega ao desenhador o retângulo
     **na PÁGINA** (o `/Rect`), e o `XGraphics` desenha dentro de um form XObject cuja
     **BBox é `[0 0 largura altura]`** — coordenada LOCAL. Desenhar em `area.X`/`area.Y`
     punha o traço inteiro fora da BBox, e o form **recorta pela BBox**: o carimbo
     simplesmente não existia no arquivo. Medido no objeto: `BBox [0 0 240 46]` com o
     conteúdo em `40 -640 240 46 re`.
  2. **Anotação sem o bit Print não é IMPRESSA.** O PDFsharp não escreve o `/F` do widget e
     o padrão é 0. Medido: o mesmo arquivo achatado para impressão sai sem o bloco. Mesmo
     com a coordenada certa, o carimbo apareceria no leitor de quem assinou e sumiria na
     folha que a enfermagem leva para a sala. E **não há onde ligar o bit**: o campo só
     existe durante o salvamento, DEPOIS de a aparência ter sido desenhada (sondado — no
     momento do desenho não há `/AcroForm`, não há anotação na página e não há objeto
     `/FT /Sig` no documento).
  A saída não foi remendar nenhum dos dois: foi tirar o bloco visível da ANOTAÇÃO e pô-lo
  no **CONTEÚDO DA PÁGINA**, que não tem nenhuma das duas armadilhas — aparece no leitor,
  sai na impressora e ainda é **coberto pela assinatura**, porque entra antes dela. O campo
  de assinatura fica **invisível** (`/Rect` de área zero): deixá-lo com o mesmo retângulo
  faria o leitor desenhar o carimbo duas vezes sobre si mesmo, e texto cinza de 6,5 pt
  desenhado duas vezes sai mais grosso.
  ⚠️ Só a **2ª** assinatura continua sendo anotação, e não é escolha: a página já está
  assinada e não se toca num byte dela. Lá o `/F 4` é escrito à mão — e por isso ela era a
  única que a clínica via.
  ⚠️ **Saiu assim da parcela 42 até 20/08/2026, em TODO documento assinado** — receita,
  atestado, pedido de exame, prescrição. A assinatura criptográfica estava perfeita por
  baixo o tempo todo; o que faltava era a folha dizer quem assinou. É a irmã mais discreta
  do defeito recorrente do projeto: não é dado sem leitor nem capacidade sem porta, é
  **desenho sem tela** — e nada falha, em lugar nenhum.
  As duas lições de método, e elas são as mesmas de sempre nesta área:
  **(a) teste que prova que o arquivo FECHA não prova que a folha MOSTRA.** Havia teste de
  integridade, de recorte do PKCS#7, de posição do retângulo (parcela 68, 8ª rodada) e de
  circuito ponta a ponta — e nenhum abria o conteúdo da página para procurar o carimbo.
  `CarimboVisivelTests` faz isso, e **três dos seus quatro testes falham no código de
  antes** (o quarto é o bit de imprimir, que já estava certo): carimbo visível que não
  reprova o carimbo invisível não prova nada.
  **(b) renderizar é de graça, e é o único jeito de ver o que só a folha montada mostra.**
  `poppler-utils` desenha o PDF para tela (`pdftoppm`) e para impressão (`pdftocairo -pdf`,
  que é o que revelou o bit Print), e o `pyhanko` confere as duas assinaturas sem custo
  nenhum. Cada tentativa no SafeID é COBRADA — conferir aqui, antes, é o que evita a
  próxima rodada paga. Os testes gravam os arquivos quando `CLINICA_DUMP_PDF` aponta uma
  pasta, inclusive a **folha REAL** (`SegundaAssinaturaExecucaoTests`), que é a única que
  mostra os dois carimbos no rodapé de verdade.
  De quebra, os dois carimbos passaram a ser desenhados **linha por linha iguais** — mesma
  ordem, mesmo tamanho, mesma cor e a moldura recuada meia caneta nos dois. Eles ficam LADO
  A LADO na folha, e qualquer diferença ali se lê como se um deles fosse de outro sistema;
  o travessão do título exigiu traduzir U+2014 para `0x97`, porque a fonte do carimbo
  incremental é **WinAnsiEncoding** e o arquivo é escrito em Latin-1 — as duas tabelas só
  divergem na faixa 0x80–0x9F, onde mora a tipografia (acento não precisa de nada: "é" é
  0xE9 nas duas).

- **Carimbo visível é dado do BANCO numa caixa de largura FIXA — logo, precisa de corte**
  (parcela 68, 9ª rodada, continuação). Assim que o bloco da assinatura passou a aparecer
  de verdade, ele virou o caso clássico da parcela 50: `DrawString` com `TopLeft` num
  `XRect` do PDFsharp **não quebra linha nem corta**, e o operador `Tj` do PDF também não —
  o que não cabe sai por cima do que estiver ao lado. As quatro linhas levam nome do
  profissional, registro, CPF e o **emissor do certificado**, e nenhum deles tem tamanho
  máximo.
  **Medido antes de decidir**, com o emissor ICP-Brasil mais longo que existe ("Autoridade
  Certificadora do SERPRO Final v5") e um nome composto: sobravam **28 pontos** no carimbo
  da prescrição, **20** no da enfermagem e **8,4 na RECEITA** — o papel que vai à farmácia,
  onde a caixa tem 220 e o QR fica logo ao lado. Oito pontos são dois caracteres. Não
  estourava, e não havia guarda nenhuma.
  ⚠️ **O corte é EXATO nos dois, e a razão de não ser o mesmo mecanismo importa**: o carimbo
  do prescritor é desenhado pelo PDFsharp, então mede com `MeasureString`; o da enfermagem
  é escrito à mão em **Helvetica**, e o resolvedor de fontes do projeto só conhece a Segoe
  WP, que é **~9% mais estreita** — medir com ela deixaria estourar justamente no caso
  apertado. Por isso entrou a tabela de larguras da base-14, que foi **medida** (215,319
  pontos para a linha do emissor a 6,5) e é **conferida em teste** contra o número que o
  poppler devolve no arquivo gerado: um dígito errado ali só apareceria como texto por cima
  do carimbo vizinho, na clínica.
  A lição de método é a que esta área cobra sempre: **quando um desenho deixa de ser
  recortado e passa a ser visível, tudo o que o recorte escondia vira defeito de uma vez.**
  E a de ferramenta: `pdftotext -bbox-layout` dá o limite real de cada glifo em pontos —
  dá para medir folga de caixa sem abrir o PDF, de graça, o que nesta área vale por uma
  tentativa paga no PSC.

- **Conteúdo de página HERDA o estado gráfico de quem desenhou antes — e quem equilibra a
  pilha do QuestPDF é o PDFsharp** (parcela 68, 9ª rodada, 3ª parte — achado de uma revisão
  adversarial do próprio diff, com o CI verde). Ao pôr o carimbo no conteúdo da página em
  vez de na aparência da anotação, ele passou a depender de uma coisa que a aparência não
  dependia: os fluxos de conteúdo de uma página são **CONCATENADOS**, e o `q` que o
  `XGraphics` emite **salva** o estado, não o reinicia.
  Medido no arquivo real: o fluxo do QuestPDF abre com `q .25 0 0 -.25 0 842 cm` (a escala
  do Skia) e **não fecha esse `q`**; quem fecha é um ` Q` que o **PDFsharp** acrescenta no
  fim. Ou seja, a posição do carimbo é o resultado de uma combinação entre o que uma
  biblioteca EMITE e o que a outra CONSERTA — as duas se atualizam sozinhas.
  Se essa combinação mudar, o carimbo sai com a escala herdada: **um oitavo do tamanho, no
  meio da folha, e criptograficamente válido**, com build, testes e as três redes verdes.
  ⚠️ A rede que faltava não é sobre texto, é sobre ESTADO:
  `A_pilha_grafica_chega_equilibrada_no_carimbo` percorre os fluxos anteriores ao do
  carimbo e cobra profundidade `q`/`Q` **zero** e nenhum `cm` na raiz. Os outros testes
  verificam que o carimbo está no fluxo, nunca ONDE ele cai, e o `CarimboNoRodapeTests`
  mede o retângulo **PEDIDO**, não o desenhado — três testes sobre o mesmo assunto e um
  buraco no meio deles.
  A lição: **ao trocar um desenho isolado (form XObject) por um desenho no fluxo comum,
  o que você ganhou em visibilidade você pagou em ACOPLAMENTO** — e o preço só aparece
  quando a outra biblioteca muda de versão.
- **Resolvedor de fonte que descarta o `bold` faz o título nascer sem hierarquia** (mesma
  rodada): `FonteDoCarimbo.ResolveTypeface` devolvia sempre `SegoeWP#`, ignorando o
  parâmetro, e o `PdfSharp.WPFonts` traz `SegoeWPBold` ao lado. As quatro linhas do carimbo
  saíam com o mesmo peso. Enquanto o bloco era invisível ninguém via; agora ele é a única
  linha de cabeçalho, no papel que a clínica entrega. Conferido no arquivo: antes, um
  recurso de fonte só (`/F0` nas duas medidas); agora `/F0: Segoe WP,Bold` e `/F1: Segoe
  WP`, e há teste cobrando que título e corpo resolvam para faces **diferentes**.

- **`/Size` do trailer calculado À MÃO desencontra na primeira linha nova — e quem recusa é
  o leitor ESTRITO, não o nosso** (parcela 68, 9ª rodada, 4ª parte). Para igualar os dois
  carimbos, o incremental ganhou a face **Helvetica-Bold** do título — um objeto PDF a
  mais. O `/Size` era escrito como `nSig + 4`, certo enquanto o último objeto novo fosse a
  fonte: a tabela xref passou a declarar um objeto que o trailer dizia não existir.
  ⚠️ **Os 1675 testes ficaram verdes, o nosso `Conferir` aprovou e o arquivo abriu**, porque
  o leitor da casa é tolerante. Quem recusou o arquivo INTEIRO foi o **pyhanko**: *"Xref
  table size mismatch: table allocated object with id 47, but according to the trailer 46 is
  the maximal allowed object id"*. Em produção, quem recusaria seria o validador do
  farmacêutico.
  A correção não foi ajustar a conta: foi **derivá-la dos objetos realmente escritos**
  (`objetos.Max(Numero) + 1`), para não poder desencontrar de novo. E entrou
  `O_trailer_declara_um_tamanho_maior_que_o_maior_objeto`, que é a régua estrita dentro de
  casa — ele reprova com a conta errada.
  É a 7ª rodada da parcela 67 pela terceira vez, agora do lado da ESTRUTURA em vez do
  formato do CMS: **aceitar o próprio arquivo não é o mesmo que EMITIR o arquivo que o
  mundo lá fora lê.** E a lição operacional: **validador de fora roda de graça — passe TODO
  arquivo assinado por ele antes de dar a rodada por fechada**, porque nesta área a
  alternativa custa uma tentativa paga no PSC.

- **A folha selada pela MÉDICA não pode mostrar a execução — e a que mostra tem de ser
  selada também** (parcela 68, 10ª rodada — a clínica relatou que, com item não realizado ou
  suspenso, "está encerrando a infusão sem assinar a dela"). O relato apontava para o ciclo
  de vida e o defeito estava no PAPEL.
  **O que NÃO era**: `AguardaAssinaturaDaExecucao` sempre esteve certo. Cinco casos medidos
  (tudo realizado, um não realizado, um suspenso, nada realizado, tudo suspenso) e em todos
  a folha encerrada continua pedindo a assinatura — `NaoRealizadoESuspensoTests` fixa isso.
  Os testes anteriores só exercitavam a folha TODA realizada, e **é sempre o caminho não
  exercitado que a clínica encontra**.
  ⚠️ **O que era**: o REGISTRO DE EXECUÇÃO — a única folha que mostra o ✓, a rodela, o
  suspenso e as justificativas — é montado NA HORA e **nunca foi assinado**. Pior: depois de
  a enfermeira assinar a prescrição, o rodapé dele passava a escrever *"Assinado
  digitalmente com certificado ICP-Brasil (assinatura qualificada) · titular Joana
  Técnica"* — num PDF sem `/ByteRange`, sem PKCS#7 e sem carimbo. E, ao afirmar isso, ele
  **engolia a linha de caneta**: a folha saía sem carimbo digital e sem onde assinar à mão,
  isto é, **sem prova nenhuma de autoria**. Da cadeira dela, isso é exatamente "encerrou sem
  a minha assinatura". É a garantia aparente da parcela 3, e um comentário do próprio código
  a escondia — ele supunha que "na reimpressão nunca se chega aqui, os bytes selados são
  devolvidos guardados", e o registro **nunca foi guardado**.
  ⚠️ **A via que parecia óbvia foi MEDIDA e está fechada**: acrescentar a página do registro
  ao arquivo que a médica assinou, por revisão incremental, faz o pyhanko devolver
  `mod=OTHER · ILLEGAL_MODIFICATIONS` para a assinatura **dela**. Nosso lado aprovaria; o
  Adobe e o ITI acusariam. É a lição da 7ª rodada da parcela 67 pelo avesso, e ela custou
  trinta linhas de experimento em vez de um importador de PDF inteiro.
  **O desenho que ficou** (escolha da direção, com o custo na mesa): a enfermeira escolhe o
  certificado UMA vez e o sistema sela **DOIS documentos** — a prescrição (revisão
  incremental, o carimbo dela ao lado do da médica) e o registro (assinatura própria, um
  carimbo, mostrando tudo). São duas cobranças do SafeID por folha, e a direção aceitou.
  Três regras que o código não conta sozinho: **o registro é GERADO antes de a assinatura
  ser registrada** (depois dela, o rodapé escreveria "este arquivo NÃO é assinado", e selar
  esse texto produziria um arquivo assinado dizendo que não é assinado); **falhar ao selar o
  registro NÃO desfaz a assinatura da prescrição** (a hierarquia da parcela 65: o ato
  irreversível não depende do passo que veio depois), e a degradação é honesta — sem selo, o
  registro volta a ser espelho e DIZ que não é assinado, apontando a folha que é; e **cada
  folha devolve o SEU arquivo selado** (`ArquivoId` × `ArquivoRegistroId`), porque trocar um
  pelo outro entregaria a segunda via de um documento no lugar do outro.
  ⚠️ **E a causa do relato era ainda mais simples: a caixinha não estava marcada.** Os
  prints de produção fecham o diagnóstico — o rodapé do registro da folha 0009 traz a LINHA
  DE CANETA e o nome à direita, leiaute que só sai quando `ExigeAssinaturaEletronicaDaExecucao`
  é **falso**. A folha 0007, com dois carimbos, tinha a caixinha marcada. A clínica leu a
  coincidência ("as duas que ficaram sem assinatura tinham item não realizado") como causa,
  e ela não é — mas **o que ela via é o que vale**. Por isso o campo passou a **nascer
  marcado**: garantia que depende de alguém lembrar não é garantia. Desmarcar continua
  existindo, e continua sendo de quem prescreve; o que mudou é o lado para o qual o
  esquecimento cai.
  ⚠️ Teste que depende do PADRÃO passa a medir a escolha errada quando o padrão vira — o da
  trilha do encerramento quebrou junto, e a correção foi **declarar o regime no próprio
  teste** em vez de ajustar a asserção.
  A lição de teste: **a decisão do rodapé foi tirada de dentro do desenho do PDF**
  (`AvisoDoRegistroDeExecucao`, público). Lá dentro nenhum teste a alcançava — o QuestPDF
  embute a fonte em subconjunto e escreve o texto como IDs de glifo, então nem o texto do
  arquivo se lê de volta. **O que decide o que o papel AFIRMA precisa morar onde o
  `dotnet test` alcança.**

- **O ESCOPO da autorização em nuvem é escolhido pelo ATO, e errá-lo só aparece na SEGUNDA
  assinatura** (parcela 68, 11ª rodada — achado na revisão da própria mudança, antes de
  chegar à clínica). O encerramento passou a selar DOIS documentos com o mesmo certificado,
  e o seletor autorizava com o padrão do serviço: `EscopoSafeID.AssinaturaUnica`. O próprio
  arquivo já dizia o que isso significa — *"um hash só; **o token morre no uso**"*.
  Ou seja: a primeira selagem passaria e a **segunda seria recusada SEMPRE**, em toda folha
  da clínica, e a tela cairia no caminho degradado todo dia.
  ⚠️ **Nenhum teste pegaria**, e a razão é estrutural: o assinador de teste é local e não
  tem token que morra. Em produção, cada tentativa é COBRADA — então o defeito seria
  descoberto pagando. Foi achado LENDO os escopos, não medindo, e é o tipo de coisa que só
  se acha perguntando "o que muda quando eu chamo isto DUAS vezes?".
  A correção mora em `EscopoSafeID.ParaAto(assinaturas)` — na APPLICATION, não na ViewModel
  do WPF que escolhe o certificado, pela razão de sempre: **decisão dentro de ViewModel é
  decisão que o `dotnet test` não alcança**. E a sessão é pedida **encurtada** (5 min): o
  padrão do PSC para pessoa física vai a **sete dias**, e autorizar uma semana para selar
  duas folhas abre muito mais do que o ato pede — daí `AutorizarAsync` ter ganhado
  `duracaoSegundos`, que só existe para ENCURTAR.
  A lição que generaliza: **ao passar a chamar um serviço externo duas vezes onde antes era
  uma, leia o que a AUTORIZAÇÃO permite — não só o que a chamada faz.** Escopo, cota e
  idempotência são propriedades do primeiro uso, e nenhuma delas aparece no teste local.

- **A EVOLUÇÃO DE ENFERMAGEM, e por que ela não é a `Evolucao` com outro autor** (parcela
  71 — a cliente: *"todo paciente precisa passar pela enfermagem, por isso precisamos que
  também tenha um registro de evolução para enfermagem"*). São dois registros de naturezas
  e responsabilidades diferentes: a evolução responde *"o que eu concluí e o que decidi
  fazer"* e é de quem ATENDEU; esta responde *"o que eu observei no paciente, e a que
  horas"* e é de quem EXECUTOU, com o registro no conselho ao lado. A evolução é UMA por
  sessão, salva em várias passadas até ficar certa; esta são VÁRIAS por passagem — 14h20,
  14h50, 15h10 —, cada uma um fato pontual que não se reescreve.
  ⚠️ **Reusar a `Evolucao` com um campo de tipo foi medido e recusado**, e o argumento não
  é conceitual: QUATRO leitores quebrariam **em silêncio**. `ConsultorioService.
  EvolucaoDoHorario` cai para paciente + data, então a anotação da técnica cobriria a
  sessão do médico e ela sumiria de "sessões sem evolução" — elo partido não vira erro,
  vira LISTA VAZIA; `EvolucoesNoPeriodoAsync` filtra `ProfissionalId == null`, e a anotação
  apareceria no "Meu dia" de TODOS; `CompletudeProntuario` passaria a medir o trabalho de
  outra pessoa; e o relatório que o paciente leva ao CONVÊNIO imprime o autor por sessão.
  É o mesmo argumento com que a parcela 42 recusou enfiar a folha de infusão em
  `TipoDocumentoClinico`.
  **O paciente é o DONO; a infusão é procedência.** `PrescricaoInternaId` é anulável, e não
  por conveniência: FK obrigatória força `Cascade`, e apagar uma folha levaria junto
  registro clínico — a cascata que a parcela 60 achou no excluir paciente. Sem isso ficariam
  sem lugar o curativo, a sala de observação, a triagem e **a reação que aparece meia hora
  depois de a folha ter sido encerrada**, que é justamente a que mais importa.
  **Os SINAIS VITAIS moram na evolução, e não em `MedidaClinica`**: aquela não tem HORA (só
  `DateOnly`), então três aferições na mesma infusão ficariam indistinguíveis — e é a
  sequência dentro da sessão que faz a leitura de enfermagem ("PA 120x80 na admissão, 90x60
  aos vinte minutos"). Faltam-lhe também temperatura, FC, FR e SpO₂. A divisão fica escrita
  na tela: **aqui é o ponto no tempo; a CURVA continua sendo a tela de Medidas.** E a dor
  aferida **não entra na curva de dor do prontuário** (`Evolucao.EvaAntes/Depois`): aquela
  mede o efeito do TRATAMENTO entre sessões e a direção já a lê — misturá-las mudaria em
  silêncio um número existente.
  **A porta é a LINHA DA FILA da sala, não uma aba dentro da folha**, e a primeira razão
  sozinha decide: `PodeMexer => PodeChecar && EmExecucao` — **folha encerrada apaga a janela
  dela inteira**, e o botão nasceria morto exatamente no caso que justifica a feature. Some
  a isso que a folha é MODAL (registrar uma reação na cadeira 3 com a folha da cadeira 1
  aberta custaria dez gestos, com o paciente reagindo) e que ela já tem um campo de hora com
  OUTRO significado, num registro cuja razão de existir é a hora ser a certa.
  **Bit próprio** (`RegistrarEvolucaoEnfermagem`), e a alternativa de custo zero foi
  recusada de propósito: reusar `ChecarPrescricao` não custaria uma linha, mas **checar é
  afirmar que aquilo entrou no paciente; evoluir é descrever o que se observou nele** — dois
  atos de peso diferente, e o corte pelo ATO é a regra desde a parcela 45.
  **No prontuário, lista SEPARADA da médica — e não é estética, é bug evitado:**
  `LinhaEvolucao.EvolucaoId` é a chave das ações destrutivas da lista de sessões, e ids são
  POR TABELA. A evolução de enfermagem nº 42 na mesma lista cancelaria a `Evolucao` nº 42 —
  de outro paciente, com o motivo escrito para outra coisa. Não estoura, não avisa.
  ⚠️ **E a aba "Prontuário" da ficha não tinha barreira NENHUMA.** O item Pacientes pede só
  `VerFichaPaciente`, o `TabItem` não tinha `Visibility` e `CarregarProntuarioAsync` não
  tinha um único `Pode` — **Recepção, Financeiro e Faturista liam a evolução inteira de
  qualquer paciente**, que é literalmente o que o corte da parcela 49 existe para impedir. A
  contraprova estava 270 linhas abaixo no MESMO arquivo: `CarregarTermosAsync` tem a
  barreira e o comentário nomeando esses perfis. Achado ao construir esta parcela, e
  corrigido antes dela — **entregar dado clínico novo por uma porta aberta pioraria a
  conformidade que a feature existe para melhorar.**
  ⚠️ **E as duas primeiras portas não bastavam — foi preciso a TELA.** A evolução nasceu
  alcançável pela fila da sala de infusão e pela ficha do paciente, e as duas resolvem o
  caso da INFUSÃO. A clínica devolveu a frase que muda o desenho: *"todo paciente precisa
  passar pela enfermagem"*. A maioria dessas passagens não tem folha nenhuma — curativo,
  triagem, observação, pós-consulta —, e a enfermeira não tinha de onde alcançá-las: a sala
  só mostra as folhas do DIA, e a ficha exige saber o nome e passar pelo módulo da recepção.
  A tela `Enfermagem` é a **terceira tela do shell publicada por dois módulos** (a sala e os
  pacotes são as outras), e é SEPARADA da sala de propósito: a sala responde *"o que
  executar agora"*, esta responde *"quem eu atendi e o que escrevi"*. Terceira pergunta,
  terceira tela. A lista traz **todos os pacientes cadastrados** (`limite: null`, como a
  listagem do balcão) e a evolução mora atrás de UM clique — lista de largura inteira → tela
  do paciente, nunca a faixa lateral que o README proíbe.
  A lição: **porta no fluxo não é porta na rotina.** Pendurar o registro nas telas do
  processo que o motivou (a infusão) cobre quem está DENTRO daquele processo; quem faz o
  mesmo trabalho fora dele fica sem lugar — e é o defeito recorrente do projeto na variante
  "a porta existe, mas só para metade dos casos".
  De quebra: `SessaoUsuario.RegistroConselho` nasceu porque a folha gravava `Conselho: null`
  **literal** desde a parcela 42 — a coluna existia, o PDF tinha o ramo que a imprime e a
  exportação tinha a coluna, e nenhuma checagem de produção saía identificada. **Registro de
  enfermagem sem o número do conselho não é registro de enfermagem.**

- **X · Y · XY — a LEITURA que os dois compartilham, a ESCRITA que os separa** (parcela 72;
  o mapa completo está em `docs/atendimento-medico-e-enfermagem.md`, e é lá que se
  atualiza). A cliente pediu: *"o médico enxerga X itens de atendimento, a enfermagem
  enxerga Y, e caso X e Y se completem entreguem XY para ambos"*. A medição do domínio
  respondeu antes do desenho: `PerfilAcesso.Profissional` e `PerfilAcesso.Enfermagem` **já
  compartilhavam** `VerAgenda | VerFichaPaciente | VerProntuario | ColherAssinaturaPaciente`.
  ⚠️ **Nenhuma permissão nova era necessária para entregar o XY — faltava PORTA**, o defeito
  recorrente do projeto pela décima segunda vez. E havia um argumento duro contra inventar
  bit: `Permissao` é `[Flags]` de **`int`** gravada como INTEIRO em produção, e
  `RegistrarEvolucaoEnfermagem = 1 << 30` — **sobra UM bit** antes de o enum precisar virar
  `long`, o que muda o tipo da coluna numa base viva.
  **A frase que decide tudo: XY é a LEITURA; X e Y são as ESCRITAS.** O que separa é
  `EditarProntuario | Prescrever` (X) contra `ChecarPrescricao | RegistrarEvolucaoEnfermagem`
  (Y), e cada lado escreve com o conselho dele — médico escrevendo evolução de enfermagem é
  registro assinado com o conselho errado, pior que a lacuna que resolveria.
  **A linha do tempo clínica** (`LinhaDoTempoClinicaView`, no shell) é o componente das TRÊS
  portas: a ficha da Recepção, o Consultório e a tela da Enfermagem. Chips de seção
  **contados**, um marcado por vez — e os desmarcados MOSTRAM a contagem, porque é o número
  visível que faz a enfermeira descobrir que há 12 sessões médicas para ler.
  ⚠️ **Ela NÃO funde as listas, e as duas razões decidem sozinhas:** (a) os ids são POR
  TABELA — a evolução de enfermagem nº 42 e a `Evolucao` nº 42 são de pacientes diferentes,
  e um comando destrutivo sobre lista fundida cancelaria o registro errado **sem estourar e
  sem avisar** (o bug que `PacientesView.xaml` documentava desde a 71); (b) `Evolucao.Data`
  é `DateOnly` — ordenar a médica às 00:00 a poria antes de todas as aferições do dia,
  inclusive da reação que a motivou, e **ordenar um prontuário por uma hora que não existe é
  fabricar sequência de eventos num documento que responde em auditoria**. O item carrega
  `Natureza` + `Id`, e é isso que permitirá fundir quando `Evolucao` ganhar hora.
  ⚠️ **A lista rica de sessões do Consultório NÃO foi substituída** pela genérica: ela tem
  busca no texto, contagem de anexos, marca de correção e os botões da linha. Trocá-la
  tiraria capacidade de quem a usa todo dia — a regra 3 do bloco do faturamento aplicada a
  uma tela clínica. O componente entra ali só para o que faltava.
  **Antes de acrescentar informação ao Atendimento foi preciso corrigir o leiaute**: a tela
  exigia 520 + 16 + 400 = 936 px e a janela mínima da suíte deixa 856 (606 com o painel de
  categoria fixado). O WPF honra o `MinWidth` e **corta o excesso à direita** — não há
  rolagem horizontal ali, então acrescentar era cortar o que já existe.

- **Entidade clínica nova entra em UMA lista, e quatro leitores a consomem**
  (`CatalogoRegistroClinico`, parcela 72). O defeito já tinha sido cometido DUAS vezes neste
  exato assunto, e os comentários confessam: a folha de infusão ficou de fora da primeira
  versão da GUARDA e a lista de problemas — onde moram as ALERGIAS — ficou de fora da
  primeira versão da EXPORTAÇÃO. Uma terceira estava de pé: `SituacaoGuarda` contava CINCO
  naturezas enquanto o prazo de 20 anos era calculado sobre SETE, e o documento do art. 18,
  II cobria TRÊS de nove. A tela que responde ao auditor *"o que vocês guardam por 20 anos"*
  mostrava "0 sessão · 0 medida · 0 prescrição" com uma data vinda de lugar nenhum visível,
  para a ficha cujo único registro era enfermagem — **número errado com cara de exato, na
  tela de conformidade**.
  As nove naturezas moram no DOMÍNIO com rótulo e permissão de leitura; leem a lista a linha
  do tempo, a guarda (contagem e frase), a exportação do fornecedor e o direito do titular —
  que ganhou as SEIS seções que faltavam, execução de infusão item a item inclusive.
  ⚠️ **`ConjuntoClinicoTests` é COMPORTAMENTAL, não declarativo**: ele grava um registro de
  cada natureza e exige que ele APAREÇA na contagem, no CSV e no texto. Declaração conferida
  contra declaração ficaria verde com as duas erradas do mesmo jeito. Ele falha no commit em
  que a próxima entidade clínica nascer, que é meses antes de a clínica esbarrar.

- **A alergia é conferida na ADMINISTRAÇÃO, não só na assinatura** (parcela 72 — a quinta
  recusa do projeto, e a única que impede dano ao PACIENTE). `AssinarAsync` confere a folha
  contra as alergias e recusa sem confirmação escrita desde a parcela 42; a EXECUÇÃO não
  conferia **nada** — as únicas guardas de `ChecarAsync` eram "item já checado" e hora
  futura. O caminho de dano é inteiro e mora dentro do que a parcela 71 construiu: folha
  assinada de manhã sem alergia registrada → o item 2 causa reação → **a própria técnica**
  grava a alergia pelo campo "Reação a registrar como alergia" → itens 3, 4 e 5 seguem
  pendentes com a folha na sala → **ninguém reconfere**. O sistema tinha o dado, gravado por
  quem seria a vítima do silêncio, e não o usava.
  Três decisões: **só na administração** (não administrar é o desfecho seguro, e cobrar
  confirmação para a rodela treinaria a equipe a confirmar sem ler); **confere o ITEM, não a
  folha** (repetir a resposta da folha inteira acenderia na linha do soro por causa da
  dipirona da linha de baixo); e **avisa e exige confirmação — não impede**. Vale igual na
  RETIFICAÇÃO: retificar de "não realizado" para "realizado" é administrar, e deixar a
  conferência só na checagem normal seria a cópia que fica para trás.
  Junto veio o **COREN obrigatório** em checar, registrar e retificar. O comentário da
  entidade já dizia que "evolução sem registro no conselho não é evolução de enfermagem" e
  **não havia guarda em lugar nenhum**: bastava um login sem `Profissional` vinculado para
  todo registro daquela máquina entrar no prontuário sem COREN **para sempre** — e sem
  conserto barato, porque o campo é COPIADO no ato. A frase nomeia o conserto, pelo
  precedente do "Meu dia".

- **A curva de PRESSÃO vem de DUAS fontes, e cada ponto diz de onde veio** (parcela 72): a
  PA está no `CatalogoMedidas` com faixas publicadas desde a parcela 37 e a série **nascia
  vazia e continuava vazia** — a única porta de ESCRITA de `MedidaClinica` está no app do
  MÉDICO, enquanto a pressão de verdade é aferida na ENFERMAGEM, toda sessão, e vai para
  `EvolucaoEnfermagem`. **Curva vazia se lê como "este paciente nunca teve a pressão
  aferida"**, que é falso.
  A ponte é de **LEITURA**, e isso é decisão: destravar a colheita de `MedidaClinica` pela
  enfermagem daria DOIS lugares para gravar a mesma aferição, sem nada na tela dizendo qual.
  As regras: **procedência escrita em cada ponto** (dois pontos do mesmo dia sem dizer que um
  é de antes da consulta e o outro de meia hora depois da bomba escondem justamente a leitura
  clínica), **faixa da MESMA definição do catálogo** (a evolução grava os números crus, e uma
  segunda leitura de "pressão normal" daria duas respostas para a mesma aferição), **cancelada
  e retificada não entram** e **dentro do dia a HORA desempata**.

- **Comentário que promete o que o código não faz, terceira ocorrência** (parcela 72): a
  entidade `EvolucaoEnfermagem` afirmava que a marca de intercorrência *"viaja para a tela de
  atendimento do médico"* — e `grep EvolucaoEnfermagemService src/Clinica.Modulo.Clinico`
  devolvia **zero**. Aqui o estrago não era erro, era **AUSÊNCIA**, indistinguível de "não
  houve intercorrência". Agora ela entra na lista de `AlertasClinicos` que já existe (zero
  pixel novo), com **janela de 48 horas** — e a janela decide a utilidade da lista: alergia é
  ESTADO, intercorrência é EVENTO DATADO, e a marca é `bool`, então **não há como
  descartá-la**. Sem janela, seis meses depois o paciente crônico teria uma náusea de março e
  um extravasamento de abril acima da alergia real, e é assim que se ensina alguém a fechar o
  alerta sem ler. A janela é `const` no domínio, ao lado da regra.
  O mesmo commit corrigiu a promessa das *"quatro portas — … e o do Consultório"* da janela de
  escrita (a do Consultório não existe, e é decisão) e a frase divergente do cabeçalho do
  workspace, que dizia "da agenda de HOJE" para qualquer horário enquanto a tela de dentro já
  dizia a DATA desde a parcela 69 — a lição das parcelas 64 e 68 pela sétima vez.

- **Propriedade `init` NÃO existe ainda dentro do construtor** (parcela 72, revisão do
  próprio diff — o defeito mais caro da rodada, e o mais silencioso). O componente da linha
  do tempo fazia `Secao = SecaoInicial` no corpo do construtor, e `SecaoInicial` é `init`:
  o *object initializer* roda **depois** do construtor, então a leitura devolve sempre o
  DEFAULT. A tela da **Enfermagem** — cuja razão de existir é a evolução de enfermagem —
  abria marcada em "Médica", listando as sessões do médico; e o comentário do XAML afirmava
  por escrito que ela "abre no chip Enfermagem".
  ⚠️ O que fez isso passar foi um **socorro acidental**: a rotina que monta os chips troca a
  seção quando a atual não está visível, e as duas portas que restringem a lista de seções
  funcionavam por tabela. Só a porta que mostra TUDO ficava errada — a que mais importava.
  **Valor que depende de `init` resolve-se na primeira carga, nunca no construtor.**

- **`TextoParaVisibilidade` sobre qualquer coisa que não seja `string` é `Collapsed` para
  sempre** (parcela 72): o conversor faz `value as string`, e um `TimeOnly?` encaixotado
  devolve `null`. A HORA da evolução de enfermagem — o dado que a parcela 71 existe para
  registrar, e cuja leitura clínica é a sequência dentro da sessão (14h20 · 14h50 · 15h10) —
  **nunca apareceu**, nas três portas. É o defeito do `BooleanToVisibilityConverter` sobre
  string (parcela 61) pelo AVESSO, e nada falha: XAML bem-formado, binding válido, nenhuma
  exceção. **Amarre `Text` e `Visibility` na MESMA propriedade, e que ela seja string** —
  foi por isso que `RegistroClinicoPaciente.HoraTexto` nasceu.

- **Ao substituir uma lista por um componente genérico, liste o que a antiga MOSTRAVA**
  (parcela 72). A troca perdeu quatro coisas sem que nada quebrasse: o **selo
  INTERCORRÊNCIA** (que virou texto suave dentro do detalhe, com o mesmo peso do resumo da
  queixa — e aceso também no registro CANCELADO, que é registro desdito), o **realce do não
  vigente**, o **`RegistradoEm`** e — o pior — o **autor da sessão**, que passou de
  `Profissional?.Rotulo` (quem ATENDEU) para `CriadoPor` (quem DIGITOU, nulo em toda sessão
  anterior ao dia em que o sistema passou a gravá-lo). **A linha continuou existindo e
  passou a responder outra pergunta**, que é o jeito mais discreto de perder informação.

- **Portão de acesso por NATUREZA tem de ser o PISO quando a natureza tem regra por ITEM**
  (parcela 72): o catálogo declarava `VerProntuario` para "documento clínico", e o montador
  aplicava esse portão ANTES do filtro por folha. Isso engolia as duas folhas que a parcela
  59 decidiu não serem dado de saúde — declaração de comparecimento e termo de consentimento
  LGPD, ambas `VerFichaPaciente`, que saem do balcão o dia inteiro. **Onde a natureza tem
  regra por item, quem decide é o item; o portão de fora é só "pode abrir este paciente".**

- **Comando novo que faz DADO DE SAÚDE SAIR nasce com as duas barreiras** (parcela 72): o
  "Enviar documento pelo WhatsApp" foi portado para a ficha **sem um único `Exigir`**, e a
  metade visível era estado puro (`Assinado && !Cancelado`). A enfermeira, que tem
  `VerProntuario` e não tem `Prescrever`, via a receita na lista e o botão ACESO. E o
  "Assinar" ao lado tinha as duas barreiras **discordando sobre que ato é aquele**: o botão
  acendia pelo bit do TIPO do papel e o comando exigia `Prescrever` fixo — o corredor sem
  saída da parcela 69 em quatro dos oito tipos, com a fuga oposta de brinde (quem tem
  `Prescrever` e não tem o bit do tipo passava direto). **Confira o bit do TIPO que está na
  linha, não o da porta por onde se entrou** — e procure a mesma ação nos DOIS lados: a
  cópia da Recepção era a atrasada, e foi dela que o defeito veio.

- **Recusa nova de serviço precisa da metade VISÍVEL, senão ela chega depois do trabalho
  feito** (parcela 72): o COREN passou a ser obrigatório para checar — regra certa, porque o
  número é COPIADO no ato e corrigir depois exigiria retificar registro a registro. Mas os
  botões continuavam acesos: a técnica sem ficha de profissional vinculada administrava o
  soro, vinha marcar e levava um erro que **ela não pode consertar sozinha** (o conserto é em
  Equipe e em Acessos, bits que o perfil dela não tem). Agora a folha avisa na ABERTURA e o
  botão nasce apagado. **Toda recusa de serviço tem um botão que precisa saber dela.**

- **Resultado vazio de workflow é para ser investigado, nunca lido como aprovação** (parcela
  72 — a lição da parcela 66 cobrada de novo, agora na minha própria ferramenta). A
  auditoria adversarial devolveu `sobreviventes: []` porque **26 dos 28 agentes falharam no
  limite semanal** — os achados foram para a lista de "derrubados" com a razão `"sem voto"`.
  A conta era minha: `sobreviveu = votos.Length > 0 && refutados == 0` põe o achado
  SEM VOTO no balde dos refutados. **Cético que não votou não refutou nada** — e o script
  que trata as duas coisas igual transforma falha de infraestrutura em aprovação silenciosa.

- **"Está muito CRU!" — a parcela 72 entregou QUEM VÊ e não encostou no ATO DE ATENDER**
  (parcela 73; o mapa completo está em `docs/registro-do-atendimento.md`, e é lá que se
  atualiza). Foi a oitava reprovação do cliente, e a mais precisa: a parcela anterior deu
  portas, linha do tempo e permissões, e os dois registros clínicos continuavam sendo **um
  campo de texto**. O médico não tinha onde escrever história da doença atual, exame físico
  nem hipótese diagnóstica — os três eixos sem os quais o prontuário registra o que foi feito
  e não registra **por quê**.
  ⚠️ **E a metade da enfermagem não era pobreza, era ILEGALIDADE.** A **COFEN 358/2009** torna
  o Processo de Enfermagem obrigatório e **registrado formalmente** em cinco etapas
  (histórico, diagnóstico, planejamento, prescrição de enfermagem, avaliação), e a **Lei
  7.498/1986, art. 11, I, "i"** faz da consulta de enfermagem ato **privativo do Enfermeiro**.
  Oferecer uma caixa de texto à enfermagem não é oferecer pouco: é **impedir que ela cumpra a
  Resolução do próprio conselho**.
  **ANOTAÇÃO × CONSULTA é derivado do CONTEÚDO, nunca um campo de tipo** (`EhConsulta`):
  campo de tipo teria de ser preenchido, e seria preenchido errado — a técnica marcaria
  "consulta" por engano e a folha sairia cobrando cinco etapas de uma observação de sinais
  vitais. Derivado, ele não mente: quem escreveu diagnóstico fez consulta, quem escreveu uma
  linha fez anotação. E a **consulta incompleta AVISA e não impede** (`EtapasEmFalta`): a
  etapa 5 só existe depois de o cuidado ter sido prestado, e recusar salvar antes dela faria
  a enfermeira digitar tudo de novo no fim do turno — **registro clínico que não se consegue
  salvar é registro que não acontece**. A recusa seria a garantia aparente pelo avesso; o que
  se recusa é imprimir a consulta pela metade **sem dizer que está pela metade**, e por isso
  o aviso sai na tela E no papel.
  **O catálogo é a lista DESTA clínica, e NANDA-I não foi importada** — é licenciada. O que
  se reusa é a redação em três partes (problema, *relacionado a*, *evidenciado por*), que é
  método e não texto protegido; sem a terceira parte ninguém consegue avaliar depois se o
  diagnóstico foi resolvido, e a etapa 5 vira opinião. O `Codigo` é **anulável de propósito**:
  lista fechada faria a enfermeira parar de diagnosticar o que não está nela, e diagnóstico
  não escrito é cuidado não prestado. Aplicar **COPIA** — aqui não é desenho, é a Lei
  13.787/2018.
  ⚠️ **O item que quase ficou de fora é o PAPEL.** A fiscalização do COREN se faz no
  PRONTUÁRIO, que numa clínica é a via impressa e arquivada: **consulta de enfermagem que só
  existe na tela é consulta que a fiscalização não enxerga** — o defeito recorrente do
  projeto na variante que custa multa. As cinco etapas entraram em
  `PrescricaoInternaPdfService`, com o que falta escrito ao pé.
  **Só o registro que TEM processo ganha o bloco**: carimbar as cinco etapas em toda
  observação encheria a folha de rótulos vazios, e rótulo vazio é o que faz alguém parar de
  ler a folha inteira. Pela mesma razão o detalhe do médico abre num `Expander` FECHADO: a
  sessão de seguimento — a maioria absoluta do movimento — se registra em queixa, EVA e
  conduta, e quatro campos vazios em toda sessão viram paisagem antes do dia em que a
  anamnese importa.
  ⚠️ **O CID da hipótese NÃO sai no relatório de evolução** — a economia do CID da parcela 3,
  com mais razão: o relatório circula fora da clínica, o código é o que se lê num campo de
  formulário sem ninguém ler a frase ao lado, e este documento não passa pela autorização
  expressa que a receita e o atestado pedem. E a **hipótese não é a lista de problemas**:
  `ProblemaPaciente` é o que o paciente TEM, `HipoteseDiagnostica` é o que se pensou NAQUELA
  sessão — a de terça pode estar errada, e a lista não deve carregá-la.

- **"Estamos anos luz atrás" — o atendimento não era um ESTADO, era um formulário**
  (parcela 74; o mapa completo está em `docs/registro-do-atendimento.md`, §8 em diante).
  A cliente mandou o print do prontuário do iClinic. Medidas as duas telas lado a lado, os
  dois buracos que importam não eram cosméticos, e o primeiro é conceitual: **num prontuário
  eletrônico o profissional ENTRA no atendimento, o relógio corre e ele FINALIZA.** O nosso
  era um formulário com "Salvar sessão" no rodapé.
  ⚠️ **O carimbo do INÍCIO existe desde a parcela 38** (`Agendamento.InicioAtendimentoEm`,
  para o kanban do balcão) **e nenhuma tela do Consultório o lia.** O defeito recorrente do
  projeto na variante mais discreta de todas — nada falha, só que o médico não sabe que está
  há 40 minutos com o mesmo paciente e o balcão não sabe que ele terminou.
  **Finalizar NÃO conclui, e é a decisão da parcela 61**: concluir são QUATRO fatos do mesmo
  ato (a guia nasce, o pacote debita, o insumo sai, o dinheiro entra) e três são do balcão.
  Marcar `Realizado` daqui pularia os três **em silêncio**: o dia fecharia com o caixa sem a
  sessão, e nada falharia. Por isso `EncerrarAtendimentoAsync` carimba `FimAtendimentoEm` e
  deixa o `Status` em `Agendado`, e há teste que falha se alguém "simplificar" isso.
  ⚠️ **A ORDEM entre gravar e carimbar é a hierarquia da parcela 65, não estilo**: grava a
  sessão PRIMEIRO. Falhando a gravação, o carimbo não acontece — mandar o recado de que o
  médico terminou enquanto a evolução não existe em lugar nenhum é falha exibida como
  sucesso. O inverso também vale: gravada a sessão, falhar o carimbo vira AVISO e nunca
  desfaz o prontuário. É o que fez `SalvarAsync` virar `TentarSalvarAsync` devolvendo `bool`.
  **E o recado CHEGA**: o cartão da fila do balcão ganha o selo "Encerrado às 14h32" e sobe
  para a frente da raia. É o par do `ChamadoEm`, que atravessa no sentido contrário — até
  aqui a recepcionista descobria que o médico tinha terminado quando o paciente aparecia na
  frente dela, e o cartão ficava em "Em atendimento" meia hora depois de a sala estar vazia,
  o que faz o quadro do dia mentir sobre quem está ocupado. ⚠️ **É SELO e não raia nova**:
  uma coluna permanente para um estado que dura minutos é a faixa vazia comendo a tela que o
  README condena desde a parcela 38 — o que a recepcionista precisa saber não é que existe
  uma coluna nova, é QUAL cartão está pronto para fechar.
  **Encerrar com a sessão em branco PERGUNTA, não impede** (registro que não se consegue
  salvar é registro que não acontece), e **encerrar de novo não reescreve a hora** (`??=`,
  a razão do "chamar de novo": quem clica duas vezes precisa continuar vendo a hora em que
  terminou, e o segundo clique esconderia justamente o atendimento demorado).
- **Três seções do prontuário estavam FORA do paciente** (parcela 74): prescrever exigia
  **sair do paciente**, ir a um item de menu, escolher a pessoa de novo e voltar. É a porta
  no lugar errado — o defeito que o projeto já corrigiu doze vezes ENTRE módulos — cometido
  agora dentro de um app só, e era o que mais separava a tela de um prontuário eletrônico de
  verdade. Viraram sete seções num **RAIL VERTICAL**, e a escolha não é gosto: o `TabPanel`
  do WPF **espreme** as abas (o defeito da parcela 50 — "Convê", "Prontu", "Documer") e sete
  rótulos quebrariam a régua em duas linhas mesmo com `WrapPanel`. `AbaAtual` continua sendo
  o contrato — as chaves de navegação de outros módulos caem cada uma na sua seção
  (`ModuloClinico.AbaDe`), e trocar leiaute não pode quebrar navegação (a regressão da
  parcela 37, 4ª rodada). A tela que vira seção ganha `MostrarCabecalho = false`: dentro do
  workspace o nome já está no crachá, e o **seletor de busca dela trocaria o
  `PacienteEmFoco` por baixo das outras seis seções**, que continuariam mostrando o paciente
  anterior.
  **"Exames e anexos" muda o EIXO da leitura, e é a lição que generaliza**: os anexos só se
  alcançavam sessão a sessão, o que responde *"o que tem nesta consulta"*. A pergunta de
  quem atende é outra — *"eu pedi a ressonância; ela chegou?"* — e ela não se responde
  abrindo quarenta sessões uma por uma. **Dado com leitor pode estar com a CHAVE errada**, e
  isso não aparece em teste nenhum.
- **A alergia é ATRIBUTO DA PESSOA, não alerta do dia** (`CabecalhoClinicoPaciente`, parcela
  74). O cabeçalho do consultório mostrava iniciais, nome e uma linha de contexto; idade,
  convênio, desde quando a pessoa se trata ali e as ALERGIAS estavam no banco e não no olho
  de quem atende. Alerta é faixa que se lê uma vez e se ignora nas quarenta sessões
  seguintes — é a razão pela qual este projeto recusa alerta que dispara para todo mundo
  desde a parcela 26. No crachá ela fica ao lado do nome enquanto o prontuário estiver
  aberto, e **entra mesmo dada por RESOLVIDA** (a regra da parcela 37: "resolvida" numa
  alergia é quase sempre "não reagiu da última vez"); só o DESCARTE, que exige motivo
  escrito, a cala.
  O crachá é também o **primeiro leitor da `HipoteseDiagnostica`** que a parcela 73 criou —
  sem ele seria mais um campo gravado sem leitor, o defeito recorrente cometido na parcela
  seguinte à que criou o dado. As últimas hipóteses saem **distintas e no máximo três**:
  repetir "lombalgia" nas oito últimas sessões gastaria a linha inteira dizendo uma coisa só.
  A **linha de identificação é montada no MODELO**, não no XAML, porque ela precisa PULAR o
  que não existe: paciente sem data de nascimento não pode produzir "· anos ·" com um vão no
  meio, e cadastro novo não tem "desde". Frase feita de bindings concatenados não sabe pular.
  E o **total de sessões conta só o que ACONTECEU** (`RealizadoEm`): desde a parcela 70 a
  guia nasce no agendamento, então contar linhas de `Atendimento` somaria as sessões da
  semana que vem — o crachá diria "24 sessões" a quem teve 18.
- **`BooleanToVisibilityConverter` sobre um `BitmapImage` é `Collapsed` para sempre**
  (parcela 74 — defeito meu, pego na revisão do próprio diff antes de sair). É a lição da
  parcela 61 na terceira variante: o conversor do WPF devolve `Collapsed` para qualquer
  valor que não seja `bool`, e a foto do paciente é um objeto. **A foto nunca apareceria**,
  com XAML bem-formado, binding válido, nada lançando e as três redes verdes. Entrou
  `ObjetoParaVisibilidade` no shell, e ele **não substitui** o `TextoParaVisibilidade`:
  string vazia não é nula, e usá-lo numa mensagem deixaria a caixa de alerta aberta e vazia
  depois de a mensagem ser limpa. **A pergunta que decide é "o que significa 'não tem'?"** —
  para texto é o branco, para objeto é o nulo.
- **`--` é ilegal DENTRO de comentário XML** (parcela 74): a linha de sublinhado de um
  cabeçalho de comentário (`----------------`) quebra o XAML inteiro com "not well-formed
  (invalid token)". Nos `.cs` ela é livre; no XAML, não. O `verificar-suite` pega na hora —
  mas vale saber antes de escrever, porque o estilo de comentário deste projeto usa
  sublinhado em toda seção.

- **Método de repositório SEM chamador em teste é código que ninguém executou** (parcela 74,
  2ª rodada — auditoria do próprio diff, e os dois primeiros achados derrubariam a tela em
  produção com 1778 testes, três redes locais e o CI verdes).
  ⚠️ **Consulta LINQ só se prova EXECUTANDO**, porque a tradução para SQL acontece em
  runtime. `AnexosDoPacienteAsync` filtrava por `!a.Evolucao.Cancelada`, e `Cancelada` é
  propriedade DERIVADA (`=> CanceladaEm is not null`), não mapeada: o EF recusa com
  *"Translation of member 'Cancelada' on entity type 'Evolucao' failed"*. A seção "Exames e
  anexos" derrubaria a tela no primeiro clique. A regra: **em `Where`/`OrderBy`/`Select`
  traduzido, use a COLUNA (`CanceladaEm == null`), nunca a derivada** — e todo método novo
  de repositório nasce com um teste que o EXECUTA, mesmo que trivial.
- **`Task.WhenAll` sobre o MESMO `IClinicaRepositorio` é um defeito, não uma otimização**
  (parcela 74, 2ª rodada). As leituras passam pelo mesmo `DbContext`, que **não aceita duas
  operações ao mesmo tempo**: `CabecalhoAsync` estouraria com *"a second operation was
  started on this context instance"* em toda troca de paciente.
  ⚠️ **Os treze testes do crachá passavam**, porque o SQLite em memória completa a consulta
  quase sincronamente e as quatro nunca chegavam a se sobrepor. É a mesma família do `xmin` e
  das datas com fuso — **o que só aparece com latência de rede real**. A rede que fechou o
  buraco é barata e generaliza: um `DbCommandInterceptor` que injeta `Task.Delay` no
  `ReaderExecutingAsync` (ver `CabecalhoClinicoTests.Cracha_monta_contra_um_banco_LENTO`).
  **Toda leitura composta nova deveria passar por ele.**
  ⚠️ E o agravante é de método: eu escrevi um comentário JUSTIFICANDO o paralelismo por
  performance. **Comentário que explica uma decisão errada a torna invisível para o próximo
  revisor** — foi por isso que ele sobreviveu à minha primeira leitura do diff.
- **Carimbo novo da fila entra no bloco que a REMARCAÇÃO limpa** (parcela 74, 2ª rodada). A
  parcela 69 documentou que "remarcar levava junto os carimbos da fila" e a 74 criou um
  carimbo novo (`FimAtendimentoEm`) **sem o pôr no bloco**. Um horário remarcado para outro
  dia nasceria com o selo verde "Encerrado às 09h40" de uma sessão que não aconteceu — e
  cartão em "Aguardando" dizendo que já terminou é lido pelo balcão como pronto para fechar.
  **Estado derivado de carimbo de hora tem de ser revisto em TODA escrita que muda o que o
  carimbo significa** — e "mudou de dia" continua sendo a maior delas.
- **`DispatcherTimer` ligado no ViewModel mantém viva cada tela já trocada** (parcela 74, 2ª
  rodada). Quem liga e desliga é a VIEW, no `Loaded`/`Unloaded` — o `MeuDiaView` faz assim
  desde a parcela 38 **com o motivo escrito no comentário**, e a parcela 74 o repetiu errado.
  Aqui o estrago era maior: cada workspace abandonado segurava a ViewModel dele **e as SETE
  sub-ViewModels**, batendo a cada 15 s; num turno de vinte pacientes, vinte deles.
  ⚠️ E não basta mover o `Start()`: a função que recalcula o estado religa o timer sozinha, e
  por isso ela precisa de uma guarda de "a tela está montada" (`_naTela`).
- **Antes de aceitar uma leitura, meça o que ela TRAZ** (parcela 74, 2ª rodada — a lição da
  parcela 69 pela segunda vez). O crachá lia `EvolucoesDoPacienteAsync` — o prontuário
  INTEIRO, com o texto de cada evolução, a conduta e as orientações — para extrair **três
  frases curtas**. Num tratamento de quarenta sessões é meio megabyte a cada troca de
  paciente, que no consultório é o gesto mais repetido do dia. Virou `HipotesesRecentesAsync`,
  uma projeção de uma coluna com teto no SQL.

- **Campo novo de prontuário entra em TRÊS lugares no mesmo commit — e nenhum quebra o
  build** (parcela 74, 2ª rodada; o defeito é da 73 e passou por tudo). Os quatro campos da
  sessão médica (`HistoriaDoencaAtual`, `ExameFisico`, `HipoteseDiagnostica`, `CidSessao`)
  entraram na entidade, na tela, no PDF, na exportação e no art. 18 II — e **não** entraram
  em `ProntuarioService.SalvarAsync`, que copia campo a campo para a entidade rastreada.
  ⚠️ **O efeito era o pior possível porque a CRIAÇÃO funcionava**: o objeto é novo, tudo era
  gravado, a tela mostrava certo. Só a primeira EDIÇÃO — acrescentar uma linha à evolução —
  apagava a anamnese, o exame físico, a hipótese e o CID. Sem erro, sem aviso. E
  `GuardarVersao` também não os copiava, então **a versão anterior não os tinha**: o dado
  sumia para sempre, contra o ponto 2 do compromisso de conformidade e o art. 3º da Lei
  13.787/2018 (rastreabilidade da retificação).
  Os três lugares são: **a cópia** do serviço, **o `GuardarVersao`** (com a coluna na
  `VersaoEvolucao` e a migration) e **a validação de "evolução vazia"** — sem a terceira, a
  primeira consulta, que é história + achado + hipótese ANTES de haver conduta, seria
  recusada nomeando campos que o médico preencheu.
  ⚠️ E há um QUARTO lugar quando existe mais de uma porta de edição: **quem não edita,
  PRESERVA.** A janela de evolução do BALCÃO não tem esses campos na tela e reenviava nulos —
  com a cópia corrigida, ela passaria a APAGAR o que o médico escreveu. Ela agora os carrega e
  os devolve intactos, e eles não viram propriedade pública de propósito: propriedade pública
  convida um XAML a mostrá-los, e dado clínico do médico não se edita no balcão. É a mesma
  armadilha que a parcela 68 achou no vínculo com o horário, com a diferença que decide o
  desenho: `AtendimentoId` nulo é "o chamador não sabe" porque **nenhuma tela oferece
  desligar**; texto nulo é ambíguo, porque a tela do Consultório oferece LIMPAR — e por isso a
  saída é preservar no CHAMADOR, não adivinhar no serviço.
- **Teste que reenvia a ENTIDADE RASTREADA não testa versionamento** (parcela 74, 2ª rodada —
  falso positivo meu, pego pelo próprio teste). Mutar o objeto que `SalvarAsync` devolveu e
  reenviá-lo faz `GuardarVersao` copiar o valor **já alterado**, e a versão anterior nasce
  igual à nova. As duas telas de produção constroem um `new Evolucao`, e o teste tem de fazer
  o mesmo — senão ele reprova o produto por um defeito que só existe dentro dele.

- **`ToQueryString()` contra o Npgsql é a rede que faltava para tradução de consulta**
  (`TraducaoNoNpgsqlTests`, parcela 74, 2ª rodada). A tradução de LINQ para SQL acontece em
  **RUNTIME** e é **específica do provedor** — o que o SQLite dos testes aceita pode não
  existir no Postgres da clínica. `ToQueryString()` COMPILA a consulta contra o provedor real
  e devolve o SQL **sem abrir conexão nenhuma**: a cadeia pode apontar para um banco que não
  existe. Se a expressão não for traduzível, lança ali, no `dotnet test`, meses antes de a
  clínica esbarrar. **Consulta nova que use navegação, agregação ou função entra nessa
  suíte** — e ela também confere que a projeção NÃO traz a coluna cara (foi assim que o
  `Conteudo` do anexo ficou fixado fora do SELECT).
  ⚠️ Ela tem **autoteste**, pela regra da checagem 34: um caso que escreve a consulta como
  estava ANTES da correção e afirma que a compilação REPROVA. Rede que não prova ter dentes
  nasce cega.
- **`git add -A` commita o que você não revisou — e a suíte local pode rodar contra uma
  ÁRVORE DIFERENTE do commit** (parcela 74, 2ª rodada; o CI reprovou por isso). Dois arquivos
  de prova temporários (`ZzProvaTemp`, `ZzSqlProva`) vazaram para commits meus. O primeiro
  derrubou o CI — e reprovava **pelo motivo certo**, porque era a única coisa no repositório
  que enxergava o defeito da anamnese.
  ⚠️ O detalhe que ensina é por que a rodada local ficou **verde**: o arquivo estava no
  COMMIT e tinha sido apagado do DISCO antes de eu rodar a suíte. 1787 testes verdes contra
  uma árvore que não era a que eu havia commitado. É uma variante nova de "verde não quer
  dizer funciona", e o hábito que a fecha é ler `git status --porcelain` **antes** do
  `add -A`, e olhar o conteúdo de todo arquivo de prova antes de descartá-lo — foi olhando
  o `ZzProvaTemp` que o defeito da anamnese apareceu, e olhando o `ZzSqlProva` que a rede de
  tradução nasceu.

- **Quem RETIFICA precisa receber tudo o que quem REGISTRA recebe** (parcela 74, 2ª rodada —
  achado da auditoria adversarial, e é bloqueador). `EvolucaoEnfermagemService.RetificarAsync`
  não tinha sequer o parâmetro do processo de enfermagem: **corrigir uma vírgula do texto
  descartava a consulta inteira** — histórico, exame físico, avaliação, diagnósticos e
  cuidados, as cinco etapas que a COFEN 358/2009 torna obrigatórias. A linha anterior ficava
  na base (o prontuário não se apaga) mas virava a SUBSTITUÍDA, e a que passa a valer nascia
  vazia. A tela dizia "Registrado".
  ⚠️ E a metade da TELA era pior: `Corrigir` copiava hora, texto e intercorrência **da LINHA
  da lista** — que é um resumo FORMATADO (os sinais vitais vêm como a frase "PA 160/100"). A
  correção nascia sem a pressão aferida, e o ponto sumia da curva do paciente. Agora ela
  CARREGA o registro do banco e devolve os números ao compositor.
  ⚠️ Terceira metade: a chamada passava `hoje` como data. **A correção mantém a DATA DO
  FATO** — a técnica que corrige na segunda um registro observado no sábado estaria movendo o
  fato de dia, e a folha do sábado perderia a linha.
- **O flag que decide a PERGUNTA não é o que decide a GRAVAÇÃO** (parcela 74, 2ª rodada).
  `SessaoEmBranco` olha só os campos de TEXTO, e com razão: ela existe para decidir se a tela
  pergunta *"encerrar sem escrever a evolução?"*, e EVA e mapa são MEDIDA, não o registro do
  que aconteceu. Usá-la também para decidir se GRAVA fazia o "Finalizar atendimento"
  **descartar em silêncio** a sessão de acupuntura mais comum da casa — EVA antes 8, depois 3,
  seis pontos no mapa, nenhuma linha de texto. O serviço aceita a sessão só com EVA desde
  sempre; era a tela que não a mandava. Entrou `TemAlgoParaGravar`, e a lição generaliza:
  **quando um booleano começa a ser usado numa segunda decisão, releia se ele responde à
  segunda pergunta** — o nome não avisa.
- **Voltar etapa que consome o clique sem mover o cartão é botão que não faz nada** (parcela
  74, 2ª rodada). `VoltarEtapaAsync` promete "volta o cartão UMA coluna", e `FimAtendimentoEm`
  **não é coluna** por decisão da própria parcela — então gastá-lo num passo próprio fazia o
  primeiro clique não mudar nada enquanto as duas telas afirmavam que o cartão tinha voltado.
  O encerramento passou a sair JUNTO com a entrada na sala (sair da sala é o fato: quem não
  está em atendimento não tem fim de atendimento), e desfazer SÓ o encerramento virou ato
  próprio — `ReabrirAtendimentoAsync`, com porta na barra —, do mesmo jeito que
  `DesfazerChamadaAsync` é separado desde a parcela 38.

- **Estilo local sem `BasedOn` apaga o controle inteiro, e o diálogo rotulado "desisti" era o
  que GRAVAVA** (parcela 75, 2ª rodada — a auditoria adversarial que ainda estava rodando
  quando a primeira foi dada por fechada; as 70 rodadas de refutação dela morreram por limite
  de sessão, então **nenhum achado chegou contestado** e todos tiveram de ser reconferidos à
  mão). Três defeitos, e os três da mesma família: **nada falha, nada avisa, e quem descobre é
  quem abre a tela.**
  ⚠️ **(a) `<Style TargetType="ctrl:EstadoDaTela">` local, sem `BasedOn`.** O `Template` do
  controle mora no estilo IMPLÍCITO do design system (`Feedback.xaml`) e **não existe
  `themes/generic.xaml` em projeto nenhum** — então o `DefaultStyleKeyProperty.OverrideMetadata`
  não tem tema de onde cair. Estilo explícito no elemento SUBSTITUI o implícito, e o que sobra
  é um controle vivo, com `Ativo`, `Titulo` e `Descricao` corretos, desenhando **nada**. Some o
  estado vazio E some o terceiro estado — a tela de uma leitura que FALHOU fica idêntica à de
  um paciente sem antecedentes, que é exatamente a confusão que o `NaoVerificado` existe para
  impedir. **As dez ocorrências equivalentes do repositório trazem o `BasedOn`; a minha era a
  única sem.** Padrão que dez arquivos seguem e um não é sinal de que o um está errado.
  ⚠️ **(b) `PromptWindow` só aceita texto OBRIGATÓRIO — e duas perguntas prometiam o
  contrário.** A janela recusa o vazio com um erro em vermelho ("Escreva o motivo para
  continuar"), e está certa: quase toda pergunta dela é o motivo de um cancelamento. O defeito
  nasce quando a PERGUNTA anuncia o campo como opcional — *"Se quiser, diga o que mudou
  (opcional)"* na revisão da anamnese e *"Deixe em branco para usar o nome do paciente"* no
  recibo do caixa. A pessoa clica Confirmar, leva o erro, e a **única saída que lhe resta é o
  Cancelar** — que o chamador lia como "siga". Nos dois casos isso GRAVAVA: uma revisão de
  prontuário com versão e auditoria (não se desfaz) e um recibo numerado por ano (desfeito só
  com cancelamento e motivo escrito). **A porta rotulada "desisti" era a que efetivava o ato.**
  A correção mora no COMPONENTE — `obrigatorio: true` por padrão, e com `false` a janela aceita
  o vazio e devolve `string.Empty`, o que separa "desisti" (`null`) de "siga, sem texto". Nos
  DOIS design systems, para as cópias não divergirem (o débito permanente da parcela 7); e a
  frase de erro deixou de dizer "motivo", porque a mesma janela pergunta nome do recibo, agente
  da alergia e quantidade contada.
  ⚠️ **(c) A checagem 39 nasceu acusando a correção que estava certa.** Ela cobra as duas
  metades juntas (a pergunta opcional passa `obrigatorio: false`; quem passa `false` distingue o
  `null`) — e disparou no código recém-corrigido, porque a janela de busca da guarda era medida
  em CARACTERES e `_sem_comentarios` **apaga o comentário com espaços, preservando as
  posições**: o bloco de cinco linhas que explicava a correção empurrou o `if (motivo is null)`
  para fora da janela. É literalmente o tropeço da parcela 41, cometido de novo dentro da rede
  que existe para não deixar rastro. O autoteste sintético não o pegava porque não tinha
  comentário no meio — **caso de teste sem o ruído do mundo real é caso de teste que aprova a
  checagem cega.** Agora o branco é colapsado antes de medir, e há autoteste com oito linhas de
  comentário entre a chamada e a guarda.

- **Relógio INJETADO com vazamento: o carimbo que a auditoria compara escapou dele** (parcela
  75, 2ª rodada — `Registro_atrasado_de_ontem_e_legitimo` falhou às 00h21, com 1816 outros
  testes verdes). `EvolucaoEnfermagemService` recebe `Func<DateTime> agora` e o fixture do teste
  o **congela ao meio-dia**, com o comentário dizendo por quê: *"senão o teste vira loteria
  perto da meia-noite"*. E `RegistradoEm = DateTime.Now` ignorava o relógio injetado — logo o
  atraso de um registro de "ontem às 16h" era medido contra a hora REAL da máquina: 20h quando
  a suíte roda de tarde, **8h quando ela roda depois da meia-noite**, e a asserção pedia mais de
  12h. O teste falha todo dia entre 00h e 04h e passou meses porque nenhuma rodada caiu nessa
  faixa. É a mesma aritmética do `TrimEnd('0')` da parcela 67: **teste que depende do relógio
  não é teste, é sorteio** — só que aqui o dado tem 24 faces e seis delas são ruins.
  ⚠️ **O mais revelador é onde o defeito NÃO está**: `ChecagemPrescricaoService` (parcela 42),
  que inventou o relógio injetado neste projeto, faz `RegistradoEm = _agora()` desde sempre e
  tem escrito ao lado quais usos de `DateTime.Now` ficaram de fora **de propósito**. A cópia
  posterior — a evolução de enfermagem, parcela 71 — herdou a ideia e perdeu a metade que a
  torna testável. **Ao copiar um mecanismo de um serviço para outro, copie a REGRA junto: o par
  `Momento` × `RegistradoEm` é o que a auditoria compara, e um lado medido por outro relógio não
  é um par.**

- **"Árvore limpa" não prova que a árvore é a que você empurrou** (parcela 75, 2ª rodada — o
  contêiner reiniciou duas vezes no meio da conferência). `git status` respondeu *"nothing to
  commit, working tree clean"* e *"up to date with origin/…"* sobre um clone recriado num commit
  **oito parcelas atrás**: a referência `origin/…` local também tinha voltado, então os dois
  lados concordavam — e concordavam sobre a coisa errada. Foi um `find` que não achou
  `AnamneseView.xaml` que denunciou. **Confira o SHA do `HEAD` contra o remoto (`git fetch` e
  comparar), nunca só o `git status`** — e desconfie de toda leitura de arquivo que responda
  "não existe" para algo que você acabou de escrever.

- **O roteiro preenchia MENOS DA METADE da consulta, e os sinais vitais paravam na
  enfermagem** (parcela 76 — a direção pediu enriquecimento do módulo clínico para os dois
  lados, médico e enfermagem). Os dois achados são o mesmo defeito recorrente do projeto em
  duas variantes, e nenhum quebrava nada.
  ⚠️ **(a) `ModeloEvolucao` nasceu na parcela 63 com QUATRO campos e a evolução passou a ter
  NOVE nas parcelas 73 e 75.** Quem montava "sessão de acupuntura — lombar" recebia quatro
  campos prontos e **redigitava os outros cinco toda sessão** — de modo que os campos novos,
  que existem para o registro ficar mais completo, viravam digitação a mais em vez de menos.
  A lição vai para a lista dos oito lugares como o **nono**: *campo novo de evolução entra no
  MODELO*. E o teste que a fixa é o do lugar 3 — `SalvarModeloAsync` **copia campo a campo**
  quando o nome já existe, então o que ficar de fora da lista some ao REGRAVAR enquanto a
  criação continua funcionando, que é o que esconde o defeito.
  ⚠️ **O modelo aplica o que a tela MOSTRA.** A janela de evolução da RECEPÇÃO edita quatro
  campos e PRESERVA os cinco da consulta sem exibi-los; aplicar ali os cinco gravaria no
  prontuário um texto que a pessoa não viu e não pode conferir — a garantia aparente que
  este projeto recusa desde a parcela 3. Então ela não os aplica **e DIZ isso** quando o
  modelo escolhido os traz: aplicar cinco de nove campos em silêncio é meio sucesso
  apresentado como sucesso.
  ⚠️ **(b) A clínica disse que TODO paciente passa pela enfermagem** — a PA, a FC e a
  temperatura são colhidas minutos antes da consulta —, `SinaisVitais` era gravado desde a
  parcela 71, e `AtendimentoViewModel` **não tinha uma única referência a ele**. Quem
  prescreve escrevia a sessão sem os números na frente, ou saía da tela para procurá-los.
  A tela mostra **LEITURA, nunca coleta**: colher ali daria dois lugares para gravar a mesma
  aferição. E são **três estados escritos** — aferido (com a procedência ao lado), não
  aferido naquele dia, e não foi possível conferir —, porque num campo de sinais vitais
  confundir "ninguém mediu" com "o banco não respondeu" é do tipo que muda conduta.
  A **procedência** ao lado do número (`às 09:12, por Joana (COREN-SP 999999)`) é o que
  separa "eu medi" de "a técnica mediu"; sem ela o número parece do próprio exame físico.
  ⚠️ **E a regra de QUAL REGISTRO VALE virou uma função só.** `EvolucaoEnfermagem.Vigentes`
  (canceladas fora, retificadas fora) já existia escrita à mão dentro do
  `MedidaClinicaService`, e a tela nova precisava da mesma resposta. Duas definições de "o
  registro que vale" divergem na primeira correção — e a que ninguém lembra de ajustar
  passa a responder com um número **DESDITO**, que muda conduta e é indistinguível do
  certo. É a asserção que carrega `SinaisVitaisNoAtendimentoTests`.
  A **data é a da SESSÃO, nunca hoje** (a dívida de prontuário e a Minha semana abrem
  horários de dias passados), e a aferição de **outro dia não entra**: ela já tem casa — a
  curva de PA da tela de Medidas, que junta as da enfermagem com a procedência em cada
  ponto. Trazê-la para o cabeçalho da consulta seria pôr um número de três semanas atrás
  onde se lê "os sinais deste paciente agora".

- **A enfermeira prescrevia o cuidado e nada registrava que ele foi FEITO** (parcela 76,
  segunda metade — a etapa 4 da COFEN 358/2009). A Resolução divide o Processo de
  Enfermagem em CINCO etapas, e o sistema cobria as três primeiras (histórico, diagnóstico,
  resultado esperado) e a quarta **só como texto**: escrevia-se "curativo a cada 12h" e a
  execução não existia em lugar nenhum. Implementação sem registro é intenção — e cuidado
  que não se registra é, para qualquer fiscalização, cuidado que não aconteceu.
  **Tudo o que o serviço faz já tinha sido pago caro na parcela 42** e foi reusado tal e
  qual: hora **INFORMADA** com o relógio ao lado, hora futura **recusada**, não realizado
  **exige justificativa**, nada se apaga (**retifica-se** em linha nova), e quem checa é
  quem fez **login**, com o COREN copiado no ato. O bit é o MESMO (`ChecarPrescricao`):
  checar a execução é o mesmo ato e a mesma responsabilidade, e um bit novo nasceria
  desligado justamente para quem já faz isso hoje.
  ⚠️ **A regra que NÃO se copia, e é a asserção que carrega a suíte**: na folha de infusão
  "item já checado não se edita" — o item é de administração ÚNICA. O cuidado tem
  FREQUÊNCIA e é executado de novo a cada turno; copiar aquela guarda impediria a segunda
  troca de curativo do dia, e a técnica registraria a primeira e desistiria da segunda.
  ⚠️ **`SeNecessario` é BOOLEANO, não adivinhado do texto da frequência.** Cuidado
  condicional sem registro não é trabalho atrasado — é a condição que não ocorreu —, e
  contá-lo deixaria todo plano com um SOS eternamente pendente, com o contador da sala
  apontando para nada (a sutileza que a folha de infusão já documentava). Ler "se dor > 5"
  com regex daria um palpite que erra nos dois sentidos sobre um campo que é texto livre
  por desenho. E o campo nasceu **com porta no mesmo commit** — caixinha na linha do
  cuidado —, senão a regra que ele sustenta nunca valeria.
  ⚠️ **Retificar a evolução tinha de devolver o `SeNecessario`.** A linha que recarrega os
  cuidados para a correção copia campo a campo, e sem ela a retificação DESLIGARIA o "se
  necessário" de todo cuidado condicional, em silêncio — a regra "quem não edita, PRESERVA"
  aplicada ao que EDITA.
  ⚠️ **E a regra 8 do compromisso foi cumprida no mesmo commit**: a execução entra na
  EXPORTAÇÃO (planilha própria, com a retificada marcada — exportar o cuidado prescrito sem
  o que foi executado dá um prontuário que diz o que se mandou fazer e cala sobre o que foi
  feito) e na GUARDA, onde ela **move o prazo** — um plano escrito em janeiro é executado
  por semanas, e contar só a prescrição daria o prazo calculado pelo registro errado.
  ⚠️ O nome do cuidado, na exportação, sai do que **já está em memória** e nunca de
  `x.Cuidado`: a leitura é `AsNoTracking` sem `Include`, a navegação viria NULA em produção
  e a coluna sairia "#123" — enquanto o teste passaria pelo relationship fixup do EF. É a
  lição da parcela 68, e ela foi pega **na escrita** desta vez, não numa auditoria depois.

- **A conferência na escrita pega muito, e a RELEITURA do próprio diff ainda pega três**
  (parcela 76, 3ª rodada — a direção perguntou se eu havia conferido durante a escrita). A
  resposta honesta: a lista pegou seis coisas enquanto eu escrevia (a cópia campo a campo do
  `SalvarModeloAsync`, o `x.Cuidado` que viria nulo em produção, o `SeNecessario` que a
  retificação desligaria, a checagem 24 no selo, o `defaultValue` da coluna nova, o
  `timestamp without time zone`) — **e reler o diff inteiro depois de verde ainda achou mais
  três, todos meus e todos da mesma família.**
  ⚠️ **`PlanoNaoVerificado` gravado e sem LEITOR.** Eu escrevi a propriedade, documentei-a
  como "o terceiro estado" e nenhum XAML a lia. O texto do resumo salvava a situação por
  acidente; o booleano era enfeite. **Booleano de estado que ninguém lê não é estado — é uma
  atribuição.**
  ⚠️ **Um comentário prometendo o que o código não fazia.** `RetificarAsync` diz "a folha
  mostra as duas", e o quadro do dia filtrava as corrigidas ANTES de chegar à tela: sumia da
  vista justamente o registro retificado, que é o que uma auditoria de enfermagem procura. É
  o defeito da parcela 67 na área onde ele custa mais. A separação certa é a que ficou:
  `Checagens` leva TODAS as do dia (a corrigida marcada) e `Vigentes` é quem decide o que
  VALE — filtrar na leitura misturava as duas perguntas numa lista só.
  ⚠️ **`HistoricoDoCuidadoAsync` com chamador só no TESTE** — capacidade sem porta nascida
  no mesmo commit que a documenta. Removido: a exportação já leva o histórico inteiro, e o
  teste passou a ler pelo repositório, que é o caminho que a produção usa. **Método que só o
  teste chama é método que o teste inventou.**
  A lição de método, e ela não contradiz a regra da AUDITORIA DE LINHA: **a lista se percorre
  ao escrever CADA trecho, e o diff inteiro se relê UMA vez antes do commit.** São conferências
  diferentes — a primeira olha a linha, a segunda olha o que sobrou. Nenhuma das duas é rodada
  de agentes, e as duas cabem em minutos.

- **As duas fichas eram UMA rolagem só, e a do médico escondia metade do registro atrás de
  um Expander recolhido** (parcela 77 — a direção pediu reorganização de abas e sub-abas nas
  fichas de atendimento). As duas viraram **sub-abas**, e a forma não foi inventada:
  **S-O-A-P** do lado do médico (o que a pessoa DIZ, o que se ACHA nela, o que isso É, o que
  se vai FAZER) e as **cinco etapas numeradas da COFEN 358/2009** do lado da enfermagem. Não
  por acaso são a mesma forma — e ver as duas fichas com a mesma estrutura é o que faz o
  prontuário parecer um só sistema.
  ⚠️ **Sub-aba ESCONDE campo, e campo escondido de prontuário é como se escreve menos sem
  perceber.** É a única objeção séria à reorganização, e ela precisa de resposta no desenho,
  não de boa vontade: cada aba do médico anuncia se TEM conteúdo, com um ponto no rótulo que
  se vê de qualquer aba. É ele que denuncia o "Subjetivo" vazio de quem foi direto ao Plano.
  Sem esse indicador, dividir em abas seria trocar leiaute por registro mais pobre.
  ⚠️ **E a ficha da enfermagem NÃO ganhou o ponto — de propósito.** Ali já existe a faixa
  `EtapasEmFalta`, que NOMEIA a etapa que falta. Um segundo indicador seria uma segunda
  resposta para a mesma pergunta: **um estado vazio por pergunta** (parcela 37). A regra é
  "resolva a objeção", não "aplique o mesmo widget nos dois lados".
  ⚠️ **A altura fixa do `TabControl` da enfermagem conserta um defeito, não é estética.**
  Marcar "Consulta de enfermagem" abria as cinco etapas dentro de um `StackPanel` sem teto,
  numa janela de altura FIXA: o compositor crescia e espremia a **linha do tempo** — que é
  justamente o que se lê ANTES de escrever — até ela sumir. Cada aba rola por dentro.
  ⚠️ **O Expander recolhido era pior que a rolagem.** Os cinco campos da parcela 73 nasciam
  escondidos atrás de um clique que ninguém dá — metade do registro clínico invisível por
  padrão. Aba vazia se vê; seção recolhida, não.

- **A checagem 24 acusava `Text` LITERAL por causa de um binding no vizinho** (parcela 77):
  ela procurava a palavra "Binding" **na tag inteira**, então um `<TextBlock Text="●"
  Visibility="{Binding ...}" />` — o indicador das abas da sessão — era reclamado como "texto
  do banco sem quebra". A regra dela é "texto do BANCO tem tamanho imprevisível"; um literal
  o programador mede ao escrever, tenha ele binding no `Visibility`, no `Foreground` ou em
  nada. Passou a olhar o atributo `Text`, com autoteste nos dois sentidos.
  A lição vale para toda checagem futura: **quando ela reclamar do que está certo, o defeito
  é dela.** Silenciar o aviso acrescentando o atributo pedido é o caminho fácil e é o que
  ensina a próxima pessoa a ignorá-la — e checagem que se ignora para de pegar o defeito de
  verdade.

- **Os itens novos das duas fichas, e o lugar por onde um campo de prontuário some sem
  quebrar nada** (parcela 77, 2ª parte). **Médico**: `RetornoSugeridoEm` + nota e
  `Encaminhamento`. **Enfermagem**: o `AcessoVenoso` (local, calibre, punção).
  ⚠️ **O retorno NÃO vira agendamento**, e a regra é a mesma que a parcela 58 arrancou do
  2º código: materializar a sugestão como `Agendamento` põe na fila do balcão e na agenda de
  quem atende um cartão fantasma com botões que fabricam guia. Aqui é AFIRMAÇÃO CLÍNICA —
  "reavaliar em 7 dias" —, e quem marca horário é a recepção, com o paciente na frente.
  ⚠️ **E ele NÃO entra no `ModeloEvolucao`**, com a exceção declarada na entidade: o nono
  lugar da lista (parcela 76) vale para o que é ROTEIRO, e retorno e encaminhamento são
  decisões DESTA consulta. Um modelo que trouxesse "encaminhar para psiquiatria"
  preencheria sozinho, em toda sessão, uma afirmação sobre um paciente que ninguém leu.
  ⚠️ **O acesso venoso é três campos na EVOLUÇÃO, e não uma entidade.** Ele é um ACHADO da
  passagem, como a PA: quem o descreve é quem o viu naquele momento. Uma entidade "acesso"
  com ciclo de vida próprio precisaria de retirada, troca e motivo — nada disso foi pedido,
  e seria construir a exceção que ninguém vai exercer. E **em branco quer dizer "não
  avaliado", nunca "não há acesso"**: gravar o vazio como afirmação de ausência é inventar
  um achado que ninguém fez.

- **A busca no prontuário existia DUAS vezes, e as duas já tinham divergido** (parcela 77).
  `Casa(Evolucao, termo)` estava copiada na tela do Consultório e na da Recepção — e o
  comentário da segunda dizia, **por escrito**, que a primeira tinha sido atualizada e ela
  "ficou para trás". Consertaram uma vez e a cópia ficou; acrescentar campo agora as faria
  divergir de novo. Virou `BuscaNoProntuario.Casa`, no domínio.
  ⚠️ O que torna esta duplicata pior que as outras: divergência aqui não vira erro, vira
  **lista incompleta** — indistinguível de "não há nada escrito sobre isso", que é a pior
  resposta possível para uma busca de prontuário.

- **Três derivadas nascidas sem leitor, no commit seguinte ao que documentou isso**
  (parcela 77): `AcessoResumo`, `TemAcesso` e `DiasDeAcesso` existiam calculadas e nenhuma
  tela as mostrava — estruturar o acesso serve exatamente para LER, e a releitura do diff
  as pegou antes do commit. Ganharam três leitores: a linha da passagem (é o achado que a
  próxima punção consulta), a trilha de auditoria e a exportação do titular (art. 18 II).
  **É a segunda vez em duas parcelas.** A conferência que pega isto não é a lista percorrida
  linha a linha — é a releitura do diff INTEIRO perguntando "o que aqui não tem quem leia?".

- **O painel das últimas sessões mostrava 4 dos 12 campos — e o que faltava era a razão da
  consulta de hoje** (parcela 77, 3ª rodada — a direção perguntou se, ao abrir o atendimento
  de um paciente que retorna, o profissional vê a sessão passada). **Vê**: as três últimas
  ficam ABERTAS ao lado do formulário desde que a tela nasceu, com a linha do tempo de
  enfermagem e infusões abaixo, e a aba "Histórico de sessões" com o prontuário inteiro. O
  desenho estava certo; o CONTEÚDO tinha envelhecido.
  ⚠️ `LinhaSessaoAnterior` nasceu com quatro campos e a sessão passou a ter doze (parcelas
  73, 75 e 77). Ficava cega para a hipótese, o CID, o plano, o encaminhamento e — o que mais
  dói — o **retorno sugerido**: o profissional escrevia "voltar em 7 dias para reavaliar a
  EVA", o paciente voltava, e a tela **não dizia**. E a `Evolucao`, que é o texto mais
  escrito do sistema, estava no modelo e **não estava no XAML** desde que o painel existe —
  dado calculado sem leitor no lugar onde ele mais importa.
  A lição, que é a mesma da parcela 76 num SEGUNDO leitor: **campo novo de evolução entra
  também em quem MOSTRA a sessão anterior.** Ela vai para a lista dos oito lugares junto com
  o modelo — os dois são leitores que não quebram nada quando são esquecidos.
  ⚠️ **E a composição mudou de casa**: saiu da ViewModel para a `Application`
  (`ResumoSessaoAnterior`). Ela tem decisões — a hipótese com o CID entre parênteses, o CID
  sozinho quando é só ele, a linha que SOME quando não há o que dizer — e decisão que mora
  em projeto WPF não é alcançada pelo `dotnet test`. É a lição da grade da semana (parcela
  69) aplicada de novo, e é o que permitiu os nove testes que agora a fixam.
  O **retorno é o único campo do painel que a tela NÃO corta**: os outros saem com
  reticências e a dica mostra o texto inteiro, porque a coluna tem ~350 px e três sessões, e
  o texto completo está a uma aba de distância. Ele não, porque é curto e é a resposta.

- **A FICHA DO ATENDIMENTO: o motor existia, faltava a PORTA — e faltava a ENFERMAGEM
  inteira** (parcela 78 — a direção pediu um PDF do atendimento, com a logo e no padrão dos
  papéis que já saem, para o médico e a enfermeira entregarem ao paciente). A resposta
  honesta era **temos o motor e falta a porta**: o relatório de evolução é um
  `DocumentoClinico` desde a parcela 3, com a marca da clínica, numeração por ano, código de
  conferência, PDF pelo `DocumentosClinicosPdfService` e assinatura ICP-Brasil — e os **dois
  únicos chamadores dele estavam na RECEPÇÃO** (`FichaPacienteViewModel` e
  `DocumentosViewModel`). Quem acabou de escrever a sessão pedia ao balcão para imprimi-la.
  É o defeito recorrente do projeto na variante "a porta está no módulo de quem não usa",
  agora com o agravante de o papel ser justamente o que se entrega **com o paciente ainda na
  sala**.
  As três metades que faltavam, e o que cada uma decide:
  (a) **O RECORTE.** Os dois chamadores emitem sem `inicio`/`fim`, isto é, o histórico
  INTEIRO — para entregar "o atendimento de hoje", um paciente de quarenta sessões recebia
  quarenta. A porta nova emite recortada **na data DESTA sessão** (a do médico) e em **hoje**
  (a da enfermagem). O relatório sem recorte continua existindo e é outro papel: é o do
  convênio.
  (b) **A ENFERMAGEM não tinha papel NENHUM.** A passagem só saía impressa dentro da folha
  de infusão, ou seja, quando havia folha: a técnica que colhe sinais vitais, troca o
  curativo e registra a consulta de enfermagem completa — as cinco etapas da COFEN 358/2009 —
  não tinha o que entregar a ninguém. Ela entra no **MESMO documento**, e não num tipo novo:
  o paciente veio uma vez, e o que aconteceu com ele é um fato só; dois papéis obrigariam a
  clínica a entregar dois e o convênio a casar duas numerações. Só as **vigentes** —
  cancelada ou já retificada é registro desdito, e ele fica no prontuário, não no papel que
  sai da clínica. Quem assina cada linha é quem a fez, **com o COREN**.
  ⚠️ (c) **A LINHA DO TEMPO era uma promessa não cumprida.** O comentário das escalas
  garante, desde a parcela 36, que elas entram "na MESMA linha do tempo das sessões" — e
  elas eram **anexadas depois de todas**: o papel saía com as sessões, depois os escores,
  depois a enfermagem. Quem lê comparava o escore de agosto com a sessão de junho. Agora
  cada item carrega a DATA e a lista é ordenada na emissão (`OrderBy` do LINQ é ESTÁVEL, e é
  isso que mantém sessão → enfermagem → escala na ordem em que o dia aconteceu). É o defeito
  da parcela 67 — comentário que descreve um comportamento e não o realiza — num arquivo que
  eu estava editando por outro motivo.
  ⚠️ **`evolucoes[0]` ESTOURAVA numa ficha só de enfermagem**, e essa ficha passou a existir
  nesta parcela: o período do documento saía da primeira evolução MÉDICA. Agora sai do que de
  fato entrou no papel (`Menor`/`Maior` das duas listas), e a frase da EVA **só existe quando
  há sessão médica** — numa ficha de enfermagem, "nenhuma sessão tem a EVA medida" seria uma
  afirmação sobre um registro que não se propôs a medi-la.
  ⚠️ **O DIAGNÓSTICO e os CUIDADOS quase ficaram de fora, e o comentário que eu mesmo
  escrevi foi quem denunciou.** A primeira versão levava ao papel o texto, os sinais vitais,
  o acesso venoso e as etapas 1 e 5 — com um comentário dizendo "as etapas do processo entram
  quando FORAM escritas". As etapas **2, 3 e 4** não entravam, e são elas que fazem a consulta
  de enfermagem ser uma consulta: sem elas o papel diz "consulta de enfermagem" e mostra um
  texto livre, que é exatamente o que a clínica tinha antes de a etapa existir. As duas
  listas já vinham carregadas da consulta do repositório — era dado gravado sem leitor no
  único papel que sai da clínica. A regra de leitura vale para o próprio diff: **quando
  terminar de escrever um comentário, confira se o código abaixo dele faz o que ele diz.**
  **Emitir é FATO, então a ficha sai depois de SALVAR.** O papel é numerado, fica na lista do
  paciente e não se apaga — cancela-se com motivo; imprimir o que ainda não está no prontuário
  entregaria ao paciente uma versão que o prontuário não tem. ⚠️ A pergunta da guarda é
  `TemAlgoParaGravar`, **nunca** `!SessaoEmBranco`: confundir as duas é o defeito da parcela
  74, e aqui ele faria a sessão de acupuntura mais comum da casa — EVA 8→3, seis pontos no
  mapa, nenhuma linha de texto — **sair impressa dizendo "EVA não medida"** com o 8→3 na tela
  de quem imprimiu. E o `IsEnabled` do botão cobre as **duas** pré-condições que a guarda
  impede (paciente em foco E `VerProntuario`), senão quem não tem o bit clica e leva a recusa
  depois.

- **A FICHA DESENHADA: o mapa corporal e a curva da dor no papel** (parcela 79 — a direção
  pediu a ficha "animada com mapas corporais e outros, estilizado mas sem fluflu"). O mapa
  existe desde a parcela 3 e **nunca saiu impresso**: a sessão de acupuntura — a
  especialidade da casa — era relatada por texto, e onde a agulha entrou só se via na tela
  de quem marcou. As decisões, e por que cada uma:
  ⚠️ **A silhueta SUBIU PARA O DOMÍNIO, e não foi desenhada de novo.** Ela era um
  `GeometryGroup` no XAML do `MapaCorporalControl` — e o comentário no topo daquele arquivo
  já dizia por que copiá-la é proibido: *"copiar a figura teria criado duas versões do mesmo
  desenho — e a segunda correção de silhueta já sairia divergente"*. Pôr o mapa no papel ia
  criar essa segunda cópia, agora **atravessando camadas**, que é onde ninguém as lê lado a
  lado. Divergir aqui não estoura nada: produz um papel em que a agulha está num lugar do
  corpo e a tela mostra outro. Agora `SilhuetaCorporal.Formas` é a lista, o WPF monta o
  `GeometryGroup` dela e o PDF monta um SVG — e o **220×460 também virou uma definição só**
  (a const da tela lê a do domínio, e o Canvas não tem mais os números no XAML): os três
  TÊM de concordar, porque é dividindo por eles que o clique vira fração, e divergir espalha
  as marcações **em silêncio**.
  ⚠️ **O desenho é COPIADO na emissão** (`ItemDocumento.Desenho`, migration aditiva), nunca
  lido do prontuário na hora de imprimir. É a regra mais antiga do `DocumentoClinico` — a
  segunda via tem de sair idêntica à que o paciente levou —, e a sessão pode ser corrigida
  depois. Aqui ela não é desenho, é a Lei 13.787/2018.
  ⚠️ **E foi um `new` que quase engoliu tudo:** `EmitirAsync` copia o item **campo a campo**,
  e o `Desenho` era montado certo pelo relatório e sumia ali. O **lugar 3 da auditoria de
  linha**, na emissão do documento — a ficha saía sem mapa nenhum, sem erro em lugar nenhum.
  Quem o pegou foi o teste que abre o PDF, não a leitura do código.
  **A curva da dor** (`GraficoDaDor`, na Application) não repete a frase do corpo: mostra o
  que a frase não cabe — se a queda foi contínua, se houve recaída, se o alívio de cada
  sessão **dura** até a seguinte (daí serem DUAS linhas, antes e depois; só a de depois
  diria que o paciente melhorou, só a de antes esconderia o efeito da sessão). **O eixo
  ancora em 0 e 10** porque é a escala publicada da EVA — ajustá-lo aos dados faria uma
  queda de 8 para 7 parecer um despencar. **Duas sessões no mínimo**: uma é linha de base, e
  um ponto solto num eixo prometeria uma evolução que o registro não tem.
  **Três armadilhas de leiaute do QuestPDF, e as três só a folha montada mostra:**
  (a) **`AspectRatio` é largura/altura**, e invertê-lo pediu uma curva de 1633 pt de altura
  — o QuestPDF então **RECUSA o documento inteiro**, não desenha torto;
  (b) **elemento com largura FIXA maior que a área útil derruba a folha**: os 520 pt do
  sistema de coordenadas do gráfico contra ~510 pt de A4 com as margens da casa. Quem tem
  tamanho próprio pede proporção, não pontos;
  (c) **`ShowEntire` sobre bloco que cresce com o dado é uma bomba**: envolvendo a legenda,
  uma sessão no teto do mapa (80 pontos) pediria mais que uma página e a ficha **deixaria de
  imprimir**. Ele cobre o título e as figuras — altura conhecida — e a legenda corre solta.
  E o título entra JUNTO das figuras: a data sozinha no pé da página, com o corpo na folha
  seguinte, é um rótulo que não diz de que sessão ele é.
  ⚠️ **A lição de método é a da parcela 68, e ela se pagou três vezes nesta rodada:
  RENDERIZAR É DE GRAÇA.** `pdftoppm` desenha a folha, e foi só olhando que apareceram o
  título órfão, a página e meia em branco (uma figura de 130 pt fazia caber UM mapa por
  página; 112 pt fazem caber dois) e o vão morto ao lado do corpo, que virou o **resumo por
  técnica** — a leitura que o `TecnicaPonto` existe para permitir desde a parcela 3 ("o que
  dá para contar depois sem ler prontuário") e que nenhuma folha mostrava. **Teste que prova
  que o arquivo FECHA não prova que a folha MOSTRA**: o teste que carrega esta parcela gera
  o MESMO documento com e sem o desenho gravado e compara os operadores de caminho dentro
  dos fluxos de página.
  Detalhes que o código não conta sozinho: o SVG é escrito em cultura **INVARIANTE**
  (`cx="110,5"` em pt-BR é atributo inválido — o círculo some, ou a figura inteira não
  desenha, e nada avisa); a legenda numera **1..n na ordem**, não pelo `Ordem` cru, que fica
  com buracos depois de uma remoção e faria o leitor procurar os pontos 3 e 4 que não
  existem; **ponto estragado descarta só ele** — uma ficha que se recusa a imprimir por um
  campo mal gravado é pior do que uma com um ponto a menos, com o paciente esperando; e
  **técnica desconhecida mantém a marcação** (vira "Outra técnica"), pela regra do conversor
  tolerante da parcela 67: o ato aconteceu, e sumir com ele apagaria do papel algo que foi
  praticado.

- **O NOME DA TABELA SAI DO `DbSet`, NUNCA DA CLASSE — e migration aqui é escrita à mão**
  (parcela 79 — a clínica mandou o print: *"Não foi possível conectar ao banco de dados:
  42P01: relation `UsuariosSistema` does not exist"*, ao abrir o Consultório). A FK da
  tabela nova da parcela 76 apontava `principalTable: "UsuariosSistema"`, o nome da CLASSE;
  a tabela chama-se **`Usuarios`**, porque quem a nomeia é o `DbSet<UsuarioSistema>
  Usuarios`. `dotnet ef` acertaria sozinho — e não há `dotnet ef` neste ambiente, então a
  migration é digitada, e este é o único lugar do repositório onde o nome de uma tabela é
  digitado à mão.
  ⚠️ **O estrago não é a tabela que falta: é o que o usuário lê.** O erro estoura no
  `MigrateAsync` da ABERTURA, e o `SuiteApp` o apresenta como *"não foi possível conectar ao
  banco — deseja informar outra conexão?"*, que manda a clínica caçar a connection string,
  trocar a que estava certa e (se disser Sim) apagar a configuração da suíte. **Erro de
  schema exibido como erro de conexão é diagnóstico errado impresso na cara de quem não tem
  como saber** — é a mesma família da mensagem "See the inner exception" da parcela 67, com
  o agravante de a frase sugerir uma ação que piora o estado.
  ⚠️ **NENHUMA das redes podia pegar, e a razão é estrutural**: o C# compila (é string), o
  `compilar-sombra` não lê migration, e **os testes não executam migration nenhuma** — o
  SQLite deles monta o schema pelo MODELO, com `EnsureCreated`. É a terceira vez que este
  buraco aparece com outra roupa (o `xmin` da concorrência otimista, as datas com fuso da
  parcela 67): **"só o Postgres pega" quer dizer "só a clínica pega".** Virou a **checagem
  41**, que cruza todo `principalTable:`/`table:` das migrations contra as tabelas que
  alguma migration CRIA ou RENOMEIA — medida antes de decidir: 68 tabelas, ~80 migrations,
  **uma** ocorrência, que era o defeito. Autotestada contra o caso real e contra cinco
  legítimos (inclusive a tabela criada no próprio arquivo e a renomeada).
  **O banco da clínica não ficou quebrado**, e isso é do Postgres, não nosso: DDL é
  transacional e o EF envolve cada migration numa transação — a `20260824020000` inteira
  voltou atrás, sem gravar a linha no `__EFMigrationsHistory`. As duas seguintes nem
  chegaram a rodar (o EF para na primeira que falha). Atualizado o app, a fila continua de
  onde parou. **Não peça à clínica para mexer no banco antes de conferir se o erro foi
  transacional** — o conserto costuma ser mais caro que a falha.

- **A RODADA DOS PRINTS DO CONSULTÓRIO: quatro defeitos de leiaute, e três eram regras já
  escritas que a tela nova não seguiu** (parcela 79 — o cliente mandou quatro prints e a
  pergunta "você não quer que eu apresente isso a cliente, ou quer?"). Nenhum quebrava
  build; todos se viam no primeiro olhar. O que cada um ensina:
  (a) **Cinco botões numa StackPanel horizontal estouram a linha — e dois deles eram o RAIL
  disfarçado de botão.** "Evolução da dor" e "Avaliações" existiam como botões da barra E
  como itens do rail ao lado: duas portas com o mesmo nome fazem a pessoa procurar a
  diferença que não existe, e foram os 240 px deles que empurraram o "Ficha do atendimento"
  para fora da tela. **Só AÇÃO mora na barra; navegação é do rail** — e os dois comandos
  órfãos foram removidos (código morto que duplica a navegação, parcela 63).
  (b) **A pilha de faixas voltou no app onde a regra nasceu.** Alertas clínicos,
  administrativos e a faixa do termo eram `Alerta*` empilhados com moldura própria, mais o
  botão avulso do termo flutuando sozinho — a pilha que o README proíbe. Viraram UMA
  superfície com uma linha por aviso (traço de 3 px, o padrão da parcela 47), o termo com a
  porta NA linha, e a região inteira some quando não há aviso. O peso segue a **gravidade**,
  não a categoria.
  (c) **A mensagem da tela morava numa faixa no TOPO e quem mais a escreve é o Salvar, no
  RODAPÉ.** A recusa da ficha aparecia como banner rosa de largura inteira, longe do clique
  (a lição da parcela 65). Ela desceu para o lugar da dica do rodapé — mesmo lugar, fundo
  opaco, cor pela gravidade.
  (d) ⚠️ **O `EstadoDaTela` do componente cobria o componente INTEIRO — e o RESUMO era a
  segunda resposta para o mesmo vazio.** Na `LinhaDoTempoClinicaView`, a sobreposição
  cobria chips e resumo, e o resumo em seção vazia escrevia "Sem registro de …" — as duas
  respostas saíam desenhadas UMA POR CIMA DA OUTRA (o texto atravessando o ícone, no
  print). As regras violadas eram três, todas deste arquivo: a sobreposição pertence à
  REGIÃO cujo vazio ela explica (58/64/68), um estado vazio por pergunta (37) — e vazio
  agora NÃO escreve resumo. `Ativo` desliga a sobreposição no caso SEM ACESSO, onde quem
  explica é o resumo.
  (e) **Três Cards empilhados na aba Histórico viraram uma superfície** (problemas ·
  enfermagem · sessões, separadas por rótulo + `Separator`); e em Medidas, **a régua
  PRIMEIRA/ATUAL/VARIAÇÃO some sem série** — três traços "—" sobre a frase "nenhuma
  colheita" eram duas respostas para a mesma pergunta, e régua vazia se lê como tela
  quebrada.
  A lição de método: **tela nova herda as regras deste arquivo por LEITURA, não por
  osmose** — as quatro violações eram de regras já escritas e pagas em outras telas. Ao
  criar uma tela, a lista de conferência inclui reler a REGRA DE LEIAUTE com a tela
  desenhada na frente, região por região: barra (só ações?), avisos (uma superfície?),
  mensagem (perto do clique?), vazios (um por pergunta, na região certa?).

- **FILHO ANCORADO QUE NÃO CABE É DECEPADO — e o que ficava fora da dobra era o botão de
  SALVAR** (parcela 79, 2ª rodada — o cliente mandou o print da consulta COFEN cortada na
  borda da janela de Evolução de enfermagem). A janela tinha 680 px fixos e o compositor
  era `DockPanel.Dock="Bottom"`, uma PILHA que, com a consulta aberta, pedia ~850 px
  (campos fixos ~300 + banner + `TabControl Height="320"` + mensagem + botões). O
  DockPanel **não encolhe filho ancorado**: ele arranja no que sobra e o excesso é
  decepado na borda — as abas pela metade E os botões Fechar/Registrar fora da tela. No
  monitor de 768 px do balcão, **a consulta de enfermagem completa não podia ser SALVA**.
  É a irmã da checagem 36 (lá a pilha era `StackPanel` numa página; aqui, filho ancorado
  numa janela fixa), e o `Height="320"` era um remendo da parcela 77 que devolvia a linha
  do tempo à tela — somado aos vizinhos, continuava passando da janela.
  As decisões da correção, e cada uma é uma regra velha aplicada:
  (a) **rodapé da JANELA, ancorado e declarado ANTES do miolo** (parcela 47) — mensagem ao
  lado dos botões que a escrevem;
  (b) **escrever à ESQUERDA, reler à DIREITA** — o MESMO desenho do Atendimento do médico;
  dois desenhos para a mesma pergunta no mesmo sistema é a reclamação da parcela 47, e a
  linha do tempo deixou de disputar ALTURA com o formulário: passou a disputar LARGURA;
  (c) **as abas são o filho que PREENCHE, com piso BAIXO** (`MinHeight="100"`): quem
  protege do corte é o preenchimento elástico — o DockPanel dá às abas exatamente o que
  sobra —, e um piso alto seria o `Height="320"` de volta, só que menor. Janela maior =
  abas maiores; janela mínima = abas apertadas com rolagem interna, e NUNCA corte;
  (d) **o teto do campo de texto é parte da conta** (`MaxHeight="72"` no "observado"): sem
  ele, o texto crescendo comeria a coluna e sobraria menos que o piso das abas.
  ⚠️ E a checagem 15 pegou a primeira tentativa em segundos — Height=720 + barra de título
  passa dos 728 úteis do balcão. **Quando a resposta a "não cabe" for "aumente a janela",
  a rede do teto é quem diz até onde**: 690 + barra = 721 ≤ 728.
  A regra que fica: **pilha dentro de filho ancorado de janela FIXA é a checagem 36 sem a
  checagem** — ao ancorar um bloco que cresce com um checkbox, o elemento que cresce vira o
  filho que PREENCHE (com piso baixo), nunca uma altura fixa somada a vizinhos.

- **A ALERGIA NO TERMO DO BSV: o destaque vermelho antes da assinatura, e por que ele NÃO
  é um campo do termo** (parcela 80 — pedido da cliente: *"antes da assinatura um ponto de
  destaque grande pra documentar alergia... em vermelho, que chame atenção, que a técnica
  possa relatar ali também... porque dali o paciente já tem que sair com uma pulseira de
  alergia"*). A janela de coleta (`AssinaturaPacienteWindow`, ponto único das quatro
  portas) ganhou uma região VERMELHA entre as declarações e a área de assinar — antes da
  assinatura porque a pergunta tem HORA: depois de assinado o paciente levanta, e ele tem
  de levantar COM a pulseira.
  As decisões, e cada uma é uma regra da casa aplicada:
  (a) **O que a técnica relata ali vai para a LISTA DE PROBLEMAS** (`NaturezaProblema.
  Alergia`, via `ProblemaPacienteService.SalvarAsync` — validação, auditoria e gravação
  atômica). A alergia tem morada ÚNICA (parcela 75): é a lista que acende os alertas nas
  quatro telas e RECUSA a assinatura de prescrição — um texto guardado no termo seria uma
  segunda verdade, e a que ninguém lembraria de atualizar é justamente a que o alerta lê.
  É também o que faz a pulseira valer além do papel: o alerta segue o paciente em toda
  prescrição futura.
  (b) **A região mostra as alergias JÁ registradas** (o filtro do `AlertasAsync`: alergia
  alerta MESMO dada por resolvida; só o descarte cala) e **a instrução operacional** em
  negrito vermelho quando há alguma: "ele sai daqui com a PULSEIRA DE ALERGIA no braço".
  Vazia e conferida, ela manda PERGUNTAR — a região existe para forçar a pergunta, não
  para decorar.
  (c) **Falha de leitura é terceiro estado**, nunca região vazia: "sem alergia" e "não deu
  para conferir" são respostas diferentes, e a segunda manda perguntar e registrar.
  (d) **Leitura sob `VerProntuario`** (dado de saúde, art. 5º, II — quem não pode ler não
  vê a região: anunciá-la contaria que existem alergias, a regra da parcela 59); **escrita
  sob `EditarProntuario` OU `RegistrarEvolucaoEnfermagem`** (`ExigirAlgum` — o precedente
  da evolução de enfermagem: a técnica escreve alergia pelo bit dela). O perfil Enfermagem
  tem os dois lados; a recepcionista não vê nem escreve.
  (e) **O registro não depende do desfecho do termo**: a alergia é verdadeira mesmo que o
  paciente recuse assinar — botão próprio, gravação imediata, confirmação que já manda
  colocar a pulseira.
  (f) **Nada entra no conteúdo SELADO nem na tela do paciente**: o selo cobre o que o
  paciente declarou e assinou; a alergia é registro clínico de quem a colheu, com a trilha
  própria da lista de problemas. Mexer no selo seria mexer na evidência por causa de um
  destaque de tela.

- **"PRESSÃO CAIU" SAIU COMO ALERGIA — e a causa era o RÓTULO de um campo** (parcela 80,
  2ª rodada — a médica viu a folha de execução listar "• Dipirona • Pressão caiu" sob
  ALERGIAS REGISTRADAS e mandou separar as intercorrências). A auditoria do dado achou a
  porta exata: a janela de Evolução de enfermagem tinha o campo **"Reação a registrar como
  alergia"** — que pergunta a REAÇÃO — e o serviço grava o texto como o ALÉRGENO na lista
  de problemas. A técnica escreveu "Pressão caiu" (a reação, exatamente o que o rótulo
  pediu) e o sistema passou a afirmar, em toda tela e papel, que o paciente é alérgico a
  "pressão caiu". A porta da RODELA sempre perguntou certo ("Alergia a quê? Escreva só o
  agente — ex.: Dipirona"), e foi por ela que a Dipirona entrou certa: **o mesmo ato com
  duas perguntas é a lição da parcela 64, na variante em que a diferença não é a regra, é
  a FRASE** — e a frase errada fabrica dado clínico errado com cara de certo.
  As correções:
  (a) **O rótulo pergunta o AGENTE** ("Alergia observada — a quê?", placeholder "só o
  agente (ex.: dipirona)"), e o tooltip manda a reação para onde ela mora: o texto da
  passagem com "Foi uma intercorrência" marcado. O placeholder do termo (parcela 80)
  ganhou o mesmo aperto — "o que o paciente relatou" convidava narrativa.
  (b) **A folha de execução ganhou a caixa INTERCORRÊNCIAS DESTA EXECUÇÃO, separada da de
  alergias.** São dois fatos de naturezas diferentes, cada um com a sua morada: alergia é
  atributo do PACIENTE (lista de problemas — vale para sempre); intercorrência é EVENTO
  desta execução (evolução de enfermagem com o selo). A leitura é pública e pura
  (`IntercorrenciasDaExecucao`): vigentes com o selo; cancelada e substituída são registro
  desdito; texto dramático sem o selo não conta. "Nenhuma registrada nesta execução" é
  AFIRMAÇÃO impressa, não linha omitida — quem audita precisa ler que não houve.
  (c) **O dado errado que já existe não se conserta por código**: "Pressão caiu" está na
  lista de problemas como alergia, e a porta desenhada para isso é o DESCARTE com motivo
  (o único que cala o alerta) — a clínica descarta com "registrada como alergia por
  engano; era intercorrência, e está no registro da passagem".
  A lição que fica: **campo que alimenta registro clínico se audita pelo RÓTULO, não só
  pelo código** — o serviço estava certo, o banco estava certo, e a frase na tela fabricou
  o dado errado. Ao criar campo que grava em lista de outra natureza, a pergunta do rótulo
  tem de ser a pergunta da LISTA ("a quê?"), nunca a do contexto ("o que houve?").

- **O TERMO PELO WHATSAPP: o paciente assina no PRÓPRIO celular, na sala de espera**
  (parcela 81 — o pedido da cliente: *"a gente envia, ele lê, assina, envia e já cai no
  nosso banco... o médico/enfermeiro/secretária abre o termo e envia via WhatsApp para o
  paciente assinar/ler enquanto espera"*. O mapa completo está em
  **`docs/termo-pelo-whatsapp.md`**, e é lá que se atualiza). O cenário derruba a objeção
  que a parcela 66 tinha ao link: o paciente está NA CLÍNICA, no dia — o jejum continua
  sendo sobre o presente, e a identidade se confere no check-in.
  O desenho em uma linha: o desktop publica um PEDIDO minimizado no balde
  (`t/{token}`, o token de 2^127 das receitas), abre o wa.me com o link, e fica LENDO o
  balde à espera da RESPOSTA que o Worker grava; a resposta volta à janela da técnica, que
  confere e conclui pelo MESMO `ColherAsync` do balcão. As decisões que não são óbvias:
  (a) **O Worker NUNCA toca o banco** — só um binding R2 no prefixo `t/`. Vazamento da
  borda expõe no máximo os pedidos em aberto, nunca credencial do Postgres. Foi a escolha
  estrutural contra "Worker consulta o Neon", que seria mais direto e poria a chave do
  banco da clínica na borda.
  (b) **O pedido é MINIMIZADO e há teste fixando** (nem o SOBRENOME sai): cada campo a
  mais ali é dado de saúde a mais no ar. A mensagem do WhatsApp também — notificação
  aparece em tela bloqueada.
  (c) **A resposta nunca sela sozinha**: quem conclui é a técnica, vendo o traço e as
  respostas, com o documento conferido obrigatório como sempre. O papel do fluxo é tirar o
  custo do pad, não a pessoa do circuito. **O selo não mudou** — mudá-lo invalidaria a
  conferência dos termos já assinados.
  (d) **Write-once na borda** (segunda gravação recusada) e, do lado de cá, traço inválido
  manda CANCELAR E REENVIAR — não há segunda chance no mesmo token, de propósito.
  (e) **Expira em 24h FIXAS** (o link é para a sala de espera); a limpeza é tripla —
  concluir apaga, a varredura do Gerente apaga, e o Worker recusa vencido pela data DE
  DENTRO do pedido. `EnviarAsync` é idempotente (o 2º clique reaproveita o token em
  aberto: o paciente pode já estar com o link na mão) e o vencido é cancelado COM registro
  — a linha `ColetasRemotasTermo` não se apaga nunca: ela é a evidência do canal
  (telefone da ficha, quem enviou, IP/aparelho/hora da resposta).
  As armadilhas da casa que a implementação REENCONTROU, e pagou na hora:
  ⚠️ **A Visibility da área local de assinatura tem UM dono** — o code-behind já a
  atribuía por código (modo duas telas), e o gatilho de estilo que escrevi primeiro
  morreria no primeiro valor local (parcela 58). Virou `AtualizarAreaLocal()` no
  code-behind, com as DUAS condições (painel do paciente OU traço remoto).
  ⚠️ **A vigia de 5s e o Confirmar compartilham o DbContext do escopo da janela** — sem
  porteiro, o clique no instante da batida estoura "a second operation was started on this
  context" (parcela 69). `SemaphoreSlim(1,1)` na frente de TODA operação de serviço da
  janela.
  ⚠️ **O teste verde não provava o `Include`**: o serviço lê `documento.Itens` e o teste
  passa por relationship fixup mesmo sem Include (parcela 68) — conferido no repositório
  que `ObterDocumentoAsync` traz os itens, senão a página sairia SEM AS PERGUNTAS, calada.
  ⚠️ **O STJ escapa acento por padrão** (`Simpático`): para artefato lido por gente e
  pelo nosso próprio Worker, `UnsafeRelaxedJsonEscaping` — e o teste confere por VALORES
  parseados, não por substring cega.
  O que fica dito e não prometido: envio automático (WhatsApp Business) não existe — é o
  wa.me de um clique; assinar em casa dias antes continua fora (o jejum nunca); e o Worker
  (`tools/worker-termo-whatsapp.js`, rota `dominio/t/*`) precisa ser publicado no
  Cloudflare para o botão fazer sentido — o botão só APARECE quando o endereço público da
  publicação está configurado.

- **NO `workers.dev` O HOSTNAME É O WORKER — e o desenho de "dois workers, duas rotas"
  morreu no primeiro teste da clínica** (parcela 82; o cliente mandou o print: o link do
  termo respondia "Documento não encontrado ou fora do ar", a frase do worker das
  RECEITAS). O link do termo é `{endereço público}/t/{token}`, e o endereço público
  configurado é o hostname `workers.dev` do worker das receitas: rota por caminho entre
  dois workers **só existe com domínio próprio** — no `workers.dev` a requisição vai
  inteira para o worker dono do hostname, e o segundo worker (criado certinho pelo
  cliente) nunca recebeu nada. O erro foi MEU desenho presumir o CNAME da parcela 53 numa
  clínica que publica pelo `workers.dev`.
  A correção segue a regra da casa: **um arquivo só** (`tools/worker-clinica.js`), com as
  duas funções despachadas pelo caminho (`/t/*` → termo; resto → receitas/validador),
  colado no worker cujo hostname está configurado, usando o binding `BUCKET` que já existe.
  Dois arquivos eram também DUAS CÓPIAS de infraestrutura para o mesmo domínio — a segunda
  definição que diverge na primeira correção. Os dois arquivos antigos foram REMOVIDOS.
  ⚠️ **Registrado, não resolvido**: o endereço selado nos QRs das receitas já impressas é
  o `workers.dev` — renomear aquele worker (ou a conta) mata os QRs no ar, que é
  exatamente o risco que a parcela 53 documentou ao exigir domínio próprio. Migrar para um
  CNAME da clínica é decisão para quando a direção quiser: o worker unificado atende os
  dois mundos sem mudar nada no app.
  A lição de método: **quando a instalação depende de infraestrutura do CLIENTE (domínio,
  DNS, conta), o roteiro tem de ser escrito para a infraestrutura que ele TEM, não para a
  que o desenho gostaria** — e a primeira pergunta do roteiro devia ter sido "o endereço
  público de vocês é um domínio próprio ou um workers.dev?".

- **O CSP ESCRITO À MÃO BLOQUEOU O PRÓPRIO POST DA PÁGINA — e o erro culpava a internet
  do paciente** (parcela 82, 2ª rodada — no teste da clínica, o Enviar da página do termo
  morria com "Sem conexão"; o console dizia a verdade: `violates Content Security Policy
  "default-src 'none'"... 'connect-src' was not explicitly set`). Eu escrevi
  `default-src 'none'` por rigor e esqueci que a página faz exatamente UMA chamada — o
  POST da assinatura para a própria origem. Sem `connect-src 'self'`, o navegador recusa
  o fetch ANTES de ele sair, o `catch` de rede o apanha, e a frase que eu escolhi mandava
  o paciente conferir o Wi-Fi por um bloqueio meu.
  Duas regras que ficam: **CSP restritivo lista as chamadas que a página FAZ** — escrever
  `'none'` e ir liberando é o caminho certo, mas o checklist é percorrer cada `fetch`,
  `img`, `style` e `script` da página, e esta tinha um fetch que o autor do CSP era eu
  mesmo; e **mensagem de erro de rede não pode afirmar a causa que não conferiu** — "sem
  conexão" era plausível e errada, a mesma família da mensagem que manda trocar a
  connection string por um 42P01 (parcela 79). A frase virou "o envio não saiu", que é o
  que de fato se sabe.

- **A CENTRAL DE DOCUMENTOS ENTERRAVA A PRÓPRIA LISTA — 700 px de cartões de emitir e "o
  que já saiu" abaixo da dobra** (parcela 82, 3ª rodada — o cliente: *"esse layout me
  irrita... não dá pra ver os itens em 'o que já saiu'"*). Os 11 cartões de emissão
  tinham **248×222 px fixos** cada (título + descrição + pendência + botão, com ar de
  sobra) e, somados à faixa lateral de 320 px com TRÊS caixas empilhadas
  (Paciente/Período/Conferir), a lista de emitidos — justamente a porta da 2ª via —
  começava fora da tela. A tela respondia três perguntas com o peso invertido: o que se
  FAZ às vezes (emitir, conferir) ocupava tudo; o que se CONSULTA (a lista) não aparecia.
  O redesenho, só XAML, zero mudança de comportamento:
  (a) **cartão virou FICHA compacta** (232×118): o que se lê no RELANCE fica — nome da
  folha e O QUE FALTA (a regra da parcela 24: o requisito se descobre antes de errar) —,
  a descrição longa foi para a DICA, e o botão ancora embaixo (fileira com botões na
  mesma altura se lê como conjunto). ~700 px viraram ~260;
  (b) **a faixa lateral ficou com UMA caixa** (Paciente — contexto real de sete folhas);
  (c) **Período foi para o cabeçalho da lista que ele recorta** (na lateral parecia
  configuração da página) e **Conferir pelo código virou uma linha ali** — caixa
  permanente de 200 px para ato ocasional é o proibido do README;
  (d) a barra do cabeçalho é **WrapPanel como filho que PREENCHE** (dock Top = largura
  finita → dobra em tela estreita; a checagem 32 continua de olho).
  Duas conferências que a mudança exigiu e valem de lição: **os textos que apontam lugar
  ("escolha o paciente AO LADO", "usa o período escolhido ABAIXO") foram conferidos contra
  o leiaute novo** — os dois continuavam verdadeiros, mas texto espacial é o primeiro a
  mentir numa reforma; e **"as NOVE folhas" do subtítulo eram onze** — número escrito em
  prosa apodrece a cada folha nova, e saiu.

- **A CENTRAL DE DOCUMENTOS EM DUAS ABAS — e a correção anterior tinha parado no meio**
  (parcela 82, 3ª rodada — o cliente: *"Ainda está totalmente amador! Por que o 'O que já
  saiu' não vira uma sub-aba dentro de Documentos?"*). A rodada anterior tinha encolhido
  os cartões e desmontado a faixa de três caixas — e DEIXOU a tela como duas caixas
  empilhadas com lateral permanente: mexeu no tamanho das peças sem responder a pergunta
  da regra de leiaute, **"quantas perguntas esta tela responde?"**. São DUAS ("emitir um
  papel novo" e "achar o que já saiu"), e pergunta a mais é ABA (parcela 55: item que
  cobre mais de um assunto usa sub-abas), não caixa menor.
  O que a separação destravou de graça: **a lista ganhou a largura inteira** — o seletor
  de paciente é contexto da EMISSÃO (sete das nove folhas são de alguém) e foi para a aba
  dela; a lista recorta por PERÍODO, não por paciente (conferido no ViewModel:
  `EmitidasAsync(inicio, fim, acessos)` — a lateral nunca a filtrou). E o H3 "O que já
  saiu" dentro da aba homônima saiu — rótulo da região é a própria aba; o que fica é o
  RESUMO, que diz o recorte.
  A lição, e ela é sobre o MEU processo: **quando o cliente reprova uma tela, a correção
  começa pelas três perguntas da regra de leiaute, não pelo tamanho das peças.** Encolher
  cartão, compactar barra e mover caixinha são refinamentos DA resposta errada quando a
  pergunta estrutural ("isto é uma tela ou duas?") ainda não foi feita — e foi preciso o
  cliente apontar a aba pelo nome para ela ser feita.

- **Quando o texto APROVADO chega, o botão do rascunho vira SUBSTITUIÇÃO — a recusa "já
  existe" viraria beco sem saída no exato momento em que o texto certo chegou** (parcela
  84 — o advogado da cliente mandou os dois termos do BSV por escrito, e ela pediu para o
  conteúdo entrar no lugar do rascunho da parcela 67). `ModelosTermoBsv` agora carrega o
  texto dela: o **TCLE** (assinado UMA vez, fica na ficha — `SoValeNoDiaDoProcedimento:
  false`) e o **Termo da sessão** (assinado a CADA sessão: "fui reavaliado NESTA data"
  não se herda — `: true`). A marca "(rascunho — revisar)" morreu COM o rascunho:
  mantê-la num texto aprovado mandaria o responsável técnico revisar o que ele já
  assinou embaixo. E a guarda "o BSV já exige termo" — certa quando o clique repetido só
  podia ser engano — deixaria a clínica SEM caminho para adotar o texto novo: virou
  `ConfirmarPerigo` que lista o que está amarrado, DESLIGA (`AlternarAsync`, nunca
  apaga — os termos assinados guardam o texto lido, porque aplicar COPIA) e amarra o par
  novo; só as exigências ATIVAS entram na pergunta, porque `ExigenciasAsync` devolve
  também as desligadas e desligada não está "em uso".
  A transcrição papel→sistema é adaptação de FORMA, não de conteúdo, e as regras: os
  blocos de IDENTIFICAÇÃO/ASSINATURAS com linhas em branco saem (a folha emitida imprime
  isso e a assinatura é colhida na tela); **lacuna impressa ("____", Diagnóstico, CID)
  vira remissão ao prontuário** — modelo é texto FIXO copiado na emissão, e lacuna
  sairia em branco para sempre; as listas de "☐" viram as declarações Sim/Não (que é o
  que elas são no papel), mantidas AFIRMATIVAS e incondicionais mesmo vindas do
  advogado — a redação das declarações é responsabilidade nossa de FORMA, porque o "Não"
  aceso no balcão precisa continuar significando problema; e **seção que é ato da EQUIPE
  (avaliação médica, dados da sessão, intercorrências, alta) não entra no papel que o
  PACIENTE assina** — cada uma já tem registro próprio com autoria e trilha, e o corpo
  do termo diz onde. O teste que fixava o rascunho virou o que fixa as ÂNCORAS do texto
  aprovado (o jejum "6 (seis) horas", os imunobiológicos da seção 7, a remissão ao
  TCLE) — para uma edição futura no código não devolver o texto genérico em silêncio.

- **Os três da fila da parcela 69 que não dependiam de ninguém: espera de falta, bloqueio
  invadido e a coluna do desativado** (parcela 85). Nenhum quebrava nada; cada um tem uma
  regra que generaliza:
  (a) **Espera ABERTA só corre para quem ainda está na FILA.** `EsperaMinutos` completava
  com `agora` sempre que faltava a chamada — então quem chegou e virou FALTA "esperava"
  até hoje, e a média do painel ia a milhares de minutos num dia passado. A regra que
  ficou no domínio: falta e cancelamento não CONCLUEM a espera, DESFAZEM a medida
  (ninguém carimbou quando a pessoa desistiu — medir até `agora` é ficção), e espera
  aberta não atravessa o dia da chegada. A já MEDIDA (chegada → chamada) sobrevive ao
  desfecho: o fato aconteceu. **Métrica com relógio em aberto precisa dizer QUANDO o
  relógio para de valer** — e "quando o caso sai da fila" é quase sempre a resposta.
  (b) **A folga da BUSCA tem de cobrir o que o filtro fino decide.** `MarcadosDentroAsync`
  já usava `ColideCom` (sobreposição correta) — mas a consulta do repositório filtrava
  por `DataHora >= inicio`, então a sessão das 13h30 que invade o bloqueio das 14h nunca
  CHEGAVA ao `ColideCom`. Filtro fino certo depois de busca grossa errada é o filtro
  certo rodando sobre a lista errada — e não aparece em teste nenhum que só marque
  sessões DENTRO do período. A folga é a meia-noite do dia (sessão não atravessa o dia).
  (c) **Coluna por recurso ATIVO esconde o horário do recurso DESATIVADO.** A grade do
  dia montava colunas só para profissionais/salas ativos; o horário de quem foi
  desativado sumia com o cabeçalho contando-o, o vão ficava clicável e a recepção marcava
  por cima. O dono inativo com horário ganha coluna própria "(inativo)" — nunca cai em
  "Sem profissional", porque o horário TEM dono e atribuí-lo a ninguém esconderia quem
  precisa ser remarcado. As caixas "Ativo (aparece na agenda)" da tela de equipe
  prometiam o contrário do que acontecia e foram reescritas: **quando um sinalizador
  ganha efeito novo, o texto da caixinha que o liga entra no mesmo commit.** E o resumo
  passou a contar COLUNAS, não salas ativas — número do cabeçalho que não bate com o
  desenho da grade foi exatamente o que denunciou o defeito.
- **A VALIDAÇÃO COMPLETA DA RECEPÇÃO — "tudo funcionando perfeitamente" pedido pela
  direção, e o que três auditorias adversariais acharam com 1670 testes verdes**
  (parcela 86 — numerada após as do prontuário clínico, 71-85, trabalhadas em paralelo). As três redes locais passaram ANTES da rodada; nenhum dos achados
  quebrava build ou teste — a assinatura de sempre. Os que ensinam regra nova:
  **String na posição de string: a troca de parâmetro que nenhuma rede vê.** O
  "Excluir" da aba Prontuário da FICHA chamava `CancelarAsync(id, Operador)` — o
  operador caía no parâmetro `motivo`, o motivo obrigatório da parcela 52 virava o
  login (nunca vazio, então o serviço não recusava) e `CanceladaPor` ficava nulo: a
  trilha respondia "?" para "quem cancelou este registro clínico". A tela irmã
  (Prontuário) fazia certo, com `PerguntarTexto`. É o `CS1503` da família que o
  compilar-sombra não alcança — string encaixa em string. **Ao chamar método com dois
  parâmetros do mesmo tipo, escreva os nomes** (`motivo:`, `operador:`); e quando duas
  telas fazem o mesmo ato, o teste que falta continua sendo o que compara as duas.
  **O backfill que só é seguro na PRIMEIRA vez.** Religar a chave `GuiaNoAgendamento`
  repetia o backfill de `RealizadoEm` sobre "todo atendimento sem carimbo" — e depois
  da primeira ativação existe atendimento SEM carimbo de propósito: a sessão marcada
  (e a cancelada). Desligar e religar — o ritual que a própria tela ensina — carimbava
  como visita sessão que nunca houve, corrompendo retenção/origem/estreia sem sintoma.
  O filtro exclui atendimento pendurado em agendamento não-`Realizado`. **Premissa de
  migração escrita como "neste momento não existe X" expira no momento em que X passa a
  existir — releia-a em toda operação que pode rodar DUAS vezes.**
  **Confirmar presença de horário CANCELADO produzia sessão Realizada com todas as
  guias suspensas.** A corrida é real (duas máquinas, fila que relê a cada minuto), a
  recusa faltava no SERVIÇO — as telas só guardam a metade visível. Agora recusa
  mandando reabrir pelo Remarcar, que é quem devolve as guias.
  **A especialidade da consulta é parte da GUIA, e só a modalidade disparava a
  regeneração.** Trocar "Consulta/Psiquiatria" por "Consulta/Geriatria" mantém o código
  "Consulta": o horário gravava a nova e a guia seguia com a antiga. `regerarGuias`
  agora olha as duas; e a SÉRIE ganhou o `primeiroCodigo` que o ramo único já passava
  (campo que a tela oferece e o serviço descarta, de novo).
  **O aviso de guia descartado nas portas irmãs.** `CancelarAsync`/`MarcarFaltaAsync`
  devolvem os avisos do regime novo — inclusive "guia JÁ BAIXADA no portal" — e a FILA
  os jogava fora (a Agenda os punha em snackbar de 4s; a cópia do faturamento também
  descartava). Aviso que exige ação vai em DIÁLOGO nas três portas. E o formulário de
  agendamento do FATURAMENTO ganhou a pergunta de duplicidade informada pela capa — era
  a única porta de criação sem ela.
  **A mensagem escrita antes do recarregar, pela terceira vez** (Retorno e
  Confirmações — a rodada dizia "N sem consentimento (LGPD)" e a carga zerava a frase
  antes de ela renderizar); **o catch da batida silenciosa que pinta a tela**, pela
  segunda (as sub-cargas do Painel limpavam as pendências do dia numa engasgada do
  banco de 2 em 2 min); **barreiras**: enviar documento clínico sem `Exigir` na cópia
  da Recepção (a 68 corrigiu só o Consultório), o Emitir do orçamento exigindo o bit de
  LEITURA (`VerFinanceiro`) onde as portas exigem escrita, o WhatsApp de confirmação
  exigindo `EditarAgenda` num ato de leitura (as duas metades discordavam), e a guarda
  do "marcar" decidindo pela chave em CACHE da tela (na dúvida, a guarda exige MAIS).
  **Menores pagos**: "Excluir"→"Cancelar…" nos dois botões de evolução; N+1 de anexos
  na ficha (o método de uma consulta existia desde a 37 e a ficha não o usava); falta
  fora da espera média; o menu "⋯" da Fila escondendo falta/cancelamento de quem a
  guarda autoriza; capa do dia dizendo "particular" para sessão cancelada.
  **Ficou documentado sem correção (decisão ou desenho)**: itens 1, 2 e 7 da fila da
  69 (decisões de cliente/leiaute); o formulário da agenda sem reconferir elegibilidade
  na troca de data (item 6, metade); a observação do faturista sobrescrita pela
  suspensão; a NC de sessão cancelada que ressuscita na volta do paciente; a
  `Categoria` do paciente atualizada na marcação sem reversão no cancelamento; os dois
  commits do `ListaEsperaService.ChamarAsync`; férias do profissional invisíveis na
  visão de SEMANA da Recepção.

- **O KANBAN NOVO DA FILA — o redesenho que a cliente pediu comparando com o Smart
  Clinic** (parcela 87; mockup aprovado em canvas antes de uma linha de WPF — a lição
  das seis reprovações aplicada de véspera). "A fila/agenda em kanban do smart clinic é
  muito melhor que a nossa, tanto visualmente quanto funcional" — e o inventário deu
  razão com fatos: a raia tinha a MESMA cor do fundo da tela (o quadro não se lia como
  quadro), o nome do paciente tinha o mesmo corpo da legenda (12px — hierarquia só de
  peso), e o cartão calava atraso, confirmação, convênio, término e pacote — tudo dado
  que o banco JÁ TINHA.
  As decisões:
  (a) **Cores de estado moram no CABEÇALHO da raia, não no cartão** — `CabecalhoRaia`
  ganhou `Ponto`/`PilulaFundo`/`PilulaTexto` (nulos = o visual de sempre; os padrões da
  pílula moram no ESTILO, porque o C# não alcança StaticResource). Cartão continua
  branco em toda coluna: cor por estado no cartão brigaria com os DOIS estados que já
  pintam ele inteiro (chamada demorada, atraso).
  (b) **`Brush.Raia` é token** (Cinza.100) — raia um degrau acima do fundo é o que
  devolve o quadro ao olho, e vale para os dois quadros pelo mesmo token.
  (c) **O atraso mora no DOMÍNIO** (`Agendamento.AtrasoMinutos`): só Agendado, sem
  check-in, hora estourada — quem chegou está ESPERANDO, não atrasado (os dois selos
  juntos se contradiriam), e dois quadros com duas contas de "atrasado" divergiriam.
  (d) **O selo de confirmação só afirma o que a rodada afirmou**: "Confirmou" =
  Respondido; "Não confirmou" = Enviado sem resposta; sem contato = SEM selo — acusar
  de "não confirmou" quem nunca foi avisado seria o selo mentindo. E só na coluna
  AGUARDANDO: depois do check-in é ruído. A leitura é em lote
  (`ConfirmacoesDosAgendamentosAsync`), uma consulta para o dia.
  (e) **A espera média virou definição ÚNICA** (`PainelRecepcaoService.EsperaMediaMinutos`,
  estático) — o painel e o resumo da fila leem a mesma conta; nula sem base, nunca zero.
  (f) **A faixa CHAMANDO nomeia o MAIS ANTIGO e carrega o "Entrou" DENTRO dela**, pelo
  MESMO comando (e a mesma guarda) do botão do cartão — é para a faixa que a
  recepcionista está olhando quando o paciente levanta; os demais chamados viram uma
  linha ("Também: …").
  (g) **O filtro por profissional remonta da memória** (`MontarQuadro` separado da
  carga; entre o Clear e o último Add não há await) e só aparece com DOIS ou mais
  profissionais no dia — chip único é ruído. O filtro vigente sobrevive à recarga só
  enquanto o profissional continua no dia.
  (h) **Paridade entre os dois quadros é regra, não coincidência**: Meu dia ganhou as
  mesmas cores, a mesma hierarquia (nome grande, hora–fim como contexto), e as duas
  divergências históricas caíram (FINALIZADOS→FINALIZADO; encaixe Badge.Info→Aviso) —
  o mesmo fato com duas cores nos dois quadros se lia como dois fatos.
  O que ficou POR DECISÃO da clínica (as notas do canvas): cancelado/falta dentro do
  quadro (o Meu dia já mostra; a fila segue fora), situação de pagamento no cartão, e
  o clique no cartão abrindo a ficha.

- **A ENFERMAGEM NÃO TEM AGENDA PRÓPRIA — e cadastrá-la CERTO esvaziava as cinco telas
  dela** (parcela 88 — a cliente: *"os enfermeiros podem ver todos os pacientes e clicar em
  atender, em vez de ver só os pacientes dele"*; o mapa completo está em
  `docs/atendimento-medico-e-enfermagem.md` §13, e é lá que se atualiza). Três fatos
  verdadeiros, e o defeito era a SOMA deles: (1) as cinco listas do Consultório filtram por
  `SessaoUsuario.Atual.ProfissionalId`; (2) a enfermeira PRECISA de um `Profissional`
  vinculado, porque é dele que sai o COREN copiado em cada registro (parcela 72) e
  `IdentificacaoExecutante.Exigir` recusa sem ele; (3) os horários pertencem a quem
  CONSULTA — ela passa por todos eles.
  ⚠️ **Nada falhava.** Build verde, 1908 testes verdes, três redes verdes — e o dia, a
  semana, a carteira, a dívida de prontuário e os números dela abriam VAZIOS, justamente
  por ela estar cadastrada certo. Tela vazia se lê como sistema quebrado, não como "esta
  lista não é para você".
  A decisão mora no **DOMÍNIO** (`PerfisAcesso.EscreveComoEnfermagem` /
  `ProfissionalDaListaDoPosto` / `MotivoDaListaDoPosto`), e `PostoClinico` é só o adaptador
  que lê a sessão: projeto WPF não compila no `dotnet test`, e regra que o teste não alcança
  apodrece sem ninguém notar.
  ⚠️ **Pelos BITS, nunca pelo texto do conselho.** `RegistroConselho` é campo livre
  ("COREN-SP 999999", "Coren SP 12345", "coren/sp 12345"): procurar "COREN" nele erraria no
  dia em que alguém digitasse diferente, e erraria em SILÊNCIO. O corte é o X · Y da parcela
  72 — escreve por `ChecarPrescricao | RegistrarEvolucaoEnfermagem` e **não** por
  `EditarProntuario | Prescrever`. E quem tem OS DOIS lados (o Gerente, que recebe `Todas`)
  responde **false**: ele TEM agenda própria, e devolver-lhe a clínica inteira esconderia
  justamente os pacientes dele. A regra é "escreve **SÓ** por Y".
  ⚠️ **A FRASE vale tanto quanto o filtro.** São DOIS motivos para a lista ser a da clínica,
  e a mensagem não pode ser uma só: *"peça à direção para ligar o seu usuário ao seu
  cadastro"* é verdade para quem não tem vínculo e MENTIRA para a enfermeira, que está
  vinculada. Instrução errada com cara de instrução certa manda o suporte procurar um
  defeito que não existe — é a irmã de "falha exibida como sucesso", e ela sobrevivia
  escrita à mão dentro da guarda do "Chamar próximo" depois de o texto da tela já ter sido
  corrigido. **Frase de guarda sai do PONTO ÚNICO.**

- **"Atender" é uma palavra só, e precisa levar à seção de escrita de QUEM clicou**
  (parcela 88). `PostoClinico.ChaveDoAtendimento()` bifurca: quem consulta cai no S-O-A-P,
  quem executa cai na passagem de enfermagem — as duas na MESMA tela do paciente (mesmo
  crachá, mesmo rail, mesmas seções de leitura), mudando só a seção que abre. Sem a
  bifurcação a técnica cairia no formulário do médico, onde o `Salvar` segue
  `PodeEditarProntuario` e ela não o tem: o **botão que não faz nada** da parcela 41, com
  uma tela inteira em volta.
  A seção nova (`Atendimento de enfermagem`, posição 1 do rail) repete o desenho do
  Atendimento do médico DE PROPÓSITO — escrever à esquerda, reler à direita, rodapé
  ancorado fora do scroll —, e a coluna da direita é a **imagem espelhada**: o médico relê a
  enfermagem e a infusão; a enfermagem relê a sessão médica e a infusão.
  ⚠️ **O compositor NÃO foi reescrito**: a seção declara `DataContext="{Binding Passagem}"`
  sobre o MESMO `EvolucaoEnfermagemViewModel` da janela da sala. As regras caras (hora
  informada, hora futura recusada, retificação que preserva a data do fato, alergia no mesmo
  `SaveChanges`) não podem existir em duas cópias.
  ⚠️ **E a janela CONTINUA** — ela não é dívida, é a porta que a seção não alcança: a folha
  de execução é MODAL e `PodeMexer => PodeChecar && EmExecucao`, então folha encerrada apaga
  o painel inteiro, e é ali que se registra a reação que aparece meia hora depois da última
  bomba. **A seção é a quinta porta, não a substituta.**
  ⚠️ **O caminho de volta é parte da feature.** A tela da Enfermagem é do SHELL e é publicada
  por DOIS módulos: no `Clinica.Recepcao.exe` o módulo Clínico não está carregado, e
  `NavegacaoSuite.Ir` devolveria `false` EM SILÊNCIO. Pergunta-se antes com `Existe`, e o
  painel da própria tela atende onde não há posto clínico.

- **A cópia campo a campo do serviço engoliu o "se necessário" por doze parcelas** (parcela
  88). `EvolucaoEnfermagemService.AplicarProcesso` copia o cuidado campo a campo — o **lugar
  3** da lista de conferência — e `SeNecessario` ficou de fora desde que o campo nasceu
  (parcela 76). A caixinha da tela gravava sempre `false`.
  ⚠️ **E o estrago não aparecia como falha, e sim como CONTAGEM errada**: `CuidadoDoDia.
  Pendente` é `!SeNecessario && Vigentes.Count == 0`, então TODO cuidado condicional ("se
  dor > 5") ficava eternamente aguardando registro e o contador da sala passava a apontar
  para nada — exatamente o que o comentário do próprio campo diz existir para impedir. A
  retificação sofria do mesmo (mesmo `AplicarProcesso`), então corrigir uma vírgula
  desligava o SOS de todo cuidado condicional.
  Os dois testes novos **falham no código anterior** — foi verificado, não presumido.

- **Dado gravado sem leitor, na variante que faz o COMENTÁRIO mentir** (parcela 88):
  `EvolucaoEnfermagem.AgendamentoId` era gravado desde a parcela 71, preservado na
  retificação, e **nenhuma consulta, tela ou papel o lia**. A seção nova ia escrever
  *"a passagem fica registrada nesta sessão"* — promessa que o código não cumpre, o defeito
  da parcela 67. Ganhou o primeiro leitor: o selo **DESTA SESSÃO** na lista de passagens,
  que responde a pergunta de quem está com o paciente na frente ("já registrei alguma coisa
  NESTA passagem, ou o que estou vendo é de outro dia?").
  A regra que fica: **antes de escrever um comentário que promete um vínculo, procure quem
  o LÊ.** Se ninguém lê, ou se constrói o leitor, ou a frase muda.

- **A MESMA lacuna do lado de quem CONSULTA — e a lista da clínica é o segundo clique, não
  o padrão** (parcela 88, 2ª rodada; a cliente corrigiu: *"a tarefa acima se refere a
  enfermeiros e médicos também, eu mencionei somente enfermeiros"*).
  `MeusPacientesAsync(profissionalId)` devolve só quem ELE já atendeu, então o paciente de
  **primeira consulta**, o do **colega** que ele cobre e o que o **balcão acabou de
  cadastrar** eram INALCANÇÁVEIS do Consultório — não havia segunda porta, e a busca da
  tela filtra em memória o que já veio. "Meus pacientes" ganhou os mesmos dois chips
  exclusivos da tela da Enfermagem.
  ⚠️ **A carteira dele continua sendo o PADRÃO**: "quem eu acompanho" é a pergunta que a
  tela responde todo dia, e trocá-la pela clínica inteira afogaria os pacientes dele no
  cadastro. O pedido era o segundo clique, não a troca do primeiro.
  ⚠️ **Busca sobre lista CORTADA tem de ir ao SQL.** No modo "todos" a lista vem com teto, e
  filtrar em memória o que veio cortado faz a busca responder *"não existe"* para todo
  paciente além dele. É a resposta errada mais cara que uma busca de paciente pode dar,
  porque leva a **cadastrar a pessoa de novo** — o CPF duplicado da parcela 57 pela porta
  de trás. O termo desceu para `MeusPacientesAsync(..., termo:)` (que casa nome OU CPF),
  com o agrupamento de teclas do `SeletorPacienteViewModel`, e o **teto é DITO** na frase
  do resumo: lista cortada que se anuncia como "todos os pacientes" é corte silencioso.
  ⚠️ **Filtro de memória sobre resultado de servidor DERRUBA o que o servidor casou a mais.**
  O SQL casa nome OU documento; o filtro em memória só conhece o nome. Refiltrar depois
  faria a pessoa achar o paciente pelo CPF e vê-lo SUMIR da lista.
  ⚠️ **Chip que não muda nada é pior que chip nenhum**: para quem não tem carteira própria
  (sem vínculo, ou a enfermagem) os dois modos mostram a MESMA lista, então eles SOMEM e
  quem explica é a linha de motivo. *(Os dois chips morreram na 3ª rodada, logo abaixo — a
  lição fica porque ela é geral.)*
  ⚠️ **E a porta nova mudou o que a porta VELHA precisava fazer.** Abrir pela carteira
  fixava só id e nome — aceitável enquanto a lista era só dele, porque o caminho normal é
  "Meu dia", que já traz o agendamento. Com a lista alcançando a clínica, ela virou o
  caminho de quem cobre o colega, e sem o vínculo a evolução nasce SOLTA: a sessão fica em
  "Sessões sem evolução" para sempre, mesmo depois de escrita.
  `EntregaDoPaciente.AoPostoAsync` (shell) virou o ponto único que a Enfermagem e a carteira
  compartilham — e as TRÊS portas da linha (Atender, Dor, Avaliações) passam por ele,
  porque as três caem na mesma tela e o rail troca de seção sem trocar de paciente: se só
  uma amarrasse, bastaria entrar por outra para gravar solto.
  A lição que generaliza: **ao ALARGAR o alcance de uma lista, releia o que o clique dela
  faz.** O que era suficiente para um recorte estreito costuma deixar de ser quando o
  recorte cresce — e o que falha não é a porta nova, é a velha.


- **"MEU PACIENTE" NÃO EXISTE — e a resposta certa não era um segundo clique, era apagar o
  recorte** (parcela 88, 3ª rodada; a clínica: *"não existe 'meu paciente', todos atendem
  todos"*). A rodada anterior tinha resolvido o alcance da lista do Consultório com **dois
  chips** (*Meus pacientes* × *Todos os pacientes*), a carteira como padrão. A frase da
  direção derrubou a premissa dos dois: **oferecer a escolha entre "meus" e "todos" numa
  clínica que não distingue as duas coisas é inventar uma decisão** — e a que abre por
  padrão seria justamente a lista MAIS ESTREITA, escondendo o paciente do colega de quem
  foi chamado para cobri-lo.
  O recorte saiu do repositório, não da chamada: `PacientesDoProfissionalAsync(id, …)` →
  `PacientesAtendidosAsync(limite)`, `ConsultorioService.MeusPacientesAsync(profissionalId,
  …)` → `PacientesAsync(termo, limite, comDor)`, record `PacienteDoProfissional` →
  `PacienteDaCarteira`. **Passar `null` no lugar do id não teria bastado**: aquele caminho
  fabricava `Sessoes = 0` e `UltimaSessao = null` para todo mundo, esvaziando as duas
  colunas que são o assunto da tela.
  ⚠️ **A pergunta que decide o que continua recortado não é conceitual, é sobre a natureza
  do dado: PACIENTE não tem dono; HORÁRIO tem.** Por isso "Meu dia", "Minha semana", "Meus
  números" e "Sessões sem evolução" seguem filtrados por profissional — a agenda é fato de
  marcação, e `PodeChamarProximo` depende disso (na lista da clínica inteira o primeiro da
  fila pode ser de outro profissional, e o clique cego anunciaria um nome para a sala do
  colega). A AUTORIA também não se perdeu: quem atendeu está no agendamento, quem escreveu
  assina a evolução, e "Meus números" continua medindo o trabalho de cada um.
  ⚠️ **Sem termo, a lista é quem a clínica JÁ ATENDEU — e isso não é o recorte de volta.**
  É a leitura que a tela existe para dar (sessões, última visita, queda da dor); o cadastro
  inteiro, quem nunca veio inclusive, chega pela BUSCA. E a busca precisou de
  `SessoesDosPacientesAsync` (uma consulta em lote, o MESMO critério do agrupamento da
  lista): sem ela, um paciente de vinte sessões apareceria como "sem sessão registrada" só
  por ter sido achado pela busca — **dado exibido como ausente é a irmã de falha exibida
  como sucesso**.
  ⚠️ **Ao apagar um recorte, releia os RÓTULOS que o nomeavam.** "Meus pacientes" virou
  "Pacientes" no menu e na tela — e isso colidiu com a aba "Todos" da Recepção, deixando
  duas abas irmãs quase homônimas. Elas passaram a dizer a PERGUNTA de cada uma
  ("Cadastro" × "Em tratamento"), que é a diferença que sempre existiu e que o rótulo antigo
  escondia atrás do dono. **A CHAVE não muda junto** (`consultorio-pacientes`): chave é
  contrato de navegação entre módulos, e renomeá-la para acompanhar um rótulo é a regressão
  da parcela 37, 4ª rodada.
  ⚠️ **E o vazio ganhou DUAS perguntas novas no mesmo movimento.** Enquanto a busca filtrava
  a tela, "não há ninguém aqui" bastava; quando ela passou a alcançar o cadastro inteiro,
  responder *"entra aqui quem já foi atendido"* a quem digitou um nome faz concluir que o
  paciente EXISTE e só não foi atendido — quando o sistema está dizendo que ele **não está
  cadastrado**. E é essa leitura errada que leva a cadastrar quem já tem ficha (o CPF
  duplicado da parcela 57). A terceira apareceu na releitura: **quem ESCONDEU responde
  primeiro** — com o "só quem sumiu" marcado sobre uma busca que TROUXE gente, "não achei
  ninguém no cadastro" mentiria sobre a causa e mandaria cadastrar de novo alguém que a tela
  acabou de achar. Um estado vazio por pergunta, como sempre.
  ⚠️ E as três frases foram para a **Application** (`ResumoDaCarteira.Montar`), não para a
  ViewModel: é a regra da `GradeSemana` (parcela 69) e do `ResumoSessaoAnterior` (77) — **o
  que decide o que a tela AFIRMA precisa morar onde o `dotnet test` alcança**. Foi só ao
  escrever o teste que apareceu o exagero da frase do recorte, que afirmava *"vieram nos
  últimos 45 dias"* sobre uma lista que pode conter quem **nunca veio** — a busca alcança a
  primeira consulta, e essa pessoa também não é "sumida". Afirmar uma sessão que não houve,
  numa frase de tela, é a mesma família de garantia aparente que este projeto recusa.


- **O CATÁLOGO ERA UMA CAIXINHA DE 240 px, e o que não cabia nele era o que ele sabe**
  (parcela 88, 4ª rodada — a clínica mandou o print da aba de Cuidados: *"preciso de um pop
  out para que a enfermeira consiga maximizar e ler tudo e todas as opções"*). Os dois
  catálogos da consulta de enfermagem (diagnósticos e cuidados) eram colunas de 240 px com
  teto de 180, **permanentes ao lado de uma lista que nasce VAZIA** — a aba abria com um
  quarto da largura gasto num atalho e o resto em branco, mostrando três itens por vez com o
  título quebrado em três linhas ao lado de um `+`.
  A regra de leiaute do projeto decide sozinha: **escolher do catálogo é o que a enfermeira
  faz uma ou duas vezes por consulta; a prescrição é o que ela VÊ o tempo todo** — botão e
  janela para o primeiro, a tela para o segundo (parcela 37, 3ª rodada).
  ⚠️ **Mas o pior não era o tamanho, era o que não cabia**: o catálogo guarda a FREQUÊNCIA
  SUGERIDA de cada cuidado e o RESULTADO ESPERADO de cada diagnóstico, e **nada disso
  aparecia**. É o defeito recorrente do projeto na variante mais discreta de todas — não é
  dado sem leitor nem capacidade sem porta, é **porta pequena demais para dizer o que ela
  sabe**. Ao encolher um painel para caber ao lado de outra coisa, pergunte o que o dado
  tem que a caixa não mostra.
  ⚠️ **"Já no plano" tem de ser VISÍVEL.** `AdicionarCuidado` recusa o repetido em silêncio,
  e na caixinha o segundo clique simplesmente não fazia nada (o defeito da parcela 41). Na
  janela o botão SOME e no lugar dele fica a marca; e a lista **não é remontada** depois de
  acrescentar (a rolagem voltaria ao topo a cada escolha), só o estado de cada linha é
  corrigido no lugar — todas, porque um diagnóstico traz os cuidados dele junto.
  ⚠️ **UMA janela para os DOIS catálogos**, com o `CatalogoDeEnfermagem` chegando por
  parâmetro: duas seriam duas definições de "escolher do catálogo". E ela recebe o MESMO
  objeto da tela de trás (a regra da parcela 49), então **não tem "Salvar"** — o rodapé diz
  isso em vez de deixar a pessoa supor.
- **O compositor COFEN existia DUAS vezes, e esta seria a primeira divergência** (parcela 88,
  4ª rodada). O bloco das cinco etapas tinha ~300 linhas na janela da sala de infusão e ~300
  na seção do Consultório, que eu escrevi espelhando a primeira na 1ª rodada desta parcela.
  Medido antes de mexer: **47 linhas de diferença em 300, todas cosméticas** (comentários
  requebrados, uma margem, uma largura). O catálogo em janela cairia numa das duas cópias, a
  outra ficaria com a caixinha antiga, e **nada falharia** — build, testes e as três redes
  verdes, com a enfermeira descobrindo pelo app que ela usa.
  Virou `ProcessoDeEnfermagemView`, no shell, pela regra do mapa corporal (parcela 36):
  **quando duas telas precisam do MESMO bloco, ele sobe INTEIRO e não se reescreve.** Sem
  ViewModel próprio — o `DataContext` é o de quem o hospeda —, senão as regras caras (hora
  informada, hora futura recusada, alergia no mesmo `SaveChanges`) existiriam em duas cópias.
  A lição de método: **duplicação que ninguém pagou só cobra na primeira correção de
  verdade** — e a hora de pagá-la é justamente essa, e não "depois", porque depois quer dizer
  com uma das duas já divergente.
\n

- **Tag de tipo da casa SEM PREFIXO: a terceira variante da família, e a que as duas
  checagens não viam** (parcela 88, 4ª rodada — o CI reprovou o PR). Ao extrair o compositor
  da consulta de enfermagem para o shell, escrevi `<ProcessoDeEnfermagemView … />` dentro de
  uma janela que só declarava o prefixo de `Clinica.Desktop.Controls`. Sem prefixo, o WPF
  procura a tag no namespace PADRÃO — o dele — e recusa com **MC3074**.
  As checagens 33 e 33-B cobrem o `xmlns` que EXISTE e está errado (o `;assembly=` que sobra
  e o que falta). Aqui ele **não foi declarado**, e nenhuma das duas olha a TAG.
  ⚠️ Nenhuma rede local pegava, pela razão de sempre nesta família: o XML é bem-formado, o
  `compilar-sombra` **não lê o corpo** do XAML e o C# compila.
  Virou a **checagem 42**, e o critério é estreito de propósito: só reclama de tag sem
  prefixo cujo nome é um tipo que algum `.cs` do repositório DECLARA — tipo do WPF (`Grid`,
  `TabControl`) não está nessa lista e passa. **Medido antes de ligar: ZERO ocorrências em
  todo o repositório**, então ela nasce sem uma linha de ruído. Autotestada contra o caso
  real (verificado revertendo a correção: ela acusa a linha exata) e contra os três
  legítimos — com prefixo, tipo do WPF e a tag citada dentro de um COMENTÁRIO.
  A lição que generaliza, e é a terceira vez que esta família a cobra: **ao escrever uma
  rede para um erro, liste as FORMAS que ele pode tomar, não só a que mordeu.** O `xmlns`
  errado e a tag sem prefixo produzem o MESMO `MC3074`, e eu tinha duas checagens para o
  primeiro e nenhuma para o segundo.
\n

- **"O pop out é da CONSULTA, não do catálogo" — o pedido que eu li um degrau abaixo**
  (parcela 88, 5ª rodada; a clínica: *"ao clicar em «Consulta de enfermagem» ainda não abre
  um pop out como janela, que foi o solicitado"*). O print da rodada anterior mostrava a aba
  de Cuidados, e eu tratei "essa tela" como o CATÁLOGO — que é um degrau abaixo do que ela
  precisa maximizar.
  E o processo nunca coube onde estava: as cinco etapas ficavam empilhadas dentro do
  compositor da passagem, disputando altura com a hora, o texto, os sinais vitais e a linha
  do tempo, numa janela de altura FIXA. Foi essa disputa que a parcela 79 corrigiu tirando o
  `Height="320"` — **o remédio certo para o sintoma errado**. A pergunta que eu não fiz lá:
  *isto cabe nesta tela, ou é outra tela?*
  ⚠️ **`Click`, e NÃO `Checked`, na caixinha que abre janela.** `Checked` dispara também
  quando o ViewModel muda a propriedade por código — e ele muda, ao carregar um registro
  para CORRIGIR: a janela abriria sozinha no meio de uma carga. `Click` só existe quando foi
  a PESSOA que clicou.
  ⚠️ **Ao tirar algo da tela, releia o que o gesto vizinho passou a custar EM SILÊNCIO.**
  Desmarcar "Consulta de enfermagem" sempre descartou as cinco etapas na gravação
  (`ColherProcesso` devolve nulo no modo anotação) — e enquanto as abas estavam na tela, elas
  sumiam na frente da pessoa. Com a consulta em janela, nada muda visualmente e o registro
  vai para o prontuário sem elas. Daí a confirmação, e ela só aparece quando **há o que
  perder**: cobrar confirmação sobre uma consulta em branco treinaria a equipe a confirmar
  sem ler (a causa raiz do incidente da parcela 65).
  ⚠️ **Confirmação em callback de propriedade precisa de guarda contra a carga por CÓDIGO.**
  São três caminhos programáticos aqui (a reversão da própria confirmação, a limpeza depois
  de gravar e o `Corrigir`), e sem o flag a pergunta apareceria no meio de uma carga que
  ninguém pediu — pergunta que aparece sem gesto é pergunta que se fecha sem ler.
  E o **caminho de volta é parte da feature**: fechar a janela deixaria a consulta
  inalcançável sem desmarcar e marcar de novo — e desmarcar é justamente o gesto que devolve
  o registro para anotação. Daí o botão "Abrir a consulta…", e o aviso `EtapasEmFalta`
  ficando na TELA, que é onde ela o lê antes de clicar em Registrar.
- **Checagem que não enxerga dentro de controle da casa acusa o que está certo** (parcela
  88, 5ª rodada). A checagem de rolagem (15/16) varria só `raiz.iter()` do XAML da janela, e
  uma janela cujo miolo é `<comp:ProcessoDeEnfermagemView />` — com um `ScrollViewer` por aba
  lá dentro — era acusada de não ter rolagem nenhuma. **Checagem que reclama do que está
  certo é checagem que alguém desliga**, e aí ela para de pegar o defeito de verdade.
  Ela passou a seguir **UM nível** de composição: o tipo da tag é casado com o `x:Class` dos
  XAML do repositório, e a árvore dele entra na conta. Um nível basta para a composição deste
  projeto; mais níveis dariam um grafo para percorrer sem ganho medido.
  ⚠️ **Alargar uma checagem é o gesto que a deixa cega, então ela ganhou autoteste nos DOIS
  sentidos** — controle da casa COM rolagem conta, controle SEM rolagem não conta —, e foi
  verificado que o autoteste REPROVA quando a varredura é "simplificada" de volta.


- **PROVA DE CAMPO — o que deixou de ser suposição em ago/2026.** Três coisas que este
  arquivo descrevia como "sem prova" passaram a rodar na clínica, e a lista existe para
  ninguém voltar a hedgeá-las:
  **(a) O banco saiu da Neon e está numa VPS da Locaweb, com datacenter no BRASIL**
  (`docs/banco-na-vps.md`, que deixou de ser plano). A consequência que mais pesa não é
  técnica: o ponto 10 do compromisso de conformidade — **transferência internacional do
  art. 33** — deixou de existir, porque o dado passou a residir no país. O suboperador
  agora é um fornecedor nacional, e o mTLS acrescenta uma fechadura que a Neon não
  oferecia (cada máquina cliente prova quem é, com certificado da própria clínica).
  **(b) A assinatura ICP-Brasil foi testada com e-CPF REAL pelo SafeID.** Era a maior
  incógnita do projeto: o `docs/safeid-congelado.md` listava "e-CPF real",
  "`exigirCadeiaConfiavel` com cadeia ICP-Brasil de verdade" e "publicação no S3 com
  documento real" como nunca exercitados, e as rodadas 3 a 8 da parcela 67 corrigiram o
  caminho de nuvem **às cegas**, uma mensagem de erro por vez. Continuam sem prova o
  **carimbo do tempo no caminho de nuvem** (a ACT configurada não é aplicada, e a
  configuração é ignorada em silêncio) e o **LTV/PAdES-LT**, que nunca existiu.
  **(c) Os Workers do Cloudflare estão publicados e funcionando** — o do validador do QR
  (parcela 68, 7ª rodada) e o da coleta remota do termo (parcela 81), que hoje são um
  arquivo só (`tools/worker-clinica.js`, parcela 82).
  ⚠️ **A lição de método é sobre o que fazer com isto, não sobre o que foi provado.** Este
  projeto documenta com cuidado o que ainda não rodou, e isso é certo — mas afirmação de
  "sem prova" tem prazo, como o motivo de uma exclusão de rede (parcela 51). **Quando um
  caminho passa a rodar em produção, o documento que o chamava de incógnita vira uma
  hedge FALSA** — e hedge falsa num documento que vai ao cliente custa a mesma confiança
  que a promessa exagerada. O `entrega-ao-cliente.md` afirmava, meses depois da parcela
  42, que "**não** há certificado ICP-Brasil".

- **A AGENDA DEPOIS DA ABERTURA: as seis melhorias, e o que a lista pegou na escrita**
  (set/2026, PR 167 — a direção pediu "resolva as 6" da lista ordenada: retornos a marcar,
  próximos horários na ficha, ✓ na grade, jornada por profissional, próxima vaga e o
  lembrete automático por e-mail). Nenhuma era capacidade nova de motor: eram DADOS que o
  sistema já gravava sem leitor no lugar da decisão. As regras que decidiram cada uma:
  ⚠️ **O retorno pedido na sessão NUNCA vira agendamento sozinho.** `RetornoSugeridoEm`
  existia desde a parcela 77 e o único leitor era o painel da sessão anterior — a
  recepcionista não tinha como saber que o médico pediu "voltar em 7 dias". Virou FILA do
  balcão (`RetornosAMarcar`, Application, pura): coberto quando há horário ATIVO depois do
  dia da sessão, atrasado quando a data passou, e o "Marcar horário" chega ao formulário
  pré-preenchido. Materializá-lo como horário seria o cartão fantasma da parcela 58 de
  novo. E o convite pelo WhatsApp diz QUEM pediu e PARA QUANDO, nunca por quê — o motivo
  é registro clínico, e a notificação aparece em tela bloqueada.
  ⚠️ **O ✓ da grade só afirma o que a RODADA afirmou** (a regra do kanban, parcela 87):
  Respondido → "Confirmou"; Enviado sem resposta → "Não confirmou"; sem contato → SEM
  selo. Acusar de "não confirmou" quem nunca foi avisado seria o selo mentindo. Leitura
  em LOTE pelos agendamentos da grade, uma consulta por dia carregado.
  ⚠️ **Jornada em branco é o comportamento de SEMPRE.** `DiasDeAtendimento`/`AtendeDas`/
  `AtendeAte` são nulos em toda linha gravada, e nulo quer dizer "não declarou": a grade
  não pinta, o Salvar não reclama, a ocupação continua pela jornada global. Declarada, o
  fora-do-expediente vira `RecursoAgenda.Expediente` no `ConflitosAsync` e **RECUSA como o
  bloqueio recusa** — é conflito de RECURSO (quem atende não está lá), não aviso de
  paciente —, com a frase dizendo quando ele atende e a saída de sempre: "escolha outro
  horário ou marque como encaixe". O encaixe passa. A ocupação (`IndicadoresService`) usa
  a jornada DELE; a global vale para quem não declarou — a clínica que não cadastra
  jornada não vê número nenhum mudar.
  ⚠️ **E aqui eu escrevi a decisão de DOIS jeitos.** O teste
  (`Marcar_fora_do_expediente_e_recusado_dizendo_quando_ele_atende`) e a tela fixam a
  recusa; a lição, a tabela de features e a descrição do PR diziam "avisa, sem impedir".
  Nada falhava — o código estava certo e coerente com o bloqueio; o que mentia era o
  texto, em três lugares, e quem pegou foi a pergunta da direção ("você fez algo de
  errado?"), não uma rede. **A frase que descreve o comportamento se confere contra o
  TESTE que o fixa, não contra a intenção lembrada** — e "a regra da agenda é avisar" vale
  para o que é do PACIENTE (carteirinha, cota, choque com ele mesmo); recurso disputado ou
  indisponível sempre recusou.
  ⚠️ **A busca de vaga anda no PASSO da grade, não na duração pedida** — e o teste que
  escrevi esperava o contrário. Uma sessão de 60 min cabe às 14h00, 14h30 e 15h00 dentro
  de 14h–16h; oferecer só 14h e 15h esconderia a vaga das 14h30 que a grade aceita. O
  código estava certo; a expectativa era minha. **Quando o teste reprova, a primeira
  pergunta é qual dos dois está errado** — e a resposta se decide pela regra, não pela
  asserção que já estava escrita.
  ⚠️ **`Include` que o consumidor não lê é carga medida e removida**: a leitura dos
  horários do profissional em 60 dias trazia o `Profissional` de cada linha para um
  cálculo que só usa hora, duração e status. A rede de tradução do Npgsql passou a cobrar
  `NotContain("JOIN")` — é a lição da parcela 74 ("meça o que a consulta TRAZ") virando
  asserção.
  ⚠️ **O lembrete por e-mail é a MESMA rodada de confirmação, com outro canal.** Mesmo
  contato, mesma chave de idempotência (o agendamento), mesmo `RegistrarEnvioAsync` — só o
  `CanalContato` muda. É isso que faz quem já foi avisado pelo WhatsApp não receber e-mail,
  e quem recebeu e-mail aparecer na janela do balcão como "avisado por e-mail". Duas
  rodadas divergiriam sobre quem já foi avisado, e o paciente receberia dois.
  ⚠️ **Sem servidor configurado NÃO GERA contato nenhum — e a ORDEM das duas leituras é a
  decisão.** A pergunta "há servidor?" vem ANTES de `GerarConfirmacoesAsync`: invertida, a
  abertura passaria a criar a rodada do dia numa clínica que nunca ligou o e-mail, e a
  janela "Confirmar sessões" do balcão abriria com os contatos já criados sem ninguém ter
  clicado em "Gerar rodada". Ligar o e-mail é o que passa a gerar na abertura; desligado, o
  balcão funciona exatamente como antes, e há teste fixando a contagem zero. **Feature
  opt-in não muda o comportamento de quem não a ligou.**
  ⚠️ **Falha de envio DEIXA o contato pendente**, contada e no log — é o que mantém o
  WhatsApp de um clique como saída e faz a próxima abertura tentar de novo. Marcar como
  enviado o que o servidor recusou seria a rodada dizendo "avisado" sobre quem não recebeu
  nada. E a REleitura rastreada do contato antes de mandar existe porque a Recepção e o
  Gerente abrem no mesmo minuto — o que a outra máquina acabou de avisar não recebe o
  segundo.
  ⚠️ **A abertura lembra hoje, amanhã E o fim de semana até a segunda** (`DiasDaAbertura`,
  pura, testada): a véspera da segunda é o domingo, e no domingo ninguém abre o sistema —
  sem a regra, a sessão de segunda só seria lembrada na própria segunda de manhã. É a
  mesma razão de a jornada presumida da busca de vaga excluir só o domingo.
  ⚠️ **A credencial do SMTP mora no BANCO** (a regra da parcela 53, aplicada sem hesitar
  desta vez): o lembrete sai de dois postos, e configuração por máquina falharia calada no
  posto em que ninguém a digitou. A senha em claro é o mesmo problema aberto do
  `client_secret` do SafeID — separado, conhecido, e não uma novidade daqui.
  ⚠️ **O texto do e-mail e o do WhatsApp são UMA definição** (`MensagensDeContato`, na
  Application — o e-mail sai de um serviço sem tela, e a Application não enxerga o shell).
  A frase ganhou "hoje" / "amanhã" / "segunda-feira, 14/09" em cultura FIXA pt-BR, porque é
  ENVIADA: dois postos com culturas diferentes mandariam "Monday" e "segunda-feira" pelo
  mesmo motivo. E o nome que assina cai do nome fantasia para a razão social e por último
  para o nome do remetente cadastrado — o teste pegou o caso em que a clínica não tem
  prestador preenchido e a assinatura saía vazia.
  ⚠️ **`Email` como nome de classe estática colide com a propriedade `Email` de todo
  ViewModel que a usaria** — dentro da classe, `Email.Valido(Email)` resolve para a
  propriedade. Daí `EnderecoDeEmail`, no Domínio: uma validação para a ficha (que grava),
  o lembrete (que envia) e as Configurações (que cadastram o remetente).
  ⚠️ **O quinto botão numa barra de `StackPanel` horizontal sai cortado pela borda** da
  janela mínima (560). Virou `WrapPanel` filho do `StackPanel` vertical — largura FINITA,
  dobra de verdade (a checagem 32 pelo avesso: lá o WrapPanel não dobrava por estar num
  filho de largura infinita).
  As redes pegaram o resto, como devem: `ctrl:Ajudantes.Placeholder` numa janela que não
  declarava `xmlns:ctrl` (o parser XML do `verificar-suite` acusou o prefixo solto — o
  `MC3074` que o CI pegaria sete minutos depois) e um `TextBlock` de data sem
  `TextTrimming` numa célula de 150 px (checagem 24).

- **A AGENDA DO DIA VIROU LISTA, E A "FILA DO DIA" DEIXOU DE SER ITEM** (set/2026, PR 167 —
  a cliente, com o print do Smart Clinic na mão: a agenda e a fila do dia *"acabam sendo uma
  duplicata para a mesma função"*). O kanban em raias (parcelas 26 → 58 → 87, aprovado em
  mockup) respondia à pergunta que EU tinha atribuído ao balcão — "vinte pessoas em cinco
  estados". Para quem usa, a agenda do dia e a fila são UMA pergunta, e no Smart Clinic são
  UMA tela: a lista do dia com o status na linha, cancelado e falta apagados, o ATENDER na
  linha. O Meu dia do médico já era essa lista (parcela 95); o balcão ganhou a mesma, com
  as ações dele, como aba **Dia** do item Agenda (Dia · Grade · Semana do profissional).
  ⚠️ **Decisão de leiaute aprovada em mockup não é imune à leitura que a cliente faz DEPOIS
  de usar a tela irmã.** A parcela 95 escreveu que "o quadro em raias continua sendo o da
  Fila do balcão, onde a pergunta é outra" — a pergunta era a mesma, e quem disse foi quem
  usa. A reprovação só não veio porque o desenho aprovado do Meu dia já existia para
  copiar: **quando duas telas da suíte respondem à mesma pergunta, a segunda copia a
  primeira, e a diferença que sobrar precisa de razão escrita.**
  ⚠️ **Um vocabulário para as duas listas** (`StatusDaFila`, Application). O médico lia
  "No local" e o balcão leria "Na recepção" sobre a MESMA pessoa — dois fatos para um só.
  Palavra e detalhe ("chegou às 14:40 · espera 12 min") saem de uma função, o Meu dia
  passou a chamá-la, e o teste é onde a frase mora.
  ⚠️ **Sub-tela que vira aba continua DECLARADA** (checagem 28) e quem a esconde é o pai —
  "Fila do dia" saiu do menu sem mudar a chave `fila`, porque o painel e o Consultório
  navegam por ela (a regressão da parcela 37 espera exatamente esse atalho).
  ⚠️ **O que a lista NÃO repete: a janela do horário.** Remarcar, reabrir o cancelado,
  comprovante e WhatsApp continuam na GRADE, e a linha leva até lá ("Abrir na grade…") —
  duas janelas do horário divergiriam na primeira correção. Para a linha cancelada é a
  ÚNICA ação do "⋯", com razão: ela não tem passo.
  ⚠️ **Selo do PACIENTE não entra na linha cancelada.** Guia pendente, termo e pacote são
  avisos de quem VEM; numa linha apagada viram ruído que ensina a ignorar o selo da linha
  viva ao lado. Pego relendo o diff, não por rede — o kanban nunca teve linha cancelada.
  ⚠️ **O arrasto morreu com as raias, e o código dele saiu inteiro** (o code-behind foi de
  223 para ~100 linhas): numa lista ordenada pela hora não há para onde arrastar, e o passo
  seguinte é o botão da linha, com a mesma regra e a mesma guarda. Código que sobrevive ao
  desenho que o justificava é a segunda definição esperando divergir.
  **O que ficou de fora, e é decisão**: a BARRA da grade (dez controles) não foi mexida — o
  botão da lista de espera anuncia no próprio rótulo "quem chamar para o horário que vagou",
  e escondê-lo num menu "Mais" mataria o aviso (parcela 58); e a grade continua sendo aba
  porque é nela que se marca clicando no vão e que mora a janela do horário.

- **ALTURA DE LINHA FIXA NÃO ENCOLHE CONTEÚDO: ELA O DECEPA — e a agenda do dia não
  contava o que já tinha acontecido** (set/2026, PR 167 — a cliente mandou o print da
  coluna PROFISSIONAL com o "sala —" cortado ao meio, perguntou se o fluxo da agenda
  estava correto e pediu *"a sincronização/atualização da agenda com a mudança de
  status"*). Três achados, e nenhum quebrava nada.
  ⚠️ **(a) `RowHeight="36"` do estilo implícito contra célula que EMPILHA duas linhas.**
  Os DOIS design systems fixam a altura, e ela é a densidade certa da tabela de uma linha
  por célula — a maioria. Onde a célula empilha (nome + contexto + recado), o WPF não
  encolhe nada: corta na borda da célula, sem erro, sem aviso e **sem rolagem para
  alcançar o que sumiu**. O que fica de fora é sempre a ÚLTIMA linha, que é o dado
  secundário que alguém pôs ali de propósito. `RowHeight="{x:Static sys:Double.NaN}"`
  devolve o "do tamanho do conteúdo" (o padrão do WPF) e o `MinRowHeight` guarda o piso.
  Eram QUATRO grades, todas recentes — a lista do balcão, o Meu dia, os Retornos e os
  Lançamentos —, e virou a **checagem 47**, medida antes de ligar: quatro antes, zero
  depois, autotestada no caso real e nos três legítimos.
  ⚠️ **(b) O sentinela "—" repetido em TODA linha.** A clínica não usa salas, e as doze
  linhas do dia traziam "sala —" debaixo do profissional — gastando justamente a segunda
  linha que a altura fixa decepava. Crachá igual em toda linha não distingue nada (parcela
  57). O campo passou a nascer VAZIO, e a regra é a que o cartão da grade já seguia: quem
  não tem sala não menciona sala. **Tirar o sentinela é mexer em TODOS os leitores** —
  aqui seis, cada um com a frase dele (a linha de contexto filtra, a dica e o "Também:" e
  o aviso da chamada trocam de frase, os dois textos da tela ganham `Visibility`): "· "
  sobrando é tão ruim quanto o travessão.
  ⚠️ **(c) A GRADE não dizia o que tinha acontecido com o horário.** O paciente fazia
  check-in, era chamado, entrava na sala — e o cartão continuava idêntico ao das oito da
  manhã. O `StatusRotulo` que o cartão carregava responde OUTRA pergunta (o status
  gravado) e só é lido dentro da janela do horário. A situação entrou na **linha de
  contexto**, primeiro e colorida, com a palavra de `StatusDaFila` — a mesma das duas
  listas: três telas sobre o mesmo horário, um vocabulário. **Ela não ganhou linha
  própria por medida**: numa faixa de meia hora o cartão tem ~46 px, e a terceira linha
  seria decepada — aparecer em metade dos cartões e sumir na outra metade é pior do que
  não existir. E a ORDEM é a decisão: quem vem antes sobrevive às reticências, e o que
  perece é o estado do dia; a modalidade continua no traço colorido e na dica.
  ⚠️ **(d) VOLTAR À ABA NÃO RELIA — e é a metade que o relógio não cobre.** Grade e lista
  são ABAS do mesmo item, e `TelaComAbas` monta cada uma UMA vez e a guarda: voltar
  devolve a mesma tela com os dados de quando a pessoa saiu. A batida de um minuto não
  ajuda, porque ela havia **parado junto com a tela** (`Unloaded`). Quem marcava a chegada
  na lista e passava para a grade via a grade de antes. O gancho ganhou nome honesto —
  `AoEntrarEmCena`/`AoSairDeCena`, nas cinco telas que o usam —, e as TRÊS que vivem
  dentro de um item composto (a lista do dia, a grade e o Meu dia) releem em SILÊNCIO,
  com duas guardas: nunca na primeira exibição (o construtor já leu, e o `Loaded` chega
  logo atrás) e nunca por cima de uma carga no ar. O painel e a tela do paciente ficam só
  com o renome: item SIMPLES o shell monta do zero a cada navegação, e reler ali seria a
  consulta que ninguém pediu. **Sem a recusa de "só HOJE" que a batida
  periódica tem**: aquela existe para a tela não se mexer sozinha enquanto alguém lê, e
  esta acontece UMA vez, no instante em que a pessoa chega — que é exatamente quando ela
  quer o estado de agora (cancelar um horário de amanhã numa aba e ver a outra
  desatualizada seria o mesmo defeito noutro dia).
  ⚠️ E a releitura do próprio diff pegou **quatro coleções mortas**: as raias
  (`Aguardando`, `NaRecepcao`, `EmAtendimento`, `Finalizados`) continuavam sendo
  preenchidas a cada carga depois de a lista tê-las substituído, escritas e lidas por
  ninguém. Só `Chamados` tem leitor — a faixa do topo. **Ao trocar um desenho, o `grep`
  de quem LIA o desenho antigo é parte da troca.**
  ⚠️ **O que o fluxo tinha de certo, conferido e escrito para a próxima varredura não
  refazer**: os passos da lista batem com `Agendamento.Etapa` (derivada dos carimbos, e
  `Realizado` mapeia para `Finalizado` ANTES da etapa, então sessão concluída nunca
  aparece como "Marcado"); as duas barreiras estão em todos os comandos de escrita
  (`ExigirAlgum(EditarAgenda | MovimentarFila)` no movimento de fila, mais
  `LancarAtendimento` no Concluir); `ExecutarAsync` recarrega depois de toda ação; e os
  avisos de guia da falta e do cancelamento saem em DIÁLOGO, nunca em snackbar.

- **O CRONÔMETRO DO ATENDIMENTO — e o que "respeitando o design system" significa quando a
  referência vem de fora** (set/2026; a direção: *"um temporizador dentro da consulta quando
  o médico/enfermeiro clica em atender, só para ele saber há quanto tempo já está atendendo
  aquele paciente"*, com a foto de um painel de LED de academia e a emenda *"claro que
  respeitando nosso design system"*; mockups em `docs/mockups/temporizador-*.html`).
  ⚠️ **METADE JÁ EXISTIA, e dizer isso foi a primeira resposta.** A pílula verde "Em
  atendimento há 12 min" está no crachá desde a parcela 74, contando do carimbo e se
  reescrevendo sozinha. O que faltava eram duas coisas: ela é uma FRASE (lê-se palavra por
  palavra, e um cronômetro se lê de relance) e **a tela da Enfermagem não tinha nenhuma**.
  Cobrar como feature nova o que já está no repositório é o oposto do que este documento
  pede; o trabalho foi medir antes e mostrar o que sobrava.
  ⚠️ **O fundo escuro NÃO É COR NOVA** — `Brush.Visor.Fundo`/`Texto` existem nos tokens
  desde a captura do retrato do paciente. Traduzir uma referência de fora é separar o
  CONCEITO (visor escuro, dígitos grandes, h:mm:ss correndo) da PALETA: o painel dela é um
  objeto de um metro visto de longe numa academia; este são poucos centímetros a sessenta do
  olho, e o azul-neon sobre preto não sobrevive à tradução. **Antes de inventar um token,
  procure o que o design system já tem para aquele papel.**
  ⚠️ **MONOESPAÇADA não é gosto**: num visor que muda a cada segundo, fonte de largura
  variável faz os dígitos DANÇAREM e o vizinho pular — o movimento que se nota deixa de ser
  o tempo passando e passa a ser o leiaute se mexendo.
  ⚠️ **A conta é UMA.** `Agendamento.DuracaoDoAtendimento` passou a derivar de
  `TempoDeAtendimento`: o visor e a frase da pílula saem do mesmo cálculo, senão o médico
  veria o número discordar da frase ao lado dele. E o que decide o TEXTO mora na Application
  (`CronometroDaSessao`, puro e testado) — a regra da `GradeSemana` e do
  `ResumoSessaoAnterior`: o que a tela AFIRMA precisa morar onde o `dotnet test` alcança.
  ⚠️ **Sem consulta em curso não há visor** — nem zerado. Prontuário aberto pela carteira,
  sessão de outro dia, horário cancelado, sessão encerrada: nada. Um cronômetro parado em
  `00:00:00` convidaria a "iniciar" um atendimento que não existe na agenda de ninguém, e
  depois do Finalizar quem fala é a pílula ("durou 24 min") — dois leitores da mesma coisa
  com pesos diferentes é o defeito de sempre. **As horas aparecem SEMPRE** (`00:12:35`):
  escondê-las até virar a hora faz o visor mudar de LARGURA no meio do atendimento.
  ⚠️ **A promessa do mockup obrigou o trabalho na Enfermagem.** Eu escrevi ali que o visor
  valeria "para os dois lados, Consultório e tela da Enfermagem" — e a tela da Enfermagem
  abre o paciente SEM vir da agenda: a lista dela guarda o `Paciente` e descarta o horário.
  Ou eu cumpria ou corrigia a promessa (a lição da parcela 67, agora num documento que vai à
  direção). Cumpri com uma leitura pequena que já existia (`AgendamentosDoPacienteNoDiaAsync`)
  mais a decisão `CronometroDaSessao.EmCurso`, testada — e isso **destravou o selo "DESTA
  SESSÃO"** da linha do tempo, que naquela tela nunca acendia porque `agendamentoAberto`
  chegava sempre nulo: `EvolucaoEnfermagem.AgendamentoId` era gravado, calculado e lido só
  pela seção do Consultório.
  ⚠️ **E o bloqueador foi o de sempre, pego na releitura do próprio diff: criei o
  `DispatcherTimer` e não registrei o `Tick`.** O visor ficaria congelado no valor da carga —
  um cronômetro que não anda, que é pior do que cronômetro nenhum —, e nada falharia: build,
  2405 testes e as três redes verdes. **Timer novo se confere pelo par: quem o liga (a VIEW,
  no Loaded/Unloaded) e quem o ESCUTA.**

- **OS BOTÕES DE FILA SAÍRAM — e a conta que eles sustentavam saiu junto, em vez de virar
  ZERO** (set/2026; a direção, depois de ler o manual: *"pode retirar os botões, deixe
  somente o atender para o médico — a cliente não quer todo esse fluxo"*). A fila em
  etapas (Chegou · Chamar · Entrou · Voltar) morreu nas DUAS listas do dia: o balcão ficou
  com o "⋯" e o profissional com o **Atender**, que carimba a entrada desde a parcela 95.
  ⚠️ **O que a remoção transforma em MENTIRA é o que decide o tamanho da parcela.**
  `IniciarAtendimentoAsync` carimbava `ChegadaEm` e `ChamadoEm` junto com a entrada, com o
  argumento — então correto — de que "entrar direto" era a EXCEÇÃO e o kanban precisava
  distinguir quem esperou. Sem os botões ela virou a REGRA, e a ficção passaria a produzir
  um número: chegada igual à entrada dá `EsperaMinutos = 0` para todo paciente, e o painel
  anunciaria **"espera média 0 min"** — *ninguém espera nesta clínica*. Nada falharia. O
  carimbo inventado saiu, a espera voltou a ser NULA ("não medido"), e os dois cartões que
  a liam foram embora com ela (o "espera média" do painel e do placar da lista); "Na
  recepção" — que contava check-in — virou **"A atender"**, ligado ao `Aguardando` que
  estava calculado e sem leitor desde que o painel nasceu.
  A regra que fica: **ao apagar uma PORTA, procure o que ela alimentava e pergunte se o
  número que sobra ainda é verdade.** Métrica cuja base deixou de ser coletada não vira
  zero — ela deixa de existir, e o cartão que a mostrava sai da tela: cartão que diz
  sempre a mesma coisa é o que ensina a não olhar a fileira.
  ⚠️ **O MOTOR fica, e está escrito no motor.** `RegistrarChegadaAsync`, `ChamarAsync`,
  `DesfazerChamadaAsync` e `VoltarEtapaAsync` continuam no `AgendaService`, testados e
  **sem porta em produção** — decisão, não esquecimento: a fila volta a ter tela no dia em
  que uma clínica a quiser, e apagá-la obrigaria a reescrevê-la. Quem varrer "método sem
  chamador" (parcela 63) lê o aviso antes de remover. O que SAIU foram as derivadas que
  serviam só ao botão (`DiaDoProfissional.NaRecepcao`/`Chamados`/`ProximoAChamar`): essas
  eram contagem sem leitor, o defeito na versão barata de remover.
  ⚠️ **Cinco testes existiam para fixar o CONTRÁRIO, e foram parte da mudança** (a regra da
  parcela 95): `IniciarAtendimento_SemChamadaAnterior_CarimbaAChamadaJunto` virou
  `..._SemChegada_NaoInventaEsperaZero`, e os três de "voltar etapa desce uma coluna"
  passaram a carimbar a chamada de propósito — o que se desfaz é o carimbo que EXISTE.
  Dois testes novos prendem a regra onde a clínica a vê: o painel devolve espera NULA no
  fluxo curto, e "A atender" conta quem ainda não entrou na sala.
  ⚠️ **E o TEXTO da tela é parte da remoção.** O subtítulo do painel dizia "quem chegou,
  quem espera" sobre uma tela que não sabe mais nenhuma das duas coisas. Frase que
  descreve um mecanismo removido é a mesma família do comentário que promete o que o
  código não faz (parcela 67) — só que impressa na cara de quem usa.

- **A SIDEBAR GANHOU O GRUPO ATENDIMENTO E OS GRUPOS RECOLHÍVEIS** (set/2026; o mockup
  `docs/mockups/sidebar-tres-desenhos.html` foi aprovado ANTES de uma linha de WPF — o
  caminho da parcela 87). A cliente pediu para ver a sidebar "se fizéssemos uma
  mudança", escolheu o desenho A (grupos que abrem e fecham) e pediu para **separar o que
  é de atendimento de paciente**. Medido antes de desenhar: o Gerente Geral tinha 25
  itens visíveis pedindo ~1290 px de menu para 658 px de janela — FINANCEIRO e
  INTELIGÊNCIA abaixo da dobra.
  ⚠️ **PACIENTE é a PESSOA; ATENDIMENTO é o ATO — e o corte é o da permissão.** O que
  fica em PACIENTE se abre com `VerFichaPaciente` (cadastro, documentos, pacotes, preços,
  retorno, importação); o que fica em ATENDIMENTO é dado de saúde ou o gesto que o cria
  (lançar e marcar, prontuário, prescrições, sala de infusão, enfermagem). Um grupo que
  mistura os dois é o bit sobrecarregado da parcela 49 vestido de menu. **O mesmo item
  mora no mesmo grupo nos quatro apps**, e o custo declarado é que no Consultório
  PACIENTE fica com um item só.
  ⚠️ **Navegar abre o grupo do destino e fecha os outros; o clique no cabeçalho alterna
  SÓ aquele grupo.** Sem a primeira metade a lista volta a crescer a cada Ctrl+F; sem a
  segunda, quem abriu FINANCEIRO para olhar veria GESTÃO se fechar por isso. Navegar para
  uma tela OCULTA (o paciente em foco, pelo "Atender") **não mexe nos grupos** — fechar
  tudo deixaria a sidebar sem um item à vista. E com a sidebar RECOLHIDA (Ctrl+B) todos
  os itens aparecem como ícone: cabeçalho que não se lê não se clica.
  ⚠️ **"Atendimento" dentro de ATENDIMENTO virou "Lançar e marcar"** — sem barra, pela
  lição da parcela 95 (item com dois nomes é item que não decidiu o que é). A CHAVE
  (`atendimento`) não mudou: chave é contrato de navegação.
  ⚠️ **Registros e pendências e Exames SAÍRAM do composto "Pacientes" do Consultório**:
  são prontuário, não ficha. O Consultório ganhou o composto "Prontuário" com a MESMA
  chave do da Recepção (`ChavesSuite.GrupoProntuario`, que passou a atravessar módulo
  junto com `ConsultorioExames`) — chave literal nos dois módulos seria a duplicata da
  checagem 45 de novo. O composto "Pacientes" do Consultório sobrou com UMA aba, e o shell
  mostra a tela direto.
  ⚠️ **OS ÍCONES SÃO OS DO MOCKUP, e a palavra do cliente venceu a regra do design
  system.** A primeira versão trocou os desenhos do mockup por glifos da Segoe Fluent
  (regra 3 do design system: "iconografia é Segoe Fluent"), com o argumento de que SVG
  por item seria uma segunda definição de ícone. A direção respondeu na hora: *"os
  ícones não ficaram iguais e isso é um problema, pois queremos os ícones!"* — e
  **mockup aprovado inclui os ícones que ele mostra**: trocá-los por "equivalentes" é
  entregar outra coisa com o mesmo nome. Os 34 desenhos moram em
  `Styles/Componentes/Icones.xaml` (geometria de traço, 24 unidades, mostrada a 18 px),
  gerados DO MESMO texto do mockup (retângulo e círculo viraram arcos; o `d` do SVG o
  WPF lê tal e qual, depois de um tokenizador que separa os argumentos — as flags de
  arco coladas ao número seguinte, `1-12`, são o caso que o parser do WPF não devolve
  igual ao do navegador). `ItemMenuModulo.Icone` é o nome; o conversor `IconePorNome`
  acha a geometria; o `Glifo` continua como caminho de baixo para item sem ícone.
  ⚠️ **É a única exceção à regra 3, e ela é DECLARADA no dicionário** — o CardKpi, o
  placar da agenda e os botões de ícone continuam na fonte. O dicionário e o conversor
  foram portados ao design system do faturamento no mesmo commit (regra 4), sem mexer na
  sidebar de lá. E `Application` num arquivo de conversor resolve para o namespace
  `Clinica.Application`, não para a classe do WPF — pego pelo `compilar-sombra`.
  De quebra, os glifos de baixo ficaram únicos por item: "Pacotes" estava com glifo
  VAZIO (`""`) desde que subiu ao shell, "Minha agenda" repetia o da Agenda do balcão,
  "Conformidade" repetia o da Auditoria e "Repasses" usava o de Copiar.
  **A sidebar do FATURAMENTO não recebeu o acordeão, e é decisão**: ela tem poucos itens,
  não rola, e o app está em produção. O desenho do ITEM (ícone, rótulo, barra de 3 px)
  continua idêntico nos dois — é ele que faz os dois apps parecerem o mesmo produto.

- **O INTERVALO DA RELEITURA SEGUE QUEM RECEBE, NÃO QUEM MUDA** (set/2026 — decisão da
  direção depois de medirmos a latência da correção do horário: *"diminua para 30
  segundos a releitura do Meu dia"*). O quadro do médico passou a bater a cada **30 s**;
  os do balcão (lista do dia e grade) continuam em 1 min e o painel em 2, e a assimetria
  é o ponto: **a tela de quem MUDA já recarrega a cada ação** — quem marca a chegada,
  cancela, marca falta e corrige a modalidade está no balcão, e cada um desses cliques
  recarrega a tela dele —, então quem precisa de batida curta é o lado que só RECEBE.
  ⚠️ **Frequência se decide pelo CUSTO da batida, medido, não pela simetria.** A
  silenciosa do Meu dia é UMA leitura de UM dia (`DoDiaAsync` — os horários e as
  evoluções daquele dia); as 30 dias de pendências ficaram de fora dela desde a parcela
  69, justamente para ela poder ser barata. O painel custa três consultas por batida e é
  por isso que ele bate de dois em dois minutos: dobrá-lo custaria seis.
  ⚠️ E o comentário que justificava o relógio ainda falava do **"Chamar próximo"**, que
  saiu com o fluxo de fila — descrevia como razão do intervalo um botão que não existe
  mais. Foi reescrito no mesmo commit, com a razão de hoje. **Ao mexer num número que um
  comentário justifica, releia a justificativa antes: ela é a primeira coisa a
  apodrecer.**

- **A CONFERÊNCIA DE CHOQUE EXISTE PARA IMPEDIR QUE SE CRIE UMA SOBREPOSIÇÃO — reconferi-la
  numa edição que não move o horário vira corredor sem saída** (set/2026 — a clínica trocou
  a especialidade de um horário e levou *"Dr. … já atende SUELLI às 14:01. Escolha outro
  horário ou marque como encaixe"*, com o médico ainda não tendo atendido ninguém).
  ⚠️ **A mensagem estava certa sobre o BANCO e errada sobre o ATO.** O choque era real: o
  horário importado ficou parado às 14h00 e a sessão foi lançada ao lado como ENCAIXE às
  14h01 — mesmo paciente, mesmo profissional, o cenário da parcela 93. O que a secretária
  fez não cria sobreposição nenhuma: o formulário devolve a MESMA hora, o MESMO
  profissional, a MESMA sala e a MESMA duração, e só a especialidade muda. `RemarcarAsync`
  chamava `GarantirSemChoqueAsync` **incondicionalmente**, então ela atravessava a porta,
  escolhia a especialidade certa e levava no Salvar uma recusa **que não tem como cumprir**
  — a única forma de obedecer seria cancelar a sessão que aconteceu. É a lição da parcela 69
  ("a porta que ficou de fora vira um corredor sem saída") vinda pelo outro lado: aqui as
  duas barreiras concordam, e quem está errado é a REGRA ser reavaliada num ato que não a
  disputa.
  ⚠️ **E o botão Editar não CRIOU o defeito: ele o EXPÔS.** O caminho é o mesmo `Remarcar`
  de sempre; o que mudou foi passar a ser usado para o que ele agora se propõe — corrigir o
  que a sessão É, sem mexer em quando ela é. **Ao abrir uma porta nova para um serviço que
  já existe, releia as guardas dele perguntando quais foram escritas para o uso ANTIGO.**
  A correção é a condição, não o afrouxamento: só reconfere quando **disputa recurso** —
  data/hora, duração, profissional, sala ou o sinalizador de encaixe mudaram. E **reabrir
  cancelado/falta/substituído confere sempre**, porque ali o horário tinha SOLTADO o
  recurso e o vão pode ter sido dado a outra pessoa — é a metade que não podia cair junto,
  e tem teste próprio. O que reproduz o caso da clínica
  (`Corrigir_a_especialidade_nao_esbarra_no_encaixe_que_ja_existia`) foi verificado
  REPROVANDO no código anterior, não presumido.
  ⛔ **SUPERADA duas semanas depois: hoje NENHUM choque recusa** (ver a lição logo abaixo).
  A condição `disputaRecurso` não existe mais — nem a recusa que ela condicionava. Fica
  aqui porque o *diagnóstico* continua valendo e é o que levou à decisão maior: a clínica
  vinha esbarrando na recusa em situações em que ela não protegia nada, e a correção
  condicional tratou o caso que doeu em vez da premissa ("o profissional só atende um
  paciente por vez") que nunca foi verdade nesta clínica. **Correção condicional de uma
  regra que a casa não segue é remendo que a próxima situação fura.**

- **CORRIGIR O QUE A SESSÃO É, SEM SAIR DA AGENDA DO DIA** (set/2026 — a cliente, com o
  print da lista: *"temos muitos atendimentos que foram importados que vieram como
  consulta e não as nossas especialidades cadastradas… um botão de editar na agenda para
  que a secretaria possa colocar a situação correta para que reflita no médico"*). A
  importação do Smart Clinic trouxe 227 horários e o sistema antigo não guardava mais que
  **"Consulta"** — que não é nenhuma das modalidades que a clínica atende.
  ⚠️ **A capacidade existia; faltava a PORTA — e o nome dela mentia.** Corrigir era
  possível pelo formulário do horário, a QUATRO cliques e uma troca de aba (lista →
  "Abrir na grade" → cartão → janela do horário → **"Remarcar"**), atrás de um botão cujo
  nome fala de mudar o horário de LUGAR, que não é o que se quer fazer. A lista ganhou o
  botão **Editar**, e ele abre a MESMA `AgendamentoWindow` da grade: uma segunda janela do
  horário divergiria na primeira correção — é a razão pela qual a lista nunca repetiu as
  sete ações do cartão. O que muda é o TÍTULO, e ele vem da PORTA
  (`TituloDaEdicao`), pela lição de `RotulosDeAbrir`: rótulo fixo mente numa das duas.
  ⚠️ **O botão SOME na linha fechada, e não é enfeite:** `RemarcarAsync` recusa o
  realizado ("estorne o atendimento antes") e **REABRE** o cancelado e a falta
  (`Status = Agendado`). Um "Editar" que ressuscita um horário cancelado em silêncio é
  pior do que não ter o botão; para esses a porta continua sendo a grade, onde ele se
  chama "Reabrir este horário" e diz o que faz. A condição é o `EmAberto` que já existia —
  **booleano reusado numa segunda decisão**, e desta vez ele responde à segunda pergunta:
  os dois casos que ele exclui são exatamente os dois que o serviço trata de outro jeito.
  Fica escrito nele, senão o próximo a mexer só enxerga um leitor.
  ⚠️ **A metade visível é `EditarAgenda` ESTRITO, não `PodeEditarAgenda`** — aquele é o OU
  com `MovimentarFila`, e a enfermagem move a fila sem mexer na agenda: com a conta larga
  ela veria o botão aceso e levaria a recusa depois do clique.
  ⚠️ **A ESPECIALIDADE da consulta era gravada e NENHUMA tela de quem atende a lia.** É a
  segunda metade do pedido ("que reflita no médico"): a modalidade já chegava ao "Meu dia"
  (`CatalogoModalidades.Nome`), a especialidade não — a consulta de psiquiatria e a de
  geriatria saíam as duas como "Consulta". `NomeComEspecialidade` compõe as duas coisas
  numa frase só, e mora no DOMÍNIO porque são QUATRO leitores (a lista e a grade do
  balcão, o dia e a semana do médico) e quatro frases divergem na primeira correção. Ela
  só entra quando a modalidade é consulta — nas outras `RemarcarAsync` limpa a
  especialidade, e escrevê-la ao lado de "Acupuntura" afirmaria sobre a sessão algo que o
  horário não guarda. De quebra, ela mata a armadilha do `Nome(codigo ?? familia.
  ToString())` nas duas telas do balcão: **`Base("")` cai no padrão dele** (acupuntura com
  eletro), e a consulta deixaria de ser consulta.
  ⚠️ **O aviso das GUIAS morria entre as camadas.** Trocar a modalidade de um horário que
  já tem guia as REGERA, `AjustarAoRemarcarAsync` devolve isso escrito, `RemarcarAsync`
  aceita o canal — e o formulário **não o passava**: a janela fechava e o único fato que
  interessa ao faturamento sumia (a lição da parcela 62). Agora ele é dito pelas DUAS
  portas do mesmo formulário, porque capacidade que existe numa porta só é o defeito
  recorrente do projeto. O que RECUSA continua sendo exceção, e essa fica na tela.
  ⚠️ **O teste que carrega isto é o do CIRCUITO** (`CorrigirModalidadeDoHorarioTests`):
  balcão corrige pelo caminho do botão (mesma data, mesma hora, modalidade nova) →
  `ConsultorioService` devolve a modalidade nova na lista do médico. Elo partido aqui não
  vira erro: vira uma tela que continua dizendo "Consulta" depois de alguém ter corrigido.
  Verificado que ele REPROVA no código anterior, não presumido. Junto vão as duas garantias
  que a edição não pode levar embora: o **check-in** de quem já está no balcão (a data não
  mudou, então os carimbos ficam) e a **procedência** escrita na observação, que é por onde
  a clínica reconhece o que ainda falta corrigir.
  **O que ficou de fora, com o motivo**: não há edição em LOTE — regerar guia em massa é
  decisão que ninguém revisa linha a linha, e a correção acontece com a agenda do dia à
  frente; e a lista continua sendo por DIA, então achar os importados é navegar os dias
  em que eles estão.

- **A AGENDA AVISA E NÃO IMPEDE — nenhum choque recusa** (set/2026, decisão da direção; o
  pedido da clínica foi literal: *"horário na agenda livre, não precisa dar choque/bloqueio
  porque já tem paciente naquele mesmo horário"*). `GarantirSemChoqueAsync` foi REMOVIDO, e
  com ele a última recusa por sobreposição: profissional ocupado, sala além da capacidade,
  agenda fechada (feriado, férias, folga) e hora fora da jornada declarada viraram TODOS
  aviso. `ConflitosAsync` fica, e ficou mais importante — é a leitura que as telas
  criticam a cada tecla.
  ⚠️ **A premissa que caiu não é técnica, é sobre como a casa trabalha.** "O profissional
  já atende alguém às 14h" nunca foi verdade aqui: na acupuntura o paciente fica na maca
  com as agulhas enquanto outro é atendido. Enquanto a recusa existiu, a única saída dela
  era marcar como ENCAIXE — e o campo passou a ser preenchido **para contornar a recusa em
  vez de descrever o fato**, que é como um dado deixa de significar o que promete. A
  caixinha continua, com o verbo trocado ("registrar como encaixe"), e o encaixe segue
  sendo o que dispensa o profissional na marcação (parcela 95).
  ⚠️ **A recusa que FICOU é a de "quem vai atender"** (parcela 95), e ela não é choque:
  horário sem dono não aparece na agenda de ninguém nem entra no repasse — não é um
  horário disputado, é um horário que o fluxo inteiro não alcança.
  ⚠️ **Tirar a barreira sem perder o aviso é o trabalho todo**, e ele é maior que a
  remoção. O que se ganha em fluidez se paga em ATENÇÃO: sem a lista de avisos, marcar em
  cima do feriado passaria a depender da memória de quem está no balcão. Por isso as duas
  telas ganharam **cabeçalho que DIZ que nada impede** (tarja de aviso com o botão
  "Lançar" aceso ao lado é tela que se contradiz, e a recepcionista que já levou a recusa
  antiga fica procurando o que fazer para o aviso sumir) e **duas cores por gravidade**:
  âmbar para a rotina da casa, VERMELHO só para a agenda fechada, que é o único aviso sem
  ninguém do outro lado. Pintar as duas de vermelho ensina a ignorar as duas.
  ⚠️ **A frase saiu das ViewModels para a Application** (`AvisosDeChoque`) — havia DUAS
  cópias de `Descrever`, uma em cada tela de marcação, e o que a tela AFIRMA precisa morar
  onde o `dotnet test` alcança. Foi só ao escrevê-la ali que ela ganhou teste.
  ⚠️ **Na GRADE, a metade visível**: `PodeMarcar` deixou de exigir vão aberto (o feriado e
  o fora-do-expediente voltaram a ser clicáveis, com a hachura e o motivo ficando como
  INFORMAÇÃO), e o vão OCUPADO ganhou um "+" próprio (`VaoMaisUm`, no design system).
  Sem esse botão a correção resolveria metade: a recepcionista veria o vão tomado e nem
  tentaria. Ele é VISÍVEL, ao contrário do `VaoLivre` — lá o argumento para escondê-lo é a
  quantidade; aqui a DESCOBERTA é metade do que a mudança precisa entregar.
  ⚠️ E a releitura do diff pegou o preço de mexer em camada de desenho: pôr
  `IsHitTestVisible="False"` nas hachuras (para o clique passar até o botão) **matou o
  ToolTip que dizia quando o profissional atende** — elemento sem hit test não recebe
  hover. Trocar a recusa pelo aviso e perder o aviso no mesmo gesto seria trocar barreira
  por SILÊNCIO, o único desfecho que esta mudança não podia ter; o motivo subiu para a
  dica do próprio "+" (`CelulaAgenda.DicaDoVao`). **Ao tirar o hit test de um elemento,
  procure o que ele mostrava no hover.**
  ⚠️ **O que a decisão custa, e está escrito no teste**: empurrar as trinta sessões de um
  congresso (`RemarcarEmLoteAsync`) passou a EMPILHÁ-LAS sobre quem já estiver na semana
  seguinte, sem pular nada; a série marca por cima do feriado; e reabrir um cancelado cujo
  vão foi dado a outra pessoa põe os dois no mesmo horário. Numa clínica que atende vários
  ao mesmo tempo isso é o certo — e é por isso que a consequência mora no nome dos testes
  em vez de numa nota de rodapé. Cinco testes existiam para provar o CONTRÁRIO e foram
  reescritos (a regra da parcela 95), cada um passando a cobrar as DUAS metades: que marca,
  e que o aviso continua sendo produzido.
  ⚠️ **No FATURAMENTO a confirmação ficou**, e não é incoerência: aquela tela não tem a
  lista criticada a cada tecla, então o diálogo é o único lugar em que o faturista vê que
  há alguém ali — confirmar não é recusar. De quebra, o texto dele parou de prometer
  "(encaixe)": a chamada nunca passou `encaixe: true`, então o que a pessoa confirmava era
  um encaixe que não nascia — e, até aqui, um Salvar que estourava logo depois.

- **"CHEGOU NO LOCAL" VOLTOU, e só ele** (set/2026 — a secretária: *"hoje temos Marcado e
  Concluído, poderíamos colocar um chegou no local entre os dois"*). Semanas antes, a
  direção mandou tirar a fila em etapas (*"a secretaria marca e o médico/enfermeiro
  atende"*) e os quatro botões saíram das duas listas do dia. O uso mostrou o que ela de
  fato dispensava: as ETAPAS INTERMEDIÁRIAS, não o *"quem já está aqui"*.
  ⚠️ **A lição de método é sobre a granularidade do pedido.** "Não quero esse fluxo" foi
  lido como "não quero nenhum passo", e a leitura estava perto o bastante para ninguém
  questionar — o custo só aparece quando a pessoa que usa a tela pede de volta a metade
  que servia. **Quando um pedido manda REMOVER um conjunto, vale perguntar qual peça dele
  a pessoa usa todo dia.**
  Chamar e entrar continuam sem porta (quem registra a entrada é o "Atender" do
  profissional), e `ChamarAsync`/`DesfazerChamadaAsync`/`VoltarEtapaAsync` seguem no motor,
  testados e sem chamador, com o aviso escrito para quem varrer "método sem chamador".
  ⚠️ **De "No local" o balcão não tem passo seguinte**, e o botão não aparece: quem age em
  seguida é quem atende. Dar à recepcionista um "Concluir" ali seria pular o registro
  clínico inteiro.
  ⚠️ **A ESPERA volta a ter base, e o cartão dela NÃO volta junto.** Sem carimbo de chegada
  `EsperaMinutos` é nulo, e foi por isso que os cartões de espera média saíram do painel e
  da lista quando o check-in saiu. Repô-los agora daria uma média sobre metade do dia —
  número com cara de exato enquanto a clínica ainda não está de fato carimbando. A espera
  aparece só na LINHA, ao lado do fato que a produziu.
  ⚠️ E `IniciarAtendimentoAsync` continua NÃO inventando a chegada. Com a porta de volta,
  inventá-la daria espera zero para todo mundo — o *"ninguém espera nesta clínica"* que
  aquela remoção evitou.
  ⚠️ **O defeito que a releitura do diff pegou**: o método novo entrou ENTRE o
  `[RelayCommand]` e o `FinalizarAsync`, e o atributo trocou de dono em silêncio. É a
  lição já escrita neste arquivo (set/2026, o circuito do particular) cometida de novo —
  e desta vez nem o `compilar-sombra` acusou, porque o comando gerado errado não é
  referenciado por XAML nenhum. **`[RelayCommand]` fica COLADO ao método; helper novo vai
  antes do comentário XML do vizinho, nunca entre o atributo e a assinatura.**

- **O PARTICULAR NÃO EXISTIA — e por isso o fluxo dele não tinha por onde começar**
  (set/2026 — o cliente: *"o fluxo do particular não está intuitivo e nem eu mesmo
  consegui entender como fazer um atendimento particular, vender pacote ou sessão
  particular"*). A parcela 60 desenhou o particular inteiro (`ConvenioCadastro.GeraGuia`,
  códigos `NaoAplicavel`, fora da pendência e da rodada), a de set/2026 lhe deu tabela de
  preço e conciliação — e **nenhuma delas criou o cadastro**. A migration semeia quatro
  convênios (Unimed x2, Amil, Petrobras) e os quatro geram guia; "particular" só passava a
  existir se alguém abrisse o app de FATURAMENTO → Configurações → Convênios, criasse uma
  linha à mão e desmarcasse "gera guia". **A recepção não tem essa tela.** Sem a linha,
  lançar caía na recusa da parcela 92 e a janela que oferece escolher abria SEM opção de
  particular: beco sem saída.
  ⚠️ **É a variante mais discreta do defeito recorrente do projeto**, e ela não estava
  catalogada: não é dado sem leitor nem capacidade sem porta — é **comportamento testado
  que nenhum CADASTRO alcança**. O `ConvenioParticularTests` cobre o particular desde a
  parcela 60 e monta o catálogo À MÃO, com uma entrada que o teste inventa. A lição de
  teste: **quando o teste de uma feature MONTA o dado que a torna possível, falta o teste
  de que alguém o cria.**
  `ConvenioCadastro.Particular()` é GARANTIDO na leitura do catálogo, como os embutidos —
  não por migration: a linha nasce no primeiro uso, e a base que nunca precisou dele não
  ganha cadastro que ninguém pediu. Garantido no `ListarAsync`, ele aparece em TODA porta
  que lista convênios, e não só naquela em que alguém lembrasse de construí-lo.
  ⚠️ **"A definir" e "Particular" não se confundem**, e a diferença decide o alerta do
  balcão: o primeiro é a PERGUNTA (a ficha veio do sistema anterior sem convênio, e o
  `ElegibilidadeService` acusa em vermelho até alguém responder); o segundo é a RESPOSTA.
  Tratá-los como a mesma coisa faria a clínica inteira de particulares aparecer como
  pendência para sempre.
  ⚠️ **Três frases de tela mandavam procurar no app errado**, e elas são metade do "não
  consegui entender": o estado vazio da janela de convênio mandava cadastrar *"no
  aplicativo do Gerente"* (só o faturamento tem a tela) e a prévia do preço mandava ao
  *"Gerente → Tabela de preço"* (a Recepção publica a mesma tela desde que ela nasceu).
  **Instrução que manda procurar no lugar errado é pior que instrução nenhuma** — e ela
  não aparece em rede nenhuma, porque é texto.
  ⚠️ **A janela de convênio ordena por GERA GUIA, não por nome**: alfabética, "Particular"
  caía entre a Petrobras e a Unimed com o mesmo peso, e essa lista é justamente onde a
  recepcionista descobre que ele existe. E a linha dele diz o que a escolha significa —
  *"sem guia: o paciente paga a sessão"* —, não só que não gera guia.
  Do lado da sidebar, "Pacotes" e "Preços do particular" eram dois itens SOLTOS no grupo
  PACIENTE, separados por outros e sem nada dizendo que respondem à mesma pergunta: quem
  procurava "particular" achava um item que fala de PREÇO e nenhum que fale de VENDER.
  Viraram o composto **"Particular e pacotes"** — o rótulo diz as duas coisas porque
  "Particular" sozinho mentiria (pacote é venda da clínica, e o paciente de convênio
  também compra).
  ⚠️ **As duas sub-telas continuam DECLARADAS pela Recepção**, e não é só a checagem 28:
  `AbasDisponiveis` só enxerga aba cuja chave é item de um módulo CARREGADO, e quem as
  declara fora dali é o Financeiro (Pacotes) e o Gerente (Preços) — nenhum dos dois no exe
  do balcão. Sem a declaração, o composto ficaria SEM ABAS, sumiria da sidebar, e a
  recepcionista perderia as duas telas. **Ao criar um composto, simule a sidebar de cada
  exe**: a conta que importa não é "a aba existe?", é "ela existe NESTE executável?".

- **A CENTRAL DE DOCUMENTOS NÃO ASSINAVA NEM ENVIAVA — três portas para o mesmo papel, cada
  uma com um pedaço do trabalho** (set/2026 — a continuação do pedido *"resolver as questões
  de intuitividade de documentos e do lançamento/atendimento de paciente particular"*). O
  mesmo documento clínico era operado por TRÊS telas, e o que cada uma sabia fazer não era
  decisão de ninguém — era o que sobrou de quem a escreveu por último:
  a **central** (Documentos) emitia, imprimia a 2ª via e cancelava, e **não assinava com
  e-CPF, não enviava ao paciente e não mexia no link publicado**; as **Prescrições da
  Recepção** faziam os seis atos; as **Prescrições do Consultório**, idem. A central é a
  porta mais caminhada — é ela que responde "onde está o papel que saiu?" —, e era a mais
  pobre: quem emitia uma receita ali tinha de sair da tela e achar a MESMA receita noutra
  para assiná-la.
  ⚠️ **Nada falhava**, e é por isso que durou: cada tela funcionava por inteiro dentro do que
  ela se propunha. O defeito só existe quando alguém compara duas — e quem compara é o
  usuário, no meio de uma tarefa.
  A correção é a de sempre: os seis atos numa definição só (`AcoesDoDocumento`, no shell) e
  os RÓTULOS do menu noutra (`MenuDoDocumento`). Três cópias de "assinar" divergiriam na
  primeira correção, e a que ficasse para trás produziria um PDF selado por outro caminho.
  ⚠️ **A escolha do cliente foi "uma porta só", e ela custou uma TELA.** Com a central
  fazendo tudo, "Prescrições / Receituário" da Recepção passou a ser a mesma tela com menos
  coisas — e duas telas que respondem à mesma pergunta é o que faz a recepcionista procurar
  a diferença que não existe. Ela saiu, e com ela o item da sidebar. **A conferência que
  autoriza remover uma tela não é "o que ela fazia?", é "quem a alcançava, e por onde ele
  passa agora?"**: `PerfilAcesso.Enfermagem` tem `VerProntuario` e **não** tem
  `VerDocumentos` — ela nunca alcançou a central, e continua chegando aos documentos do
  paciente pela FICHA, que pede só `VerFichaPaciente`. Quem tem `Prescrever` chega pela
  central ou pelo Consultório. Nada se perdeu, e isso foi conferido perfil a perfil.
  ⚠️ **A central ganhou o que a tela que saiu tinha de melhor**: o filtro **"Só deste
  paciente"**. Sem ele, quem vinha de "emitir a receita da Maria" via a lista do período
  inteiro e teria de procurar — a tela removida abria já filtrada. **Ao aposentar uma tela,
  liste o que ela fazia MELHOR e leve isso junto**; o resto é regressão vestida de
  simplificação.
  ⚠️ **O FILTRO NOVO TRANSFORMOU UM SEGUNDO CÁLCULO EM DIVERGÊNCIA.** O resumo da lista
  vinha de `CentralDocumentosService.ResumoAsync`, que chamava `EmitidasAsync` **por dentro**
  — a mesma ida ao banco duas vezes para desenhar uma linha de texto. Enquanto as duas
  consultas eram iguais isso era só desperdício; com o recorte por paciente, a lista saía
  filtrada e a contagem **não**: "30 folhas" acima de uma lista de doze manda a pessoa
  procurar as dezoito que faltam. A contagem passou a sair da LISTA que a tela acabou de
  ler, e a FRASE foi para a Application (`ResumoFolhas.Montar`) — não para a ViewModel, pela
  regra da casa: **o que decide o que a tela AFIRMA precisa morar onde o `dotnet test`
  alcança.** A lição geral: **ao acrescentar um filtro a uma leitura, procure quem mais
  CONTA aquela leitura** — dois caminhos que davam o mesmo número passam a dar dois.
  ⚠️ E o `PorFolha` ("quantas de cada folha") saiu no caminho: ele era lido só pelos testes.
  Quem responde isso no agregado é o `PainelDeDocumentos` da direção, sobre outro recorte —
  **número calculado sem leitor é só uma atribuição**. Os testes que o cobriam não foram
  apagados: passaram a exercitar a contagem e a frase pelo caminho que a tela usa, com dois
  novos cobrando que a frase **DIGA** que está filtrada (no cheio e no vazio).
  ⚠️ E a chave do composto "Prescrições" voltou a ser `const` do `ModuloClinico`: ela morava
  em `ChavesSuite` porque DOIS módulos a publicavam (a dedupe do shell funde por chave — a
  duplicata da checagem 45). Com a Recepção fora, ela deixou de ser contrato de alguém. **O
  comentário que justifica uma chave compartilhada é o primeiro a apodrecer quando um dos
  dois publicadores some.**

- **A MESMA ESCOLHA COM DUAS REGRAS, e a do lugar mais importante era um ACIDENTE
  ALFABÉTICO** (set/2026, `OpcoesDeConvenio`). "Convênio ou particular?" era respondida em
  dois lugares: a **janela de vínculo** (parcela 92) ordena quem gera guia primeiro, explica
  o que cada opção significa, exclui o "a definir" como resposta e **não pré-seleciona
  nada** — porque "um padrão que ninguém escolheu" é o defeito que ela existe para corrigir;
  o **CADASTRO do paciente**, que é por onde TODO paciente novo recebe um convênio, listava
  em ordem ALFABÉTICA, pré-selecionava o primeiro e não explicava nada.
  ⚠️ **Numa base que importou a carteira do sistema anterior, o primeiro em ordem alfabética
  é "A definir (importado sem convênio)".** Todo paciente cadastrado no balcão nascia com um
  convênio que AFIRMA ter vindo de importação, que não gera guia e que acende alerta
  vermelho na primeira tentativa de lançar a sessão dele. **Um padrão que depende da ordem
  alfabética não é uma decisão — é um acidente com aparência de decisão**, e foi metade do
  *"não consegui entender como fazer um atendimento particular"*.
  É a lição da parcela 64 ("o mesmo ato com duas regras: a metade sem regra é a que ninguém
  confere") aplicada à porta principal — e a regra foi para o DOMÍNIO, porque enquanto ela
  vivia dentro de uma ViewModel do WPF **nenhum teste a alcançava**.
  ⚠️ **A frase diz também ONDE o dinheiro entra.** "Não gera guia" informa e cala sobre o
  que fazer em seguida, que é exatamente a dúvida de quem nunca lançou um particular.
  ⚠️ **O `incluirADefinir` é do CHAMADOR, e as duas respostas são certas**: a janela de
  vínculo passa `false` (oferecê-lo como resposta devolveria uma tela de sucesso e um
  lançamento recusado em seguida); o cadastro passa `true` por DUAS razões — "ainda não sei"
  é resposta legítima no balcão, e a ficha importada precisa continuar mostrando o convênio
  que ela TEM: combo cujo `ItemsSource` não contém o `SelectedItem` **devolve NULL pelo
  binding**, e o Salvar apagaria a escolha.
  ⚠️ **O faturamento MANTEVE o padrão dele (`UnimedIntercambio`), e está escrito por quê**:
  `SugerirCategoria` deriva de um convênio não-nulo, e aquele app fatura a clínica em
  produção. A regra 3 do bloco do faturamento vale para o efeito colateral de uma
  atualização; aqui o que mudou foi a ORDEM e a EXPLICAÇÃO, não o padrão.

- **A PRÉVIA AFIRMAVA UMA COBRANÇA QUE O FINALIZAR NÃO IA FAZER** (set/2026,
  `CobrancaDaSessao`). Para todo particular a coluna direita dizia *"R$ 180,00 (tabela do
  particular). O pagamento é registrado no Finalizar"* — **inclusive para quem tem um pacote
  de dez sessões com sete pelo uso**. Nesse caso a sessão não é cobrada: ela DEBITA do
  pacote, e é isso que `FechamentoSessaoService.PrepararAsync` propõe (sessão comprada já foi
  paga). A tela afirmava um número que ninguém ia cobrar, com o paciente na frente.
  ⚠️ **Nada falhava** — é a garantia aparente na forma mais barata de cometer: a frase estava
  certa no caso comum e errada no caso que o pacote existe para criar.
  ⚠️ **E a linha estava DENTRO do documento da guia**, que abre recolhido desde set/2026: a
  única resposta que a coluna tinha para o particular só aparecia depois de clicar em **"Ver
  a guia"** — um botão que fala de uma guia que o particular NÃO TEM. Ela subiu para uma
  região própria ("O PAGAMENTO"), com visibilidade própria: **a pergunta do dinheiro não
  depende de a guia nascer neste clique.** Ao recolher um bloco por padrão, liste o que ele
  guardava e pergunte se algum daqueles itens é a resposta de OUTRA pergunta.
  ⚠️ **Dinheiro montado na Application sai em pt-BR FIXO.** `:C` usa a cultura do processo, e
  a Application é lida também pela web de leitura, que roda em Linux com a cultura
  invariante — "¤180.00" numa clínica brasileira. Quem mostrou foi o teste, que roda com a
  invariante; a correção foi na PRODUÇÃO, não na asserção.

- **"QUAL PACOTE DEBITA" ESTAVA ESCRITO TRÊS VEZES** (set/2026, `PacotePaciente.ADebitar`).
  O consumo automático, a proposta do Finalizar e (agora) a frase da prévia respondem à
  mesma pergunta, e as três a resolviam à mão — `Where(PodeConsumir).OrderBy(ValidoAte)`
  repetido. Divergir aqui não estoura nada: **muda o número que o paciente paga**, porque a
  tela apontaria um pacote e o serviço debitaria outro. A regra desceu para a ENTIDADE, e o
  desempate ficou completo (`ValidoAte` → `DataCompra` → `Id`): sem o último, dois pacotes
  comprados no mesmo dia com a mesma validade eram escolhidos pela ordem que o banco
  devolvesse — e ela pode mudar entre duas leituras.
  ⚠️ O teste que carrega isto compara a PRÉVIA com o que a BAIXA automática debita. É a
  forma da parcela 64: quando o mesmo ato existe em dois lugares, o teste que falta é o que
  compara os dois — nunca o que prova que o que você acabou de escrever funciona.

- **A VENDA DE PACOTE TINHA UMA PORTA, E ELA PERGUNTAVA O QUE A TELA JÁ SABIA** (set/2026 —
  a escolha do cliente foi *"no lançamento do atendimento"*). Vender só se alcançava pela
  tela Pacotes, que abre pedindo o paciente — e a conversa *"quer fechar um pacote de dez?"*
  nasce no instante em que o balcão diz o preço da sessão avulsa, com a pessoa na frente. O
  botão foi para o lado do preço, no Novo atendimento, e o paciente entra **já escolhido**.
  ⚠️ **`Selector.SelectedItem` que recebe item FORA do `ItemsSource` devolve NULL pelo
  binding** — a escolha se limparia no mesmo instante em que é feita, sem erro nenhum. O
  componente já tinha a porta (`SelecionarGarantindoNaLista`, da remarcação): **antes de
  escrever a seleção por fora, procure o método que o componente já tem.**
  ⚠️ **O botão só existe para o PARTICULAR.** Oferecer a venda em toda linha de todo
  lançamento seria o botão que aparece sempre e que por isso ninguém lê — e o paciente de
  convênio não compra pacote.
  ⚠️ **E o catálogo vazio não tinha primeiro passo.** O sistema não semeia pacote nenhum, e
  com razão (o que a clínica vende é decisão dela); o que faltava era a tela DIZER isso —
  ela abria com o combo vazio, a pessoa clicava em Vender e levava *"Escolha o pacote"* sobre
  uma lista que não tem nada. É o mesmo defeito do particular que não existia:
  **comportamento pronto e testado que nenhum CADASTRO alcança**, sem uma frase dizendo qual
  é o primeiro passo. A janela passou a oferecer "Cadastrar um pacote…" ali mesmo, abrindo a
  MESMA janela de catálogo da tela de Pacotes (um segundo formulário divergiria na primeira
  correção).
  ⚠️ **E a janela de venda tinha o defeito da mensagem invisível — pela enésima vez, numa
  tela que as limpezas das parcelas 62/64 não alcançaram**: `Visibility="{Binding
  MensagemEhErro}"` esconde junto a mensagem de ÊXITO, que zera o booleano. Minha
  confirmação nova ("Pacote cadastrado no catálogo") nasceria invisível. **Quem decide se
  aparece é o TEXTO; quem decide a cor é a GRAVIDADE** — e o padrão volta na tela N+1 porque
  o par `AlertaPerigo` + `MensagemEhErro` continua sendo o que a mão escreve primeiro.

- **Três armadilhas de XAML que a releitura do próprio diff pegou** (set/2026, mesma
  parcela): `ChipFiltro` é `TargetType="ToggleButton"` e eu havia escrito um `<CheckBox>` —
  estilo com `TargetType` que não bate **não é aplicado**, e o chip sairia com a cara de
  caixa de seleção do sistema; `BooleanToVisibilityConverter` **ignora o
  `ConverterParameter`**, então "mostre quando estiver VAZIO" não se escreve invertendo o
  conversor (é `DataTrigger`, como o resto da suíte); e um `_` como parâmetro de lambda
  colide com o descarte quando o corpo também usa `_` — o `compilar-sombra` acusou o
  `CS0029` em segundos, sete minutos antes de o CI o acusar.

- **Classe de tela Blazor nova leva o PREFIXO da tela: `entrada`, `saida` e `dica` já são do
  comum** (out/2026, mockup `docs/mockups/whatsapp-nativo/`). As bolhas da conversa nasceram
  `.conv-m.entrada` / `.conv-m.saida` e o atalho do teclado `.dica` — três nomes que o CSS
  comum já usa: `.entrada` é a TELA DE ENTRADA (login) em `moldura.css`, com
  `min-height:100vh`, e `.dica` é o tooltip ABSOLUTO do gráfico em `clinica.css`. Resultado:
  cada mensagem recebida virou uma caixa da altura da janela e "Ctrl + Enter envia" apareceu
  flutuando sobre o cabeçalho. Nenhuma rede acusa (é CSS válido); só a foto. Regra: toda
  classe que uma tela cria leva o prefixo dela (`conv-`, `ctx-`, `ficha-`), e antes de usar
  um nome curto, `grep -n "^\.nome\b\|\.nome{" Ativos/css/clinica.css Ativos/css/telas/moldura.css`.

- **Arquivo novo da prévia que uma SEMENTE usa entra também no `ValidarLayoutWindows.csproj`**
  (out/2026, WhatsApp nativo). As sementes de `tools/PreviaBlazor/Sementes/*.cs` são compiladas em
  TRÊS lugares: a prévia, o `Clinica.Tests` e o `tools/ValidarLayoutWindows` (que o CI do Windows
  roda). A semente do WhatsApp passou a usar classes do `CrmDeMentira.cs`; o agente o incluiu no
  `Clinica.Tests.csproj` e esqueceu o validador. Prévia e testes verdes, CI vermelho. Só o
  `compilar-sombra` acusou. E o validador não tem `using System.IO` implícito. Antes de commitar
  uma semente que usa arquivo de fora de `Sementes/`: `grep -rn "PreviaBlazor/Sementes" --include=*.csproj`
  e inclua o arquivo em todos.

- **`textarea` herda a fonte da casa pela regra COMUM, não por remendo de tela** (out/2026,
  WhatsApp nativo). O `clinica.css` fazia `button,select,input{font:inherit}` e deixava o
  `textarea` de fora: o navegador desenha o campo de várias linhas (e o texto de exemplo dele) em
  fonte de máquina de escrever. Duas telas já tinham remendado localmente
  (`fat-pendencias.css`, `configuracoes.css`) e cada formulário novo repetia o defeito. A regra
  comum agora inclui o `textarea`; os remendos locais ficaram inofensivos. Defeito que aparece
  em DUAS telas é da regra comum: conserte lá.


- **Parâmetro `string` de componente Razor recebe expressão com `@`; sem ela recebe o TEXTO**
  (out/2026, WhatsApp nativo, "Cadastrar com este telefone"). Em `PacienteId="f.PacienteId"` o
  Razor avalia a expressão porque o parâmetro é `int?`; em `TelefoneInicial="f.TelefoneInicial"`,
  com o parâmetro `string`, ele passa a frase literal `"f.TelefoneInicial"` — compila sem aviso e
  o campo do cadastro abriu com esse texto escrito. Só o teste bUnit que lia o `value` do campo
  pegou. Regra: atributo de componente que leva expressão começa com `@` sempre
  (`TelefoneInicial="@f.TelefoneInicial"`), e parâmetro novo `string` nasce com um teste que lê o
  que ele mostra.

- **Semente com data relativa a "hoje" respeita a JORNADA de quem tem agenda protegida**
  (out/2026). A sessão do Dr. Caio nas férias dele era `hoje + 20` às 15h; ele só atende terça e
  quinta, e no Postgres o gatilho `CK_Agenda_Protegida` recusava a semente em cinco dias de cada
  sete. O SQLite não tem o gatilho, então a prévia e o CI nunca viram. Regra: horário semeado para
  profissional com `AgendaProtegida` escolhe um dia da jornada dele (procure o dia, não some um
  número fixo), e o horário que "já estava marcado" antes de um bloqueio é gravado num
  `SaveChanges` ANTERIOR ao do bloqueio. Para conferir: a família com `CLINICA_TESTES_POSTGRES` e
  `faketime` em cada dia da semana.
