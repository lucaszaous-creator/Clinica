# Agenda paralela de preventivo e a 2ª guia (out/2026)

**Pedido** (ginecologia, ao começar a atender Unimed, 29/10/2026): não dá para atender 15
pacientes e colher 15 preventivos no mesmo dia. Cada dia comporta um número fixo de coletas
(5). Quando o dia lota, a secretária precisa saber na hora e dizer à paciente: "no dia 25 não
tem coleta; o próximo dia é tal". A consulta com preventivo gera **duas guias**: uma de
consulta e outra de preventivo, ambas pelo mesmo fluxo de faturamento que já existe.

Mockups aprovados pela cliente: `docs/mockups/agenda-preventivo/mapa-real.png` e `marcar-real.png`.

## O que foi feito

| Peça | Onde |
|---|---|
| Marcação de coleta no horário e no atendimento | `Agendamento.ColetaPreventivo`, `Atendimento.ColetaPreventivo` (migration `ColetaDePreventivo`, só adiciona colunas) |
| Limite por profissional | `Profissional.LimitePreventivoPorDia` (nulo = 5, 0 = não faz), editado em Profissionais e salas |
| Conta e recusa | `VagasDePreventivoNoDia` (conta pura) + `AgendaService.GarantirVagaDePreventivoAsync` nas portas: marcar, remarcar, lançar no horário, avulso |
| 2ª guia | `RegraConsultaAvulsa` acrescenta um código `TipoCodigo.Preventivo` (do dia, `NaoAplica`) quando o atendimento tem coleta. Vale para todos os convênios; o particular vira `NaoAplicavel` como o resto |
| Guias separadas | `TissExportService.SepararGuias`: as consultas vão numa guia de consulta e os procedimentos numa SP/SADT. O PDF da guia sai com uma página por guia |
| TUSS | linha "Preventivo (coleta)" em Configurações do faturamento → códigos TUSS |
| Telas | chave "Coleta de preventivo" no Marcar/Lançar do balcão e no formulário da agenda; aba **Agenda → Por especialidade** (o mapa) |

## UX — quem usa

- **Marcar.** A secretária escolhe Consulta, a especialidade e liga a chave. Ao lado aparece "25/11: 3 de 5". Se o dia lotou, o aviso já traz os próximos dias com vaga como botões; um clique troca a data. A consulta sem coleta continua cabendo no dia lotado.
- **Mapa.** Uma linha por especialidade e, abaixo dela, a coleta de preventivo de quem a faz. Cada célula mostra o que sobra. Clicar num dia de coleta mostra quem está marcado e o que dizer à paciente.
- **Faturar.** Nada muda no jeito de trabalhar: a guia do preventivo aparece nas pendências, na baixa e no lote como qualquer outra.

## DX — quem mantém

- A conta mora em `VagasDePreventivoNoDia` e serve a três leitores: a recusa, o formulário e o mapa. O mapa usa a mesma conta de horário livre do "Próximas vagas" (`BuscaDeVagas.Calcular`).
- A recusa fica no serviço porque há quatro portas de marcação.
- `RemarcarAsync` e `LancarNoHorarioAsync` recebem `bool? coletaPreventivo`: nulo quer dizer "não sei", e o horário mantém a marcação que tinha.
- Ligar ou desligar a coleta regera as guias quando a regra "guia no agendamento" está ativa.
- Testes: `ColetaDePreventivoTests` cobre as duas guias, a separação no TISS, o teto, o cancelamento que devolve a vaga, o limite 0, a remarcação e o mapa.
- `FaturamentoCongeladoTests` foi atualizado DE PROPÓSITO.

## CX — a clínica

- O app de faturamento passa a mostrar o tipo "Preventivo", no filtro de pendências e na tabela de preços. É o objetivo.
- **Antes de publicar:** a clínica cadastra o TUSS do preventivo. Sem ele, o lote avisa: "Código TUSS de Preventivo não configurado".
- **O que não se promete:** a conferência do teto é feita na gravação, sem trava no banco. Se dois balcões gravarem o mesmo dia no mesmo instante, as duas coletas podem passar.
- A lista de espera ainda não marca com coleta. A secretária liga a chave depois, no horário criado.
