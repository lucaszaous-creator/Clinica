# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## O que é o projeto

Sistema de **faturamento** (não recebíveis — não há campos de dinheiro) para clínica médica de acupuntura,
em .NET 8, desktop WPF. O coração do produto é impedir que o **2º código/guia** (obtido +24h depois do
atendimento) seja esquecido, via dashboard de pendências com semáforo. Também cobre o ciclo TISS completo:
lote → envio → retorno → glosa → recurso, com XML TISS 4.01 e guia em PDF no leiaute ANS.

Todo o código, comentários, commits e UI são em **português (pt-BR)** — mantenha esse padrão.

## Comandos

```bash
# Testes (multiplataforma — única parte que roda fora do Windows)
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj

# Um teste específico
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj --filter "FullyQualifiedName~RegrasFaturamentoTests"

# Rodar o app (apenas Windows — WPF)
dotnet run --project src/Clinica.Desktop

# Verificação estática da suíte multi-exe (roda em qualquer sistema) — RODE ANTES DE TODO PUSH
python3 tools/verificar-suite.py

# Migrations (usa a env var CLINICA_DB como connection string)
CLINICA_DB="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" \
  dotnet ef migrations add NomeDaMigration -p src/Clinica.Infrastructure -s src/Clinica.Infrastructure
```

⚠️ `Clinica.Desktop` e toda a suíte multi-exe **só compilam no Windows** (`net8.0-windows`). Neste
ambiente Linux, valide mudanças com `dotnet build src/Clinica.Application` / `Clinica.Domain` /
`Clinica.Infrastructure`, com os testes e com `tools/verificar-suite.py` (XAML, pack URIs, chaves do
design system, projetos na solução); o CI (`.github/workflows/build-exe.yml`, runner Windows) compila
os quatro apps em cada push na `main` **e em cada PR para a `main`** — commit em branch de trabalho
não gera build sozinho.

Release: tag `vX.Y.Z` (ou Actions → "Release") dispara `.github/workflows/release.yml`, que empacota
os quatro apps com **Velopack** (um canal por app; o faturamento fica no canal padrão `win` e **nunca
muda**) e publica na mesma release; os apps instalados se auto-atualizam.

## Arquitetura

Camadas clássicas, todas em `src/`:

- **Clinica.Domain** — entidades, enums (`Enums.cs` concentra quase todos) e o **motor de regras**:
  uma classe `Regra<Convenio>` por fluxograma de convênio, todas implementando `IRegraConvenio`
  (`Gerar(paciente, atendimento, contexto) → ResultadoFaturamento` com os códigos, datas previstas e
  categoria/semáforo). `RegistroRegras` resolve a regra pelo convênio; `RegraGenerica` +
  `ConfiguracaoRegraGenerica` atendem convênios personalizados criados em runtime (via
  `CatalogoConvenios`, alimentado do banco).
- **Clinica.Application** — serviços de caso de uso (`Servicos/`) orquestrando o repositório e as regras.
  Ponto único de acesso a dados: `Abstracoes/IClinicaRepositorio`. Os principais: `AtendimentoService`
  (gera os códigos ao lançar atendimento), `PendenciaService` (alimenta o dashboard: 2º códigos,
  consultas a renovar, glosas com prazo de recurso, carteirinhas), `LoteTissService`/`TissExportService`
  (lotes e XML TISS 4.01), `GlosaService`, `ParametrosService` (configuração global no banco, com snapshot).
- **Clinica.Infrastructure** — EF Core + Npgsql (PostgreSQL/Neon), `ClinicaDbContext`,
  `ClinicaRepositorio` (única implementação do repositório), migrations. Migrations são aplicadas
  **automaticamente na abertura do app** (`App.xaml.cs` → `MigrateAsync`).
