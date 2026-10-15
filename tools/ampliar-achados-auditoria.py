"""Registro editorial da segunda passagem; âncoras verificadas pelo consolidador."""
import json
from pathlib import Path
b=Path(__file__).resolve().parents[1]/'docs/auditoria-global-2026-10-01'
f=json.loads((b/'achados.json').read_text(encoding='utf-8'))[:30]
def add(area,titulo,atual,impacto,proposta,aceite,fontes,classe='Confirmado no código',prioridade='P2'):
    f.append(dict(id=f'A{len(f)+1:02}',prioridade=prioridade,classe=classe,area=area,titulo=titulo,atual=atual,impacto=impacto,proposta=proposta,aceite=aceite,fontes=fontes))
def c(path,needle):return ['Clinica','src/'+path,needle]
def s(path,needle):return ['clinica-site',path,needle]
def r(path,needle):return ['semdor-crm','src/Clinica.Crm/'+path,needle]
add('Campanhas','Abrir WhatsApp é registrado como envio',
    'EnviarAsync abre o aplicativo externo e, se a abertura não retorna erro, chama RegistrarEnvioAsync e mostra Envio registrado. Abrir a conversa não comprova que a pessoa enviou a mensagem.',
    'O acompanhamento pode indicar contato realizado mesmo que o operador apenas tenha aberto e fechado o WhatsApp.',
    'Separar Preparar mensagem, Confirmar envio manual e estados de entrega confirmados por integração. Conservar autor e horário de cada evento.',
    'Abrir e fechar o aplicativo externo não marca envio; confirmação manual fica distinguível de entrega pelo provedor.',
    [c('Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs','private async Task EnviarAsync'),c('Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs','await campanhas.RegistrarEnvioAsync')],prioridade='P1')
add('Faturamento','Atalho de glosa descarta o contexto da linha',
    'O botão Abrir glosas de cada linha do painel não passa CommandParameter. AbrirGlosas não recebe a guia e dispara um evento sem argumento.',
    'A pessoa precisa procurar outra vez a glosa cujo prazo acabou de consultar.',
    'Passar guia/glosa e período ao destino, selecionar o registro e oferecer Voltar ao painel preservando filtros.',
    'Abrir uma glosa específica no painel mostra essa mesma glosa no destino.',
    [c('Clinica.Modulo.Faturamento/Views/DashboardView.xaml','Content="Abrir glosas"'),c('Clinica.Modulo.Faturamento/ViewModels/DashboardViewModel.cs','private void AbrirGlosas()')])
add('Configurações','Convênio fecha uma janela mas só salva na tela de trás',
    'ConvenioWindow liga os campos diretamente à edição e tem Fechar; o rodapé informa que é necessário Salvar configurações na tela de trás.',
    'A conclusão do diálogo não conclui a tarefa de edição. O usuário precisa lembrar de uma segunda ação em outro contexto.',
    'Dar ao diálogo Salvar convênio e Cancelar com estado próprio, ou expor explicitamente uma revisão única de alterações pendentes na tela principal.',
    'A pessoa consegue identificar sem ambiguidade se as alterações já estão persistidas e descartá-las antes de salvar.',
    [c('Clinica.Modulo.Faturamento/Alertas/ConvenioWindow.xaml','As alterações valem quando'),c('Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs','private void EditarConvenio')])
add('Configurações','Catálogos misturam salvar em lote e excluir imediatamente',
    'Editar/adicionar integra o Salvar configurações; RemoverConvenio e RemoverEspecialidade chamam exclusão no serviço imediatamente, com confirmação. Não há alegação de exclusão sem confirmação.',
    'Uma mesma tela usa dois momentos de persistência; sair sem salvar não desfaz uma exclusão já confirmada.',
    'Padronizar persistência por entidade; se mantiver exclusão imediata, dizer na confirmação que ela independe do botão Salvar e mostrar resultado por item.',
    'Criar, editar e excluir têm estados e consequências explícitos, incluindo o que permanecerá ao abandonar a página.',
    [c('Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs','await catalogo.ExcluirAsync'),c('Clinica.Modulo.Faturamento/ViewModels/ParametrosViewModel.cs','await catalogo.SalvarAsync')])
