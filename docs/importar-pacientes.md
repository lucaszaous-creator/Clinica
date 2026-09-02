# Importar pacientes do sistema anterior (Smart Clinic)

Tela: **Gerente → Paciente → Importar pacientes** (bit `EditarPaciente`). Roteiro para a
clínica, e o que o sistema garante — e o que não.

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

## O que NÃO entra (decisão pendente)

- **Prontuário do sistema antigo.** Ele NÃO vem em PDF: vem em TEXTO estruturado, por
  paciente e com data — `pos_operatorio.csv` (4.287 registros de 1.047 pacientes: anamnese,
  exame físico, conduta), `ficha_soap.csv` (173, nos quatro campos S-O-A-P), `prescricao.csv`
  (156), `ficha_clinica.csv` (168 anamneses com medicamentos) e `consulta_multi.csv` /
  `prontuario_personalizado.csv`. Isso cabe no `Evolucao` do sistema (que tem os campos do
  S-O-A-P desde a parcela 73) como registro IMPORTADO, com procedência e sem autor daqui —
  e é registro clínico: nasce sob a guarda de 20 anos, entra na exportação e não se apaga.
  É uma parcela própria, com decisão da direção sobre a forma; a carteira não espera por ela.
- **A agenda antiga** (`agenda.csv`, 9.456 horários de 2.110 pacientes, com profissional e
  procedimento): serviria para a data da primeira visita e para "quem parou de vir", que
  hoje só enxergam o que aconteceu neste sistema. Mesma decisão.
- **Agenda, financeiro, pacotes e guias** do sistema antigo — a migração pedida é da
  carteira; o resto começa aqui.
