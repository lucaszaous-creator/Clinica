# Como se faz uma tela Blazor nesta casa 🎨 (out/2026)

> A regra curta está no `CLAUDE.md` ("⛔ TELA NOVA É BLAZOR"). Este é o passo a passo.
> O modelo de referência é o **Caixa** (`src/Clinica.Ui.Blazor/Telas/Caixa.razor`) e os
> formulários dele (`Formularios/LancamentoForm`, `RecebimentoForm`, `CobrancaPixForm`,
> `HistoricoObrigacaoForm`). Antes de escrever uma tela nova, leia os dois.

## 1. Onde cada coisa mora 🗂️

| O quê | Onde |
|---|---|
| A tela | `src/Clinica.Ui.Blazor/Telas/<Nome>.razor` — `@inherits TelaBase` |
| Formulário (o que no WPF era uma `Window`) | `src/Clinica.Ui.Blazor/Formularios/<Nome>Form.razor`, desenhado com `<Modal>` |
| Componente comum | `src/Clinica.Ui.Blazor/Componentes/` |
| Estilo comum | `src/Clinica.Ui.Blazor/Ativos/css/clinica.css` |
| Estilo só desta tela | `src/Clinica.Ui.Blazor/Ativos/css/telas/<tela>.css` (já importado no `clinica.css`) |
| Regra de negócio / o que a tela AFIRMA | `src/Clinica.Application` — com teste |
| Dados fictícios da prévia | `tools/PreviaBlazor/Sementes/<Tela>.cs` (implementa `ISemente`, Ordem > 0) |
| Ligar no menu | `CriarTela` do módulo: `new Web.TelaBlazor(typeof(Telas.X), servicos)` |

A prévia acha **sozinha** as telas (`/tela/<NomeDaClasse>`) e as sementes (por reflexão):
nenhum arquivo comum precisa ser editado para acrescentar uma tela.

## 2. O esqueleto de uma tela 🧱

```razor
@inherits TelaBase

<div class="pagina @(Carregando && _linhas.Count > 0 ? "carregando" : null)">
    <CabecalhoPagina Sobretitulo="Financeiro · Contas" Titulo="Contas a pagar e a receber"
                     Subtitulo="A pergunta que a tela responde, numa frase.">
        <SeletorMes Valor="Mes" ValorChanged="TrocarMesAsync" />
        <button class="botao primario" disabled="@(!Pode(Permissao.EditarFinanceiro))" @onclick="Nova">
            <Icone Nome="mais" />Nova conta</button>
    </CabecalhoPagina>

    <section class="kpis"> <Kpi Rotulo="…" Valor="…" Icone="…" Tom="…" Nota="…" /> … </section>

    <section class="cartao lista-cartao">
        <div class="filtros">…</div>
        @if (_naoVerificado) { <FalhaDeLeitura Detalhe="@_erro" AoTentarDeNovo="CarregarAsync" /> }
        else if (Carregando && _linhas.Count == 0) { <div class="cartao-corpo"><Esqueleto Altura="220" /></div> }
        else if (_linhas.Count == 0) { <EstadoVazio Titulo="…" Descricao="…" /> }
        else { <div class="tabela-rolagem"><table class="lista">…</table></div> }
    </section>
</div>

@if (_editando is { } e) { <ContaForm … AoFechar="() => _editando = null" AoConcluir="GravouAsync" /> }
```

## 3. A lista de conferência — passe por ela AO ESCREVER ✅

1. 🔐 **Duas barreiras** em todo comando que grava ou exporta: `disabled="@(!Pode(...))"`
   no botão **e** `if (!Exigir(Permissao.X, "o que é o ato")) return;` no clique. Copie os
   bits EXATOS do ViewModel WPF — inclusive `ExigirAlgum` onde ele usava.
2. 🔁 **Escopo por operação** (`using var escopo = Escopo();`). Nada de `@inject` de serviço
   da Application.
3. 🧮 **Contador de geração** (`var g = ++_geracao; … if (g != _geracao || Descartada) return;`)
   em toda carga, e tudo PUBLICADO depois do último `await`.
4. ❗ **Terceiro estado**: leitura que falha mostra `FalhaDeLeitura`, nunca a lista vazia.
   E o log: `Clinica.Application.Diagnostico.Registrar("Blazor — …", ex)`.
