# Importar o sistema anterior (Smart Clinic)

Tela: **Gerente → Paciente → Importar pacientes** (bit `EditarPaciente`). Roteiro para a
clínica, e o que o sistema garante — e o que não. A decisão da direção (set/2026) foi
**"a cliente não quer perder NADA"**: o ZIP inteiro entra, e cada arquivo tem destino escrito.

## Onde cada arquivo do ZIP cai

| Arquivo | O que é | Destino aqui |
|---|---|---|
| `pacientes.csv` | a carteira (2.238 fichas, 55 colunas) | **Ficha do paciente**. O que não tem campo (e-mail, RG, profissão, estado civil, nome dos pais, naturalidade, cônjuge, etiquetas, data de cadastro, histórico de edições…) vai para as **observações** da ficha, com rótulo. Login e senha NÃO entram. |
| `pos_operatorio.csv` | 4.287 evoluções (anamnese, exame físico, conduta) de 1.047 pacientes | **Evolução** no prontuário, com a data de lá, o texto convertido de HTML e o autor como o sistema antigo gravou (nome + conselho); quando o nome casa com alguém da Equipe, vinculada a ele. |
| `ficha_soap.csv` | 173 fichas S-O-A-P | Evolução: S → história, O → exame físico, A → hipótese, P → conduta. |
| `prescricao.csv` | 156 prescrições em texto | Evolução, na conduta, marcada "Prescrição registrada no Smart Clinic". |
| `ficha_clinica.csv` | 168 anamneses com 140 campos de marcação | Evolução: os itens marcados viram uma lista legível na história ("Marcados na ficha: HAS; Alergias; …"), achados → exame físico, tratamento → conduta. |
| `consulta_multi.csv` | 106 consultas em formulário (JSON) | Evolução: "pergunta: resposta" por item respondido. |
| `prontuario_personalizado.csv` | 110 fichas personalizadas | Evolução com o título e os campos. |
| `ficha_pre_consulta.csv`, `ficha_pedi.csv` | 1 registro cada | Evolução (sinais vitais no exame físico). |
| `agenda.csv` | 9.456 horários (2020 → nov/2026) | **De hoje em diante → horário na agenda daqui** (a clínica troca de sistema com as próximas semanas já marcadas), com o profissional vinculado quando o nome casa com a Equipe. **Passados → histórico legível na ficha** ("Visitas no sistema anterior: data · procedimento · profissional"). Eles NÃO viram sessão: marcá-los como realizados sem evolução ligada inundaria a dívida de prontuário do médico com milhares de sessões antigas, e criar atendimento inventaria guia. |
| `contratante.csv`, `contratante_usuario.csv` | a clínica e os 11 usuários (com senha) | Não entram: usuários se criam em Acessos. Os NOMES servem para reconhecer autor e profissional. |
| `exame.csv` | catálogo de 912 nomes de exame | Não entra: não é dado de paciente (é a lista do dropdown de lá). |
| `prontuarios_dav.csv` | vazio | — |

Tudo é **idempotente**: ficha, evolução e horário carregam a chave `IMPORT:smartclinic:{arquivo}:{id}`
(índice único). O mesmo ZIP importado duas vezes só grava o que faltou.

**Uma rodada basta.** O sistema antigo tem 31 pessoas cadastradas duas vezes (mesmo CPF): a
segunda linha entra FUNDIDA na ficha da primeira, na mesma rodada — só os campos vazios —, e o
prontuário e os horários dos dois ids antigos caem na mesma ficha. Nada é duplicado e nada
fica de fora. A única linha que não entra sozinha é a do CPF inválido (uma) — corrija no
arquivo ou apague a célula. Rodar de novo o mesmo ZIP é seguro e não grava nada duas vezes.

**A Equipe NÃO precisa existir antes.** O registro importado guarda o nome e o conselho de
quem escreveu (como o Smart Clinic gravou), e isso é o que o prontuário mostra. Cadastrar em
Equipe só importa para quem AINDA trabalha na clínica e vai usar o sistema: aí o horário
futuro dele aparece no "Meu dia" e a evolução fica vinculada. Pode ser feito DEPOIS — a
próxima rodada do mesmo ZIP vincula o que estava sem vínculo (a tela diz quantos).

**Convênio: aponte o que souber, deixe o resto "a definir".** As 2.021 fichas sem convênio
no arquivo entram como **"A definir (importado sem convênio)"** — um convênio do catálogo que
não gera guia. Quando a pessoa aparecer para marcar ou atender, a elegibilidade acusa em
VERMELHO ("Convênio a definir") no agendamento, no check-in, no Novo atendimento, na ficha e
no Consultório; a escolha é feita com o paciente na frente. Ninguém decide 2.021 fichas antes
de importar.

⛔ **E, desde set/2026, o atendimento dessa ficha NÃO é lançado enquanto o convênio não for
escolhido.** O alerta vermelho sozinho não bastava: a sessão era lançada por cima dele, os
códigos nasciam "não aplicável", a tela dizia "Atendimento registrado" e o faturamento não
via guia nenhuma — a diferença só aparecia no fim do mês. A recusa mora no serviço que monta
o atendimento, então vale para **todas** as portas: o Novo atendimento, o Concluir da Fila e
a marcação com "guia no agendamento" ligada.

