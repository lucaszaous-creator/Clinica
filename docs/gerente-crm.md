# Gerente: o atendimento pelo CRM na Mesa da direção (out/2026, caminho A)

Etapa 5 da reorganização do Gerente. A Mesa prometia os números do CRM (conversas, tempo de
resposta, SMS) e o banco da clínica não os tinha. A direção escolheu o **caminho A**: o CRM
calcula o resumo do dia e a ponte o grava no banco da clínica. O Gerente lê só o banco, como
lê todo o resto.

## O circuito

1. **CRM** (`semdor-crm`, `DailySummary.cs`). A cada 30 min ele calcula o dia de hoje (parcial)
   e o de ontem (fechado) a partir das próprias conversas e manda os dois à ponte. Liga com
   `CRM_DAILY_SUMMARY_ENABLED=true`; sem a variável, nada sai.
2. **Ponte** (`ClinicSummaryBridge`, rota `POST /crm/v1/summary/daily`). Grava UMA linha por dia
   em `ResumosCrmDiarios` e troca a linha só por um cálculo **mais novo**: o envio que chega
   fora de ordem não troca o dia fechado pelo parcial da manhã. Recusa número impossível:
   contagem negativa, mediana sem base, mais respostas que perguntas, dia no futuro.
3. **Gerente** (`CrmNaDirecaoService` + `CrmNaTela`, Application e puros). A Mesa ganhou o bloco
   **Atendimento pelo CRM**, com os últimos 7 dias fechados e o dia de hoje à parte:
   - conversas;
   - tempo típico de resposta;
   - confirmações respondidas;
   - SMS enviados, com os segmentos e as falhas.

## As regras que não são óbvias

- **Só contagens atravessam.** Nenhum nome, telefone ou texto de mensagem sai do CRM por aqui.
- **As definições são as do CRM.** "Atendimento" e "primeira resposta" usam os ciclos de
  `ServiceMetrics`, os mesmos da tela de indicadores da recepção. Uma segunda definição daria à
  direção um número que a recepção não reconhece.
- **O tempo típico não é "a mediana da semana".** Com só as medianas de cada dia, a mediana da
  semana não se calcula. A Mesa mostra a média das medianas, pesada pelo número de respostas,
  e diz isso na dica. Um dia sem resposta humana fica fora da conta: ele não tem tempo de
  resposta, e contá-lo como zero diria que a recepção respondeu na hora.
- **O custo de SMS sai em segmentos, não em reais.** Nem o CRM nem a clínica guardam o preço
  da Brevo. O segmento é o que a Brevo cobra: um SMS longo ou com acento vira vários.
- **Silêncio é dito.** Sem nenhum resumo, a Mesa diz o que liga o envio. Se o último resumo
  tem mais de 3 h, ela avisa que os números podem estar atrasados.

## Para ligar em produção (pede a senha do sudo da VPS 1)

1. Publicar os apps (a migration `ResumoCrmDiario` é aplicada na abertura do Gerente).
2. Atualizar a ponte na VPS 1 e rodar de novo `deploy/ponte-crm/papel-ponte-crm.sql`. Ele dá
   à `ponte_crm` o INSERT/UPDATE em `ResumosCrmDiarios`; sem isso a gravação leva 42501.
3. Publicar o CRM na VPS 2 e pôr `CRM_DAILY_SUMMARY_ENABLED=true` no ambiente dele.

A ordem importa. Um CRM ligado antes da ponte atualizada recebe 404 e só registra no log, e a
Mesa continua dizendo que não chegou resumo.

## 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**: a direção vê, na tela de abertura, se a recepção está respondendo e se os
  pacientes confirmam, sem abrir o CRM. São quatro linhas, sem gráfico.
- 🛠️ **DX**:
  - Uma tabela aditiva, uma rota e um record de contrato no fim do arquivo (o
    `ClinicContracts.cs` continua idêntico nos dois repositórios).
  - Testes: `ResumoCrmDiarioTests` (gravar, trocar, recusar, ler);
    `PonteComRepeticaoTests` e `PontePapelRestritoTests` ganharam um caso cada; no CRM,
    `DailySummaryTests`.
- 🤝 **CX**:
  - O CRM faz duas chamadas pequenas a cada 30 min pelo túnel, e a ponte grava uma linha por dia.
  - Nada sai sem a variável.
  - Nenhum dado de paciente vai para o banco por este caminho.
