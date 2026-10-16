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

(preenchida pela integração — ver abaixo)

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
