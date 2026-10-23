# Regras de negócio que não são óbvias pelo código

Movido do `CLAUDE.md` em out/2026 (o arquivo passava de 9.700 linhas e era carregado inteiro em toda
sessão). O texto é o MESMO, sem reescrita: cada tópico é a decisão, o defeito que ela evitou e onde
mora o código. Mexer numa área? Leia antes o tópico dela — vários registram armadilhas que nenhum
teste pega. Lição nova de uma área entra aqui, no tópico dela; regra que vale para TODA mudança
continua no `CLAUDE.md`.

## Índice (183 tópicos, na ordem do arquivo)

Procure a palavra da área (`grep -n "palavra" docs/agentes/regras-de-negocio.md`) e leia o tópico inteiro antes de mexer nela.

- Faturamento ≠ recebíveis
- A SUÍTE PASSOU A RODAR CONTRA UM POSTGRES DE VERDADE — e a primeira rodada achou TRÊS
- A AMARRA ENTRE O HORÁRIO E A SESSÃO, e a pergunta que ela produz
- O CIRCUITO DO PARTICULAR — a Recepção é o centro, e o dinheiro de quem não tem
- ESTORNAR UM ATENDIMENTO
- A BUSCA DE PACIENTE VIROU A TELA
- O SELETOR DE PACIENTE DEIXOU DE DESPEJAR O ALFABETO
- LANÇADOS HOJE VIROU A ABA "LANÇAMENTOS"
- A GUIA QUE VAI NASCER, DESENHADA
- SEM CONVÊNIO ESCOLHIDO NÃO NASCE ATENDIMENTO
- O número da guia tem FORMA, e ela é do convênio
- Permissão granular no faturamento, e o login que a torna útil
- O atendimento saiu do faturamento e foi para o balcão
- Consultar guias filtra por modalidade e especialidade
- Rodar as pendências
- Mapa corporal e protocolo
- Documento clínico é fato
- Pacote é venda da clínica, cota é do convênio
- Fechamento da sessão
- Taxa de cartão e imposto
- Contas a pagar e a receber
- Fluxo de caixa
- Fechamento de caixa
- Regime tributário
- Recebíveis de cartão
- Custo de transação
- Retenção na fonte por convênio
- Tabela de preço por convênio
- Rentabilidade por convênio
- Tabela de preço por convênio
- Auditoria
- Painel da direção
- Inadimplência
- Central de documentos
- Repasse
- Estoque
- Capacidade sem porta
- Meta e teto são a mesma ideia aplicada aos dois lados
- Apuração por tributo e resultado do mês
- O papel que sai da agenda
- Aniversário e padrão de falta
- Acerto de inventário
- Quem parou de vir
- O consultório do profissional
- O Consultório não é uma ilha
- A folha de infusão e a checagem de enfermagem
- A certificação digital, e por que NÃO se escaneia o carimbo
- O documento que SAI da clínica, e a lei que manda nele
- Prescrever o alérgeno que a própria clínica anotou
- Botão aceso que não faz NADA
- O nome do enum vazando para a tela
- Botão ao lado de campo usa a ALTURA do campo
- A sidebar do Consultório tinha TRÊS itens
- A chamada do próximo paciente
- O que o profissional precisa ver e não alcança
- Medidas seriadas
- Lista de problemas
- Tela que abre vazia é tela inacabada
- Chave de navegação sem item de menu = botão que não faz NADA
- Lista → tela do item. Não enfie mestre-detalhe numa tela só
- Ferramenta de uso pontual mora em BOTÃO, não em painel aberto
- A ficha do paciente é LISTA → TELA DO ITEM, e as abas têm estilo
- O lançamento avulso é TELA CHEIA — nem faixa lateral, nem pop-up
- Tela da suíte que precisa buscar ao abrir declara `ICarregarAoAbrir`
- A Recepção estava completa em FEATURE e furada em PORTA
- Permissão granular que não distingue o que a clínica distingue não é granular
- Cadastro grudado ao lado da operação é faixa lateral
- Dica ESTÁTICA não é aviso: o campo precisa criticar a cada tecla
- A rede que não cobre o app em PRODUÇÃO
- Acessos no faturamento, e por que a trava é o BIT e não o perfil
- Valor de propriedade que o WPF valida em RUNTIME passa pelas quatro redes
- Estilo de PARÁGRAFO não serve de CÉLULA de tabela
- O nome do convênio é da OPERADORA; o enum é da REGRA
- Sobreposição posta como IRMÃ desaba a tela inteira
- Login sem saída faz a auditoria assinar o nome errado
- Resposta de banco que chega fora de ordem é "não atualiza"
- `TabPanel` ENCOLHE as abas; `WrapPanel` não
- Cartão de altura FIXA precisa de teto no texto
- Texto que estoura é dado do BANCO sem quebra nem reticências
- O prontuário NÃO SE APAGA, e "alterar" sem guardar o anterior é apagar devagar
- Controle de acesso responde "quem PODE"; só a trilha responde "quem FEZ"
- Ferramenta não é política
- O QR da receita e a escolha do provedor não podem estar no mesmo caminho crítico
- Credencial de serviço externo mora no BANCO, com o ambiente podendo sobrepor
- A suíte chamava o snackbar 143 vezes e nunca teve onde mostrá-lo
- "Consigo escrever?" e "dá para LER pela URL?" são testes diferentes
- O TERMO LIGADO À SESSÃO — e por que o elo é o HORÁRIO, não a evolução
- ⛔ ANTES DE ESCREVER QUALQUER XAML, LEIA A REGRA DE LEIAUTE NO `README.md`
- A 5ª reprovação: faixas empilhadas comem a tela
- Cada retângulo a mais é uma costura a mais
- `TextoSuave` alinha à ESQUERDA, e isso anda junto do `MaxWidth`
- O mapa corporal e a emissão de documento moram no SHELL
- Escalas clínicas por especialidade
- Como os módulos se falam
- A glosa que voltava para ninguém
- O que os outros módulos mandam ao balcão
- A consulta a renovar chega ao horário
- A Recepção no balcão
- LGPD além do consentimento
- Recibo e orçamento
- Foto do paciente
- Acesso (parcela 5)
- A permissão tem DUAS barreiras, e as duas são obrigatórias
- Quem assina a ação é quem fez login
- Campanhas (parcela 5)
- Indicadores (parcela 5)
- O lote TISS é POR OPERADORA, e a guarda de duplicidade era o que tornava o erro
- A cópia que ficou para trás é onde a permissão vaza
- Excluir paciente era a última porta que apagava prontuário
- O descarte de resposta fora de ordem virou padrão de TODA carga async
- A sala executa o que não alcançava imprimir
- Mover a fila é UM ato com UMA regra nos dois quadros
- `BooleanToVisibilityConverter` sobre STRING é `Collapsed` para sempre
- A trilha de leitura cobre a PORTA que abre o dado, não só a tela clássica
- O painel de pendências diz DE QUÊ elas são
- O circuito clínico é testado de ponta a ponta
- Nenhum defeito da Recepção QUEBRAVA nada — e é por isso que eles chegaram até aqui
- A varredura de "capacidade sem porta" achou o contrário: código morto que DUPLICA a
- O que a auditoria de features achou, e o padrão que se repete
- Nome de propriedade errado em controle da CASA: só o compilador de MARCAÇÃO pega
- A LINHA "Personalizado" DO CATÁLOGO É RENOMEÁVEL, e isso vazava para TODA operadora
- Tabela empilhada em `StackPanel` sem rolagem: o fim da tela é CORTADO, sem barra e sem
- Duas caixas editando o MESMO campo é o que faz uma tela "não se entender"
- O CONSENTIMENTO ERA UMA CAIXINHA — e a única prova era a palavra de quem clicou
- LIGAR UM PORTÃO NOVO FAZ O DADO ANTIGO SATISFAZER A CONDIÇÃO NOVA
- LISTA QUE ROLA POR DENTRO COME A RODA DO MOUSE, E A PÁGINA NÃO ROLA
- LISTA SEM TETO CRESCE COM O DIA — e é a metade que só quebra DEPOIS de lançar
- A MESMA RODA COMIDA EM MAIS DEZESSEIS LUGARES — e metade foi CRIADA por uma correção
- `CornerRadius` 999 no WPF desenha um OVO, não uma pílula
- AS TELAS PLANAS DE PRONTUÁRIOS E EXAMES, e a situação que só existe se houver FATO
- O LAUDO EM ARQUIVO, e por que ele não virou "anexo"
- A IMPORTAÇÃO DA CARTEIRA DO SISTEMA ANTERIOR — e a auditoria que ficaria pendurada
- O PACOTE DO SMART CLINIC — "não perder NADA", e o que isso decidiu
- O NOVO ATENDIMENTO RECONHECE O HORÁRIO DO DIA — lançar SOBRE ele, nunca ao lado
- O ACERVO DE ARQUIVOS DO SISTEMA ANTERIOR, e por que ele não virou "anexo"
- O PRONTUÁRIO DA SESSÃO: a aba duplicada, a sessão que não abria e o papel dela
- A FOLHA DA SESSÃO VIROU COMPONENTE — e o modal do balcão tinha QUATRO dos doze campos
- A BUSCA DE PACIENTE VIROU COMPONENTE, E A TELA DEIXOU DE CONSULTAR AO ABRIR
- A COLUNA DE ORIGEM FOI ALARGADA E A DE DESTINO NÃO — e o Postgres recusa a gravação
- A FOLHA ÚNICA: doze campos viraram um, e nenhuma coluna foi apagada
- A FAIXA LATERAL DE RELEITURA VIROU ABA — e o rótulo saiu de dentro do texto
- "Por que numa tela dá e na outra não?" — quando as duas telas são o MESMO código, a
- ATENDER É UM CLIQUE, E FINALIZAR ENCERRA O ASSUNTO — a simplificação do fluxo do
- A SIDEBAR DO CONSULTÓRIO NUNCA TINHA USADO AS SUB-ABAS DO SHELL
- O "MEU DIA" DO MÉDICO VOLTOU A SER LISTA — e não é a lista que a parcela 38
- O RAIL DA TELA DO PACIENTE: dez seções viraram oito, e a checagem 38 decidiu COMO
- "QUANTO MAIS SIMPLES, MELHOR" — o Novo atendimento em duas abas e o cartão da fila
- "UMA FOLHA, DOIS LADOS" — o atendimento remodelado, e o que a extração de componente
- A CONFERÊNCIA PEDIDA PELA DIREÇÃO SOBRE A AGENDA — três defeitos com 2261 testes
- A EVOLUÇÃO NASCE COM O NÚMERO DO ATENDIMENTO — o mesmo que a recepção gera
- O DINHEIRO DO BALCÃO: o alerta que não tinha porta, a receita órfã e o preço que não
- AS TRÊS PEQUENAS DA AGENDA — a data que não reconferia, as férias invisíveis e os três
- A MÍDIA DO PRONTUÁRIO E OS CAMPOS QUE ESTA CLÍNICA ANOTA
- A WEB DE LEITURA — o dia, o mês e a ficha de fora da máquina
- A AGENDA COM COR — a família no traço, no avatar e no cartão; o estado na pílula; o
- A TELA APROVADA CONSTRUÍDA, e a idade impossível em OITO cópias
- A TRAVA DAS MIGRATIONS PAROU A CLÍNICA — trava sem prazo é trava que alguém esquece
- DDL de migration só alcança as tabelas cujo DONO é o papel do desktop
- O build do PR roda TUDO EM PARALELO
- A suíte roda em SEIS FATIAS no CI, divididas pelo TEMPO MEDIDO
- A PONTE DO CRM NA VPS: o que só a montagem real mostrou
- O usuário do banco da ponte do CRM grava SÓ nas tabelas que a ponte grava
- Triggers usados pelo portal devem funcionar com seu papel restrito.
- Navegação superior e largura útil nos cinco aplicativos (17/09/2026, correção da cliente).
- O PAPEL SAI DA SESSÃO SEM SAIR DA SESSÃO — e o documento nascia AVULSO
- A REVISÃO DA PARCELA: quatro achados que as três redes deram por verdes
- QUEM COLHE NÃO ALCANÇAVA O QUE COLHEU — o termo assinado que sumia da recepção
- A ASSINATURA CHEGAVA E MORRIA NA VARREDURA DE 24 H — porque ela só existia no BALDE
- A CORREÇÃO PAROU NUMA PORTA E CINCO CONTINUARAM DIZENDO O CONTRÁRIO
- UMA REGIÃO TEM DUAS BARREIRAS: a que a CARREGA e a que a DESENHA
- VENDER PACOTE: a lista de pacientes tinha 8 px, e sem escolher o paciente não se vende
- DOCUMENTOS EM TRÊS PASSOS — e o passo 3 só promete o que entrega
- "DOIS OU MAIS CAMINHOS LEVAM AO MESMO LUGAR" — o recall tinha TRÊS motores e a agenda
- A RODADA DE UI DA RECEPÇÃO — quatro telas refeitas a partir de mockup aprovado, e o
- O LAUDO MÉDICO, e o que "alterar" quer dizer num documento que não se reescreve
- A TELA DE DOCUMENTOS É UMA SÓ EM TODOS OS MÓDULOS — e tirar o navegador não podia
- 🎨 O FINANCEIRO INTEIRO VIROU BLAZOR — e a migração achou defeito que o WPF escondia
- 🎨 O FATURAMENTO DE GUIAS VIROU BLAZOR — e "nada se perde" foi medido, não prometido
- A COMISSÃO DE RECUPERAÇÃO É APURADA DOS FATOS, NUNCA MARCADA
- O CANAL DE SMS, a confirmação por link e "não vem" como resposta cotidiana (SAIU na fase 3)
- A FASE 3 RECEPÇÃO×CRM: autorização de mensagens no termo do BSV, recall com trava, um PARAR só
- A RESPOSTA DO PACIENTE VIRA AVISO NO SINO E NO PAINEL DA DIREÇÃO
- "JORNADA" ERA TERMO DE RH, E O DESENHO NÃO CABIA A SEMANA REAL
- TENDÊNCIA E METAS POR RECORTE

---


- **Faturamento ≠ recebíveis**: "baixa" = a secretária efetivou a guia no sistema do convênio; nunca
  adicione campos de dinheiro/pagamento.
- **A SUÍTE PASSOU A RODAR CONTRA UM POSTGRES DE VERDADE — e a primeira rodada achou TRÊS
  defeitos de produção com 2310 testes verdes** (set/2026, item 1 da lista "o que falta
  para ficar profissional"). Os testes rodam em SQLite, e "só o Postgres pega" já tinha
  custado à clínica o `xmin`, as seis datas com fuso da parcela 52 e o 22001 do "Imprimir
  esta sessão" — cada um com uma rede própria escrita DEPOIS do estrago. A rede genérica
  era o job `testes-postgres` do `verificar.yml` (que saiu do CI em out/2026 — hoje roda
  LOCAL, com a variável `CLINICA_TESTES_POSTGRES` e a MESMA suíte, antes de todo push que
  mexa em migration, coluna ou consulta).
  ⚠️ **O interruptor tem o MESMO nome do método do EF de propósito.** Os 147 arquivos de
  teste montam o banco com `UseSqlite(conn)` + `EnsureCreated()`, sem fixture comum; um
  helper `BancoDeTeste.Abrir()` seria contrato que depende de alguém lembrar, e o teste
  escrito daqui a seis meses com o padrão de sempre ficaria fora da rede sem ninguém
  notar. `BancoDosTestes.UseSqlite` mora no namespace `Clinica.Tests`, e o C# procura
  extensão do namespace mais interno para fora — a do EF só entra se esta não servir. Com
  a variável, ela aplica as migrations UMA vez num banco-modelo (é isso que prova a
  migration escrita à mão, que o SQLite nunca executa) e dá a cada teste uma cópia
  (`CREATE DATABASE … TEMPLATE`), apagada quando a `SqliteConnection` do fixture é
  descartada. **A mesma conexão é o mesmo banco**: os testes de "escopo separado como em
  produção" abrem um segundo contexto sobre a conexão do fixture, e sem a tabela de
  conexão→banco cada chamada criaria um banco novo e o segundo contexto leria o vazio.
  Os três defeitos, e o que cada um ensina:
  (a) **O texto aprovado do TCLE do BSV (parcela 84) tem mais de 4.000 caracteres e
  `ModelosDocumento.Corpo` era varchar(4000)** — o botão "Criar os termos do BSV" levaria
  um 22001 na clínica. `DocumentosClinicos.Corpo` foi junto porque é a CÓPIA (a lição da
  `FichaDaSessaoCabeNaColuna`, duas migrations antes, cobrada de novo). O teste que fixa
  lê os tetos do MODELO e mede os textos da `ModelosTermoBsv` contra a origem E o destino.
  (b) **A restauração de backup NUNCA tinha rodado num Postgres.** Dois motivos, ambos
  invisíveis no SQLite: as migrations SEMEIAM convênios, modalidades e especialidades,
  então "base vazia" nunca foi verdade numa instalação real e a restauração recusava toda
  base recém-instalada ("Convenios tem 4 registro(s)"); e o backup grava tudo como texto
  e mandava o texto cru de volta — o Postgres não converte parâmetro `text` para coluna
  `integer` sozinho ("42804"). Agora os catálogos semeados não contam como dado da
  clínica (`BackupService.TabelasSemeadasPelaInstalacao`) e são esvaziados antes da
  restauração, e cada valor volta TIPADO pelo modelo do EF (`Retipar`, o inverso exato
  do `Converter`). **Teste de restauração que nunca restaurou no banco de produção é o
  "backup que ninguém sabe restaurar" com um teste verde ao lado.**
  (c) **A tradução de erro do banco tem frase própria no Postgres e não tem no SQLite.**
  O teste da mensagem de gravação provocava uma chave estrangeira quebrada e afirmava
  "O banco respondeu" — no Postgres o 23503 vira `VinculoQuebrado`, que é o certo. O
  teste passou a afirmar os dois desfechos, um por banco, em vez de escolher um.
  ⚠️ **Teste que usa a `SqliteConnection` do fixture para SQL cru lê OUTRO banco** quando a
  suíte roda no Postgres: a conexão do fixture é só o gancho, e o contexto aponta para o
  banco copiado. SQL cru sai por `_db.Database.GetDbConnection()`, com identificadores
  entre aspas — o Postgres dobra `DocumentosClinicos` sem aspas para minúsculas.
  ⚠️ **Não é substituto do SQLite**: 3 min contra 10, e é por isso que o job roda em
  PARALELO e não em série. Quando os dois discordam, o Postgres é quem diz a verdade da
  clínica — e a pergunta certa é "qual dos dois está medindo o banco de produção".
- **A AMARRA ENTRE O HORÁRIO E A SESSÃO, e a pergunta que ela produz** (parcela 93). A
  clínica não trabalha o check-in pela agenda: a recepcionista vai direto ao Novo
  atendimento. A parcela 91 fez o lançamento reconhecer o **horário do dia** e nascer
  pendurado nele (`AgendaService.LancarNoHorarioAsync`) — mas só quando a **data bate**, e a
  agenda importada do Smart Clinic está nas datas do sistema antigo. O que não bate vira
  encaixe, e o horário fica "Aguardando" para sempre: infla a ocupação, entra no "Meu dia"
  do médico e **captura a evolução importada** (distribuída pela ordem da hora marcada),
  deixando a sessão real em "Sessões sem evolução".
  `ConciliacaoAgendaService` é a LEITURA que transforma isso em fila de trabalho, e ela não
  decide nada: ausência de clique não é prova de falta.
  ⚠️ **A pergunta tem TRÊS respostas, e a do meio é a maior do backlog.** Faltou
  (`MarcarFaltaAsync`) · aconteceu e ninguém lançou (`LancarNoHorarioAsync`, que já data o
  atendimento pela data **do horário**, não pela de hoje) · **aconteceu e já foi lançada por
  fora** — e aqui lançar criaria um SEGUNDO jogo de guias para a mesma sessão. Por isso a
  linha carrega `HorarioParado.TemSessaoNoDia` e o botão de lançar fica **apagado**, com a
  frase ao lado (botão cinza sem explicação vira "o sistema travou", parcela 41).
  **Encerrar esse horário pediu um `StatusAgendamento` NOVO, e ele existe desde set/2026:
  `Substituido`** (item 2 da lista "o que falta para ficar profissional"). `Cancelado` é
  contado por `IndicadoresService` e `RelacionamentoService` (uma sessão que ACONTECEU
  inflaria o indicador de cancelamento), `Faltou` culparia o paciente, e `Realizado`
  dobraria a ocupação e o repasse (o atendimento já está pendurado no encaixe). Quem
  aponta para a sessão é `AtendimentoSubstitutoId` (coluna aditiva), NUNCA `AtendimentoId`:
  o backfill de `RealizadoEm` só carimba atendimento cujos horários estão todos em
  Realizado, e um segundo horário apontando por aquela coluna o deixaria de fora para
  sempre. `AgendaService.SubstituirPorSessaoAsync` recusa sessão de outro paciente, de
  outro dia, estornada e horário que não está em aberto; suspende as guias PRÓPRIAS do
  horário (chave "guia no agendamento") pelo mesmo caminho da falta; e o Remarcar reabre
  e solta o vínculo.
  ⚠️ **As comparações NEGATIVAS foram o custo, e a saída foi trocá-las pela lista
  POSITIVA.** Eram 120 usos em 28 arquivos; os `is not (Cancelado or Faltou)` viraram
  `OcupaAgenda` (agendado ou realizado) em memória e `Status == Agendado || Status ==
  Realizado` nas consultas traduzidas (o EF não traduz a derivada). `StatusDaFila.ForaDaFila`
  e `SessaoDoDia.ForaDoDia` passaram a ser a NEGAÇÃO dessa lista: o próximo status cai do
  lado de "não ocupa" sem ninguém lembrar. Os que ficaram por extenso são os dois que
  excluem SÓ o cancelado de propósito (a lista da Enfermagem e a entrega do paciente ao
  posto, que mantêm a falta) — ali o substituído entrou nomeado.
  ⚠️ **Valor novo de enum é o que o binário ANTERIOR não lê** (parcela 67): um app velho
  lendo um dia com um horário `Substituido` perde a consulta inteira. O valor só é escrito
  pela conciliação — tela da mesma versão — e a release dos cinco apps é conjunta; o que
  isto pede é que a conciliação do backlog só comece DEPOIS de os cinco apps terem
  atualizado, e está escrito no enum.
  ⚠️ **`WithMany()` no mapeamento de `Agendamento.Atendimento` NÃO é descuido, e trocar por
  `WithOne()` NÃO é de graça**: o EF exige índice ÚNICO para o dependente 1‑1, e migration
  roda na ABERTURA do app — índice único que falha na criação é o faturamento não abrindo.
  Conte as duplicatas pela conciliação antes.
- **O CIRCUITO DO PARTICULAR — a Recepção é o centro, e o dinheiro de quem não tem
  convênio só entra por ela** (set/2026; a cliente: *"agora precisamos controlar e gerir
  tudo que for particular… as infos dos pacientes particular são enviadas ao módulo
  financeiro para que seja conciliado e seja feito o caixa"*). Medido antes de escrever:
  o circuito do CONVÊNIO estava fechado (guia → conciliação → caixa) e o do particular
  tinha **três buracos, nenhum dos quais falhava** — 2266 testes verdes.
  (a) **A venda de pacote não movia dinheiro.** `VenderAsync` gravava o pacote e mais
  nada; `PacotePaciente.LancamentoFinanceiroId` existia desde a parcela 4 **sem um único
  escritor em produção** — campo gravável sem escritor, a variante espelhada do dado sem
  leitor. Agora a venda exige a decisão (`PagamentoDaVenda`: à vista, ou entrada +
  parcelas mensais contadas do PRIMEIRO vencimento) e grava os lançamentos no MESMO
  `SaveChanges`, apontando para o pacote por `LancamentoFinanceiro.PacotePacienteId`
  (coluna aditiva). ⚠️ **Quem aponta é o lançamento, não o pacote**: a venda parcelada
  tem vários, e a coluna antiga só cabe um — ela fica, pela regra aditiva.
  (b) **O particular de primeira vez nunca era perguntado sobre dinheiro.**
  `SugereLancamento = pacote is null && valor is not null` só valia com HISTÓRICO de
  recebimento; sem ele `TemDecisao` dava falso, a janela não abria e a tela dizia
  "concluída — particular, sem guia a faturar". A sessão ficava registrada e sem uma
  linha de dinheiro. Agora `PropostaFechamento.EhParticular` (o convênio da ficha não
  gera guia) faz a pergunta SEMPRE, e ela tem **duas respostas**: pago agora, ou **fica
  a receber** com vencimento (`DecisaoFechamento.FicaAReceber` → conta prevista com dono,
  vencimento e `AtendimentoId`, via `ContasService`). O convênio sem pacote continua sem
  janela: o dinheiro dele vem pela guia.
  (c) **Não havia conciliação do particular.** `GuiasSemLancamentoAsync` é inacessível a
  ele por construção (`CodigosNoPeriodoAsync` exclui o `NaoAplicavel`). Nasceu
  `SessoesParticularesSemReceitaAsync` — realizada, não estornada, todos os códigos
  `NaoAplicavel`, sem lançamento de entrada vivo e sem sessão de pacote — como aba
  **Particulares** da Conciliação, com o preço da tabela do particular quando a direção o
  cadastrou. A linha sai por passar a TER lançamento — recebido OU a receber —, nunca por
  alguém marcá-la. E ela chega ao balcão: `ElegibilidadeService`
  avisa (amarelo) a sessão anterior sem dinheiro, porque é com o paciente na frente que
  se resolve.
  ⚠️ **Repasse só conta o REALIZADO.** `LancamentosDosAtendimentosAsync` devolve tudo o
  que não está cancelado, e até aqui toda entrada ligada a atendimento era realizada.
  Com o "fica a receber" passou a existir a PREVISTA — e o repasse pagaria o profissional
  por dinheiro que o paciente ainda não trouxe. **Ao criar um status novo num dado que já
  tem leitores, releia cada leitor perguntando se ele filtrava por status ou por
  acidente.** O estorno já filtrava certo (cancela a conta prevista junto — o certo).
  ⚠️ **`{Binding X}` não alcança propriedade ESTÁTICA** — as formas de pagamento da linha
  da conciliação nasceram `static` e o combo sairia vazio, sem erro nenhum. Pego na
  releitura do diff. E o `[RelayCommand]` fica colado ao método: um helper inserido entre
  o atributo e o método foi o único erro que o `compilar-sombra` acusou nesta parcela —
  rede que roda antes do push é rede que pega antes do CI.
  ⚠️ **O PREÇO DO PARTICULAR É OUTRA TABELA, com outra chave** (pedido da direção no
  mesmo dia: *"um cadastro de preço por tipo de especialidade atendida quando o paciente
  for particular"*). A primeira versão lia a tabela POR CONVÊNIO com o código "Particular"
  — e ela é chaveada pelo TIPO DE GUIA, conceito do faturamento que o particular não
  tem: o preço da sessão saía do primeiro código `NaoAplicavel`, uma indireção que
  ninguém entenderia na tela do Gerente. `PrecoParticular` é chaveado pelo que a clínica
  FAZ, nas palavras dela: **modalidade** (código do catálogo, obrigatória) +
  **especialidade atendida** (código do catálogo, opcional — é o que separa a consulta de
  psiquiatria da de geriatria; em branco vale para todas). A mais específica ganha
  (especialidade > genérico; código de variante > família), tem vigência, e é PROPOSTA:
  copiada no lançamento, com desconto no clique. Cadastro na aba **Particular** da Tabela
  de preço do Gerente; leitores no Finalizar e na Conciliação, pelo MESMO serviço — dois
  leitores com duas regras proporiam dois números para a mesma sessão. Modalidade e
  especialidade estão no HORÁRIO, então a proposta não precisa mais esperar o atendimento
  existir.
  ⚠️ **E o cadastro é da RECEPÇÃO também** (*"o ideal seria a recepção também cadastrar e
  editar preços"*). A primeira versão nasceu como aba dentro do item do Gerente — a porta
  no módulo de quem não usa, pela enésima vez. A tela SUBIU para o shell
  (`PrecosParticularView`), como o Pacotes: a Recepção a publica como item e o Gerente como
  aba do composto "Tabela de preço" (Convênios · Particular), os dois pela MESMA chave
  (`ChavesSuite.PrecosParticular`). **A permissão é `VenderPacote` OU `EditarFinanceiro`,
  e não um bit novo**: combinar preço é o ato que `VenderPacote` já nomeia (o rótulo dele
  passou a dizer isso), e o enum tem UM bit sobrando antes de virar `long` numa coluna de
  produção — gastá-lo numa caixinha que o bit existente já cobre seria pagar a migration
  mais cara do sistema por nada.
  ⚠️ **E O CATÁLOGO DE PACOTES SEGUIU A MESMA DECISÃO** (*"e como ficou a questão dos
  pacotes?"*). A tela de Pacotes já era do shell e a Recepção já a publicava (parcela 60),
  mas o CATÁLOGO — o que está à venda e por quanto — exigia `EditarFinanceiro`, com o
  argumento de que "mudar o preço vale para todo mundo": a recepcionista vendia e não
  cadastrava. É o mesmo argumento que a direção acabou de derrubar para o preço do
  particular, e o preço de tabela do pacote é o mesmo ato — duas tabelas de preço do
  particular com dois donos divergiriam na primeira correção. Passou a `VenderPacote` OU
  `EditarFinanceiro`; **cancelar a venda continua só do Financeiro** (desfaz o dinheiro que
  outra pessoa registrou). E **não existia EDITAR um pacote do catálogo, para ninguém** —
  só Novo e Excluir: reajustar preço era excluir a linha e criar outra, que nasce com outro
  Id e SOLTA a procedência (`PacotePaciente.PacoteCatalogoId`) de tudo o que foi vendido
  dela. A janela de edição manda objeto NOVO com o Id, e carrega o que ela NÃO mostra
  (ordem, observações, ativo) para devolver intacto — a cópia campo a campo do
  `SalvarCatalogoAsync` apagaria o que chegasse nulo (lugares 3 e 6). O teste faz o mesmo
  caminho da tela (parcela 74: entidade rastreada reenviada não testa a cópia).
  **O que ficou de fora, com o motivo**: o lançamento manual do Caixa continua sem
  seletor de paciente (a receita órfã não liga à sessão nem some da conciliação — a
  correção é dar-lhe o `SeletorPacienteViewModel` da conta a receber); a cobrança de
  parcela vencida usa a porta que já existe ("Quem me deve", WhatsApp e Receber, no
  Financeiro) — o balcão vê o aviso e ainda não RECEBE dali; e a prévia da guia do Novo
  atendimento ainda não mostra o preço do particular — ele aparece no Finalizar, que é
  onde a cobrança acontece.
- **ESTORNAR UM ATENDIMENTO** (parcela 94). O `RemarcarAsync` recusava horário realizado
  dizendo "Estorne o atendimento antes" — e **não havia estorno nenhum**: a instrução
  mandava fazer o que não existe, e a saída que sobrava era o `Cancelar`, sem trava.
  O motivo de não existir é que o lançamento tem **cinco efeitos em três serviços**
  (atendimento+códigos · NCs reabertas · consulta renovada · carimbo do horário · pacote,
  insumo e caixa) e só um é a guia. `EstornoAtendimentoService` **pergunta item a item** —
  as guias saem sempre; caixa, pacote e insumo só se marcados. Recusa quando o fato já saiu
  da clínica (guia baixada, em lote TISS ou em NC), reusando a trava do
  `AjustarAoRemarcarAsync`. Bit `LancarAtendimento`: com aquela recusa, o que se desfaz é
  guia que ainda **não** saiu — corrigir o próprio erro na hora.
  ⚠️ **NUNCA apaga.** `OnDelete(SetNull)` transformaria um `Remove` em horário ÓRFÃO — o
  estado de 12/08/2026. O atendimento fica marcado (`EstornadoEm`) e os códigos vão para
  `NaoAplicavel` com a `MarcaEstorno`.
  ⚠️ **A ARMADILHA, e por que `EstornadoEm` existe.** O estorno SOLTA o horário (para a
  sessão poder ser relançada limpa — sem soltar, o `ConfirmarNucleoAsync` reaproveitaria o
  atendimento anulado e a sessão nova nasceria sem guia faturável, em silêncio). Só que
  `MarcarAtendimentosSemCarimboComoRealizadosAsync` carimba como realizado todo atendimento
  sem carimbo **que não tenha horário em outro estado apontando para ele** — e sem horário
  nenhum, o estornado seria RESSUSCITADO na próxima ativação da chave "guia no
  agendamento", voltando a contar em BI, retenção e origem. A coluna entra nos DOIS filtros
  do backfill; `EstornoDeAtendimentoTests.O_backfill_NAO_ressuscita_atendimento_estornado`
  é o que amarra isso.
  ⚠️ **NÃO desfaz a consulta renovada** (`StatusConsulta` não tem "cancelada"; desfazer
  exigiria ressuscitar a anterior, e receita emitida sob a nova ficaria inválida) **nem as
  NCs reabertas** (o paciente apareceu de verdade). As duas são decisão escrita, não
  esquecimento.
  ⚠️ **`Sum(x => (decimal?)x.Valor)` sobre sequência VAZIA devolve 0, não nulo** — foi assim
  que a prévia ofereceu "desfazer entrada de R$ 0,00" num atendimento sem caixa. Conte
  primeiro (`lista.Count > 0 ? lista.Sum(...) : null`). Um teste pegou; a lição fica.
- **A BUSCA DE PACIENTE VIROU A TELA** (set/2026, mockup 05 aprovado pelo cliente).
  Enquanto não há paciente escolhido, o Novo atendimento tem UM assunto — quem — e passou a
  assumir isso: o cabeçalho da página e o passo "1 · QUEM" somem, e no lugar entra uma coluna
  de **760 px CENTRADA** com a pergunta, a busca e o resultado. Largura explícita, **sem
  nenhuma coluna estrela na composição**, então não há deserto possível.
  ⚠️ **`HorizontalAlignment="Left"` NÃO é como se limita largura de conteúdo.** A tentativa
  anterior fez exatamente isso num Grid com coluna `*`, e piorou os dois lados de uma vez: com
  esse alinhamento o WPF arranja o elemento pela largura DESEJADA, a estrela encolhe até o
  conteúdo, e a tabela ficou estreita **e** o deserto continuou. Limite por medida
  (`Width`/`MaxWidth`) no container que estica.
  ⚠️ **`--` é PROIBIDO dentro de comentário XML.** Uma régua de hífens sob um título de
  comentário quebrou o arquivo — e a mensagem do parser aponta a linha seguinte, não a régua.
  Os comentários deste projeto usam `====` justamente por isso. Vale validar o XAML com
  `xml.etree` além das três redes: nem `compilar-sombra` nem `verificar-suite` leem o arquivo
  como XML puro.
  ⚠️ **A ÚNICA altura de campo fora dos 36 px da suíte é a desta busca (48 = `Espaco.12`).**
  A regra "não invente altura para dar ênfase" continua valendo e foi ela que reverteu um
  campo de 40 px duas parcelas atrás — porque lá ele estava numa RÉGUA de campos de 36. Aqui
  não há formulário: o campo é o único controle da tela, sem vizinho com quem desalinhar.
  ⚠️ **NUMA COMPOSIÇÃO CENTRADA, TODO VÃO EM BRANCO APARECE** — e a regra do `TextBlock`
  vazio (que já estava escrita duas parcelas acima) foi violada TRÊS vezes neste mesmo
  arquivo: o resumo da lista, a linha de erro e um `MinHeight="120"` no container do
  resultado, que reservava altura mesmo com os dois filhos colapsados (o dia sem agendamento
  nenhum). No leiaute antigo esses vãos ficavam escondidos por um cartão que já era grande;
  numa coluna centrada de 760px eles são a composição. **Regra ampliada: elemento amarrado a
  texto opcional leva `Visibility`, não só o `TextBlock`.**
  ⚠️ **Pílula de modo segue o que ESTÁ NA TELA, não a flag de configuração.** As duas
  estavam amarradas em `SugestaoLigada`/`SugestaoDesligada` — que continuam verdadeiras
  enquanto se digita —, então com um termo no campo a tela dizia "Com horário hoje" aceso
  sobre uma lista de resultados de busca. Agora seguem `MostrandoSugestao` e `ListandoTodos`,
  e com termo digitado **nenhuma** acende, que é a verdade: nem um modo nem o outro está no
  ar. Quem achou isto foi o renderizador de `tools/` — não a leitura do código.
  ⚠️ **A listagem alfabética voltou — como escolha.** A pílula "Todos os pacientes"
  (`ChipFiltro`, que já existia) desliga a sugestão. O defeito nunca foi a lista existir, foi
  ela CHEGAR sem ninguém pedir, com cara de resposta. Digitar RELIGA a sugestão, senão o modo
  fica grudado depois que a pessoa busca e apaga o campo.
- **O SELETOR DE PACIENTE DEIXOU DE DESPEJAR O ALFABETO** (set/2026). Com o campo de busca
  vazio, `BuscarPacientesAsync` não filtra nada — cai direto no `OrderBy(Nome).Take(50)` —,
  e a tela abria com o começo do alfabeto de 2.238 fichas: ACELINO, ADAISE, ADAO. Uma lista
  com cara de resposta que não é resposta de ninguém. Agora o Novo atendimento oferece
  **quem tem horário HOJE** (uma consulta, a mesma da agenda do dia, com `Include(Paciente)`
  já junto), e a busca continua a de sempre assim que alguém digita.
  ⚠️ **A correção é OPT-IN (`SugestaoInicial`), e tinha de ser**: dezesseis outras telas usam
  este seletor, e numa tela de LISTAGEM o despejo alfabético é o certo — é a lista, e ela
  passa `limite: null`. Mudar o comportamento do componente compartilhado consertaria uma
  tela e estragaria as outras.
  ⚠️ **O `Refinar` não se aplica à sugestão**: ele é o refino de um RESULTADO DE BUSCA, e
  quem monta a sugestão já decidiu o que ela contém — passá-la pelo refino filtraria duas
  vezes com dois critérios diferentes.
  ⚠️ **`Cpf.Formatar` fora dos 11 dígitos devolve SÓ OS DÍGITOS** (a segunda vez que esta
  armadilha aparece, agora em `Paciente.DocumentoFormatado`): o campo se chama `Documento`,
  a clínica cadastra RG, e um "12.345.678-9" voltaria "123456789". `Telefone.Formatar` não
  tem o problema — devolve a entrada quando não reconhece o padrão. `DocumentoEhTelefoneNaListaTests`
  amarra os dois contratos.
  ⚠️ **Crachá igual em quase toda linha não distingue nada.** 2.021 das 2.238 fichas estão
  em `ADefinir`, cujo nome de catálogo tem 33 caracteres — o selo saía cortado em "A definir
  (importado sem conv…" em noventa por cento da lista, gastando uma coluna para não
  responder nada. Virou um aviso curto ("sem convênio"), com o nome cheio no ToolTip.
  ⚠️ **Coluna `*` num monitor largo vira deserto.** O nome ficava na esquerda e os crachás
  pendurados na direita, com ~700px de branco no meio — o olho não liga as duas pontas. Teto
  de 1100px na lista; em 1366 (o balcão) não muda nada.
  ⚠️ **A lista virou TABELA COM MOLDURA E CABEÇALHO** — o mesmo tratamento da prévia da guia.
  Eram quatro colunas de dado flutuando no branco do cartão, nenhuma dizendo o que era. E o
  `Grid.IsSharedSizeScope` teve de SUBIR do ListBox para o DockPanel: o cabeçalho é um Grid
  separado das linhas, e sem dividirem o mesmo escopo ele sai em escada sobre elas (a lição
  da parcela 58 outra vez).
  ⚠️ **`MaxHeight` de lista é número INTEIRO de linhas.** Com 300 a sétima linha aparecia
  fatiada ao meio, e lista cortada no meio de uma linha se lê como defeito, não como
  rolagem. 332 = 8 de Padding + 6 × 54.
  ⚠️ **Não invente altura para dar ênfase.** O campo de busca chegou a ganhar 40px enquanto
  todo campo da suíte tem 36 — medida nova para um efeito que o LUGAR e a LARGURA já dão. Foi
  revertido antes de virar precedente.
  ⚠️ **Lista de escolha SEM estado vazio é tela quebrada.** Com a moldura, a tabela some junto
  com as linhas: digitar um nome que não existe deixava o cartão em BRANCO. O `EstadoDaTela`
  entra com `Ativo="{Binding Seletor.BuscandoPorTermo}"` — desligado enquanto a lista é a
  sugestão, porque ali o que falta não é o paciente, é o horário de hoje, e "nenhum paciente
  encontrado" num dia sem agenda é frase falsa sobre uma clínica de 2.238 fichas.
  ⚠️ **A LIÇÃO DE MÉTODO desta parcela: `PacientesView` já tinha o cabeçalho de quatro
  colunas com os mesmos `SharedSizeGroup` e o mesmo `EstadoDaTela`** — eu desenhei do zero o
  que estava a um arquivo de distância. Duas telas da mesma suíte listando pacientes com caras
  diferentes é o defeito que este documento descreve como "faz a recepcionista achar que abriu
  outro programa". Antes de desenhar lista, procure a irmã dela. **Débito aberto:** a de
  Pacientes segue com o cabeçalho solto, sem a moldura — as duas ainda não são iguais.
- **LANÇADOS HOJE VIROU A ABA "LANÇAMENTOS"** (set/2026, pedido do cliente). O bloco morava
  no rodapé do Novo atendimento e estava no lugar errado por três razões que só apareceram
  juntas: comia a altura da tela de lançar (com dois remendos que só existiam por isso —
  teto de 300px e `RodaDaPagina`, senão a lista comia a roda do mouse), não tinha onde
  receber um filtro, e respondia só pelo dia de hoje. O item "Atendimento" **já era
  composto**, então a mudança foi acrescentar a terceira `AbaMenu`, não inventar estrutura.
  A tela nova tem período livre (teto de 366 dias — passou disso a pergunta é outra, e a
  resposta é a Consulta de guias do faturamento), quatro filtros (convênio, modalidade,
  quem lançou, só com guia por liberar) e `DataGrid` com virtualização de verdade, que o
  `ItemsControl` de antes não tinha.
  ⚠️ **A leitura passou a ser de `Atendimentos`, não da agenda.** A versão antiga lia os
  agendamentos do dia e ia buscar o atendimento de cada um — N+1 que num dia de 30 sessões
  custava 31 idas ao banco, e num período de um mês custaria centenas. **O filtro
  `EstornadoEm == null` não é enfeite**: pela agenda o estornado sumia de graça (o estorno
  SOLTA o horário); pela fonte direta ele voltaria a aparecer.
  ⚠️ **`TelaComAbas` monta cada aba UMA vez e a guarda.** O estorno foi junto para a aba
  nova, e sem gancho a tela de lançar continuaria dizendo "já lançado hoje" sobre um
  atendimento recém-estornado. `NovoAtendimentoView` escuta `IsVisibleChanged` e chama
  `RevalidarPacienteEscolhido()` **só na volta** — reler nas duas pontas dobraria as
  consultas a cada troca de aba. Visibilidade é assunto da VIEW: a ViewModel não tem como
  saber que sumiu da vista.
  ⚠️ **ComboBox cuja `ItemsSource` é LIMPA escreve NULL na propriedade do `SelectedItem`.**
  Os filtros são `string?` por causa disso: declarados não-anuláveis, o filtro passava um
  instante com null — que não é "Todos" — e escondia a lista inteira. `Livre(null)` e
  `Livre("Todos")` são a mesma coisa.
  ⚠️ **O gancho de visibilidade vale nos DOIS lados**, e esquecer o segundo é o mesmo
  defeito espelhado: quem abrisse a conferência, voltasse para lançar e voltasse de novo
  veria a lista de ANTES do lançamento. `LancamentosView` relê ao ficar visível — mas **só
  quando o período inclui hoje**: a sessão lançada na aba do lado nasce com a data daquela
  tela, então um período antigo não pode ter mudado, e reconsultá-lo a cada troca de aba
  pagaria até 366 dias de consulta para não mudar nada.
  ⚠️ **Botão que nunca pode nascer apagado não leva `IsEnabled`** (a lição da parcela 41
  pelo avesso): o "Estornar…" ganhou um `IsEnabled` ligado à permissão, e o ITEM inteiro já
  exige a mesma — quem chega à aba sempre a tem. Ligação morta que sugere um estado que não
  existe. A guarda de verdade é o `Exigir` dentro do comando, como sempre foi.
  ⚠️ **O que se perdeu, e é decisão:** lançar não recarrega mais a lista com a linha nova
  logo abaixo. A troca se sustenta porque a coluna direita já mostra o desfecho e porque a
  lista responde outra pergunta, noutro momento — a conferência antes de fechar o balcão.
- **A GUIA QUE VAI NASCER, DESENHADA** (set/2026, redesenho aprovado em mockup). A coluna
  direita do Novo atendimento resumia a prévia em TEXTO — "2 guias · a 2ª libera 09/08" —,
  e a guia, que é o que a clínica entrega, só virava objeto depois de gravada e noutro app.
  O número estava certo e mesmo assim passava o erro que mais custa: guia emitida com o
  campo errado, que volta glosada semanas depois. Agora a coluna desenha o **documento**,
  com os campos que vão nele — convênio, carteirinha, validade, beneficiário, executante —
  e os códigos embaixo; o campo que FALTA sai com o fundo do erro, porque no documento o
  buraco tem de se ver como buraco. **Nenhum campo é inventado**: o `EntradaModalidade` do
  catálogo tem `Codigo` interno, `Nome` e `Base` — **não há código TISS por modalidade**, e
  o mockup que os mostrava estava errado. O documento é PRÉVIA: não há número (ele nasce na
  baixa) e a tarja diz isso.
  As modalidades saíram dos cartões de 200×130 e viraram **linhas**: para comparar "quantas
  guias" entre cinco opções em grade o olho salta na diagonal, e em linha os números caem
  numa COLUNA — que é como se compara número. Cada linha ganhou o traço da FAMÍLIA à
  esquerda, com os mesmos tokens `Brush.Modalidade.*` do `CartaoDeHorario`: quem vê a agenda
  o dia inteiro já lê teal como BSV. Os gatilhos de `Escolhida` vêm DEPOIS dos de família,
  porque gatilho posterior vence e a linha marcada tem de se distinguir das outras quatro.
  A faixa do paciente passou a carregar **convênio e carteirinha em colunas rotuladas**, no
  lugar de dois badges do mesmo tamanho da categoria — são os dois campos que decidem se a
  guia nasce e se o convênio a aceita, e a carteirinha só aparecia quando VENCIA.
  ⚠️ **O botão continua habilitado sem convênio, e isso é a parcela 92, não um esquecimento**:
  o clique é que abre a janela de convênios (`VinculoDeConvenio`). Desabilitá-lo devolveria a
  recusa muda que a parcela 92 aposentou.
  ⚠️ `CornerRadius="8,8,0,0"` literal na tarja do documento: o WPF **não deriva raio parcial**
  de um `CornerRadius` do dicionário. Os 8 são `Raio.Medio` — se o token mudar, a tarja fica
  com uma lasca de fundo na quina.
  ⚠️ O `verificar-suite` **não resolve estilo local**: um `TextBlock` cujo `TextTrimming` vem
  de um `Style` declarado no `UserControl.Resources` é acusado do mesmo jeito. Repetir o
  atributo na tag é o preço, e ele deixa a intenção à vista onde ela é lida.
  ⚠️ **`Cpf.Formatar` fora dos 11 dígitos devolve SÓ OS DÍGITOS**, e o campo da ficha se
  chama `Documento`, não `Cpf`: passar um RG "12.345.678-9" por ele volta "123456789" — a
  pontuação de um documento de verdade apagada na exibição. Formate quando `Normalizar`
  der 11; no resto, mostre o que está gravado. (`Telefone.Formatar` não tem o problema —
  devolve a entrada quando não reconhece o padrão.)
  ⚠️ **String vazia num `TextBlock` ocupa a altura de uma linha.** A meta do paciente
  (documento · telefone · idade) devolve NULO quando a ficha não tem nenhum dos três, e
  não `string.Empty`: a importação do Smart Clinic produziu fichas incompletas às centenas,
  e cada uma abriria um buraco debaixo do nome.
  ⚠️ **`ExecutanteADefinir` não é `SemProfissionalEscolhido`.** Aquele só vale no modo
  MARCAR — é o aviso amarelo de lá. O campo do executante na guia vale nos DOIS modos,
  porque a guia é a mesma, e sem a marca o "a definir" saía com a mesma cara de um dado
  preenchido: o único campo cujo vazio custa dinheiro a quem atendeu era o que menos
  aparecia. Âmbar e não vermelho — sem executante a guia nasce; o que se perde é o
  repasse. (O aviso em PALAVRAS no modo avulso continua não existindo: é a pendência
  antiga de estender `SemProfissionalEscolhido`, que segue de pé.)
- **SEM CONVÊNIO ESCOLHIDO NÃO NASCE ATENDIMENTO** (parcela 92). A importação do Smart Clinic
  trouxe **2.021 das 2.238 fichas sem convênio**, no código `ConvenioCadastro.CodigoADefinir` —
  que não gera guia. A defesa era o alerta VERMELHO da elegibilidade, e ela não impede nada por
  contrato ("quem decide é a clínica"): a sessão era lançada por cima do vermelho, os códigos
  nasciam `NaoAplicavel`, a tela dizia "Atendimento registrado" e o faturamento não via guia
  nenhuma. Nada falhava — é o defeito do segundo tipo, o que **deixa de fazer o que promete**.
  A recusa (`ConvenioNaoDefinidoException`) mora em **`AtendimentoService.MontarAsync`**, a
  montagem por onde TODAS as portas passam: avulso do balcão, lançamento sobre o horário do dia,
  `ConfirmarPresencaAsync` da Fila e `AgendarAsync` com "guia no agendamento" ligado. Escrevê-la
  numa tela cobriria uma e deixaria três passando.
  **Recusar sem oferecer a saída seria trocar guia perdida por balcão travado**, então a metade
  visível é `VinculoDeConvenio` + `EscolhaDeConvenioWindow` (Shell): as telas perguntam ANTES,
  na hora, e a ficha é atualizada no mesmo clique — é o `ColetaDeTermo` de novo, e pela mesma
  razão (várias portas, uma montagem só). Quem pergunta: Novo atendimento (o aviso já traz o
  botão) e o Concluir da Fila. O faturamento (`Clinica.Desktop`) só mostra a mensagem — ele não
  referencia o shell, é o débito permanente da Fase 4.
  ⚠️ **Três assimetrias deliberadas.** (a) **PARTICULAR não é "sem convênio"**: é escolha
  registrada (`ConvenioCadastro.GeraGuia` desmarcado, parcela 60) e continua lançando.
  (b) **Marcar por telefone com a chave desligada continua passando**: aí a marcação não cria
  atendimento nenhum, e exigir o convênio ali travaria quem marca sem ter a resposta em mãos —
  a condição vale exatamente onde o atendimento nasce, e a UI espelha isso
  (`!MarcarParaDepois || GuiaNaMarcacao`). (c) **A carteirinha da janela é opcional e, em
  branco, PRESERVA a da ficha** — a janela responde uma pergunta, não regrava a ficha.
- **O número da guia tem FORMA, e ela é do convênio** (`RegraNumeroGuia`, `ConvenioCadastro.
  FormatoNumeroGuia`, parcela 45): a Unimed numera só com dígitos; Petrobras, Amil e Sul América
  misturam letra e número. O que se pega com isso é o erro que o olho não vê — o **"O" digitado no
  lugar do zero** —, que passava batido, dava a guia por baixada e só aparecia no retorno da
  operadora, semanas depois, com boa parte do prazo de recurso já corrida. A regra mora no
  **DOMÍNIO** e é aplicada em `FaturamentoService.DarBaixaAsync`, não na tela, porque a baixa tem
  **quatro portas** (tela de baixa, baixa em lote, rodada de pendências e fila do Gerente): validar
  na tela cobre uma e deixa três passando, que é o defeito recorrente do projeto vestido de
  validação. A tela usa a MESMA regra para avisar ANTES do clique — a dica ao lado do campo é a
  metade que explica; o serviço é a que impede.
  O formato é **dado, não código**: fica no `ConvenioCadastro`, editável em Configurações, porque
  "Sul América" não existe no enum `Convenio` (entrou como personalizada) e amarrar a forma à
  família obrigaria a publicar versão nova a cada operadora. A migration **semeia os embutidos**
  com o que a cliente informou — sem isso a regra nasceria desligada e só passaria a valer no dia
  em que alguém abrisse Configurações, que pode ser nunca. O que ela **não** faz é conferir
  tamanho, prefixo ou dígito verificador: cada operadora tem o seu, eles mudam sem aviso, e regra
  apertada demais recusa guia legítima — o que é **pior** do que aceitar uma errada, porque trava
  o faturamento do dia e o faturista não tem como contornar (ele não inventa o número). Três
  degraus na resolução, e o terceiro é o que separa "não configurado" de "não sei quem é":
  catálogo → padrão da família → **sem validação** para código desconhecido.
  **Número em branco passa em qualquer formato**: quem valida FORMA não decide obrigatoriedade.
- **Permissão granular no faturamento, e o login que a torna útil** (parcela 45): a cliente pediu
  "permissões granulares para a gerente auditar o que está sendo feito e liberar ou não". A metade
  das permissões não resolvia sozinha — **toda ação do faturamento gravava `Environment.UserName`
  na auditoria**, isto é, o login do WINDOWS: as duas pessoas que dividem o balcão assinavam com o
  mesmo nome, e a trilha da parcela 21, que existe para responder "quem fez isso?", respondia o
  nome da MÁQUINA. Por isso o app ganhou tela de login (`Clinica.Desktop/Acesso/LoginWindow`) e
  `SessaoUsuario` subiu para `Clinica.Domain`.
  Os bits novos cortam pelo **ATO, não pela tela** (`BaixarGuia`, `EstornarBaixa`,
  `RegistrarGlosa`, `GerenciarLotesTiss`, `LancarAtendimento`, `MarcarNaoConformidade`,
  `ConfigurarFaturamento`) — quebrar por tela daria uma lista que muda a cada leiaute novo.
  `VerFaturamento` virou **só a leitura**. Estornar é separado de baixar porque desfazer apaga o
  trabalho de outra pessoa; NC é separado de tudo porque é a única permissão que faz uma pendência
  **sumir do painel sem a guia ter sido faturada**.
  Três decisões que andam junto: (a) o padrão do perfil `Faturista` reproduz **exatamente** o que
  o app deixava fazer antes do login — a granularidade serve para a direção TIRAR, não para a
  atualização tirar sozinha; (b) as telas do Gerente que baixam/glosam/reabrem passaram a pedir os
  bits novos no mesmo commit, senão negar "Dar baixa" a alguém no Acessos não impediria nada e a
  permissão seria só uma caixinha na tela; (c) a rodada BLOQUEANTE **não abre** para quem não pode
  decidir nada — travar o sistema com uma tarefa que a pessoa não pode cumprir é o pior desfecho
  possível de uma permissão bem-intencionada.
  A entrada no app exige `VerFaturamento` **e diz isso na porta**: deixar entrar e mostrar sidebar
  vazia faz a pessoa ligar para o suporte em vez de falar com a direção. E "Trocar usuário"
  **reabre o app** em vez de repontar a sessão: as ViewModels leem a permissão quando são
  construídas, e metade delas já está viva — permissão que parece aplicada e não está é pior do
  que permissão nenhuma.
- **O atendimento saiu do faturamento e foi para o balcão** (parcela 46): "Novo
  atendimento" e "Consultas" viraram itens do `Clinica.Modulo.Recepcao`, e **saíram de vez**
  do `Clinica.Desktop`. Nenhum dos dois era feature nova — os dois existiam, no posto
  errado. Lançar atendimento AVULSO (quem chegou sem horário) e renovar a consulta do
  convênio são atos que se fazem **com o paciente na frente**, e moravam na máquina de quem
  não recebe ninguém: a recepção via o selo "consulta a renovar" no cartão da agenda desde a
  parcela 44 e **não tinha por onde renovar**. É a nona ocorrência do defeito recorrente do
  projeto, na variante "a porta está no módulo de quem não usa".
  **O circuito com o faturamento não mudou, e é isso que os testes fixam**: os dois caminhos
  do atendimento — este e a Fila → Finalizar (`FechamentoSessaoService` →
  `AgendaService.ConfirmarPresencaAsync`) — desembocam em `AtendimentoService.LancarAsync`,
  que é **ponto único** e grava `Atendimento` + `CodigoFaturamento` pelas regras do convênio.
  Não há atendimento que nasça sem guia. `AtendimentoNaRecepcaoTests` prova pelas consultas
  do PRÓPRIO faturamento (`PendenciaService`, `ConsultarCodigosAsync`), e não por uma
  consulta escrita para o teste: elo partido aqui não vira erro, vira **lista vazia**, que é
  indistinguível de um dia fraco.
  O que **não** foi junto foi a BAIXA. Ela é o ato do faturamento, tem as quatro portas de
  lá, e o perfil que usa a tela nova não tem o bit — botão que nasce apagado para quem usa a
  tela é o defeito da parcela 41. A lista de guias geradas ficou como **confirmação**: é onde
  o balcão vê, na hora, que a guia nasceu e quando ela libera. E `PerfilAcesso.Recepcao`
  ganhou `LancarAtendimento` no mesmo commit — sem o bit, as duas telas nasceriam invisíveis
  para quem passou a ser dono delas.
  O faturamento continua criando atendimento pela **agenda dele** (confirmar presença); o que
  saiu foi a tela de lançamento avulso.
- **Consultar guias filtra por modalidade e especialidade** (parcela 45): a pergunta da direção é
  "o que vem sendo feito", e a consulta só respondia por paciente, número, data, status e
  convênio. O filtro é pelo **enum**, não pelo código do catálogo, pela mesma razão do convênio ao
  lado: é por FAMÍLIA, e a variante cadastrada responde junto da embutida que ela deriva — filtrar
  por código obrigaria a escolher entre "Acupuntura" e "Acupuntura (domiciliar)" para responder
  quantas acupunturas foram feitas. A especialidade casa com a do **CÓDIGO** e cai para a do
  atendimento quando ele não a tem; sem esse caminho de baixo, guia de um atendimento com
  especialidade declarada ficaria de fora do filtro da própria especialidade. E o resumo **diz que
  está filtrado**: "12 guias" e "12 guias de acupuntura em psiquiatria" respondem perguntas
  diferentes, e quem volta à tela depois do café não lembra o que deixou marcado no combo.
- Cada convênio tem um fluxograma próprio (README lista os cinco modelados). O 2º código com data
  prevista +24h e a inversão de datas do BSV são requisitos do convênio, não bugs.
- Guia exportada num lote TISS não pode entrar em outro lote; glosa ganha data-limite de recurso
  (prazo configurável, padrão 30 dias) vigiada no dashboard.
- **Rodar as pendências** (`RodadaPendenciasService`): o prazo de decisão é **por guia** — cada
  pendência vence N dias (configurável, padrão 10) depois de **virar pendente**, ou seja da
  `DataPrevistaFaturamento` (`CodigoFaturamento.PrazoDecisaoVencido`). Não conta do atendimento: o 2º
  código só existe +24h depois, e contar do atendimento lhe daria um prazo real menor que o do 1º,
  cobrando decisão sobre uma guia que ninguém tinha como tirar. Passado o prazo sem baixa, o painel
  alarda (banner) e a abertura do app abre uma janela BLOQUEANTE com as guias vencidas: cada uma exige
  decisão — baixa ou **não conformidade** (`StatusCodigo.NaoConformidade` + justificativa) — e o
  sistema fica travado até a resolução. Há uma **carência de 1ª execução**
  (`ParametrosService.ChaveInicioRodadaPrazo`, ancorada por `GarantirInicioAsync`): guias que já
  estavam pendentes antes da ativação da versão só passam a contar o prazo a partir da ativação, para
  o backlog acumulado não bloquear tudo de uma vez. O valor da chave no banco continua
  `"InicioRodadaPorAtendimento"` de propósito — renomeá-lo perderia a âncora já gravada.
  O usuário também pode marcar NC proativamente (sem esperar o prazo) pelo botão **NC** na
  linha da pendência no painel (`NaoConformidadeWindow`). A não conformidade sai das
  pendências ativas (`EstaPendente`/`CodigosEmAbertoAsync` a ignoram) e vai para a aba própria **NC**
  (`NaoConformidadesViewModel` / `Secao.NaoConformidades`), que lista todas via
  `RodadaPendenciasService.NaoConformidadesAsync`, permite ler a justificativa e reabrir; também entra
  no relatório. Ela reativa (volta a pendência) de duas formas: manualmente (botão Reabrir na aba NC)
  ou automaticamente quando o **paciente volta** — `AtendimentoService.LancarAsync` reabre as NCs do
  paciente e avisa a secretária para cobrar a guia na hora. Toggles em Configurações estendem a rodada
  a consultas/carteirinhas.
- **Mapa corporal e protocolo** (`MapaCorporalService`, parcela 3): o mapa é **1:1 com a
  evolução** (é a mesma sessão vista de outro jeito) e some com ela. Aplicar um protocolo
  é **COPIAR** os pontos para a sessão, nunca apontar para ele — referência viva faria
  corrigir um ponto hoje reescrever o protocolo da clínica e a sessão da semana passada.
  As coordenadas são **normalizadas (0 a 1)** sobre a figura, nunca pixels: quem converte
  o clique é a tela (`PontoMapaItem.LarguraFigura`/`AlturaFigura`, os mesmos números do
  XAML). "Repetir a sessão anterior" e "aplicar protocolo" **não gravam** — trazem os
  pontos para a tela, e só o Salvar da sessão os efetiva.
- **Documento clínico é fato** (`DocumentoClinicoService`, parcela 3): os sete papéis da
  página 21 que saem da Recepção viram um `DocumentoClinico` numerado por ano
  (`2026/0001`) com código de conferência. Não se apaga nem se reescreve: **cancela-se
  com motivo** e emite-se outro (como a revogação de consentimento). O conteúdo é gravado
  na EMISSÃO e não remontado na reimpressão — a segunda via tem de sair idêntica à que o
  paciente levou. Quatro são escritos (receita, atestado, comparecimento, pedido de
  exame) e três montados do prontuário (relatório de evolução, termo de consentimento,
  anamnese). O **CID só sai impresso com autorização expressa do paciente**
  (`CidImpresso`); receita, atestado e pedido de exame **exigem** o profissional que
  assina — única exceção à regra de "avisa, mas não impede". Não há assinatura ICP-Brasil:
  o PDF traz carimbo, linha de assinatura e código de conferência, e chamar isso de
  assinatura digital seria mentir sobre o que a via garante.
- **Pacote é venda da clínica, cota é do convênio** (`PacoteService`, parcela 4): não
  confundir `PacotePaciente` com `AutorizacaoSessoes` — as duas contam sessões e não têm
  nada a ver uma com a outra (a cota evita glosa; o pacote evita atender de graça). A
  venda **copia** o catálogo (mudar o preço de tabela não reescreve o que já foi
  comprado); a **situação é calculada, não guardada** (`Situacao(hoje)`), porque um
  pacote gravado como "Ativo" viraria mentira à meia-noite do vencimento; consumo é fato
  datado e se desfaz **cancelando com motivo**. A baixa automática
  (`ConsumirPorAtendimentoAsync`) debita o pacote **que vence primeiro**, uma vez por
  atendimento, e é chamada pelo fluxo da **Recepção** — nunca pelo `AtendimentoService`,
  que é compartilhado com o faturamento congelado.
- **Fechamento da sessão** (`FechamentoSessaoService`, parcela 6): concluir o atendimento na
  Recepção são **quatro fatos do mesmo ato** — a guia nasce, o pacote debita, o insumo sai do
  estoque e o dinheiro entra no caixa. Por cinco parcelas só o primeiro acontecia: `PacoteService.
  ConsumirPorAtendimentoAsync` e `EstoqueService.BaixarAsync` estavam prontos, testados e **sem um
  único chamador em produção**, e a Recepção não conhecia o `FinanceiroService`. Este serviço é a
  ponte, e mora fora do `AtendimentoService` pelo mesmo motivo de sempre: aquele é compartilhado
  com o faturamento congelado. A tela (`FechamentoSessaoWindow`, aberta pelo Finalizar da Fila) é
  **proposta confirmada, nunca automática** — o sistema faz o trabalho (acha o pacote que vence
  primeiro, lembra o que a última sessão gastou, sugere o valor da última entrada do paciente
  **com a procedência**), e o clique continua sendo de quem está no balcão; valor errado gravado
  sozinho vira caixa que não bate e só aparece no fim do mês. Só a conclusão do atendimento
  derruba a operação: pacote, insumo e caixa que falham viram **aviso** (`ResultadoFechamento.
  Avisos`) e a janela fica aberta dizendo o quê — desfazer a guia porque o estoque de uma agulha
  não bateu deixaria a clínica sem a guia de uma sessão que o paciente recebeu. Cobrança **não é
  sugerida** quando há pacote (sessão comprada já foi paga) nem sem histórico de recebimento do
  paciente — marcar por padrão criaria receita fantasma para quem é do convênio.
- **Taxa de cartão e imposto** (`TaxaService`, parcela 9): o **valor do lançamento continua
  sendo o BRUTO** — o que o paciente pagou. Taxa e imposto são deduções ao lado, e o líquido
  é CALCULADO (`LancamentoFinanceiro.ValorLiquido`); gravar o líquido daria duas verdades
  sobre o mesmo dinheiro. `TaxaCartao` tem **vigência**, como a regra de repasse: a
  adquirente renegocia, e o que vale no recebimento de março é o percentual de março — por
  isso o valor da taxa é **copiado** na venda, nunca referenciado. A regra mais **específica**
  ganha (bandeira vence o adquirente genérico), senão a clínica cadastraria a exceção e
  continuaria vendo o número da regra geral. **Sem taxa cadastrada não se inventa desconto**:
  o lançamento fica só com o bruto, que é a verdade disponível. A alíquota de imposto nasce
  **zero** — cada clínica tem seu regime, e chutar erraria a base inteira — e é lida com
  ponto decimal invariante ("2,5" lido como 25 multiplicaria o imposto por dez).
- **Contas a pagar e a receber** (`ContasService`, parcela 12): `LancamentoFinanceiro` tem
  **três datas e elas não são a mesma coisa** — `Data` é a competência, `DataPagamento` é
  quando o dinheiro se moveu e `DataVencimento` é quando ele PRECISA se mover. Sem a
  terceira o módulo não respondia "o que vence esta semana". `LancamentoRecorrente` é um
  **molde, não um lançamento**: ele não entra em total nenhum, quem entra é a conta prevista
  que ele gera e que dali em diante tem vida própria (a luz nunca vem igual; se a
  recorrência fosse "o lançamento que se repete", corrigir março reescreveria janeiro, já
  pago e conciliado). A série sai sempre do **primeiro vencimento mais N períodos**, nunca
  da ocorrência anterior mais um — encadear faria o aluguel do dia 31 virar aluguel do dia
  28 para sempre por causa de fevereiro. A geração é **idempotente** por
  `OrigemRecorrencia` (`REC:{id}:{aaaa-MM-dd}`, índice único), roda por **clique** e nunca
  automaticamente na abertura, e **nada nasce pago**: tudo vem `Previsto`, porque o sistema
  sabe que a conta vence, não que ela foi quitada.
- **Fluxo de caixa** (`FluxoCaixaService`, parcela 13): **realizado e previsto nunca viram
  um número só** — somados, o mês que fechou e o que ainda vai vencer teriam a mesma cara.
  Cada lançamento entra no mês pela data que corresponde ao seu estado (`LancamentoDatado.
  DataDoFluxo`): realizado pelo pagamento, previsto pelo vencimento, competência como
  último recurso. O acumulado é **variação no período, não saldo em conta** — a clínica
  nunca cadastrou saldo inicial, e chamá-lo de saldo daria um número que não bate com o
  extrato. A fração da categoria é do total **do mesmo tipo**, e lançamento sem categoria
  aparece como "Sem categoria" em vez de sumir.
- **Fechamento de caixa** (`FechamentoCaixaService`, parcela 14): a conferência da gaveta
  conta **só espécie** — cartão e PIX caem na conta dias depois, e incluí-los faria a
  conferência nunca bater, o que treina a clínica a clicar "OK" sem olhar. `ValorSistema` e
  `SaidasEspecie` são **copiados no fechamento**, não recalculados: lançamento digitado
  amanhã com a data de ontem reescreveria a conferência de ontem e levaria junto a
  justificativa. **Divergência exige justificativa escrita** — é a única regra do serviço
  que impede em vez de avisar, e vale também para sobra (dinheiro a mais costuma ser venda
  não lançada). Reabrir **não apaga**: o fechamento anterior fica marcado com o motivo e o
  dia volta a pendente; `FechamentoCaixa.Data` não é único e o que vale é o de maior `Id`.
- **Regime tributário** (`TributoService`, parcela 15): substitui a alíquota única da
  parcela 9, que respondia "quanto saiu" e não "**de quê**" — a clínica no Presumido tem
  cinco guias — e não tinha vigência, então o reajuste do ISS mudava o mês já declarado.
  `Tributo.BasePercentual` existe porque nem todo tributo incide sobre a receita inteira:
  no Presumido o IRPJ de 15% incide sobre 32% e a **efetiva é 4,8%**; sem o campo a clínica
  digitaria 15% e triplicaria o imposto. Cada tributo é **arredondado ao centavo
  separadamente** (arredondar só no fim não bate com a soma do detalhe, e é o detalhe que
  vai ao contador). `Descricao` e `DetalheImposto` são formatados em **pt-BR fixo, não na
  cultura da máquina**: eles são GRAVADOS, e dois postos escreveriam "0,65%" e "0.65%" na
  mesma coluna — não contradiz a regra invariante do `ParametrosService`, que **lê** número
  que precisa voltar a ser número. A chave `AliquotaImpostoRetido` segue como **fallback**
  enquanto não houver tributo cadastrado: o valor já está na base do cliente, e zerá-lo
  faria um mês inteiro sair sem imposto sem ninguém perceber.
- **Recebíveis de cartão** (`RecebiveisService`, parcela 16): `PrevisaoRecebimento` era
  gravada desde a parcela 9 e **nenhuma tela a lia** — terceira ocorrência do mesmo defeito
  no projeto. A conciliação é por **depósito, não por venda** (a adquirente deposita o lote
  do dia; conferir venda a venda contra um extrato de um valor só nunca fecharia).
  `RecebimentoConfirmadoEm` fica **separada** da previsão: quando a adquirente atrasa as
  duas divergem, e sobrescrever a previsão apagaria a prova do atraso. A data do crédito é
  **informada**, nunca `Today` — conferir na segunda um depósito que caiu na sexta viraria
  três dias de atraso que não houve. O líquido do depósito é bruto menos **taxa, sem
  imposto**: o imposto é recolhido depois, por guia, e descontá-lo aqui faria o número não
  bater com o extrato, que é o único uso da tela.
- **Custo de transação** (`CustoTransacaoService`, parcela 17): a visão da direção. A
  leitura que só existe aqui é a **taxa efetiva contra a de tabela** — a tabela diz 3,1%, e
  se a clínica parcela mais do que imagina o efetivo é 3,9%; é a diferença que dá argumento
  na renegociação. A tabela vem como **faixa** (menor e maior vigente), porque uma
  adquirente tem várias taxas e um número só teria de escolher uma modalidade. A fração da
  barra é do custo de **maquininha**, não do faturamento: senão "(sem maquininha)" levaria
  a maior fatia sendo que custa zero.
- **Retenção na fonte por convênio** (parcela 18): a operadora que paga serviço de PJ
  **retém** IRRF, CSLL/PIS/COFINS e às vezes o ISS antes de depositar — a guia vale
  R$ 1.000 e caem R$ 943,50. `Tributo.ConvenioCodigo` preenchido significa retido na fonte
  por aquele convênio (reaproveita a entidade porque é o mesmo fato; só muda quem recolhe).
  A retenção **substitui** os tributos gerais naquele recebimento, nunca se soma: os dois
  são o mesmo imposto, e somá-los conta duas vezes — o erro clássico da planilha de
  convênio. Sem retenção cadastrada o convênio cai nos tributos gerais (mudar isso faria a
  receita de convênio parar de sofrer imposto da noite para o dia), e retenção de convênio
  **não vaza** para recebimento que não é dele. O detalhe copiado diz "(retido na fonte)"
  porque retido já saiu e recolhido ainda vai sair. `GuiaSemLancamento.ConvenioCodigo`
  existe porque `Convenio` é a família de REGRA: duas operadoras podem compartilhá-la e
  reter percentuais diferentes.
- **Tabela de preço por convênio** (`PrecoConvenioService`, parcela 20): cadastrada no
  **Gerente** e lida pela conciliação do **Financeiro** — quem negocia tabela é a direção,
  quem concilia guia é o balcão; mesmo banco, sem sincronização. A mais **específica** ganha
  (especialidade vence o genérico do tipo), tem **vigência** (reajuste é linha nova; a guia
  de março segue valendo o preço de março) e o valor é **copiado** no lançamento. **Sem
  preço cadastrado não se inventa valor** — o campo fica vazio para digitar, e chutar um
  valor de mercado daria receita errada com aparência de exata. É **proposta, não
  imposição**: a operadora pode ter pago menos (glosa parcial), e a linha mostra a
  PROCEDÊNCIA do número, porque campo que se preenche sozinho sem explicar faz a pessoa
  confirmar sem conferir. A proposta usa a data da **baixa da guia**, não a de hoje.
- **Rentabilidade por convênio** (`RentabilidadeConvenioService`, parcela 19): o encontro
  dos dois módulos. **Líquido por guia** é a leitura central — o único número comparável
  entre operadoras que pagam valores e volumes diferentes, e por isso a "menos rentável" é
  apurada por guia e não pelo total (que apontaria sempre a de menor volume). O **prazo
  médio** só conta o que JÁ foi pago: incluir o previsto mediria uma promessa, e quem
  atrasa teria o melhor número. O agrupamento é pelo **código**, não pelo nome exibido —
  duas operadoras da mesma família fora do catálogo resolvem para o mesmo nome padrão e
  seriam fundidas. O período é o do **atendimento**, não o do recebimento.
- **Tabela de preço por convênio** (`PrecoConvenioService`, parcela 20): o valor da guia é
  cadastrado no **Gerente** e refletido no Financeiro (a Conciliação já abre com o valor
  preenchido e a **procedência** escrita ao lado). A regra mais **específica** ganha —
  especialidade declarada vence a genérica, depois a vigência mais recente —, senão a
  clínica cadastraria a exceção e continuaria vendo o preço geral. Sem preço cadastrado o
  campo fica **vazio**, não zero: chutar um valor viraria receita inventada.
- **Auditoria** (`AuditoriaService`, parcela 21): `EventoAuditoria` era gravado por
  praticamente tudo o que mexe em dinheiro ou permissão e **nenhuma tela o lia** — quarta
  ocorrência do defeito "dado gravado sem leitor" (o pacote que debitava, o insumo que
  baixava, a previsão de recebimento) e a mais grave, porque esta é a resposta para "quem
  fez isso?". A trilha é **somente leitura, e isso é decisão**: registro de auditoria que
  se pode editar ou apagar não é auditoria, é rascunho — não há exclusão no serviço nem na
  tela. A ação casa por **prefixo** ("Conta" acha ContaCriada e ContaReagendada); o dia
  final entra **inteiro** (`TimeOnly.MaxValue`), senão um evento das 14h ficaria fora de um
  filtro que pede "até hoje"; o resumo conta sobre o **resultado do filtro**, não sobre a
  base; e **bater no limite é avisado**, porque "300 eventos" que são 300 de 4.000 faria a
  direção concluir que o período teve pouca movimentação. Fica sob `Permissao.VerAuditoria`,
  não sob `GerenciarUsuarios`: ler a trilha e mexer em permissão são coisas diferentes.
- **Painel da direção** (`PainelDirecaoService`, parcela 22): o Gerente carrega os três
  módulos e abria no primeiro item do primeiro deles — o painel da **Recepção**. Daí
  `ItemMenuModulo.Inicial` (reordenar os módulos desmontaria a sidebar, que já está na
  ordem do dia de trabalho) e `NavegacaoSuite`, pela qual uma tela pede ao shell para abrir
  outra por chave; `ChavesSuite` guarda só as chaves que **atravessam módulo** — nenhum
  módulo passa a conhecer o outro, o que se evita é repetir `"fechamento-caixa"` à mão do
  outro lado, onde renomear a seção compila e só falha na clínica. O painel **não calcula
  nada**: cada número vem do serviço dono dele (a pendência vem do
  `RodadaPendenciasService`, que já resolve prazo e carência — recontar daria outro número
  para a mesma guia). A comparação é com o **mesmo trecho do mês anterior**: no dia 5,
  cinco dias contra trinta apontariam queda de 80% todo começo de mês, e a direção
  aprenderia a ignorar a seta. **Cada bloco falha sozinho** (`NaoVerificados`), porque um
  painel que diz "nada vencido" por causa de uma consulta quebrada é pior do que um painel
  que não abre. Cada alerta **leva** ao assunto, e o botão fica desabilitado quando o
  destino não existe para quem está usando.
- **Inadimplência** (`InadimplenciaService`, parcela 23): a conta a receber vencida existia
  desde a parcela 12, mas dissolvida na lista de Contas — uma linha por lançamento,
  misturada com o que a clínica tem a **pagar** —, e `LancarContaAsync` **não tinha
  `pacienteId`**: a dívida não tinha dono. Só conta de **paciente** entra (a receber sem
  paciente é reembolso de convênio ou venda de produto, e cobrar quem não deve custa mais
  que a sessão em aberto). A situação é **calculada, nunca gravada** — campo "inadimplente"
  no cadastro continuaria lá depois de o paciente pagar, como a situação do pacote. A ordem
  padrão é o **mais antigo**, não o maior valor: a chance de receber cai com o tempo, e o
  caso de seis meses precisa de decisão, não de mais um lembrete. **Não há pagamento
  parcial** — receber passa pelo `FinanceiroService`, que é quem grava dinheiro. Todas as
  faixas de envelhecimento aparecem, **mesmo vazias** (aging sem a faixa de 90+ se lê como
  "não há dívida velha"). A mensagem de cobrança é **lembrete, não ameaça**, e não leva dado
  clínico — o telefone pode não ser só do paciente; e **cobrança não exige consentimento de
  marketing**, é transacional como a confirmação da própria sessão. No painel da direção,
  conta a pagar e paciente devendo são alertas **separados**: somá-los daria um número sem
  significado, porque um se resolve pagando e o outro cobrando.
- **Central de documentos** (`CentralDocumentosService`, parcela 24): as nove folhas do
  mockup existiam todas e **nenhuma estava no mesmo lugar** — quatro numa janela dentro da
  ficha do paciente, três no botão certo da aba certa dessa ficha, o recibo no Caixa, o
  orçamento só de dentro de um pacote vendido e o fechamento do período só no app
  congelado. Não faltava capacidade, faltava porta; e havia duas capacidades **sem porta
  nenhuma** (`DocumentoFinanceiroService.EmitirAsync`, que aceita linhas quaisquer desde a
  parcela 4, e `FechamentoPdfService`) — a variante mais discreta do defeito "dado gravado
  sem leitor": o CI fica verde e a clínica pega o bloquinho de papel. A tela **não
  reimplementa emissão nenhuma**: abre a janela que já existe ou chama o serviço dono da
  folha. `FolhaCatalogo.Exigencia` é o que faz cada cartão dizer **o que falta** em vez de
  deixar o botão aceso e avisar depois — descobrir o requisito errando é o que faz a pessoa
  desistir da tela. O **recibo continua nascendo no Caixa** (aponta para o lançamento; de
  outro lugar sairiam dois recibos do mesmo pagamento) e o cartão navega até lá. Cancelada
  **aparece marcada, nunca sumindo**; filtro de folha desconhecida devolve **nada, não
  tudo**; e o fechamento do período **não tem segunda via** — não é gravado, é conferência
  montada na hora, então a lista devolve vazio para ele de propósito. O rótulo segue o
  **mockup**, não o enum ("Receituário", não "Receita").
- **Repasse** (`RepasseService`, parcela 4): quem atendeu vem do **agendamento**
  (`Agendamento.ProfissionalId` + `AtendimentoId`), porque `Atendimento` é do faturamento
  e não guarda profissional. O percentual incide sobre a **receita que entrou**
  (lançamentos de entrada não cancelados), não sobre o faturado. `RepasseApurado` existe
  para travar o período: repasse pago duas vezes é dinheiro que não volta; cancelar a
  apuração cancela junto a saída no caixa. Regra tem vigência — mudar o percentual hoje
  não reescreve o mês já pago.
- **Estoque** (`EstoqueService`, parcela 4): o **saldo é a soma dos movimentos**, nunca um
  campo — total guardado é como o estoque para de bater. A **validade fica no movimento
  de entrada** (o lote), não no item, senão o lote que vence primeiro some. Saída maior
  que o saldo é recusada e perda exige motivo escrito.
- **Capacidade sem porta** (parcela 25): a quinta rodada do defeito recorrente do projeto,
  e a mais discreta — não é dado gravado sem leitor, é **serviço testado que nenhuma tela
  chama**. Seis capacidades estavam assim, todas com teste verde: o custo por sessão
  (`CustoDoAtendimentoAsync`, que a parcela 6 destravou e ninguém lia), devolver uma sessão
  ao pacote (`CancelarConsumoAsync`), desfazer a confirmação de depósito
  (`DesfazerConfirmacaoAsync`), sugerir quem chamar para o horário que vagou
  (`CandidatosParaAsync`), conferir o documento pelo código impresso (`PorCodigoAsync`) e
  apagar modelo/protocolo. **Antes de dar por pronta uma feature, procure o chamador em
  produção** — e, desde a parcela 36, procure também o leitor em OUTRO MÓDULO: dado que
  só o módulo que o grava consegue ler é a mesma falha vestida de arquitetura — `dotnet test` verde e CI verde não provam que a clínica alcança a função.
  Regras que a parcela fixou: no custo por sessão **só entra saída COM atendimento** (a
  baixa digitada à mão não é de sessão nenhuma, e rateá-la daria a cada uma um custo que
  não teve) e **média sem base é nula, nunca zero**; a lista de espera filtrada **diz que
  está filtrada** (título e texto do vazio mudam — "ninguém espera" e "ninguém serve para
  este horário" são respostas diferentes); a conferência por código é **o que este sistema
  oferece no lugar do ICP-Brasil**, então não podia continuar sem tela onde digitar; e
  **modelo e protocolo se apagam mesmo** — não são registro do que aconteceu, e as sessões
  e documentos feitos com eles não mudam porque aplicar **copia**, nunca referencia.
- **Meta e teto são a mesma ideia aplicada aos dois lados** (`MetaService` parcela 28,
  `OrcamentoService` parcela 31): antes das duas, o painel comparava tudo com o **mês
  anterior** — e variação responde "melhorou?", nunca "chegamos onde a gente disse que ia
  chegar?". Sem alvo, um mês 10% melhor que um mês ruim aparece como vitória, e gastar 10%
  menos que um mês caro aparece como economia. As duas entidades seguem as mesmas regras:
  **fato datado** (uma linha por mês; reajustar agosto não reescreve julho, como a vigência
  da taxa e do preço), **índice único** (dois alvos para a mesma pergunta dariam duas
  réguas e a tela escolheria uma sem dizer qual) e **ausência ≠ zero** — sem linha é "não
  decidimos", zero é "decidimos que é zero", e inventar zero faria todo mês não planejado
  aparecer como alvo batido. O realizado vem sempre do **serviço dono** do indicador, nunca
  recalculado. Duas assimetrias que parecem detalhe: **não se projeta mês com menos de 5
  dias** (projetar no dia 2 multiplica ruído por quinze) e **ocupação não se projeta por
  regra de três**, porque ela já é uma média e multiplicá-la pelos dias que faltam daria
  300%. No orçamento, o **comprometido** (conta prevista que ainda vai vencer) conta
  separado do gasto: não é dinheiro que saiu, mas é dinheiro já decidido — e o teto costuma
  estourar no que foi CONTRATADO, não no que foi pago.
- **Apuração por tributo e resultado do mês** (parcelas 28 e 31): `TributoService` separa
  ISS, PIS, COFINS, IRPJ e CSLL desde a parcela 15 e **toda tela consolidava num número
  só** — a clínica sabia quanto de imposto saiu e não sabia **de quê**, que é a pergunta do
  contador. Na apuração, a **retenção é resolvida por recebimento** com o convênio dele:
  apurar o mês com os tributos gerais somaria o que a clínica recolhe com o que a operadora
  já reteve. A **divergência** entre o apurado agora e o gravado na época **aparece** em vez
  de ser escondida — ela significa que uma alíquota mudou depois de o dinheiro entrar, e é a
  clínica que decide qual número vai para a guia. O `ResultadoMensalService` é **regime de
  CAIXA e a tela diz isso**: competência exigiria provisão e rateio que a clínica não
  mantém, e número aproximado apresentado como contábil é pior que número honesto
  apresentado como caixa. **Taxa e imposto são DEDUÇÃO, não despesa** (saem da receita antes
  de ela existir; listá-los junto do aluguel faria a clínica achar que pode cortá-los), e
  **margem sobre zero é nula** — 0% faria um mês sem faturamento parecer um mês que faturou
  e não sobrou nada.
- **O papel que sai da agenda** (`AgendaPdfService`, parcela 29): a folha do dia e o
  comprovante do paciente. Os dois saem **sem dado clínico** — a folha circula pela sala e
  pela recepção, e o comprovante é papel avulso; nome, horário, profissional e sala bastam.
  Cancelados e faltas entram na folha **marcados, nunca sumindo**: quem lê às 14h precisa
  saber que o horário das 15h vagou, e linha ausente se confunde com horário que nunca
  existiu.
- **Aniversário e padrão de falta** (`RelacionamentoService`, parcela 29): as duas perguntas
  do balcão que o sistema gravava e ninguém lia. A data de nascimento está no cadastro desde
  sempre — a ligação mais barata que a clínica faz era a que se perdia. A janela cobre a
  **semana** porque a clínica não abre todo dia. Do outro lado, a agenda registra `Faltou`
  desde a parcela 1 e os indicadores calculam a taxa da **clínica**; a do paciente nunca foi
  lida, e é ela que decide se vale dar a alguém o horário mais disputado da semana.
  **Cancelamento avisado conta separado da falta** (quem desmarcou deu chance de reocupar) e
  a reincidência **exige base mínima**: uma falta em duas sessões dá 50% e não diz nada.
- **Acerto de inventário** (`EstoqueService`, parcela 30): a clínica só podia lançar
  `Perda` quando a contagem não batia — e **perda é uma AFIRMAÇÃO**: alguém quebrou, venceu
  ou extraviou. Quando a contagem acha A MAIS, ou quando a diferença vem de erro de
  digitação antigo, chamar de perda mente sobre o que aconteceu, e é essa mentira que faz o
  custo médio do insumo parar de valer. O tipo novo não precisou de migration (o enum é
  gravado como texto); o que virou coluna foi a **direção** do acerto, e ela é campo
  separado em vez de quantidade negativa porque negativa espalharia `Math.Abs` por todo
  cálculo de saldo — um esquecido daria saldo negativo silencioso. O método recebe a
  quantidade **CONTADA**, nunca a diferença: quem está com a caixa na mão conta "tenho 37".
- **Quem parou de vir** (`RetencaoPacienteService`, parcela 32): o recall dispara mensagens
  por regra de tempo; esta é a **lista**, para a direção olhar caso a caso e decidir quem
  vale um telefonema de verdade — o paciente de tratamento longo que some vale dez recalls
  disparados no vazio. **Quem tem sessão futura marcada não sumiu**, por mais tempo que faça
  desde a última: ele já voltou, só ainda não veio. A base é o **atendimento**, não o
  agendamento (cancelado não é visita). Pacote em aberto é **destaque, não filtro**: o
  paciente pagou sessões que não usou, e essa ligação a clínica deve tanto a ela quanto a
  ele.
- **O consultório do profissional** (`ConsultorioService`, `AvaliacaoClinicaService`,
  parcela 36): o quinto app (`Clinica.Clinico.exe`, "Consultório"), na máquina do médico e
  do fisioterapeuta. Ele responde uma pergunta que nenhuma tela respondia: **o que eu
  atendi e ainda não escrevi.** É a mesma família do defeito que dá nome ao produto — a
  guia obtida +24h depois que ninguém lembra —, só que do lado clínico: a sessão acontece,
  o paciente vai embora e a evolução fica "para depois", que é o dia em que já não se
  lembra o que foi feito. A pendência olha **só dias anteriores**: a sessão das 14h não tem
  evolução às 14h05 porque o paciente ainda está na sala. O casamento sessão × evolução é
  pelo `Evolucao.AgendamentoId` e cai para paciente + data quando ele é nulo — sem esse
  caminho de baixo, a evolução escrita direto no prontuário faria o consultório cobrar para
  sempre um registro que já existe. **Sem `Profissional` vinculado ao login o app mostra o
  dia da clínica inteira e DIZ que está fazendo isso**: tela vazia se lê como defeito, não
  como cadastro faltando. O `PacienteEmFoco` é singleton porque no consultório o paciente é
  **contexto**, não parâmetro de tela — quem atende escolhe uma vez e passa vinte minutos
  entre quatro telas sobre a mesma pessoa. E o módulo não declara `Inicial`: o exe carrega
  um módulo só, então o primeiro item já é a abertura, e marcá-lo faria o Consultório
  vencer o painel da direção dentro do Gerente (o defeito que a parcela 22 corrigiu).
- **O Consultório não é uma ilha** (parcela 36): a primeira versão do módulo gravava
  `AvaliacaoClinica` e evolução, e **só ele as lia** — a sexta ocorrência do defeito
  recorrente do projeto, cometida por quem já o tinha documentado cinco vezes. O que
  fecha o circuito, nos dois sentidos:
  **Consultório → Gerente**: `PainelDirecaoService` ganhou `AssuntoDirecao.ProntuarioEmAberto`
  — a direção é a única que enxerga a clínica inteira, e o número vem do
  `ConsultorioService`, dono da leitura. Guia já faturada sem evolução é alerta
  **separado e mais grave**: a sessão sem registro é prontuário incompleto, a guia sem
  registro é cobrança sem o documento que a sustenta. No BI,
  `ProdutividadeProfissional.CompletudeProntuario` divide dois números que estavam lado a
  lado desde a parcela 5 sem ninguém os dividir — e é **nula, nunca 0%**, sem sessão
  atendida, senão acusaria de negligência quem tirou férias.
  **Consultório → Recepção/paciente**: as escalas entram no **relatório de evolução** e no
  prontuário da Recepção. O relatório é o papel que o paciente leva ao convênio, e sem
  isso ele descreveria metade do tratamento; a faixa sai como foi **gravada na aplicação**,
  não recalculada pela definição de hoje.
  **Recepção/Financeiro/Faturamento → Consultório**: o `ElegibilidadeService` aparece na
  tela de atendimento. Ele foi feito para o balcão — o lugar onde o paciente está de corpo
  presente —, e o consultório é o segundo: a urgência viaja **com cada alerta**, porque
  carteirinha vencida (vermelho) e dívida (amarelo) chegam juntas e pintar as duas da cor
  da pior faria a segunda parecer impedimento.
  **O que NÃO foi feito, e por quê**: o `PrevencaoGlosaService` seria o lugar natural do
  aviso "guia sem prontuário", e ele é chamado **só pelo app congelado**. Acrescentar o
  alerta mudaria o comportamento do faturamento em produção e dispararia para TODA guia
  numa clínica que ainda não documenta — alerta que dispara para todo mundo é alerta que
  ninguém lê. A leitura foi para a direção, onde é nova.
- **A folha de infusão e a checagem de enfermagem** (`PrescricaoInternaService`,
  `ChecagemPrescricaoService`, parcela 42): a clínica faz infusão, e a folha que ela usa
  não é a receita — é prescrição de vários itens "destinada ao próprio consultório, o
  paciente não vai apresentar lá fora". Entidade NOVA, e não um valor a mais em
  `TipoDocumentoClinico`: `DocumentoClinico` é fato IMUTÁVEL emitido (é essa regra que
  garante a segunda via idêntica), e esta tem ciclo de vida — rascunho → assinada →
  executada → encerrada. Enfiar estado vivo lá quebraria as outras sete impressões.
  **Checar não é preencher um campo**: é afirmar "foi prescrito assim e foi realizado
  assim" e responder por isso — o peso da baixa da guia, do lado clínico. O ✓ com o
  horário é o realizado; a **"rodela"** (horário circulado) é o não realizado, e ela
  **exige justificativa** — terceira recusa do projeto, junto da divergência do
  fechamento de caixa e do descarte de problema.
  As regras: **a hora é INFORMADA, nunca `DateTime.Now`** (a técnica administra às 14h e
  registra às 14h20; o relógio vai em `RegistradoEm` AO LADO, e a diferença entre os dois
  é o que uma auditoria de enfermagem procura); **hora futura é recusada**, porque
  pré-checagem é o hábito que faz um item aparecer como feito num paciente que saiu antes
  de recebê-lo — é a única regra do projeto com relógio injetado, e é assim porque regra
  de segurança que não dá para testar apodrece sem ninguém notar; **checagem não se apaga,
  RETIFICA-SE** (linha nova apontando a anterior, com motivo — apagar e regravar é
  exatamente o gesto que a auditoria procura, e a folha impressa mostra as duas);
  **item já checado não se edita nem se suspende**, senão a folha desdiz uma execução já
  assinada; **quem checa é quem fez LOGIN** (daí o perfil `Enfermagem` e a permissão
  `ChecarPrescricao`, separada de `Prescrever` — é serem duas pessoas que dá valor à
  conferência); e **o "se necessário" não conta como pendência**, senão toda folha com SOS
  ficaria eternamente aguardando e o contador da sala apontaria para nada.
  **O circuito de volta**: o exemplo que a clínica deu foi "o paciente apresentou reação
  alérgica e não quis fazer a dipirona" — e essa frase morreria num campo de texto. Agora
  a não realização por reação grava a **alergia na lista de problemas no mesmo
  SaveChanges**, e a conferência da parcela 40 acende na próxima prescrição.
- **A certificação digital, e por que NÃO se escaneia o carimbo** (`AssinaturaDigitalService`,
  `CertificadoIcpBrasil`, `AssinaturaDePrescricaoService`, parcela 42): o pedido original
  era escanear o carimbo e colar no PDF. Isso é **pior do que não assinar**: imagem não
  prova autoria (qualquer um copia para outro documento), e a partir do dia em que está no
  banco existe uma assinatura da médica reutilizável por quem tiver acesso ao sistema — o
  pior é que ela PARECE garantia, que é o que `DocumentosClinicosPdfService` se recusa a
  fazer desde a parcela 3.
  O que se faz é **assinatura qualificada ICP-Brasil**: PKCS#7 destacado SHA-256 no PDF
  (PdfSharp + `SignedCms`), carimbo do tempo RFC 3161 **opcional** (Configurações →
  Operação). O que NÃO se faz é LTV/PAdES-LT — anunciá-lo sem implementá-lo seria a mesma
  mentira do carimbo escaneado.
  **O CPF sai de DENTRO do certificado** (OID `2.16.76.1.3.1`, no *subjectAltName* como
  `otherName`; o .NET não expõe `otherName`, então o ASN.1 é lido à mão). Não é
  preciosismo: é a metade que faz a assinatura valer. Sem comparar esse CPF com
  `Profissional.Cpf`, o sistema provaria só que ALGUÉM com ALGUM token assinou, e o e-CPF
  da recepcionista assinaria a prescrição da médica sem um alerta. Certificado sem CPF,
  vencido, ou de outra pessoa é **recusado**; profissional sem CPF cadastrado também, e
  isso é decisão — aceitar em silêncio faria a conferência existir só para quem já tinha o
  campo preenchido.
  **UMA assinatura eletrônica por folha, e é a de quem prescreve** (decisão da clínica,
  ago/2026 — a primeira versão exigia duas). Quem confere e assina a EXECUÇÃO é a
  enfermeira, **na via impressa**, depois que a folha sai da impressora: por isso a
  Prescrição sai com as **colunas de checagem em branco** ("Feito às", "Visto") e três
  linhas para o motivo do não realizado. A prescritora assinar um PDF com esses campos
  vazios é correto — eles são pré-impressos do formulário, como no talão de papel; ela
  atesta o que MANDOU fazer, e o que foi feito entra por cima, à mão. Isso dispensou um
  segundo e-CPF (um por técnica) para produzir, com muita cerimônia, a garantia que a
  caneta dela já dava — e de quebra resolveu a restrição de o PDF não se assinar
  incrementalmente. O **Registro de execução** continua existindo como espelho eletrônico
  do que foi checado (prontuário e conferência do fim do dia), é montado NA HORA porque
  muda a cada item, e o rodapé dele diz que a autoria está no papel.
  **A reimpressão devolve os BYTES GUARDADOS**, nunca um PDF novo: a assinatura cobre uma
  faixa de bytes do arquivo, e um documento "igual" regerado agora abriria como inválido —
  por isso `ArquivoAssinado` é tabela e não cache. E **a folha nunca promete mais do que
  garante**: o rodapé escreve o nível da assinatura e, sem ACT contratada, diz que a data é
  declarada pelo relógio de quem assinou.
- **O documento que SAI da clínica, e a lei que manda nele** (`ConformidadeDocumentoClinico`,
  `AssinaturaDeDocumentoClinicoService`, parcela 43): a parcela 42 pôs assinatura
  qualificada na folha de infusão — que é o **único documento que a lei dispensa** (art. 13,
  parágrafo único, da Lei 14.063/2020: "não se aplica aos atos internos do ambiente
  hospitalar") — e deixou sem assinatura os quatro que a lei realmente disciplina. O motor
  estava pronto e testado; **faltava a porta**, na variante mais cara do defeito recorrente.
  O mapa completo está em `docs/prescricao-eletronica-conformidade.md`. As regras:
  **atestado em ARQUIVO só vale com assinatura qualificada** (art. 13) — não é documento com
  defeito, é arquivo sem valor —, enquanto **em papel, assinado à caneta, ele sempre valeu**;
  por isso a conferência muda de resposta conforme a forma de entrega, e o aviso SOME quando
  a folha vai ser assinada. Receita, pedido de exame e declaração caem no art. 14 (avançada
  **ou** qualificada) e assinamos com a qualificada mesmo assim, porque é a que a farmácia
  confere sozinha, num validador público, sem cadastro em plataforma nenhuma.
  O **conteúdo** é a metade que ninguém lembra: o art. 35 da Lei 5.991/1973 exige **endereço
  residencial do paciente** e **modo de usar** para a receita ser AVIADA, e o sistema
  imprimia receita desde a parcela 3 **sem ter onde guardar o endereço** — a clínica
  descobria na farmácia, com o paciente na fila. O endereço sai **só na receita** (num
  atestado iria para o RH sem exigência que o justifique — a economia do CID).
  A emissão **avisa**; a **assinatura RECUSA**, e é a quarta recusa do projeto: assinar sela
  os bytes, corrigir depois exige cancelar e emitir outro, e um PDF criptograficamente
  impecável de uma receita que a farmácia não pode aviar é a **garantia aparente** que este
  projeto se recusa a produzir desde a parcela 3.
  **Não precisamos de portal de validação**: `assinaturadigital.iti.gov.br` — o validador
  de documentos de SAÚDE do ITI, com apoio do CFM e do CFF — é público e gratuito, e é o
  que os CRFs mandam o farmacêutico usar. Não confundir com o genérico
  (`validar.iti.gov.br`): só o de saúde responde **"quem assinou é prescritor com registro
  ATIVO?"**, que é a pergunta que decide a dispensação, e é onde ela é registrada. O
  documento assinado sai com o endereço por extenso, um **QR** que leva à página do
  farmacêutico e um bloco **"PARA O FARMACÊUTICO"** com o passo a passo — PDF assinado que
  chega sem uma palavra sobre como conferir é recusado por precaução, e com razão: pela
  orientação dos CRFs, farmácia que não consegue verificar não é obrigada a dispensar. O QR
  leva ao validador e **não carrega o documento** (quem hospeda receita é plataforma —
  Memed, Mevo, CFM); prometer que ele "abre a receita" seria mentir sobre o que ele faz. E **a via impressa de
  um documento assinado é CÓPIA** (a assinatura vive nos bytes, não na tinta), então assinar
  **salva e abre o arquivo**, nunca manda para a impressora. A reimpressão devolve os
  **bytes guardados**, e a regra mora dentro do `DocumentosClinicosPdfService.GerarAsync` de
  propósito: são seis telas chamando, e uma que esquecesse produziria segunda via inválida
  sem nenhum sinal. O seletor de certificado subiu para o **shell** pelo argumento de
  sempre — capacidade que existe numa porta só é o defeito de novo, aqui com o agravante de
  ser a assinatura que dá valor jurídico ao arquivo.
- **Prescrever o alérgeno que a própria clínica anotou** (`PrescricaoService`, parcela 40):
  desde a parcela 37 o sistema GUARDA as alergias (`NaturezaProblema.Alergia`, com a regra
  de alertar mesmo dadas por resolvidas) e a emissão de receita **nunca as consultou** — a
  base sabia que a paciente é alérgica a dipirona, o profissional escrevia "Dipirona
  500mg" e o papel saía sem uma palavra. É o mesmo defeito recorrente do projeto (dado
  gravado sem leitor), só que aqui ele não custa uma guia.
  **O que o serviço NÃO é**: checador de interação medicamentosa. Isso exige base
  farmacológica licenciada e atualizada, e uma checagem caseira erraria nos dois sentidos
  e — pior — passaria a impressão de estar cobrindo o assunto. Ele compara o que está
  sendo prescrito com **o que a própria clínica anotou sobre este paciente**.
  A comparação é **textual** (a receita é texto livre por desenho) e por isso tem dois
  cuidados que valem tanto quanto o alerta: **palavra INTEIRA, nunca trecho** ("sal" dentro
  de "salbutamol" é coincidência de letras) e **piso de 4 caracteres** com lista de
  dispensáveis — sem eles, uma alergia anotada como "alergia a X" acenderia em toda receita,
  e **alerta que dispara à toa é alerta que se fecha sem ler**, o que produz o falso
  negativo na semana seguinte. Os testes cobrem as DUAS direções com o mesmo peso.
  **Avisa e exige confirmação — não impede**: o registro pode estar errado, pode haver
  dessensibilização, e quem decide é quem assina; mas não pode acontecer *sem alguém
  perceber*. É o segundo caso do projeto em que a tela cobra confirmação explícita (o
  primeiro é a divergência do fechamento de caixa). A conferência mora no **shell**
  (`DocumentoEdicaoViewModel`), não na tela do Consultório: é o único lugar por onde toda
  receita passa, nas duas portas — **checagem de segurança que só existe em uma delas é o
  defeito de novo, com a agravante de dar a impressão de estar coberto**. Ela reconfere
  no clique de emitir, porque a da abertura viu uma receita em branco; e **falha da
  conferência não bloqueia a emissão** (banco lento não pode impedir um atestado), mas
  também não passa calada — vira aviso de que a checagem não rodou.
  **Medicação contínua entra como CONTEXTO, sem casar com item**: o valor dela é o que
  ainda não foi escrito, e casá-la produziria "você prescreveu o que ele já toma", que é o
  caso normal da renovação de receita.
- **Botão aceso que não faz NADA** (parcela 41, 2ª rodada — regressão que o cliente pegou):
  os quatro botões de emitir da tela de Prescrições não abriam janela nenhuma. O comando
  começava com `if (_pacienteId == 0) return;` e voltava **calado**, enquanto o `IsEnabled`
  só olhava a permissão — e a tela abre pela sidebar **sem paciente em foco**. Quem clica e
  não vê nada acontecer conclui que o sistema quebrou; não tem como adivinhar que faltava
  escolher alguém. O mesmo defeito estava na tela de Prescrições da RECEPÇÃO.
  A regra do projeto já dizia isto para PERMISSÃO ("duas barreiras: `IsEnabled` explica,
  `Exigir` impede"); ela vale para **toda pré-condição**: o botão diz que não dá, e a
  guarda diz por quê. **Guarda que volta em silêncio é botão que não faz nada.**
  A **checagem 21** cobra as duas metades juntas, e o cuidado está nas EXCEÇÕES — sem elas
  a checagem viraria ruído e alguém a desligaria: guarda sobre **parâmetro** (`if (linha is
  null)`) nunca dispara vindo de botão de linha; guarda de **reentrância**
  (`if (Carregando)`) é "já estou fazendo", não "não dá para fazer", e ali o botão deve
  mesmo ficar aceso; e guarda sobre **variável local** (`var caminho = Escolher(); if
  (caminho is null)`) é diálogo cancelado, onde sair calado é o certo. O casamento
  tela↔comando é pela convenção de nome (`FooView.xaml` ↔ `FooViewModel`), nunca pelo nome
  do comando solto — `EditarCommand` existe em meia dúzia de telas que não se conhecem.
  Ela também **ignora comentários** antes de procurar a guarda: um bloco explicativo de três
  linhas empurrava a guarda para fora da janela de busca, e foi assim que a checagem deixou
  de ver o defeito na primeira tentativa.
- **O nome do enum vazando para a tela** (`RotulosEnum`, parcela 41): o CLIENTE achou em
  produção — o seletor de tipo da tela de documento clínico oferecia **"PedidoExame"**. A
  causa é de uma linha: `ComboBox` amarrado a uma lista de enum sem `DisplayMemberPath`
  nem `ItemTemplate`, e o WPF, sem nada melhor, chama `ToString()`. A varredura mostrou
  que eram **10 enums em 16 telas** ("RelatorioEvolucao", "CartaoCredito",
  "PercentualDaReceita", "CreditoAVista"). O rótulo certo do documento já existia
  (`TipoDocumentoInfo.Rotular`, parcela 3) e a tela não o usava — **a variante mais barata
  do defeito recorrente do projeto**: o build passa, o teste passa, e só quem abre a tela
  vê. `RotulosEnum.De` é o ponto único que resolve por tipo e reaproveita os rotuladores
  que já existiam; `ItemRotuloEnum` é a porta de XAML. Enum sem rótulo declarado cai no
  **humanizador** ("Pedido exame"), que é pior que o rótulo à mão e melhor que o
  identificador — e de propósito ele não some: a frase estranha é o que faz alguém vir
  escrever o rótulo. A **checagem 20** casa o `ItemsSource` com o TIPO declarado no VM e só
  reclama quando é enum (lista de string não precisa de rótulo); no **faturamento
  congelado** ela vira AVISO, porque o defeito está lá e não se corrige por decreto de
  leiaute — esconder seria fingir que a suíte está limpa quando a tela do cliente não está.
- **Botão ao lado de campo usa a ALTURA do campo** (parcela 41): campos têm `MinHeight`
  36 e `BotaoPequeno` (base de todos os `BotaoAcaoGrid*`) tem 26. Numa `Grid`, o botão
  baixo é esticado até a altura da linha e sai com o padding errado — é o **"fora do
  esquadro"** que o cliente viu na tela de documento clínico, em cinco linhas de uma vez.
  `BotaoPequeno` é para linha DENSA de tabela; barra de formulário usa `BotaoSecundario`
  com `VerticalAlignment="Center"`. E **destrutivo repetido não é sólido**: um "Remover"
  vermelho cheio por linha de receita faz o formulário inteiro parecer tela de erro e gasta
  a cor mais forte da paleta na ação menos frequente — daí o `BotaoSecundarioPerigo`
  (contorno). O sólido fica para o destrutivo que é o ASSUNTO da tela.
- **A sidebar do Consultório tinha TRÊS itens** (parcela 39): e não porque o app do médico
  seja simples — porque as portas estavam no módulo errado. A auditoria achou três lacunas
  da mesma família, e é a **sétima** ocorrência do defeito recorrente do projeto, na
  variante mais discreta de todas: não é dado sem leitor nem serviço sem chamador, é
  **capacidade inteira e testada cuja única porta está no módulo de quem não a usa**.
  (a) **Prescrições** — receita, atestado, comparecimento e pedido de exame só se emitiam
  pela RECEPÇÃO. Quem prescreve é quem atende, e o app instalado na sala do médico não
  tinha por onde; a parcela 36 já tinha subido a emissão para o shell (`DocumentoWindow`)
  exatamente por isto, e ninguém construiu a porta. A tela do consultório **abre no
  paciente em foco** e oferece os quatro tipos como BOTÕES, porque ali a decisão vem antes
  do clique: ninguém pensa "vou emitir um documento", pensa "vou dar um atestado".
  (b) **Minha semana** — "Meu dia" responde o que acontece hoje e não responde o que se
  pergunta com o paciente ainda na frente ("quando eu tenho espaço?", "quinta está
  cheia?"). A recepção tem visão de semana desde a parcela 26. Sete dias numa consulta só,
  não sete — o banco é remoto; a semana começa na **segunda** (o domingo é o SÉTIMO dia, e
  tratá-lo como primeiro devolveria a semana que só começa amanhã); **dia sem horário
  aparece vazio**, porque semana com cinco colunas faria procurar a quarta que sumiu.
  (c) **Meus números** — `ProdutividadeProfissional` existe desde a parcela 5 e
  `CompletudeProntuario` desde a 36, e o único leitor de ambos era o BI do GERENTE: o
  sistema media quem atende e a pessoa medida não via o próprio número. **Indicador que só
  o chefe enxerga não corrige comportamento nenhum** — ele só produz a conversa
  desagradável no fim do mês. A tela mostra **só quem está logado e não compara colegas**
  (ranking é decisão de gestão; no app de cada um viraria placar), e a **dívida de
  prontuário é de HOJE, não do período**: ela é fila de trabalho, e recortá-la pelo filtro
  faria escolher "mês passado" esconder o que está em aberto agora.
  A lição para a próxima auditoria: **ao procurar chamador em produção, conte também
  quantos ITENS DE MENU o módulo tem.** Sidebar curta demais para o que o app faz é
  sintoma, não simplicidade.
- **A chamada do próximo paciente** (parcela 38): "Meu dia" do Consultório era uma **lista
  corrida** — ela responde "quem vem hoje" e não responde a pergunta que quem atende faz
  vinte vezes por dia, "**quem já está aí e quem eu posso chamar agora**". Virou kanban de
  cinco colunas, as MESMAS da fila do balcão (que é kanban desde a parcela 26). E ganhou o
  recado de volta: quem chama pelo nome na sala de espera é a **recepção** — o profissional
  está na sala, com a porta fechada —, então o botão "Chamar próximo" carimba
  `Agendamento.ChamadoEm` e a fila do balcão anuncia a pessoa. **A sincronização é o
  BANCO**, como todo o resto da suíte: nem fila de mensagens, nem evento, nem um módulo
  conhecendo o outro; os dois leem a mesma linha, e é isso que faz os dois quadros nunca
  divergirem. O estágio `Chamado` **não virou coluna no banco** — é derivado dos carimbos
  de hora, como os outros quatro (`Agendamento.Etapa`); o que precisou de migration foi só
  o fato novo, e ela é aditiva (o faturamento congelado só lê `StatusAgendamento`).
  As regras: **só se chama quem já fez check-in** (anunciar quem não chegou tiraria da fila
  a única informação que a torna confiável, a de quem está no prédio); **chamar de novo não
  reinicia o cronômetro** (`??=`) porque quem insiste precisa ver há QUANTO tempo chamou, e
  o segundo clique esconderia justamente o caso demorado; **chamar existe dos dois lados**
  (em metade das clínicas o profissional avisa pela porta, e exigir o clique da sala faria
  a coluna nascer sempre vazia num fluxo que funciona há anos — carimba quem clicar
  primeiro); **entrar carimba a chamada junto**, porque linha do tempo com entrada e sem
  chamada não existe; e a **espera para na CHAMADA, não na entrada** — o que se mede é
  quanto tempo o paciente ficou sem notícia, e contar até ele levantar da cadeira somaria o
  tempo de atravessar a sala. A **releitura periódica** (1 min à época; 30 s no Meu dia
  desde set/2026 — ver a lição do intervalo mais abaixo —, ligada/desligada pelo
  Loaded/Unloaded da View) é o que faz o recado CHEGAR: até aqui as duas telas só reliam
  por clique, o que bastava porque tudo o que mexia no quadro era clicado nelas mesmas. Ela
  é **silenciosa** — não acende "Carregando" nem escreve erro, porque quem está no balcão
  com um paciente à frente não pode ver a fila piscar em branco a cada minuto — e só relê
  **hoje**. A faixa acima do quadro **nomeia quem chamar e para qual sala**: a coluna
  sozinha não bastaria, porque cartão que muda de coluna em silêncio é cartão que ninguém
  vê, e "1 paciente chamado" mandaria a recepcionista procurar antes de abrir a boca.
- **O que o profissional precisa ver e não alcança** (parcela 37): a auditoria do módulo
  do Consultório achou três lacunas da mesma família, e nenhuma pedia capacidade nova —
  pedia **porta no módulo certo**, que é a variante mais discreta do defeito recorrente
  do projeto (o CI fica verde e a Recepção usa todo dia). (a) **Anexos**: `AnexarAsync`
  existe desde a parcela 2 e só a Recepção o chamava — o Consultório EMITIA pedido de
  exame e não tinha onde ler o laudo de volta; ele pedia e não recebia. (b) **Busca no
  prontuário**: existe desde a parcela 28, dentro do módulo da Recepção, e o comentário
  que a acompanha lá diz "a pergunta que o profissional faz antes de atender é sempre a
  mesma" — a feature foi justificada pelo profissional e entregue na tela de quem não
  atende; a de Atendimento mostra três sessões, e num tratamento de quarenta a sessão 12
  era inalcançável. (c) **Contagem de anexos** era uma consulta POR SESSÃO
  (`ContagemDeAnexosAsync` a fez virar uma só). Ao procurar chamador em produção,
  procure também **em qual módulo está a tela** que lê o que este grava.
- **Medidas seriadas** (`MedidaClinicaService`, `CatalogoMedidas`, parcela 37): a parcela
  36 deu número às cinco especialidades pelas escalas e deixou de fora o mais básico. O
  argumento que fechou a lacuna foi o FINDRISC — ele **pergunta** IMC e circunferência de
  cintura, o paciente responde, o escore é gravado e os dados que o produziram evaporavam.
  O catálogo (peso, altura, cintura, PA, glicemia, HbA1c) mora em **CÓDIGO**, pelo desenho
  das escalas: o corte do IMC é definição publicada, não configuração da clínica.
  **Tudo o que descreve o tipo é COPIADO na colheita** (nome, unidade, faixa,
  interpretação). **O IMC não se digita** — é derivado do peso com a altura vigente, e a
  DATA dessa altura vai junto: um IMC calculado com altura de três anos atrás continua
  sendo a melhor leitura disponível, desde que quem lê saiba disso; gravá-lo criaria um
  terceiro número livre para contradizer os dois que o originam. Sem altura ANTES do peso
  cai-se para a primeira registrada, porque o adulto é pesado toda consulta e medido uma
  vez, muitas vezes depois — devolver curva vazia a quem tem os dois dados seria pior. A
  **única recusa é a implausibilidade** (2500 kg é dedo no teclado; 210 kg é anormal e
  possível, e recusá-lo esconderia quem precisa de atenção). **Faixa ausente ≠ faixa
  normal**: o peso isolado não tem leitura publicada e o selo SOME. **Meia pressão
  arterial não existe** (tipo com par exige os dois, e diastólica maior que a sistólica é
  campo trocado). Os cortes da cintura são os do FINDRISC e seguem o sexo — usar outro
  faria a mesma clínica dar duas leituras da mesma fita métrica. **Uma colheita só não é
  variação nenhuma**: `Variacao` é nula, nunca zero.
- **Lista de problemas** (`ProblemaPacienteService`, parcela 37): até aqui o CID morava só
  dentro de `DocumentoClinico` — um campo por papel emitido, nunca o diagnóstico da
  pessoa. O profissional redigitava "M54.5" a cada atestado, ninguém respondia "o que este
  paciente tem?" sem ler o prontuário inteiro, e a alergia ficava enterrada em texto livre.
  A lista é do **PACIENTE, não da sessão** (a evolução de origem é procedência opcional).
  **Não se apaga: muda-se a situação** — resolvido e descartado continuam na base, como a
  NC do faturamento. **Descartar exige motivo escrito**, única recusa do serviço, pela
  razão da justificativa do fechamento de caixa. **Alergia alerta mesmo dada por
  resolvida** — "resolvida" numa alergia é quase sempre "não reagiu da última vez", e o
  dia em que reagir é o dia em que o aviso teria valido; só o descarte a cala. **O CID é
  opcional**: exigi-lo faria o fisioterapeuta e o acupunturista pararem de usar a lista, e
  lista pela metade é pior que nenhuma porque seria lida como completa. Na tela de
  atendimento os alertas clínicos ficam em lista **separada** da administrativa: as duas
  são "avisos" e é só isso que têm em comum — carteirinha vencida se resolve no balcão
  depois, alergia se resolve ANTES de prescrever.
- **Tela que abre vazia é tela inacabada** (parcela 37, rodada de UI): as telas clínicas
  nasciam com uma caixa de busca vazia, uma coluna de 300 px em branco da altura da janela
  e um miolo de mil pixels com uma frase cinza flutuando no meio. Funcionava — e obrigava
  o profissional a DIGITAR o nome de alguém que o sistema já sabia que ele ia atender às
  9h. O `SeletorClinicoViewModel` + `PainelPacienteClinico` abrem com a **fila do dia** e a
  **carteira** (as duas já existiam, cada uma servindo a uma tela só) e deixam a busca como
  atalho para quem está fora do dia; a linha da fila leva o `AgendamentoId`, que é o que
  faz a evolução nascer ligada ao horário. Três armadilhas que a implementação encontrou e
  que valem para qualquer lista da suíte: (a) **uma ListBox por bloco não funciona** — duas
  amarradas ao mesmo `SelectedItem` se limpam mutuamente, porque a que não contém o item
  escolhido devolve `null` ao binding; use `CollectionViewSource` + `GroupStyle` sobre UMA
  coleção; (b) **remonte por busca CONCLUÍDA** (`SeletorPacienteViewModel.Atualizou`), não
  por `CollectionChanged`, que dispara uma vez por linha inserida; (c) **vazio antes da
  resposta não é resposta** — enquanto a busca do termo digitado não voltou, o painel não
  diz "nenhum paciente encontrado". A `FaixaPaciente` (avatar, nome, contexto, ações) fica
  no topo do conteúdo porque num app onde se passa vinte minutos entre quatro abas sobre a
  MESMA pessoa a identidade é âncora, não rótulo — e as ações da tela viajam com ela em vez
  de ficarem acesas no cabeçalho da página sobre uma tela sem paciente nenhum.
- **Chave de navegação sem item de menu = botão que não faz NADA** (parcela 37, 4ª
  rodada — a regressão que foi para produção). A navegação da suíte é por STRING:
  `NavegacaoSuite.Ir(chave)` faz o shell procurar a chave em `ShellViewModel.Itens`, que é
  a lista da SIDEBAR, e sem achar ele **retorna false em silêncio** — sem erro, sem log,
  sem exceção. Ao tirar as cinco telas clínicas do menu, tirei-as junto da lista: "Atender"
  na fila do dia, os atalhos da carteira e o painel da direção pararam de abrir qualquer
  coisa de uma vez. O `compilar-sombra` passou (é string), o `verificar-suite` passou (era
  C#, não XAML) e os 1023 testes passaram (nenhum monta a sidebar). A correção é
  `ItemMenuModulo.Oculto` — **navegável sem ocupar linha no menu**; `Itens` guarda tudo o
  que é destino, `Grupos` filtra o que aparece. A **checagem 19** do `verificar-suite`
  passou a exigir que toda `NavegacaoSuite.Ir(ModuloX.ChaveY)` tenha o item declarado, e é
  autotestada contra esta regressão.
- **Lista → tela do item. Não enfie mestre-detalhe numa tela só** (parcela 37, 4ª
  rodada — a correção mais cara da parcela, e a que o cliente reprovou em voz alta). As
  cinco telas clínicas tinham, CADA UMA, uma coluna de 300 px com a lista de pacientes
  grudada à esquerda. Somadas, eram seis cópias da mesma lista, metade da largura útil
  gasta com ela, e o nome do paciente repetido em toda tela — inclusive vinte minutos
  depois de ele ter sido escolhido. O desenho certo tem dois passos: **telas de LISTA com
  a largura inteira** (Meu dia, Meus pacientes) e a **tela do item** atrás de um clique
  (`PacienteWorkspaceView`: identidade no topo, uma vez, e as seções em ABAS). A regra que
  decide: **seção que só existe COM um item escolhido não é item de menu** — como item ela
  abre em branco pedindo que você vá primeiro a outro lugar, e isso ensina o usuário a
  errar; como aba, ela diz sozinha a quem pertence. As chaves de navegação antigas
  continuam valendo e caem cada uma na sua aba (`ModuloClinico.AbaDe`), porque renomear
  contrato de navegação para arrumar leiaute quebra o que funciona noutro módulo.
- **Ferramenta de uso pontual mora em BOTÃO, não em painel aberto** (parcela 37, 3ª
  rodada): o mapa corporal ocupava uma aba de 530 px permanente na tela de atendimento e o
  formulário de colher medida ocupava um terço da tela de Medidas — os dois para atos que
  acontecem uma vez por consulta. O resultado era o previsível: o mapa não cabia (as
  figuras são Canvas de 220×460 que NÃO esticam, então sobrava rolagem e os botões do
  rodapé saíam cortados pela borda da janela) e a série de medidas — que é o que se OLHA
  naquela tela — era empurrada para baixo da dobra por um formulário que ninguém estava
  preenchendo. Os dois viraram janela (`MapaCorporalWindow`, `RegistrarMedidaWindow`), o
  que de quebra deu ao mapa os 960 px de mínimo que a Recepção já lhe dava. A pergunta que
  decide: **isto é o que a pessoa VÊ nesta tela, ou o que ela FAZ de vez em quando?** O
  segundo caso é botão.
- **A ficha do paciente é LISTA → TELA DO ITEM, e as abas têm estilo** (parcela 47): a
  tela de Pacientes da Recepção era o defeito do README inteiro numa tela só — coluna de
  360 px com a lista grudada à esquerda para sempre, e a ficha à direita como pilha de
  **oito `Card` com moldura**. Virou o que a regra manda: lista de largura inteira
  (`ItemPacienteLinha`, com telefone, que só existia dentro da ficha) e a ficha atrás de um
  clique, com cabeçalho da pessoa uma vez e cinco **abas** — o MESMO desenho do
  `PacienteWorkspaceView` do Consultório. Dois desenhos para "ficha do paciente" no mesmo
  sistema é o que faz a recepcionista achar que abriu outro programa.
  O que destravou isso foi o `Styles/Componentes/Abas.xaml`: a suíte usava `TabControl`
  desde a parcela 37 e **nunca teve estilo para ele**, então o WPF desenhava o tema
  clássico — abas em forma de pasta, gradiente cinza, moldura 3D em volta do conteúdo.
  Visual de Windows XP no topo da tela, sempre visível, dentro de um app que no resto é
  plano; é a peça que mais denuncia idade. O estilo é **implícito, sem `x:Key`**, de
  propósito: as abas que já existiam no Consultório e no Gerente ficaram modernas sem
  tocar numa linha delas. O sublinhado ocupa o lugar mesmo apagado (`Transparent`, não
  `Collapsed`), senão o rótulo sobe 2 px ao ser escolhido e a régua treme a cada troca.
  No **Novo atendimento**, os quatro `Border` empilhados de aviso (carteirinha, consulta,
  cota, pendências) viraram **uma superfície com uma linha por aviso**, cor no traço de
  3 px à esquerda. Eles somavam até 280 px acima do formulário, e pioravam justamente no
  caso ruim: o paciente com quatro problemas era o que empurrava a Modalidade para fora da
  vista. A região inteira **some** quando não há aviso — em vez de quatro caixas vazias
  ocupando o lugar para dizer que não há nada.
- **O lançamento avulso é TELA CHEIA — nem faixa lateral, nem pop-up** (parcela 47,
  2ª e 3ª rodadas — a 6ª e a 7ª reprovações do cliente): a consolidação dos avisos foi
  cosmética e a tela continuava sendo o que o `README.md` proíbe — uma **faixa lateral**
  de 420 px com o formulário grudado à esquerda, permanente, para um ato que acontece
  algumas vezes por dia. A primeira correção tirou o formulário da faixa e o pôs numa
  **janela modal**; o cliente recusou também, e com razão: *"não precisa abrir uma nova
  tela, pode ser esse leiaute aí só que na mesma tela e com janela cheia"*.
  A lição corrige pela metade a regra do `README.md` — "o que a pessoa FAZ de vez em
  quando é botão ou janela" vale para o ato que acontece **DENTRO de outra tela** (o mapa
  corporal na evolução, o formulário de medida na tela de medidas). Quando o ato **É** a
  tela — o item de menu se chama "Novo atendimento" e existe só para isso —, o clique na
  sidebar já é a abertura, e exigir um segundo clique para abrir uma janela por cima é
  cerimônia sem função. **A pergunta é "isto acontece dentro de outra coisa?", não só "com
  que frequência?"**
  O desenho que ficou é o da janela com a largura da tela: cabeçalho, **barra de ação
  ancorada declarada ANTES do miolo** no `DockPanel` (senão o miolo rolante come a barra, e
  a barra é onde está o botão), miolo rolante e três passos numerados — QUEM, O QUE FOI
  FEITO, O QUE VAI SAIR. Ao fim, **LANÇADOS HOJE**: a conferência do dia, que não existia
  em lugar nenhum — quem lançava não tinha como revisar sem abrir o app de faturamento,
  que é de outra pessoa.
  Três defeitos de alinhamento que a rodada corrigiu e que valem para qualquer tela:
  (a) **barra de botões precisa de `VerticalAlignment="Center"`** — sem ele, uma mensagem
  de erro que quebra em duas linhas estica a linha da `Grid` e o WPF estica os BOTÕES
  junto; (b) **cartão em `WrapPanel` usa `Height`, não `MinHeight`** — cada filho fica com
  a altura que pede, e um nome que quebra em duas linhas deixa a fileira com a base
  serrilhada; (c) **`Button` com `Button.Template` próprio precisa declarar `Foreground`**,
  porque o estilo implícito de `Button` na suíte é o PRIMÁRIO (texto invertido) e
  `Foreground` é propriedade HERDADA — o rótulo saía branco sobre a superfície branca do
  cartão, e isso não aparece em build nenhum: só para quem abre a tela.
  O que a tela nova destravou não é leiaute, é **capacidade**: `AtendimentoService.PreverAsync`
  / `PreverModalidadesAsync`. O motor de regras sempre foi **puro** (`IRegraConvenio.Gerar`
  não grava nada), e ninguém tinha usado isso — dava para MOSTRAR o que a regra vai gerar
  antes de gerar. Cada cartão de modalidade escreve a própria consequência ("2 guias · a 2ª
  libera 09/08"), então a escolha deixa de ser às cegas: o 2º código, que é o assunto do
  produto, passa a ser anunciado no instante da decisão em vez de virar pendência amanhã.
  As regras da prévia: ela **não persiste nada** — nem atendimento, nem código, nem linha
  na agenda —, **não reabre NC**, **não renova consulta** e **não toca `paciente.Categoria`**
  (a entidade está rastreada, e a categoria calculada vazaria no próximo `SaveChanges` de
  quem quer que fosse). São N simulações em memória sobre **uma** leitura de banco, porque
  a recepcionista compara três modalidades antes de decidir e o banco é remoto. E o teste
  central é `Previa_promete_exatamente_o_que_o_lancamento_entrega`: **prévia que não bate
  com o lançamento é pior do que prévia nenhuma** — ela promete duas guias, a pessoa
  confirma, e sai uma.
- **Tela da suíte que precisa buscar ao abrir declara `ICarregarAoAbrir`** (parcela 47,
  3ª rodada): o shell monta a tela em `IModuloApp.CriarTela` e só resolve o `DataContext`
  — ele não chama método nenhum. A maioria das ViewModels dispara a busca no próprio
  construtor e funciona; as duas que vieram do FATURAMENTO na parcela 46 (Novo atendimento
  e Consultas) não faziam assim, porque lá quem chamava o `CarregarAsync` era a navegação
  do `MainViewModel` — que não existe aqui. As duas chegaram à suíte abrindo com o
  catálogo de modalidades VAZIO e a lista de consultas em branco, e **tela vazia se lê
  como sistema quebrado, não como "ninguém chamou o carregar"**. Nada disso quebra build:
  a porta existe, o serviço existe, o teste passa. O contrato resolve no **ponto único**
  por onde toda tela da suíte passa (`ShellViewModel.Navegar`), em vez de repetir a linha
  em cada construtor e depender de alguém lembrar dela na próxima porta.
- **A Recepção estava completa em FEATURE e furada em PORTA** (parcela 48): a auditoria do
  módulo achou quatro lacunas, todas da mesma família — a décima ocorrência do defeito
  recorrente do projeto — e nenhuma pedia capacidade nova.
  (a) **A cota do convênio: o balcão LIA e não ESCREVIA.** O `ElegibilidadeService` avisa
  desde a parcela 26 que a cota vai estourar (*"a próxima sessão vira glosa 2006"*), e a
  única porta para registrar a senha nova estava no app de FATURAMENTO. Quem recebe a
  senha da operadora é quem atende o telefone. É a variante que a parcela 46 já tinha
  corrigido para a consulta a renovar, e que ninguém procurou ao lado.
  (b) **O pacote só aparecia no Finalizar** — o ÚLTIMO passo, quando a sessão já
  aconteceu. Marcar a 11ª sessão de um pacote de 10 era descoberto tarde demais. A
  correção foi no **ponto único** (`ElegibilidadeService`), não na tela: de lá o alerta
  chega sozinho ao agendamento, ao check-in, à ficha e ao Consultório. **Cota e pacote são
  DOIS alertas, nunca um** — as duas contam sessões e não têm nada a ver uma com a outra:
  a cota evita GLOSA, o pacote evita ATENDER DE GRAÇA, e quem lê no balcão precisa saber
  se fala com o convênio ou com o paciente. Só avisa quem TEM ou TEVE pacote, pela regra
  de sempre: metade da clínica é de convênio e nunca comprou nada.
  (c) **O recall só rodava no Gerente**, e quem telefona é o balcão — o mesmo argumento
  que trouxe a rodada de confirmação na parcela 26, aplicado à outra ponta. A lista traz
  **todo candidato, inclusive quem a rodada não pôde gerar** (sem telefone, sem
  consentimento), com o motivo escrito: quem some da lista some da cabeça, e "12
  pacientes" que eram 30 faz a clínica concluir que quase ninguém está sumindo.
  (d) **`PerfilAcesso.Enfermagem` existia desde a parcela 42 e não levava a lugar nenhum**:
  a sala de infusão só estava no `ModuloClinico`, carregado pelo exe do MÉDICO. A tela não
  foi copiada — **subiu para o shell**, como o mapa corporal na parcela 36 —, e os dois
  módulos publicam a **MESMA chave**. Foi isso que exigiu a dedupe por chave no
  `ShellViewModel`: no Gerente, que carrega todos, a linha apareceria duas vezes, e item
  repetido faz a pessoa clicar nos dois para descobrir que são a mesma tela.
  A lição para a próxima auditoria, somada à da parcela 39 ("conte os itens de menu"):
  **procure o AVISO que a tela dá e pergunte se a porta para resolvê-lo está no mesmo
  app.** Alerta sem porta é pior que alerta nenhum — ele ensina a pessoa a ignorá-lo.
- **Permissão granular que não distingue o que a clínica distingue não é granular**
  (parcela 49): a direção apontou que "não adianta ter permissão granular se todo perfil
  nasce podendo tudo". Os padrões não davam literalmente tudo — dois davam demais, e a
  causa não era escolha, era o **bit sobrecarregado**: `VerProntuario` significava "abrir
  a ficha E ler a evolução" e `EditarProntuario` significava "cadastrar paciente E
  escrever no prontuário". Não havia como conceder um sem o outro, então a recepcionista,
  que precisa do cadastro para marcar horário, lia a evolução inteira de todo mundo. A
  granularidade existia na TELA e não no domínio.
  O corte novo (`VerFichaPaciente`/`EditarPaciente` × `VerProntuario`/`EditarProntuario`)
  é o da **LGPD**: dado de contato de um lado, dado sensível (art. 5º, II) do outro. As
  três perguntas que decidiram cada linha estão em `docs/permissoes-por-perfil.md`, com a
  tabela completa: (1) **a pessoa precisa disto para trabalhar?** — não "pode dar sem
  risco"; bit que ninguém usa vira bit que ninguém revisa; (2) **o ato apaga o trabalho de
  outra pessoa ou some com uma cobrança do sistema?** — daí saírem `EstornarBaixa` e
  `MarcarNaoConformidade` do Faturista; (3) **é dado de saúde?**
  ⚠️ **Isto TIRA capacidade de quem já a usava, e é de propósito.** A regra 3 do bloco do
  faturamento ("não tire função de quem a tinha ontem") vale para efeito COLATERAL de
  atualização; aqui a remoção É o pedido. O que a regra continua exigindo é que a
  devolução seja barata: cada bit se concede de volta a uma pessoa específica em Acessos,
  num clique, sem mexer no perfil dos outros.
  E a tela passou a **mostrar a decisão**: agrupada por assunto, com a CONSEQUÊNCIA ao
  lado de cada caixinha (`PerfisAcesso.Explicar`) e a PROCEDÊNCIA de cada uma (padrão da
  função, liberada à mão, tirada à mão). Vinte e quatro caixinhas numa lista corrida não
  são uma decisão, são um formulário — e é lendo por bloco que se percebe o bit solto que
  ninguém queria ter concedido.
- **Cadastro grudado ao lado da operação é faixa lateral** (parcela 49): as últimas cinco
  faixas da suíte estavam no Financeiro, e todas eram a mesma coisa — o CADASTRO (raro)
  ocupando 320–400 px permanentes ao lado da OPERAÇÃO (diária). Catálogo de pacotes,
  contas fixas, regras de repasse, validades do estoque e a alíquota única. Viraram botão
  + janela, e a janela recebe **o MESMO ViewModel** da tela de trás: catálogo e vendidos
  saem da mesma leitura, e dois VMs dariam duas verdades sobre a mesma tabela.
  A alíquota única do Taxas ganhou o tratamento que faltava: ela **só é aplicada enquanto
  não há tributo cadastrado**, e agora **some** quando há. Campo que não faz nada é pior
  que campo nenhum — alguém o preenche e conclui que mexeu no imposto.
  Duas armadilhas da varredura: coluna de 300–400 px numa tabela de largura inteira é a
  coluna de **Ações**, não faixa (dois falsos positivos), e trocar `BotaoPequeno` por
  `BotaoSecundario` em bloco atinge o botão de LINHA da lista, onde `BotaoPequeno` é o
  certo — e o resultado é `VerticalAlignment` duplicado, que o XML nem parseia.
- **Dica ESTÁTICA não é aviso: o campo precisa criticar a cada tecla** (parcela 51 — o
  cliente digitou oito letras num convênio "só números" e perguntou por que conseguia).
  Deixar digitar é por desenho: quem recusa é `FaturamentoService.DarBaixaAsync`, porque a
  baixa tem QUATRO portas e validar na tela cobriria uma. O que faltava era a outra metade
  da regra já escrita aqui — "a tela usa a MESMA regra para avisar ANTES do clique". Ela
  existia como **dica fixa** ("aceita somente números"), resolvida uma vez ao carregar a
  guia, e **nada acontecia enquanto se digitava**: oito letras sem uma palavra do sistema
  se leem como "aceitou", e a recusa só aparecia no Confirmar.
  Agora a crítica é derivada do que está no campo (`CriticaNumeroGuia`, a mesma
  `RegraNumeroGuia.Criticar` — sem cópia) e o botão fica apagado enquanto o número não
  serve. E as outras **duas portas com campo de número não tinham nem a dica**: a baixa em
  lote e a rodada passaram a criticar **linha a linha ANTES de processar**, cada uma com o
  formato do convênio DAQUELA linha. Não é preciosismo: o serviço recusa guia a guia, então
  uma linha ruim no meio de dez deixava as anteriores baixadas — e, na rodada BLOQUEANTE,
  o sistema travado com a pessoa sem saber qual linha corrigir.
  De quebra, a rodada escrevia `{i.Convenio}` na situação da linha — o enum de novo,
  "UnimedIntercambio · atrasada há 3 dias".
- **A rede que não cobre o app em PRODUÇÃO** (parcela 51): o `compilar-sombra` nasceu com
  nove projetos e o faturamento **de fora**, com o motivo escrito ao lado — "está
  congelado, ninguém o edita". Ele saiu do congelamento na parcela 45, e a exclusão
  sobreviveu ao motivo dela: por seis parcelas, cada linha de C# escrita no app que fatura
  a clínica só era compilada no runner Windows. Ao portar a tela de Acessos para lá, a
  inclusão pegou um `CS1061` **na primeira execução** (o `IDialogoService` do faturamento
  não tinha `PerguntarTexto`).
  O mesmo valia para o `verificar-suite`, que só varre a suíte. Ampliá-lo por inteiro
  inundaria com dívida antiga de `FontSize` numérico — e checagem que grita trinta vezes é
  checagem que alguém desliga —, então o que passou a alcançar os dois foi o grupo que pega
  **erro de runtime** (checagens 25, 26 e 27), via uma segunda lista de árvores. A regra:
  **quando uma rede exclui um projeto, o motivo tem prazo de validade — releia-o.**
- **Acessos no faturamento, e por que a trava é o BIT e não o perfil** (parcela 51): a
  direção pediu o cadastro de usuários e permissões dentro do faturamento, "só para quem é
  gerente". A trava é `Permissao.GerenciarUsuarios`, que **só o perfil Gerente recebe por
  padrão** — amarrar ao PERFIL contradiria o modelo da parcela 45 e tiraria justamente o
  que a direção pediu na 49: poder conceder a uma pessoa específica sem promovê-la.
  A tela é **porte, não compartilhamento**: o faturamento não pode referenciar
  `Clinica.Desktop.Shell` (os dois declaram tipos em `Clinica.Desktop.Controls`), e as
  ViewModels usam `CollectionViewSource`, então também não sobem para `Application`. É o
  débito permanente da Fase 4 cancelada, agora com mais 600 linhas — e o cadastro é ÚNICO,
  porque o que liga os dois apps é o BANCO, como todo o resto da suíte.
  De quebra, a tela original **tinha só uma barreira**: o item da sidebar. Nenhum comando
  chamava `Exigir`, o que contraria a regra do próprio projeto — e nesta tela mais que em
  qualquer outra, porque **quem mexe em permissão pode conceder permissão a si mesmo**. Os
  dois lados ganharam a segunda barreira, e ela **diz por que recusou** em vez de voltar
  calada (a lição da parcela 41).
- **Valor de propriedade que o WPF valida em RUNTIME passa pelas quatro redes** (parcela 50,
  4ª rodada — a tela de Pacientes abriu com "a propriedade
  'DefinitionBase.SharedSizeGroup' iniciou uma exceção", e o cliente mandou a foto).
  `SharedSizeGroup` exige um IDENTIFICADOR — letra ou sublinhado, depois letras, dígitos e
  sublinhado. Escrevi `"PacLinha.Avatar"` porque parece um nome qualificado e todo o resto
  do XAML aceita ponto; o WPF valida no `set` e LANÇA.
  A novidade não é o erro, é ONDE ele passou: **as três redes locais ficaram verdes e o
  compilador de marcação também** — o XAML é bem-formado, a propriedade existe e o tipo é
  string. Até aqui, "só o CI pega" era o pior caso; este é a categoria seguinte, **só a
  tela montada pega**, e o estrago é a tela inteira, não um desalinhamento. A lição
  generaliza: **propriedade cujo valor é validado em runtime precisa de checagem textual**,
  porque não existe compilador que a cubra. Virou a **checagem 27**, autotestada contra o
  nome que quebrou.
- **Estilo de PARÁGRAFO não serve de CÉLULA de tabela** (parcela 50, 3ª rodada — o cliente
  mandou a foto de "6R$ 0,00" e "R$ 0,00R$ 0,00" nos relatórios do Gerente). O `TextoSuave`
  fixa `HorizontalAlignment="Left"` desde a parcela 37, e por um bom motivo — sem ele o
  subtítulo da página nasce flutuando no meio da tela. O efeito colateral é que o TextBlock
  passa a ter a largura do TEXTO, e não a da célula: o `TextAlignment="Right"` escrito ao
  lado **não alinha nada**, porque não sobra espaço dentro do bloco onde alinhar. O número
  desgruda da borda direita da coluna e vai colar no valor da coluna anterior; o
  `Margin="0,4,0,0"` do mesmo estilo ainda o desce 4 px, e a linha sai com as células em
  alturas diferentes.
  Daí o `CelulaSuave`, nos DOIS design systems: mesma cor e mesmo tamanho, sem os três
  ajustes de parágrafo, e com **reticências** (regra de tabela — a coluna tem largura fixa,
  e quebrar em duas linhas desalinharia a altura da linha inteira). A **checagem 26** cobra
  a CONTRADIÇÃO declarada (`TextoSuave` + `TextAlignment` no mesmo elemento), e não
  "TextoSuave em tabela": adivinhar o que é tabela daria falso positivo em legenda
  centralizada sob uma foto — **três** das doze conversões automáticas eram exatamente
  isso, e foram revertidas à mão.
- **O nome do convênio é da OPERADORA; o enum é da REGRA** (parcela 50, 3ª rodada): o
  crachá do paciente escrevia "UnimedIntercambio" e "Personalizado". São dois ângulos do
  mesmo erro — a tela perguntou a FAMÍLIA quando queria a operadora. `{Binding Convenio}`
  faz o WPF chamar `ToString()` no enum (o defeito da parcela 41, que a checagem 20 não
  pega porque só olha `ComboBox`); e `CatalogoConvenios.Nome(p.Convenio)` devolve
  "Personalizado" para **toda** operadora que a clínica cadastrou, porque
  `Convenio.Personalizado` é a regra que todas compartilham — a clínica cadastra "Sul
  América" em Configurações, o nome fica no banco e a tela não o alcança.
  O ponto único é `CatalogoConvenios.Nome(codigo, familia)` (o código vence; a família é o
  caminho de baixo) e, para quem tem a entidade, `Paciente.ConvenioNome`. O par
  código+família teve de ser levado a três records de serviço que só carregavam a família
  — aditivo, `init` com padrão nulo, porque `PendenciaService` é compartilhado com o
  faturamento em produção.
- **Sobreposição posta como IRMÃ desaba a tela inteira** (parcela 50, 2ª rodada — o cliente
  mandou a foto da Conciliação com título, abas e texto desenhados uns por cima dos
  outros). O `EstadoDaTela` (carregando · falhou · vazio) é uma SOBREPOSIÇÃO, e por isso
  precisa de um pai que empilhe os filhos no mesmo lugar — um `Grid` —, sendo o ÚLTIMO
  filho dele (o WPF desenha na ordem do XAML). Ele foi escrito como irmão, dentro de um
  `DockPanel`, com o conteúdo embrulhado num `Grid` intermediário. Daí saem **dois**
  estragos, e o segundo é o que se vê: o `EstadoDaTela` deixa de sobrepor e passa a OCUPAR
  espaço; e todo `DockPanel.Dock` do conteúdo vira **no-op**, porque o pai dele passou a ser
  o `Grid` — então a tela inteira desaba numa célula só.
  A correção é uma troca de papéis entre os dois elementos, não um remendo: `Grid` por fora
  (hospeda a sobreposição), `DockPanel` por dentro (devolve o empilhamento vertical). Eram
  **cinco telas do Financeiro**, todas assim desde a parcela em que o estado vazio foi
  acrescentado. Nenhuma rede pegava: o XML é bem-formado, o `compilar-sombra` não lê o
  corpo do XAML e o compilador de marcação não tem o que reclamar — **o defeito só existe
  na tela montada**. Virou a **checagem 25**, que cobra as duas metades (painel linear
  nunca sobrepõe; dentro de `Grid`, ou é o último filho ou traz `Panel.ZIndex`) e é
  autotestada contra o caso real.
- **Login sem saída faz a auditoria assinar o nome errado** (parcela 50, 2ª rodada): os
  quatro apps da SUÍTE pediam login e não tinham "Trocar usuário" — só o faturamento tinha,
  desde a parcela 45. Parece conforto e não é: é a nona ocorrência do defeito recorrente do
  projeto (capacidade com porta num app só), com o agravante de **desfazer em silêncio a
  razão de o login existir**. No balcão duas pessoas dividem a máquina; sem saída, a segunda
  segue trabalhando com o login da primeira, e a trilha da parcela 21 volta a responder o
  nome errado para "quem fez isso?" — exatamente o que `SessaoUsuario` substituiu quando
  tirou o `Environment.UserName` de lá.
  Trocar de usuário **reabre o app** na suíte pela mesma razão do faturamento: as ViewModels
  leem a permissão quando são CONSTRUÍDAS, metade delas já está viva, e repontar a sessão
  deixaria a tela da colega anterior aberta com os botões dela. Uma diferença: aqui, se a
  reabertura FALHA, o app **não fecha** — derrubar o sistema sem ter conseguido abrir o
  outro deixaria o balcão sem nada.
- **Resposta de banco que chega fora de ordem é "não atualiza"** (parcela 50, dois bugs
  que o cliente achou em produção): a prévia do Novo atendimento mostrava "1 guia · tudo
  hoje" para uma modalidade que gera duas, e não mudava ao trocar qual código o convênio
  libera primeiro. O motor estava certo — quem errava era a TELA.
  Duas causas somadas: (a) toda entrada disparava `_ = PreverAsync()` sem ordenar, e
  trocar de modalidade largava **três** leituras concorrentes no ar (o `Clear()` da combo
  de "qual código primeiro" zera a seleção e devolve `null` pelo binding, depois vem o
  valor novo, depois a da própria modalidade); num banco remoto a mais VELHA podia
  responder por último e sobrescrever. A guarda que existia comparava paciente e
  modalidade, e não pegava duas leituras do MESMO paciente e da MESMA modalidade. Agora é
  **contador de geração** — quem começou primeiro perde —, que é a mesma solução que o
  `SeletorPacienteViewModel` já usava para busca fora de ordem. (b) `MontarCartoes()`
  recria os objetos do zero, e ao trocar de paciente ele corria junto com a prévia: quem
  chegasse por último decidia se a tela tinha número. A última leitura ficou **guardada**
  para a remontagem reaproveitar — e é **jogada fora ao trocar de paciente**, porque
  mostrar o número do paciente anterior é pior do que mostrar "calculando…".
  A lição geral: **toda tela que dispara leitura a cada tecla ou clique precisa de
  descarte de resposta fora de ordem.** Não é otimização; sem ele a tela mente, e mente de
  um jeito que não reproduz na máquina de quem programa (banco local responde em ordem).
- **`TabPanel` ENCOLHE as abas; `WrapPanel` não** (parcela 50): o cliente viu "Convê",
  "Prontu", "Documer", "Relacioname" e "LG" na ficha do paciente, com quase mil pixels de
  largura sobrando. Não era falta de espaço — é o painel padrão do `TabControl` decidindo
  espremer os itens quando julga que a régua não cabe. O `WrapPanel` dá a cada filho a
  largura que ele PEDE e, se um dia não couber, quebra para a linha de baixo: aba na
  segunda linha se lê, "Documer" não.
- **Cartão de altura FIXA precisa de teto no texto** (parcela 50): a mesma altura fixa que
  acerta a base da fileira (parcela 47) corta o conteúdo quando o nome quebra em duas
  linhas — foi o que apareceu no cartão escolhido do Novo atendimento. Altura fixa e
  `MaxHeight` no rótulo andam juntos.
- **Texto que estoura é dado do BANCO sem quebra nem reticências** (parcela 50): o
  cliente reclamou de "muitos textos estourando" no Gerente, e a família é sempre a mesma
  — um `TextBlock` amarrado a `{Binding}` dentro de uma célula de largura fixa. O WPF não
  corta nada por conta própria: o texto sai por cima do vizinho. A regra de decisão é
  curta: **célula de tabela leva `TextTrimming="CharacterEllipsis"`; texto de cartão leva
  `TextWrapping="Wrap"`**. Texto LITERAL o programador mede ao escrever; dado do banco é o
  que tem tamanho imprevisível (nome de 12 ou 60 caracteres, valor de três ou nove
  dígitos), e é por isso que a **checagem 24** só olha o amarrado.
  Dois casos foram resolvidos no ESTILO, que é onde valem para a suíte inteira:
  `TextoSuave` já quebrava desde a parcela 37, e `Rotulo` passou a quebrar agora — ele
  mora acima de campo em coluna de formulário ("Certificado em nuvem (SafeID) — opcional"
  em 290 px) e como cabeçalho de coluna de tabela ("Queda média da dor" em 110 px), e as
  duas estouravam. O **alinhamento não veio junto**: rótulo de tabela às vezes é
  centralizado sobre a coluna, e forçar `Left` desalinharia a régua das tabelas — é a
  meia-regra do `TextoSuave` pelo avesso.
  E a causa RAIZ dos KPIs era o padrão que o `README.md` já proibia: **`UniformGrid` (ou
  coluna estrela) de largura inteira**. Ele divide a largura em partes iguais e obriga o
  valor a caber no que sobrou — num monitor de 1366 px, cinco colunas dão ~250 px, e
  "R$ 1.234.567,89" em 28 px negrito não cabe. Virou `WrapPanel` com piso de largura: cada
  cartão pede o que precisa e a fileira quebra quando não couber.
  A checagem 24 cobre hoje os **SEIS módulos** — a dívida foi zerada de uma vez (338
  ocorrências) e `LIMPOS` os lista todos. O mecanismo de aviso por módulo continua ali
  porque é o caminho de volta: módulo novo entra fora da lista, aparece como aviso com a
  contagem, e passa a ser erro quando alguém o limpar.
  ⚠️ **A lista de estilos que "já resolvem" é LIDA dos dicionários, não escrita à mão.**
  São DOIS design systems (o da suíte e o do faturamento, que não se referenciam — o
  débito permanente da parcela 7), e a lista fixa só conhecia um: os seis `FichaValor` do
  faturamento apareciam como dívida sem serem, porque aquele estilo corta desde sempre.
  ⚠️ **A primeira versão da checagem tinha um ponto cego, e a pergunta do cliente ("você
  verificou TODAS?") foi o que o revelou**: ela só lia a TAG DE ABERTURA, e a suíte monta
  frase com pedaço variável no meio usando `<Run Text="{Binding X}" />` como FILHO —
  "Sai hoje de cada recebimento: R$ 1.234". Nenhum desses aparecia na contagem. Achar o
  ponto cego mudou os números: Financeiro foi de 122 para 130 e o faturamento de 76 para
  81. **Contagem de checagem só vale depois de alguém perguntar o que ela NÃO vê.**
- **O prontuário NÃO SE APAGA, e "alterar" sem guardar o anterior é apagar devagar**
  (parcela 52 — auditoria de fornecedor feita pela própria cliente, com dez pontos por
  escrito; o mapa completo está em `docs/conformidade-lgpd.md`). Dois deles o sistema
  não atendia, e o pior era o que ele fazia ATIVAMENTE: havia `Remove()` de verdade em
  **quatro** caminhos clínicos (evolução — levando os anexos junto —, anexo, avaliação e
  medida), e a evolução era sobrescrita no lugar, com a trilha gravando
  `"EvolucaoAlterada — sessão de 12/03"` enquanto **o texto anterior sumia**. Isso
  contradiz a Lei 13.787/2018 (art. 3º: integridade, autenticidade e rastreabilidade da
  retificação) e inviabiliza a guarda de 20 anos do art. 6º — não há como garantir
  retenção com um botão que destrói o registro.
  O mais revelador é que **a regra já estava escrita neste arquivo** e aplicada em toda
  parte: documento clínico cancela com motivo, NC não some, checagem de enfermagem
  retifica com linha nova, problema descarta com motivo. Ela só não tinha sido aplicada
  no prontuário — o lugar onde mais importa e o único com respaldo legal explícito. A
  lição generaliza: **quando uma regra do projeto vale "em toda parte", procure o lugar
  onde ela é mais óbvia e confira se está lá.** É justamente onde ninguém olha.
  As decisões: (a) **os métodos de exclusão saíram do `IClinicaRepositorio`**, e há teste
  que falha se voltarem — enquanto existirem, alguma tela futura os chama; (b) o
  versionamento é **tabela de versões** (`VersaoEvolucao`), e não o padrão de retificação
  da checagem, porque a evolução é salva várias vezes na MESMA sessão e uma linha nova
  por Salvar faria o prontuário mentir sobre quantas vezes o paciente veio; (c) o motivo
  da correção é **opcional** e o do cancelamento é **obrigatório** — exigir justificativa
  a cada Salvar produziria trinta "ajuste" por dia, que é rastro com aparência de
  controle e nenhum conteúdo, enquanto cancelar sem motivo é apagar com uma etapa a mais;
  (d) o prazo de guarda conta do **ÚLTIMO registro de qualquer natureza**, nunca do
  primeiro, e é `const` e não configuração — é prazo LEGAL, e editável numa tela alguém o
  baixa para 5 anos sem ninguém perceber; (e) **o sistema não elimina nada** ao vencer o
  prazo: o prontuário fica ELEGÍVEL, e a decisão é da clínica com a comissão do art. 7º —
  eliminar sozinho seria ler um PISO de guarda como agendamento de destruição.
- **Controle de acesso responde "quem PODE"; só a trilha responde "quem FEZ"** (parcela
  52): a auditoria da parcela 21 gravava 55 tipos de ação e **todas eram escrita**. A
  cliente pediu três coisas — *"quem acessou, quando acessou e o que realizou"* — e o
  sistema respondia só a terceira: abrir o prontuário de alguém e ler tudo não deixava
  rastro nenhum. E o acesso indevido clássico numa clínica é **LEITURA** (a funcionária
  que abre o prontuário da vizinha, do ex, de um conhecido), que é exatamente o caso que
  a permissão granular da parcela 49 **não** cobre: numa clínica pequena quase todo mundo
  tem permissão legítima sobre quase todo mundo.
  `AcessoProntuarioService` registra quem abriu, quando e **por qual porta** (ficha,
  prontuário clínico, atendimento, documento, exportação) — a porta entra porque "leu o
  telefone no balcão" e "abriu a evolução inteira" são acessos de natureza diferente ao
  mesmo paciente. Três regras: **janela de silêncio de 30 min** (um atendimento entre
  quatro abas não são quatro acessos, e trilha que ninguém consegue ler é trilha que
  ninguém lê), mas **curta de propósito** — quem abre o mesmo prontuário de manhã e à
  tarde fez DOIS acessos, e fundi-los esconderia o padrão que uma investigação procura;
  a comparação de operador dentro da janela é **exata**, porque o filtro casa por trecho
  e "ana" dentro de "mariana" faria o acesso da segunda pessoa desaparecer; e **falhar
  não derruba a tela** (banco lento não pode impedir alguém de ler o prontuário do
  paciente que está na frente), mas também não passa calado — vai para o log, senão a
  clínica acredita estar coberta e não está.
  Na tela, o registro é disparado na **troca de paciente**, nunca em todo `CarregarAsync`:
  as telas recarregam a cada tecla da busca, e a janela de silêncio cobriria a duplicata
  só DEPOIS de ir ao banco perguntar.
- **Ferramenta não é política** (parcela 52): o `BackupService` existia desde a parcela
  34, era bom (base inteira, manifesto conferível, restauração que recusa gravar por
  cima) e era **um botão**. A auditoria pediu *"política de backup, redundância e
  recuperação"* — e a clínica tinha a ferramenta, não a política. Backup que depende de
  alguém lembrar de clicar toda semana existe no manual e não no disco. É primo do
  defeito recorrente do projeto: ali é capacidade sem porta; aqui é **capacidade com
  porta que ninguém atravessa na hora certa**.
  `PoliticaBackupService` põe prazo, destino e **rotação de várias cópias** — guardar só
  a última é o erro clássico, porque a corrupção que ninguém viu na sexta é copiada por
  cima da única cópia boa no sábado. Roda na **abertura do Gerente**, e não num
  agendador: o sistema é desktop, não tem serviço residente, e inventar um daria mais uma
  peça para quebrar em silêncio. É o único lugar desta parcela em que **apagar é certo** —
  cópia velha é redundância, não registro clínico.
  O que o código **não** consegue garantir vai escrito na tela e no documento, não numa
  promessa: nenhum caminho de arquivo diz onde ele fisicamente está, então "grave fora da
  máquina" é orientação à clínica.
- **O QR da receita e a escolha do provedor não podem estar no mesmo caminho crítico**
  (`PublicacaoDocumento`, `ArmazenamentoS3`, parcela 53): a receita assinada é conferida no
  ITI pelo **envio do arquivo**, o que obriga o paciente a mandar o PDF para a farmácia — e
  é onde o balcão trava. Publicado, o farmacêutico escaneia o QR e abre. O motor saiu antes
  do provedor estar escolhido, e a lição é essa: **uma implementação para todo S3-compatível**
  (Magalu, R2, AWS, MinIO), com endpoint em campo de tela. Escolher fornecedor virou
  preencher um campo, não publicar versão.
  O que decidiu usar `AWSSDK.S3` em vez de assinar SigV4 à mão foi o **path-style
  addressing** (`ForcePathStyle`): o padrão do SDK é `bucket.host/objeto` e quase todo
  S3-compatível exige `host/bucket/objeto` — uma linha contra um caso de borda que só
  apareceria no provedor do cliente.
  **DOMÍNIO e ENDPOINT são dois campos porque são duas coisas.** O domínio é o endereço
  público que vai **selado dentro do QR do PDF assinado**; o endpoint é para onde o sistema
  escreve. Trocar de provedor tem de ser mexer no CNAME — se o QR apontasse para o
  endereço do provedor, mudar de fornecedor mataria **toda receita que os pacientes já têm
  na mão**, e elas não podem ser regeradas (a assinatura sela os bytes). É a mesma razão
  pela qual o token é **estável na renovação**.
  A **janela de dias é configurável e o prazo de guarda não**: publicação é política da
  clínica (30 dias para receita simples, 180 para uso contínuo), guarda é prazo LEGAL — daí
  `GuardaProntuario.AnosDeGuarda` continuar `const`. Configuração corrompida cai no padrão,
  porque valor inválido não pode deixar dado de saúde no ar para sempre.
  O **"Testar conexão" grava e apaga de verdade**, em vez de listar o balde: um teste que
  só lê passaria com credencial de leitura, com balde inexistente e — o caso que importa —
  com provedor que recusa a ACL de leitura pública, que é exatamente o que a publicação
  usa. Teste que não exercita o mesmo caminho atesta uma coisa e a receita falha por outra.
  E gravar-sem-apagar tem **frase própria**: a publicação funcionaria e a expiração não.
- **Credencial de serviço externo mora no BANCO, com o ambiente podendo sobrepor**
  (parcela 53 — correção de um comentário meu da 52). Eu havia escrito que as credenciais
  do armazenamento iriam por variável de ambiente, "porque segredo em tabela de
  configuração é segredo que sai no backup". Errado por duas vias, e as duas valem para a
  próxima integração:
  (a) **contradiz o que o projeto já decidiu**, com o motivo escrito no
  `ProvedorOpcoesSafeID` — variável de ambiente é **ritual de instalação**, e uma clínica
  não abre o Prompt de Comando em cada máquina. Aqui seria pior que no SafeID: quem assina
  documento é o Consultório **e** a Recepção, então a publicação funcionaria onde alguém
  digitou e falharia **calada** nas outras;
  (b) **descrevia como seguro um padrão que o sistema não segue** — o `client_secret` do
  SafeID já está gravado em claro nessa mesma tabela. Resolver o segredo-no-backup de
  raspão, numa integração nova, só o esconderia: ele é problema real, separado, e continua
  em aberto.
  A regra que fica: **quando for justificar uma decisão pelo "é mais seguro", confira se o
  resto do sistema faz assim.** Se não faz, ou você está corrigindo o sistema inteiro — e
  então corrija — ou está inventando uma exceção que ninguém vai manter.
- **A suíte chamava o snackbar 143 vezes e nunca teve onde mostrá-lo** (parcela 53 — o
  cliente clicou em "Testar conexão" e o sistema não respondeu nada). O teste tinha
  **funcionado**; o que não existia era a mensagem. O `SnackbarService` é registrado no
  `ShellBootstrap`, injetado em quase toda ViewModel da suíte e chamado 143 vezes — e o
  `ShellWindow.xaml` **nunca renderizou o host**. Ele só existe no `MainWindow` do
  FATURAMENTO, que é de onde o componente veio. Todo "salvo com sucesso" da Recepção, do
  Financeiro, do Gerente e do Consultório caiu no vazio desde que a suíte nasceu.
  É o defeito recorrente do projeto numa variante nova: não é dado gravado sem leitor nem
  capacidade sem porta — é **SAÍDA SEM TELA**. E é o mais discreto de todos porque **nada
  falha**: o serviço marshala pelo Dispatcher, atualiza o próprio estado, e ninguém observa
  esse estado. Build verde, 1374 testes verdes, e a única forma de notar é clicar em Salvar
  e reparar que a tela não respondeu. **Ao portar um componente entre os dois design
  systems, porte o HOST junto do serviço** — metade de um par não avisa que está sozinha.
- **"Consigo escrever?" e "dá para LER pela URL?" são testes diferentes** (parcela 53): o
  `TestarConexaoAsync` grava e apaga — prova credencial, balde e ACL. Ele **não** prova que
  o objeto abre pelo endereço público, e essa é a falha que chega ao balcão: o PUT passa, a
  ACL é aceita, a URL pública do balde está desligada, e o farmacêutico leva 403 com a
  receita na mão. Daí o `EnviarExemploAsync`, que sobe **um** PDF e **não apaga**,
  devolvendo o endereço para abrir no navegador. É PDF de verdade e não `.txt` porque
  metade do que se prova é que o celular **abre** em vez de baixar; usa token real para o
  endereço ter a forma exata do de uma receita; e o conteúdo é fixo, sem paciente — publicar
  documento real para conferir infraestrutura seria expor dado de saúde para testar
  endereço.
  **Validado ao vivo contra o Cloudflare R2** (ago/2026): gravação, ACL de leitura pública,
  exclusão e abertura do PDF pela URL. E a assinatura **também foi provada na clínica com
  e-CPF real pelo SafeID** (ago/2026) — ver a lição da prova de campo mais abaixo.
- **O TERMO LIGADO À SESSÃO — e por que o elo é o HORÁRIO, não a evolução** (set/2026; o
  mapa está em `docs/termo-assinado-pelo-paciente.md` §9). A direção pediu que o termo
  ficasse "linkado à sessão que o paciente tem para realizar", com um pop-out na recepção
  para escolher qual. `DocumentoClinico` já tinha `EvolucaoId` e ele **não serve**: o termo
  é colhido nos DOIS momentos e só um tem evolução — na recepção, que é o caso do pedido, o
  médico ainda não escreveu nada. O `Agendamento` existe desde que a recepcionista marcou; e
  amarrar nele faz o documento aparecer NA sessão sozinho quando ela for escrita, porque
  `Evolucao.AgendamentoId` aponta para a mesma linha.
  ⚠️ **A coluna é PROCEDÊNCIA, não regra de COBERTURA.** Quem responde "já assinou o termo do
  BSV?" continua sendo a exigência + validade, por paciente/modelo/dia. Amarrar a cobertura
  ao horário faria um termo assinado de manhã deixar de valer à tarde — mudança de
  comportamento numa clínica em produção, e decisão da direção, não efeito colateral de uma
  coluna nova. É a asserção que carrega `TermoLigadoAoHorarioTests`. **Ao acrescentar um
  vínculo a um dado que já tem uma regra de validade, diga qual das duas coisas você está
  mexendo — e prenda a outra num teste.**
  ⚠️ **Pergunta que tem uma resposta possível não é pergunta.** O pop-out só abre quando há
  DÚVIDA: com um horário hoje e nenhum outro à frente, a porta amarra sozinha — e a janela
  **DIZ** a que amarrou. Amarrar em silêncio seria uma decisão que ninguém viu ser tomada;
  perguntar o óbvio é o clique a mais que ensina a confirmar sem ler (o incidente dos três
  encaixes em 71 segundos).
  ⚠️ **A lista oferece de HOJE PARA A FRENTE**, e não só hoje: a coleta antecipada é a razão
  de a porta avulsa existir. E "Nenhuma — termo avulso" é opção de primeira classe, senão a
  janela obrigaria a inventar procedência para poder fechar.
  ⚠️ **O termo NÃO virou "anexo", e o pedido dizia que sim.** `AnexoProntuario` exige
  `EvolucaoId` e `ResultadoExame` AFIRMA ser resultado de exame; o termo é
  `DocumentoClinico` — numerado, com código de conferência, imutável —, e é isso que garante
  a segunda via idêntica e o valor jurídico. O que o pedido quer é o EFEITO (ver o termo
  junto da sessão), e ele se entrega fazendo as listas de documento **mostrarem a sessão**.
  **Quando o pedido nomeia uma TABELA, confira se o que ele quer não é o efeito.**
  ⚠️ E o de sempre: a emissão COPIA campo a campo, então o vínculo entrou na lista do
  `EmitirAsync` — fora dela ele seria descartado em silêncio, com a criação funcionando
  (lugar 3). O teste foi verificado removendo a linha: dois reprovam.

- **⛔ ANTES DE ESCREVER QUALQUER XAML, LEIA A REGRA DE LEIAUTE NO `README.md`** (topo do
  arquivo, seção "A REGRA DE LEIAUTE"). Ela é a consolidação de **seis** reprovações do
  cliente, todas pelo mesmo defeito: **tela picada em várias caixas empilhadas**. As três
  perguntas que decidem: (1) *isto é o que a pessoa VÊ nesta tela, ou o que ela FAZ de vez
  em quando?* — o segundo caso é botão/janela, nunca painel aberto; (2) *esta seção existe
  sem um item escolhido?* — se não, é aba, não item de menu; (3) *quantas perguntas esta
  tela responde?* — mais de uma, mais de uma tela. Na dúvida entre outra caixa e outra
  tela, **é outra tela**, e um botão que leva até ela.
- **A 5ª reprovação: faixas empilhadas comem a tela** (parcela 38, 2ª rodada). O "Meu dia"
  saiu com QUATRO faixas antes do quadro — o slab azul de "Chamar próximo" (que gastava
  70 px de largura inteira para escrever *"ninguém aguardando no balcão"*), o alerta de
  vínculo, a linha de resumo e a caixa de pendências com `MaxHeight="180"` cortando um nome
  de paciente ao meio. O quadro do dia começava na metade da tela, e num dia já terminado
  as quatro raias vazias viravam um buraco de mil pixels ao lado da única com conteúdo.
  As correções, todas generalizáveis: **contexto permanente é LINHA de texto, não faixa**
  (faixa permanente vira moldura); **ação é BOTÃO, não painel** — e um botão desabilitado
  já diz "não há ninguém" sem gastar a tela para dizê-lo, além de caber o nome de quem vai
  ser chamado, que a faixa não dizia; **lista longa merece TELA PRÓPRIA** com a largura
  inteira, e no lugar dela fica um botão com a contagem (foi assim que a dívida de
  prontuário virou `ChaveRegistrosPendentes`); **o número mora junto do que ele conta** —
  o resumo repetia as cinco contagens em sequência e obrigava o olho a casar cada uma com
  a sua coluna pela ordem, então elas foram para `CabecalhoRaia`; e **coluna vazia mostra
  um traço**, porque quatro vãos brancos sem marca nenhuma fazem o quadro deixar de se ler
  como quadro. As colunas do kanban deixaram de ser `Card` com borda e viraram **raias** —
  faixa de fundo levíssima, sem moldura, separadas por espaço: cinco cartões emoldurados
  lado a lado leem-se como cinco telas costuradas, que é literalmente a reclamação.
- **Cada retângulo a mais é uma costura a mais** (parcela 37, 3ª rodada): embrulhar todo
  bloco num `Card` com borda produz uma colcha de retalhos — a tela fica com aparência de
  esboço mesmo com o conteúdo certo. Uma superfície por REGIÃO, e a separação interna por
  espaço, rótulo e `Separator`. Três correções que vieram junto e valem para qualquer tela:
  **KPI não se espalha** (cinco números num `UniformGrid` de largura inteira viram cinco
  traços perdidos com meio metro de branco entre eles — em `StackPanel` horizontal à
  esquerda eles voltam a se ler como um conjunto); **gráfico sem dado SOME** em vez de
  desenhar 200 px de área vazia dizendo com o desenho o que a frase acima dele já diz com
  palavras; e **um estado vazio por pergunta** — a tela de Avaliações chegou a ter três
  respostas para "não há escala aplicada" (a frase, a área do gráfico e o `EstadoDaTela`
  da lista).
- **`TextoSuave` alinha à ESQUERDA, e isso anda junto do `MaxWidth`** (parcela 37): sem o
  `HorizontalAlignment`, o TextBlock recebe a fatia inteira do painel, o teto de 820 o
  encolhe e o WPF **centraliza** o que sobrou — num monitor de 1920 o subtítulo de toda
  página da suíte nascia flutuando no meio da tela, com o título alinhado à esquerda logo
  acima. Parecia leiaute quebrado porque era. Teto sem alinhamento é meia regra.
- **O mapa corporal e a emissão de documento moram no SHELL** (parcela 36): os dois
  nasceram dentro do módulo da Recepção, e o Consultório precisa dos mesmos — a acupuntura
  é a especialidade da casa, e quem prescreve é quem atende. Como **nenhum módulo conhece
  os outros**, as alternativas eram copiar (duas silhuetas divergindo na primeira
  correção) ou deixar o app do médico sem a ferramenta central dele. Subiram para
  `Clinica.Desktop.Shell/Componentes`, pelo mesmo argumento que já pôs o
  `SeletorPacienteViewModel` lá: **tela nova da suíte que marca ponto no corpo ou emite
  documento usa ESTES componentes; não reescreva.** O bloco foi movido INTEIRO, e não
  reescrito, para nenhuma função se perder na mudança — e, de quebra,
  `MapaCorporalViewModel.Observacoes`, gravado sempre nulo desde a parcela 3 porque
  nenhuma tela o mostrava, finalmente ganhou a caixa de texto.
- **Escalas clínicas por especialidade** (`Domain/Avaliacoes/`, parcela 36): a EVA responde
  "quanto dói" e serve à acupuntura e à clínica da dor. As outras quatro especialidades da
  casa — psiquiatria, geriatria, neurocirurgia e endocrinologia — **não tinham número
  nenhum**: escreviam "refere melhora do humor" na evolução, o que é verdade e não compara
  consulta com consulta, não desenha curva e não vai para o relatório. Cinco instrumentos
  (PHQ-9, GAD-7, Oswestry, Katz, FINDRISC) moram em CÓDIGO e não em tabela, pelo mesmo
  desenho do motor de regras de convênio: a pontuação de uma escala publicada não é
  configuração da clínica — deixar editar o peso de um item produziria um escore que
  continua se chamando PHQ-9 sem ser um. As regras:
  **tudo o que descreve o instrumento é COPIADO na aplicação** (nome, enunciado, rótulo da
  alternativa, faixa, interpretação), como o protocolo do mapa corporal e o preço por
  convênio — é o que permite corrigir uma redação sem reescrever o que o paciente respondeu
  no mês passado, e o que mantém legível a avaliação de uma escala já retirada do catálogo.
  **Escala incompleta é recusada** (item em branco vira zero numa soma, e zero é "não tenho
  esse sintoma"), salvo no Oswestry, cuja regra publicada manda calcular o percentual sobre
  as seções respondidas. **Peso fora das alternativas é recusado.** O **alerta de item não
  se dissolve no total** (`IInstrumentoAvaliacao.AlertaDeItem`): o item 9 do PHQ-9 vale 3
  dos 27 pontos, e um paciente pode marcá-lo zerando o resto — escore 3, faixa "sintomas
  mínimos", e a única resposta que exigia conduta imediata desapareceria dentro da média.
  O **Katz inverte** (`MelhorQuandoMenor = false`): 6 é o melhor resultado possível, e sem
  isso a curva chamaria a recuperação funcional de piora. **O sistema pontua e registra; ele
  não diagnostica** — a faixa é a leitura publicada da escala, e a tela diz isso o tempo
  todo. **Neurocirurgia NÃO entrou no enum `Especialidade`** — ela é um código do
  CATÁLOGO (`InstrumentoOswestry.CodigoNeurocirurgia`), e a diferença não é estilo: o
  enum é lido pelo faturamento CONGELADO, que na abertura chama
  `EspecialidadeCatalogoService.RecarregarCacheAsync` e garante as embutidas percorrendo
  `Enum.GetValues`. Um valor a mais ali viraria, sozinho, uma opção nova no seletor do
  lançamento de atendimento de um app em produção. A clínica cadastra a especialidade em
  Configurações quando quiser; até lá o Oswestry serve à acupuntura e à clínica da dor.
- **Como os módulos se falam** (parcela 27): eles compartilham o BANCO, não
  mensagens — não há fila, evento nem sincronização; o que um grava o outro lê, e a
  ligação é sempre uma CHAVE ESTRANGEIRA. O circuito completo:
  **Recepção → Faturamento**: `FechamentoSessaoService` → `AtendimentoService.LancarAsync`
  cria `Atendimento` + `CodigoFaturamento` pelas regras do convênio; o app congelado lê a
  mesma base. Volta pelo `PainelRecepcaoService` (guias pendentes só dos pacientes de
  HOJE) e pela NC que reabre quando o paciente volta.
  **Faturamento → Financeiro**: `FinanceiroService.GuiasSemLancamentoAsync` (guia baixada
  sem receita) alimenta a Conciliação; `LancarReceitaDaGuiaAsync` grava
  `LancamentoFinanceiro.CodigoFaturamentoId`, e é esse campo que fecha o elo — a guia sai
  da lista porque passou a ter receita, não porque alguém a marcou.
  **Financeiro → Gerente**: `RentabilidadeConvenioService`, `CustoTransacaoService` e o
  `PainelDirecaoService`, que não calcula nada — cada número vem do serviço dono dele.
  **Gerente → todos**: cada alerta do painel LEVA à tela dona, por `NavegacaoSuite` +
  `ChavesSuite`. A **dependência tem um sentido só**: o faturamento continua funcionando
  sem saber que o financeiro existe, e é por isso que as pontes (`FechamentoSessaoService`,
  `ReceitaGlosadaService`) moram FORA dos serviços compartilhados — dar efeito colateral
  novo ao `AtendimentoService` mudaria o comportamento de um app em produção.
- **A glosa que voltava para ninguém** (`ReceitaGlosadaService`, parcela 27): o elo que
  faltava era o de VOLTA. `GlosaService` mexia só no `CodigoFaturamento` e a palavra
  "Glosa" não aparecia em um único arquivo do módulo Financeiro: o convênio recusava a
  guia e o dinheiro **continuava no fluxo de caixa, no previsto e na rentabilidade**.
  **Receita fantasma é a pior espécie de número errado, porque tem cara de número exato.**
  As regras: a glosa **não apaga o lançamento** (fato datado — sai do total, não do
  histórico); **só o PREVISTO se cancela**, porque o REALIZADO é dinheiro que entrou na
  conta e cancelá-lo faria o caixa parar de bater com o extrato (estorno da operadora é
  uma SAÍDA, com a data dele); e a guia **volta sozinha para a conciliação** — cancelado o
  vínculo ela deixa de ter receita ativa e reaparece em `GuiasSemLancamentoAsync`, que é o
  caminho de volta funcionando sem uma linha de código para isso. Na conciliação a guia
  glosada **aparece marcada, nunca sumindo** (mesma regra da central de documentos), e
  lançar receita dela pede confirmação em vez de ser proibido: a clínica pode estar certa
  de que recupera no recurso — ela só precisava saber.
- **O que os outros módulos mandam ao balcão** (parcela 27): `ElegibilidadeService` alargou
  o contrato de propósito, e o critério é um só — **entra ali o que se resolve com o
  paciente presente e fica caro depois**. Por isso chegaram a **conta vencida**
  (Financeiro, via `InadimplenciaService`) e a **guia glosada em aberto** (Faturamento):
  as duas estavam gravadas havia parcelas e só eram lidas por quem não atende ninguém. O
  atraso só alarma a partir de `AtrasoMinimoParaAvisarDias` (5) porque a conta de ontem
  costuma estar em trânsito, e **alerta que dispara para todo mundo é alerta que ninguém
  lê**; a dívida é sempre **amarela** — vermelho neste serviço significa "a guia vai ser
  recusada", e dívida é assunto de conversa, não impedimento de atender.
- **A consulta a renovar chega ao horário** (`ConsultaService.SituacaoDeAsync` /
  `DoPacienteAsync`): a consulta renovável era lida em exatamente DOIS lugares — a aba
  Consultas e o painel de pendências —, e nenhum deles é onde a secretária está quando
  marca ou recebe o paciente. Oitava ocorrência do defeito recorrente do projeto, na
  variante "leitor existe, mas não onde a decisão acontece": renovar com a pessoa presente
  é uma assinatura, e descobrir a consulta vencida na hora de faturar é ligar para quem já
  foi embora com a guia recusada na mão. Agora aparece na **agenda** (selo no cartão +
  linha de contexto no cabeçalho), no **novo agendamento**, no **novo atendimento** e —
  pelo `ElegibilidadeService` — na Fila, na ficha e no Consultório da suíte.
  As regras: a agenda usa **`ARenovar`, não `PrecisaRenovar`** — quem NUNCA emitiu
  consulta fica de fora, porque o selo acenderia para toda a base de convênio no primeiro
  dia e alerta que dispara para todo mundo é alerta que ninguém lê (é a mesma escolha que
  `PendenciaService.ConsultasAVencerAsync` já fazia, e a aba Consultas continua mostrando
  a linha, que ali é o assunto da tela); a frase e o selo moram no **modelo**
  (`StatusConsultaPaciente.AvisoRenovacao`/`SeloRenovacao`), para o faturamento e a suíte
  não escreverem duas versões da mesma cobrança; a regra de "precisa renovar" tem **um só
  `Avaliar`**, senão a agenda passaria a discordar da aba Consultas sobre o mesmo paciente;
  a referência é a **data marcada, não hoje** (marcar para daqui a três semanas com uma
  consulta que vence em cinco dias é combinar a renovação de antemão); a leitura é **em
  lote pelos pacientes da grade** (`ConsultasDosPacientesAsync`), porque varrer a base
  inteira para responder sobre as vinte pessoas de hoje é caro num banco remoto e a agenda
  recarrega a cada navegação de dia; e **cancelado e falta não recebem selo** — não há
  ninguém para assinar nada. Falha da conferência vira **terceiro estado escrito** ("não
  foi possível conferir"), nunca agenda limpa.
- **A Recepção no balcão** (parcela 26): seis buracos do módulo que fatura o dia.
  **Elegibilidade ANTES** (`ElegibilidadeService` no check-in da Fila e no agendamento):
  carteirinha vencida e cota estourada só apareciam na hora de faturar, quando a sessão
  já aconteceu — é aviso, nunca impedimento, mas marcar dez sessões para quem está com a
  cota estourada é combinar dez glosas de antemão. A **rodada de confirmação** ganhou porta
  na Recepção (`ConfirmacoesWindow`): quem liga para o paciente é o balcão, e a campanha
  morava só no Gerente. **Bloqueio de agenda** (`BloqueioAgendaService`): férias, feriado e
  folga entram como fato que a agenda respeita — bloquear **não desmarca ninguém** (devolve
  quem já estava marcado, para a recepção remarcar; sessão que some sem avisar o paciente
  é pior que o choque), o **encaixe continua furando**, e bloqueio sem profissional e sem
  sala é da clínica inteira. **Agendamento em série** (`AgendarSerieAsync`): o pacote de
  dez marcado de uma vez, com a data saindo sempre da **primeira mais N períodos** — nunca
  da anterior mais um, senão uma sessão adiada empurraria todas as seguintes e o paciente
  perderia o horário fixo, que é o motivo de marcar em série; data com choque é **PULADA e
  dita**, e a janela fica aberta para a recepção resolver com o paciente ainda na frente
  dela. **Visão de semana** na agenda (o dia continua sendo o padrão): a semana começa na
  segunda e é para responder "quando cabe", que o dia não responde. E os **direitos do
  titular** (`TitularDadosService`): ver o item de LGPD abaixo.
- **LGPD além do consentimento** (`TitularDadosService`, parcela 26): colher e revogar
  consentimento a clínica sabia desde a parcela 2; faltavam os outros dois pedidos que o
  paciente pode fazer e que ela é **obrigada** a atender — acesso (art. 18, II) e eliminação
  (art. 18, VI). A regra que o produto não pode fingir que não existe: **prontuário não se
  apaga**. A guarda é obrigação legal do profissional de saúde (CFM 1.821/2007) e a própria
  LGPD a preserva (art. 16, II) — então o que se faz é **anonimizar**: nome, documento,
  telefone, carteirinha, nascimento e foto saem, o histórico fica sem dono identificável, e
  a tela **diz isso antes de fazer** (prometer apagar tudo e manter o prontuário seria mentir
  para o paciente por escrito). A exportação sai em **texto, não em PDF diagramado**: o
  direito é de receber dados legíveis e reutilizáveis, mesma razão do CSV dos relatórios. O
  **nome original fica na auditoria** — é o que liga o pedido ao registro, e a trilha é o
  único lugar onde esse vínculo pode existir sem expor o dado na operação do dia a dia.
  `Permissao.AnonimizarDados` nasce **separada de `EditarProntuario`**: evolução escrita se
  corrige, anonimização não tem volta — o balcão exporta (`VerProntuario`), a direção elimina.
- **Recibo e orçamento** (`DocumentoFinanceiroService`, parcela 4): mesmas regras do
  documento clínico — numerados por ano e por tipo (`REC 2026/0001`), não se apagam,
  cancelam-se com motivo, e os **valores ficam gravados na emissão**. O recibo aponta
  para o `LancamentoFinanceiro` que comprova; ele não substitui o caixa.
- **Foto do paciente**: capturada pela webcam da recepção (`Desktop/Servicos/CameraServico.cs`,
  DirectShow via AForge; `Retrato.cs` recorta em quadrado e gera os dois JPEGs). O armazenamento é
  deliberadamente partido em dois: `Paciente.FotoMiniatura` (~160px, na própria linha, alimenta os
  avatares da lista) e a tabela `PacientesFotos` (~640px, carregada só sob demanda) — assim a busca
  de pacientes não arrasta imagem. A gravação acontece no Salvar do cadastro, nunca na captura.
- Ações que alteram faturamento (baixa, estorno, glosa, lote) devem gravar um `EventoAuditoria`
  via `IClinicaRepositorio.RegistrarAuditoriaAsync` no MESMO SaveChanges da ação (atômico).
  O mesmo vale para ação administrativa de acesso (criar usuário, trocar senha, mudar permissão):
  permissão que muda sem rastro é pior do que não ter permissão.
- **Acesso (parcela 5)**: `UsuarioSistema` aponta para o `Profissional` em vez de duplicá-lo; a
  senha é PBKDF2 com sal por usuário (`Clinica.Domain.HashSenha`) e nunca é gravada em claro. A
  permissão efetiva é resolvida na LEITURA — `padrão do perfil + extras − negadas` —, então
  corrigir o padrão de um perfil alcança quem já está cadastrado; negada vence extra. Base sem
  usuário abre o **primeiro acesso** (nasce Gerente) em vez de trancar todo mundo do lado de fora,
  e o serviço recusa deixar a base sem ninguém que possa gerenciar acessos. **Os cinco apps pedem
  login** — o faturamento entrou na parcela 45 (ver a regra do formato da guia e das permissões
  granulares mais abaixo).
- **A permissão tem DUAS barreiras, e as duas são obrigatórias**: `IsEnabled` no botão (a metade
  visível, que explica) e `SessaoUsuario.Atual.Exigir(...)` no comando (a que impede). Só
  desabilitar é enfeite — atalho de teclado passa direto. Comando novo que grava nasce com as
  duas. `SessaoUsuario.Atual` é estática porque metade dos formulários é construída à mão pela
  tela e não passa pelo DI; **sem sessão autenticada, `Pode` libera** (tela vazia parece defeito,
  e no app real o login é obrigatório).
- **Quem assina a ação é quem fez login**, nunca o usuário do Windows: `SessaoUsuario.Atual.Operador`
  é o que vai para a auditoria e para o `EnviadoPor` da campanha. `Environment.UserName` só
  aparece como fallback dentro do próprio `Operador` — no balcão duas pessoas dividem a mesma
  máquina, e gravar o login do Windows apagaria a diferença entre elas.
- **Campanhas (parcela 5)**: confirmação de sessão, NPS e recall são UMA entidade
  (`ContatoCampanha`), porque o fato registrado é o mesmo. `Origem` (`AGD:123`, `ATD:987`,
  `REC:55:2026-07`) é a chave de idempotência, com índice único junto do tipo — rodar a campanha
  duas vezes não pode mandar a mesma mensagem duas vezes. **Confirmar a própria sessão é
  transacional** e não exige consentimento; **NPS e recall exigem** `ComunicacaoEMarketing`
  vigente, e quem não consentiu aparece CONTADO no resultado da rodada em vez de sumir. O envio é
  um clique por paciente de propósito: o número é o WhatsApp da clínica.
- **Indicadores (parcela 5)**: ocupação é medida contra os dias em que o profissional TEVE agenda
  × jornada configurável (`ParametrosService.ChaveJornadaDiariaMinutos`, padrão 480 min) — a
  clínica não cadastra jornada, e inventar dias úteis daria número mais bonito e menos verdadeiro.
  Cancelamento avisado não conta como falta. Toda métrica sem base de cálculo devolve `null` e a
  tela mostra "—": 0% e "não medido" são coisas diferentes.
- Concorrência otimista via `xmin` (só no Npgsql — testes rodam em SQLite e ficam de fora);
  `ClinicaRepositorio.SalvarAsync` traduz `DbUpdateConcurrencyException` em mensagem amigável.
- XML TISS gerado passa por `TissValidador.Validar` (estrutura + hash do epílogo); XSD oficial
  é opcional (pasta `%APPDATA%\ClinicaFaturamento\tiss\schemas`).
- `PrevencaoGlosaService` (radar de glosas) roda na exportação do lote: carteirinha vencida,
  duplicidade e taxa histórica por padrão (convênio+tipo). `TissRetornoImport.Ler` importa o
  demonstrativo XML da operadora e pré-preenche as decisões do retorno (casadas pelo nº real
  da guia); a leitura é tolerante ao nome local dos elementos (varia entre operadoras).
- **O lote TISS é POR OPERADORA, e a guarda de duplicidade era o que tornava o erro
  irreversível** (parcela 60 — revisão completa): o "lote do período" engolia as guias de
  TODAS as operadoras num XML endereçado a UMA (o registro ANS era um campo global), e as
  demais **nunca mais entravam em lote** — `LoteTissId` preenchido as escondia das
  candidatas para sempre, e a tela dizia "nenhuma guia nova", que se lê como dia fraco.
  Agora a exportação agrupa por operadora e gera **um lote e um XML por grupo** (uma
  operadora só = o fluxo de sempre, sem cerimônia); o registro ANS mora no
  `ConvenioCadastro` (dado, não código — em branco cai no global); `LoteTiss.ConvenioCodigo`
  grava de quem o lote é; e o **estorno da baixa solta o `LoteTissId`** — guia rebaixada
  com o número certo volta às candidatas, com o lote antigo registrado na observação. O
  agrupamento é pelo `ConvenioDaGuia` (código do catálogo, família como caminho de baixo),
  o MESMO do nome de exibição — outro critério fundiria operadoras que só compartilham a
  regra.
- **A cópia que ficou para trás é onde a permissão vaza** (parcela 60): a revisão achou
  cinco brechas críticas de permissão, TODAS no app de faturamento — sempre o mesmo
  formato: o módulo equivalente da suíte tinha as duas barreiras e a cópia do
  `Clinica.Desktop` não (estorno pela ficha, agenda, cadastro de paciente, autorizações,
  usuário). Os bits envolvidos eram exatamente os que as parcelas 49/58 tiraram dos perfis
  — cada tela esquecida era um caminho de volta. **Ao aplicar permissão nova, procure a
  MESMA ação nos dois lados** (suíte e faturamento); e a rodada fixou também: emitir na
  janela de documento confere o bit do tipo QUE ESTÁ NO COMBO (`AcessoParaEmitir`), não o
  da porta por onde se entrou — trocar o seletor para "Receita" exigia `Prescrever` só no
  papel.
- **Excluir paciente era a última porta que apagava prontuário** (parcela 60): as FKs
  clínicas apagam em CASCATA com a linha do paciente, e o botão Excluir do faturamento
  levava evolução, avaliação, medida, documento e prescrição juntos — com o
  `ConformidadeProntuarioTests` verde, porque ele só olhava os `Remover*Async` da
  interface. `PacienteService.RemoverAsync` agora RECUSA quando há registro clínico
  (explica a guarda de 20 anos e aponta a anonimização); ficha vazia continua removível. A
  lição de teste: **fixar a regra pela lista de métodos proibidos não cobre o método que
  apaga por arrasto** — o teste novo exercita a cascata de verdade.
- **O descarte de resposta fora de ordem virou padrão de TODA carga async** (parcela 60):
  a regra da parcela 50 valia em DUAS telas; a revisão achou mais de quarenta com o mesmo
  defeito, seis delas críticas (deduções GRAVADAS do valor antigo; CPF gravado no
  profissional errado; prontuário de um paciente sob o nome de outro). O padrão é um só —
  contador `_geracaoCarga`, guarda após cada await, catch e `Carregando` condicionais — e
  agora está em todos os ViewModels que leem banco por tecla, clique ou timer. Tela nova
  com carga async NASCE com o contador; guarda de reentrância (`if (Carregando) return`)
  no método de carga é incompatível com ele (descarta a carga NOVA) e foi removida onde
  conflitava.
- **A sala executa o que não alcançava imprimir** (parcela 61 — auditoria de prontidão do
  módulo clínico antes de produção; achou onze lacunas, todas do defeito recorrente). Todo
  o desenho da checagem da parcela 42 assume a via IMPRESSA (a enfermeira assina à caneta),
  e a única porta de impressão morava na tela de quem PRESCREVE, num exe que a máquina da
  enfermagem não instala. A impressão entrou na sala e na folha de execução; a folha também
  se acha pelo **código impresso** (`PorCodigoAsync`, que estava sem chamador) — a técnica
  está com o papel na mão, e a lista só mostra hoje. **Suspender item** ganhou porta na
  folha de execução sob o bit `Prescrever` (é ato de quem prescreve, não de quem executa —
  o botão fica fora do painel de `PodeMexer` de propósito). O contador de folhas aguardando
  (`PendentesDoDiaAsync`, também sem chamador) virou alerta do painel da direção
  (`AssuntoDirecao.InfusaoAguardando`), pelo argumento do prontuário em aberto: a sala vê a
  própria fila, a direção é quem vê a soma. E o `DoDiaAsync` duplicado do
  `PrescricaoInternaService` foi REMOVIDO: duas definições de "a fila da sala" divergem na
  primeira correção, e a cópia sem chamador é a que ninguém lembraria de ajustar.
- **Mover a fila é UM ato com UMA regra nos dois quadros** (`Permissao.MovimentarFila`,
  parcela 61): a Recepção exigia `EditarAgenda` para carimbar chegada/chamada/entrada, e o
  quadro do médico aceitava `VerAgenda` — a MESMA escrita na MESMA tabela com duas regras,
  e a de baixo deixava quem só lê a agenda carimbar chamada. O bit novo é o corte estreito
  de quem atende (o perfil `Profissional` o recebe por padrão, SEM ganhar `EditarAgenda` —
  marcar horário de terceiros continua sendo do balcão), e a autorização do ato é
  `EditarAgenda` OU `MovimentarFila` nos dois lados, via `SessaoUsuario.PodeAlgum` /
  `ExigirAlgum` (novos — `Pode` com bits combinados é um E, não um OU): quem movia a fila
  ontem pelo balcão continua movendo hoje. O Meu dia ganhou as transições que faltavam
  (Entrou / Voltar — a coluna EM ATENDIMENTO só enchia se o BALCÃO clicasse) e o arrasto da
  parcela 58; **Finalizar continua não existindo lá, e é decisão**: concluir são quatro
  fatos do mesmo ato (guia, pacote, insumo, caixa) e três são do balcão.
- **`BooleanToVisibilityConverter` sobre STRING é `Collapsed` para sempre** (parcela 61 —
  a mensagem da folha de execução, a justificativa da rodela e o nome do executante nunca
  apareceram desde a parcela 42). O conversor do WPF devolve `Collapsed` para qualquer
  valor que não seja `bool`: não é erro de compilação nem de binding — o elemento só nunca
  aparece, e as três redes ficam verdes. O faturamento tem `TextoParaVisibilidadeConverter`
  desde sempre; o shell não tinha e as telas ligavam `{Binding Mensagem}` no conversor de
  bool. Entrou `Clinica.Desktop.Controls.TextoParaVisibilidade` no shell. **Ao ligar
  `Visibility` a uma propriedade, confira o TIPO dela** — string pede o conversor de texto.
- **A trilha de leitura cobre a PORTA que abre o dado, não só a tela clássica** (parcela
  60): Prescrições, Prescrição de infusão, Anexos e a Folha de execução expunham dado de
  saúde sem registrar acesso — e `OrigemAcessoProntuario.Documento` existia no enum sem um
  único escritor. Todas registram agora (na TROCA de paciente ou uma vez por janela, nunca
  a cada `CarregarAsync`), o export CSV ganhou `Exigir(VerProntuario)` + origem própria
  (`ExportacaoClinica` — dado de saúde saindo para arquivo é o que uma investigação
  procura), e `AcessoProntuarioService.DoPacienteAsync` — a resposta a "quem abriu este
  prontuário", sem chamador desde a parcela 52 — ganhou porta na tela de Guarda do Gerente.
  As abas do workspace continuam cobertas pela janela de silêncio por ORIGEM: quatro abas
  do mesmo paciente no mesmo atendimento são UM acesso, e isso é desenho, não buraco.
- **O painel de pendências diz DE QUÊ elas são** (parcela 61 — a direção perguntou:
  *"12 pendências, mas 12 pendências de quê? 1º código? 2º? Acupuntura? Consulta?"*).
  A resposta tem duas formas, e a escolha entre elas não é estilo: **CHIP com contagem**
  para as perguntas de DUAS respostas que o faturista alterna o dia inteiro (1º/2º
  código, atrasada/no prazo — estilo `ChipFiltro`, pílula com a distribuição visível
  ANTES do clique, contada sempre sobre o TOTAL e nunca sobre o recorte); **combo
  rotulado** para as listas longas (tipo do código, modalidade, especialidade,
  convênio). Cada dupla de chips é exclusiva (marcar um desmarca o irmão; desmarcar os
  dois é "todas"). Para isso `PendenciaCodigo` ganhou `Modalidade` e `Especialidade` —
  aditivo, `init` nulo, o padrão de sempre para record compartilhado com produção — e a
  especialidade cai da do CÓDIGO para a do atendimento, o mesmo caminho de baixo da
  consulta de guias (parcela 45). O resumo escreve o recorte por extenso ("12 de 30 —
  2º código · acupuntura"), o Limpar só aparece quando há o que limpar, e o vazio
  distingue "não há pendência" de "nenhuma bate com o filtro" — um filtro esquecido
  respondendo "tudo em dia" faria a clínica dar o dia por resolvido com o painel inteiro
  pendente.
- **O circuito clínico é testado de ponta a ponta** (`CircuitoClinicoTests`, parcela 61):
  o `CircuitoCompletoTests` cobria agenda → faturamento → financeiro e nenhum elo clínico.
  Os quatro circuitos fixados: prescrição assinada → sala → checagem → encerramento (a
  folha SOME da fila e fica na conferência), sala → painel da direção (com a asserção de
  que o bloco não caiu em `NaoVerificados` — elo partido aqui vira ZERO com cara de dia
  tranquilo), reação na sala → alergia → PRÓXIMA prescrição recusada na assinatura, e
  acesso registrado → "quem abriu este prontuário". A chave da sala de infusão — a única
  publicada por DOIS módulos — saiu das duas strings à mão e virou `ChavesSuite.SalaInfusao`.

- **Nenhum defeito da Recepção QUEBRAVA nada — e é por isso que eles chegaram até aqui**
  (parcela 62, auditoria de prontidão do balcão antes de produção). Dez grupos, e o que os
  une é a ausência de sintoma: build verde, 1441 testes verdes, três redes locais verdes, e
  quem descobre é a recepcionista com o paciente na frente. São as variantes do defeito
  recorrente do projeto que nenhuma ferramenta alcança.
  **A porta que não abre, a venda que o dono da tela não pode fazer.** A Recepção publicava
  o item "Pacotes" e o `CriarTela` dela **não tinha o case** — o menu acendia e nada
  acontecia. Nenhuma rede viu porque a chave era uma string à mão dos dois lados; virou
  `ChavesSuite.Pacotes`, como a sala de infusão na 61. E a tela, que subiu para o shell
  vinda do Financeiro, perguntava por `EditarFinanceiro` em toda ação — bit que o perfil
  Recepção **não tem**. O balcão ganhou o item e não podia vender nada por ele.
  ⚠️ A regra que sai daí vale para toda tela que sobe para o shell: **ao mover uma tela
  entre módulos, releia as permissões que ela exige.** Elas foram escolhidas para o dono
  ANTIGO. `Pode(A | B)` é um **E**; a pergunta "o bit do balcão ou o do Financeiro" é
  `PodeAlgum` (parcela 61), e foi o mesmo conserto no passo do CAIXA do Finalizar da sessão
  — o balcão via o campo de dinheiro apagado justamente no ato de registrar o que o
  paciente acabou de pagar.
  **O aviso que morria entre duas camadas.** `FechamentoSessaoService.ConcluirAsync`
  recebia os avisos de `AtendimentoService.LancarAsync` e **abria uma lista nova vazia**.
  Pelo Finalizar da Fila — o caminho que a clínica usa todo dia — a NC reaberta ("o
  paciente voltou, cobre a guia AGORA") e o anúncio do 2º código, que é o assunto do
  produto, nunca chegaram ao balcão. `RecadosDoLancamento` fica **separado** de `Avisos`:
  aquele é o que FALHOU e segura a janela aberta, este é o que ACONTECEU — somá-los faria
  um fechamento perfeito com uma NC reaberta aparecer como fechamento com erro, e três dias
  depois ninguém mais lê a janela.
  **Mensagem de sucesso invisível em cinco telas.** O par `<Border AlertaPerigo
  Visibility="{Binding MensagemEhErro}">` mostra a caixa quando o booleano é verdadeiro, e
  a mensagem de ÊXITO (que zera `MensagemEhErro`) some junto com a de erro. Cinco janelas
  gravavam certo e não diziam nada. Agora a visibilidade segue a `Mensagem` (via
  `TextoParaVisibilidade`) e a COR segue `MensagemEhErro` num `DataTrigger`: **quem decide
  se aparece é o texto; quem decide a cor é a gravidade.**
  **Releitura de fundo na Agenda e no Painel.** Só a Fila tinha relógio. A agenda é a
  ÚNICA tela da suíte em que duas pessoas escrevem no mesmo dado ao mesmo tempo, e o vão
  vago é clicável desde a parcela 58: um horário marcado na outra máquina continuava
  aparecendo livre, e a recepcionista marcaria por cima. Três recusas na batida — só HOJE,
  só no modo DIA (a semana refaz sete colunas com um await no meio de cada, e piscaria) e
  nunca por cima de uma carga no ar. O Painel bate a cada DOIS minutos, não um: são
  contagens do dia, e cada batida custa três consultas ao banco remoto.
  **O descarte de resposta fora de ordem tem uma metade que a parcela 60 não escreveu.** O
  contador de geração impede a leitura VELHA de sobrescrever a nova; ele não impede duas
  leituras de se INTERCALAREM dentro da mesma coleção. `Equipe` e `Lançados hoje` faziam
  `Clear()` e depois `await` num laço — a segunda carga limpava o que a primeira ainda
  estava preenchendo, e a lista saía com linhas repetidas ou faltando. A regra completa é:
  **entre o `Clear()` e o último `Add` não pode haver `await`** — monte em lista local e só
  então publique.
  **E campo não é tela, nem na planilha.** A feature 02 marcava ✅ em "visão por
  profissional ou por sala" porque `Agendamento.SalaId` existe. A sala é gravada,
  respeitada no choque com a capacidade dela e bloqueável por período; **não há modo de
  grade que a use como coluna**. Voltou a 🟡 em `docs/features-por-modulo.md`, com o motivo
  escrito. É o defeito recorrente do projeto na versão mais barata de cometer e a mais cara
  de descobrir: em produção, quem o encontra é o cliente lendo a própria proposta.

- **A varredura de "capacidade sem porta" achou o contrário: código morto que DUPLICA a
  tela** (parcela 63). Varrer os 50+ serviços por método sem chamador em produção
  devolveu ~20 nomes, e quase nenhum era feature faltando — eram **segundas definições**
  do que a tela já faz por outro caminho (`PerderAsync` ao lado do movimento genérico de
  perda, `AtrasadosAsync` ao lado do atraso que a tela de Recebíveis já calcula,
  `ConferirConformidadeAsync` ao lado do `ConformidadeDocumentoClinico.Conferir` estático,
  os quatro do `MapaCorporalService` ao lado do que o ViewModel faz em memória). Oito com
  ZERO referência em `src` e em `tests` foram removidos. **Duas definições da mesma regra
  divergem na primeira correção, e a que ninguém lembra de ajustar é sempre a segunda.**
  ⚠️ E a varredura tem um resultado que só aparece depois de conferir caso a caso: dos
  seis "métodos órfãos" que eu tinha listado como buraco de feature, **um era falso** — a
  tela de Recebíveis já marca e conta os depósitos atrasados. Método sem chamador é
  SINTOMA, não diagnóstico: antes de construir a porta, procure se a tela não a tem por
  outro caminho.

- **O que a auditoria de features achou, e o padrão que se repete** (parcela 63): quatro
  capacidades prontas sem porta, duas features que nunca existiram, e uma linha de placar
  que estava errada nos DOIS sentidos.
  **A agenda ganhou as duas metades que faltavam da feature 02.** A visão por SALA nunca
  existiu — a sala era gravada, respeitada no choque com a capacidade dela e bloqueável
  por período, e nenhum modo de grade a usava como coluna. E o **vão fechado** era
  visualmente idêntico ao vão livre, que é clicável desde a parcela 58: a recepcionista
  escolhia o paciente, preenchia o formulário e levava a recusa do `AgendaService` no
  Salvar, com ele na frente dela. Os bloqueios são lidos por PERÍODO e cruzados em
  memória — havia um `BloqueioDoHorarioAsync` que respondia por um vão só e nunca teve
  chamador, e usá-lo daria ~150 idas ao banco para desenhar uma tela. "Sem sala" é coluna
  de primeira classe: metade dos horários não informa sala, e escondê-los faria a visão
  por sala mostrar um dia cheio como se estivesse vazio.
  ⚠️ **O bug mais grave da parcela não estava na lista: CANCELAR uma receita não a tirava
  do ar.** A documentação do `PublicacaoDocumentoService` afirmava, desde a parcela 53,
  que o cancelamento despublicava; a única chamada de `DespublicarAsync` era a da
  EXPIRAÇÃO. O papel dizia "CANCELADA" e o endereço público continuava entregando o PDF
  assinado por até 180 dias. A correção mora no SERVIÇO porque o cancelamento tem
  **quatro portas** (ficha do paciente, Prescrições e dois caminhos da central) — a mesma
  razão pela qual a crítica do número da guia mora no `FaturamentoService`. De quebra,
  `DespublicarAsync` passou a devolver `bool` e a receber o operador: engolir a falha do
  S3 fazia a tela dizer "saiu do ar" com o arquivo ainda acessível, que é falha exibida
  como sucesso.
  **O modelo de evolução** fechou a lacuna mais cara do dia a dia clínico: `ModeloDocumento`
  existe desde a parcela 3 e serve só aos papéis IMPRESSOS, enquanto a evolução — o texto
  mais escrito do sistema — era redigitada por inteiro a cada sessão. Vale a regra do
  protocolo do mapa corporal: **aplicar COPIA, nunca aponta**, e aqui ela não é desenho, é
  a Lei 13.787/2018 — referência viva faria corrigir uma palavra do roteiro hoje reescrever
  o prontuário da semana passada. É por copiar que o modelo é a única coisa "de prontuário"
  que **se apaga mesmo**. O índice de nome é POR DONO: um global faria o "Sessão padrão" de
  um profissional sobrescrever o de outro em silêncio.
  **O CID-10 virou atalho com conferência.** O campo era texto livre em `DocumentoClinico`
  e em `ProblemaPaciente`, e "M54.4" digitado no lugar de "M54.5" é tão plausível quanto
  ele — nada denunciava. O catálogo mora em CÓDIGO pelo desenho das escalas (classificação
  PUBLICADA, não configuração da clínica) e **não é a CID-10 inteira**: são os códigos
  desta clínica, o campo continua aceitando qualquer texto, e a tela diz isso. Recusar o
  que está fora da lista seria a regra apertada demais que o projeto já rejeitou no
  formato do número da guia. A metade que pega o erro é a DESCRIÇÃO ao lado do campo.
  **A conciliação bancária por OFX** fechou o último ponto do financeiro em que o mês
  dependia de alguém não se distrair. O leitor é à mão (o OFX 1.x tem tags que não fecham
  — não é XML), o valor é lido em cultura INVARIANTE (em pt-BR, "-2500.00" viraria
  -250000), e o sistema **propõe, a pessoa confirma**. A folga de datas é de três dias e
  não de trinta: folga grande casaria o aluguel de julho com o de agosto e a tela passaria
  a pedir desempate em tudo, que é como se ensina alguém a clicar sem olhar. A metade que
  ninguém pede é `SoNoSistema` — dado como recebido aqui e ausente do banco.
  ⚠️ **E a "dívida de leiaute" das seis telas já estava PAGA** desde a parcela 49; a
  planilha é que não tinha sido atualizada. Os 330 px do `AcessosView` e os 300 do
  `CampanhasView` são a coluna de **Ações** — o falso positivo que a parcela 54 já
  documentara. Somado ao ✅ falso da feature 02, a lição fecha nos dois sentidos: **linha
  de placar sem conferência no código é chute com aparência de registro**, e ela erra
  tanto a favor quanto contra.

- **Nome de propriedade errado em controle da CASA: só o compilador de MARCAÇÃO pega**
  (parcela 63, checagem 34 — o CI reprovou o PR). Três telas novas declararam
  `TextoVazio` no `EstadoDaTela`, que tem `TextoCarregando` e `TextoNaoVerificado` — o
  vazio se escreve com `Titulo` + `Descricao`. `MC3072`, e nenhuma rede local via: o XML é
  bem-formado, o `compilar-sombra` **não lê o corpo** do XAML e o C# compila. É a irmã da
  checagem 33, e o que a torna fácil de cometer é o nome plausível existir AO LADO do
  certo.
  A checagem casa cada atributo de `<ctrl:Tipo …>` com as propriedades declaradas no tipo
  e nas bases dele. As duas decisões que a mantêm utilizável: cadeia que termina numa base
  do WPF conhecida responde **"não tem"** (sem isso ela calaria para todo controle que
  herda de `Control`, que são todos); cadeia que termina em tipo de fora desconhecido
  responde **"não sei"** e cala.
  ⚠️ O autoteste pegou os DOIS erros dela antes de mim — a primeira versão respondia "não
  sei" para o caso real, e a segunda acusava o `Key` de `x:Key` por o *lookbehind* não
  excluir os dois-pontos. **Checagem nova sem autoteste do caso real e do caso legítimo é
  checagem que nasce cega ou barulhenta.**

- **A LINHA "Personalizado" DO CATÁLOGO É RENOMEÁVEL, e isso vazava para TODA operadora**
  (parcela 68 — a clínica mandou a foto: oito pacientes **Sul América** apareciam faturando
  como **"Porto Saúde"** na consulta de guias, e a lista de pacientes ao lado, na mesma
  janela, mostrava "SULAMERICA" para os mesmos oito).
  A causa não é o catálogo: é `ConvenioCatalogoService.ListarAsync` **garantir uma linha por
  família**, inclusive uma de código `"Personalizado"` — e essa linha é editável como
  qualquer outra. A clínica renomeou-a para a primeira operadora que cadastrou (em vez de
  clicar "Adicionar"), e a partir daí `CatalogoConvenios.Nome(Convenio.Personalizado)`
  passou a devolver o nome DELA para toda operadora personalizada.
  ⚠️ **Este é o defeito da parcela 50 na única variante que a correção dela não cobriu, e é
  a pior.** Lá, resolver pela família escrevia **"Personalizado"** — feio, óbvio, e alguém
  abre chamado. Aqui escreve **"Porto Saúde"**: o nome de uma operadora de verdade, na guia
  de outra operadora, com toda a cara de estar certo. É a **garantia aparente** de novo, e
  só foi descoberto porque o cliente comparou duas telas.
  A blindagem é `Nome(Convenio familia)` **não passar mais pelo catálogo quando a família é
  `Personalizado`** — ela devolve o rótulo neutro `"Convênio personalizado"`. Pior de ler e
  muito melhor de confiar: não responder qual operadora é **é a verdade**, porque a família
  não sabe. ⚠️ A assimetria com `Nome(codigo, familia)` é deliberada: **lá** o caminho de
  baixo continua indo ao catálogo, porque sem código a linha da família é o melhor palpite
  que existe — o paciente foi mesmo cadastrado nela, e blindar os dois lados apagaria o
  nome de quem estava certo.
  **O agrupamento por família era pior que o rótulo**, porque não é aparência, é número:
  `RelatorioService.PorConvenio` e a estatística do `PrevencaoGlosaService` agrupavam pela
  FAMÍLIA, então Sul América e Porto Saúde caíam numa linha só com o nome de uma delas. A
  direção lia "Porto Saúde: 143 guias" e eram as duas somadas — o número que ela usa para
  negociar tabela. Os dois passaram a agrupar por OPERADORA, que é o que o lote TISS já faz
  desde a parcela 60 (`ConvenioDaGuia`) e pela mesma razão.
  ⚠️ **O teste que existia para exatamente este assunto (`NomeDoConvenioTests`, parcela 50)
  passava.** Ele monta um catálogo SEM linha de código "Personalizado" e afirma que a
  resolução por família não devolve o nome da operadora **certa** — o que é verdade e deixa
  passar o caso real, que é devolver o nome de uma operadora **errada**. A asserção tinha de
  ser "não é o nome de operadora nenhuma". **Teste de "não é X" só vale quando X é o
  conjunto inteiro dos valores errados; contra UM valor errado, ele passa e o defeito
  também.**
  A **checagem 35** cobra o binding, e o que a mantém utilizável é resolver o **TIPO** e
  nunca o nome: `Convenio` é `string` já resolvida numa dúzia de records de ViewModel
  (Conciliação, Gerente, Recepção), e acusá-los seria o ruído que faz alguém desligar a
  ferramenta. Ela dispara quando o tipo declara `Convenio` como ENUM **e** oferece
  `ConvenioNome` ao lado — quem oferece o nome resolvido está dizendo que sabe qual é a
  operadora. Sem a segunda metade ela acusava a tabela "Regras por família" de
  Configurações, que é por família mesmo. O tipo do item de lista sai do ViewModel **da
  própria tela** (`FooView.xaml` ↔ `FooViewModel`), nunca da busca global da checagem 20:
  `Itens` e `PorConvenio` existem em telas diferentes com tipos diferentes, e a busca global
  respondia o tipo do vizinho — a resposta certa vinda do arquivo errado.

- **Tabela empilhada em `StackPanel` sem rolagem: o fim da tela é CORTADO, sem barra e sem
  como alcançar** (parcela 68, 2ª rodada — a cliente mandou a foto: em Relatórios, a seção
  "Não conformidades (guias justificadas na rodada)" aparecia só com o TÍTULO, decepada na
  borda de baixo da janela). A mecânica é de uma linha: a janela tem altura FINITA e o
  `StackPanel` vertical dá a cada filho a altura que ele PEDE, ignorando o disponível —
  três cards com tabela somam mais que a tela, e sem `ScrollViewer` o resto some.
  ⚠️ **Não é a mesma coisa que a tela cujo conteúdo elástico é UM `DataGrid` numa linha
  `*`** — aquele rola por dentro e a linha `*` absorve o resto. Cinco telas do faturamento
  têm essa forma e estão certas. O que distingue as duas não é a linha `*`, é o
  EMPILHAMENTO: no `StackPanel` nada absorve.
  O remédio é o padrão do `DashboardView`, e são **três** peças, não uma: `ScrollViewer` na
  raiz; **todas as linhas `Auto`** (dentro de um ScrollViewer a altura disponível é
  infinita, então `*` não distribui nada — ele mede igual a `Auto` e só engana quem lê); e
  **`MaxHeight` nas grades que crescem com o dado**, senão elas recebem altura infinita,
  desenham todas as linhas, perdem a virtualização e empurram as de baixo para longe. Grade
  de tamanho fixo (as 3 faixas de envelhecimento, os 6 meses) não leva teto — teto onde não
  precisa é o corte de volta.
  ⚠️ **A checagem 36 nasceu CEGA e o autoteste é que mostrou**: a primeira versão exigia
  que a pilha estivesse dentro de uma linha `*`, porque era assim no caso real — e ficou
  sem ver a variante PIOR, a mesma pilha com todas as linhas `Auto`, que corta igual. A
  linha `*` era coincidência do exemplo, não a causa. **Quando causa e sintoma aparecem
  juntos no primeiro caso, confira qual dos dois a checagem está olhando.** Autotestada nas
  duas formas quebradas e nas três legítimas, e ela chama a MESMA função da varredura, pela
  lição da parcela 67 (autoteste que reimplementa fica verde quando a checagem quebra).
  Nenhuma rede pegava, e é a categoria mais cara: o XAML é bem-formado, o
  `compilar-sombra` não lê o corpo, o compilador de marcação não reclama e nada lança. Só a
  tela montada mostra — e só em quem tem a janela mais baixa que o conteúdo, que **nunca é
  a máquina de quem escreveu**.

- **Duas caixas editando o MESMO campo é o que faz uma tela "não se entender"** (parcela
  69 — a cliente disse que a caixa de convênios das Configurações estava "horrível e não
  muito fácil de entender"). A aba era uma pilha de SEIS `Card` (o proibido nº 2 da regra
  de leiaute), espremida em `MaxWidth="860"` — e o pior não era o empilhamento: `Nome` e
  `Fatura como (família)` eram editáveis **na grade E no painel de baixo**, dois campos
  para o mesmo dado a 300 px um do outro. Quem lê conclui que são coisas diferentes e
  procura a diferença que não existe.
  O desenho é o que a regra manda: **lista de largura inteira → janela do item**. A lista
  passou a RESPONDER a pergunta da tela — família, gera guia ou é particular, forma do nº
  da guia e registro ANS numa linha só, o que antes exigia clicar convênio por convênio —,
  e configurar, que se faz de vez em quando, mora atrás de um clique. A janela recebe o
  **mesmo `ConvenioEdicao`** da lista (duas cópias dariam duas verdades) e por isso não tem
  "Cancelar": quem grava continua sendo o "Salvar configurações" da tela de trás, e o
  rodapé escreve isso em vez de deixar a pessoa supor.
  Três coisas que a rodada separou e que valem além desta tela: (a) **a tabela "por
  família" não é a lista de operadoras** — ela responde "quanto vale o número da regra que
  várias compartilham", e ficava embaixo da outra parecendo mais do mesmo; virou janela
  própria com o aviso de que o número vale para TODAS as operadoras daquela família;
  (b) **os três prazos globais** (alerta de consulta, recurso de glosa, rodada de
  pendências) não eram de convênio nenhum e moravam ali — foram para uma aba "Prazos",
  que é a 3ª pergunta da regra de leiaute aplicada literalmente; (c) o rótulo dessa aba é
  curto de propósito, porque o `TabPanel` padrão **encolhe** as abas e a janela mínima do
  faturamento (960) menos a sidebar (240) põe seis abas no limite — aba cortada no meio da
  palavra já foi reprovação do cliente na parcela 50.
  ⚠️ E **a linguagem é metade do "não se entende"**: "candidata a 2º código" e "Categoria
  (com app)" descrevem o modelo, não o trabalho. Viraram "A clínica atende com
  eletroacupuntura neste convênio" e "Semáforo quando o paciente TEM app", cada campo com
  a consequência escrita ao lado — o mesmo que a parcela 49 fez com as caixinhas de
  permissão. Nada foi tirado: todo campo editável antes continua editável, um clique
  adiante.

- **O CONSENTIMENTO ERA UMA CAIXINHA — e a única prova era a palavra de quem clicou**
  (parcela 89; o mapa completo está em `docs/termo-assinado-pelo-paciente.md` §8, e é lá
  que se atualiza). A direção pediu que **todo** documento que precisa da assinatura do
  paciente fosse pelo Worker, como o termo do BSV, e nomeou os quatro consentimentos LGPD
  da Recepção. Medido antes de escrever: **a coleta inteira já era genérica sobre
  `DocumentoClinico`** — a janela, a segunda tela, o traço, a evidência, o selo, o envio
  pelo WhatsApp e a volta pelo Worker olham `AguardaAssinaturaDoPaciente`, que responde por
  `TipoDocumentoInfo.AssinadoPeloPaciente`. **Faltava uma linha de portão.** É o defeito
  recorrente do projeto na variante mais barata de corrigir e a mais cara de deixar: a
  capacidade existia, testada e em produção, para um tipo só.
  ⚠️ **A inversão é a metade que importa.** O termo era o RECIBO do que o balcão marcara;
  passou a ser a FONTE. Com ele como recibo, o paciente podia responder "Não" ao marketing
  no celular e a clínica continuar mandando campanha, porque a caixinha seguia marcada —
  **duas verdades sobre o mesmo fato, e nada falha: a campanha simplesmente sai**. E o
  problema maior é jurídico: o art. 8º pede manifestação do TITULAR e o §2º põe o ônus da
  prova em quem trata o dado, e a nossa prova era um clique da recepcionista.
  Três decisões da direção: a **resposta assinada vence**; a caixinha **deixa de existir**
  (o termo é o único caminho — o que não trava quem não tem celular, porque a coleta no
  balcão continua sendo assinatura; o que acaba é o consentimento SEM assinatura nenhuma);
  e é **um termo com as quatro declarações**, não quatro papéis.
  ⚠️ **O vínculo é por CÓDIGO** (`ItemDocumento.Codigo`, migration aditiva), nunca por
  `Ordem` — seria o contrato de ÍNDICE que a parcela 41 trocou por nome: acrescentar uma
  finalidade no meio empurraria todas, e o "Sim" do uso de imagem viraria autorização para
  compartilhar com o convênio, **sem quebrar build nenhum**. Nem pelo RÓTULO, que a clínica
  reescreve. Código não reconhecível é **ignorado, nunca adivinhado**: deduzir pela posição
  gravaria a autorização errada, que é pior do que não gravar.
  ⚠️ **O PAPEL tinha de mudar junto, e essa foi a armadilha da parcela.** O desenho antigo
  (`ListaFinalidades`) marcava um X quando a resposta era a palavra `"Autorizado"` e
  escrevia "Pendente" no resto — com as respostas em "Sim"/"Não" ele imprimiria TODA
  finalidade como pendente, e um **"Não" sairia idêntico a uma pergunta não respondida**,
  no papel que o paciente leva justamente para provar o que recusou. O termo LGPD passou a
  usar o MESMO desenho do termo de procedimento. A regra que fica: **ao trocar o que um
  campo GUARDA, procure quem o IMPRIME** — o renderizador casa por VALOR e não quebra
  build ao deixar de reconhecê-lo.
  ⚠️ E **`RespostaDeclaracao` desceu da Application para o DOMÍNIO**: o termo LGPD precisa
  ler a mesma resposta e o Domínio não enxerga a Application. Uma segunda cópia de "isto é
  um sim?" divergiria na primeira correção — aqui, o paciente responder "Não" e o sistema
  gravar outra coisa. Pelo mesmo argumento, o par *linha + auditoria* do consentimento saiu
  para `ConsentimentoService.Montar`: são dois caminhos de escrita e um não pode chamar o
  outro (o termo grava no MESMO `SaveChanges` do ato; `RegistrarAsync` tem o `SalvarAsync`
  dele), e duas montagens divergiriam na AÇÃO de auditoria, que é o nome pelo qual uma
  investigação procura.
  ⚠️ **Reler RASTREADO na revogação**: `ConsentimentosDoPacienteAsync` é `AsNoTracking` —
  ela existe para LER —, e mutar o que ela devolve não grava nada. A revogação sumiria em
  silêncio, e a ficha mostraria uma autorização sem fim ao lado da recusa assinada.
  **REVOGAR continua sem assinatura, e a assimetria é da lei**: revogar é direito
  UNILATERAL do titular (art. 8º, §5º; art. 18), atendido de imediato, inclusive por
  telefone — exigir termo assinado dificultaria o lado que a LGPD manda facilitar.
  A **procedência** entrou na linha de cada finalidade ("Concedido em 12/03 · termo
  2026/0007"): é o número do termo que responde *"onde está a prova?"*, e sem ele a
  auditoria continuaria acreditando na palavra de quem clicou.
  ⚠️ E a releitura do próprio diff pegou o de sempre: `CarregarDocumentosAsync` limpava a
  lista **depois** do await, então uma falha de leitura deixaria as linhas do paciente
  ANTERIOR na tela com o botão "Colher assinatura…" apontando para o `TermoLgpdPendenteId`
  do outro — **um clique assinaria o termo de quem já saiu, em nome de quem está na
  frente**. É a lição da parcela 66, 2ª rodada, na tela vizinha à que a ensinou.

- **LIGAR UM PORTÃO NOVO FAZ O DADO ANTIGO SATISFAZER A CONDIÇÃO NOVA** (parcela 89, 2ª
  rodada — a clínica colheu o termo e o alerta *"Sem consentimento LGPD de tratamento de
  dados"* continuou aceso; o print do papel resolveu o diagnóstico em um olhar). O termo
  de consentimento passou a ser `AssinadoPeloPaciente`, e no MESMO instante todo termo LGPD
  já emitido pela versão anterior — quando ele era o **RECIBO** da caixinha do balcão —
  passou a satisfazer `AguardaAssinaturaDoPaciente`: não está cancelado, o paciente não
  assinou, não recusou. A ficha ofereceu um deles como "pendente", a coleta o
  **reaproveitou**, o paciente respondeu "Sim" nas quatro declarações, o documento saiu
  selado e completo — e **nenhum consentimento foi gravado**, porque os itens antigos não
  têm `Codigo` e `TermoConsentimento.Decisoes` não tinha por onde ler a resposta.
  ⚠️ **Nada falhou em lugar nenhum.** Build, 1938 testes, três redes locais e o CI verdes;
  o papel saiu perfeito; e o alerta continuou aceso do outro lado da tela. É a **garantia
  aparente** na forma mais discreta — e a mais cara, porque a clínica acredita ter colhido.
  **O papel é que denunciou**: a via saía com o RÓTULO da finalidade ("Tratamento de dados
  pessoais e de saúde") e o detalhe "Nunca perguntado", que são exatamente o que a emissão
  ANTIGA escrevia — conteúdo velho desenhado pelo renderizador novo. **Print de tela é
  evidência de primeira classe: leia o que está escrito, não o que devia estar.**
  A regra que fica, e ela vale para todo portão: **ao alargar a condição que um dado
  satisfaz, pergunte o que na BASE passa a satisfazê-la** — e se o que passa é a mesma
  coisa. Aqui não era: os dois papéis compartilham o `TipoDocumentoClinico` e são coisas
  diferentes; o discriminador é o `Codigo` do item, e ele não existia.
  As duas metades da correção, porque uma sem a outra não resolve: a porta **não OFERECE**
  o termo antigo (senão o paciente assina e só então leva a recusa) e `ColherAsync`
  **RECUSA** (senão a central, o link do WhatsApp ou uma tela futura reabrem o caminho).
  ⚠️ E a tela deixou de AFIRMAR "Assinado em 26/08" sobre um papel desses: header dizendo
  que o termo foi assinado com o alerta "sem consentimento" aceso no balcão são duas
  verdades sobre o mesmo fato — o defeito que esta parcela existe para acabar, cometido
  pela própria correção dele.
  ⚠️ **`Enum.TryParse` aceita NÚMERO, e `Enum.IsDefined` não salva**: `"1"` vira uma
  finalidade de verdade porque 1 É um valor definido. Quando o código guardado é o NOME, a
  conferência é de **ida e volta** (`finalidade.ToString() == codigo`) — senão um código
  numérico vindo de qualquer lugar grava a autorização de uma finalidade que ninguém
  escreveu.
  ⚠️ **A pergunta "quais termos carregam finalidade" virou consulta PRÓPRIA**, e não um
  `Include` na leitura dos documentos da ficha: aquela alimenta a lista inteira, e puxar os
  itens de todos arrastaria o `Desenho` dos relatórios de evolução — um mapa corporal por
  sessão — a cada abertura de ficha. E decidir pela navegação `documento.Itens` ali seria a
  lição da parcela 68 de novo: vazia em produção, cheia no teste pelo fixup do EF, com TODO
  termo — o novo inclusive — parecendo da versão anterior.

- **LISTA QUE ROLA POR DENTRO COME A RODA DO MOUSE, E A PÁGINA NÃO ROLA** (parcela 90 — o
  cliente: *"quando clicamos em lançar outro ele nos leva a tela de pacientes que está toda
  cortada junto a tela de lançados hoje"*, com a janela MAXIMIZADA). O `ScrollViewer` que o
  `ListBox` traz no template marca o evento da roda como TRATADO — sempre, inclusive no
  limite —, e ele nunca sobe para o `ScrollViewer` da página. Com a lista ocupando 300 px no
  meio da tela, ela é o **maior alvo do cursor**: a pessoa gira a roda em cima dela, a
  página não anda, e a leitura natural é que **a tela quebrou**.
  ⚠️ **O que me fez errar o primeiro palpite** foi tratar "não rola" como posição de
  rolagem. Não era: a rolagem existe e o `ScrollViewer` até desenha a barra — o que não
  existe é o EVENTO chegar nele. Perguntar *"rolar para cima resolve?"* foi o que separou as
  duas coisas, e a resposta **"não resolve"** matou a hipótese em uma pergunta. **Quando o
  sintoma é "não rola", separe POSIÇÃO de EVENTO antes de mexer em leiaute.**
  Por que só depois do "Lançar outro": `NovoLancamento` faz `Seletor.Termo = null`, o que
  dispara a busca e traz as 50 primeiras linhas. No primeiro open a lista também vem cheia
  — só que a pessoa **digita um nome imediatamente**, a lista encolhe para duas ou três
  linhas e nada passa da dobra. O estado "50 pacientes parados na tela + LANÇADOS HOJE" só
  acontece depois do reset, e é o único em que a página precisa rolar.
  ⚠️ **E ele não aparece na máquina de quem programa**: a coluna esquerda pede ~730 px, e
  isso só corta no monitor de 1366×768 do balcão ou com a escala do Windows em 125/150% —
  a mesma família da parcela 79.
  A correção é `Ajudantes.RodaDaPagina` no shell, e a **condição de borda é o que a separa
  de um remendo**: a roda vai para a página **só quando a lista já chegou ao fim naquela
  direção**. Sem isso, trocaríamos o defeito pelo oposto — a lista pararia de rolar.
  De quebra o `MinHeight="180"` da lista saiu: reservar 180 px de lista VAZIA empurrava a
  conferência do dia para fora da vista antes de haver o que mostrar.
  **Nenhuma rede pega**: o XAML é bem-formado, o binding é válido, nada lança, e as três
  redes locais e o CI ficam verdes. Só a tela montada — e só na altura errada.

- **LISTA SEM TETO CRESCE COM O DIA — e é a metade que só quebra DEPOIS de lançar**
  (parcela 90, 2ª rodada; a frase que a achou foi do cliente: *"esse erro acontece APÓS
  fazer um lançamento e clicar em 'lançar outro'"*). A roda do mouse explicava por que
  rolar não resolvia; **não explicava por que o corte só aparece depois de lançar**, e eu
  tinha dado a primeira metade por resposta inteira.
  A causa é `CarregarDoDiaAsync`, que roda **de novo a cada lançamento** e lista TODO
  agendamento de hoje com atendimento — não só o que saiu desta tela. Numa clínica que
  trabalhou o dia inteiro são 20 a 40 linhas, e o cartão LANÇADOS HOJE tinha `MinHeight` e
  **nenhum `MaxHeight`**: o `ItemsControl` desenha todas, sem virtualização, e a coluna
  esquerda passa de dois mil pixels — crescendo a cada guia lançada. Some-se a lista de
  pacientes que o reset reabre cheia, e o que a pessoa vê é a tela cortada logo depois de
  um lançamento bem-sucedido.
  ⚠️ **A regra que fica: `MinHeight` num cartão cujo conteúdo vem do BANCO é meia decisão.**
  O piso responde "não encolha quando estiver vazio"; ninguém respondeu "até onde pode
  crescer quando estiver cheio" — e a resposta implícita do WPF é *sem limite*. É a irmã da
  checagem 36 (dentro de um `ScrollViewer` a altura disponível é INFINITA, então nada
  distribui) e da parcela 79 (filho ancorado que não cabe é decepado): **quem cresce com o
  dado precisa de teto, e o teto vem com rolagem por dentro.**
  ⚠️ E o teto sozinho seria trocar o defeito de lugar: lista que rola por dentro come a
  roda (a lição acima), então o `ScrollViewer` novo nasceu com `RodaDaPagina`. **As duas
  metades andam juntas — teto sem devolução da roda é a mesma tela travada, numa altura
  menor.**
  A lição de método é a mais cara da rodada: **quando o cliente diz QUANDO o defeito
  acontece, o "quando" é parte do diagnóstico, não contexto.** Achei um mecanismo real,
  fechei a resposta com ele, e o gatilho que ele não explicava ficou de fora — foi preciso
  o cliente repetir a palavra "APÓS" para eu ir ler o que o `Lançar outro` faz de
  diferente. **Mecanismo que não explica o gatilho é meia causa.**

- **A MESMA RODA COMIDA EM MAIS DEZESSEIS LUGARES — e metade foi CRIADA por uma correção
  nossa** (parcela 90, 3ª rodada — a varredura que respondeu à pergunta *"ainda ficou
  erros?"*). `ScrollViewer.OnMouseWheel` marca o evento como TRATADO **sempre**, inclusive
  quando não há o que rolar; então todo controle cujo template traz um `ScrollViewer`
  (`DataGrid`, `ListBox`, `ListView`, `TreeView`, `RichTextBox`, e o próprio `ScrollViewer`
  aninhado) come a roda. A primeira varredura filtrou por "tem `MaxHeight`" e por isso
  deixou dois de fora: **teto não é a condição — a condição é ter `ScrollViewer` no
  template.**
  ⚠️ **Doze das dezesseis estavam no FATURAMENTO, e a checagem 36 é quem as pôs lá.** A
  correção da parcela 68 (seção decepada em Relatórios) manda pôr `ScrollViewer` na raiz e
  teto nas grades — e é exatamente isso que dá a cada grade um `ScrollViewer` próprio para
  comer a roda. **As duas metades andam juntas**, e uma correção que só aplica a primeira
  troca o corte por uma tela travada. Quatro delas ficam no **Dashboard**, que é a tela de
  ABERTURA do app que fatura a clínica.
  As outras quatro: a régua da EVA na janela de evolução (duas — `ListBox` horizontal, sem
  nada que role, comendo a roda vertical do formulário inteiro) e as duas listas da Guarda
  de prontuário do Gerente.
  ⚠️ **O que NÃO era defeito, e a regra que o separa**: os dez `ScrollViewer` das raias do
  kanban (Fila e Meu dia) estão dentro de uma página que rola **só na horizontal**
  (`VerticalScrollBarVisibility="Disabled"`) — ali comer a roda vertical é o
  comportamento certo, porque quem deve rolar na vertical é a raia. A busca sobe **pulando**
  essas páginas horizontais: se houver uma vertical mais acima, o defeito continua de pé.
  `Ajudantes.RodaDaPagina` teve de ser **portado para o design system do faturamento** — os
  dois não se referenciam, o débito permanente da Fase 4 —, como o `obrigatorio` do
  `PromptWindow` (parcela 75) e o `TextoParaVisibilidade` (parcela 61). **Correção de
  ajudante do shell se porta para o outro lado no mesmo commit**; a cópia que ficar para
  trás é onde a capacidade some.
  Virou a **checagem 43**, medida antes de ligar: **zero** ocorrências depois da correção —
  ela nasce sem uma linha de ruído. Autotestada contra o caso real (verificado removendo o
  atributo de uma grade do Dashboard: ela acusa a linha exata) e contra os três legítimos —
  com o atributo, kanban horizontal, e sem página rolante acima.

- **`CornerRadius` 999 no WPF desenha um OVO, não uma pílula** (parcela 91 — o cliente
  mandou a foto da busca global e dos chips ovais). O CSS trava raio maior que a metade da
  altura NA metade; o WPF não trava — os arcos saem por inteiro e as bordas de cima e de
  baixo ficam curvas. O token `Raio.Pilula` (999, espelho fiel do CSS do kit web) era
  referenciado cru em TREZE lugares dos dois design systems, e ninguém tinha visto porque a
  deformação cresce com a LARGURA: o badge estreito engana, a busca larga denuncia.
  Pílula de verdade é **`ctrl:Ajudantes.Pilula="True"`** (nos dois design systems), que
  mede a altura REAL do Border e aplica raio = metade do menor lado a cada mudança de
  tamanho; círculo de tamanho FIXO usa raio explícito. O token fica — é o espelho do CSS,
  onde 999 é correto — com o ⚠️ escrito; quem impede o uso cru é a **checagem 44**,
  autotestada nas duas formas do defeito (atributo e setter de estilo) e nos dois
  legítimos (a definição do token e o comentário). A lição que generaliza é a das
  parcelas 68/79: **valor que atravessa a fronteira entre dois sistemas de desenho (CSS ↔
  WPF, QuestPDF ↔ PDFsharp) tem a semântica MEDIDA no destino, nunca presumida da origem.**


- **AS TELAS PLANAS DE PRONTUÁRIOS E EXAMES, e a situação que só existe se houver FATO**
  (set/2026 — as duas telas do handoff que faltavam no Consultório). A tela de Exames
  pede "Aguardando resultado / Resultado disponível", e o domínio não tinha o elo que
  sustenta a resposta: o laudo entrava como anexo da sessão ou como `ResultadoExame`
  avulso, sem dizer DE QUAL pedido era. O elo é `ResultadoExame.PedidoDocumentoId`
  (migration aditiva, nullable — linha antiga fica com nulo, que é a verdade), a situação
  é DERIVADA da contagem de resultados vigentes amarrados, e o serviço RECUSA amarrar em
  pedido de OUTRO paciente ou em documento que não é pedido — o vínculo errado daria
  baixa na espera de outra pessoa. O "Agendado" do mockup ficou de FORA: não há fato de
  agendamento de exame no domínio, e situação sem fato é a garantia aparente de sempre.
  ⚠️ **A tela de Prontuários dá a cada linha a situação que ela TEM, não a que o mockup
  pinta**: anamnese é DOCUMENTO e tem assinatura de verdade ("A assinar" → o fluxo
  ICP-Brasil existente); evolução NÃO é assinável — o pendente real dela é a sessão sem
  registro ("A escrever", levando ao atendimento daquele horário). As linhas carregam
  `EvolucaoId` e `DocumentoId` em campos SEPARADOS (ids são por tabela — parcela 71), e
  quem pede AÇÃO sobe para o topo. Os montadores são PUROS na Application
  (`ListaDeProntuarios`, `PedidoDeExameLinha`) — o que a tela afirma mora onde o
  `dotnet test` alcança. As listas são PROJEÇÕES novas do repositório (sem os textos da
  evolução, sem a miniatura da foto — a lição da parcela 74), executadas em teste e na
  rede de tradução do Npgsql. O combo "responde ao pedido" mora na ÚNICA janela de
  registro de resultado, para as duas portas amarrarem pela mesma regra; e o "Novo pedido
  de exame" de uma tela SEM paciente em foco pergunta QUEM primeiro
  (`EscolherPacienteWindow`, no shell — capacidade em porta única é o defeito recorrente).
  Na lista da CLÍNICA inteira (posto sem vínculo/enfermagem) cada linha diz o
  profissional — booleano de estado sem leitor é só uma atribuição (parcela 76).

- **O LAUDO EM ARQUIVO, e por que ele não virou "anexo"** (set/2026 — a clínica recebe o
  PDF do laboratório por WhatsApp e precisa SUBIR). O caminho óbvio seria o
  `AnexoProntuario`, que já guarda arquivo clínico — e ele foi MEDIDO e recusado:
  `EvolucaoId` é obrigatório e `AnexosDoPacienteAsync` resolve o paciente ATRAVÉS da
  evolução (`a.Evolucao!.PacienteId`), então anexo sem sessão não existe. Aceitá-lo
  exigiria `AlterColumn` (migration não aditiva) mais uma coluna nova obrigatória com
  backfill, num app em produção — e o laudo que chega pelo WhatsApp não é de sessão
  nenhuma: forçá-lo a uma evolução seria inventar uma consulta que não houve.
  O laudo mora no `ResultadoExame`, que É o que ele registra. As decisões:
  **os BYTES ficam em tabela 1:1** (`ArquivosResultadoExame`) e só os METADADOS na
  linha — é o padrão do retrato do paciente (`FotoMiniatura` na linha, `PacientesFotos`
  à parte), e sem ele toda leitura da lista arrastaria os PDFs pela rede (a lição da
  parcela 74; a rede de tradução do Npgsql fixa que o SELECT não os traz);
  **valor OU laudo** — o PDF é registro completo por si, e exigir também o número faria
  a técnica inventar um para conseguir anexar; o que se recusa é o registro sem conteúdo
  nenhum; **o teto é o MESMO do anexo de prontuário** (`ProntuarioService.
  TamanhoMaximoAnexo`), porque dois limites divergem na primeira correção; e o arquivo é
  gravado no **MESMO `SaveChanges`** do resultado — laudo sem a linha que o descreve é
  arquivo órfão, e linha que promete arquivo sem ele é um "abrir laudo" que não abre.
  ⚠️ **Anexar aparece em TODO pedido vivo, não só no que aguarda**: um pedido de vários
  exames recebe vários laudos, e esconder o botão no primeiro resultado impediria o
  segundo. Só o pedido CANCELADO não recebe.
  ⚠️ E a mudança **desatualizou dois textos que ninguém teria relido**: o comentário do
  modal de resultado dizia que "o resultado estruturado não tem arquivo", e o subtítulo
  da seção Exames e anexos mandava anexar pela sessão no Prontuário. Os dois foram
  corrigidos no mesmo commit — **ao dar capacidade nova a um registro, procure o que
  AFIRMAVA que ela não existia.**

- **A IMPORTAÇÃO DA CARTEIRA DO SISTEMA ANTERIOR — e a auditoria que ficaria pendurada**
  (set/2026 — a clínica migrou do Smart Clinic e recebeu a exportação; roteiro em
  `docs/importar-pacientes.md`). `ImportacaoPacientesService` em dois passos separados de
  propósito: `PreverAsync` lê o CSV com o mapeamento e diz linha a linha o que VAI acontecer
  sem gravar; `ExecutarAsync` grava o que a prévia mostrou. As regras: **idempotente pela
  chave** (`Paciente.ChaveImportacao = IMPORT:{sistema}:{id}`, índice ÚNICO — a coluna nasce
  vazia, então o índice não tem como falhar na abertura), **quem existe é COMPLETADO e nunca
  sobrescrito** (CPF com ou sem máscara, ou nome + nascimento; convênio da ficha é mantido),
  **a criação passa pelo `PacienteService`** (a regra do CPF mora lá), e **convênio do
  arquivo é TEXTO que a direção aponta para um `ConvenioCadastro`** — sem palpite, porque
  "Unimed" pode ser Padrão ou Intercâmbio.
  ⚠️ **Auditoria acrescentada ANTES de um serviço que pode RECUSAR fica pendurada no
  contexto.** A primeira versão registrava o `EventoAuditoria` e só então chamava
  `SalvarNovoAsync`; quando ele recusava a ficha (CPF que entrou por outra porta), a linha
  "PacienteImportado" ficava rastreada e saía gravada junto da ficha SEGUINTE — trilha
  afirmando uma importação que não houve. Aqui a ordem é ato → trilha → Salvar, com o id
  da ficha; é uma exceção declarada à regra 7 do compromisso. **E entidade RASTREADA mexida
  antes de uma recusa sai no próximo Salvar de outra linha** — daí o retrato/restauração
  na ficha completada. Regra: **num laço que grava N linhas com um `DbContext` só, toda
  linha que falha tem de deixar o contexto como o encontrou.**
  ⚠️ O prontuário antigo **não é PDF**: a exportação real traz o prontuário em TEXTO por
  paciente (pós-operatório, S-O-A-P, prescrições, anamnese), e a direção decidiu **"não
  perder NADA"** — ver o bloco do PACOTE abaixo.
  ⚠️ **Sugestão de coluna se mede contra o arquivo REAL, não contra os sinônimos que o
  autor imagina.** A primeira versão do sugestor, rodada no `pacientes.csv` da clínica,
  mandou o convênio para `operadora` (a operadora do CELULAR — a prévia oferecia 80
  números de telefone como convênios a mapear), deixou a carteirinha sem coluna
  (`numero_convenio`) e escolheu `telefone` (78 preenchidos) em vez de `celular` (2.145).
  Nenhum teste sintético pegaria: os sinônimos eram os meus. A regra que ficou: a
  prioridade é do SINÔNIMO dentro de cada campo (o primeiro que existir no arquivo vence),
  nunca da ordem das colunas; e o cabeçalho real virou teste. E **aviso por linha que se
  repete em 3 de cada 4 linhas não é aviso** — o sexo em branco virou contagem no aviso
  geral, senão os 19 avisos que importavam ficavam enterrados em 1.712 iguais.

- **O PACOTE DO SMART CLINIC — "não perder NADA", e o que isso decidiu** (set/2026;
  `ImportacaoSmartClinicService`, mapa arquivo→destino em `docs/importar-pacientes.md`).
  O ZIP tem 14 CSVs, e cada um ganhou destino ESCRITO: a carteira vira ficha; os oito
  arquivos de prontuário viram `Evolucao` (HTML → texto, data de lá, autor como o sistema
  antigo gravou em `CriadoPor`, vínculo com a Equipe quando o nome casa); a agenda FUTURA
  vira `Agendamento`; a agenda PASSADA e toda coluna sem campo (e-mail, RG, nome da mãe…)
  vão para as OBSERVAÇÕES da ficha, com rótulo. Login e senha nunca entram.
  ⚠️ **Visita passada não vira sessão.** Marcar 9.000 horários antigos como `Realizado`
  sem evolução ligada inundaria "sessões sem evolução" e o alerta da direção; criar
  `Atendimento` inventaria guia. Preservar como texto legível na ficha é o que não mente.
  ⚠️ **Alargar coluna é a saída consciente da checagem 18, e é a certa aqui**: 88
  registros passavam de 4.000 caracteres, e cortar registro clínico na importação é
  perder o que a clínica pediu para não perder. Os quatro textos da `Evolucao` (e os da
  `VersaoEvolucao`, senão a primeira correção de um importado falharia ao guardar o
  anterior) viraram `text`, com a marca `MIGRATION-NAO-ADITIVA-CONSCIENTE(AlterColumn)`.
  ⚠️ **A regra do "registro vazio" virou UMA função** (`ProntuarioService.TemRegistro`):
  a importação grava pelo repositório em LOTES de 200 (4.287 registros um a um, com
  auditoria cada, seriam minutos num banco remoto — e milhares de linhas de trilha
  enterrariam a trilha), e precisava da mesma pergunta que o `SalvarAsync` faz. Duas
  definições divergiriam na primeira correção. A trilha é uma linha por LOTE; a
  procedência de cada registro está nele (chave e autor).
  ⚠️ **As três objeções da direção mudaram o desenho, e as três eram sobre DECISÃO FORA
  DO LUGAR.** (a) "Cadastrar a Equipe antes? Metade nem trabalha mais": o registro guarda
  o autor como texto, e a rodada seguinte REVINCULA quem foi cadastrado depois
  (`RevincularAsync`, UPDATE em lote pelo nome em `CriadoPor`/observação) — a ordem
  deixou de importar. (b) "Apontar convênio? Que o sistema acuse quando o paciente
  aparecer": nasceu `ConvenioCadastro.ADefinir` (não gera guia) e o alerta VERMELHO no
  `ElegibilidadeService` — a decisão vai para onde ela é possível, ficha a ficha, com a
  pessoa na frente, em vez de 2.021 vezes antes de importar. (c) "Importar duas vezes é
  a duplicação que não queremos": a duplicata do próprio arquivo passou a entrar FUNDIDA
  na mesma rodada (`LinhaPrevia.FundeNaLinha`, resolvida na execução por
  `ResultadoImportacao.IdPorLinha`), e o prontuário dos dois ids antigos cai na mesma
  ficha. A lição: **quando o roteiro pede à pessoa um passo antes ou depois da ferramenta,
  pergunte se a ferramenta não deveria dar esse passo sozinha** — três "faça isso antes"
  eram três defeitos de desenho.
  ⚠️ **O JSON do sistema antigo tem quebra de linha CRUA dentro das strings** — a norma
  proíbe e o `JsonDocument` recusa. `ComposicaoSmartClinic.Sanear` escapa só dentro das
  aspas; JSON que ainda assim não se lê vira registro VAZIO na prévia, nunca texto
  inventado. E `Enum`/HTML/JSON: **tudo o que decide o que o prontuário importado DIZ mora
  na Application e é puro** — os compositores recebem a linha e devolvem a evolução, e
  são eles que os testes exercitam.

- **O NOVO ATENDIMENTO RECONHECE O HORÁRIO DO DIA — lançar SOBRE ele, nunca ao lado**
  (set/2026; o mapa está em `docs/guia-no-agendamento.md` §3.7.1). A semana da migração
  expôs o buraco: 227 horários importados do Smart Clinic, todos "Consulta", e a
  secretária lançando as sessões pelo Novo atendimento — que SEMPRE criava um encaixe.
  A pergunta de duplicidade não disparava (ela olha ATENDIMENTO já registrado, e o
  horário importado não tem), e sobravam dois cartões da mesma sessão. ⚠️ **E o parado
  levava a evolução**: a evolução importada depois (sem `AgendamentoId`) é distribuída
  na ordem da hora marcada (`EvolucaoDoHorario`), o importado das 09h00 vinha antes do
  encaixe das 09h12 — a sessão de verdade ficava em "Sessões sem evolução".
  `AgendaService.LancarNoHorarioAsync` é o gesto atômico do avulso aplicado a um horário
  que JÁ EXISTE: modalidade da tela passa a valer para ele (trilha quando mudou),
  `ChegadaEm ??=`, e o atendimento nasce pendurado nele pelo MESMO `ConfirmarNucleoAsync`.
  Com a chave "guia no agendamento" ligada, a modalidade nova regera pelo MESMO
  `AjustarAoRemarcarAsync` do Remarcar — segunda regra de regeração divergiria.
  As decisões: **nulo mantém** (sem código de modalidade, sem profissional, sem sala: o
  horário fica com o que tem — a regra da parcela 68); **a observação do horário não se
  perde** ("Importado do Smart Clinic · Consulta" fica, a nota da tela entra abaixo);
  **recusa com frase que diz o que fazer** para realizado (a outra máquina concluiu) e
  cancelado/falta (reabra ou lance como encaixe); **a caixinha "criar um encaixe
  separado"** é a saída do caso legítimo e só existe no modo "lançar agora"; **o botão
  DIZ** "Lançar no horário das 09h00 e gerar 2 guias"; e **leitura que falha escreve o
  terceiro estado** ("não foi possível conferir — lance pela Fila") em vez de cair no
  encaixe em silêncio, que é justamente a duplicata que o aviso existe para impedir.
  A lição que generaliza: **quando uma tela cria um registro que outra tela também cria,
  pergunte se o registro já EXISTE antes de criar** — a duplicidade que a capa pegava era
  a de atendimento; a de HORÁRIO passou seis parcelas invisível porque nenhuma das duas
  telas perguntava à outra.

- **O ACERVO DE ARQUIVOS DO SISTEMA ANTERIOR, e por que ele não virou "anexo"** (set/2026
  — a clínica recebeu a segunda exportação: 756 PDFs de receitas de 113 pacientes, com um
  índice dizendo de quem é cada um). A receita pertence à PESSOA e não a uma sessão, e os
  dois lugares que já guardam arquivo clínico foram MEDIDOS e recusados: `AnexoProntuario`
  exige `EvolucaoId` (forçar o PDF a uma evolução inventaria uma consulta que não houve —
  o argumento do laudo em arquivo) e `ResultadoExame` afirma que aquilo É um resultado —
  gravar uma receita ali seria mentir sobre a natureza do registro. Nasceu
  `AnexoPaciente`: a **décima raiz clínica**, no `CatalogoRegistroClinico`, com os oito
  lugares percorridos no mesmo commit (guarda, exportação, art. 18 II, cascata do excluir,
  rede de tradução) — o `ConjuntoClinicoTests` é quem cobra.
  As decisões: bytes em tabela 1:1 (a lista de 756 arquivos não pode arrastar 756 PDFs);
  **o MESMO teto** do anexo de prontuário; **cancela-se com motivo**, nunca se apaga; a
  montagem é pública e estática (`AnexoPacienteService.Montar`) porque tem DOIS chamadores —
  a tela e a importação em lotes — e duas validações divergiriam.
  ⚠️ **A ficha é achada pelo id do sistema anterior; o NOME só resolve quando é ÚNICO.**
  Casar "Maria Silva" com uma de duas põe a receita na ficha errada sem falhar em lugar
  nenhum — a homônima fica de fora COM o motivo. Pela mesma razão o ZIP se importa DEPOIS
  do pacote de pacientes, e a prévia diz isso em vez de deixar 756 linhas caírem em "sem
  paciente" caladas.
  ⚠️ **"Fechou" na conferência não quer dizer "tudo entrou"**: quer dizer que o que não
  entrou está LISTADO com a razão. O teste da conferência nasceu esperando o contrário e
  a regra já escrita do pacote é que estava certa — duas semânticas de "fechou" no mesmo
  botão fariam a direção ler "conferido" com significados diferentes a cada ZIP.
  ⚠️ **A LINHA DO TEMPO É O LEITOR DA FICHA — e a natureza nova nasceu fora dela** (o
  cliente: *"importei a pasta zipada mas não encontrei os arquivos dentro de cada
  ficha"*). Os oito lugares foram percorridos, o `ConjuntoClinicoTests` ficou verde, e a
  ficha da Recepção não mostrava nenhuma das 756 receitas: `LinhaDoTempoClinica.Montar`
  recebe listas TIPADAS, uma por natureza, e o teste dele afirmava as quatro que existiam
  em vez de percorrer o catálogo — **asserção que enumera à mão é asserção que a próxima
  natureza não alcança**. A porta que eu tinha construído (a região no Consultório) era a
  do app do MÉDICO; quem confere a importação abre a ficha no Gerente/Recepção. Agora o
  arquivo entra no montador, e abrir/cancelar passam por UM ponto do shell
  (`ArquivosDaFicha`), porque são três portas e a cópia que ficasse para trás abriria o
  PDF sem registrar quem leu.
  ⚠️ **Snapshot escrito à mão: no 1:1, o bloco de relacionamento do lado DEPENDENTE (o
  que declara `WithOne("Arquivo")`) vem ANTES do principal que chama `Navigation("Arquivo")`**
  — o `BuildModel` roda os blocos em sequência, e a navegação só existe depois do
  `WithOne`. Invertido, o snapshot inteiro LANÇA ao ser carregado ("Navigation … was not
  found"), e o `Snapshot_do_EF_esta_em_dia` é quem pega; copie a ordem do par
  `ArquivoResultadoExame` → `ResultadoExame`, nos dois arquivos (snapshot e Designer).

- **O PRONTUÁRIO DA SESSÃO: a aba duplicada, a sessão que não abria e o papel dela**
  (set/2026 — os três pedidos do cliente, e os três são o defeito recorrente do projeto
  em três variantes). Build verde, 2117 testes verdes e as três redes verdes o tempo
  todo: nenhum dos três quebrava nada.
  ⚠️ **A DUPLICATA DE RÓTULO NA SIDEBAR É A SEGUNDA, E AGORA TEM REDE.** "Prontuário"
  (Recepção, `prontuario`) e "Prontuários" (Consultório, `consultorio-prontuarios`)
  apareciam LADO A LADO em PACIENTE no Gerente Geral. A dedupe do `ShellViewModel` casa
  por **CHAVE** — ela funde a mesma tela publicada por dois módulos (a Sala de infusão, a
  Enfermagem, a Ajuda) e **não alcança** duas telas DIFERENTES com rótulos que dizem a
  mesma coisa. É literalmente o que aconteceu com "Prescrições" na parcela 55, e a
  correção é a mesma: item COMPOSTO com as duas telas como abas ("Por paciente" ×
  "Registros e pendências"). Nenhuma tela foi apagada, porque elas respondem perguntas
  diferentes — uma é "abra o prontuário DESTE paciente", a outra é a fila de trabalho do
  que falta escrever na clínica.
  A lição da parcela 68 manda escrever a rede na SEGUNDA ocorrência, e o ruído foi
  **medido antes de ligar**: a simulação da sidebar do Gerente devolveu **zero**
  duplicatas depois da correção, e a lista exata do defeito antes dela. Virou a
  **checagem 45**, que reproduz o que o shell FAZ (oculto não conta, chave repetida é
  fundida, sub-tela reivindicada por composto sai) — sem as três ela acusaria os pares
  legítimos, e checagem que grita no que está certo é checagem que alguém desliga.
  ⚠️ **Ela normaliza o PLURAL, e é isso que a faz valer**: o defeito real era
  "Prontuário" × "Prontuári**os**", e comparar texto cru deixaria passar justamente o
  caso que a motivou. Autotestada nos quatro cenários — e foi o autoteste que mostrou que
  o meu "antes" sintético estava errado (ele não fazia a chave atravessar por
  `ChavesSuite`, que é como o código real a passa).
  ⚠️ **A SESSÃO TINHA DOZE CAMPOS E QUATRO LEITORES MOSTRAVAM QUATRO.** História da doença
  atual, exame físico, hipótese, CID, plano terapêutico, retorno sugerido e encaminhamento
  (parcelas 73, 75 e 77) estavam gravados e **não tinham leitor em tela nenhuma** para uma
  sessão PASSADA: a lista do Consultório mostra quatro truncados, o modal de leitura
  rápida compõe UMA frase, e o painel da sessão anterior corta de propósito (a coluna tem
  350 px). Nasceu `SessaoDoProntuario` (Application, **puro** — o que a tela AFIRMA mora
  onde o `dotnet test` alcança) e a janela do shell que a abre.
  ⚠️ **VER e EDITAR são bits diferentes, e a linha só tinha o de escrever.** O "Abrir" da
  Recepção exige `EditarProntuario` e o rótulo MENTIA sobre o que ele faz (é a janela de
  edição): quem tem só `VerProntuario` — a técnica de enfermagem, o faturista — não
  alcançava a sessão por porta nenhuma. É o corte da parcela 49 sem a metade de ler.
  ⚠️ **E A FICHA DO PACIENTE FICOU PARA TRÁS — a correção foi feita na TELA, não no
  COMPONENTE que as duas usam** (set/2026, o cliente: *"se já é uma sessão feita, não
  deveria me dar a opção de ver/imprimir? Por que aparece para editar?"*). A tela de
  Prontuário ganhou `Ver · Editar · Cancelar…`; a aba Prontuário da FICHA, que lista as
  mesmas sessões pelo `LinhaDoTempoClinicaView`, continuou com **um** botão "Abrir" que
  abre a janela de EDIÇÃO — a sessão de ontem apresentada como formulário em branco, e
  nenhuma porta para quem só tem `VerProntuario`. **A cópia que fica para trás é onde a
  capacidade some** (parcelas 61, 75, 90), na variante em que a "cópia" é a TELA que não
  recebeu a correção do componente compartilhado. A regra que fica: **quando a correção
  é de uma tela que usa componente compartilhado, pergunte se ela pertence à tela ou ao
  componente** — no componente ela chega às outras portas sozinha; na tela, cada porta
  precisa que alguém lembre.
  ⚠️ **Ler e mexer viraram DUAS ações injetadas** (`AoVer`/`NaturezasComVer`/
  `AcessoParaVer` ao lado de `AoAbrir`), e não uma com dois bits: as duas não cobrem as
  mesmas naturezas — a ficha sabe LER a sessão médica e não tem o que "ver por inteiro"
  num arquivo, **cujo abrir já É a leitura**. E o RÓTULO do botão de abrir passou a vir da
  porta (`RotulosDeAbrir`), pela mesma razão: "Abrir" mente na sessão médica (ali ele
  edita) e está certo no arquivo da ficha. Rótulo fixo no componente compartilhado é a
  frase que mente numa das portas.
  ⚠️ **E a mesma lacuna estava na tela de ATENDIMENTO DA ENFERMAGEM**, onde ela custa
  conduta: a coluna que relê as sessões do médico mostra o resumo CORTADO, e quem executa
  precisa da hipótese e do plano ANTES de administrar. Ela ganhou só o **Ver** — escrever
  a sessão médica continua sendo do médico, e a técnica não tem `EditarProntuario`.
  **Quando o componente ganha uma capacidade, as portas que já sofriam da falta dela
  entram no mesmo commit** — senão a correção fica onde alguém reclamou e falta onde
  ninguém reclamou ainda.

- **A FOLHA DA SESSÃO VIROU COMPONENTE — e o modal do balcão tinha QUATRO dos doze campos**
  (set/2026; a cliente, abrindo Prontuário → Nova sessão: *"esse modal aí não está
  condizente com o do atendimento médico/enfermagem. Não queremos esse tipo de modal/box
  de atendimento"*). Havia DOIS desenhos para o mesmo ato: a folha única do Consultório
  (mockup 01, aprovado) e um formulário de duas colunas na Recepção, com o mapa corporal
  aberto num painel FIXO de 530 px. O segundo tinha ficado para trás nas parcelas 73, 75,
  76 e 77: história da doença atual, exame físico, hipótese, CID, plano, retorno,
  encaminhamento e os campos personalizados não existiam lá.
  ⚠️ **Ele PRESERVAVA os oito na gravação (a regra do lugar 6), e foi isso que escondeu a
  divergência**: nada se perdia, nada falhava, e a única forma de notar era abrir as duas
  telas lado a lado — que é o que a cliente fez. **Preservar o que não se edita evita o
  estrago e não corrige o desenho**; a pergunta que faltou, a cada campo novo, era *"a
  outra porta de escrita mostra isto?"*.
  ⚠️ **A extração foi por HERANÇA, e a razão não é gosto**: `AtendimentoViewModel :
  FolhaDaSessaoViewModel`. Compor obrigaria a trocar ~40 bindings da tela mais crítica do
  módulo clínico (`{Binding TextoEvolucao}` → `{Binding Folha.TextoEvolucao}`), e binding
  que deixa de casar **não quebra build nem teste** — o campo simplesmente para de
  aparecer. Herdar preservou todos eles; o que muda entre as portas entra por gancho
  (`PacienteId`, `AgendamentoDaSessao`, `AtendimentoDaSessao`, `ProfissionalDaSessao`,
  `ModalidadeDaSessaoAsync`, `DepoisDeSalvarAsync`) e, no XAML, por propriedade de
  dependência (`Contexto`, `Ferramentas`) — nunca por cópia.
  ⚠️ **`partial void On<X>Changed` é gerado NA BASE, e a derivada não pode implementá-lo.**
  `OnSemPacienteChanged` virou o gancho `AoMudarPresencaDePaciente`; sem ele a tela de
  Atendimento pararia de reavaliar "Emitir documento" e "Imprimir a sessão" ao trocar de
  paciente — a parcela 41 pela porta de trás. O mesmo vale para o `PodeAnexar` da janela,
  que passou a escutar o `PropertyChanged`.
  ⚠️ **A janela nova ganhou o que só existe FORA do posto, e cada peça tem razão**: *quem
  atendeu* (no Consultório é sempre quem fez login; no balcão a sessão é de outra pessoa, e
  o repasse e a auditoria leem essa coluna) e os *anexos* (que o balcão faz desde a parcela
  2 — tirá-los seria tirar capacidade de quem a usa). E **Salvar não fecha**, como na folha
  do posto: é isso que destrava o anexo numa sessão nova, porque o arquivo se prende à
  evolução e antes dela não há a que se prender.
  ⚠️ **Sem snackbar, a gravação ficava MUDA.** O host do snackbar mora na `ShellWindow`,
  atrás da janela modal, e a base só escrevia `Mensagem` quando a EVA estava pela metade —
  então o Salvar da janela não dizia nada. Gravação que não responde é o que faz a pessoa
  clicar de novo (o incidente dos três encaixes em 71 segundos, parcela 65): quando não há
  snackbar, a confirmação vai para a mensagem inline. **Ao tornar opcional uma dependência
  de FEEDBACK, procure o que ela dizia e escreva onde a nova porta lê.**
  ⚠️ **O combo lista só a equipe ATIVA — e o profissional que saiu da clínica sumia da
  sessão.** Abrir uma sessão antiga para corrigir uma vírgula deixava o campo em branco, e
  o Salvar gravaria `ProfissionalId = null`. É "quem não edita, PRESERVA" na variante em
  que o campo ESTÁ na tela e o VALOR é que não está na lista; o defeito vinha do modal
  antigo e foi corrigido na mudança (`_profissionalOriginal` como caminho de baixo).
  ⚠️ **A checagem de comando não seguia a classe BASE**, e acusou `SalvarCommand` — que a
  folha declara e a tela herda — como "o botão aparece e o clique não faz nada". Checagem
  que reclama do que está certo é checagem que alguém desliga: ela passou a seguir a
  herança, com autoteste que reprova quando a busca da base quebra.
  ⚠️ E `BooleanToVisibilityConverter` **ignora o `ConverterParameter`**: o "Salve a sessão
  antes de anexar" nasceu com `ConverterParameter=Inverso` e apareceria exatamente ao
  contrário — visível quando já dá para anexar. Inversão de visibilidade é `DataTrigger`,
  como o resto da suíte.
  ⚠️ **A FICHA DO ATENDIMENTO ESTAVA EM TRÊS CÓPIAS, E ELAS JÁ TINHAM DIVERGIDO EM TRÊS
  PONTOS** — cada um uma regra que só existia numa delas: a guarda de paciente que DIZ por
  que não dá (só a da enfermagem), a recusa de permissão que vira frase em vez de exceção
  (só uma), e **a guarda do rascunho não gravado** (só a do médico) — a mais cara, porque
  a tela da enfermagem TAMBÉM tem compositor e entregava ao paciente um papel sem o que a
  técnica acabara de escrever. Virou `FichaDoAtendimento` no shell, e o
  `temRascunhoNaoGravado` é parâmetro **obrigatório**: com valor padrão, a porta seguinte
  nasceria sem responder à pergunta e o defeito voltaria calado. De quebra, a emissão
  passou a deixar **trilha** (`ExportacaoClinica`) — a ficha é dado de saúde saindo para
  um arquivo no disco, e as três cópias não registravam nenhuma.
  ⚠️ **Botão que devolve INTENÇÃO só existe onde a dona sabe agir.** A janela é do shell e
  a de ANEXOS mora no módulo Clínico: nas portas que não a alcançam (a Recepção, o modal),
  oferecer o botão daria um clique que fecha a janela e não faz nada — a parcela 41
  construída de propósito. As CORREÇÕES são do shell e a janela as abre por dentro. **A
  pergunta que decide não é "a ação existe?", é "onde mora a janela de destino?"**
  ⚠️ E a releitura do diff pegou o de sempre: eu escrevi no cabeçalho que a janela tinha
  **"QUATRO portas — … e a tela da Enfermagem"** e não construí a quarta. Comentário que
  promete o que o código não faz é o defeito da parcela 67, cometido no arquivo que eu
  estava escrevendo para acabar com duplicata. São três, e a Enfermagem não é porta:
  ela lista `EvolucaoEnfermagem` e esta janela abre `Evolucao` — ids POR TABELA, e abrir
  uma pela outra mostraria a sessão de OUTRO paciente sem estourar nada.

- **A BUSCA DE PACIENTE VIROU COMPONENTE, E A TELA DEIXOU DE CONSULTAR AO ABRIR**
  (set/2026 — pedido da direção: *"nas telas onde precisa haver busca por pacientes, essa
  seja a tela inicial, parecida com a tela de atendimento; isso nos ajuda e minimiza o
  tempo de resposta entre sistema servidor"*).
  Escolher paciente tinha **TRÊS desenhos** na suíte, e dois deles o `README.md` proíbe: a
  composição centrada do Novo atendimento (mockup 05), lista de largura inteira → tela do
  item (Prontuário e Prescrições da Recepção) e **faixa lateral de 320 px** (Prescrições e
  Prescrição de infusão do Consultório). Três desenhos para a MESMA pergunta é o que faz a
  recepcionista achar que abriu outro programa.
  ⚠️ **O bloco SUBIU INTEIRO, e não foi reescrito** (`BuscaDePacienteView`, no shell): ele
  carrega meia dúzia de lições pagas uma a uma — a coluna de 760 px sem coluna estrela, o
  campo de 48, o `MaxHeight` de número INTEIRO de linhas, o `RodaDaPagina`, a ausência de
  `MinHeight`, o `IsSharedSizeScope`. Reescrever teria perdido metade delas em silêncio; e
  ao mover o teclado eu quase troquei o handler RICO do Novo atendimento (Enter com UM
  resultado escolhe; com vários só desce) pelo meu, mais pobre — **ao mover, liste o que a
  versão antiga FAZIA**.
  ⚠️ **E o Novo atendimento passou a usar o componente no mesmo commit.** Eu tinha
  COPIADO o bloco em vez de mover, e deixado a cópia lá: a segunda definição criada pelo
  próprio commit que existe para acabar com duplicata. A releitura do diff é que pegou.
  ⚠️ **`SemBuscaInicial` é o que entrega o pedido do servidor, e é OPT-IN.** Doze telas
  chamavam `BuscarAsync(imediato: true)` na abertura com o termo VAZIO — e com o termo
  vazio a consulta não filtra nada, cai no `OrderBy(Nome).Take(50)`: uma ida ao banco
  REMOTO, na abertura, para trazer ACELINO, ADAISE, ADAO. A regra que decide quem recebe é
  a mesma da `SugestaoInicial`: **a lista é a RESPOSTA da tela, ou o CAMINHO até ela?** Nas
  de listagem (Pacientes, Enfermagem) ela é a resposta, e abrir vazio seria trocar um
  defeito pelo oposto.
  ⚠️ **A DECISÃO foi para a Application** (`BuscaDePaciente.Modo`), e aqui isso vale mais
  que de costume: o `SeletorPacienteViewModel` é WPF e não compila no projeto de teste, e o
  que se decide não é uma frase — é **se a tela vai ao banco**. A matriz resolve TRÊS
  coisas de uma vez (se consulta, qual pílula acende, qual vazio aparece), e as três
  estavam espalhadas em booleanos soltos.
  ⚠️ **A ARMADILHA DO ESTADO NOVO, e ela é a lição que generaliza: ao acrescentar um
  estado a uma máquina de dois, todo booleano definido por NEGAÇÃO precisa ser relido.**
  `BuscandoPorTermo => !MostrandoSugestao` era verdade enquanto só havia sugestão e busca.
  Com o OCIOSO ele passou a dar verdadeiro numa tela recém-aberta — o `EstadoDaTela`
  ligava e escrevia *"Nenhum paciente encontrado"* por cima do convite, que é uma afirmação
  falsa sobre uma clínica de 2.238 fichas e justamente a que leva a cadastrar de novo quem
  já tem ficha (parcela 57). Pelo mesmo motivo `ListandoTodos` acenderia o chip "Todos os
  pacientes" sobre a tela em branco. Os dois passaram a derivar do MESMO `Modo` — derivar
  do mesmo lugar é o que impede uma pílula de acender na INTENÇÃO e a outra no RESULTADO.
  ⚠️ **A Central de documentos ficou de fora, e é decisão**: DUAS das nove folhas não
  exigem paciente (o recibo, que navega ao Caixa, e o fechamento do período). Fazer a tela
  abrir como busca as tornaria inalcançáveis — a regra 3 do bloco do faturamento ("não
  tire capacidade de quem a usava"). Ali o seletor é um CAMPO: a tela responde "que papel
  eu quero emitir", não "quem é o paciente".
  ⚠️ **MARGEM VERTICAL FIXA NUM CAMPO É O QUE CORTA O TEXTO** (2ª rodada — o cliente
  mandou o print com "lucas" decepado na base). O template do `CampoPesquisa` **ignora o
  `Padding`** e usa `Margin="8,8,12,8"` no `PART_ContentHost` — 16 px verticais TRAVADOS,
  medidos para o campo de 36 com a fonte de corpo. A conta já era apertada no caso padrão:
  36 − 2 de borda − 16 = **18 px úteis para uma linha de 14 que ocupa ~19**. Num campo mais
  alto com fonte maior (a busca de paciente é 48/18) ela aperta mais, e o que sobra é a
  metade de cima das letras.
  A margem vertical fixa é o defeito: **ela não acompanha nem a altura do campo nem o
  tamanho da fonte**. Zerada, quem centra é o `VerticalAlignment` e o texto cabe em
  qualquer combinação das duas; a HORIZONTAL fica, porque ela posiciona em relação à lupa e
  isso não muda com a altura. Corrigido nos DOIS templates — a busca de página e a pílula
  da busca global —, pela lição de sempre: **quando um defeito de template é corrigido,
  procure o irmão**. E o campo passou a ter `MinHeight`, nunca `Height`: altura travada com
  fonte maior é a mesma armadilha pelo outro lado.
  ⚠️ **E A TELA DE PACIENTES ERA A CONSULTA MAIS CARA DO SISTEMA** (mesma rodada — *"a tela
  de pacientes ainda carrega todos os pacientes"*). Eu a tinha deixado de fora com o
  argumento de que "numa tela de LISTAGEM a lista É a resposta". O argumento vale para o
  que a tela **OFERECE**, e não para o que ela faz sozinha: com `limite: null` e o termo
  vazio, abrir a tela trazia o cadastro INTEIRO — 2.284 fichas — de um banco remoto, sem
  ninguém ter pedido. A listagem continua a um clique ("Ver todos"), e **é justamente por
  ser cara que ela precisa ser PEDIDA**.
  Três coisas andaram junto, e as três são a mesma regra: no ocioso o **resumo** deixa de
  dizer "Nenhum paciente encontrado" (falso sobre 2.284 fichas), o **`EstadoDaTela`**
  desliga (`AlgoFoiPedido`) e a **região da lista some** — sem ela, a moldura ficaria vazia
  com o cabeçalho de colunas órfão em cima de nada. Um estado vazio por pergunta.
  ⚠️ **As irmãs foram conferidas e estavam certas**: a Enfermagem abre com a FILA DO DIA
  (só vai ao seletor com termo ou "ver todos") e a de Pacientes do Consultório traz quem
  já foi ATENDIDO, com teto — as duas respondem uma pergunta, não despejam um cadastro.
  **O que ficou pendente, com o motivo**: os três formulários que ainda abrem despejando o
  alfabeto (agendamento da Recepção, lista de espera e o agendamento do faturamento). Neles
  a lista é uma caixa de `MaxHeight="150"` SEMPRE visível, então `SemBuscaInicial` sozinho
  abriria um vão em branco no meio do formulário — o ganho de servidor ali custa também a
  visibilidade condicional em cada um. E a tela de Pacientes do FATURAMENTO tem o mesmo
  despejo, com o seletor PRÓPRIO daquele app (o débito permanente da Fase 4): portar o
  `SemBuscaInicial` para lá é a cópia que ainda falta.

- **A COLUNA DE ORIGEM FOI ALARGADA E A DE DESTINO NÃO — e o Postgres recusa a gravação
  INTEIRA** (set/2026 — a clínica clicou em "Imprimir esta sessão" e levou *"Não foi
  possível gravar. O banco respondeu: 22001: value too long for type character
  varying(1000)"*). A importação do Smart Clinic alargou os quatro textos longos de
  `Evolucao` para `text`, com a razão escrita ao lado (88 registros acima de 4.000
  caracteres, o maior com 11.221) — e `ItemDocumento.Detalhe`, que recebe **NOVE** desses
  campos concatenados no relatório de evolução, continuou em `varchar(1000)`. É **a cópia
  que ficou para trás** (parcelas 61, 75, 90) na variante que ainda não tinha aparecido:
  não entre dois design systems nem entre dois apps, mas **entre a tabela que GUARDA o
  dado e a que o COPIA**.
  ⚠️ **A regra que fica: ao alargar uma coluna, procure quem COPIA o conteúdo dela.** Todo
  `DocumentoClinico` é gravado por cópia — é isso que garante a segunda via idêntica —, e
  cópia por definição tem um destino com tamanho próprio. O `grep` que resolve é pelo nome
  do campo alargado dentro dos montadores.
  ⚠️ **E o teto se mede contra o que a origem PODE ter, não contra o que ela costuma ter.**
  A mesma linha estourava por um segundo lugar, sem nada a ver com o prontuário:
  `Quantidade` nasceu para *"1 caixa"* (60) e hoje recebe **quem assina** — o
  `Profissional.Rotulo`, que é o `Nome` de até 120, e na enfermagem `"{AutorNome}
  ({AutorConselho})"`, até 183. Nome comprido de brasileiro passa de 60 sem esforço. Ela
  foi para 200, que é o **teto provado** pela soma das origens, não um chute.
  ⚠️ **RECORTAR seria pior do que o erro.** É o argumento que o `Desenho` já carrega no
  mapeamento ao lado ("recortar o desenho silenciosamente tiraria marcações do papel sem
  ninguém saber quais") e o da própria importação: cortar registro clínico em silêncio
  entrega ao paciente uma ficha com a história da doença picada no meio de uma frase. A
  correção é `text`, como na origem.
  ⚠️ **NENHUM teste podia pegar, e a razão é estrutural: o SQLite IGNORA o tamanho
  declarado de uma coluna de texto.** Gravar passa aqui e falha na clínica — a mesma
  família do `xmin` e das datas com fuso (parcela 67), e *"só o Postgres pega" quer dizer
  "só a clínica pega"*. A saída é a daquela rodada: **medir o que o serviço PRODUZ contra
  o teto que o MODELO do EF declara** (`RelatorioCabeNaColunaTests`). Os dois lados são
  lidos do modelo — o destino e a ORIGEM —, senão o teste ficaria verde no commit em que
  alguém alargasse `Profissional.Nome` e recriasse o defeito. Os seis testes falham no
  código anterior; foi verificado, não presumido.
  ⚠️ **Alargar não pode só MUDAR O LUGAR da falha**: o `Detalhe` é desenhado numa CÉLULA
  de tabela, e a lição da parcela 68 vale inteira — *teste que prova que a gravação passa
  não prova que a FOLHA SAI*. O sétimo teste emite o relatório com os 11.221 caracteres do
  maior registro importado e **gera o PDF**.

- **A FOLHA ÚNICA: doze campos viraram um, e nenhuma coluna foi apagada** (set/2026 —
  a direção olhou a tela de atendimento e disse *"são coisas não tão profissionais que
  eram aceitáveis no começo do projeto"*, pedindo menos campos, texto livre e os botões
  de imprimir e salvar à vista; o mapa está em `docs/registro-do-atendimento.md` §21-26,
  e os desenhos em `docs/mockups/`). Foram desenhados CINCO mockups antes de uma linha de
  WPF — o caminho da parcela 87 —, a direção aprovou o **01 · Folha única**, e as sete
  telas do Consultório saíram na mesma língua.
  ⚠️ **Reduzir a TELA nunca é reduzir o REGISTRO.** Os doze campos da sessão continuam no
  banco (guarda de 20 anos), continuam saindo no relatório do convênio separados por
  assunto e continuam editáveis — atrás da linha "Detalhar…" ao pé da folha, que
  **ANUNCIA quantos a sessão aberta já tem**. Sem esse selo, a sessão antiga com hipótese
  e conduta preenchidas sumiria de VISTA sem sumir do banco: o defeito recorrente do
  projeto cometido pela própria reforma que o corrige.
  ⚠️ **O detalhe é JANELA, não bloco recolhido**: bloco cresce com o dado e disputa altura
  com a folha, e filho ancorado que não cabe é DECEPADO (parcela 79, nesta mesma tela). É
  o mesmo gesto que a consulta da COFEN já usa desde a parcela 88 — e ela edita o MESMO
  ViewModel, sem gravar nada: quem grava é o Salvar de trás.
  ⚠️ **A promessa do mockup que o código NÃO cumpre virou o que ele faz.** O desenho dizia
  "salva sozinha a cada pausa"; cada gravação de evolução existente cria uma
  `VersaoEvolucao` (parcela 52), e salvar a cada pausa encheria o prontuário de dezenas de
  versões por sessão. O rodapé passou a dizer "Última gravação às 14h37". **Mockup
  aprovado não dispensa a regra da garantia aparente** — ele é o lugar mais fácil de
  cometê-la, porque a promessa já foi vista e aprovada por quem pediu.
  ⚠️ **O rail deixou de ser vigiado e passou a ser impossível de divergir.** Eram nove
  `ListBoxItem` escritos à mão no XAML ao lado da lista do C#; agora ele é montado de
  `ModuloClinico.RailDoPaciente()` (`SecoesDoPaciente` × `GruposDoPaciente`, casadas por
  posição). A **checagem 38** mudou de "compare as duas listas" para "RECUSE a segunda
  lista", com autoteste nos dois sentidos: quando dá para trocar o contrato, trocar o
  contrato vence vigiar (a lição do índice→nome da parcela 41, um nível acima).
  ⚠️ **A régua de chips tem de ser montada TAMBÉM no construtor.** `SecaoEscolhida` e
  `TipoAcompanhado` são inicializados em campo, e inicializador de campo **não emite
  PropertyChanged** — sem a chamada, a tela abre sem chip nenhum, com a folha funcionando
  e sem como trocar de seção. Nada falha; achado relendo o próprio diff.
  ⚠️ **Campo cuja escrita volta ao ViewModel a cada tecla precisa de guarda de
  reentrância**: reemitir a propriedade no meio da digitação devolve o valor à caixa e é
  como se perde a posição do cursor. O gancho só reemite quando o campo mudou POR FORA.
  ⚠️ **Onde o desenho aprovado NÃO foi seguido, ficou DECLARADO** (§25 do documento): o
  Histórico não funde as listas de sessão e enfermagem (ids são por tabela — um "cancelar"
  na linha errada cancelaria o registro de outra pessoa —, e a lista rica tem busca no
  texto, anexos e correções que a genérica não tem); "Salvar e finalizar" continua sendo
  dois atos (finalizar avisa o balcão que a sala vagou, e todo Salvar mandaria o recado
  errado); e os chips de Medidas e Avaliações não trazem contagem. **Desvio de mockup
  aprovado se escreve, não se comete em silêncio.**

- **A FAIXA LATERAL DE RELEITURA VIROU ABA — e o rótulo saiu de dentro do texto** (set/2026,
  a 9ª reprovação do cliente: *"essas tabelas laterais não me agradam! Seria melhor criar
  uma aba para elas e deixá-las profissionais"*, com o print das duas telas de atendimento
  já redesenhadas). As duas telas eram DUAS COLUNAS — escrever à esquerda, reler numa faixa
  de 320 px à direita —, e a faixa não cabia no que precisava dizer: a sessão passada saía
  em seis frases cortadas em pontos diferentes, com o rótulo DENTRO do texto
  (`"Queixa: lombalgia há três mes…"`), e a linha do tempo da enfermagem vivia num vão em
  que o estado vazio ocupava quase toda a altura.
  A régua de leiaute do `README.md` responde sozinha: **quantas perguntas esta tela
  responde?** Três — o que eu escrevo agora, o que veio antes, o que o outro lado registrou
  —, e pergunta a mais é **ABA**, não caixa menor. É a lição da central de documentos
  (parcela 82) outra vez: **quando o cliente reprova uma tela, a correção começa pelas três
  perguntas da regra de leiaute, não pelo tamanho das peças.**
  ⚠️ **A barra de ações, os avisos e o RODAPÉ ficam FORA das abas.** Trocar de aba não pode
  esconder o botão que grava nem a mensagem que ele escreve — botão que some quando alguém
  vai reler a sessão passada é a gravação que não acontece.
  ⚠️ **O que a coluna aberta dava — reler ENQUANTO se escreve — não se perde por inteiro.**
  A folha ganhou uma LINHA quieta (`ResumoSessaoAnterior.ContextoDaUltima`): data, par da
  EVA e retorno sugerido da última sessão. É a resposta para "por que este paciente está
  aqui hoje", e ela não pode custar um clique; o resto está na aba ao lado. **Ao trocar um
  painel aberto por uma aba, pergunte o que dali NÃO pode custar um clique** — sem essa
  linha, a troca teria custado justamente o campo que a parcela 77 existiu para pôr na tela.
  ⚠️ **RÓTULO DENTRO DA FRASE não é rótulo, é texto.** `ResumoSessaoAnterior` devolvia
  `"Conduta: agulhamento lombar"` pronto; agora devolve o par `Rotulo`/`Valor`, e a aba
  desenha uma COLUNA de rótulos alinhada (`SharedSizeGroup`) com o valor ao lado, que é como
  se lê um prontuário no papel. A data também virou coluna compartilhada: sem isso, a linha
  com "EVA não medida" fica mais larga que a com "EVA 7 → 4" e a régua sai em escada.
  ⚠️ **E a separação do rótulo REVELOU um defeito de duas parcelas atrás.** `RepetirUltima`
  comparava com `"—"` — o sentinela que o modelo deixou de produzir na parcela 77, quando o
  vazio passou a ser `string.Empty`: a comparação era sempre verdadeira, e o que ia para o
  campo Conduta era o texto JÁ ROTULADO, gravado assim no prontuário e impresso assim no
  relatório do convênio (no segundo clique, "Conduta: Conduta: …"). **Nada estourava**, e
  só apareceu porque o compilador passou a exigir que alguém dissesse QUAL campo estava
  sendo lido. A lição de método: **quando um modelo troca o formato de um valor, procure
  quem o COMPARA** — o `grep` é pelo sentinela antigo, não pelo tipo.
  ⚠️ Junto veio a segunda metade: sem conduta escrita, o botão dizia *"trazida para a tela"*
  tendo trazido NADA — e depois da folha única esse é o caso NORMAL. Frase de sucesso sobre
  uma tela que não mudou é a garantia aparente numa linha: a pessoa confere, não vê
  diferença, e conclui que o botão está quebrado.
  ⚠️ **Cinco sessões anteriores, não três**: o três era a altura da coluna de 350 px, não uma
  decisão clínica. E não é "todas" de propósito — o prontuário inteiro, com busca no texto,
  é a seção Histórico, e duas telas respondendo à mesma pergunta é o que faz alguém procurar
  a diferença que não existe.

- **"Por que numa tela dá e na outra não?" — quando as duas telas são o MESMO código, a
  variável é o DADO** (set/2026, a pergunta que acompanhou o 22001 acima: *"Prontuário →
  Ver prontuário → Imprimir esta sessão está retornando erro, mas Pacientes → Histórico de
  sessões → Abrir sessão → imprimir esta sessão eu consigo, por quê?"*). Havia exatamente
  **um** botão "Imprimir esta sessão" em toda a suíte (`SessaoDoProntuarioWindow`), um
  único `FichaDoAtendimento.EmitirAsync` e três portas que só diferem no `ofereceAnexos`.
  As duas telas são a mesma linha de código: o que mudava era **qual sessão** estava
  aberta — a que imprimiu tinha texto curto, a que falhou era uma importada do sistema
  antigo.
  ⚠️ A tentação é procurar a diferença ENTRE AS TELAS, e ela consome a rodada inteira. O
  `grep` pelo rótulo do botão responde em segundos: **um único chamador significa que a
  assimetria está nos dados, não no caminho** — e aí a pergunta certa deixa de ser "o que
  esta tela faz de diferente" e passa a ser "o que este REGISTRO tem que o outro não tem".

- **ATENDER É UM CLIQUE, E FINALIZAR ENCERRA O ASSUNTO — a simplificação do fluxo do
  médico** (parcela 95; o mapa está em `docs/guia-no-agendamento.md` §4). A cliente
  descreveu o que quer em uma linha — *"secretária agenda → cai na agenda do médico
  respectivo → ele clica em atender e faz o atendimento"* — e mandou o print do Smart
  Clinic, onde a linha da agenda tem o status ali, a coluna PRONTUÁRIO dizendo "Pendente"
  e o botão ATENDER. O que o print fecha, e é o que decidiu a parcela: **não existe um
  passo de conclusão pelo balcão**.
  As três emendas, e o que cada uma paga:
  **(a) Marcar EXIGE quem vai atender** (`AgendaService.AgendarAsync`). A primeira seta do
  fluxo só existe com esse campo: "Meu dia" e "Minha semana" filtram por `ProfissionalId`,
  e o repasse lê quem atendeu do AGENDAMENTO. Até aqui a tela AVISAVA o custo e deixava
  salvar (parcela 69) — com o horário virando a agenda do médico, avisar deixou de bastar.
  ⚠️ **O ENCAIXE continua passando**, e é a assimetria deliberada: encaixe é o paciente que
  está no balcão AGORA, e recusá-lo travaria quem chegou sem hora marcada quando a
  recepcionista ainda não sabe quem vai pegar. É onde o aviso funciona — com a pessoa na
  frente.
  **(b) "Atender" carimba a ENTRADA NA SALA.** Ele só abria o prontuário: o cartão ficava
  parado em AGUARDANDO enquanto o paciente estava na cadeira, o quadro do balcão mentia
  sobre quem estava ocupado e a espera continuava correndo depois de a pessoa ter entrado.
  Quem clica em Atender está com ela na frente — o carimbo é FATO. ⚠️ Falhar o carimbo
  **não impede abrir o prontuário**: ler o registro de quem está na sala não pode depender
  de uma escrita de fila ter respondido; vira aviso, nunca silêncio.
  **(c) "Finalizar atendimento" CONCLUI** (carimba a presença e gera as guias). O
  argumento da parcela 61 — *concluir são quatro fatos e três são do balcão* — continua
  verdadeiro para pacote, insumo e caixa, e **não se sustentava para a GUIA**, que é o fato
  do atendimento. A medição fechou a decisão: no caso mais comum (convênio, sem pacote, sem
  insumo) `RegistroAtendimento.TemDecisao` é **falso**, e o Concluir do balcão não abria
  janela nenhuma — era um clique para carimbar o que o médico já sabia.
  ⚠️ **O que o balcão continua fazendo é o DINHEIRO, e ele precisou de PORTA**: a raia
  FINALIZADO ganhou "Fechar sessão", que abre a MESMA janela de fechamento. Nada foi
  reimplementado — `FechamentoSessaoService.GarantirAtendimentoAsync` já sabia reaproveitar
  a presença confirmada desde a parcela 65. Sem essa porta, o pacote da sessão que acabou
  de acontecer não teria por onde debitar: alerta sem porta é pior que alerta nenhum.
  ⚠️ **E a pendência SOME quando é resolvida** (`AtendimentosComFechamentoAsync`, em LOTE —
  uma ida por cartão daria trinta a cada batida do relógio do quadro). Botão que fica aceso
  para sempre em toda sessão do dia ensina a ignorar a coluna, que é a lição da parcela 68.
  ⚠️ **A ORDEM dos três passos é amarra, não estilo**: grava a sessão → encerra → conclui.
  Falhar o passo 3 não desfaz os dois primeiros, e cada desfecho tem frase própria — a
  exceção pode vir do `Exigir`, do carimbo ou da conclusão, e a diferença entre elas é o
  que a pessoa precisa fazer em seguida. E `EncerrarAtendimentoAsync` recusa horário que já
  saiu de `Agendado`, então concluir antes deixaria a sessão sem o carimbo de fim: há teste
  fixando as duas pontas.
  ⚠️ **Concluir é IRREVERSÍVEL por ali**: com a presença carimbada, desfazer é ESTORNO, e
  por isso o "Reabrir atendimento" SOME da tela (`PodeReabrir`) — botão que só existe para
  levar recusa é o defeito da parcela 41.
  ⚠️ **A permissão foi junto porque o ATO mudou de lugar**: `PerfilAcesso.Profissional`
  passou a receber `LancarAtendimento` — é o bit que nomeia "criar o atendimento e, com
  ele, as guias". É a lição da parcela 69 pelo outro lado (lá o bit existia e não guardava
  a porta nova; aqui a porta nasceu e o bit precisava vir junto), e é ACRÉSCIMO: ninguém
  que atendia ontem perde nada, e a direção continua podendo tirá-lo de uma pessoa
  específica em Acessos. `EditarAgenda` **não** veio junto — marcar horário de terceiros
  continua sendo do balcão, e há teste que falha se alguém o incluir de raspão.
  ⚠️ **O CONVÊNIO "a definir" é perguntado ANTES de concluir**, pelo `VinculoDeConvenio` do
  shell — o MESMO ponto único do Concluir da Fila. Com a chave "guia no agendamento"
  desligada (o caso da clínica), é o clique do médico que gera a guia, e a importação
  deixou 2.021 fichas sem convênio: sem a pergunta, a recusa da parcela 92 chegaria por
  exceção na cara de quem não é dono do cadastro. Desistir não é erro — a sessão fica
  gravada e encerrada, e a fila conclui depois.
  ⚠️ **O custo medido, e ele foi pago**: a recusa de (a) quebrou **129 testes** de uma vez —
  `AgendarAsync` é o construtor de horário de todo o projeto, e os cenários criavam
  horários sem dono porque a regra permitia. Dois deles existiam para provar o contrário
  (`Agendar_SemProfissionalNemSala_NuncaEhBarrado`, `Folha_do_dia_aguenta_agendamento_sem_
  profissional_nem_sala`) e foram REESCRITOS, não remendados: o primeiro virou o par
  recusa/encaixe da regra nova, o segundo passou a criar o horário como encaixe — porque a
  folha continua tendo de aguentar os encaixes e os horários importados sem profissional.
  **Quando uma regra nova quebra um teste que existia para provar o contrário, o teste é
  parte da mudança, não um obstáculo a contornar.**
  ⚠️ E o efeito colateral que a correção em massa produziu vale a lição: com todos os
  cenários passando a usar o MESMO profissional, dois horários no mesmo instante viraram
  **choque de verdade**. Semear um dado no fixture muda o mundo dos vizinhos — o helper do
  fechamento passou a andar uma hora por chamada, e os testes de contagem de profissional
  ficaram sem a semente.

- **A SIDEBAR DO CONSULTÓRIO NUNCA TINHA USADO AS SUB-ABAS DO SHELL** (parcela 95, 2ª
  parte — a direção pediu para *"organizar abas, sub-abas e opções dentro do módulo
  clínico"*). Doze itens soltos, e o mecanismo da parcela 55 (`ItemMenuModulo.Abas`) usado
  pela Recepção e pelo Financeiro e nunca por este módulo. Viraram seis: Minha agenda
  (Hoje · Semana · Sem evolução), Enfermagem (Sala de infusão · Passagens), Pacientes (Em
  tratamento · Registros · Exames), Prescrições (Receitas e documentos · Infusão), Meus
  números, Ajuda. A régua foi a de sempre: **um item por PERGUNTA, as telas de sempre como
  abas, nenhuma chave de navegação mudando** — a sub-tela continua sendo item e o pai a
  esconde do menu.
  ⚠️ **Composto com o mesmo rótulo em dois módulos precisa da MESMA chave** — senão o
  Gerente Geral, que carrega os dois, mostra "Pacientes" e "Pacientes" lado a lado (a
  duplicata da checagem 45, que só funde por CHAVE). As chaves de grupo `pacientes` e
  `receituario` subiram para `ChavesSuite`, e no Gerente vence o composto da Recepção,
  carregada antes, cujas abas contêm as do Consultório. Onde o rótulo não podia ser o
  mesmo, mudou: "Minha agenda", porque a Recepção publica "Agenda".
  ⚠️ **`Requer` do composto é o bit MAIS FROUXO, e quem filtra são as abas.** `Pode` com
  bits somados é um E: `ChecarPrescricao | RegistrarEvolucaoEnfermagem` no item
  "Enfermagem" deixaria de fora a técnica que só checa. Com `VerAgenda` o item entra para
  todos e as abas entram só para quem tem o bit de cada uma; sem aba nenhuma o composto
  some sozinho — é assim que o médico não o vê.
  ⚠️ **A abertura do app sem `Inicial` é o PRIMEIRO item declarado no primeiro grupo** —
  o composto "Minha agenda" foi declarado antes do "Meu dia" de propósito, e a ordem de
  declaração dentro do grupo é leiaute.
  ⚠️ **O rail da tela do paciente esconde a seção de ESCRITA do outro lado** (a enfermeira
  não vê o S-O-A-P, o médico não vê a passagem; quem tem os dois vê as duas). A decisão é
  `PerfisAcesso.SecoesDeEscritaDoPosto`, no domínio e com teste. E a linha escondida
  **COLAPSA — nunca sai da lista**: o `SelectedIndex` do rail é o índice da lista e a régua
  do TabControl, e filtrar a vista deslocaria os índices para o clique abrir a seção do
  vizinho, sem erro nenhum (a regressão da checagem 38). O índice a esconder sai do MESMO
  mapa da navegação (`AbaDe`, que virou público), e não de um rótulo escrito à mão.
  **O que NÃO foi mexido, e é decisão**: as abas internas do Atendimento (A sessão de hoje ·
  Sessões anteriores · Enfermagem e infusões) vêm de mockup aprovado (parcelas 74-77) e
  não havia pedido específico — reorganizar leiaute aprovado sem pedido é o caminho da
  sétima reprovação.

- **O "MEU DIA" DO MÉDICO VOLTOU A SER LISTA — e não é a lista que a parcela 38
  aposentou** (parcela 95, 3ª parte; a direção: *"quanto mais simples, melhor"*). O quadro
  de cinco raias foi feito por PARIDADE com a fila do balcão (parcela 87), e as raias
  respondem à pergunta do BALCÃO, que administra vinte pessoas em cinco estados. O médico
  tem oito horários e uma pergunta: *quem é o próximo e o que eu já escrevi*. O print do
  Smart Clinic que a cliente mandou é exatamente isso — uma linha por horário, o status
  ali, a coluna PRONTUÁRIO e o botão ATENDER.
  ⚠️ **A lista corrida da parcela 38 não respondia "quem posso chamar agora" porque não
  tinha a coluna de STATUS com a hora do fato** ("No local · chegou às 14:40 · espera 12
  min"). É ela que substitui as raias, e ela sai dos MESMOS carimbos que a fila do balcão
  lê (`SessaoDoDia` ganhou `ChegadaEm`/`InicioAtendimentoEm`/`FimAtendimentoEm`). Nenhuma
  sincronização mudou: os dois leem a mesma tabela.
  O que a lista NÃO perdeu: cancelado e falta continuam na lista, APAGADOS (a regra da
  folha do dia); as transições (chamar, desfazer, voltar) continuam na linha, na etapa em
  que servem; "Chamar próximo" continua no cabeçalho. O que perdeu foi o ARRASTO, que numa
  lista não tem para onde ir — e o code-behind de 140 linhas foi junto.
  ⚠️ **"Iniciar atendimento" na barra da tela do paciente NÃO era redundante**, apesar de o
  Atender do Meu dia carimbar a entrada desde a 1ª parte: a enfermagem chega ao paciente
  pela tela da Enfermagem (shell), que não carimba — a barra é o caminho dela. Eu tinha
  anunciado a remoção; a conferência de quem CHEGA à barra por outra porta é que a salvou.
  **Antes de tirar um botão "redundante", liste as portas que chegam à tela sem passar
  pelo botão que o tornou redundante.**
  Junto: os rótulos com barra da Recepção ("Recepção / Check-in" → "Fila do dia";
  "Pacotes / Sessões" → "Pacotes", nos DOIS módulos que publicam a chave). Item que precisa
  de dois nomes é item que ainda não decidiu o que é.

- **O RAIL DA TELA DO PACIENTE: dez seções viraram oito, e a checagem 38 decidiu COMO**
  (parcela 95, 4ª parte). "Evolução da dor", "Medidas" e "Avaliações" eram três linhas do
  rail para uma pergunta ("como está indo"); viraram a seção "Acompanhamento", com as três
  como abas internas — e o grupo de três itens virou um item do grupo Paciente. As três
  chaves de navegação continuam valendo (`AbaDe` leva à seção, `SubAbaDe` à aba de dentro).
  ⚠️ **As abas de dentro moram numa View PRÓPRIA (`AcompanhamentoView`), não em `TabItem`
  aninhados no workspace.** A checagem 38 conta os `<TabItem` do XAML do workspace contra
  as seções declaradas no C# — uma por seção — e um `TabControl` aninhado ali inflaria a
  contagem para 11 contra 8. A saída não foi afrouxar a checagem: foi compor um nível, que
  é o nível que a checagem de rolagem também segue. **Quando uma rede reprova um desenho
  legítimo, a primeira pergunta é se há uma composição que a rede já entende.**

- **"QUANTO MAIS SIMPLES, MELHOR" — o Novo atendimento em duas abas e o cartão da fila
  com três selos** (set/2026; os dois desenhos foram aprovados em mockup ANTES do WPF —
  `docs/mockups/balcao-dois-desenhos.html` — com uma emenda da cliente: a série sai).
  ⚠️ **Pergunta que muda campos de lugar não é campo, é ABA.** O rádio QUANDO (parcela 70)
  fazia seis campos aparecerem e sumirem no meio do formulário. Virou a escolha da aba do
  item "Atendimento" (Lançar · Marcar), sobre o **MESMO** `NovoAtendimentoViewModel` com o
  modo fixado por quem monta a aba (`FixarModo`, chamado em `CriarTela`): dois ViewModels
  divergiriam na primeira correção da prévia, da elegibilidade ou da gravação. A agenda
  navega para a chave da aba Marcar, e a ponte `PreenchimentoNovoAtendimento` passou a
  ser consumida **só pela aba do modo do pedido** (`ConsumirPara`) — a aba Lançar, se
  carregasse primeiro, engoliria o pedido da outra.
  ⚠️ **A prévia da guia abre RECOLHIDA, e só onde a guia nasce neste clique**
  (`MostrarPrevia`): sempre no Lançar; no Marcar só com a chave "guia no agendamento"
  ligada. Com a chave desligada, desenhar um documento que o clique não cria é prometer o
  que a tela não faz — a nota diz onde a guia nasce (no Finalizar do profissional,
  parcela 95). O documento aprovado continua igual, a um clique de "Ver a guia".
  ⚠️ **A SÉRIE ("N sessões a cada X dias") saiu dos DOIS formulários da Recepção** por
  decisão da cliente: "se o paciente precisar voltar, a recepcionista marca uma agenda".
  O motor `AgendaService.AgendarSerieAsync` fica sem porta, de propósito e escrito — as
  séries já marcadas continuam com o "cancelar o resto da série" na janela do horário.
  ⚠️ **Oito selos num cartão são uma tela dentro do cartão.** `SelosDaFila` (Application,
  pura, testada) decide o que MERECE selo, por ordem FIXA — o que impede (termo), o que
  cobra agora (guia; pacote só na penúltima/última/esgotado), o estado da coluna (atraso
  só em AGUARDANDO, encerrado só em EM ATENDIMENTO) — e corta em TRÊS. **Selo a menos não
  é dado a menos**: o que saiu ganhou leitor no mesmo commit — o ✓ ao lado da hora
  (confirmação), a linha de contexto (pacote N/M, encaixe) e a dica do cartão (a
  resposta da rodada por extenso, o "retorno do 2º código" legado). Regra que decide o
  que o balcão lê de relance mora onde o `dotnet test` alcança.

- **"UMA FOLHA, DOIS LADOS" — o atendimento remodelado, e o que a extração de componente
  cobra** (set/2026; mockup aprovado em `docs/mockups/atendimento-uma-folha-dois-lados.html`,
  mapa em `docs/registro-do-atendimento.md` §28). O crachá virou duas linhas e a BARRA VERDE
  de "atendimento em curso" morreu (a situação é uma pílula com os dois links que só
  existem quando há o que fazer); a folha do médico ficou com UMA barra (ferramentas na
  tira, **Imprimir · Salvar · Finalizar** no rodapé, o Finalizar alcançado pelo comando do
  workspace via `RelativeSource`); as quatro linhas de contexto viraram uma
  (`ContextoDaFolha`, Application) com os sinais vitais que a ENFERMAGEM aferiu hoje; o
  rail foi de 8 para 7 ("Anamnese" é a 2ª aba de Paciente — `PacienteView`, a mesma
  composição do `AcompanhamentoView` que a checagem 38 já entende); a PA virou UM campo
  (`PressaoArterialTexto`); e o compositor e a lista da passagem de enfermagem subiram
  para o shell (`PassagemDeEnfermagemView`, `PassagensDeEnfermagemView`) — um XAML cada,
  três portas: a janela da sala, a seção do Consultório e a tela Enfermagem, que deixou
  de abrir a janela modal e escreve NA TELA, nas mesmas três abas da seção.
  ⚠️ **Quem hospeda um compositor LÊ o que ele grava, e precisa ser AVISADO.** O plano de
  cuidados de hoje e a última aferição do contexto são lidos pelo hospedeiro; a consulta
  de enfermagem PRESCREVE os cuidados; e ninguém avisava que a passagem tinha sido gravada
  — a enfermeira registrava a consulta e via o plano de ANTES dela, sem nada falhar. O
  compositor ganhou `Gravou` (registrar, corrigir, cancelar), disparado DEPOIS da frase
  de êxito, porque o recarregar só escreve mensagem quando FALHA (parcela 68).
  ⚠️ **Edição por âncora textual acha a PRIMEIRA ocorrência.** A troca do compositor da
  janela da sala foi ancorada em `<Border Grid.Column="0"`, e a janela tinha DOIS: o da
  mensagem do rodapé e o do miolo. O compositor caiu no rodapé, a mensagem sumiu, o
  antigo continuou no miolo — XML bem-formado, `compilar-sombra` verde, `verificar-suite`
  verde. Quem pegou foi o `git diff` do arquivo, lido antes de seguir. **Depois de um
  script de edição, leia o diff do arquivo inteiro, não o trecho que o script mostrou.**
  ⚠️ **Setter que NORMALIZA o valor num binding de tecla só escreve o que mudou.** O campo
  "120/80" grava o par `Sistolica`/`Diastolica`; reemitir os dois a cada tecla devolveria
  "120" ao campo no instante em que a pessoa digitou "120/" (o WPF segura a volta durante
  a própria atualização, mas a guarda é o que não depende disso).
  ⚠️ **Ao subir um bloco para o shell, procure a FRASE que ele já dizia no hospedeiro.** O
  compositor escreve o `Contexto` na primeira linha; a janela da sala também o escrevia
  no cabeçalho — a mesma frase lida duas vezes. Saiu do cabeçalho.
  ⚠️ **Três abas iguais nas duas portas é decisão, não preguiça**: "A passagem de hoje ·
  Passagens do paciente · Prontuário do paciente" na tela Enfermagem são as da seção do
  Consultório, na mesma ordem — quem trabalha nos dois apps encontra a mesma tela. E o
  prontuário da tela deixou de mostrar o chip Enfermagem (as passagens têm aba própria;
  duas respostas para a mesma pergunta, parcela 37), com a lista de naturezas vinda do
  ENUM, não de uma lista à mão.

  ⚠️ **`<Style.Triggers>` ENTRE dois `<Setter>` é `MC3088`, e só o CI vê** (o `build` do
  PR ficou vermelho no crachá da parcela 95, e ninguém notou por uma rodada inteira).
  Elemento de PROPRIEDADE (`<Tipo.Propriedade>`) vale antes ou depois de TODO o conteúdo
  — nunca no meio. O gatilho foi posto "perto do comentário que o explica", no meio da
  lista de `Setter`, e o XML é bem-formado, o `compilar-sombra` não lê o corpo do XAML e o
  C# compila. Virou a **checagem 46**, com a regra EXATA do compilador (conteúdo dos dois
  lados) e não a aproximação "propriedade depois de conteúdo", que acusaria 235
  `Style.Triggers` legítimos — medido antes de ligar: UMA ocorrência no repositório, que
  era o defeito; autotestada no caso real e nas três formas legítimas. **Quando o CI de
  um PR fica vermelho, a primeira pergunta é "qual rede local deveria ter visto isto?"** —
  e se a resposta é nenhuma, a correção vem com a checagem.

  ⚠️ **`TargetName` de gatilho resolve no template DONO do gatilho, não no arquivo**
  (out/2026 — o build do Windows da rodada de UI da Recepção caiu com `MC4111`). O
  gatilho "entrada direta sem seta" foi escrito no template do ITEM do menu, e a `Seta`
  mora no template da CATEGORIA, vinte linhas abaixo no mesmo dicionário. O nome existia
  no arquivo, e foi isso que o fez parecer certo. Virou a **checagem 49** (o nome tem de
  estar no CONTEÚDO do mesmo template, sem descer em template aninhado), medida antes de
  ligar: uma ocorrência, que era o defeito.

- **A CONFERÊNCIA PEDIDA PELA DIREÇÃO SOBRE A AGENDA — três defeitos com 2261 testes
  verdes, e os três da mesma família** (set/2026: *"verifique se nossa agenda tem falhas
  que você não viu quando construiu"*). Nenhum quebrava build, teste ou rede; os três
  moram no vão entre o shell e a tela — o `TelaComAbas` monta cada aba UMA vez e a guarda,
  e é nesse "uma vez" que os três nasceram.
  ⚠️ **"Retornos a marcar" abria VAZIA dizendo "Nenhum retorno a marcar".** A ViewModel não
  declarava `ICarregarAoAbrir` nem disparava a carga no construtor, e o shell só carrega
  quem declara o contrato. É a lição da parcela 47 — escrita neste arquivo — cometida na
  tela seguinte, e na variante pior: o `EstadoDaTela` afirmava a ausência. **Tela nova da
  suíte que busca ao abrir declara `ICarregarAoAbrir`, e a conferência é `grep` do nome
  da ViewModel em `CriarTela` contra a lista de quem implementa o contrato.**
  ⚠️ **O segundo "Marcar horário" vindo dos Retornos marcava o paciente ERRADO.** O pedido
  da ponte (`PreenchimentoNovoAtendimento`) era consumido só no `CarregarAsync` da aba
  Marcar, que roda quando ela é montada. Retornos e Marcar são abas do MESMO item: no
  segundo clique o shell só trocava de aba, ninguém consumia o pedido, a tela mostrava o
  paciente do retorno anterior e o pedido ficava ÓRFÃO — pré-preenchendo a abertura
  seguinte com o clique de antes, que é justamente o que o comentário da ponte diz existir
  para evitar. A grade já tinha a resposta certa para o pedido DELA (`ConsumirPedidoDeDia`
  no `AoEntrarEmCena`); a aba Marcar não. **Ponte consumida "na abertura" precisa ser
  consumida também na VOLTA à vista, quando a tela é aba** — e só na volta: na primeira
  exibição quem consome é o `CarregarAsync`, na ordem que a parcela 37 exige (equipe
  carregada, busca inicial remontada, e só então o paciente).
  ⚠️ **Voltar à Grade no modo SEMANA apagava a grade pelo tempo da leitura — e a deixava
  em branco se a leitura falhasse.** `Colunas.Clear()` vinha ANTES do `await` de
  `MontarSemanaAsync`. A batida do relógio excluía o modo semana exatamente por isso (o
  comentário dela diz "a grade piscaria"); a releitura ao voltar à aba, nova de set/2026,
  não excluía — e no `catch` silencioso nada era restaurado. A regra da parcela 62 vale
  para o **await escondido dentro de um método chamado entre o Clear e o Add**: a semana
  passou a ser montada em lista local e publicada numa passada síncrona.
  Junto, duas divergências entre as duas listas do dia: a do balcão deixava registrar
  chegada, chamada e entrada em QUALQUER dia navegado (o Meu dia já escondia — a fila corre
  HOJE, e os dois quadros passaram a seguir a mesma regra, com a guarda que FALA), e o
  botão "Debitar pacote" acendia pela metade larga (`EditarAgenda` OU `MovimentarFila`)
  enquanto o comando exige `EditarAgenda` estrito.
  **A lição de método**: a lista de conferência pega o que está NA LINHA; o que ela não
  alcança é o contrato entre a tela e quem a hospeda. Ao escrever uma aba, as três
  perguntas são: *quem a carrega na primeira vez? o que ela precisa reler ao VOLTAR? e o
  que ela promete consumir que só consome na montagem?*

- **A EVOLUÇÃO NASCE COM O NÚMERO DO ATENDIMENTO — o mesmo que a recepção gera**
  (set/2026, pedido da direção). No regime atual o médico ESCREVE a sessão no passo 1 do
  Finalizar e o atendimento só nasce no passo 3: a evolução ficava com `AtendimentoId`
  nulo para sempre, e nada ligava os dois depois. Nenhum leitor dependia disso ainda —
  era a variante "dado sem leitor" pelo avesso: um VÍNCULO sem escritor.
  ⚠️ **São DUAS metades, e uma sem a outra deixa um caso de fora.** (a) Quem NASCE amarra
  o que já foi escrito: `ConfirmarNucleoAsync` pendura as evoluções sem atendimento do
  horário no atendimento que está nascendo, no MESMO commit — pela navegação, porque o
  Id ainda não existe. (b) Quem GRAVA resolve pelo horário: `ProntuarioService.SalvarAsync`
  lê `AtendimentoDoHorarioAsync` (uma coluna) quando o chamador mandou nulo — a tela do
  médico guarda o atendimento de quando ABRIU, e ele pode ter nascido depois (o balcão
  concluiu, ou a chave "guia no agendamento" o criou na marcação).
  ⚠️ **O vínculo é pelo HORÁRIO, não pelo autor**: a evolução do colega que cobriu a
  sessão é amarrada igual ("todos atendem todos"). Sessão CANCELADA fica de fora — registro
  desdito não sustenta guia —, e há teste para as duas coisas. Três dos quatro testes
  REPROVAM no código anterior (o quarto é a guarda do que NÃO se amarra, e passa nos
  dois); foi verificado com o `git stash`, não presumido.

- **O DINHEIRO DO BALCÃO: o alerta que não tinha porta, a receita órfã e o preço que não
  chegava à decisão** (set/2026, item 3 da lista "o que falta para ficar profissional").
  Três buracos da mesma família — motor pronto, porta no lugar errado —, e nenhum falhava:
  build verde, 2321 testes verdes, três redes locais verdes.
  ⚠️ **(a) O alerta de dívida não tinha porta no app que o mostra.** O
  `ElegibilidadeService` avisa desde a parcela 27 que o paciente deve, e o argumento
  escrito ao lado é que *"o momento em que ele está na recepção é o único em que cobrar não
  custa nada — uma frase; depois custa telefonema, mensagem e constrangimento"*. A única
  porta para RECEBER ficava no Financeiro — outro app, de outra pessoa. É a parcela 48
  ("procure o AVISO que a tela dá e pergunte se a porta para resolvê-lo está no mesmo
  app") cobrada de novo, no aviso que virou dinheiro. `CobrancaDoPacienteWindow` mora no
  SHELL porque tem duas portas (Novo atendimento e ficha do paciente) e não grava dinheiro
  por conta própria: passa pelo `InadimplenciaService.ReceberAsync`, que delega ao
  `FinanceiroService` — quem grava dinheiro continua sendo um só, com a auditoria no mesmo
  `SaveChanges`.
  ⚠️ **A permissão é `EditarFinanceiro` OU `LancarAtendimento` OU `VenderPacote`
  (`ExigirAlgum`), e não um bit novo** — é o argumento do preço do particular (set/2026):
  receber a parcela vencida é a continuação do passo do CAIXA do Finalizar, que o balcão já
  faz; e o enum tem UM bit sobrando antes de virar `long` numa coluna de produção.
  ⚠️ **A FORMA de pagamento é escolhida na LINHA, nunca presumida.** Herdar a última usada
  gravaria PIX no dinheiro que o paciente acabou de pôr no balcão — e a divergência só
  apareceria no fechamento do caixa, quando ninguém mais lembra.
  ⚠️ **(b) O lançamento manual do Caixa era a única porta de dinheiro SEM paciente.** A
  receita entrava órfã: não ligava à sessão e a linha continuava na aba **Particulares** da
  Conciliação, cobrando um pagamento que já estava na gaveta — a pendência que não some é a
  que ensina a ignorar a lista (parcela 68). O campo é **só na ENTRADA** (numa saída, dono
  e sessão seriam invenção) e o combo de sessão abre em **"(não é de uma sessão)"**: a
  maioria dos recebimentos manuais é venda de produto ou acerto, e amarrar por padrão daria
  baixa numa sessão que ninguém pagou, em silêncio. A **sessão de HOJE fica de fora** — ela
  está sendo fechada agora pelo Finalizar, e oferecê-la daria dois caminhos para o mesmo
  dinheiro no mesmo minuto.
  ⚠️ **O seletor nasce com `SemBuscaInicial`**: com o campo vazio a busca não filtra nada e
  despejaria o começo do alfabeto de 2.238 fichas numa janela que quase nunca precisa de
  paciente.
  ⚠️ **(c) O preço do particular não chegava à DECISÃO.** A tabela é lida no Finalizar e na
  Conciliação desde set/2026 — os dois DEPOIS de a sessão acontecer — e a prévia do Novo
  atendimento, que é onde a recepcionista escolhe a modalidade com o paciente na frente,
  não a mostrava. É o MESMO `PrecoParticularService` dos outros dois leitores: dois
  leitores com duas regras proporiam dois números para a mesma sessão. **Sem preço
  cadastrado não se inventa valor** — a linha diz que falta cadastrar e aponta onde.
  ⚠️ **A leitura do preço é SEQUENCIAL dentro do mesmo escopo da prévia**, nunca
  `Task.WhenAll`: é o mesmo `DbContext`, e o SQLite dos testes esconde (parcela 74).
  ⚠️ **O que os testes prendem é o CIRCUITO, não a tela** (`CobrancaNoBalcaoTests`):
  receber pela porta do balcão APAGA o alerta, e amarrar o recebimento manual à sessão a
  TIRA da lista de sessões sem receita. Elo partido aqui não vira erro — vira alerta que
  nunca some e linha que nunca sai, indistinguível de "ainda não pagaram".

- **AS TRÊS PEQUENAS DA AGENDA — a data que não reconferia, as férias invisíveis e os três
  despejos do alfabeto** (set/2026, item 4 da lista). As três estavam na fila escrita da
  parcela 69, e as três têm a assinatura de sempre: nada falha.
  ⚠️ **(a) Trocar a DATA no formulário da agenda não reconferia a elegibilidade.**
  `ConferirAsync` recebe a data e mede tudo contra ela — carteirinha, cota, consulta a
  renovar, termo —, e só a troca de PACIENTE disparava a reconferência: escolher a pessoa
  hoje e marcar para o mês que vem deixava na tela o resultado de HOJE. **Aviso
  desatualizado é pior que aviso nenhum, porque ele AFIRMA** — uma carteirinha aparece
  válida porque ninguém perguntou de novo. O Novo atendimento já reconferia desde a parcela
  70; era a cópia do formulário que tinha ficado para trás.
  ⚠️ **(b) As FÉRIAS eram invisíveis na visão de semana do balcão.** A coluna do dia não
  tem dono, então `BloqueioDe(quando, null, null)` só enxergava o fechamento da CLÍNICA
  inteira: o dia de férias de um profissional aparecia apenas com menos horários — e menos
  horários se lê como "ninguém marcou". São **duas** metades, e uma sem a outra não
  resolve: com o recorte "só a minha agenda" a coluna passou a ter DONO (e aí férias e
  jornada dele pintam os vãos, como no modo dia), e na coluna de todos entrou a **linha do
  cabeçalho** nomeando o que está fechado. Pintar os vãos da coluna de todos seria mentira
  — os outros atendem —, e é por isso que a resposta ali é uma FRASE, não uma cor.
  ⚠️ **A frase mora na Application** (`FechamentosDaAgenda.Descrever`), pela regra da
  `GradeSemana` (parcela 69) e do `ResumoSessaoAnterior` (77): **o que decide o que a tela
  AFIRMA precisa morar onde o `dotnet test` alcança**. Recurso repetido é dito UMA vez —
  três bloqueios de tarde da mesma pessoa escreveriam o nome dela três vezes, e a linha
  deixaria de se ler; e com dono só entra o que alcança ELE, senão o fechamento da sala 2
  acenderia para quem atende noutra sala.
  ⚠️ **(c) Os três formulários que despejavam o alfabeto ganharam `SemBuscaInicial`** —
  agendamento da Recepção, lista de espera e o agendamento do FATURAMENTO. E o débito
  escrito na parcela 88 era real: **o faturamento tem seletor PRÓPRIO** (o débito
  permanente da Fase 4), então `SemBuscaInicial` e `TemResultados` tiveram de ser
  PORTADOS para lá — a cópia que fica para trás é onde a capacidade some (parcelas 61,
  75, 90).
  ⚠️ **E o custo previsto se confirmou: `SemBuscaInicial` sozinho abre um VÃO em branco.**
  Nos dois formulários da Recepção a lista é uma caixa de 150 px sempre visível, e caixa
  vazia no meio do formulário se lê como lista que não carregou — ela passou a SUMIR
  (`Seletor.TemResultados`). No do faturamento a lista é a coluna inteira, e esconder
  deixaria o cartão vazio: ali entrou o CONVITE no lugar dela ("digite o nome ou o CPF").
  **A pergunta é sempre a mesma — o que ocupa o lugar do que sumiu?**

- **A MÍDIA DO PRONTUÁRIO E OS CAMPOS QUE ESTA CLÍNICA ANOTA** (set/2026, item 5 da lista
  "o que falta para ficar profissional"). Duas metades do mesmo pedido: o que o prontuário
  não conseguia GUARDAR, e o que ele não conseguia PERGUNTAR.
  ⚠️ **(a) O vídeo não cabia no banco, e o teto não era capricho.** O anexo guarda os bytes
  numa coluna, com teto de 10 MB, porque o banco é REMOTO — anexo gigante trava a
  sincronização de todo mundo e o erro aparece longe da causa. Um vídeo de marcha de
  quarenta segundos passa disso sem esforço, então o profissional gravava no celular,
  mandava por WhatsApp, e o registro clínico ficava fora do prontuário: sem guarda, sem
  trilha de acesso, num aplicativo que a clínica não controla.
  O arquivo grande vai para o armazenamento que a clínica JÁ tem (o mesmo S3-compatível da
  publicação de receitas) e a linha continua no banco — `AnexoProntuario.CaminhoRemoto` e
  `AnexoPaciente.CaminhoRemoto`, migration aditiva, nulo = "está no banco", que é a verdade
  de toda linha já gravada.
  ⚠️ **A decisão da feature é o VERBO, não o upload: `GuardarPrivadoAsync`.** O
  `PublicarAsync` aplica `public-read` — é o desenho inteiro da receita, que precisa abrir
  para um farmacêutico ANÔNIMO, com o token de 128 bits como barreira e a decisão escrita.
  Vídeo do paciente é dado de saúde (art. 5º, II): endereço "inadivinhável" vaza por print,
  por histórico do navegador e por encaminhamento de mensagem, e no dia em que vazar não há
  como saber quem baixou. Aqui a barreira é a CREDENCIAL, e o caminho inadivinhável é a
  segunda tranca — nunca a primeira. **Reusar o verbo teria sido de graça e é o erro que
  esta parcela existe para não cometer.**
  ⚠️ **Falhar ao guardar IMPEDE o anexo** — a assimetria deliberada em relação a quase todo
  o resto do sistema, que degrada e avisa. Linha gravada com o arquivo perdido é um "abrir
  vídeo" que não abre, num registro que a lei manda guardar por 20 anos: o prontuário
  afirmaria ter uma prova que ninguém tem. E **cancelar NÃO apaga o objeto remoto**, ao
  contrário da receita publicada: lá o que sai do ar é a PUBLICAÇÃO e os bytes assinados
  ficam no banco; aqui o objeto remoto É o registro clínico.
  ⚠️ **O prefixo é `m/`, separado do `r/` das receitas**: uma é pública com prazo, a outra é
  privada e guardada 20 anos, e uma varredura de expiração que confundisse as duas apagaria
  registro clínico. A extensão do caminho é SANEADA — o nome do arquivo vem de fora, e
  deixá-lo compor a chave crua é como se escreve um caminho com `../` dentro.
  ⚠️ **Os dois lados (anexo de sessão e arquivo da ficha) foram feitos no MESMO commit**:
  a cópia que fica para trás é onde a capacidade some, e aqui ela sumiria justamente no
  vídeo que o paciente mandou por WhatsApp e não pertence a sessão nenhuma. E o MIME virou
  UMA definição (`MidiaClinica.MimeDe`): a que existia na ficha conhecia três formatos, e
  MIME errado gravado faz o celular baixar o vídeo em vez de tocá-lo.
  ⚠️ **(b) Os campos personalizados — e o que eles NÃO são.** A evolução tem doze campos, e
  eles cobrem o que é comum a toda clínica. O específico ("nº de agulhas", "aparelho",
  "carga do exercício") era escrito no meio do texto livre, quando era escrito: dado dentro
  de prosa não se compara entre sessões, não vira coluna de relatório e não se acha por
  busca confiável. **Não é um construtor de prontuário**: os doze campos continuam em
  CÓDIGO com as regras deles, o campo personalizado é ACRÉSCIMO, e ele não pode impedir o
  registro — registro clínico que não se consegue salvar é registro que não acontece.
  ⚠️ **Aplicar COPIA rótulo e tipo** — a regra do protocolo do mapa corporal, das escalas e
  das medidas. Sem a cópia, renomear "Agulhas" para "Nº de agulhas" reescreveria a sessão
  do mês passado, e desativar o campo deixaria valores gravados sem rótulo: um número solto
  no prontuário, pior do que não ter registrado nada. É por copiar que a definição pode ser
  DESATIVADA — e só desativada: apagá-la quebraria o vínculo pelo qual a clínica sabe que a
  coluna "Agulhas" de 2026 e a de 2027 são a MESMA pergunta.
  ⚠️ **O lugar 4 da auditoria de linha na variante mais fácil de esquecer.** O campo
  personalizado NÃO aparece na entidade como propriedade, então o `GuardarVersao` o
  esqueceria sem nada quebrar — e corrigir a sessão apagaria os valores anteriores sem
  rastro (art. 3º da Lei 13.787/2018). Ele entra na versão como TEXTO
  (`VersaoEvolucao.CamposPersonalizados`), e a escolha é deliberada: o que a lei exige é
  **recuperar o que estava escrito**, e a versão é lida por gente, não por consulta — uma
  tabela de versões dos valores custaria entidade, migration e leitura para responder a
  mesma pergunta com mais peças para divergir.
  ⚠️ **`null` PRESERVA, lista vazia APAGA.** A janela do balcão não mostra estes campos:
  `null` quer dizer "esta tela não os edita", e regravar sobre eles apagaria o que o médico
  escreveu, sem erro e sem aviso (a armadilha da parcela 74). Vazio é outra coisa — é a
  tela que os MOSTRA e teve todos apagados.
  ⚠️ **Número e data em cultura INVARIANTE, valor inválido RECUSADO.** Dois postos com
  culturas diferentes escreveriam "2,5" e "2.5" na mesma coluna, e a comparação entre
  sessões deixaria de existir sem nada falhar; e descartar em silêncio o que não serve ao
  tipo deixaria o campo em branco depois de a pessoa o ter preenchido — o registro
  afirmaria que ninguém respondeu.
  ⚠️ **Teto de 12 campos ativos, e desativar sempre passa.** Uma folha com trinta campos
  extras deixa de ser preenchida, e o que se perde não é o trigésimo campo, são os doze do
  sistema. O teto conta só quando o campo ESTÁ ficando ativo: recusá-lo ao desativar
  travaria justamente a correção que resolve o excesso.
  ⚠️ **A MODALIDADE decide onde o campo aparece** — "nº de agulhas" não faz sentido na
  consulta de psiquiatria, e campo que aparece onde não serve é o campo que ninguém
  preenche (e que faz parar de preencher os outros). Sem horário em foco valem os de todas
  as modalidades: mostrar menos do que se sabe esconderia o campo que a clínica cadastrou.
  ⚠️ **E os `Include` foram o que quase escapou**: sem eles a navegação chega VAZIA em
  produção — a versão guardaria "nenhum campo" e a folha impressa sairia sem o que a
  clínica anotou —, com o teste verde pelo relationship fixup do EF (a lição da parcela
  68). O teste que os prova usa um `DbContext` NOVO.

- **A WEB DE LEITURA — o dia, o mês e a ficha de fora da máquina** (set/2026, item 6 da
  lista; o mapa está em `docs/web-de-leitura.md`). `Clinica.Web` é ASP.NET Core `net8.0`
  puro — a ÚNICA parte da suíte que roda em Linux e que o `dotnet build` deste ambiente
  compila junto do resto.
  ⚠️ **SOMENTE LEITURA, e a única escrita é a TRILHA.** Lançar, marcar, escrever, receber e
  assinar continuam nos cinco apps; o que a web grava é o `AcessoProntuarioService`, porque
  o ponto 4 do compromisso vale para toda tela que ABRE prontuário — porta de leitura sem
  trilha é o buraco que só aparece no dia em que alguém precisa investigar. Falhar a trilha
  não impede ler (banco lento não pode travar quem está com o paciente na frente).
  ⚠️ **A régua de acesso é a MESMA do desktop** (`AcessoWeb`, na Application, com teste):
  os bits da parcela 49, e o prontuário exigindo `VerProntuario` mesmo para quem abre a
  ficha. Uma segunda regra — "na web todo mundo vê o resumo" — seria a permissão granular
  desfeita por uma porta nova, que é o que a parcela 60 achou nas cópias do faturamento.
  ⚠️ **Sem cookie é `Permissao.Nenhuma`, ao contrário do desktop.** Lá "sem sessão
  autenticada, `Pode` LIBERA", porque o login é obrigatório e tela vazia parece defeito;
  aqui a porta está na REDE, e liberar por omissão seria a web inteira aberta a quem não
  entrou. **Ao portar uma regra de sessão para outro meio, releia o caso-padrão dela.**
  ⚠️ **ONDE ela roda é decisão da DIREÇÃO, e não foi tomada.** O `banco-na-vps.md` recusou
  por escrito o desenho "API HTTPS no meio" — *o mais exposto*, porta 443 respondendo a
  qualquer IP com código nosso atrás. Publicar esta web na internet é tomar aquela decisão
  pela porta de trás, então o que se entrega é o software, com as duas instalações que não
  a contradizem (rede da clínica, ou atrás de VPN) e a lista do que ela precisaria ganhar
  ANTES de ser exposta — segundo fator, limite por IP (hoje o travamento é por USUÁRIO),
  origem do acesso na trilha. **Quando uma decisão de arquitetura já foi tomada e escrita,
  a feature nova não pode revogá-la de raspão.**
  ⚠️ **Não há framework de front, e é a decisão do gráfico desenhado com os tokens
  (parcela 5) aplicada de novo**: são três páginas de leitura, e um SPA custaria build,
  dependências que se atualizam sozinhas e uma segunda cópia do design system para manter.
  O preço disso é que **todo texto do banco passa pelo escape** (`Paginas.T`) — nome de
  paciente e observação de horário são texto que uma PESSOA digitou, e a CSP é a segunda
  tranca, nunca a primeira.
  ⚠️ **`Cache-Control: no-store` em toda resposta**: é o que impede a ficha de ficar no
  cache do navegador de um computador compartilhado, e o botão "voltar" depois do logout é
  exatamente onde isso apareceria.
  ⚠️ **A permissão é FOTOGRAFIA do login**, como o `SessaoUsuario.Entrar` — reler o banco a
  cada requisição pagaria uma consulta por clique. O preço é o mesmo do desktop e está
  escrito: permissão retirada passa a valer no próximo login, e o cookie dura 2 h.
  ⚠️ E as regras da casa atravessaram inteiras: cancelado e falta ficam na lista APAGADOS
  (a folha do dia), a busca **não consulta com o campo vazio** (`SemBuscaInicial`), o painel
  DIZ o que não pôde ler (`NaoVerificados` — "nada vencido" por causa de uma consulta
  quebrada é pior do que um painel que não abre), e quem não alcança página nenhuma **não
  entra**, com a frase dizendo por quê (a regra da parcela 45).

- **A AGENDA COM COR — a família no traço, no avatar e no cartão; o estado na pílula; o
  placar com glifo** (set/2026; a cliente: *"a nossa agenda está um pouco sem cor,
  consegue dar cor nisso? Não só cor, mas estilizar também"*; mockup aprovado em
  `docs/mockups/agenda-com-cor.html` ANTES de uma linha de WPF, o caminho da parcela 87).
  As três telas que mostram um horário — a lista do dia do balcão, a grade e o Meu dia do
  médico — saíram no mesmo commit e leem os MESMOS tokens: a mesma sessão com a mesma
  cor nos três lugares é o que faz a cor virar vocabulário em vez de enfeite.
  As decisões que não são óbvias pelo código:
  ⚠️ **Família e estado são dois eixos, e NÃO disputam a mesma peça.** A família (o que
  a sessão É) pinta o traço de 3 px da linha, o avatar e o fundo do cartão da grade; o
  estado (em que ponto do dia ela está) fica na pílula, com um ponto colorido. Pintar a
  linha inteira pelo estado brigaria com os dois casos em que ela JÁ muda de fundo — a
  chamada demorada (vermelho) e o paciente na sala (o `Sucesso.Tint`, um degrau mais
  claro que o `.Suave`, porque numa linha de 56 px o suave pinta demais).
  ⚠️ **Os tons claros são ALIASES por família** (`Brush.Modalidade.*.Suave`), inclusive
  onde o token já existia (Azul.50, Ciano.100): as telas leem os cinco pelo mesmo nome, e
  a próxima família nasce com os dois tons de uma vez em vez de esquecer um.
  ⚠️ **O avatar ganhou `Fundo`/`Tinta` como DPs, com o PADRÃO no estilo implícito** — não
  no controle. É o que permite a lista trocar o par por gatilho num estilo local
  `BasedOn` sem redesenhar o Template; e é por isso que o padrão não pode morar no
  `PropertyMetadata`: lá não há como apontar um token. Portado ao faturamento no mesmo
  commit (o débito permanente da Fase 4), embora nenhuma tela de lá o use ainda.
  ⚠️ **A legenda é UM componente do shell, e fica SEMPRE.** Três cópias divergiriam na
  primeira família nova — a que ficasse para trás mentiria sobre a cor. Os opcionais
  (`ComConfirmacao` nas listas, `ComGrade` na grade) existem porque legenda que explica
  um sinal que a tela não desenha ensina a procurar o que não existe. Ficar sempre foi
  decisão da direção: uma linha de 12 px no pé não custa altura a ninguém.
  ⚠️ **A linha do "agora" marca a FAIXA, não o minuto**, e é decisão: a faixa tem 30 min
  e cresce com o encaixe; posicionar ao minuto exigiria medir a altura, para uma pergunta
  ("onde estamos no dia?") que não pede essa precisão. Ela é sobreposição num `Grid`
  (último filho, checagem 25), `IsHitTestVisible=False` para o vão continuar clicável, e
  o recuo à esquerda é o MESMO espaçador de régua do cabeçalho das colunas — o número 64
  continua existindo num lugar só. Só hoje e só no modo dia, decidido na VM.
  ⚠️ **A hachura do vão fechado é um `DrawingBrush` do design system, só de tokens** —
  tile de 8 px com UMA diagonal de canto a canto, que é o que mantém o traço contínuo
  entre tiles. O motivo escrito por cima ganhou fundo próprio: texto sobre diagonal não
  se lê. E a legenda desenha o MESMO brush: hachura desenhada duas vezes divergiria.
  ⚠️ **`TextTrimming` que vem de estilo local se repete na TAG** (a lição da prévia da
  guia, cobrada de novo pela checagem 24 nos quatro números do placar): o verificar-suite
  não resolve estilo local, e repetir é o preço que deixa a intenção à vista.
  ⚠️ E o `compilar-sombra` pegou o `using Clinica.Domain;` que faltava nos DOIS
  ViewModels que ganharam `ModalidadeAtendimento` — o tipo já era usado nos arquivos por
  nome qualificado noutro ponto, e o campo novo não. Rede que roda antes do push é rede
  que pega antes do CI.

- **A TELA APROVADA CONSTRUÍDA, e a idade impossível em OITO cópias** (set/2026 — a
  direção: *"eu aprovei esse tipo de tela de atendimento porém ao abrir o sistema não foi
  aplicado, aí cabe a dúvida se as outras coisas também foram aplicadas!"*). O modelo 3
  ("o cabeçalho claro") tinha sido aprovado em mockup e a parcela anterior entregou o
  DESENHO, não a tela; os dois defeitos que a foto do dia mostrava seguiam de pé.
  ⚠️ **A pergunta da direção é a lição de método, e ela vale mais que a parcela:** entregar
  o desenho de uma tela aprovada e não construí-la gasta a confiança em TUDO o que foi
  entregue junto — inclusive no que estava certo. **Parcela que separa "o desenho" da
  "tela" precisa dizer, para quem aprovou, que o que ele vai ver no sistema não mudou** —
  senão o próximo relato não é "a tela não veio", é "será que alguma coisa vem?".
  **O RAIL VERTICAL DE 212 px DEITOU e virou RÉGUA.** As sete seções são as mesmas, na
  mesma ordem, da MESMA lista do C# (`SecoesAgrupadas`) e com o mesmo `SelectedIndex` —
  trocar leiaute não pode quebrar contrato de navegação (a regressão da parcela 37, 4ª
  rodada). O que mudou foi o PAINEL (`WrapPanel`) e o desenho do item (sublinhado de 2 px
  no lugar do traço à esquerda).
  ⚠️ **O comentário do arquivo defendia a decisão CONTRÁRIA** — *"sete rótulos quebram em
  duas linhas de régua"* — e estava meio certo: o `TabPanel` ESPREME (parcela 50: "Convê",
  "Prontu", "Documer"), o `WrapPanel` DOBRA. Aba na segunda linha se lê; "Documer" não. É a
  mesma lição da parcela 50 pelo outro lado, e ela estava escrita neste arquivo. **Comentário
  que justifica uma decisão a torna invisível para quem a revisita** (parcela 74): ao trocar
  a decisão, o comentário entra no mesmo commit.
  ⚠️ **A pendência do TERMO subiu para a régua, e o ganho não é de leiaute:** ela era uma
  faixa dentro da seção Atendimento, logo **invisível nas outras seis** — quem lia o
  histórico não via que o paciente não assinou. Na régua ela acompanha o prontuário aberto.
  A faixa antiga foi REMOVIDA no mesmo commit: mantê-la seriam duas portas para o mesmo ato
  na mesma tela.
  ⚠️ **`StackPanel` horizontal docado NUNCA cede, e quem some é o NOME DO PACIENTE.** A
  ponta direita da banda (botão de etapa + "Trocar paciente" + a pílula) precisou de
  ORÇAMENTO de largura — `MaxWidth` na pílula —, senão a frase longa do horário que ainda
  não começou comeria a faixa e o miolo, que é o filho que PREENCHE, ficaria com zero. É a
  parcela 58 aplicada a uma banda de cabeçalho: **num DockPanel, o docado ganha e o fill
  paga; então quem tem tamanho imprevisível é que precisa de teto.**
  ⚠️ **A IDADE ESTAVA EM OITO CÓPIAS, e sete imprimiam "1851 anos".** A conta estava certa;
  errada estava a DATA (importação do Smart Clinic, ou dedo no teclado), e nenhuma das
  portas perguntava se aquilo podia ser uma pessoa viva. As sete: o crachá, as duas fichas
  (balcão e faturamento), a busca do Novo atendimento, o contexto da Enfermagem, a lista do
  faturamento e — a pior — **o PDF que o paciente leva embora**. A oitava, a capa do
  paciente, era código morto.
  ⚠️ **E a NONA já tinha corrigido o defeito com regra PRÓPRIA**: o aniversário
  (`RelacionamentoService`) filtrava por `Year > 1900`, com um comentário dizendo *"2025
  anos na tela é pior que campo vazio"*. Alguém encontrou o defeito, resolveu-o no lugar
  onde doeu e a solução não alcançou as outras oito. **Correção local de um defeito geral é
  o que faz o defeito geral sobreviver** — e a próxima pessoa a encontrá-lo vai achar que é
  novo. A regra virou UMA (`IdadeDoPaciente`, no Domínio), e a FRASE também
  (`Texto` → "38 anos" / "data a conferir"): corrigir a conta e deixar a frase em três
  cópias seria fazer metade do serviço, porque é a frase que o paciente lê.
  ⚠️ **A regra é a da MEDIDA CLÍNICA: recusa-se o IMPLAUSÍVEL, nunca o anormal.** Teto de
  130 anos (o recorde verificado é 122): um teto apertado apagaria a idade da paciente de
  103, que é justamente aquela em que a idade MUDA a conduta. E **AUSENTE ≠ IMPLAUSÍVEL** —
  ficha sem data é o caso normal da clínica (a linha só pula a idade); data errada é
  defeito de CADASTRO, e sumir com ela em silêncio a deixaria errada para sempre. Daí o
  terceiro estado escrito: "idade a conferir" no crachá, "data a conferir" ao lado da data
  nas fichas e no PDF. Quem conserta é o balcão, e ele precisa VER.
  ⚠️ **Um teste alcançava a regra por REFLEXÃO num `private static`, e a reflexão era o
  SINTOMA**: a regra estava privada dentro de um serviço, então cada porta que precisou
  dela escreveu a sua. **Teste que precisa de reflexão para alcançar uma regra está dizendo
  que a regra está no lugar errado.**
  ⚠️ **E A NONA PORTA CONSUMIA O RESULTADO DA OITAVA, não a regra — então herdou o nulo e
  perdeu o terceiro estado.** A capa do paciente escrevia a idade a partir de
  `cabecalho?.Idade`, que é o campo do CRACHÁ e já vem nulo quando a data é implausível:
  com a regra unificada ela parou de imprimir "1851 anos" (certo) e passou a mostrar só
  "01/01/1851", em SILÊNCIO, enquanto o crachá logo acima — na MESMA tela — escrevia
  "idade a conferir". Ausente e errado voltaram a ser a mesma coisa numa porta só, e ela
  não aparece em varredura nenhuma porque o `grep` da conta à mão não a alcança: ela não
  faz conta, faz uma leitura de segunda mão. **Unificar a regra não basta enquanto houver
  leitor que consome o RESULTADO JÁ FILTRADO de outro leitor** — a lista das portas a
  percorrer é a de quem MOSTRA o dado, não a de quem o CALCULA. Pego relendo o diff, não
  por rede.
  ⚠️ **O "EMITIR DOCUMENTO" DUPLICADO ERA UM BOTÃO ACESO QUE NÃO FAZIA NADA — na OUTRA
  porta.** Ele estava no corpo COMPARTILHADO da `FolhaDaSessaoView` e no slot `Ferramentas`
  do Consultório: dois botões idênticos lado a lado ali. E `EmitirDocumentoCommand` /
  `PodeEmitirDocumento` só existem em `AtendimentoViewModel`, **não na base** — então na
  janela "Nova sessão" do Prontuário (`EscreverSessaoViewModel`) os dois bindings não
  resolviam: comando nulo e `IsEnabled` caindo no padrão `true`. O defeito da parcela 41,
  dentro do componente que a extração criou, e o comentário do slot logo acima já dizia por
  escrito que emitir documento é do Consultório.
  **A regra que fica: ao extrair um componente para DUAS portas, todo binding do corpo
  compartilhado tem de resolver na ViewModel BASE.** O que só uma porta tem entra pelo
  slot. Nenhuma rede pega isto — o XAML é bem-formado, o `compilar-sombra` não lê o corpo
  do XAML e o binding morto é silencioso —, e a conferência é `grep` do membro no tipo
  base.

- **A TRAVA DAS MIGRATIONS PAROU A CLÍNICA — trava sem prazo é trava que alguém esquece
  segurando** (18/10/2026; a Recepção e o Gerente não abriam: *"Exception while reading from
  stream"* e depois *"o banco não respondeu a tempo"*). Toda abertura da suíte chamava
  `pg_advisory_lock` SEM PRAZO, mesmo sem migration pendente. Uma conexão do posto caiu com a
  trava na mão — a sessão ficou "idle" no servidor, segurando-a até o keepalive de 2 h — e todo
  app que abria depois ficava "SELECT waiting" até estourar o tempo. Nada no código estava
  errado no caminho feliz; o defeito só existe com rede instável. A correção
  (`ShellBootstrap.PrepararBancoAsync`): **sem migration pendente, a trava nem é pedida**; com
  pendente, `pg_try_advisory_lock` de 2 em 2 s com prazo de 60 s, e passado o prazo a abertura
  segue sem ela (a corrida de DDL já é tolerada), com log. Para destravar à mão: encerrar no
  Postgres as sessões de `pg_locks` com `locktype='advisory' and objid=727411`
  (`docs/crm-em-producao-out2026.md`). **Toda espera por recurso compartilhado tem prazo.**
- **DDL de migration só alcança as tabelas cujo DONO é o papel do desktop** (out/2026 — a
  Recepção não abriu: `42501: permission denied for table SolicitacoesEnfermagem`). As
  tabelas dos pedidos da enfermagem são criadas pelo atualizador do posto
  (`deploy/tablet`) como `postgres`, e o desktop recebe só SELECT/INSERT/UPDATE nelas. A
  migration `AvisosInstantaneos` fazia `CREATE TRIGGER` incondicional nas três tabelas e o
  app parava na abertura — mensagem de "conexão" para um erro de privilégio (a lição da
  parcela 79). Agora cada gatilho só nasce onde `has_table_privilege(…, 'TRIGGER')`, o
  ouvinte lê quais tabelas TÊM gatilho (`OuvinteDeMudancas.Avisa`) e o relógio dos pedidos
  só se espaça quando a tabela dele avisa; `deploy/tablet/avisos-instantaneos-enfermagem.sql`
  cria os dois que faltam como dono. **Nenhum teste via**: a suíte aplica as migrations
  como superusuário. Reproduza com um papel sem dono antes de mexer em tabela do posto.
  ⚠️ **E `FOREIGN KEY` para essas tabelas também é DDL que pede privilégio** (`REFERENCES`):
  a comissão de recuperação (out/2026) apontava para `Acompanhamentos` — criada pelo script
  do posto — e o app não abriu com o mesmo 42501. A chave só nasce onde
  `has_table_privilege(…, 'REFERENCES')`, e `deploy/tablet/comissao-recuperacao-chave.sql`
  a cria como dono. **Tabela nova que aponta para tabela do posto: chave condicional + script.**
- **O build do PR roda TUDO EM PARALELO** (out/2026): `testes` em Linux (seis fatias),
  `conferencias` no Windows e `publicar` (os cinco `.exe`) começam juntos, e o job `build` —
  o nome do check do PR — espera os três. O pedido da direção (`.exe` de commit reprovado não
  fica para download) é cumprido pelo `build`, que APAGA os artefatos `Clinica-*` do run
  quando algum dos três reprova; esperar os testes para só então gerar somava ~5 min ao PR.
  Artefato de PR vive 7 dias (`retention-days`) — quem distribui à clínica é a release.
  Passo novo entra no job certo; pôr teste de Linux no runner Windows paga 2× e alonga a fila.
- **A suíte roda em SEIS FATIAS no CI, divididas pelo TEMPO MEDIDO** (out/2026): eram 27 min
  num job só. `FatiaDaSuite` (xUnit `TestFramework`) descarta na descoberta o que não é da
  fatia `CLINICA_FATIA=i/n`; sem a variável roda tudo. Divide por CLASSE; classe grande demais
  leva `[FatiarPorCaso]` e cada caso vai para uma fatia (`TelasBlazorAbremTests`,
  `PalcoTrocaRepetidaTests`). A distribuição sai de `tempos-dos-testes.json` (maior primeiro,
  na fatia mais leve — o "split by timings" dos CIs grandes): pelo hash do nome as fatias
  saíam com 3,9 e 7,2 min. Chave fora da tabela cai pelo hash. **Regrave a tabela**
  (`tools/medir-tempos-dos-testes.py`, a partir dos `.trx`) quando uma fatia do CI passar a
  demorar visivelmente mais que as outras. `FatiaDaSuiteTests` prova que toda chave cai em
  UMA fatia. Localmente dá para rodar as fatias em
  paralelo (`for i in 1 2 3 4 5 6; do CLINICA_FATIA=$i/6 dotnet test … & done; wait`). ⚠️ **Teste
  que espera a tela "assentar" paga a espera em CADA passo**: o mínimo de 150 ms do
  `Assentar` (50 ms × 3) era mais da metade do tempo da suíte; virou 60 ms (15 × 4), provado
  com um `throw` num botão. Teste novo de tela que espera por laço usa a mesma batida.
  ⚠️ **Teste que acha as telas por REFLEXÃO cresce sozinho com a migração** (out/2026, PR
  285 — a fatia 1 levava 12 min e reprovava ao acaso). O `PalcoTrocaRepetidaTests` foi
  desenhado para as 14 telas do Financeiro e passou a achar 75: ~375 trocas sobre 3 MB de
  HTML montado, 5–6 min SOZINHO. E o `TelasBlazorAbremTests` clicava "um botão por RÓTULO" —
  mas o rótulo de botão de DADO é o próprio dado ("08:15 Marina Teixeira", "Seg 12"), então
  a Visão geral da agenda dava 882 cliques e 4 min. Agora o palco roda em LOTES de ~15 telas
  (`[FatiarPorCaso]`, um lote por caso) e o botão de dado conta pela FAMÍLIA da classe (até
  4; `TelasBlazorAbremTests.Familia`) — os de ação (`botao`, `mini`, `icone`…) seguem um por
  rótulo. Os dois foram provados reprovando: releitura do Caixa desligada e um `throw` no
  bloco de horário. Na mesma rodada, as duas intermitências do palco: o `Assentar` esperava
  só o HTML parar (~60 ms), e o redesenho na fila atrás da leitura de outra tela chegava
  DEPOIS — agora ele espera a tela PEDIDA à vista; e as consultas eram contadas num contador
  só, creditando a uma tela a leitura de fundo de outra — agora contam POR TELA
  (`AsyncLocal`). **Ao migrar tela, olhe o tempo dos testes que as acham por reflexão.**
- **A PONTE DO CRM NA VPS: o que só a montagem real mostrou** (out/2026; roteiro em
  `docs/vps-do-crm.md`, e o diário da instalação real — acessos, endereços, etapas ligadas,
  como publicar de novo e o que ficou para depois — em `docs/crm-em-producao-out2026.md`). Duas VPS: a ponte ao lado do Postgres (escuta só no IP do túnel
  WireGuard, `RedeDaPonte` recusa endereço público na partida) e o CRM web noutra máquina.
  ⚠️ **Transação aberta à mão + `EnableRetryOnFailure` = recusa em TODA gravação.** O
  `AddClinica` liga a estratégia que repete, e o EF recusa `BeginTransaction` sob ela
  ("does not support user-initiated transactions"). Confirmar, cancelar, remarcar, marcar e
  registrar recall pela ponte devolviam 409 em produção, com os testes verdes: eles montam o
  `DbContext` SEM estratégia. Agora passam por `TransacaoDaPonte`, e `PonteComRepeticaoTests`
  dá ao SQLite uma estratégia que repete (é o que dispara a mesma recusa). **Serviço novo que
  abre transação: teste com estratégia de repetição, nunca só com o contexto cru.** E o
  comentário de `ConfigurarNpgsql` já avisava; quem escreveu a ponte não o leu.
  ⚠️ **Recusa que vira 409 genérico precisa deixar o motivo no log**, senão "a operação não é
  permitida" na VPS não diz o que conferir. Foi o log novo que mostrou o defeito acima.
  ⚠️ **`IPAddressDeny=any` no systemd também tranca a SAÍDA.** O serviço antigo da ponte só
  permitia localhost; com o lembrete de SMS ligado ali, o envio ao provedor e ao balde
  falharia calado. Quem tranca a entrada é a escuta, a lista de clientes, a chave e o ufw.
  ⚠️ O relógio do SMS virou UMA classe (`RelogioDoSmsNoServidor`, Infrastructure) para os dois
  hospedeiros (ponte e web): o lembrete sai com a clínica fechada, e a reserva no banco impede
  o SMS duplicado com os apps abertos.
- **O usuário do banco da ponte do CRM grava SÓ nas tabelas que a ponte grava** (out/2026,
  achado da revisão da PR 257): `papel-ponte-crm.sql` dava INSERT/UPDATE em todas, e uma
  credencial vazada alcançaria usuários e prontuário. A lista é MEDIDA, não suposta:
  `PontePapelRestritoTests` aplica o próprio arquivo e roda os fluxos (confirmar, marcar com
  guia no agendamento, cancelar, remarcar, recall, lembrete de SMS) conectado como
  `ponte_crm` — provado tirando `Codigos` e `MensagensSms` da lista: reprova com 42501.
  ⚠️ **Fluxo novo da ponte que grave em outra tabela: rode esse teste no Postgres antes do
  push**, senão a clínica descobre pelo 42501. E a falha do lembrete de SMS NÃO lança: vira
  `Falhas` no resultado, então a asserção é sobre `Enviados`.
- **Triggers usados pelo portal devem funcionar com seu papel restrito.** `SELECT FOR SHARE`
  exige UPDATE na tabela consultada; serializar agenda/jornada por advisory lock evita
  conceder ao portal permissao de editar profissionais. Testar com papel sem UPDATE.

- **Navegação superior e largura útil nos cinco aplicativos (17/09/2026, correção da cliente).**
  Categorias ficam na MESMA barra da pesquisa. Subpáginas abrem em menu ao passar o
  mouse, clicar ou usar o teclado; não há sidebar nem duas réguas permanentes. Ctrl+B
  foca os grupos e Ctrl+F a pesquisa. Abrir o menu não recria a tela em edição.
  Preservar permissões, destinos ocultos e abas compostas. O conteúdo ocupa a área útil
  inteira. Um WrapPanel numa coluna Auto ainda pode receber largura infinita: régua de
  documentos deve ocupar uma linha de largura finita. Conferir janelas COMPLETAS com
  dados fictícios, tabelas preenchidas e redimensionamento; não só telas isoladas ou
  placeholders. A soma das colunas deve reservar também a barra de rolagem vertical.
  Botões visíveis no visual tree ainda podem estar cortados pelo viewport da tabela.

- **O PAPEL SAI DA SESSÃO SEM SAIR DA SESSÃO — e o documento nascia AVULSO** (set/2026;
  mockup "documentos nas quatro telas" aprovado ANTES de uma linha de WPF, depois de cinco
  desenhos reprovados; o pedido foi por SUPERFÍCIE: *"a tela de documentos na hora do
  atendimento e também a tela de Documentos que aparece na recepção, gerente,
  consultório"*). Quatro telas para o mesmo assunto, cada uma respondendo a pergunta de
  quem a abre — e a janela de emissão como a peça comum.
  ⚠️ **O achado que a construção revelou: `DocumentoClinico.AgendamentoId` e `EvolucaoId`
  eram GRAVÁVEIS SEM ESCRITOR.** `ProcedenciaDaSessao` sabia escrever "da sessão de
  08/09", as TRÊS listas de documento mostravam a coluna, e o único escritor do vínculo
  era o termo assinado pelo paciente: toda receita emitida DENTRO da consulta aparecia
  como "avulsa", ao lado da sessão que a produziu. É o defeito recorrente do projeto pelo
  avesso — não é dado gravado sem leitor, é **coluna com leitor e sem escritor** —, e ele
  só apareceu porque uma tela nova precisou da frase que a coluna promete.
  ⚠️ **DESVIO DECLARADO DO MOCKUP APROVADO, e é o único.** O desenho mostra a receita
  dizendo *"já com a Passiflora que você escreveu"*, e isso não se entrega sem LER o texto
  livre da conduta à procura de nome de medicamento. Adivinhar ali é pôr no papel que a
  farmácia avia algo que o profissional não escreveu como prescrição — a garantia aparente
  que este projeto recusa desde a parcela 3. A receita abre EM BRANCO e o cartão DIZ isso,
  em cinza; o que herda de verdade é o CID da hipótese (atestado), a hipótese como
  indicação clínica (pedido de exame) e os carimbos da fila (comparecimento), cada um com a
  frase de acento explicando a origem **e dizendo que trocar ali não muda o prontuário**.
  Desvio de mockup aprovado se escreve, não se comete em silêncio.
  ⚠️ **A SAÍDA só entra quando a sessão foi ENCERRADA.** O mockup escreve "esteve aqui
  14:00 → agora", e "agora" numa declaração é afirmar um fato que ainda não aconteceu:
  enquanto o paciente está na sala a frase é "está aqui desde 14:00", e a declaração sai só
  com a chegada — que é a verdade disponível.
  ⚠️ **Quem assinava era o PRIMEIRO DA LISTA — em ordem alfabética.** `CarregarAsync` fazia
  `Profissional = Profissionais.FirstOrDefault()`, e a janela é a mesma nas quatro portas:
  no consultório, quem emitia tinha de trocar o combo toda vez. O padrão passou a ser
  `SessaoUsuario.Atual.ProfissionalId`, com o primeiro da lista como caminho de baixo — o
  nome no papel é quem responde por ele.
  ⚠️ **A régua de EMITIR tinha quatro botões escritos à mão e o catálogo tem SEIS folhas
  clínicas de paciente**: o relatório de evolução — o papel que o paciente leva ao
  convênio — e a anamnese só se emitiam pela central da RECEPÇÃO, e quem atende pedia ao
  balcão o relatório do próprio paciente. A régua passou a vir do `CentralDocumentosService.
  Catalogo`, e o ganho não é a folha nova: é **cada uma trazer o BIT dela**. O
  comparecimento pede `EditarPaciente` e a receita pede `Prescrever`; sob um `IsEnabled` só,
  quem tem um e não o outro via os seis apagados ou os seis acesos.
  ⚠️ **`Selector.SelectedItem` que recebe item FORA do `ItemsSource` devolve NULL pelo
  binding** — a pílula "quem está hoje" limparia a escolha no mesmo instante em que a faz,
  sem erro nenhum, porque a lista de resultados só existe com termo digitado. O componente
  já tinha a porta desde a remarcação (`SeletorPacienteViewModel.SelecionarGarantindoNaLista`);
  **antes de escrever a seleção por fora, procure o método que o componente já tem.**
  ⚠️ **A coluna PERMANENTE de 340 px contraria a régua do README de propósito**, e a
  exceção tem medida: a tela do paciente roda em modo IMERSIVO (sidebar e barra de cima
  recolhem), então em 1366 sobram ~1000 px para a folha. Sem o modo imersivo ela não
  caberia — e é por isso que ela existe ali e não na janela "Nova sessão" do Prontuário,
  que é modal e estreita.
  ⚠️ **Num DockPanel, o ancorado leva a altura que pede e quem paga é o FILL** (parcela
  79): a lista "já saiu hoje" cresce com o DIA, e sem teto ela espremeria os cartões — que
  são o assunto da coluna — até zero. Teto pede a roda de volta para a página; as duas
  metades andam juntas. O mesmo vale para as PÍLULAS do balcão: num dia de trinta sessões
  elas quebrariam em quatro linhas, então são dez e o rótulo DIZ que a lista está cortada
  ("10 de 31 — digite para achar os outros"), senão a recepcionista conclui que só dez
  pessoas vêm hoje.
  ⚠️ **A tela da direção não tem um único botão de emitir**, e é a decisão inteira dela: o
  balcão opera, a direção lê o agregado. `PainelDeDocumentos` (Application, puro) responde
  quantos saíram — **cancelados INCLUSIVE**, porque o número da folha foi gasto e a
  numeração por ano não se reaproveita —, quantos foram selados com e-CPF, quantos estão no
  ar e quem assinou o quê. O desempate do "mais emitido" é pelo NOME: sem ele, duas folhas
  com a mesma contagem trocariam de lugar entre duas leituras do MESMO período, e a direção
  veria o número mudar sem nada ter mudado.
  **O que ficou de fora, com o motivo**: a PRÉVIA DO PAPEL ao lado do formulário (a metade
  direita da "janela que escreve o papel") — ela é a quinta peça do mockup, não uma das
  quatro telas, e é um desenho de ~490 px que nenhuma rede deste ambiente consegue ver
  montado; a lista da tela 2 continua sendo a rica (badges, link, correções) em vez da
  tabela de quatro colunas do desenho, porque trocá-la perderia o que ela já mostra (a
  lição da parcela 72).

- **A REVISÃO DA PARCELA: quatro achados que as três redes deram por verdes** (set/2026,
  conferência da PR das quatro telas de documentos — build, `compilar-sombra`,
  `verificar-suite` e 2472 testes verdes na entrada).
  ⚠️ **`BasedOn="{StaticResource {x:Type T}}"` SÓ EXISTE SE ALGUÉM DECLAROU O IMPLÍCITO.**
  A lista de avisos do formulário de agendamento nasceu com um `Style` local herdando o
  estilo implícito de `TextBlock` — e o design system não tem nenhum: **todos os estilos de
  `TextBlock` da casa têm `x:Key`**. `TextBlock` não é `Control`, então não há
  `generic.xaml` para onde cair, e o `StaticResource` lança
  `ResourceReferenceKeyNotFoundException`. Pior: dentro de um `DataTemplate` o conteúdo é
  ADIADO, então a janela ABRE e só quebra quando o primeiro item aparece — que ali é
  exatamente quando há aviso para mostrar. **A checagem 2 não via**: ela IGNORA toda chave
  `{x:Type ...}` presumindo que é do sistema, e o ponto cego durou até esta PR. Virou a
  **checagem 48**, com a pergunta certa ("existe estilo implícito para este tipo?") e a
  distinção que a torna sem ruído: controle do WPF cai no TEMA, controle da CASA e
  não-`Control` não caem em lugar nenhum. Medida antes de ligar — nove usos no repositório,
  oito legítimos, o nono era o defeito.
  ⚠️ **Limpar antes do await vale para TODA coleção da tela, não só a que o comentário
  lembra.** A coluna ENTREGAR AGORA zerava os carimbos da sessão antes da leitura (com a
  lição da parcela 89 escrita ao lado) e deixava o `SaiuHoje` para depois — então, na troca
  de paciente, o crachá já mostrava quem entrou na sala e a lista "JÁ SAIU HOJE" continuava
  com os papéis de quem saiu, com o "2ª via" apontando para o documento da outra pessoa.
  **Contador de geração impede a resposta VELHA de sobrescrever a nova; ele não impede a
  lista velha de FICAR na tela** — e a janela aqui é a carga inteira do prontuário.
  ⚠️ **A cópia campo a campo tem um segundo lugar: o ramo de UPDATE do REPOSITÓRIO.**
  `SalvarConvenioAsync` copia treze campos e não copiava `GeraGuia` nem
  `RegistroAnsOperadora` — os dois editáveis na tela de Convênios do faturamento. A
  CRIAÇÃO funcionava (o outro ramo é um `Add` do objeto inteiro), que é o que escondeu o
  defeito por duas parcelas: desmarcar "gera guia" numa linha existente era um clique que
  não fazia nada — a outra metade do *"não consegui entender como fazer um atendimento
  particular"* —, e o registro ANS digitado nunca valia, então o lote TISS saía com o
  registro global. **Ao procurar o lugar 3, olhe também quem grava no banco.**
  ⚠️ **Lista cortada não responde por quem ficou de fora.** As pílulas de "quem está hoje"
  na central de documentos param em dez, e o contexto do paciente escolhido as consultava
  para dizer "tem horário hoje às 14h": quem fosse o décimo primeiro do dia perdia a frase.
  Atalho cortado é atalho; a resposta sai da lista INTEIRA.

- **QUEM COLHE NÃO ALCANÇAVA O QUE COLHEU — o termo assinado que sumia da recepção**
  (set/2026 — a clínica: *"a recepção enviou, a paciente assinou, confirmou, porém não
  achamos onde está indo. Até mesmo na ficha da paciente não aparece"*). O termo do BSV
  foi emitido, mandado pelo WhatsApp, assinado no celular, conferido e selado — estava
  gravado, numerado e ligado à sessão. O que faltava era a recepcionista poder LÊ-LO.
  ⚠️ **A assimetria estava entre os DOIS bits da mesma folha**: `termo-procedimento` pede
  `ColherAssinaturaPaciente` para EMITIR e pedia `VerProntuario` para VER — e o perfil
  `Recepcao` tem o primeiro e não tem o segundo (o corte da parcela 49). Ela atravessava a
  porta, fazia o trabalho todo, e o resultado ficava invisível nas QUATRO portas de leitura
  (a lista de documentos da ficha, a seção de termos do dia, a central e a linha do tempo)
  — inclusive na **2ª via**, que é como o paciente recebe a via dele. É o corredor sem
  saída da parcela 69, com o agravante de o único sinal ser o selo "falta termo" SUMIR do
  cartão da fila: sumir não se lê como "deu certo", se lê como "some do sistema".
  ⚠️ **As duas saídas fáceis eram as erradas, e por que**: conceder `VerProntuario` à
  recepção resolveria num clique e devolveria a evolução clínica inteira ao balcão (desfaz
  a parcela 49, ponto 5 do compromisso); baixar a folha para `VerFichaPaciente` mentiria
  sobre o que ela é — o termo diz qual procedimento a pessoa vai fazer e o que ela declarou
  sobre o próprio corpo, que é dado de saúde (art. 5º, II).
  A saída é `FolhaCatalogo.PermissaoVerTambem`: **um SEGUNDO acesso, por folha, só para
  VER**. O que autoriza o campo é a pergunta que ele carrega escrita — *este bit já alcança
  o CONTEÚDO deste papel por outro caminho?* Aqui sim: quem colhe abre a janela, lê o termo
  inteiro, vê as declarações e assiste à assinatura; ver a mesma folha depois não expõe uma
  linha a mais. **Bit que ainda não alcança o conteúdo não entra ali** — seria a permissão
  granular desfeita por uma porta nova. E vale só para ver: assinar, enviar, republicar e
  cancelar continuam em `PermissaoEmitir`, sem segunda via.
  ⚠️ **A armadilha que o campo cria, e ela é de uma linha: `HasFlag` sobre dois bits é um
  E.** As quatro telas perguntavam por conta própria com `HasFlag`, e `Pode(A | B)` também
  exige os DOIS — deixá-los assim fecharia a folha para as duas pessoas que deveriam
  alcançá-la, e o defeito apareceria como **lista vazia**, indistinguível de "esta paciente
  não assinou nada". A pergunta virou ponto único (`CentralDocumentosService.PodeVer`), e
  quem usa a sessão passou a `PodeAlgum`/`ExigirAlgum`. `AcessoParaVer` **mudou de
  semântica** (de bit para união), então a conferência que a mudança exige é reler TODO
  consumidor dele — foi por isso que a 2ª via precisou trocar `Exigir` por `ExigirAlgum`:
  sem isso a correção deixaria justamente a entrega da via ao paciente de fora.
  ⚠️ E a barreira da seção de termos do dia estava escrita À MÃO na ViewModel
  (`Pode(VerProntuario)`), divergindo da lista logo abaixo dela no mesmo arquivo. Passou a
  sair do catálogo. **Regra de acesso copiada para uma tela é a cópia que fica para trás.**
  O teste que a fixa percorre o CATÁLOGO (toda folha com segundo acesso é alcançada por
  cada bit sozinho, via `CatalogoPara` — o caminho real das telas), e não o termo pelo
  nome: asserção escrita à mão é a que a próxima folha não alcança. Verificado que ele
  REPROVA sem a linha do catálogo, não presumido.

- **A ASSINATURA CHEGAVA E MORRIA NA VARREDURA DE 24 H — porque ela só existia no BALDE**
  (set/2026, a segunda metade do "a paciente assinou e não achamos onde está indo"). A
  primeira metade era de acesso (quem colhe não alcançava o que colheu, logo acima); esta
  é pior, porque não é o papel que fica invisível: é a assinatura que **deixa de existir**.
  O traço do celular vivia no objeto do balde, e quem o trazia para dentro era o polling da
  **janela do termo ABERTA**. Quem enviava o link e fechava a janela nunca via a resposta
  chegar; 24 h depois `LimparVencidasAsync` cancelava a coleta e apagava o objeto. **Nada
  falhava** — o termo voltava a parecer "nunca assinado", e o gesto natural diante disso é
  mandar OUTRO link, que é justamente o que o link write-once não aceita.
  ⚠️ **A regra que fica: quando um dado só existe no ARMAZENAMENTO EXTERNO, a rotina que o
  limpa é a rotina que o destrói.** A ordem passou a ser **colher → guardar → só então
  apagar**, e a limpeza não cancela mais quem já respondeu: o que venceu foi o LINK, e o
  link já cumpriu o papel dele. Coleta respondida sai do ar e continua na fila de
  conferência.
  ⚠️ **O traço reusa o `TracoAssinatura` do balcão** (tabela à parte, bytes fora da linha) e
  as RESPOSTAS vão junto no mesmo `SaveChanges`: guardar o traço sem as declarações seria
  meia recuperação — a conferência traria a assinatura com o formulário em branco, e quem
  responderia pelo paciente seria a técnica.
  ⚠️ **A janela deixou de ser o caminho crítico**: `SincronizarRespostasAsync` é varredura
  de fundo chamada pela **lista do dia do balcão** (que já relê a cada minuto) e pela
  limpeza. Ela **NUNCA lança** — balde fora do ar ou traço ilegível de UMA coleta viraria
  agenda do dia em branco; vira log, e a janela do termo, onde alguém está de fato
  esperando, continua dizendo o erro por extenso. O custo normal é UMA consulta indexada.
  ⚠️ **E "assinado" ganhou selo PRÓPRIO na lista** (`Termo assinado — conferir`, âmbar). Os
  dois estados são "o termo não está cumprido" e mandam fazer coisas OPOSTAS — um pede
  colher a assinatura, o outro pede um clique sobre uma que já existe. Enquanto eram o
  mesmo selo vermelho, a tela mandava a recepcionista repetir o gesto que não funciona; e
  gastar o vermelho aqui ensina a ignorá-lo no caso em que ele importa. O rótulo do "⋯"
  segue o selo pela mesma razão, e `EnviarAsync` **RECUSA** o reenvio para quem já assinou,
  dizendo o que fazer.
  ⚠️ **A CONDIÇÃO da varredura é "não tem assinatura GUARDADA", nunca "não respondeu"** — e
  a diferença é o dia da atualização: a coleta que a versão anterior deixou respondida tem
  carimbo e nada guardado dentro, e era justamente a primeira que a limpeza destruiria. Pela
  mesma razão `ColherRespostaAsync` responde do banco **com queda para o balde**: sem ela,
  essa coleta responderia "não há assinatura" sobre uma que está no ar, com o reenvio
  recusado logo atrás — corredor sem saída. **Ao guardar num lugar novo o que antes vivia
  noutro, pergunte o que a base tem no INSTANTE da atualização.**
  ⚠️ **O objeto só sai do ar quando a assinatura está segura**: se a colheita falhou, a
  limpeza não apaga — a coleta fica no ar mais um dia e a varredura seguinte tenta de novo.
  Apagar ali seria destruir a assinatura pelo caminho exato que a correção existe para
  fechar. E o log carrega o **id**, nunca o TOKEN: ele é a única barreira de acesso ao termo
  publicado, e o log é um `.txt` na pasta da instalação.
  ⚠️ **`MeioAssinaturaPaciente.LinkRemoto` deixou de ser "não implementado" e passou a ser
  GRAVADO.** `ColherAsync` carimbava `NaClinica` em toda assinatura, inclusive nas que
  vieram do celular — o sistema afirmando algo falso sobre como a assinatura foi obtida, no
  único documento cujo valor inteiro é ser evidência. O canal entra também na TRILHA, que é
  o primeiro leitor que a coluna teve. **Valor de enum documentado como "não implementado"
  tem prazo: quando o caminho passa a existir, o comentário vira hedge falsa.**
  **O que NÃO mudou, e é decisão da direção, não esquecimento:** a resposta continua não
  selando nada sozinha — quem confere a identidade, preenche o documento conferido e conclui
  é uma pessoa (parcela 81: *"o papel deste fluxo é tirar o custo do pad, não a pessoa do
  circuito"*). O que a parcela tira do caminho é a JANELA ABERTA, não a conferência.

- **A CORREÇÃO PAROU NUMA PORTA E CINCO CONTINUARAM DIZENDO O CONTRÁRIO** (set/2026, a
  conferência da própria parcela do termo pelo WhatsApp, com CI verde nos três checks e
  2513 testes verdes). O estado novo — "o paciente já assinou no celular, falta conferir"
  — ganhou leitor no selo da lista do dia, e os **cinco outros leitores da mesma
  `SituacaoTermo`** ficaram lendo `Pendente` cru: o `ElegibilidadeService` (que sozinho
  alcança agendamento, check-in, Novo atendimento, ficha e Consultório) afirmava em
  VERMELHO *"Falta o termo «X» assinado pelo paciente"*; a linha e o resumo da ficha
  diziam *"Falta o paciente assinar"* e *"Falta 1 termo assinado"*; a faixa do Consultório
  dizia *"Termo não assinado · colher"*; e o botão das duas telas de enfermagem dizia
  **"Colher"** — o gesto que não funciona, porque o link write-once recusa a segunda
  assinatura.
  ⚠️ **É o defeito que a parcela existia para corrigir, sobrevivendo nas portas que não
  reclamaram** — e cometido pelo commit que escreve, no próprio corpo, que *"a cópia que
  fica para trás é onde a capacidade some"*.
  A regra que fica, e ela é de MÉTODO: **ao acrescentar um ESTADO a um modelo que já tem
  leitores, o `grep` da propriedade ANTIGA é parte da mudança.** Não basta o campo novo
  ter um leitor; a pergunta é *quem lia o estado que eu acabei de dividir, e o que cada um
  desses passou a afirmar?* Aqui eram `s.Pendente` em cinco arquivos, e nenhum quebrava.
  ⚠️ **A frase e a ordem desceram para a Application** (`SituacaoTermo.VerboDaPendencia` /
  `RotuloDaPendencia`, `PendenciasDeTermo.Primeira`): eram quatro telas montando
  `$"Colher: {nome}"` à mão e quatro escolhendo qual pendência oferecer. Quatro cópias
  divergem na primeira correção — e o que decide o que a tela AFIRMA precisa morar onde o
  `dotnet test` alcança (a regra da `GradeSemana` e do `ResumoSessaoAnterior`).
  ⚠️ **Contar por SUBTRAÇÃO é armadilha**: o resumo da ficha nasceu com
  `Pendente - aConferir`, que fica NEGATIVO no dia em que um dos dois deixar de implicar o
  outro — e número negativo ali vira uma frase sem sentido. Conte direto
  (`Pendente && !AguardaConferencia`).
  ⚠️ E o alerta novo é **ÂMBAR, com valor próprio de enum**: `TemImpedimento` só olha o
  vermelho, e o vermelho é de quem não assinou nada — gastá-lo em quem está a um clique de
  terminar ensina a ignorá-lo no caso em que importa. Valor próprio porque um futuro
  consumidor que roteie pelo enum abriria a porta de COLHER para quem precisa CONFERIR.

- **UMA REGIÃO TEM DUAS BARREIRAS: a que a CARREGA e a que a DESENHA** (set/2026, a
  conferência da própria parcela antes de ela ir a produção). A correção de "quem colhe
  alcança o que colheu" trocou o bit da CARGA dos termos do dia na ficha
  (`CarregarTermosAsync`) e deixou intacto o `TemTermoDoDia`, que é o que o XAML amarra na
  `Visibility` do cartão. Resultado: para a recepcionista a consulta passou a rodar e a
  região continuou **`Collapsed`** — trabalho pago e nada na tela. O comentário da carga
  dizia, por escrito, *"nem ler nem desenhar: `TemTermoDoDia` fica falso e a região SOME"*,
  descrevendo um acoplamento que a própria mudança tinha desfeito.
  ⚠️ **Nenhuma rede pega**: o XAML é bem-formado, o binding é válido, o C# compila e nada
  lança — e as duas barreiras ficam a 700 linhas uma da outra, em arquivos diferentes.
  A regra que fica: **ao trocar a regra de acesso de uma região, o `grep` da SEGUNDA
  barreira é parte da mudança** — a que decide se ela é DESENHADA. As duas passaram a sair
  do MESMO `CentralDocumentosService.AcessoParaVer`, que é o que impede de divergirem de
  novo; barreira escrita à mão ao lado de uma lida do catálogo é a cópia que fica para trás.
  ⚠️ E na mesma conferência: o botão da faixa do Consultório ganhou a FRASE nova e
  continuou **vermelho** (`BotaoSecundarioPerigo`) nos dois estados, enquanto as outras
  quatro portas pintam o termo já assinado de âmbar. **Trocar o texto de um aviso sem
  trocar o PESO dele é meia correção**, e aqui ela contradizia a regra escrita no commit
  anterior — o vermelho é de quem não assinou nada. Resolvido com estilo local
  `BasedOn="{StaticResource BotaoSecundarioPerigo}"` + `DataTrigger` sobre os tokens de
  aviso, sem componente novo no design system.

- **VENDER PACOTE: a lista de pacientes tinha 8 px, e sem escolher o paciente não se vende
  nada** (set/2026 — o cliente mandou o print: com "lucas" digitado, o primeiro resultado
  aparecia como uma FATIA de oito pixels, cortado ao meio). `PacoteVendaWindow` é um
  `DockPanel` em que o formulário inteiro — pacote, data, valor, forma de pagamento,
  parcelas, observações — estava `Dock="Bottom"`, e a lista de resultados era o filho que
  PREENCHE. **Num `DockPanel` o ANCORADO leva a altura que pede e quem paga é o fill**: o
  formulário soma ~500 px numa janela de 690, e o que sobrou para a lista foi o resto.
  ⚠️ **É a parcela 79 pelo AVESSO, e é pior.** Lá o filho ancorado que não cabia era
  DECEPADO — dava para ver o estrago. Aqui quem some é o fill, e o que se vê é uma janela
  bonita, com todos os campos no lugar, em que **a venda simplesmente não acontece**: sem
  paciente escolhido o `Vender` recusa, e a pessoa não tem como saber por quê. Nada falha,
  nada avisa, e as três redes e o CI ficam verdes — o XAML é bem-formado, o
  `compilar-sombra` não lê o corpo e nada lança.
  A regra que fica: **quem cresce com o DADO é que precisa de teto e de rolagem; quem tem
  altura conhecida é que se ancora.** Aqui a lista ganhou `MaxHeight="150"` e virou
  `Dock="Top"`, logo abaixo do campo que a produz, e o formulário virou o fill **dentro de
  um `ScrollViewer`** — ele rola em vez de ser cortado. A pergunta que decide não é "o que
  é mais importante?", é *"qual destes dois eu não sei o tamanho?"*.
  ⚠️ **E a lista SOME quando não há resultado** (`Seletor.TemResultados`): caixa vazia
  reservada no meio de um formulário se lê como lista que não carregou — é a lição dos três
  formulários que ganharam `SemBuscaInicial`, e o custo previsto ali é o mesmo aqui.
  ⚠️ **Quem foi escolhido é dito por extenso**, num crachá logo abaixo: com o formulário
  rolando, a linha marcada dentro da lista sai de vista, e "a linha está azul lá em cima"
  não é resposta — vender o pacote no nome errado é dinheiro registrado para quem não
  comprou, e desfazer isso é cancelamento de venda, do Financeiro.


- **DOCUMENTOS EM TRÊS PASSOS — e o passo 3 só promete o que entrega** (set/2026; mockup 5
  de `docs/mockups/documentos-depois-de-escolher.html`, escolhido pela direção entre cinco).
  O print da clínica mostrava a tela **acumulando** em vez de mudar de estado: paciente
  escolhido, e a lista de quatro resultados continuava aberta, as linhas saíam em escada e
  uma caixa azul de largura inteira dizia uma coisa só — o nome. As onze folhas, que são o
  assunto, ficavam abaixo da dobra.
  O desenho é o do **Novo atendimento**, que a clínica já aprovou: QUEM · QUAL PAPEL · O QUE
  VAI SAIR. O passo cumprido RECOLHE — a busca some e vira a faixa de identidade, com os
  campos que decidem se o papel pode sair naquele nome (documento, idade, convênio, o
  horário de hoje).
  ⚠️ **O cartão passou a ESCOLHER, e escolher NÃO exige que dê para emitir.** A folha cuja
  exigência não está cumprida é justamente a que precisa ser escolhida para o passo 3 poder
  EXPLICAR o que falta; recusar ali devolveria o botão que não faz nada da parcela 41. A
  barreira fica no passo 3, onde o ato acontece.
  ⚠️ **O passo 1 é um passo, NÃO um portão.** Duas folhas não são de pessoa nenhuma (o
  fechamento do período e o recibo, que navega ao Caixa), e um assistente que exigisse o
  paciente as tornaria inalcançáveis — a regra 3 do faturamento, que a parcela 88 já
  invocou para deixar esta tela de fora da busca obrigatória. O passo 2 existe sempre.
  ⚠️ **DESVIO DECLARADO DO MOCKUP APROVADO: o passo 3 não mostra o NÚMERO da folha.** O
  desenho trazia "2026/0189"; o número é atribuído na EMISSÃO, por ano, e adivinhá-lo
  escreveria na tela um número que a próxima emissão concorrente torna falso. É a prévia da
  guia outra vez — ela não tem número porque ele nasce na baixa, e a tarja diz isso.
  **Desvio de mockup aprovado se escreve, não se comete em silêncio.**
  ⚠️ **O que o passo 3 acrescenta de verdade é a frase que o produto nunca teve: o que o
  CLIQUE faz.** Quatro folhas não imprimem nada ao serem clicadas — uma navega para o
  Caixa, duas abrem a coleta de uma assinatura, uma abre a janela onde se escreve — e a
  leitura natural de um botão chamado "Emitir" é que o papel saiu. A frase sai da MESMA
  `ExigenciaFolha` que ROTEIA a emissão de verdade: duas definições de "o que este botão
  faz" divergiriam na primeira correção, e a que ficaria para trás é a da TELA, isto é, a
  que a pessoa lê.
  ⚠️ E a DESCRIÇÃO da folha voltou para a tela: nos cartões ela vive na dica do mouse, e
  dica é o que ninguém lê — quem não conhece a folha descobre o que ela é ERRANDO, e o erro
  aqui custa um cancelamento com motivo.
  ⚠️ **`PreviaDaFolha` mora na Application e é PURA**, com onze testes — inclusive um que
  percorre o CATÁLOGO INTEIRO em vez das folhas que eu lembrei: é o que faz a folha nova
  cobrar a própria cobertura, como o `Os_sete_documentos_geram_PDF` cobrou quando nasceu o
  oitavo tipo. O que a tela AFIRMA não pode morar num projeto WPF, onde o `dotnet test` não
  alcança.
  ⚠️ **`{Binding Garantias}` NÃO alcançaria a constante.** A frase das garantias nasceu
  `const`, e binding do WPF não alcança membro ESTÁTICO: o texto sairia vazio, sem erro e
  sem aviso, verde em todas as redes. Ela ganhou uma propriedade de instância ao lado.
  ⚠️ **`CornerRadius="999"` num círculo de tamanho FIXO é a parcela 91 de novo** — o WPF não
  trava o raio na metade da altura como o CSS. Os três números dos passos são 19×19 e usam
  raio explícito (9,5); `Ajudantes.Pilula` é para o que TEM largura variável.
  ⚠️ **Dois textos que apontavam LUGAR já estavam mentindo, e a reforma os expôs:** a
  pendência dizia "Escolha o paciente **ao lado**" (ele está acima, no passo 1) e "Usa o
  período escolhido **abaixo**" — e o período saiu desta aba na **parcela 82**, quando ela
  virou duas. Texto espacial é o primeiro a apodrecer numa reforma de leiaute, e ele não
  aparece em rede nenhuma.
  ⚠️ E a faixa diz **"sem convênio"**, a MESMA palavra que a linha da lista usa dois
  segundos antes — o nome de catálogo da ficha "a definir" tem 33 caracteres, e dois jeitos
  de dizer o mesmo fato na mesma tela fazem a pessoa procurar a diferença que não existe.
  **O que se perdeu, e é o custo aceito do desenho:** emitir virou TRÊS cliques onde eram
  dois, e a declaração de comparecimento — o papel mais corriqueiro do balcão — paga esse
  clique todo dia.


- **"DOIS OU MAIS CAMINHOS LEVAM AO MESMO LUGAR" — o recall tinha TRÊS motores e a agenda
  tinha seis botões de marcar** (out/2026, pedido da direção sobre a PR 250). Nada falhava;
  cada porta fazia o seu pedaço, e a pessoa abria as três para descobrir a diferença.
  ⚠️ **Recall: uma fila só.** "Quem parou de vir" (Gerente, `RetencaoPacienteService`), a
  rodada de recall do `CampanhaService` e a `RetornoView` (sem rota desde a navegação
  unificada) respondiam a MESMA pergunta do Recall do Acompanhamento — com duas definições
  de "sumido" e sem o registro do contato. Saíram inteiras, com os métodos de repositório
  que só elas chamavam. O que tinham de PRÓPRIO foi para a fila que fica: o **tratamento
  longo** (`LinhaAcompanhamento.TotalSessoes`, contado no banco, com condição e atalho), o
  **pacote em aberto** (já existia) e o **CSV** — com a coluna de quem pode receber
  mensagem, senão a planilha vira lista de disparo. **Ao aposentar uma tela redundante,
  liste o que ela fazia MELHOR e leve junto**; o resto é regressão vestida de limpeza.
  ⚠️ **A configuração "Recall: dias sem vir" era gravada e NINGUÉM a lia** — o Acompanhamento
  usava 60 fixo. É a configuração que não faz efeito, e ela só apareceu porque a tela que a
  "usava" saiu: a fila passou a lê-la. **Ao remover um leitor, procure os campos que só ele
  parecia ler — e confira se ele os lia de fato.**
  ⚠️ **Atalho para a aba vizinha é caminho duplicado.** Campanhas tinha "Abrir fila de
  recall" (a aba ao LADO, no mesmo item) e "Abrir confirmações"; a Grade abria uma JANELA
  própria de Confirmações com o mesmo ViewModel da aba; a Visão geral tinha um combo
  "Outras operações" repetindo as abas de Operações da agenda. Os atalhos saíram, a janela
  saiu (o botão da Grade agora NAVEGA), e o mesmo ato passou a ter uma pergunta só (o
  Cancelar da lista do Dia ganhou a confirmação que a Grade já fazia).
  **Marcar é UM botão por tela**, com o nome que a sidebar usa ("Marcar atendimento") e o
  pré-preenchimento do dia: eram dois lado a lado no Dia e dois na Grade, levando à mesma aba.
  ⚠️ **Script de remoção de membro por chaves balanceadas erra o expression-bodied** que
  tem `new { ... }` no meio: ele parou no `}` do tipo anônimo e deixou o resto do método
  solto — o `compilar-sombra` acusou em segundos. Depois de remover código por script,
  leia o diff do arquivo inteiro (a lição da parcela 95, outra vez).
  **Ficou para a rodada de UI, por decisão da direção**: o redesenho visual (Acompanhamento
  incluído), juntar Início e Visão geral, unificar a fórmula de ocupação dos dois e a faixa
  de pedidos da enfermagem dentro da Grade (que tem o salto para o dia do pedido).

- **A RODADA DE UI DA RECEPÇÃO — quatro telas refeitas a partir de mockup aprovado, e o
  que a fidelidade custou** (out/2026; mockups em canvas aprovados antes de uma linha de
  WPF, com a promessa de entregar "fielmente igual" e declarar todo desvio).
  ⚠️ **Início**: saudação + quatro números + "próximos a chegar" + o que é do balcão. Tudo
  o que a tela AFIRMA mora em `InicioDoBalcao` (Application, pura): a situação da chegada
  só diz "Confirmou/Não confirmou" quando a rodada disse (sem contato não há selo), e
  "Confirmar amanhã" é NULO sem rodada — contar zero ali afirmaria que ninguém precisa de
  aviso. A ocupação vem do `IOcupacaoAgendaService`, a mesma da agenda.
  ⚠️ **Pacientes**: os chips de recorte e a página usam o MESMO filtro no banco
  (`AplicarRecorte`) — contagem de chip calculada por outra consulta divergiria do que o
  clique mostra. Visitas (última e próxima) vêm em LOTE para a página, nunca por linha.
  ⚠️ **Ficha do balcão ≠ ficha clínica.** A Recepção abria o workspace do Consultório, que
  responde "o que foi feito com este paciente"; o balcão pergunta quem é, quando volta, se
  o convênio está em dia e o que falta assinar. `FichaBalcaoView` reusa o
  `FichaPacienteViewModel` inteiro (mesma leitura, barreiras e trilha) e o prontuário
  fica a um botão, para quem tem `VerProntuario` — tirar a porta seria a regra 3 do
  faturamento. A linha do tempo do balcão é ADMINISTRATIVA (sessão, contato, papel): a
  evolução é dado de saúde e não entra. Sessão sai sem hora porque o atendimento guarda só
  o dia — inventar hora seria a sequência fabricada da parcela 72.
  ⚠️ **Marcar**: as horas viraram pílulas (`PilulasDeHorario`, pura): passo = duração da
  sessão, jornada declarada ou 8h–18h presumidas, hoje começa agora, e a OCUPADA não some
  (fica tracejada e clicável — a agenda avisa e não impede). O campo "Outro horário"
  continua: pílula é atalho, não teto.
  ⚠️ **"Colher termo" saiu do balcão** (Dia e Início), a pedido da direção: a enfermagem
  já o colhe. O SELO de termo pendente fica — é informação para quem recebe; o que saiu
  foi a porta duplicada.
  A lição de método: **fidelidade a mockup não é copiar o que o desenho mostra, é
  entregar o que ele promete com os dados que o sistema TEM** — onde falta dado (busca
  por telefone, hora da sessão realizada, histórico de pagamento por pacote), o desvio é
  escrito na entrega com o motivo, nunca preenchido com algo que pareça certo.

- **O LAUDO MÉDICO, e o que "alterar" quer dizer num documento que não se reescreve**
  (out/2026 — a clínica: *"escrever e ficar registrado caso precise emitir novamente ou
  alterar"*; a direção: *"em emitir dentro do atendimento, sem nova aba"*).
  `TipoDocumentoClinico.LaudoMedico` é um papel ESCRITO, com os bits do atestado
  (`VerProntuario` para ler, `Prescrever` para emitir) e o CID sob a MESMA autorização
  expressa. A porta é o botão "Laudo médico" no EMITIR do atendimento, e ele aparece
  sozinho na régua das Prescrições e na central porque as duas leem o catálogo.
  ⚠️ **"Emitir novamente" já existia (2ª via idêntica); "alterar" não podia ser
  reescrever.** `EmitirNovaVersaoAsync` numera a versão nova e CANCELA a anterior no
  MESMO `SaveChanges`, com o motivo nomeando o número que a substituiu e a trilha
  `DocumentoClinicoSubstituido`. Em duas gravações, uma falha no meio deixaria dois
  laudos válidos dizendo coisas diferentes, ou nenhum; e cancelar à mão para emitir de
  novo é exatamente o passo que se esquece. A recusa vem antes de qualquer gravação
  (outro paciente, outro tipo, já cancelado), e o teste prova que a versão recusada não
  cancela a anterior.
  ⚠️ **Só o laudo** (`TipoDocumentoInfo.PermiteNovaVersao`): receita e atestado valem
  para um dia e se corrigem como sempre. O ato mora em `AcoesDoDocumento.NovaVersaoAsync`
  e entra no "⋯" por um comando OPCIONAL no fim de `ComandosDoDocumento` — as telas que
  não listam laudo continuam compilando sem saber dele.
  ⚠️ **O texto do laudo nunca é herdado da sessão**: copiar a conduta para o papel que
  vai à perícia seria decidir pelo médico o que a perícia lê. Herda só o CID.
  ⚠️ E a lição de leiaute da mesma rodada: **em `DockPanel`, filho que não é o último
  ganha `Dock=Left` por padrão** — o botão de Treinamento declarado sem `Dock` antes do
  menu ficou à esquerda dele e foi empurrado para fora da janela em 880 px. Quem ancora
  à direita declara `DockPanel.Dock="Right"` e vem ANTES do filho que preenche.

- **A TELA DE DOCUMENTOS É UMA SÓ EM TODOS OS MÓDULOS — e tirar o navegador não podia
  trazer a tela antiga de volta** (out/2026; a direção, com o print da tela de produção:
  *"você retrocedeu… ela tem que ser a mesma para todos os módulos"*). A PR 250 tirou o
  WebView2 e devolveu "Receitas e documentos" e "Prescrição de infusão" ao XAML que existia
  ANTES da versão em HTML — a régua de botões soltos e a lista corrida. A clínica já usava o
  desenho HTML (sobretítulo, título e marca; "Selecione o paciente" com a faixa de acento;
  cartões de papel em três colunas; histórico com contador) e leu o XAML como regressão.
  ⚠️ **Ao trocar a TECNOLOGIA de uma tela, o desenho que está em produção é o contrato, não
  o XAML que havia antes dela.** Converter de volta "para o que existia" é restaurar a tela
  que a clínica já tinha aposentado.
  Agora o desenho mora no shell (`Styles/Componentes/PaginaDocumentos.xaml` +
  `PacienteDaPaginaView`) e as TRÊS páginas o usam — Receitas e documentos, Prescrição de
  infusão e a central de Documentos (Recepção/Gerente), que perdeu as duas abas e virou a
  mesma página com o histórico embaixo. Os cartões saem de `DocumentosDoConsultorio.Cartoes`
  e o ícone de `DocumentosDoConsultorio.Icone`: duas listas escritas à mão divergiriam na
  primeira folha nova.
  ⚠️ **"Sempre leva ao mesmo papel": a ficha abria a janela de emissão num tipo PADRÃO, com
  o tipo escondido atrás do link de dados.** Agora ela pergunta (`EscolhaDeDocumentoWindow`,
  os mesmos cartões) e a janela de emissão mostra o tipo como régua de abas no topo, trocável
  em um clique. Uma folha branca com editor é parecida em todos os tipos — tipo que só se vê
  no título é tipo que a pessoa não confere. `CadaPapelAbreOSeuTests` prende chave → tipo
  das três portas.

- **🎨 O FINANCEIRO INTEIRO VIROU BLAZOR — e a migração achou defeito que o WPF escondia**
  (out/2026; a direção, depois de ver o piloto: *"Simplesmente perfeito! … refazer todo o
  visual do módulo financeiro dessa forma"* e *"todo o financeiro, telas e formulários"*). As
  14 telas (Caixa, Fechamento, Fluxo, Contas, Quem me deve, Plano de contas, Recebíveis,
  Conciliação, Extrato do banco, Taxas e impostos, Estoque, Repasses, Resultado do mês e
  Produção) e ~20 formulários foram reescritos em `Clinica.Ui.Blazor` sobre os MESMOS
  serviços; `CriarTela` aponta para `TelaBlazor`. **As telas, janelas e ViewModels WPF do
  Financeiro foram REMOVIDOS** a pedido da direção (*"deixaremos só o Blazor"*), junto com a
  ferramenta `tools/validar-design-financeiro` e os trechos das validações do Windows que
  montavam aquelas janelas — o que era REGRA nelas (modo de materiais, baixa pendente) ficou,
  conferido pelo serviço. **Pacotes ficou em WPF** de
  propósito: é a tela do shell que a Recepção também publica, e migrá-la num módulo só daria
  duas telas para a mesma chave no Gerente.
  ✅ **Como foi feito, e é o roteiro para o próximo módulo**: primeiro a BASE (componentes,
  `TelaBase`, `Dialogos`, `Notificacoes`, `IHostDaTela`, prévia com `AddClinica` e sementes
  por reflexão), depois UMA tela de escrita completa como MODELO (o Caixa, com quatro
  formulários), depois o guia (`docs/telas-blazor.md`) — e só então o resto em paralelo, cada
  frente num worktree, com arquivos que não se cruzam (tela, formulário, `telas/<x>.css`,
  semente). Nenhuma frente editou arquivo comum, e o merge foi cherry-pick sem conflito.
  🐛 **Reescrever lendo o ViewModel inteiro achou o que três redes e 3000 testes não viam**:
  a faixa da alíquota única que sumia enquanto continuava cobrando (condição diferente da do
  serviço — a lição da parcela 69 sobrevivendo NUMA linha); editar taxa, tributo e conta fixa
  APAGANDO as observações (a cópia campo a campo, lugar 3, em três formulários); o percentual
  zero que a tela pedia e a leitura recusava; o resumo de Contas contando vencidos a receber
  sob "Só a pagar"; a falha do "Gerar fixas" que a recarga apagava antes de aparecer; o "Nova
  regra" de repasse sem botão; e a auditoria da receita de guia gravando "?" no operador.
  **Migração de tela é auditoria de graça — desde que quem migra leia o ViewModel inteiro, e
  não só o XAML.**
  ⚠️ **Três armadilhas que a base comum cobrou e agora estão escritas no guia**: (a) classe de
  TOM com nome de componente — `.aviso` era o alerta, e o `Selo Tom="aviso"` herdou padding e
  borda e saiu inchado; os tons viraram `tom-*`; (b) `.icone{height:16px}` sem seletor de
  elemento achatava o BOTÃO que também tinha a classe `icone` — virou `svg.icone`; (c) o guia
  mandava ler quantidade com `TentarLerQuantidade`, que lê em pt-BR primeiro e transforma
  "2.5" em 25 (o ponto é milhar) — o WPF do estoque usava `TentarLerNumeroExato`. **Regra de
  guia vale o mesmo que regra de código: ela é copiada por todo mundo de uma vez**, e uma
  instrução errada nele espalha o defeito para N telas no mesmo dia.
  ⚠️ **A prévia precisa da cultura da clínica** (`DefaultThreadCurrentCulture = pt-BR`): os
  serviços formatam mensagens na cultura do processo, e em Linux a invariante mostrava
  "¤15.00" — um defeito que não existe no app, escondendo os que existissem. O que a TELA
  formata continua em pt-BR FIXO (`Formato`), porque a web de leitura roda em Linux de verdade.
  ⚠️ **Parâmetro de componente interativo da prévia tem de ser serializável**: um `Type` não
  é; a rota passa o NOME da tela e um componente de dentro resolve o tipo.
  ⚠️ **`pkill -f` com um trecho do próprio comando mata o shell que o roda** — foi assim que
  duas rodadas de fotos morreram com código 144. Pare a prévia pelo pid (`subir.sh` cuida).
  🔁 **Voltar à aba relê** (`SinalDeVolta` → `TelaBase.AoVoltarAsync`), o equivalente do
  `IAtualizarAoVoltar`: quem avisa é o `Palco`, ao reativar uma tela já montada. Sem isso a aba guardada voltaria com o lançamento da outra máquina invisível.
  🔁 **Trocar de tela muitas vezes, medido em três lugares** (out/2026, pedido da direção
  depois do vídeo): o palco mantém toda tela montada, então defeito de troca NÃO some ao
  fechar — acumula. `PalcoTrocaRepetidaTests` (bUnit, todas as telas reais em lotes de ~15, 5 rodadas + rajada),
  `tools/PreviaBlazor/trocas.js` + `analisar.py` (Chromium, quadro a quadro) e
  `ValidarLayoutWindows --blazor` (o preto do WebView2, na tela real do runner Windows, no
  CI do PR). Acharam DOIS restos: o **Extrato do banco não recruzava ao voltar** (mostraria
  como pendente o que a outra máquina conciliou) e **o diálogo aberto ficava por cima da
  próxima tela** — o serviço é do navegador, e "Sim, continuar" agiria sobre a tela que
  saiu; trocar de tela agora DESISTE (seguro: "desisti" nunca grava, checagem 39).
  ⚠️ **Toda medição foi provada com o defeito** antes de valer: o teste reprova sem a
  correção do Extrato, o `analisar.py` reprova com `FLASH_DE_TESTE=1`, e o `--blazor`
  nunca dá "zero flash" com a câmera cega: no runner a captura da tela NÃO vê o WebView2
  (uniformidade 0,99), então ele pergunta ao próprio navegador se desenhou — sim vira AVISO
  "não medido" no PR, não vira aprovação. E a análise errou uma vez por MIM: 7
  "quadros sem tela" eram a tela WPF ainda valendo (o clique chega ao servidor depois do
  seguinte) — **antes de chamar de defeito, olhe os quadros vizinhos**.
  **O que fica para depois, com o motivo**: aquecer o navegador no login (a primeira tela
  do dia ainda espera o navegador nascer) e o menu "⋯" dos cartões de número, que o `Kpi`
  ainda não tem. (O "um `BlazorWebView` por aba" que estava aqui foi pago na rodada da tela
  preta — ver o 🕰️ da seção de telas.)

- **🎨 O FATURAMENTO DE GUIAS VIROU BLAZOR — e "nada se perde" foi medido, não prometido**
  (out/2026; a direção, depois do Financeiro aprovado: *"se certificando que nada irá ser
  perdido ou nada irá deixar de funcionar"*; o mapa está em `docs/faturamento-blazor.md`).
  As nove telas (Pendências, Consultar guias, Faturados, Glosas, NC, Lotes TISS, Relatórios,
  Configurações e o Resumo, que mora no Gerente) e as onze janelas viraram Razor sobre os
  MESMOS serviços, em cinco frentes paralelas, cada uma devolvendo a LISTA DE PARIDADE — cada
  comando, filtro, coluna, atalho, exportação e permissão do ViewModel WPF → onde ficou.
  ⚠️ **Continuam WPF, de propósito, as duas janelas da ABERTURA** (aviso de pendências e a
  rodada BLOQUEANTE): elas travam o app antes de a área de trabalho existir, e um formulário
  dentro do navegador trava só a tela dele. A REGRA da rodada é uma só
  (`DecisoesDaRodada`, Application, com teste), chamada pela janela e pelo painel.
  ⚠️ **A hospedagem subiu para `Clinica.Desktop.Blazor`**: o dicionário "um navegador por
  janela" é estático POR CLASSE, e uma cópia no Faturamento daria dois navegadores (e um flash
  a mais) na janela do Gerente, que carrega os dois módulos.
  ⚠️ **As perdas que a migração quase produziu, e que só a lista de paridade mostrou**: os
  atalhos Ctrl+S/P/F5 (com o navegador em foco o WPF não recebe a tecla — e o F5 recarregaria
  a página; agora `TelaBase.AoAtalhoAsync`), o menu "⋯" dos cartões de número (já faltava no
  Financeiro; agora mora no `Kpi`), a ficha do paciente a partir da pendência
  (`AbrirFichaDoPacienteAsync`, a MESMA ficha do shell por cima da tela) e — a mais cara — o
  lote TISS: o WPF perguntava o destino ANTES de criar o lote, e a primeira versão Blazor
  criava primeiro e salvava depois, gastando o número da sequência da ANS quando a pessoa
  desistia da janela, e mandava vários lotes num .zip que a operadora não espera.
  **Ao trocar a tecnologia de uma tela, o que mais se perde não é o que ela mostra — é a
  ORDEM em que ela faz as coisas.**
  🐛 **E reler o ViewModel inteiro achou de novo o que as redes não viam**: o retorno do lote
  regravava a glosa já registrada com motivo VAZIO e reiniciava o prazo de recurso; o XML de
  recurso saía pelo Ctrl+P sem a segunda barreira; a guia em NC aparecia como "Aberto —
  faturar em…"; "Salvar configurações" aparecia nas abas que ele não grava; excluir o
  "Particular" apagava em silêncio o nome que a clínica deu (o catálogo o recria). Todos
  corrigidos nas telas novas, com teste.

- **A COMISSÃO DE RECUPERAÇÃO É APURADA DOS FATOS, NUNCA MARCADA** (out/2026; o mapa está em
  `docs/comissao-recuperacao.md`). Atendente assume quem sumiu (fila do Acompanhamento, Recepção
  e Faturamento) → paciente volta → conclui a sessão → a guia nasce → a gestão VALIDA no Gerente.
  Quem atende não vê valor; o valor (`RegraComissaoRecuperacao`, com vigência, o mais específico
  ganha) e a verificação são da gestão (`VerFinanceiro`/`EditarFinanceiro`).
  ⚠️ **O responsável PADRÃO da indicação de BSV não é "a atendente assumiu"**: a configuração
  distribui o caso com `ResponsavelId` preenchido no próprio registro de abertura. Contar esse
  registro pagaria comissão a quem não fez nada — só conta registro feito pela própria pessoa
  (assumir ou contato). O primeiro teste do "ninguém assumiu" PASSAVA com o defeito, porque o
  recall abre sem dono; quem o pegou foi um teste pelo caminho do BSV. **Teste de exclusão tem de
  exercitar o caminho em que o dado excluído EXISTE.**
  ⚠️ Validar copia o valor; quem recebe não valida a própria; recusar e desfazer exigem motivo e
  não apagam (índice único filtrado em `CanceladaEm IS NULL` deixa decidir de novo). A tela
  calcula e valida — não lança pagamento no caixa, por decisão.
  ⚠️ **`dotnet ef` instala neste ambiente** (`dotnet tool install --global dotnet-ef --version
  8.0.11`, pelo proxy do NuGet): migration gerada pela ferramenta, com Designer e snapshot
  certos, em vez de escrita à mão.

- **A FASE 3 RECEPÇÃO×CRM** (23/10/2026; mapa e comparação em `docs/fase3-recepcao-crm.md`). O
  Clinica é a verdade dos dados e das regras (quem está no recall, quantos dias, quem autorizou);
  o CRM é o único que fala com o paciente. (1) **Autorização de mensagens no termo do BSV**: todo
  termo EXIGIDO para o BSV ganha, na EMISSÃO (`DocumentoClinicoService.AutorizacaoDeMensagensAsync`,
  as duas portas: balcão e tablet), a declaração opcional `ModelosTermoBsv.AutorizacaoDeMensagens`
  com `Codigo = ComunicacaoEMarketing` — na emissão e não no modelo, para não reescrever o texto
  que o advogado da clínica aprovou. Só é perguntada a quem NUNCA respondeu sobre mensagens;
  "Sim" grava o consentimento com o número do termo, "Não" grava a recusa e **nunca revoga**;
  "Não" não acende alerta (`RespostaDeclaracao.RequerAtencao`) e o "Marcar todas como Sim" não a
  marca. (2) **Recall com trava**: a ponte só devolve a fila de recall (`Acompanhamentos` tipo
  Recall), bloqueia "Sem autorização de mensagem" e usa os dias do Gerente (`InactivityDays`).
  (3) **Um PARAR só**: o PARAR do CRM vira `POST /crm/v1/consent/optout` e revoga a autorização
  de toda ficha com aquele telefone. (4) **Um SMS só**: o Clinica não manda mais SMS.
  ⚠️ **A fila de recall só se renova quando alguém abre o Acompanhamento** (é lá que
  `GerarRecallAsync` roda, com os dias do Gerente). A ponte só LÊ a fila; se ninguém abrir a tela,
  o CRM fica sem convidados novos — falha segura, nunca mensagem a mais.
- **O CANAL DE SMS, a confirmação por link e "não vem" como resposta cotidiana** (⛔ SAIU na fase 3
  Recepção×CRM, 23/10/2026 — o que segue é histórico; o mapa está em `docs/canal-sms.md`). Lembrete 24h antes (`ConfirmacaoPorLink.MomentoDoLembrete`:
  sábado e domingo adiantam para a sexta, porque ninguém liga o computador no domingo), link
  `/c/TOKEN` no MESMO Worker das receitas, resposta write-once pelo balde — o Worker nunca toca o
  banco. É a MESMA rodada de confirmação do balcão (o mesmo contato, a mesma origem `AGD:`), com
  outro canal.
  ⚠️ **SMS é cobrado, então a idempotência é de BANCO**: a linha de `MensagemSms` é gravada ANTES
  do envio com `ChaveIdempotencia` única, e a segunda máquina (Recepção e Gerente rodam o relógio)
  esbarra no índice. Reconhecer a duplicidade do SQLite pelo NOME do tipo, porque o pacote do
  SQLite é condicional no csproj e não existe no build Release.
  ⚠️ **Um acento só triplica o custo**: fora do GSM-7 o segmento cai de 160 para 70 caracteres.
  O texto que sai passa por `TextoSms.ParaGsm`, e o lembrete cabe em UM segmento com o link
  (token de 12 caracteres, não os 26 das receitas).
  ⚠️ **Status sozinho chamava de "Confirmou" quem respondeu que NÃO vem.** Cinco leitores
  (agenda, lista do dia, Início, ficha, painel) traduziam `Respondido` → "Confirmou", e só a tela
  de Confirmações lia o comentário. Com o link do SMS, "não vou" virou resposta de todo dia. O selo
  virou UMA função (`RodadaDeConfirmacao.SeloDaLista`), alimentada por `RespostasDeConfirmacaoAsync`
  (status + comentário). **Ao acrescentar um VALOR a uma resposta que já tem leitores, procure
  quem resume a resposta pelo status** — eles não quebram, só passam a mentir.
  ⚠️ **"Não vem" NÃO cancela sozinho**: vira selo vermelho, e a recepção liga, remarca e oferece a
  vaga na mesma ligação. Com o SMS ligado, o e-mail da abertura CEDE para quem tem celular — senão
  ele tiraria o contato de pendente dias antes e o SMS, que a clínica paga, nunca sairia.
- **A RESPOSTA DO PACIENTE VIRA AVISO NO SINO E NO PAINEL DA DIREÇÃO** (out/2026; o pedido: a
  confirmação, o "não vou" e o silêncio do SMS só apareciam em Relacionamento → Confirmações,
  tela que ninguém deixa aberta). UMA decisão pura (`AvisosDeConfirmacao.Decidir`, Application,
  testada em `AvisosDeConfirmacaoTests`), UMA leitura em projeção
  (`ContatosParaAvisosDeConfirmacaoAsync`, com entrada na rede do Npgsql) e UM serviço
  (`AvisosDeConfirmacaoService`) que o sino do shell (`AvisosConfirmacaoShell`, Recepção e
  Gerente, os perfis do sino) e o `PainelDirecaoService` (`PacientesNaoVem`,
  `LembretesSemResposta` → Confirmações) chamam igual — dois cálculos de "quem não vem" dariam
  dois números para a mesma agenda. As regras:
  ⚠️ **"Sem resposta" é conservador**: contato `Enviado`, COM LINK (`TokenResposta` — só o SMS
  tem; no WhatsApp manual quem registra a resposta é a recepção), enviado há ≥ 3 h, sessão
  futura no horizonte. **O horizonte é o maior entre agora + 36 h e o fim do PRÓXIMO DIA
  ÚTIL**: 36 h puras escondiam, na sexta, a segunda — e o lembrete da segunda sai na sexta
  (`MomentoDoLembrete` adianta o fim de semana).
  ⚠️ **Confirmação marcada pela recepção não é notícia** — só a do paciente (link do SMS ou
  WhatsApp do CRM; o texto do CRM subiu para `RodadaDeConfirmacao.RespostaConfirmouPeloWhatsApp`
  para os dois lados não divergirem). Trinta "confirmou" que ela mesma marcou enterrariam o
  "não vem". **"Não vem" fica, de qualquer origem, até o horário ser cancelado ou remarcado**:
  a consulta só traz sessão `Agendado` na janela, e é isso que "resolve" o aviso.
  ⚠️ **Cada aviso pisca UMA vez, e reabrir o app não repete o dia**: o conjunto de anunciados
  é de memória, e a PRIMEIRA leitura semeia em silêncio o que aconteceu antes da abertura
  (menos 15 min de folga — o relógio do SMS lê o balde a cada 5). O que foi semeado continua
  na lista e no número do sino; só não vira notificação de novo. Gravar o conjunto em disco
  seria mais uma peça (máquina dividida, atualização) para evitar um aviso repetido.
  Clicar no aviso abre as Confirmações NO DIA da sessão (`AberturaDasConfirmacoes`, consumido
  pela tela ao carregar). 🧑‍💻 UX: a frase diz quem, quando e o que fazer ("João avisou que NÃO
  VEM — amanhã 09:00: ligue para remarcar"); o número do sino conta só o que pede ligação.
  🛠️ DX: regra e frases na Application, o shell só lê e desenha. 🤝 CX: uma consulta pequena
  por posto por minuto (e no aviso de `Agendamentos`, no máximo a cada 20 s) — sem migration.
- **"JORNADA" ERA TERMO DE RH, E O DESENHO NÃO CABIA A SEMANA REAL** (out/2026; o pedido:
  *"esse negócio de jornada de trabalho, que porra é essa?"* — mapa em
  `docs/recepcao-ux-out2026.md`). Duas telas editavam o mesmo campo com frases opostas
  ("nenhum dia = qualquer dia" × "= todos os dias"), um horário só valia para todos os dias
  (o almoço e "segunda de manhã, quinta à tarde" não cabiam) e quem não cadastrou aparecia
  como erro. Virou **"Horário de atendimento"** por dia, com faixas
  (`Profissional.HorariosAtendimento`, coluna aditiva; `FaixasEm(dia)` é a pergunta única —
  vazio = não atende, nulo = sem horário/qualquer hora) e UM editor. Os três campos antigos
  continuam gravados (a união) para o binário anterior. ⚠️ **Termo que a casa não usa é
  defeito de UX, não de vocabulário**: a pergunta do balcão é "quando o doutor atende?".
  ⚠️ **Sem horário cadastrado não é erro**, e "não atende" em cada dia do editor vazio
  contradizia a frase "aceita qualquer dia e hora" logo abaixo — com nenhum dia ligado, a
  linha diz "—".
  E a busca de paciente virou UM componente Blazor (`BuscaDePacienteNaTela` +
  `FaixaDoPaciente`) para Pacientes, Marcar e Documentos — que saíram do WPF. Integração
  feita por quatro worktrees com arquivos que não se cruzam; o conflito que sobrou foi um
  `@import` em `clinica.css` e um teste de frase — **tela nova importa o próprio CSS numa
  linha só**, e é essa a única linha comum.
- **TENDÊNCIA E METAS POR RECORTE** (out/2026; `docs/tendencias-e-metas.md`). "Em alta"/"em baixa"
  exigem percentual E diferença mínima (a regra da reincidência de faltas: 1 → 2 é "+100%" e não diz
  nada). Sessões pelo MESMO critério do `IndicadoresService` (status Realizado).
  ⚠️ **A meta de UM profissional era apurada com o número da CLÍNICA inteira** desde a parcela 28:
  `ApurarMesAsync` ignorava `ProfissionalId`. Nada falhava — a meta dele aparecia batida com o
  trabalho de todos. Toda meta de recorte agora sai dos fatos da tendência. **Campo de recorte que a
  apuração não lê é filtro de enfeite.**
  ⚠️ **`a ?? b` entre duas navegações de ENTIDADE não traduz no EF** (`l.Atendimento ??
  l.CodigoFaturamento.Atendimento`); a rede do Npgsql pegou, e o SQLite nunca teria executado a
  consulta porque nenhum teste a chamava. Resolva o ID como escalar e entre por JOIN.
