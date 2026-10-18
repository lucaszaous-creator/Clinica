# O Gerente em Blazor — etapa 6 (out/2026)

> Pedido da direção: *"remover o WPF e implementar full Blazor hybrid"* no Gerente. O passo a
> passo de como se faz uma tela está em `docs/telas-blazor.md`; o método da lista de paridade é o
> do Faturamento (`docs/faturamento-blazor.md`). Aqui fica o que foi decidido para o MÓDULO
> Gerente, tela a tela, e por quê.

## 1. O que mudou de lugar

| Antes (WPF) | Agora (Blazor) | Lote |
|---|---|---|
| `IndicadoresView` + `IndicadoresViewModel` | `Telas/Gestao/IndicadoresDaClinica` + `IndicadoresNaTela` (Application) | 1 |
| `RentabilidadeConvenioView` + `RentabilidadeConvenioViewModel` (+ `LinhaConvenio`) | `Telas/Gestao/RentabilidadePorConvenio` + `RentabilidadeNaTela` | 1 |
| `CustoTransacaoView` + `CustoTransacaoViewModel` (+ `LinhaAdquirente`, `LinhaMesCusto`) | `Telas/Gestao/CustoDeTaxasEImpostos` + `CustoTransacaoNaTela` | 1 |
| Preços por convênio, Campanhas, Origens, Auditoria, Guarda do prontuário, Documentos emitidos, Acessos, Configurações, Importação | continuam WPF — um lote por PR | 2+ |

As chaves de navegação (`indicadores`, `rentabilidade-convenio`, `custo-transacao`) **não
mudaram**: chave é contrato entre módulos (a Mesa da direção e o composto "Produção e margem"
navegam por elas).

Peça comum nova: **`Componentes/GraficoLinha`** — o `GraficoLinha` do WPF em SVG com tokens
(valor nulo é buraco, eixo ancorado no zero, número do SVG em cultura invariante). Indicadores e
Custo o usam; a próxima tela com série também.

## 2. Paridade, tela a tela

### 2.1 Indicadores (`IndicadoresDaClinica`)

| WPF | Blazor |
|---|---|
| Período (Este mês · Últimos 3 meses · Este ano) — `PeriodoGerencial` | Segmentado com as MESMAS opções e a MESMA conta (`ResumoFaturamentoNaTela.Periodos/Intervalo`, a cópia que já estava na Application) |
| Botão Atualizar | Atualizar + **F5** |
| Exportar CSV (sem barreira) | Exportar CSV + **Ctrl+P**, com as duas barreiras (`VerIndicadores`, o bit do item) — CSV é saída de dado |
| Cabeçalhos do CSV (`Tipo; Nome; Atendidos; Faltas; Cancelados; No-show %; Ocupacao %; Prontuario %`), "—" sem base | Iguais, byte a byte (`IndicadoresNaTela.Csv`, com teste) |
| KPIs Atendidos (com variação), Faltas no-show (variação em PONTOS), Ocupação, Pacientes atendidos, NPS (+ detalhe) | Os mesmos cinco, com as mesmas regras de "—" e de cor da variação |
| `Agendados`, `Encaixes`, `HorasOcupadas` calculados e **não mostrados** | Viraram as notas dos cartões ("de N horário(s) agendado(s)", "N encaixe(s)", "N h ocupadas") — campo calculado sem leitor |
| Jornada ("Ocupação medida sobre 8 h…") | Linha abaixo dos cartões |
| Produtividade (8 colunas) | Mesma tabela, mesmas colunas |
| Barras de ocupação normalizadas pela MAIOR | Iguais (`IndicadoresNaTela.Barras`) |
| Gráficos de linha Atendimentos e No-show | `GraficoLinha` ×2, com buraco no mês sem horário fechado |
| Lista mensal abaixo do gráfico | Tabela mês a mês |
| `EstadoDaTela` (carregando / não verificado) | Esqueleto / `FalhaDeLeitura` + `Diagnostico` |
| Contador de geração | Contador de geração, publicação depois do último `await` |
| — | Volta à aba relê (`AoVoltarAsync`) |

**Diferenças (correções):** a coluna No-show da produtividade e da lista mensal mostrava
`0%` quando não havia horário fechado (o KPI e o CSV já diziam "—"); agora as três dizem "—".
"1 pontos" virou "1 ponto". Sem série (um mês só) a tela diz por que não há gráfico em vez de
desenhar um ponto solto.