add('Financeiro','Prévia da retenção apaga o resultado quando falha',
    'PreverAsync captura a exceção, grava diagnóstico e atribui Retencao = null; nesse catch não explica a falha ao usuário. Valor inválido também retorna sem mensagem.',
    'Reter? pode parecer não ter efeito, e a ausência de prévia não esclarece se há indisponibilidade ou dado inválido.',
    'Exibir erro junto à linha, conservar o valor digitado e oferecer Tentar novamente. Diferenciar retenção inexistente de consulta não concluída.',
    'Falha simulada no serviço deixa indicação visível de cálculo indisponível, nunca de imposto zero.',
    [c('Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs','private async Task PreverAsync'),c('Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs','Financeiro — prévia da retenção falhou')])
add('Financeiro','Recebi na inadimplência fixa a data em hoje',
    'ReceberAsync confirma valor e descrição e passa DateOnly.FromDateTime(DateTime.Today). Esse atalho não solicita a data real do recebimento.',
    'Ao registrar hoje um pagamento de outro dia, este percurso não permite informar a data correta.',
    'Reutilizar uma confirmação de recebimento com data, forma e referência disponíveis conforme a operação; iniciar em hoje sem impedir correção autorizada.',
    'Recebimento de ontem pode ser registrado por esse caminho com a data escolhida e auditada.',
    [c('Clinica.Modulo.Financeiro/ViewModels/InadimplenciaViewModel.cs','private async Task ReceberAsync'),c('Clinica.Modulo.Financeiro/ViewModels/InadimplenciaViewModel.cs','conta.LancamentoId,')])
add('Financeiro','Devolução de receita recebida exige reconstruir lançamento no Caixa',
    'Conciliação bloqueia corretamente cancelar dinheiro já recebido, mas apenas instrui lançar a devolução como saída no Caixa.',
    'A pessoa troca de tela e recompõe paciente, guia, valor e motivo; a orientação não oferece a continuação contextual.',
    'Oferecer Registrar devolução, abrindo saída pré-preenchida e vinculada à receita original, mantendo a conferência do operador.',
    'Nenhuma entrada original é apagada; a saída preserva vínculo, data efetiva, valor e motivo.',
    [c('Clinica.Modulo.Financeiro/ViewModels/ConciliacaoViewModel.cs','Se a operadora estornou o valor, lance a')],classe='Oportunidade de simplificação')
add('Relacionamento','Chamar de volta não registra a tentativa nessa ação',
    'RetencaoViewModel.Chamar valida permissão e consentimento, abre WhatsApp e retorna se não houver erro. Não persiste tentativa ou próximo contato nesse método.',
    'Quem consulta a lista depois não sabe por essa ação se outro operador já iniciou o contato.',
    'Usar o histórico de acompanhamento como destino canônico: preparar mensagem, registrar tentativa/resultado e combinar próximo passo, sem declarar envio automático.',
    'Uma tentativa registrada fica visível no acompanhamento por responsável e data; simples abertura não se torna entrega.',
    [c('Clinica.Modulo.Gerente/ViewModels/RetencaoViewModel.cs','private void Chamar'),c('Clinica.Modulo.Gerente/ViewModels/RetencaoViewModel.cs','if (erro is null) return;')])
add('Campanhas','NPS de ontem deixa a recuperação de outros dias pouco acessível',
    'O comando GerarNpsAsync usa Today.AddDays(-1), e o botão se chama NPS de ontem. Não oferece período nesse comando.',
    'Se a rotina não for feita em um dia, o atalho seguinte não resolve explicitamente o período perdido.',
    'Manter Ontem como opção rápida e permitir Selecionar período/Recuperar pendências, com prévia de elegíveis e de contatos já gerados.',
    'Selecionar período anterior não duplica contatos já gerados e explica quem ficou fora.',
    [c('Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs','private async Task GerarNpsAsync'),c('Clinica.Modulo.Gerente/Views/CampanhasView.xaml','Content="NPS de ontem"')],classe='Oportunidade de simplificação')