**A escolha acontece no lugar onde a recusa aparece.** No **Novo atendimento** e no
**Concluir** da Fila, o sistema abre a janela *"Qual é o convênio?"* com os convênios
cadastrados; escolhido um (e, se quiser, a carteirinha e a validade — os dois são opcionais e,
em branco, preservam o que a ficha já tinha), a ficha é atualizada e o lançamento segue no
mesmo clique. Não é preciso abrir a ficha nem perder o paciente de vista. No Novo atendimento
o aviso vermelho já traz o botão **"Escolher convênio…"**, para resolver antes mesmo de tentar
lançar.

**Quem paga do bolso entra no convênio PARTICULAR**, que é uma entrada do catálogo com "gera
guia" desmarcado (Configurações → Convênios). Particular **não** é o mesmo que ficha sem
convênio: é uma escolha registrada, e continua sendo lançado normalmente — a sessão fica no
histórico e nos indicadores, sem mandar guia a operadora nenhuma.

**Os horários importados que ficarem para trás têm tela própria.** A agenda antiga está nas
datas do sistema antigo, e o paciente aparece no dia que aparece: quando não bate, o
atendimento vira encaixe e o horário importado fica "Aguardando". **Recepção → Agenda →
"Conciliar agenda…"** lista esses horários e pergunta o que houve — ver
`docs/conciliacao-da-agenda.md`.

**Marcar por telefone continua funcionando** com a chave "guia no agendamento" desligada: aí a
marcação não cria atendimento nenhum, e o convênio é pedido no dia da sessão, que é quando o
paciente está lá para responder.

Medido na exportação real (banco de teste): 2.206 fichas, 4.963 registros de prontuário e
226 horários futuros em 30 segundos, sem erro.

O que a conversão perde: a **formatação** do HTML (negrito, cor). O conteúdo inteiro fica,
sem corte — os textos longos do prontuário são coluna `text` desde esta parcela.


## O que a exportação do Smart Clinic traz (medido em set/2026)

O ZIP vem com 14 CSVs (ponto e vírgula, UTF-8). O que a importação lê é **`pacientes.csv`**
(55 colunas); a sugestão de colunas foi conferida contra esse cabeçalho e está fixada em
teste. O que se viu no arquivo real, e como o sistema trata:

- `celular` é o número do WhatsApp (o `telefone` fixo vai para as observações quando os dois
  existem); `operadora` é a operadora do CELULAR, não convênio — não é sugerida para nada.
- O endereço vem em sete partes (`endereco`, `numero`, `complemento`, `bairro`, `cidade`,
  `estado`, `cep`) e é juntado numa linha só, que é o endereço da receita.
- `convenio` vem em texto livre ("PARTICULAR", "Particular", "BRASEG", erro de digitação e
  **2.021 em branco**): cada texto é apontado para um convênio daqui no passo 2 — inclusive a
  linha "(em branco)", que precisa de destino (quase sempre o Particular).
- `sexo` está vazio em 3 de cada 4 fichas: elas entram como Masculino e a prévia diz quantas.
- `data_nascimento` em `aaaa-mm-dd`; uma dúzia de datas impossíveis (ano 0085, 2028) fica em
  branco com aviso. `cpf` com máscara; 31 CPFs repetidos no próprio arquivo (a mesma pessoa
  cadastrada duas vezes lá): a primeira linha entra, a segunda fica de fora e, numa segunda
  importação, completa a ficha que entrou.
- `id_paciente` (hexa de 32 caracteres) vira a chave de idempotência.
- Não têm onde entrar (a ficha não tem o campo): `email`, `rg`, `profissao`, `estado_civil`,
  `nome_mae`/`nome_pai`, `tags`, `created_at`.

Os outros CSVs são o **prontuário em texto** (ver "O que NÃO entra").

## O que a clínica faz

1. **Exportar do Smart Clinic** a lista de pacientes (a exportação de "pacientes"/"cadastro",
   com o máximo de colunas que ele oferecer). Se vier em Excel, abrir e **Salvar como →
   "CSV UTF-8 (delimitado por vírgulas)"**. A primeira linha tem de ser o cabeçalho.
2. **Ensaiar num banco de teste antes**: apontar o app para uma cópia (ver
   `docs/testar-sem-publicar.md`) e importar lá primeiro. A prévia mostra tudo, mas a
   carteira inteira num clique merece um ensaio.
3. Na tela: escolher o arquivo → conferir o que cada coluna significa (a sugestão já vem
   marcada) → **mapear cada nome de convênio do arquivo para um convênio cadastrado aqui**
   ("Unimed" pode ser Padrão ou Intercâmbio; a diferença é regra de faturamento) → gerar a
   prévia → ler → importar.
4. Depois: conferir as fichas marcadas com aviso (sexo em branco, homônimo, data ilegível).

## O que o sistema garante