- **Clinica.Desktop** — WPF/MVVM. `App.xaml.cs` é o bootstrap: auto-update Velopack → obtenção da
  connection string (env `ConnectionStrings__Clinica` → `ConexaoStore` criptografado via DPAPI em
  `%APPDATA%\ClinicaFaturamento` → tela `SetupWindow`) → host DI → migrations → `MainWindow`.
  ViewModels em `ViewModels/` (um por seção da sidebar, registrados em `App.ConstruirHost`), design
  system em `Styles/` (tokens + um ResourceDictionary por família de componente; documentado em
  `docs/design-system/`).
- **Suíte multi-exe** (`Clinica.Desktop.Shell`, `Clinica.Modulo.*`, `Clinica.Recepcao`,
  `Clinica.Financeiro`, `Clinica.Gerente`) — três executáveis NOVOS, ao lado do faturamento e sem
  encostar nele. O shell tem o design system, a janela genérica, o contrato de módulo (`IModuloApp`),
  a abertura padrão (`SuiteApp`) e o login (`LoginWindow` + `SessaoUsuario`); cada módulo é uma
  **biblioteca** com suas telas; cada `.exe` é uma casca que só escolhe a lista de módulos — o
  Gerente Geral carrega todos, incluindo o `Clinica.Modulo.Gerente` (BI, campanhas, acessos e a
  leitura do faturamento), que só ele carrega. **Leia `docs/arquitetura-multi-exe.md` antes de
  mexer aqui**: fases, débito assumido (design system e log duplicados, agora permanentes) e
  canais de release.
- **tests/Clinica.Tests** — xUnit; os testes de regras validam cada fluxograma de convênio de ponta a
  ponta usando repositório fake em memória (sem banco).

⚠️ **O FATURAMENTO (`Clinica.Desktop`) ESTÁ CONGELADO.** Ele fatura a clínica hoje e não se encosta
nele: nada de editar telas, ViewModels ou fluxos dele, e nada de migration que renomeie ou remova o
que ele usa (**só aditiva**). Criar entidade/serviço novo nas camadas compartilhadas é permitido;
feature nova vai para Recepção, Financeiro ou Gerente. Por isso a **Fase 4 foi cancelada**. O que
cada módulo deve entregar, e em que ordem, está em `docs/features-por-modulo.md` e
`docs/entrega-ao-cliente.md`.

### Regras de negócio que não são óbvias pelo código

- **Faturamento ≠ recebíveis**: "baixa" = a secretária efetivou a guia no sistema do convênio; nunca
  adicione campos de dinheiro/pagamento.
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
  e o serviço recusa deixar a base sem ninguém que possa gerenciar acessos. **O faturamento
  continua sem login** — está congelado.
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

### Convenções

- Ao adicionar um convênio fixo: nova classe em `Domain/Regras/`, registrar em `RegistroRegras`,
  adicionar ao enum `Convenio`, cobrir o fluxograma com testes em `RegrasFaturamentoTests`.
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
- **O verificador é a única barreira local contra erro de compilação WPF.** `Clinica.Desktop`
  e a suíte só compilam no Windows, então `tools/verificar-suite.py` cobre o que dá para
  conferir aqui: XAML, chaves do design system, pack URIs, dicionário que usa token sem
  mesclá-lo (quebra em runtime, não no build), **aridade de `new XViewModel(...)` escrito à
  mão** (checagem 7) e **membro `required` não inicializado** (checagem 8). As duas últimas
  nasceram de falhas de CI reais: metade dos VMs de formulário é construída à mão pela tela
  dona, então dependência nova num VM quebra os chamadores e o DI não avisa. **Rode-o antes
  de todo push.** O que ele não pega — `using` faltando, tipo trocado — continua sendo do
  build do PR; não invente heurística para isso.
- **Gráfico é desenhado com os tokens, sem biblioteca** (`Controls/Graficos.cs`,
  `Componentes/Graficos.xaml`). Os quatro apps se auto-atualizam por Velopack e uma
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