add('Cadastro','Busca vazia orienta trocar de página para cadastrar',
    'O componente BuscaDePacienteView mostra Pacientes → Novo paciente como instrução textual no resultado vazio.',
    'Um fluxo iniciado com nome/CPF precisa abandonar o seletor, cadastrar e voltar a procurar.',
    'Quando o perfil puder cadastrar, oferecer Cadastrar esta pessoa e retornar seu ID ao fluxo de origem; checar duplicidade antes de criar.',
    'Ao concluir cadastro a partir da busca, o paciente fica selecionado na tarefa anterior sem nova busca.',
    [c('Clinica.Desktop.Shell/Componentes/BuscaDePacienteView.xaml','Quem ainda não tem ficha entra em Pacientes')],classe='Oportunidade de simplificação')
add('Clínico','Resumo de prontuário instrui abrir outra seção para trabalhar anexos',
    'ResumoProntuarioWindow informa que baixar ou anexar arquivos exige abrir o prontuário completo, seção Exames e anexos.',
    'O resumo é útil para leitura, mas não oferece nessa instrução uma continuação direta para o arquivo.',
    'Converter a orientação em Abrir exames e anexos deste paciente, mantendo sessão e seleção.',
    'O atalho leva ao mesmo paciente e à seção correta com permissões preservadas.',
    [c('Clinica.Modulo.Clinico/Janelas/ResumoProntuarioWindow.xaml','Para baixar ou anexar arquivos')],classe='Oportunidade de simplificação')
add('Portal','Pendências usa uma página compartilhada entre filas diferentes',
    'pendencias(pagina) envia um único número e renderiza sessões, documentos e conferência da recepção, com Anterior/Próxima comuns e até 50 registros por seção.',
    'Avançar para ver mais documentos também avança as outras listas. Uma seção vazia nessa página não significa que a fila esteja zerada.',
    'Separar paginação/filtro por fila ou usar abas de trabalho com total e posição próprios; manter um resumo geral.',
    'Percorrer documentos não altera a posição de sessões e recepção; os estados vazios informam o recorte.',
    [s('portal/profissional/posto.js','async function pendencias(pagina=0)'),s('portal/profissional/posto.js','até 50 registros por seção')])
add('Portal','Imprimir uma sessão de enfermagem busca todo o histórico paginado',
    'folhaEnfermagem consulta a ficha e percorre todas as páginas de enfermagem antes de filtrar agendamentoId da sessão escolhida.',
    'O tempo de abrir uma única folha cresce com o histórico do paciente, mesmo que a sessão tenha poucos registros.',
    'Criar leitura autorizada por sessão e carregar somente seus registros e dados de cabeçalho; manter histórico completo em consulta própria.',
    'Abrir uma folha faz número de consultas limitado e independente da quantidade de páginas antigas.',
    [s('portal/profissional/posto.js','async function folhaEnfermagem(sessao)'),s('portal/profissional/posto.js','while(mais)')])
add('Portal de termos','Portal do paciente é uma entrada da equipe para coleta presencial',
    'O site oferece Acessar o portal do paciente; a página explica que a equipe faz login e o paciente assina sem senha. O portal não é um painel de autosserviço do paciente.',
    'O nome amplo pode criar expectativa de consultar agenda, exames e documentos em casa, embora o texto explique a função real.',
    'Nomear Coleta de termos na clínica no site e Termos e assinaturas na operação. Reservar Portal do paciente para autosserviço caso venha a existir.',
    'Antes do clique a finalidade e quem faz login ficam explícitos; não se exige credencial da equipe do paciente.',
    [s('conteudo/para-pacientes.html','Acessar o portal do paciente'),s('portal/portal.js','A equipe entra com seu usuário')],classe='Oportunidade de clareza')
add('Portal de termos','Busca de pacientes atende somente BSV agendado hoje',
    'dia() apresenta Buscar paciente, mas informa que lista e busca mostram somente pacientes com BSV agendado para hoje.',
    'Quem tenta colher termo de outro procedimento ou outra data encontra uma fronteira funcional que precisa conhecer.',
    'Renomear Buscar BSV de hoje e, se o escopo desejado for mais amplo, oferecer coleta por sessão autorizada com data/procedimento explícitos.',
    'O campo deixa claro o universo pesquisado; eventual ampliação mantém conferência de identidade e contexto da sessão.',
    [s('portal/portal.js','A lista e a busca mostram somente pacientes com BSV agendado para hoje.')],classe='Limitação funcional explícita')
