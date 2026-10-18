# Gerente: a entrada Dinheiro (out/2026)

Etapa 3 da reorganização do Gerente (a etapa 2 foi a [Mesa da direção](gerente-mesa-da-direcao.md)).
O mockup aprovado prometia, na entrada **Dinheiro**: o resultado por caixa e por competência,
a projeção de 90 dias e as metas e o teto. Esta etapa entrega os três sem inventar número:
só entra o que tem lançamento no financeiro.

## O que mudou

- **Dinheiro** agora tem três abas: **Resultado do mês**, **Próximos 90 dias** e **Metas**.
- **Resultado do mês → aba Resultado.** Abaixo da cascata do caixa há um quadro novo, *Caixa e
  competência, lado a lado*:
  - Mostra receita, deduções, despesas, resultado e margem nos dois regimes.
  - Uma coluna diz o que, na competência, ainda não se moveu (a receber e a pagar).
  - Uma frase explica a diferença entre os dois resultados.
  - A competência usa a **data do fato** do lançamento (`Data`). Entram o realizado e o
    previsto; o cancelado fica fora.
  - A produção que ainda não tem valor no financeiro **não é somada a preço de tabela**. Ela é
    CONTADA (guias baixadas sem receita e sessões particulares sem pagamento), e um aviso diz
    que o número está abaixo do real. Se a contagem falhar, a tela diz que não conferiu.
- **Resultado do mês → aba Metas e teto** (antes "Teto de gasto"). As metas do mês aparecem
  acima dos tetos, lidas e montadas como na tela de Metas (`MetasNaTela.Montar`). Para editar,
  o botão leva à tela de Metas: a porta de edição continua sendo uma só. A aba ganha o selo
  "N em risco" ao lado do "N estourou".
- **Próximos 90 dias** (tela nova, `Telas/Gestao/CaixaDosProximosDias.razor`):
  - A linha do tempo é semana a semana, com o acumulado desde hoje. O acumulado é variação,
    **não saldo**: a clínica não cadastra saldo inicial, e a tela diz isso no subtítulo.
  - Cada parcela diz de onde veio:
    - conta lançada (ou conta fixa já gerada);
    - conta fixa **ainda não gerada**, pelo valor do molde;
    - depósito de cartão, líquido, com o bruto e a taxa.
  - O **já vencido** e o **sem data prometida** (sobretudo a receita de guia que o convênio
    não disse quando paga) ficam em blocos à parte, **fora da conta**. Pôr isso numa semana
    seria adivinhar.
  - O CSV exporta parcela por parcela, com a procedência.
- **Mesa da direção.** A régua ganhou o **Resultado por competência**, ao lado do resultado no
  caixa, com a nota "inclui R$ X a receber · N atendimentos sem valor lançado". A régua passou
  a ter cinco números: em 1024 px eles ficam em 3 + 2.

## Onde mora cada coisa

| O quê | Onde |
|---|---|
| A conta da competência | `ResultadoMensalService.CompetenciaDoMesAsync` / `ApurarCompetencia` (pura) |
| A projeção | `ProjecaoDeCaixaService`, que reaproveita `ContasService.EmAbertoAsync`, `RecebiveisService.EsperadosAsync` e o molde da conta fixa (`VencimentosAte` + `OrigemDe`) |
| Montagem e frases | `DinheiroNaTela` (Application, pura) e `MesaDaDirecaoNaTela.NotaDaCompetencia` |
| Leitura nova | `IClinicaRepositorio.PrevistosSemVencimentoAsync`, e `ValorTaxa`/`ValorImposto` na `LancamentoDatado` |
| Testes | `DinheiroNaTelaTests` (puros e contra o SQLite, executando as leituras) |

## O que não foi feito, e por quê

- **A competência não valora a produção sem lançamento.** Valorar guia baixada por tabela de
  preço daria um número aproximado com cara de contábil. Ela é contada e nomeada. Se a direção
  quiser a estimativa, a decisão está em [`gerente-pendencias.md`](gerente-pendencias.md).
- **A guia a receber não tem data na projeção.** Não existe "prazo médio de pagamento por
  convênio" no banco. A pergunta também está nas pendências.
- **Não há saldo em conta.** Continua sem saldo inicial cadastrado, como no Fluxo de caixa.
- **O "A receber vencido" do mockup** (pacientes) já estava na régua como "Pacientes devendo".
- **O "Gasto contra o teto" do mockup** não entrou na régua da Mesa. A régua já tinha cinco
  números, e o teto continua no alerta da pauta ("Ver tetos") e na aba Metas e teto.

## 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**:
  - A direção vê, na mesma tela, quanto o mês sobrou no caixa e quanto ele produziu.
  - A diferença entre os dois resultados vem explicada em uma frase.
  - "Vai faltar dinheiro?" agora tem resposta semana a semana, com a origem de cada número.
  - Metas e teto ficam juntos, sem uma porta de edição a mais.
  - Fotografado em 1024 e 1366. Cada bloco falha sozinho e diz que não leu (terceiro estado).
- 🛠️ **DX**:
  - Um serviço novo pequeno (`ProjecaoDeCaixaService`) e uma classe pura (`DinheiroNaTela`).
  - Uma consulta nova, só sobre colunas, com teste que a executa.
  - Duas colunas a mais na projeção da `LancamentoDatado` (init, não quebra quem constrói).
  - Nenhuma regra no Razor. A aba de metas reaproveita `MetasNaTela`.
- 🤝 **CX**:
  - Nenhuma migração e nada novo no servidor.
  - Três leituras a mais na abertura do Resultado do mês (competência e metas, em fila) e uma
    na Mesa.
  - O risco é de leitura: dois "resultados do mês" na mesma tela. A frase da diferença e o
    aviso da produção sem valor existem para isso.
