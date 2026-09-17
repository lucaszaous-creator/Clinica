# Abas superiores e agenda protegida — 17/09/2026

## Uso

- Os cinco aplicativos passam a usar grupos e telas visíveis no topo, sem sidebar,
  menu expansível ou botão para recolher. Os itens quebram linha conforme a largura.
- Selecionar um grupo mantém a tela em edição; selecionar uma tela executa a navegação.
  No shell compartilhado, clicar novamente na tela ativa preserva sua instância.
- Ctrl+B foca os grupos; Ctrl+F foca a pesquisa. Identidade, troca de usuário, senha,
  notificações e atualização continuam acessíveis. Permissões continuam filtrando destinos.
- No atendimento, Consultar ficha, Exames e anexos e Receitas, documentos e infusão
  abrem consultas/emissões sobre o mesmo paciente. Ao fechar, a evolução em edição permanece.
- Evolução ocupa a largura toda; o formulário rola, com Salvar e Finalizar no rodapé.
  Nomes e ações nas listas clínicas/recepção passam a ter espaço e quebra de linha.
- A fila da recepção também abre a ficha do paciente. Equipe usa abas de largura inteira
  para Profissionais, Salas e Bloqueios.

## Horários por profissional

Na Recepção: **Gestão → Agenda → Horários e travas**. A recepcionista com EditarAgenda
seleciona o profissional, marca a proteção, escolhe dias e faixa de atendimento e salva.
A configuração fica no banco compartilhado, valendo nos demais postos.

A proteção começa **desligada** para todos. Quando ativada, impede sobreposição do mesmo
profissional, horário fora da jornada e bloqueios aplicáveis. Encaixe não contorna a trava.
O intervalo inteiro do atendimento precisa caber na jornada. Horários adjacentes são aceitos.
Sem dias selecionados, todos os dias são permitidos; sem faixa, não há limite por hora.
Capacidade da sala e sobreposição de paciente continuam como avisos.

Agendamentos já existentes permanecem no lugar. Alterar observação ou concluir o atendimento
no horário original continua permitido mesmo se a jornada tiver mudado. Remarcar ou reativar
um horário cancelado passa novamente pelas regras. Configurações alteradas por outro posto
exigem atualizar a lista antes de salvar para evitar sobrescrever uma edição antiga.

## Banco e implantação

Migration `20260917133639_TravaOpcionalDaAgenda`: coluna booleana com padrão false e
trigger PostgreSQL que serializa reservas por profissional. Assim, clientes antigos e
reservas concorrentes também passam pela proteção. Não recria atendimentos nem guias.
A falha de gravação desfaz os registros da tentativa e libera o contexto para nova operação.

Publicar a migration no banco correto com backup, primeiro na homologação e depois em
produção; versões antigas continuam compatíveis com a coluna adicional. Não usar a conexão
local antiga como prova do estado da VPS. Não gerar pacientes ou sessões fictícias em produção.
Releases necessários: Clínico, Recepção, Financeiro, Faturamento e Gerente Geral.

## Verificações

- 2.656 testes de domínio/aplicação/infraestrutura em SQLite e 9 de fronteira HTTP passaram.
- 12 casos da agenda protegida, incluindo três cenários que executam somente em PostgreSQL:
  concorrência direta, gravação por cliente antigo e rollback sem atendimentos/guias órfãos.
  A execução real desses três deve ser conferida no job `testes-postgres` do CI.
- Build Windows da solução: zero erros. Verificação estática, espelho de tokens e compilação
  sombra dos dez projetos WPF passaram.
- Renderização WPF com dados fictícios em 1024×680 e 1366×768; comandos, seleção de grupos,
  destaque ativo, preservação da tela e filtros de perfis verificados no shell real.
- Ficha, exames, emissões, fila, equipe e configuração de horários também renderizados;
  editor preserva rascunho e traz atualização gravada por outro contexto.
- Estas verificações não substituem o aceite de médicos/enfermagem usando seus próprios
  certificados SafeID, ainda dependente da disponibilidade da equipe.
