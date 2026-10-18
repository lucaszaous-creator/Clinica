# Gerente: margem por sessão (out/2026)

Etapa 4 da reorganização do Gerente ("Produção e margem"). A Mesa da direção prometia a
**margem por sessão** e o sistema não tinha esse número. Agora ele existe, como a primeira
aba do item **Produção e margem** do grupo Direção.

## O que a tela responde

> Quanto sobra de cada sessão, e onde sobra mais?

- **Receita das sessões**: a entrada **realizada** ligada ao atendimento. É o mesmo critério
  do repasse, então a previsão "a receber" fica de fora.
- **Custos das sessões**: taxa e imposto do recebimento, o insumo que saiu do estoque para a
  sessão e o repasse do profissional.
- **Margem**: receita menos os custos, com a fração de cada real que entrou.
- **Margem por sessão**: a média das sessões **com receita**. Sem nenhuma, a tela mostra "—",
  nunca zero.
- **Onde sobra mais**: a mesma conta em três recortes, por modalidade, por profissional e por
  pagador (o convênio da ficha, ou particular), ordenados pela margem.
- **O rodapé** diz o que ficou fora da conta: as sessões realizadas que ainda não têm receita,
  e as que usaram insumo sem custo cadastrado. Ele some quando não há nada a dizer.

## As regras que não são óbvias

- **Sessão sem receita fica FORA da margem.** Com receita zero e custo cheio, a guia que o
  convênio ainda não pagou daria uma margem negativa que não aconteceu. Ela é contada à
  parte, e a linha do recorte diz quantas são ("2 sem receita").
- **Nada é recalculado.** A receita vem do financeiro, o insumo do `EstoqueService` e o
  repasse do `RepasseService`. A margem não pode discordar das telas de onde cada número vem.
- **O repasse é rateado por sessão** proporcionalmente à receita, que é como o repasse por
  percentual nasce. Sem receita nenhuma, o rateio é em partes iguais (repasse por sessão). Os
  centavos que sobram vão para a última sessão, para a soma bater com o repasse apurado.

## 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**:
  - Uma tela, com o mês, quatro números e uma tabela trocável de recorte.
  - A barra ao lado de cada linha mostra a fração da receita que sobrou.
  - O CSV leva os três recortes e o total.
- 🛠️ **DX**:
  - `MargemService` (leitura) e `MargemNaTela` (a conta e as frases, puras) moram em
    `Clinica.Application/Servicos/MargemService.cs`, com 5 testes em `MargemTests`, um deles
    de ponta a ponta no banco.
  - A tela é `Telas/Gestao/MargemPorSessao.razor`, com o estilo em `css/telas/margem.css`.
  - A chave é `ModuloGerente.ChaveMargem`. O item é declarado e o composto o esconde.
- 🤝 **CX**:
  - Nenhuma migração e nada novo no servidor.
  - A leitura do mês faz quatro consultas em fila: horários realizados, lançamentos dos
    atendimentos, custos de insumo e repasse.
  - O número precisa de receita lançada para existir. Uma clínica que concilia as guias com
    atraso vê a margem do mês crescer à medida que a conciliação anda, e o rodapé diz isso.