add('Site','Agendar consulta do cabeçalho abre página intermediária',
    'O CTA global Agendar consulta aponta para /atendimento/. A página Contato já oferece Agendar pelo WhatsApp.',
    'Para quem já decidiu falar com a recepção, o cabeçalho acrescenta uma etapa informativa ao percurso.',
    'Oferecer Falar com a recepção/Agendar pelo WhatsApp como ação direta e manter Como funciona o agendamento como orientação secundária. Medir antes de remover conteúdo útil.',
    'O CTA informa o canal de destino e não promete reserva confirmada de horário.',
    [s('modelos/base.html','agendar-topo'),s('conteudo/contato.html','Agendar pelo WhatsApp')],classe='Oportunidade de simplificação')
add('CRM','Concluir conversa muda a lista para concluídos',
    'Depois de fechar o diálogo, o handler define filter=closed e atualiza a tela.',
    'Quem está esvaziando uma fila precisa trocar novamente o filtro para continuar os atendimentos abertos.',
    'Preservar a seleção de origem e oferecer Próximo atendimento elegível; mostrar a conclusão por confirmação e link ao histórico.',
    'Concluir uma conversa não tira o operador da fila em que estava trabalhando.',
    [r('wwwroot/app.js',"filter='closed';document.querySelectorAll")])
add('CRM','Falha de atualização após concluir escreve erro em diálogo já fechado',
    'No handler closeForm, a conclusão é gravada, closeDialog.close() é chamado e depois ocorre await refresh(). O mesmo catch escreve em closeError, que fica dentro do diálogo fechado.',
    'Se apenas a atualização falhar, a mensagem pode ficar fora da vista; a operação já persistida pode ser confundida com falha de conclusão.',
    'Separar sucesso da mutação e atualização da tela. Após concluir, mostrar aviso global e Tentar atualizar se a leitura falhar.',
    'Simular sucesso do POST e falha no refresh exibe Conclusão registrada, atualização pendente, sem repetir a conclusão.',
    [r('wwwroot/app.js',"submit('closeForm',async()=>")],prioridade='P1')
add('CRM','Modelos WhatsApp necessários ficam em Mais ações',
    'mountSettings move o botão templates para um details Mais ações junto a aiOfferHuman. Não é uma cópia do botão.',
    'Em situações que exigem modelo aprovado, a ação necessária pode ficar em um menu pouco evidente.',
    'Quando a conversa exigir modelo, promover Enviar modelo aprovado junto ao compositor e explicar a condição; manter Mais ações para usos ocasionais.',
    'No estado em que texto livre não pode ser enviado, o caminho disponível para um modelo aparece diretamente.',
    [r('wwwroot/app.js',"moreLabel.textContent='Mais ações ⌄'"),r('wwwroot/app.js',"on('templates','click'")],classe='Oportunidade de simplificação')
add('CRM','Alerta de espera mistura tempo corrido e expediente',
    'serviceWaitBadge mostra as duas medidas; renderServiceQueue conta Acima de 15 min usando Date.now()-waitingSince, isto é, tempo corrido.',
    'O resumo não explicita no próprio título que o limite inclui período fora do expediente, embora a linha individual diferencie.',
    'Nomear Acima de 15 min corridos e permitir alternar para Em expediente, com mesma base no indicador e na filtragem.',
    'Uma conversa recebida fora do expediente tem classificação consistente com a métrica selecionada.',
    [r('wwwroot/app.js','function renderServiceQueue()'),r('wwwroot/app.js','function serviceWaitBadge(c)')])