### 2.2 Rentabilidade por convênio (`RentabilidadePorConvenio`)

| WPF | Blazor |
|---|---|
| Janela 1 · 3 · 6 · 12 meses (padrão 6), mês corrente inteiro | Iguais (`RentabilidadeNaTela.Janelas/Intervalo`, com teste) |
| Exportar (sem barreira) · Atualizar | Exportar CSV (**Ctrl+P**, duas barreiras `VerFinanceiro`, o bit do item) · Atualizar (**F5**) |
| CSV `Convenio; Guias; Bruto; Retido; Liquido; Liquido/guia; Prazo medio; Pendencias` | Igual |
| KPIs Guias (+período), Recebido, Retido (+%), Líquido, Glosa | Iguais |
| Resumo ("N convênio(s) · M com guia SEM receita") e "rende mais · rende menos" | Iguais |
| Linha: convênio, guias, bruto, retido ("—" se zero), líquido, por guia, prazo, barra, pendências em vermelho | Iguais; a barra some abaixo de 1200 px (o líquido ao lado já diz o tamanho) |
| Texto explicativo do líquido por guia e do prazo | Rodapé da lista |
| Erro em faixa vermelha | `FalhaDeLeitura` (terceiro estado) |

**Diferença (correção):** com EMPATE no líquido por guia o serviço devolve a mesma operadora
nas duas pontas, e a tela escrevia "rende mais: X · rende menos: X". A frase agora só aparece
quando as duas pontas diferem (com teste).

### 2.3 Custo de taxas e impostos (`CustoDeTaxasEImpostos`)

| WPF | Blazor |
|---|---|
| Janela 3 · 6 · 12 · 24 meses (padrão 12) | Iguais (`CustoTransacaoNaTela.Janelas`) |
| Exportar (sem barreira) · Atualizar | Exportar CSV (**Ctrl+P**, duas barreiras `VerFinanceiro`) · Atualizar (**F5**) |
| CSV `Tipo; Nome; Bruto; Taxa; Imposto; Liquido; Deducoes %` (meses e adquirentes) | Igual |
| KPIs Bruto (+período), Maquininha (+%), Imposto (+%), Líquido (+ deduções %) | Iguais |
| Gráficos Deduções % (buraco no mês sem faturamento) e Deduções R$ | `GraficoLinha` ×2 |
| "Adquirente que mais descontou" | Igual |
| Por adquirente: barra, valor, efetiva × tabela, vermelho acima do TETO | Igual (`Comparacao`/`AcimaDaTabela`, com teste) |
| Mês a mês (6 colunas, "—" sem base) | Igual |

Sem diferença de comportamento.

## 3. Como se garantiu que nada se perdeu

1. A lista de paridade acima, montada lendo cada ViewModel INTEIRO.
2. O que as telas afirmam subiu para a Application, puro e com teste:
   `IndicadoresNaTelaTests` e `RentabilidadeECustoNaTelaTests` (19 casos).
3. `TelasBlazorAbremTests` abre as três telas com as sementes e clica cada botão.
4. Semente própria da prévia: `tools/PreviaBlazor/Sementes/IndicadoresDaDirecao.cs` (NPS
   respondido, EVA em par, receita de guia com retenção e prazo, taxa de tabela abaixo do efetivo).
5. Fotos em 1024 e 1366 — elas acharam os valores cortados nos cinco cartões em 1024
   ("R$ 9.55…"), corrigidos para 3 + 2 colunas abaixo de 1180 px.

## 4. UX · DX · CX

- 🧑‍💻 **UX** — as três abas de "Produção e margem" e "Preços e regras" passam a ter a mesma
  linguagem visual da Mesa, da Margem e das Tendências, no mesmo navegador (sem flash entre
  elas); os atalhos F5/Ctrl+P chegam; a falha de leitura tem cara de falha.
- 🛠️ **DX** — três ViewModels WPF a menos; a regra mora na Application com teste; o gráfico de
  linha virou componente comum.
- 🤝 **CX** — nenhuma capacidade a menos; o CSV que a direção leva à planilha mantém as colunas;
  a exportação passou a ter a segunda barreira.
