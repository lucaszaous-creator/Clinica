# Tendências e metas por categoria e especialidade (out/2026)

> Pedido da cliente: *"sistema de metas, e para isso precisamos colocar filtros para sabermos as
> categorias e especialidades de atendimento que estamos tendo, as que estão tendo alta, as que
> estão em baixa."*

## 1. As duas perguntas, e por que são duas telas

- **"O que está crescendo e o que está caindo?"** — é uma VARIAÇÃO: o período contra o anterior.
  Tela **Tendências** (`TendenciaAtendimentosService`).
- **"Chegamos onde dissemos que íamos chegar?"** — é um ALVO. Tela **Metas** (`MetaService`).

A parcela 28 já tinha separado as duas: variação responde "melhorou?" e nunca "chegamos lá?".
Elas se LIGAM: da linha "Acupuntura ▼ 22%" sai o botão "Criar meta para esta linha".

## 2. As regras (todas em `TendenciaAtendimentos`, pura e testada)

| Regra | Por quê |
|---|---|
| "Categoria" = **modalidade** do catálogo (Acupuntura, BSV, Consulta e as variantes da clínica) | é como a clínica fala do que faz, e é o que a agenda e o faturamento já gravam |
| A **especialidade** de uma sessão é a da consulta; sem ela, a de quem atendeu; sem nenhuma, "Sem especialidade informada" | acupuntura feita pelo psiquiatra conta na especialidade dele; sumir com o movimento sem especialidade esconderia parte do mês |
| Sessões = agendamento com status **Realizado** | o MESMO critério do `IndicadoresService` — dois critérios dariam duas respostas |
| Receita = entrada **realizada**, pela data do pagamento, ligada a atendimento (direto ou pela guia); estornado não conta | regime de caixa, como o Resultado do mês |
| Mês cheio compara com o mês cheio anterior; recorte livre, com os mesmos dias imediatamente antes | "outubro contra setembro" é a pergunta da direção |
| **Em alta / em baixa** exigem ≥ 15% **e** ≥ 3 sessões (ou R$ 300) de diferença; abaixo da base mínima a linha diz "pouca base" | de 1 para 2 sessões é "+100%" e não diz nada (a regra da reincidência de faltas) |
| Variação sobre zero é **nula** ("nova no período"), nunca 100% | 0 → 5 não é crescimento de nada |
| Desempate da ordem pelo nome | duas leituras do mesmo período nunca trocam linhas de lugar |

## 3. Metas por recorte

- `MetaMensal` ganhou `ModalidadeCodigo` e `EspecialidadeCodigo` (vazio = todas; vazio e não
  nulo porque NULL não é igual a NULL num índice único do Postgres). A chave única alargou —
  migration `MetaPorSegmento`, declarada como não aditiva consciente (só `DropIndex` +
  `CreateIndex` mais largo; nenhuma linha se perde).
- Só **Sessões** e **Faturamento** aceitam recorte. Ocupação é minuto de agenda sobre jornada, e
  jornada não tem modalidade.
- **Defeito antigo corrigido**: a meta de UM profissional era apurada com o número da CLÍNICA
  inteira (a tela mostrava a meta dele "batida" com o trabalho de todos). Toda meta de recorte —
  profissional, modalidade ou especialidade — agora é apurada pelos mesmos fatos da Tendência.
  ⚠️ No **Faturamento** de recorte, o realizado é a receita LIGADA a atendimento; o faturamento
  da clínica inteira continua sendo todas as entradas realizadas (venda de produto, acerto…).

## 4. Onde aparece

- **Gerente → Inteligência**: Tendências e Metas.
- **Financeiro**: Tendências, abrindo pela receita.
- **Faturamento** (no Gerente): Tendências como aba, pelo convênio.

## 5. UX · DX · CX

- 🧑‍💻 **UX** — a frase do topo diz com nomes o que subiu e o que caiu; a tabela tem a série de 6
  meses de cada linha; da linha nasce a meta.
- 🛠️ **DX** — uma função pura monta o quadro (`TendenciaAtendimentos.Montar`); a mesma base de
  fatos apura as metas de recorte, então as duas telas não divergem. A consulta da receita está na
  rede de tradução do Npgsql (`TraducaoNoNpgsqlTests`) — a primeira versão não traduzia, e o
  SQLite nunca a teria executado.
- 🤝 **CX** — duas leituras de banco por tela; nada novo para instalar; a direção decide alvo por
  especialidade com o número da tendência ao lado.