add('CRM','Listas cortadas não oferecem paginação no componente',
    'O CRM renderiza só 60 confirmações pendentes, 70 eventos da agenda, 60 eventos de recall e 100 candidatos de recall. O recall informa até 100 e tem filtros; esses renders não oferecem próxima página. Meu dia, ao contrário, tem atalhos para listas maiores e não é tratado como perda de acesso.',
    'O operador precisa restringir filtros ou ir a outra área para investigar o que ficou fora; o total pode ser maior que a lista disponível.',
    'Usar paginação/Carregar mais com total do recorte e manter falhas pendentes acessíveis independentemente da idade. Não remover limites técnicos sem paginação no servidor.',
    'Com mais de 100 candidatos e 60 pendências, todos os itens autorizados podem ser percorridos sem mudar artificialmente o filtro.',
    [r('wwwroot/app.js','filtered.slice(0,100)'),r('wwwroot/app.js','p.pending.slice(0,60)'),r('wwwroot/app.js','jobs.slice(0,70)'),r('wwwroot/app.js','d.jobs.slice(0,60)')])
add('Web de consulta','Prontuário web termina nas 20 sessões mais recentes',
    'Paginas.Sessoes usa Take(20) e informa o total. Nesse render não há paginação nem caminho para sessões mais antigas.',
    'Um usuário autorizado consulta parte do histórico e precisa mudar de aplicação para continuar.',
    'Oferecer Carregar anteriores ou pesquisa por período, mantendo esse produto como leitura e preservando a trilha de acesso.',
    'Paciente fictício com 21 sessões permite alcançar a mais antiga e continua mostrando o total correto.',
    [c('Clinica.Web/Paginas.cs','foreach (var e in sessoes.Take(20))'),c('Clinica.Web/Paginas.cs','As {Math.Min(sessoes.Count, 20)} sessões')],classe='Limitação funcional explícita')
add('Navegação','Pesquisa de seções depende de acentos e dos nomes exatos',
    'ShellViewModel.Casa usa Contains com OrdinalIgnoreCase, sem normalização de acentos nem catálogo de sinônimos. Isso ignora caixa, mas não torna relatorios equivalente a relatórios.',
    'A busca que deveria encurtar caminhos pode não encontrar o destino quando o operador escreve sem acento ou usa um nome anterior.',
    'Normalizar acentos e cadastrar aliases por chave canônica, como impressão, segunda via e documentos, respeitando as permissões do destino.',
    'Pesquisar com/sem acentos retorna os mesmos destinos; aliases não revelam telas proibidas.',
    [c('Clinica.Desktop.Shell/Shell/ShellViewModel.cs','bool Casa(string? t)')])
add('Navegação','Pesquisa sem resultados fecha o painel de resultados',
    'Ao terminar a busca, PesquisaAberta recebe ResultadosPesquisa.Count > 0. Com texto não vazio e zero resultados, o painel é fechado.',
    'A pessoa não recebe nesse painel uma explicação de que não houve correspondência nem sugestão para continuar.',
    'Manter o painel aberto para a consulta não vazia com Nenhuma seção encontrada e exemplos de busca; distinguir ausência de destino de falta de permissão sem revelar dados restritos.',
    'Digitar termo inexistente exibe estado vazio acessível; apagar o termo fecha o painel.',
    [c('Clinica.Desktop.Shell/Shell/ShellViewModel.cs','PesquisaAberta = ResultadosPesquisa.Count > 0;')])
add('Treinamento','Progresso das aulas é local à máquina',
    'AcervoTreinamento grava progresso-{usuarioId}.json em LocalApplicationData. O progresso desse componente não acompanha automaticamente o profissional entre computadores.',
    'Ao mudar de posto, o mesmo usuário pode ver aulas sem o progresso que marcou em outro dispositivo.',
    'Explicitar Progresso neste computador ou sincronizar por usuário com política de conflito e uso offline. Manter cache de vídeo local.',
    'A interface informa o escopo do progresso; se houver sincronização, dois dispositivos retomam a posição correta.',
    [c('Clinica.Desktop.Shell/Treinamento/AcervoTreinamento.cs','_progresso=Path.Combine')],classe='Limitação funcional explícita',prioridade='P3')
