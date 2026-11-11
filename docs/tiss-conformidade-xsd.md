# Lote TISS conforme o esquema oficial da ANS (nov/2026)

**Pergunta da direção:** "a nossa guia segue os padrões TISS?" — e, diante da resposta, "enviamos
o XML e também digitamos no portal de cada convênio. Corrija-o."

## O que foi encontrado

O esquema oficial (TISS 4.01.00, os `.xsd` do site da ANS) foi baixado e os três arquivos que o
sistema gera foram validados contra ele. **Os três eram reprovados**, com a suíte verde e o
validador interno dizendo "0 problemas":

| Arquivo | O que o esquema exige e não saía |
|---|---|
| Guia SP/SADT | solicitante (contratado, nome, profissional com conselho, número, UF, CBO); caráter do atendimento; tipo de atendimento, indicação de acidente, regime; valor unitário e total de cada procedimento; valor total da guia |
| Guia de consulta | profissional executante; indicação de acidente; regime; valor do procedimento; CNES obrigatório |
| Recurso de glosa | quase tudo: operadora, objeto, lote, protocolo, valor recursado, data |
| Todos | código na operadora **e** CNPJ juntos (o padrão aceita um); arquivo em UTF-8 sem a declaração do XML |
| O lote | consulta e SP/SADT no mesmo lote (o esquema aceita **um tipo por lote**) e sem teto (o esquema aceita **100 guias**) |

Ninguém via porque a conferência contra o esquema só rodava se alguém copiasse os `.xsd` para
uma pasta da máquina — e os testes conferiam o que o próprio gerador escrevia.

## O que mudou

1. **O XML valida.** Lote de consulta, lote de SP/SADT e recurso de glosa são aceitos pelo
   esquema oficial — conferido em dois validadores independentes (o do .NET, que o sistema usa,
   e o `xmllint`).
2. **O esquema vai embutido** (`Clinica.Application/Recursos/Tiss`). Toda exportação e toda 2ª
   via é validada contra ele, sem depender de arquivo na máquina; o que reprovar aparece no
   aviso "Validação do XML TISS", com o campo.
3. **Quem atendeu** vem do horário da agenda do atendimento: o registro do conselho já
   cadastrado em Equipe ("CRM-RJ 52.1234") é lido para conselho, número e UF. O **CBO** é campo
   novo em Equipe; em branco, vale o da especialidade principal (acupuntura, psiquiatria,
   geriatria, ginecologia, endocrinologia, psicologia).
4. **Quanto vale** vem da **tabela de preços do convênio** que já existia, pelo dia do atendimento.
5. **Um lote por operadora E por tipo de guia**, até cem guias: "Unimed — consultas",
   "Unimed — procedimentos (1 de 2)". Operadora com um tipo só e menos de cem guias continua igual.
6. **Recurso de glosa por lote**, amarrado ao número do lote e ao protocolo da operadora. Glosa
   sem lote, sem protocolo ou sem código do motivo fica de fora **com a frase que diz por quê**.
7. **Arquivo em ISO-8859-1 com a declaração** — a codificação dos esquemas da ANS e dos
   demonstrativos que as operadoras devolvem; o hash do epílogo é calculado sobre esses bytes.
8. **Antes de gerar, a tela avisa o que falta**, com nome: "Dr. Beto: o registro do conselho não
   pôde ser lido…", "Amil: sem preço de Acupuntura na tabela…". Nada é inventado: sem preço sai
   0,00, sem conselho o campo sai em branco e a validação aponta.

## Opções comparadas

**De onde vem o CBO**

| | A — campo em Equipe + padrão pela especialidade (feito) | B — só o padrão pela especialidade | C — um CBO único da clínica em Configurações |
|---|---|---|---|
| 🧑‍💻 UX | funciona no dia da publicação para as especialidades comuns; quem foge preenche um campo | zero campo, mas Clínica da Dor fica sem resposta para sempre | um campo só |
| 🛠️ DX | uma coluna (migration aditiva) e uma tabela de seis linhas | nenhuma coluna | nenhuma coluna |
| 🤝 CX | CBO certo por profissional | aviso que não tem como ser resolvido vira ruído | psicóloga sairia como médica acupunturista — glosa |

**De onde vem o valor**

| | A — tabela de preços do convênio (feito) | B — digitar o valor na baixa da guia | C — sair sempre 0,00 |
|---|---|---|---|
| 🧑‍💻 UX | nada novo no dia a dia; cadastra-se o preço uma vez | um campo a mais em toda baixa | nada |
| 🛠️ DX | reusa `PrecoConvenioService` | coluna de dinheiro na guia — o faturamento foi desenhado sem ela | trivial |
| 🤝 CX | valor coerente com o que o Financeiro já concilia | erro de digitação vira glosa | válido no esquema, mas a operadora paga o que a guia pede |

**Como garantir que continua conforme**

