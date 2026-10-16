# O Faturamento de guias em Blazor (out/2026)

> Pedido da direção, depois de aprovar o Financeiro em Blazor: *"agora preciso que você faça
> essa refatoração visual que fizemos no financeiro no módulo faturamento — se certificando que
> nada irá ser perdido ou nada irá deixar de funcionar. Lembre-se da UX/DX/CX."*
> O passo a passo de como se faz uma tela está em `docs/telas-blazor.md`; aqui fica o que foi
> decidido para ESTE módulo, e por quê.

## 1. O que mudou de lugar

| Antes (WPF) | Agora (Blazor) |
|---|---|
| `DashboardView` + `BaixaView` (tela de detalhe) | `Telas/Faturamento/PendenciasFaturamento` + `BaixaGuiaForm` (Modal) |
| `ConsultaGuiasView` + `HistoricoGuiaWindow` | `ConsultaDeGuias` + `HistoricoGuiaForm` |
| `FaturadosView` + `GlosaWindow` | `GuiasFaturadas` + `GlosaForm` |
| `GlosasView` + `GuiaGlosasWindow` | `GlosasERecursos` + `GuiaGlosasForm` |
| `NaoConformidadesView` | `NaoConformidadesFaturamento` |
| `TissView` + `EnvioLoteWindow` + `RetornoLoteWindow` | `LotesTiss` + `EnvioLoteForm` + `RetornoLoteForm` |
| `RelatoriosView` | `RelatoriosDeGuias` |
| `ParametrosView` + `ConvenioWindow` + `RegrasFamiliaWindow` | `ConfiguracoesFaturamento` + `ConvenioForm` + `RegrasFamiliaForm` |
| `FaturamentoGerencialView` (Gerente, aba "Resumo") | `ResumoFaturamento` |
| Janelas `BaixaLote`, `NaoConformidade`, `ObservacaoPendencia`, `RodadaPendencias` (do painel) | `BaixaLoteForm`, `NaoConformidadeForm`, `ObservacaoPendenciaForm`, `RodadaPendenciasForm` |

As chaves de navegação (`faturamento-pendencias`, `faturamento-guias`, …) **não mudaram**: chave é
contrato entre módulos, e trocar a tecnologia da tela não pode quebrar quem navega até ela.

**Continuam WPF, de propósito:** a janela BLOQUEANTE da rodada na abertura do app
(`RodadaPendenciasWindow` pelo `RodadaPendenciasFluxo`) e o aviso de pendências da abertura
(`AvisoPendenciasWindow`). Elas aparecem ANTES de a área de trabalho existir e TRAVAM o
aplicativo inteiro; um formulário dentro do navegador trava só a tela dele (a pessoa trocaria de
item no menu e seguiria trabalhando com a guia vencida sem decisão), e uma segunda janela com
navegador próprio traria de volta o instante preto do WebView2. A regra da rodada, essa sim,
é UMA: `DecisoesDaRodada` (Application, com teste) é chamada pela janela da abertura e pelo
formulário do painel.

## 2. As decisões, cada uma com a comparação

### 2.1 A dar baixa: tela de detalhe ou formulário?

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Tela de detalhe (como no WPF: o painel some, a baixa ocupa a área) | perde o filtro e a rolagem do painel a cada baixa — 30 baixas no dia são 30 voltas | precisa de navegação interna ao módulo | igual a hoje |
| **Formulário (Modal) sobre o painel** — escolhida | o painel fica atrás, com o filtro; fechar volta exatamente à linha seguinte | um componente, o padrão da casa | a baixa em série fica mais rápida |

### 2.2 A ficha do paciente a partir de uma pendência

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Navegar para a Recepção | sai do faturamento; o app do faturamento nem tem a Recepção | depende de outro módulo carregado | quebra no exe do faturamento |
| Reescrever a ficha em Blazor | a ficha "igual" teria duas versões | duas fichas divergiriam na primeira correção | caro e arriscado |
| **Janela por cima da tela** (`IHostDaTela.AbrirFichaDoPacienteAsync`) — escolhida | a MESMA ficha da Recepção; fechar volta ao painel com o filtro | uma rotina do hospedeiro | nada novo para aprender |

### 2.3 Um navegador para o Faturamento ou o da janela?

| Opção | 🧑‍💻 UX | 🛠️ DX | 🤝 CX |
|---|---|---|---|
| Copiar o hospedeiro do Financeiro para o Faturamento | no Gerente (que carrega os dois) DOIS navegadores: um flash a mais ao entrar no Faturamento | duas cópias do código mais delicado da tela | memória a mais por janela |
| **Subir o hospedeiro para `Clinica.Desktop.Blazor`** — escolhida | um navegador por janela, sem flash entre Financeiro e Faturamento | uma definição; módulo novo só chama `AddHospedagemBlazor` | o mesmo comportamento aprovado no vídeo |

### 2.4 Atalhos de teclado (Ctrl+S, Ctrl+P, F5)

O faturamento WPF tinha atalhos (`IAtalhosDeTela`): Ctrl+P exportava, Ctrl+S salvava, F5 relia.
Com o navegador em foco o WPF não recebe a tecla — sem cuidado, **o atalho seria perdido** (e o
F5 recarregaria a página). A página passa a repassar os três à tela à vista
(`TelaBase.AoAtalhoAsync`), pelo MESMO caminho do botão, inclusive as duas barreiras.

## 3. Como se garantiu que nada se perdeu

