# Médico, enfermagem e guias

## Perfis

- O cadastro mostra **Médico** e **Enfermagem** separadamente. Médico conserva o identificador interno `Profissional`: não exige migração de usuários, senhas ou vínculos.
- As permissões padrão de Médico e Gerente Geral permanecem iguais.
- Em Gerente Geral → cadastro de usuários, selecione Enfermagem e vincule o cadastro profissional da pessoa que fará o registro. Não use o vínculo do médico responsável como identidade da técnica.
- A nova tela **Sessões de enfermagem** aparece somente no perfil Enfermagem. As telas já disponíveis para médico e gerente continuam disponíveis.

## Sessão médica e guia

Salvar sessão médica vinculada ao agendamento grava a evolução e solicita a conclusão com as guias aplicáveis. O sistema reutiliza o atendimento original e não cria novas guias ao repetir a conclusão. Se o fechamento falhar no desktop, a mensagem informa que a evolução foi salva e que conclusão/guias continuam pendentes.

O Gerente Geral tem a ação **Concluir sessão**, inclusive sem vínculo como médico. A ação preserva a autoria da evolução já registrada. A rotina automática de 24 horas permanece como recuperação para sessões antigas/gravadas por versões anteriores; o novo salvamento médico não espera esse prazo.

O registro avulso sem agendamento não cria um atendimento por inferência. É necessário usar o fluxo de atendimento da sessão original.

## Enfermagem

A tela inicia em “Hoje e pendentes anteriores”. Inclui BSV e BSV + acupuntura, de todos os médicos, excluindo horários cancelados, faltas e substituições. Sessões antigas concluídas pelo médico continuam na lista enquanto não tiverem evolução de enfermagem vigente vinculada.

Filtros: paciente, médico, início, fim e situação (hoje e pendentes anteriores, hoje, pendentes, registradas, todas). Datas são opcionais e inclusivas; páginas de 50 registros. Data em branco não limita pendências antigas a um número fixo de dias.

**Abrir** leva ao atendimento de enfermagem com o paciente e o agendamento corretos. Salvar a evolução de enfermagem não conclui o atendimento médico nem gera uma segunda guia. Infusões continuam no fluxo próprio, inclusive em outras modalidades.

## Portal e publicação

O portal passa `concluirAoSalvar: true`. O valor padrão da API permanece falso para compatibilidade com clientes antigos. A mesma consulta de sessões BSV atende desktop e portal; o endpoint do portal exige sessão autenticada e perfil Enfermagem a cada consulta. Nomes pesquisados seguem no corpo da requisição, não na URL.

Publicar a API atualizada antes do frontend de `clinica-site`. Os executáveis Windows precisam de releases novos para receber as alterações. Código compilado e testes locais não significam implantação na VPS ou atualização já entregue.

## Validação

- Testes de conclusão imediata, repetição sem duplicar guias, enfermagem tardia, autorização por perfil, gerente sem vínculo médico, filtros e paginação.
- Harness WPF com banco SQLite em memória e dados fictícios para notebook, incluindo ação do gerente e nova lista de enfermagem.
- Portal: testes de interface, responsividade e acessibilidade com contratos fictícios; validação de vínculo à sessão selecionada e preservação do fluxo de infusões.