- **Nada é gravado antes da prévia**, e a prévia diz linha a linha o destino: ficha nova,
  ficha já cadastrada a COMPLETAR, já importada, ou "não entra" com o motivo.
- **Quem já existe é completado, nunca sobrescrito**: reconhecido pelo CPF (com ou sem
  máscara), ou por nome + data de nascimento. Só os campos VAZIOS da ficha são preenchidos;
  o convênio da ficha é mantido.
- **O mesmo arquivo importado duas vezes não duplica ninguém** — a ficha guarda a chave
  `IMPORT:smartclinic:{id}` (índice único). Por isso a coluna de ID do sistema antigo vale
  ouro: sem ela, a reimportação só reconhece quem tem CPF.
- **CPF inválido, CPF ou ID repetido no arquivo, linha sem nome e convênio sem destino não
  entram** — e a linha diz por quê. A gravação de uma linha que falhe não derruba as demais.
- **Trilha de auditoria** por ficha (`PacienteImportado`, `PacienteCompletadoPorImportacao`)
  e uma linha de resumo por importação.

## O ZIP de arquivos (receitas, laudos, exames em PDF)

A segunda exportação que o Smart Clinic entrega é uma pasta zipada com os **arquivos dos
pacientes** — nomeados pelo id do arquivo (`164001527.pdf`) — e um índice
`relacao_arquivos.csv` (`url;id_arquivo;nome_paciente;id_paciente;titulo;data`) que diz de
QUAL paciente é cada um. Medido no ZIP real: 756 PDFs de 113 pacientes, todos "Receita
#número", de 2024 a 2026.

Eles entram nos **ARQUIVOS DA FICHA** de cada paciente, e não como anexo de sessão: a
receita pertence à PESSOA, e forçá-la a uma evolução inventaria uma consulta que não houve.
Onde aparecem: na **ficha do paciente** (Recepção → Pacientes → ficha → aba **Prontuário**,
chip **Arquivos** da linha do tempo), na tela da **Enfermagem** (mesma linha do tempo) e no
**Consultório** (paciente → "Exames e anexos" → região "Arquivos da ficha"). A aba Prontuário
e o chip só existem para quem tem `VerProntuario` — a receita é dado de saúde.

**A ordem importa: este ZIP se importa DEPOIS do pacote de pacientes.** Cada arquivo acha
a ficha pelo id do paciente no sistema anterior (`IMPORT:smartclinic:{id_paciente}`, a
chave que a importação da carteira gravou). Sem a carteira importada, tudo cai em "sem
paciente" — e a prévia diz isso, com o aviso de importar o pacote antes e repetir este ZIP.

Como se faz: Gerente → Importar pacientes → **"Ou o ZIP de arquivos (receitas, laudos)…"** →
Gerar a prévia → ler → Importar. Como sempre, ensaie num banco de teste primeiro.

O que o sistema garante:

- **Prévia sem gravar nada**, linha a linha: entra · já importado · sem paciente · o índice
  cita um arquivo que não está no ZIP · inválido (sem id, vazio ou acima do teto).
- **A ficha é achada pelo id do sistema anterior; sem ele, pelo NOME — e só quando o nome é
  ÚNICO no cadastro.** Homônimo fica de fora com o motivo: pôr a receita na ficha errada,
  calado, é pior do que deixá-la para conferir à mão.
- **Idempotente**: cada arquivo guarda a chave `IMPORT:smartclinic:arquivo:{id_arquivo}`
  (índice único). O mesmo ZIP importado duas vezes não duplica nada — importar de novo é o
  jeito de completar o que ficou de fora depois de cadastrar o paciente que faltava.
- **A data é a do documento**, como o sistema anterior a gravou. Ilegível ou futura, o
  arquivo entra com a data de hoje e a observação DIZ — a data não pode ser o que impede
  um laudo de chegar à ficha.
- **O mesmo teto do anexo de prontuário** (10 MB), a mesma validação do anexo pela tela.
- **Trilha**: uma linha por lote de 40 (`AnexoFichaImportado`) e um resumo por importação —
  756 linhas de auditoria enterrariam a trilha que existe para ser lida.
- **Arquivo no ZIP sem linha no índice não entra**, e a prévia lista quais: sem o índice
  não há como saber de quem é.
- A **conferência** ao fim relê o ZIP contra o banco pela chave de cada arquivo.

## O que fica FORA, e por quê

- **As visitas passadas não viram sessão nem atendimento** (ver a tabela): ficam legíveis
  na ficha. Consequência honesta: "quem parou de vir", "primeira visita" e o BI só enxergam o
  que aconteceu NESTE sistema — a data da última visita antiga está nas observações.
- **Alergia anotada no sistema antigo** (a coluna `alergia`, vazia nesta exportação) iria
  para as observações com aviso: o alerta de prescrição só lê a lista de problemas da ficha.
- **Formatação** do HTML (negrito, cor, tabela) — só a forma, nunca o conteúdo.
- **Agenda, financeiro, pacotes e guias** do sistema antigo — a migração pedida é da
  carteira; o resto começa aqui.