1. **Lista de paridade por tela**: cada comando, filtro, coluna, KPI, mensagem, atalho,
   exportação, navegação e permissão do ViewModel WPF → onde está na tela Blazor (seção 4).
2. **Regras que o ViewModel decidia subiram para a Application, com teste** (a rodada
   `DecisoesDaRodada`; as demais na seção 4).
3. **`TelasBlazorAbremTests`** abre cada tela nova com as sementes do faturamento (atendimentos
   lançados pelo motor de regras de verdade) e clica cada botão.
4. **`PalcoTrocaRepetidaTests`** e a conferência do Windows (`ValidarLayoutWindows --blazor`)
   passam a trocar também entre as telas do faturamento.
5. **As três redes** (`compilar-sombra`, `verificar-suite`, `dotnet test`) e o build do Windows no PR.
6. **Fotos** de cada tela em 1024 e 1366, e de cada formulário aberto.

## 4. Paridade, tela a tela

Cada frente devolveu a lista completa; aqui fica o essencial e TODA diferença.

**Pendências e baixas** (`PendenciasFaturamento`) — banner de rodada vencida/não verificada,
os quatro números, chips 1º/2º e atrasada/no prazo (contagem sobre o total, exclusivos dois a
dois), filtros de tipo/modalidade/especialidade/convênio, resumo do recorte, vazio que separa
"não há" de "nenhuma bate", copiar com um clique, dar baixa (`BaixarGuia`, crítica do número a
cada tecla, capa de conclusão em PDF), observação, NC (`MarcarNaoConformidade`), WhatsApp,
baixa em lote (caixinhas no lugar do Ctrl/Shift+clique), glosas com prazo (leva a Glosas),
carteirinhas (ficha do paciente) e consultas a renovar (`LancarAtendimento`), rodada pelo
painel (`DecisoesDaRodada`), F5. Diferença: o número do badge do topo do app WPF não existe no
shell da suíte (não havia onde mostrá-lo).

**Consultar guias** (`ConsultaDeGuias`) — todos os filtros, resumo que diz o recorte inteiro,
capa em PDF, histórico (`VerAuditoria`), F5. Diferença: os filtros buscam ao mudar (texto após
uma pausa ou Enter) em vez de só no botão Buscar.

**Faturados** (`GuiasFaturadas`) — período, filtro em memória, guia TISS em PDF, capa, glosar
(`RegistrarGlosa`), estornar (`EstornarBaixa`, confirmação de perigo), CSV (Ctrl+P), F5.

**Glosas e recursos** (`GlosasERecursos`) — filtros, resumo, "o que mais glosa", reapresentar,
recuperada, XML de recurso (Ctrl+P, agora com a segunda barreira), o guia dos motivos. Diferença:
o XML de recurso abre depois de salvo.

**Não conformidades** (`NaoConformidadesFaturamento`) — filtro, ler a justificativa, reabrir.

**Lotes TISS** (`LotesTiss`) — um lote por operadora, radar de glosas, dados incompletos, destino
perguntado ANTES de criar o lote (um arquivo, ou uma pasta para várias operadoras), validação do
XML (com XSD), cópia de histórico, marcar enviado, registrar retorno com importação do
demonstrativo, 2ª via do XML, Ctrl+P, F5.

**Relatórios de guias** (`RelatoriosDeGuias`) — os números, quebras, envelhecimento, 6 meses,
fechamento em PDF, CSV (Ctrl+P), F5.

**Regras e catálogos TISS** (`ConfiguracoesFaturamento`) — convênios (lista → `ConvenioForm`
com todos os campos), regras por família, modalidades, especialidades, códigos TUSS, prazos e
dados da clínica; Salvar com as duas barreiras e o TUSS gravado relendo o prestador (o Pix
gravado por outra tela continua: `ConfiguracoesFaturamentoNaTela.SalvarCodigosTussAsync`, com
teste, e a verificação do Windows passou a usá-lo). Diferença deliberada: "Particular" e "A
definir" não se excluem (o catálogo os recria, e a exclusão apagava o nome que a clínica deu).
O "Abrir pasta de logs" do ViewModel nunca aparecia no XAML — continua só em Configurações da suíte.

**Resumo de faturamento** (`ResumoFaturamento`) — período, os números com variação, envelhecimento,
quebra por operadora, curva de 6 meses com buraco no mês sem guia (a lista também diz "—", e não
mais "0%"), fechamento em PDF.

**Comum a todas**: contador de geração, terceiro estado quando a leitura falha, releitura
silenciosa ao voltar à aba, o menu "⋯" dos cartões de número.

## 5. UX · DX · CX do conjunto

- 🧑‍💻 **UX** — a mesma linguagem visual do Financeiro aprovado (cabeçalho, números, tabela,
  formulário); a troca entre as telas do faturamento é instantânea e sem flash (o navegador é o
  mesmo da janela); a baixa não tira mais a pessoa do painel; os atalhos continuam.
- 🛠️ **DX** — uma tecnologia de tela por módulo; o que decide regra está na Application com
  teste; o hospedeiro é um só para todos os módulos Blazor; telas novas entram sozinhas na
  prévia e nos testes.
- 🤝 **CX** — nenhuma capacidade a menos para quem fatura; a rodada bloqueante continua
  bloqueando; o app em produção ganha as conferências automáticas que o WPF não tinha (a tela
  é aberta e clicada em todo `dotnet test`).