add('Relacionamento','Seção CRM da ficha apresenta contatos de campanha',
    'RelacionamentoPacienteView chama-se Relacionamento (CRM) e fala das últimas conversas, mas renderiza Contatos com tipo, detalhe e situação de campanha; o estado vazio é Nenhum contato de campanha registrado.',
    'O nome pode sugerir que o histórico completo de conversas do semdor-crm está ali. A auditoria não encontrou esse histórico nesse componente.',
    'Nomear Campanhas e origem do paciente e oferecer abertura contextual da central de conversas quando a integração estiver comprovada; mostrar a origem de cada registro.',
    'O operador consegue distinguir contato de campanha, conversa WhatsApp e acompanhamento clínico.',
    [c('Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml','Relacionamento (CRM)'),c('Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml','Nenhum contato de campanha registrado.')])
add('Privacidade','Exportar meus dados usa a voz do paciente em tela da equipe',
    'PrivacidadePacienteView apresenta Exportar meus dados na ficha de um paciente aberta pelo operador.',
    'O pronome meus pode ser interpretado como dados da conta conectada, em vez dos dados do paciente selecionado.',
    'Usar Exportar dados deste paciente, com nome/identificador e descrição do conteúdo na confirmação; manter autor e destinatário distintos.',
    'A confirmação deixa explícito de quem são os dados e qual arquivo será produzido.',
    [c('Clinica.Modulo.Recepcao/Views/PrivacidadePacienteView.xaml','Content="Exportar meus dados"')],prioridade='P3')
add('Financeiro','Pix presencial oferece copia e cola, sem QR nessa janela',
    'CobrancaPixWindow exibe texto Pix copia e cola e Copiar. A explicação pede colar no aplicativo do paciente ou enviar pelo WhatsApp; a janela não contém QR.',
    'No balcão, transferir texto entre computador e celular acrescenta uma etapa ao pagamento.',
    'Adicionar QR do mesmo payload e opção de impressão, com valor e recebedor visíveis. Manter o aviso de que gerar código não confirma recebimento.',
    'QR e texto codificam exatamente o mesmo valor/referência; mudar esses campos invalida ambos.',
    [c('Clinica.Modulo.Financeiro/Janelas/CobrancaPixWindow.xaml','Pix copia e cola'),c('Clinica.Modulo.Financeiro/ViewModels/CobrancaPixViewModel.cs','Código copiado. Cole no aplicativo do paciente')],classe='Oportunidade de simplificação')
add('Integrações','Teste de publicação deixa limpeza para o painel do provedor',
    'EnviarExemploAsync publica um arquivo de exemplo e orienta abrir o endereço e depois apagar o objeto no painel do provedor.',
    'Testar a configuração exige alternar de sistema e lembrar de uma limpeza manual.',
    'Exibir Abrir teste e Remover arquivo de teste, com identificador do objeto criado; oferecer expiração automática apenas para objetos explicitamente de teste.',
    'O teste informa seu estado e permite limpar somente seu próprio objeto, sem acesso aos documentos reais.',
    [c('Clinica.Modulo.Gerente/ViewModels/ConfiguracoesViewModel.cs','private async Task EnviarExemploAsync'),c('Clinica.Modulo.Gerente/ViewModels/ConfiguracoesViewModel.cs','abre — e apague o objeto')],classe='Oportunidade de simplificação')
add('Acesso','Troca de usuário exige reiniciar a aplicação',
    'TrocarUsuario avisa para salvar, abre um novo processo e encerra o atual. O comentário explica que permissões das ViewModels são construídas com a sessão.',
    'Em posto compartilhado, a troca implica reconstruir a sessão de trabalho; não basta mudar o nome conectado.',
    'Projetar encerramento controlado com pendências salvas/descartadas e reconstrução dos escopos de usuário. Preservar a barreira atual até comprovar isolamento entre sessões.',
    'A nova sessão não herda dados, botões ou permissões da anterior; alterações não salvas têm tratamento explícito.',
    [c('Clinica.Desktop.Shell/Shell/ShellViewModel.cs','private void TrocarUsuario()')],classe='Oportunidade de simplificação')