5. 💬 **Diálogos**: `await Dialogos.ConfirmarAsync(...)`, `PerguntarTextoAsync(...)` (Cancelar
   = `null`; pergunta opcional passa `obrigatorio: false`), `AvisoAsync(...)`.
6. 📝 **Formulário** = componente `<Modal>` em `Formularios/`; erro de campo INLINE
   (`<Aviso Tom="erro">` ou `Campo Erro=`), confirmação passageira em `Notificacoes`.
   O formulário grava e chama `AoConcluir`; a tela recarrega.
7. 💾 **Disco/cópia/navegação** pelo `Host` (`ExportarCsvAsync`, `SalvarEAbrirAsync`,
   `EscolherArquivoAsync`, `CopiarAsync`, `NavegarAsync`). CSV: `Exigir(VerFinanceiro)` antes.
8. 💰 **Formato pt-BR fixo** (`Formato.Dinheiro/Data/Mes/Numero/ParaCampo`); o que a pessoa
   digita, pela `LeituraDeValores` — o MESMO método que o ViewModel WPF usava naquele
   campo. ⚠️ `TentarLerQuantidade` lê em pt-BR primeiro, então "2.5" vira 25 (o ponto é
   milhar): use-o só onde o WPF usava; quantidade de estoque é `TentarLerNumeroExato`. Número dentro de SVG: `CultureInfo.InvariantCulture`.
9. 🏷️ **Enum nunca cru na tela**: `RotulosEnum.De(valor)` (o defeito da parcela 41).
10. 🎨 **Só token no CSS**. Classe de TOM é `tom-*` (`Selo Tom="aviso"` vira `.tom-aviso`):
    `.aviso` sozinho é o ALERTA — foi assim que o selo "Previsto" saiu inchado.
11. 🧠 **Nenhuma regra nova no Razor**: se a tela precisa decidir algo que o ViewModel WPF
    decidia (uma frase, um filtro com regra, uma conta), a decisão sobe para a Application,
    pura e com teste.
12. 📸 **Fotografe** em 1024 e 1366, e cada formulário aberto, antes do push (seção 5).

## 4. Os componentes 🧩

| Componente | Para quê |
|---|---|
| `CabecalhoPagina` | Sobretítulo, título, subtítulo e as ações à direita (`ChildContent`) |
| `Kpi` | Cartão de número. `Tom`: `""`, `saida`, `sucesso`, `perigo`, `aviso`, `neutro`; `ClasseValor`: `positivo`/`negativo` |
| `Modal` | O formulário. `Largura`: `pequeno`, `medio`, `grande`, `largo`. Esc fecha; clique fora NÃO fecha |
| `Campo` | Rótulo + controle + dica/erro, dentro de `<div class="campos">` (grade de 2; `.tres`; `Classe="largo"`) |
| `Aviso` | Mensagem inline. `Tom`: `info`, `erro`, `sucesso`, `atencao`. Some sozinho sem texto |
| `EstadoVazio` / `FalhaDeLeitura` / `Esqueleto` | Vazio, falha (terceiro estado), carregando |
| `Selo` | Badge de situação (`Tom` como o `Kpi`, mais `info`) |
| `SeletorMes` | ◀ mês ▶ (`DateOnly` do dia 1) |
| `SeletorDePaciente` | Busca de paciente com as regras do `SeletorPacienteViewModel` |
| `Icone` | Ícone por nome (ver o dicionário em `Componentes/Icone.razor`) |

Classes úteis: `botao` (`primario`, `perigo`, `fantasma`, `contorno-perigo`, `mini`, `icone`),
`campo` (`valor` para dinheiro à direita), `marcar` (checkbox), `segmentado`, `alternar`,
`filtros`, `busca`, `lista-cartao` + `table.lista` (`direita`, `centro`, `nao-quebra`,
`tr.apagada`), `principal`/`secundario`, `valor-entrada`/`valor-saida`, `botoes-linha`,
`rodape-lista`, `pares` (`<dl>` rótulo:valor), `abas`, `secao-form`, `kpis` (`.tres`).

Ação de linha: o principal com rótulo (`botao mini primario`), os secundários só ícone
(`botao mini icone` + `title` + `aria-label`) — em 1024 a coluna de ações não quebra.

## 5. Ver a tela 📸

