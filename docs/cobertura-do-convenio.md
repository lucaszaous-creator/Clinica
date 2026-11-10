# O que a clínica atende por convênio (nov/2026)

**Pedido da direção, sobre a Petrobras** (áudios de 10/11/2026): "a gente fazia uma miscelânea
de profissionais para eles fazerem acupuntura; vamos parar com isso, está dando um rolo no
faturamento. Agora Petrobras é só consulta de especialidade — geriatria, ginecologia e
psiquiatria —, uma por semana de cada." Respostas às três perguntas: especialidades diferentes
podem ser na mesma semana; o sistema **não permite** marcar acupuntura pela Petrobras, **nem
libera**; os que já estão marcados **ficam como estão**.

## O que foi feito

Uma configuração por convênio — **Gerente Geral → Configurações → Operação → "O que a clínica
atende por convênio"**:

- **Só consulta** — acupuntura, eletro e BSV pelo plano são recusados;
- **Especialidades atendidas** — consulta de outra especialidade pelo plano é recusada;
- **Intervalo por especialidade (dias)** — 7 = uma consulta por semana de CADA especialidade.

A marcação fora da cobertura é **recusada com a frase que diz a saída** ("…marque como
PARTICULAR"), em todas as portas de marcação da agenda: formulário do horário, Marcar/Lançar do
balcão, encaixe avulso, série, lista de espera e a ponte do CRM (todas passam pela
`AgendaService`). Não há pedido de liberação.

⚠️ **A regra nasce desligada.** Para valer na Petrobras, o gerente abre a tela, escolhe
Petrobras, liga "só consulta", marca Geriatria, Ginecologia e Psiquiatria, põe 7 dias e grava.

## O que a regra NÃO alcança (de propósito)

- A sessão **particular** — pela ficha ou marcada assim.
- **Quem já está na agenda**: concluir, confirmar presença e lançar sobre um horário existente
  não conferem. Remarcar sem trocar modalidade, especialidade nem a forma de atendimento só
  confere o intervalo (o dia mudou).
- **Outros convênios** — só o que tiver configuração.
- A **trava de consulta** geral (prazo único por convênio) deixa de valer para o convênio que
  tem intervalo próprio aqui: ela conta QUALQUER consulta e barraria a ginecologia por causa da
  geriatria da mesma semana.
- O **intervalo é em dias corridos** (7 dias depois da anterior), não por semana do calendário:
  segunda e a segunda seguinte passam; segunda e sexta da mesma semana, não.

## Opções comparadas

| | A — configuração por convênio (feito) | B — regra fixa "Petrobras" no código | C — só aviso, sem recusa |
|---|---|---|---|
| 🧑‍💻 UX | a recusa diz o porquê e a saída; a direção muda sem esperar versão | igual para a recepção | a secretária continua podendo marcar o que não fatura |
| 🛠️ DX | uma regra pura (Domain) + uma chave de configuração; sem migration | menos peças, mas cada mudança de ideia é um release | menor |
| 🤝 CX | serve ao próximo convênio que a clínica restringir | só à Petrobras | não resolve o "rolo no faturamento" |

## UX · DX · CX

- 🧑‍💻 **UX** — a recusa aparece ao gravar, com o nome do plano, o que ele aceita e a saída. Para a
  consulta repetida, diz a data da outra e a partir de quando pode. Nenhum clique a mais para
  quem marca dentro da cobertura.
- 🛠️ **DX** — a decisão mora em `CoberturaDoConvenioRegra` (Domain, pura); a configuração em
  `ParametrosService` (chave `CoberturaDosConveniosV1`, JSON, **sem migration**); a aplicação em
  `AgendaService.GarantirCoberturaDoConvenioAsync`. Leitura nova no repositório
  (`ConsultasPeloConvenioComEspecialidadeAsync`), em `TraducaoNoNpgsqlTests`. Testes:
  `CoberturaDoConvenioTests` (SQLite e Postgres).
- 🤝 **CX** — a acupuntura pela Petrobras deixa de entrar na agenda e, portanto, no faturamento.
  Publicar: Recepção e Gerente. E publicar a **ponte do CRM** na VPS para a recusa valer também
  na marcação feita pelo WhatsApp.

## O que NÃO foi feito

- **O lançamento direto de atendimento pelo app de faturamento** (`Clinica.Desktop`, sem horário
  na agenda) não confere a cobertura: é recusa nova num app em produção, e o pedido foi sobre a
  MARCAÇÃO. Se a direção quiser, é uma linha no `AtendimentoService` — com teste e aviso a quem fatura.
- **A tela não avisa ANTES do Salvar** (como a box da trava faz): a recusa vem ao gravar.
- **O atendimento de infertilidade "na guia de outra especialidade"** (o mesmo áudio): não foi
  construído — ver a resposta à direção. A guia TISS vai à operadora com o profissional
  executante; um campo que faça a guia sair em nome de quem não atendeu não é rastreabilidade,
  é o sistema registrando uma informação falsa ao convênio.
- A suíte completa e o `TelasBlazorAbremTests` ficam com o CI do PR; os testes do tablet não rodaram.
