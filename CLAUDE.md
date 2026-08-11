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

# Compilação do C# das telas WPF em Linux — RODE ANTES DE TODO PUSH que mexa em ViewModel/janela
python3 tools/compilar-sombra.py

# Migrations (usa a env var CLINICA_DB como connection string)
CLINICA_DB="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" \
  dotnet ef migrations add NomeDaMigration -p src/Clinica.Infrastructure -s src/Clinica.Infrastructure
```

⚠️ **Confira o carimbo de hora da migration recém-gerada.** Várias migrations deste repositório
foram escritas à mão com horas FUTURAS (`20260731230000`, `20260801000000`), porque não havia
`dotnet ef` no ambiente. O `dotnet ef` carimba com a hora REAL do relógio, então uma migration
gerada hoje pode nascer ordenando **antes** delas — e o EF aplica na ordem do ID, o que faria a
migration nova tentar alterar uma tabela que ainda não existe. Se o ID novo não for o maior de
todos, renomeie os dois arquivos e o `[Migration("...")]` do Designer. `ls Migrations/ | sed
's/_.*//' | sort -u` mostra a ordem real.

⚠️ `Clinica.Desktop` e toda a suíte multi-exe **só compilam no Windows** (`net8.0-windows`) — o SDK
de Linux não traz o `Microsoft.NET.Sdk.WindowsDesktop`. **Isso não é desculpa para empurrar sem
compilar.** Neste ambiente há três redes, e as três rodam antes de todo push:

| ferramenta | cobre | não cobre |
|---|---|---|
| `dotnet build` + `dotnet test` | Domain, Application, Infrastructure e os 988 testes | nada das telas |
| `tools/compilar-sombra.py` | **o C# dos 10 projetos WPF, faturamento incluído** (nome, tipo, aridade, atributo) | XAML |
| `tools/verificar-suite.py` | XAML, pack URIs, chaves do design system, projetos na solução, **migration destrutiva** | semântica de C# |

Se o SDK não estiver instalado: `apt-get update && apt-get install -y dotnet-sdk-8.0` (o instalador
da Microsoft está bloqueado pelo proxy; o repositório do Ubuntu não). O CI
(`.github/workflows/build-exe.yml`, runner Windows) segue sendo o build oficial dos cinco apps, em
cada push na `main` **e em cada PR para a `main`** — commit em branch de trabalho não gera build
sozinho.

**Testar sem publicar**: `build-exe.yml` tem `workflow_dispatch` e roda em qualquer branch,
gerando os cinco `.exe` PORTÁTEIS — sem `vpk pack`, então `mgr.IsInstalled` é falso e eles não se
auto-atualizam nem mexem no canal do app instalado. A armadilha não é o exe, é o BANCO: aponte
`ConnectionStrings__Clinica` para uma branch do Neon (a env var vence a config salva e **não grava**
em `%APPDATA%`) e **nunca** use a tela de Setup no build de teste, que grava. Roteiro completo em
`docs/testar-sem-publicar.md`.

Release: tag `vX.Y.Z` (ou Actions → "Release") dispara `.github/workflows/release.yml`, que empacota
os cinco apps com **Velopack** (um canal por app; o faturamento fica no canal padrão `win` e **nunca
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
  `Clinica.Financeiro`, `Clinica.Gerente`, `Clinica.Clinico`) — quatro executáveis NOVOS, ao lado do faturamento e sem
  encostar nele. O shell tem o design system, a janela genérica, o contrato de módulo (`IModuloApp`),
  a abertura padrão (`SuiteApp`) e o login (`LoginWindow` + `SessaoUsuario`); cada módulo é uma
  **biblioteca** com suas telas; cada `.exe` é uma casca que só escolhe a lista de módulos — o
  Gerente Geral carrega todos, incluindo o `Clinica.Modulo.Gerente` (BI, campanhas, acessos e a
  leitura do faturamento), que só ele carrega. O `Clinica.Clinico` (Consultório, parcela 36)
  carrega um módulo só: a máquina do médico não precisa da agenda do balcão nem do caixa. **Leia `docs/arquitetura-multi-exe.md` antes de
  mexer aqui**: fases, débito assumido (design system e log duplicados, agora permanentes) e
  canais de release.
- **tests/Clinica.Tests** — xUnit; os testes de regras validam cada fluxograma de convênio de ponta a
  ponta usando repositório fake em memória (sem banco).

⚠️ **O FATURAMENTO (`Clinica.Desktop`) SAIU DO CONGELAMENTO NA PARCELA 45 — e o cuidado que o
congelamento protegia continua valendo por inteiro.** A cliente pediu três coisas dentro dele
(formato do número da guia por convênio, login com permissões granulares e filtros na consulta de
guias), e feature pedida no app que fatura a clínica não se entrega noutro app. O que mudou foi a
proibição de **editar arquivo**; o que NÃO mudou é a razão dela: **ele fatura a clínica hoje, roda
em produção e não tem quem o teste antes do usuário.**

Na prática, o que continua valendo:

1. **Migration nele é SÓ ADITIVA** (checagem 18 do `verificar-suite.py`). Nada de renomear nem
   remover coluna que ele usa.
2. **Mudança nas camadas compartilhadas chega à tela dele sem ninguém abrir uma pasta sua.** Os
   três caminhos que já morderam: (a) **valor novo num enum embutido** — `Especialidade`,
   `ModalidadeAtendimento`, `Convenio`, `TipoCodigo` — vira opção nova no seletor do lançamento,
   porque os catálogos garantem as embutidas por `Enum.GetValues`; (b) **alerta ou recusa nova num
   serviço que ele chama** (foi por aí que a crítica do número da guia chegou às quatro portas de
   baixa de uma vez); (c) **migration destrutiva**. `FaturamentoCongeladoTests` fixa a superfície
   (a); a checagem 18 pega (c); (b) é julgamento, e a pergunta continua sendo **"isso vai aparecer
   na tela de quem fatura amanhã de manhã?"** — só que agora a resposta "vai" às vezes é o objetivo,
   e aí ela precisa vir com teste e com a lição escrita aqui.
3. **Não tire capacidade de quem já a usava.** Foi a regra que desenhou o padrão do perfil
   `Faturista`: ele nasceu com exatamente o que o app deixava fazer antes do login, inclusive
   marcar na agenda e cadastrar paciente. Versão que introduz permissão e, de quebra, tira uma
   função que a pessoa usava ontem vira chamado de suporte na segunda de manhã — e o pedido era o
   contrário, poder **liberar ou não**, caso a caso, na tela de Acessos.
4. **A Fase 4 continua cancelada**: o faturamento não vira módulo da suíte. Ele tem o design system
   dele, o log dele e agora a tela de login dele — e não pode referenciar `Clinica.Desktop.Shell`,
   porque os dois declaram tipos no namespace `Clinica.Desktop.Controls` e as referências ficariam
   ambíguas. Foi por isso que `SessaoUsuario` subiu para `Clinica.Domain`: compartilhar a DECISÃO
   sem compartilhar a janela.

Feature nova que não seja do faturamento continua indo para Recepção, Financeiro, Consultório ou
Gerente. O que cada módulo deve entregar, e em que ordem, está em `docs/features-por-modulo.md` e
`docs/entrega-ao-cliente.md`.

### ⛔ COMPROMISSO DE CONFORMIDADE — leia antes de mexer em qualquer coisa clínica

A cliente enviou, por escrito, os **dez pontos** que ela verificaria ao contratar um
prontuário eletrônico (LGPD 13.709/2018 + Lei 13.787/2018). Eles deixaram de ser uma
avaliação e viraram **compromisso do produto**: o mapa completo, com o que atendemos e o
que não, está em `docs/conformidade-lgpd.md`, e é lá que se atualiza o placar.

Aqui ficam só as regras que a construção não pode violar. **Toda parcela nova passa por
esta lista**, e a pergunta é sempre a mesma: *isto tira, esconde ou enfraquece alguma
garantia que já demos?*

1. **Registro clínico NÃO SE APAGA.** Nunca acrescente `Remove()`, `ExecuteDelete` nem
   método `Remover*Async` para evolução, anexo, avaliação, medida, documento ou
   prescrição. Cancela-se com **motivo obrigatório**, e a linha fica. Vale para entidade
   clínica NOVA também — nascer com exclusão é nascer fora da lei da guarda de 20 anos.
   `ConformidadeProntuarioTests` falha se um desses métodos voltar à interface.
2. **Alterar registro clínico guarda o que ele dizia antes.** Sobrescrever no lugar é
   apagar devagar. Se a entidade nova é editável e é prontuário, ela precisa do
   equivalente a `VersaoEvolucao` — e de leitura para recuperá-la.
3. **Migration que mexe em tabela clínica é ADITIVA.** Coluna que guarda dado de saúde não
   se renomeia nem se remove: o dado tem de sobreviver 20 anos, e migration destrutiva é o
   caminho mais rápido de perdê-lo. (A checagem 18 já cobre o faturamento; aqui a regra é
   por decisão, não por ferramenta.)
4. **Tela nova que ABRE prontuário registra o acesso.** `AcessoProntuarioService.
   RegistrarAsync`, disparado na **troca de paciente** (nunca a cada `CarregarAsync` — as
   telas recarregam a cada tecla). Sem isso o ponto 3 tem buraco, e buraco em trilha de
   acesso só aparece quando alguém precisa investigar.
5. **Permissão nova separa dado SENSÍVEL de dado cadastral.** `VerFichaPaciente` /
   `EditarPaciente` são contato; `VerProntuario` / `EditarProntuario` são saúde (art. 5º,
   II). Bit que junte os dois desfaz a parcela 49 e devolve a evolução inteira a quem só
   precisa marcar horário.
6. **Quem assina a ação é quem fez LOGIN.** `SessaoUsuario.Atual.Operador`, jamais
   `Environment.UserName` — no balcão duas pessoas dividem a máquina, e a trilha
   responderia o nome da máquina para "quem fez isso?".
7. **Auditoria grava no MESMO `SaveChanges` do ato.** Ação que possa acontecer sem a linha
   correspondente é ação sem trilha.
8. **Entidade clínica nova entra na EXPORTAÇÃO e na GUARDA.** `ExportacaoProntuarioService`
   e `GuardaProntuarioService` precisam enxergá-la, senão a clínica exporta um prontuário
   incompleto e calcula o prazo pelo registro errado. O backup pega a tabela sozinho (ele
   lê o modelo do EF) — a exportação e a guarda, não.
9. **Não prometa garantia que o código não dá.** É a regra mais antiga do projeto (parcela
   3, o carimbo escaneado) e a que mais aparece aqui: sem LTV, o rodapé diz que a
   assinatura é PAdES-B; sem certificação SBIS/CFM, o sistema não substitui o papel; o
   backup não sabe se a pasta está fora da máquina, então isso é orientação na tela e não
   promessa. **Garantia aparente é pior que ausência de garantia.**
10. **Dado clínico novo em serviço externo é transferência internacional** (art. 33) até
    que se prove o contrário. Nenhuma integração nova manda prontuário para fora sem passar
    pela decisão do ponto 10 do documento.

⚠️ **Motor não é porta, e a auditoria olha a porta.** Vários pontos estão com o serviço
pronto e sem tela (guarda, exportação, trilha de acesso, configuração de backup) — a lista
viva está no placar de `docs/conformidade-lgpd.md`. Enquanto a porta não existe, **a
resposta honesta a quem pergunta é "o motor existe, a clínica ainda não alcança"**, e é
assim que se escreve no documento. Marcar ✅ porque o teste passa é a variante mais cara do
defeito recorrente do projeto: aqui ela vira promessa a um cliente que está auditando.

### Regras de negócio que não são óbvias pelo código

- **Faturamento ≠ recebíveis**: "baixa" = a secretária efetivou a guia no sistema do convênio; nunca
  adicione campos de dinheiro/pagamento.
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
  tempo de atravessar a sala. A **releitura periódica** (1 min, ligada/desligada pelo
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
  exclusão e abertura do PDF pela URL. O que continua sem prova de campo é a assinatura, que
  depende de e-CPF — e **sem certificado a publicação não funciona em produção também**, já
  que é a assinatura que dispara o upload.
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

### Convenções

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