add('Exames','Estado vazio reúne nenhum pedido e todos respondidos',
    'AnexosPacienteView mostra Nenhum exame aguardando e explica duas possibilidades: todos com resultado ou nenhum exame pedido.',
    'Os dois estados pedem próximos passos diferentes, mas recebem a mesma orientação.',
    'Quando não houver pedidos, oferecer Solicitar exame ao perfil habilitado; quando todos estiverem respondidos, oferecer Ver resultados.',
    'Os dois cenários fictícios geram mensagem e ação próprias, sem sugerir um pedido clínico automaticamente.',
    [c('Clinica.Modulo.Clinico/Views/AnexosPacienteView.xaml','Todo pedido deste paciente já tem resultado amarrado')],classe='Oportunidade de clareza')
add('Recepção','Pagamentos vazio instrui voltar ao fechamento ou à venda',
    'O estado vazio de PagamentosView orienta registrar cobrança no fechamento da sessão ou na venda do pacote, sem ação contextual nesse bloco.',
    'Quem chegou para receber precisa descobrir qual tarefa anterior está faltando e procurar o paciente novamente em outro fluxo.',
    'Oferecer Conferir sessões deste paciente e Ver pacotes, com retorno à cobrança. Não criar dívida automaticamente só porque a lista está vazia.',
    'O operador pode continuar para a origem da cobrança mantendo o paciente e retornar ao recebimento.',
    [c('Clinica.Modulo.Recepcao/Views/PagamentosView.xaml','Registre a cobrança no fechamento da sessão')],classe='Oportunidade de simplificação')
add('Nomenclatura','Acompanhamento designa cuidado clínico e retorno comercial',
    'AcompanhamentoView do Clínico reúne dor, medidas e avaliações. AcompanhamentoView da Recepção reúne recall e novos BSV, responsáveis e contato.',
    'Busca, treinamento e pedidos de suporte podem usar o mesmo nome para tarefas diferentes.',
    'Usar Evolução e medidas para a leitura clínica e Acompanhamento de retornos para a operação de contato; conservar vínculos entre os domínios.',
    'Menus, busca e ajuda distinguem os dois assuntos sem fundir seus dados.',
    [c('Clinica.Modulo.Clinico/Views/AcompanhamentoView.xaml','Header="Evolução da dor"'),c('Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml','Recall · pacientes sem retorno')],classe='Oportunidade de clareza',prioridade='P3')
add('Faturamento','Guias sem resposta no XML permanecem com decisão de aceite',
    'O fluxo alerta que guias ausentes no XML ficam aceitas e podem ser ajustadas manualmente. RetornoLoteWindow constrói todas as linhas e AplicarImportacao altera apenas as decisões encontradas; ao confirmar, todas entram em Decisoes.',
    'Ausência de resposta exige correção manual para não ser tratada como decisão positiva. O aviso existe, mas o padrão favorece a conclusão indevida.',
    'Representar Sem retorno como estado próprio, destacar divergências e exigir decisão explícita para itens ausentes antes de encerrar o lote; validar o modelo de domínio necessário.',
    'XML parcial conserva itens ausentes como pendentes e nunca os converte silenciosamente em aceitos.',
    [c('Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs','Elas ficam como aceitas'),c('Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml.cs','foreach (var decisao in resultado.Decisoes)'),c('Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml.cs','Decisoes = Linhas')],prioridade='P1')
add('Faturamento','Baixar XML como segunda via regenera com dados atuais',
    'O botão XML é descrito como baixar novamente/2ª via. BaixarXml lê o lote e os parâmetros atuais e chama GerarLoteXml novamente, preservando o registro ANS salvo no lote. Não lê o arquivo originalmente exportado.',
    'Depois de alterar cadastro/parâmetros, uma suposta segunda via pode não ter os mesmos bytes do XML já entregue.',
    'Separar Baixar original arquivado e Gerar versão corrigida, com hash, data e situação de envio explícitos.',
    'Alterar parâmetros não muda o original; regeneração fica identificada e não substitui silenciosamente o artefato enviado.',
    [c('Clinica.Modulo.Faturamento/Views/TissView.xaml','Baixar novamente o XML deste lote'),c('Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs','private async Task BaixarXml')],prioridade='P1')
