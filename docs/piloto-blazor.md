# Piloto de interface em Blazor — Fluxo de caixa (out/2026)

> ✅ **Aprovado pela direção.** O Financeiro inteiro foi migrado depois do piloto; o padrão
> para toda tela nova está em `docs/telas-blazor.md`. Este documento fica como o registro do
> porquê.

## Por que existe

A direção perguntou por que os mockups saem bonitos e as telas WPF saem com cara de
Windows XP. O C# não é a causa. São duas outras coisas:

1. **O WPF de 2006 desenha o padrão com o tema do Windows.** Todo controle que ninguém
   redesenhou à mão (calendário, pop-up do combo, menu de contexto, rolagem, diálogos
   nativos) sai com o visual antigo. No HTML/CSS o padrão já sai moderno.
2. **As telas WPF eram escritas sem que ninguém as visse.** O ambiente de
   desenvolvimento é Linux, e o WPF só desenha no Windows. As redes locais conferem se o
   código compila, mas não mostram a tela. Os mockups, ao contrário, são feitos em HTML e
   olhados a cada ajuste.

O piloto testa a saída que resolve as duas causas de uma vez: **a tela em HTML/CSS
(Blazor), com o C# por trás chamando os MESMOS serviços**, exibida dentro do app WPF.

## O que foi feito

| Peça | Onde | O que é |
|---|---|---|
| A tela | `src/Clinica.Ui.Blazor/Telas/FluxoDeCaixa.razor` | Razor (HTML + C#). Chama o `FluxoCaixaService`, o mesmo da tela WPF. Nenhuma regra nova. |
| O estilo | `src/Clinica.Ui.Blazor/Ativos/css/clinica.css` | Importa os tokens de `tokens/*.css`, o espelho do `Tokens.xaml` conferido no CI. As cores são as do design system. |
| O hospedeiro WPF | `src/Clinica.Modulo.Financeiro/Web/TelaBlazor.cs` | Um `BlazorWebView` que serve a página de dentro da DLL. |
| A porta | Financeiro → Caixa → aba **"Fluxo (novo visual)"** | Ao lado da aba "Fluxo", que continua sendo a tela WPF intacta. |
| A prévia | `tools/PreviaBlazor` | Servidor de desenvolvimento com SQLite em memória e dados FICTÍCIOS. Roda em Linux e é o que permite fotografar a tela. Não é produto. |
| Testes | `tests/Clinica.Tests/PilotoBlazorTests.cs` | Confere que a página, o CSS e os tokens estão embutidos na DLL (senão a aba abriria em branco só na clínica) e que o dinheiro sai em pt-BR. |

As capturas da prévia estão em `docs/capturas/piloto-blazor/`.

## Por que não é o WebView2 que saiu em out/2026

O WebView2 saiu por causa da **tela preta de 1 a 5 s** a cada tela nova. Ali eram três
problemas de implementação:

- **Um navegador novo a cada tela, sem cor de fundo.** Aqui, a cor de fundo é pintada nas
  três camadas que aparecem antes do conteúdo: o controle WPF, o próprio WebView2
  (`WEBVIEW2_DEFAULT_BACKGROUND_COLOR`) e a primeira linha da `index.html`. A aba é
  montada uma vez e guardada pelo `TelaComAbas`, então voltar a ela não parte navegador
  nenhum.
- **HTML e JavaScript escritos à mão conversando com as ViewModels por mensagens.** Aqui a
  tela é C# chamando o serviço direto, sem ponte e sem segunda cópia de regra.
- **Arquivos soltos numa pasta ao lado do `.exe`.** Aqui eles vão embutidos na DLL
  (manifesto de recursos), e não há arquivo que o pacote possa esquecer.

## Como rodar a prévia (Linux)

```bash
dotnet build tools/PreviaBlazor/PreviaBlazor.csproj
cd tools/PreviaBlazor && ASPNETCORE_URLS=http://127.0.0.1:5199 dotnet bin/Debug/net8.0/PreviaBlazor.dll
# abrir http://127.0.0.1:5199/ (ou fotografar com o Playwright do ambiente)
```

## O que conferir no Windows antes de decidir

1. Abrir Financeiro → Caixa → "Fluxo (novo visual)" e conferir que **não há tela preta**,
   nem na primeira abertura nem ao voltar à aba.
2. Comparar lado a lado com a aba "Fluxo": os mesmos números, mês a mês.
3. Exportar CSV pela tela nova: a janela "Salvar como" é a mesma das outras telas.
4. Reduzir a janela até o mínimo: os cartões viram 2 × 2 e nada é cortado.

## O que ficou de fora, com o motivo

- **Navegação entre telas Blazor.** O piloto é uma tela. Se a direção aprovar, o próximo
  passo é UM `BlazorWebView` para o módulo inteiro, trocando de página dentro dele, em vez
  de um por aba.
- **Nenhuma escrita.** O fluxo de caixa é só leitura. Uma tela que grava (com as duas
  barreiras de permissão e a auditoria) é o segundo teste a fazer, antes de migrar qualquer
  formulário.
- **Faturamento.** Não foi tocado, e não deveria ser o primeiro a migrar: é o app em
  produção.