| | A — esquema embutido + teste (feito) | B — pasta de `.xsd` na máquina (como era) | C — só teste, sem validar na clínica |
|---|---|---|---|
| 🧑‍💻 UX | o aviso aparece na hora de exportar | ninguém nunca copiou os arquivos | o problema de DADO (preço, conselho) só apareceria na operadora |
| 🛠️ DX | 300 KB na DLL; `TissConformidadeXsdTests` reprova elemento fora de ordem | nenhuma rede | rede só para código |
| 🤝 CX | lote recusado vira aviso antes de sair | lote recusado vira retrabalho de dias | idem |

## UX · DX · CX

- 🧑‍💻 **UX** — a tela de Lotes TISS é a mesma. Mudam: a pergunta de dados incompletos passa a
  listar preço, conselho e CBO que faltam; operadora com consulta e procedimento no período gera
  dois arquivos (a pergunta "um lote por…" explica); o recurso de glosa salva um arquivo por
  lote (pasta, quando há mais de um) e diz o que ficou de fora. Em Equipe, um campo novo (CBO),
  opcional. Fotos em `docs/evidencias/tiss-conformidade/`.
- 🛠️ **DX** — a ordem dos elementos é a do esquema, e mora em `TissExportService`; as tabelas de
  domínio (conselho, UF, CBO) em `Domain/Regras/TabelasTiss`; o que vem do banco em
  `ContextoTissService` (uma leitura de profissionais para o lote inteiro); a divisão dos lotes
  em `ExportacaoLoteTiss.Agrupar`; o plano do recurso em `RecursoDeGlosaTiss`. Redes:
  `TissConformidadeXsdTests` (o esquema), `ContextoTissServiceTests` (do banco ao XML aceito) e
  a linha nova de `TraducaoNoNpgsqlTests`. Migration aditiva `CboDoProfissional`.
- 🤝 **CX** — ⚠️ **isto chega à tela de quem fatura** (Lotes TISS e Glosas são do Faturamento e do
  Gerente), e é o objetivo. Antes do primeiro lote depois de publicar, a clínica precisa:
  (1) conferir em Equipe o registro do conselho **com a UF** de quem atende convênio, e o CBO de
  quem não é das seis especialidades; (2) ter a tabela de preços de cada convênio preenchida;
  (3) informar o protocolo da operadora ao marcar o lote como enviado (o recurso depende dele).
  Sem isso o lote sai, mas com o aviso. Publicar: **Faturamento e Gerente** (Lotes e Glosas),
  **Recepção** (formulário de Equipe).

## Regras de tela

1. *Uma porta por pergunta* — nenhuma tela nova; o CBO mora no cadastro do profissional.
2. *Função que saiu, sai inteira* — a pasta `tiss\schemas` deixou de ser lida, e o parâmetro saiu.
3. *O que pede ação vira aviso* — o que falta aparece antes de gerar, com nome e onde corrigir.
6. *Responsivo* — o formulário de Equipe fotografado em 1024 e 1366.

## O que NÃO foi feito, e por quê

- **A guia em PDF** continua sendo um resumo de dez campos, não o formulário oficial da ANS. É
  trabalho próprio (leiaute de dezenas de campos numerados), e fica para a próxima rodada.
- **Lotes antigos** (mistos ou com mais de cem guias) regeneram como foram criados: a 2ª via
  avisa que o esquema reprova. Refazê-los mudaria número de lote já enviado.
- **`numeroGuiaOperadora`, senha e data de autorização** (opcionais no esquema): o sistema
  guarda um número de guia só, que continua indo em `numeroGuiaPrestador` — é por ele que o
  retorno da operadora é casado.
- **Equipe executante por procedimento** (`equipeSadt`, opcional): o profissional vai no bloco do
  solicitante; operadora que exigir o executante por item precisa desse acréscimo.
- **`tipoConsulta`** sai sempre "1" (primeira), como antes: o sistema não distingue seguimento.
- **Regra particular de operadora** (tabela própria, campo que o esquema dá como opcional e ela
  exige) não é coberta pelo esquema: só o retorno de cada uma diz. O esquema é o piso.
- **Codificação**: ISO-8859-1 é a dos esquemas e dos demonstrativos; o documento do Componente de
  Comunicação da ANS não foi lido nesta rodada. Se alguma operadora reclamar, é uma linha
  (`TissExportService.Codificacao`), e o hash acompanha sozinho.

## Redes que rodaram

- `dotnet test --filter` nas famílias TISS, glosa, equipe e tradução: em SQLite e no Postgres
  local. No Postgres, a única falha é `ChatEquipeServiceTests.Quem_chegou_depois…`, que falha
  igual sem esta mudança.
- `TelasBlazorAbremTests` das telas Lotes TISS, Glosas e Profissionais.
- `tools/verificar-suite.py`. A suíte completa e os cinco apps ficam com o CI do PR.
- Não rodou: testes do tablet e a web de leitura (não tocados).