add('Faturamento','Validação TISS não expõe separadamente XSD não executado',
    'ValidarXmlGerado faz validação estrutural e tenta XSD somente se encontrar arquivo na pasta local. Exceção nessa etapa vai para log; o retorno é apenas uma lista de problemas.',
    'Uma lista sem problemas não informa se o XSD rodou, faltou ou falhou; o operador não vê o nível de verificação realizado.',
    'Retornar estado por etapa: estrutura, schema/versão, não executado e erro de validação, com correção acessível. Não prometer aceite da operadora.',
    'Sem XSD ou com erro ao lê-lo, a tela informa validação incompleta mesmo que a estrutura básica passe.',
[c('Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs','private static IReadOnlyList<string> ValidarXmlGerado'),c('Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs','Validação XSD do lote TISS')])
add('Faturamento','Rodada vencida bloqueia a janela até decidir todas as guias',
    'A inicialização do Faturamento chama a rodada com bloqueante:true. Nesse modo Cancelar some e OnClosing impede fechar sem concluir todas as decisões exigidas. A ativação efetiva depende do status da rodada.',
    'Quem precisa consultar outra informação para decidir pode ficar preso numa tarefa que exige resolver tudo naquele momento.',
    'Avaliar uma fila de pendências obrigatórias com progresso salvo e bloqueio apenas das ações dependentes; prever responsável, escalonamento e adiamento justificado conforme regra aprovada.',
    'O operador consegue consultar o necessário e retomar, mantendo a obrigação de resolver as guias e o histórico das decisões.',
    [c('Clinica.Desktop/App.xaml.cs','bloqueante: true'),c('Clinica.Modulo.Faturamento/Alertas/RodadaPendenciasWindow.xaml.cs','if (_bloqueante && !_concluido)')],classe='Oportunidade de redesenho de regra')
add('Faturamento','Radar interrompe exportação com texto, sem abrir a correção',
    'Exportar apresenta riscos em confirmação e, ao cancelar, orienta corrigir guias/configurações. Os trechos mostram listas textuais e retorno, sem destino contextual para cada pendência.',
    'A pessoa precisa localizar as entidades mencionadas e depois reconstruir a seleção do lote.',
    'Criar prévia de prontidão com linha por guia/campo e Corrigir aqui/Abrir cadastro, retomando o lote após a correção.',
    'Cada pendência aponta para seu registro e a prévia é recalculada antes de gerar o lote.',
    [c('Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs','Exportação cancelada pelo radar de glosas'),c('Clinica.Modulo.Faturamento/ViewModels/TissViewModel.cs','Corrija na tela Configurações')],classe='Oportunidade de simplificação')
add('Ajuda','Ajuda exige localizar manualmente arquivos de diagnóstico',
    'AjudaView descreve caminhos de logs e pede anotar horário e tarefa. Esse componente é texto e não oferece coletar um diagnóstico.',
    'O suporte depende de navegar no sistema de arquivos e correlacionar o episódio manualmente.',
    'Oferecer Copiar diagnóstico deste erro com referência, versão e contexto técnico revisável, e Abrir pasta de logs para quem estiver autorizado. Evitar incluir dados de pacientes por padrão.',
    'Um erro tem referência recuperável e o operador consegue fornecer contexto sem procurar arquivo mensal à mão.',
    [c('Clinica.Desktop.Shell/Componentes/AjudaView.xaml','Para relatar um problema técnico')],classe='Oportunidade de simplificação',prioridade='P3')
add('Nomenclatura','Ajuda promete que falha nunca aparece como sucesso',
    'AjudaView usa a afirmação absoluta Falha nunca aparece como sucesso. A35 e A48 mostram percursos cujo feedback ainda precisa de correção; a frase descreve uma intenção arquitetural como garantia universal.',
    'A ajuda pode levar a interpretar silêncio ou uma confirmação como prova de que todo o fluxo terminou.',
    'Explicar os estados esperados e a recuperação sem garantia absoluta: operação salva, atualização pendente, envio preparado, envio confirmado.',
    'O texto de ajuda corresponde aos estados implementados e é verificado nos casos de falha documentados.',
    [c('Clinica.Desktop.Shell/Componentes/AjudaView.xaml','Falha nunca aparece como sucesso')],prioridade='P3')
(b/'achados.json').write_text(json.dumps(f,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(f),'achados registrados')
