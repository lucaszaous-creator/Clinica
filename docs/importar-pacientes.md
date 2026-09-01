# Importar pacientes do sistema anterior (Smart Clinic)

Tela: **Gerente → Paciente → Importar pacientes** (bit `EditarPaciente`). Roteiro para a
clínica, e o que o sistema garante — e o que não.

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

- **Prontuário do sistema antigo.** A entidade de anexo do prontuário exige uma SESSÃO
  (`AnexoProntuario.EvolucaoId` obrigatório); não há hoje onde pendurar um PDF "histórico do
  sistema anterior" sem inventar uma sessão que não houve. Entra numa parcela própria, com
  decisão da direção sobre a forma (PDF por paciente numa natureza nova de registro clínico,
  com guarda de 20 anos e exportação).
- **Agenda, financeiro, pacotes e guias** do sistema antigo — a migração pedida é da
  carteira; o resto começa aqui.