```bash
tools/PreviaBlazor/subir.sh 5199                       # compila e sobe (porta opcional)
node tools/PreviaBlazor/foto.js http://127.0.0.1:5199/tela/Caixa /tmp/caixa-1366.png 1366 900
node tools/PreviaBlazor/foto.js http://127.0.0.1:5199/tela/Caixa /tmp/caixa-1024.png 1024 768
# um formulário: cliques em ordem — "texto=Rótulo do botão" ou um seletor CSS
node tools/PreviaBlazor/foto.js http://127.0.0.1:5199/tela/Caixa /tmp/novo.png 1366 900 "texto=Novo lançamento"
```

O `foto.js` imprime os erros do console: tela bonita com erro no console não está pronta.
A prévia roda os MESMOS serviços do app (`AddClinica`) sobre um SQLite em memória; sem
sessão autenticada, `Pode` libera tudo (como no app fora do login).

**O `dotnet test` também abre a tela, sem navegador** (`tests/Clinica.Tests/TelasBlazorAbremTests.cs`):
toda tela de `Clinica.Ui.Blazor.Telas` é montada pela `Raiz` com as mesmas sementes e a direção
logada, e cada botão é clicado uma vez por rótulo, descendo dois níveis no que o clique faz
aparecer. Reprova com o cartão "Algo deu errado", com falha de leitura na abertura e com exceção
que não é recusa de regra no log. Tela nova é coberta sozinha; dê a ela uma semente.

## 6. Antes do push 🚦

```bash
dotnet build src/Clinica.Ui.Blazor && dotnet build tools/PreviaBlazor
python3 tools/compilar-sombra.py && python3 tools/verificar-suite.py
dotnet test tests/Clinica.Tests/Clinica.Tests.csproj
```

## 7. O navegador: UM por janela 🪟 (decisão de out/2026)

A primeira versão criava um `BlazorWebView` **por tela**, e a clínica mandou o vídeo: ~100 ms
**pretos** a cada tela aberta. A janela nativa do WebView2 nasce preta antes de o navegador
existir dentro dela, e cor de fundo nenhuma vale nesse instante. Hoje:

- `TelaBlazor` (no módulo) só **marca o lugar** da tela na área de trabalho;
- `NavegadorDaJanela` é o navegador **único** da janela, na camada persistente do
  `ShellWindow` (`CamadaDaJanela`), posicionado sobre a tela à vista e escondido quando a tela
  é WPF. Ele nasce **escondido** e só aparece depois da primeira renderização (rede: 3 s);
- `Palco` + `PalcoDeTelas` trocam as telas **dentro** dele: cada tela aberta fica montada
  (oculta), num contêiner que rola sozinho, e reativá-la dispara a releitura silenciosa
  (`SinalDeVolta`). Diálogos e notificações existem uma vez por navegador.

**Nunca crie `BlazorWebView` por tela.** Tela nova só precisa do `new Web.TelaBlazor(...)` no
`CriarTela`; o palco cuida do resto.

### As opções que foram comparadas 🧑‍💻 🛠️ 🤝

| Opção | UX (quem usa) | DX (quem mantém) | CX (a clínica) |
|---|---|---|---|
| Um navegador por tela | flash preto e ~0,5 s a cada tela; perde filtro/rolagem ao voltar | a mais simples | amadora (o vídeo); memória cresce por tela |
| Um navegador por módulo | flash só ao entrar no módulo | média | igual à de baixo enquanto só o Financeiro for Blazor; no Gerente, um navegador e um flash a mais por módulo que migrar |
| **Um navegador por janela** (escolhida) | sem flash depois da primeira; troca instantânea; a tela volta como foi deixada | camada sobreposta que acompanha a área da tela | profissional; memória fixa por janela |
| + aquecer no login (próximo passo) | até a primeira tela é instantânea | um gancho no login | o melhor do curto prazo |
| App inteiro em Blazor (menu incluso) | uma tecnologia só, sem fronteira WPF↔navegador | reescrever o shell | direção de longo prazo, quando mais módulos migrarem |

⚠️ **O que ficou de fora e o custo aceito:** nada do WPF desenha POR CIMA da área do
navegador (é janela nativa — vale para qualquer WebView2); menus e popups do shell são janelas
próprias e passam por cima normalmente. E um diálogo HTML aberto numa tela continua aberto se
a pessoa trocar de tela pelo menu (o serviço de diálogos é do navegador).
