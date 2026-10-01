# Catálogo de superfícies extraídas

Extração estática: controles declarados podem depender de perfil, estado ou módulo. Contagens não equivalem a telas visíveis simultaneamente. Funções, rotas e arquivos completos estão nos CSVs.

## Clinica · src/Clinica.Clinico/App.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Clinico/App.xaml) · 12 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/AjudaView.xaml) · 97 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 42 | TextBlock | Ajuda e suporte |  |
| 43 | TextBlock | As dúvidas mais comuns do dia a dia, e o que fazer quando algo der errado. O que não estiver aqui, pergunte à direção da clínica. |  |
| 46 | Expander | Por que um número aparece como “—” em vez de zero? |  |
| 47 | TextBlock | O traço quer dizer “não medido”: a métrica não teve base de cálculo naquele período — um dia sem horário concluído não tem taxa de falta, um mês sem faturamento não tem margem. 0% e “não medido” são coisas diferentes, e o sistema nunca troca um pelo outro; nas planilhas exportadas o traço sai igual, para a média do Excel não ser calculada sobre um número que não existe. |  |
| 51 | Expander | Não encontro um item de menu que deveria existir |  |
| 52 | TextBlock | A sidebar só mostra o que o seu acesso alcança: item que exige uma permissão que o seu usuário não tem simplesmente não aparece. Quem concede é a direção, na tela de Acessos — cada permissão pode ser liberada a uma pessoa específica, num clique, sem mudar o perfil dos colegas. Cada aplicativo também carrega só os módulos do seu posto: uma tela pode existir no Gerente Geral e não existir no aplicativo da Recepção. |  |
| 56 | Expander | Como fechar a agenda para férias ou feriado? |  |
| 57 | TextBlock | Na Agenda, use “Fechar agenda…”: o período fica bloqueado e os vãos deixam de ser oferecidos. Fechar não desmarca ninguém — quem já estava marcado dentro do período é devolvido para a recepção remarcar, porque sessão que some sem ninguém avisar o paciente é pior do que o choque de horário. O encaixe continua furando o bloqueio de propósito. |  |
| 61 | Expander | Como dar baixa em uma guia de convênio? |  |
| 62 | TextBlock | Dar baixa — registrar que a guia foi efetivada no sistema do convênio — é ato do aplicativo de Faturamento; no Gerente Geral, a fila de Pendências também baixa. O número da guia é conferido contra o formato daquele convênio enquanto você digita (a Unimed só numera com dígitos, por exemplo), para o “O” no lugar do zero não passar e só aparecer no retorno da operadora, semanas depois. |  |
| 66 | Expander | O convênio glosou uma guia — e agora? |  |
| 67 | TextBlock | Registre a glosa no aplicativo de Faturamento (Controle de glosas), que monta o recurso e mostra o que mais glosa nesta clínica e como evitar. Cada glosa ganha uma data-limite de recurso vigiada no painel — passado o prazo, não é mais pendência, é prejuízo consumado. A leitura consolidada fica no Gerente Geral. |  |
| 71 | Expander | Como exportar dados para o Excel? |  |
| 72 | TextBlock | As telas com o botão “Exportar” geram planilha CSV pronta para o Excel em português — ela abre com as colunas separadas e a acentuação certa. Os lotes TISS exportam XML no padrão ANS. Métrica sem base de cálculo sai como “—” na planilha, igual à tela. |  |
| 76 | Expander | O sistema recusou uma ação. Onde vejo o porquê? |  |
| 77 | TextBlock | A recusa aparece escrita perto do botão que a causou, e sempre diz o motivo — botão que fica apagado também está dizendo que falta algo (permissão, paciente escolhido, campo obrigatório). Algumas ações pedem justificativa escrita por desenho: divergência no fechamento de caixa, perda de estoque, cancelamento de registro clínico. A justificativa fica guardada e aparece na trilha de auditoria — é ela que responde “quem fez isso, e por quê” depois. |  |
| 86 | TextBlock | Quando algo der errado |  |
| 87 | TextBlock | Falha nunca aparece como sucesso: quando uma leitura não pôde ser feita, a tela mostra “não foi possível verificar” em vez de zeros — um painel dizendo “nenhuma pendência” por causa de uma consulta quebrada seria pior do que um painel que avisa. Se aparecer esse estado, use Atualizar; persistindo, é problema de conexão ou defeito, nunca “não há nada”. |  |
| 90 | TextBlock | Para relatar um problema técnico: anote a hora em que aconteceu e o que estava sendo feito. O sistema guarda um registro técnico em arquivos mensais (erros-ano-mês.txt) na pasta “logs”, ao lado de onde o aplicativo está instalado — e, quando não pode gravar ali, em ClinicaSemDor\logs dentro da pasta do usuário (AppData). É esse arquivo que diz a causa exata, e é o que o suporte vai pedir. |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/AssinaturaPacienteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/AssinaturaPacienteWindow.xaml) · 376 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Termo do procedimento |  |
| 10 | Window |  |  |
| 22 | TextBlock | {Binding Titulo} |  |
| 24 | TextBlock |  |  |
| 35 | TextBlock |  |  |
| 61 | TextBlock | {Binding Mensagem} |  |
| 65 | Button | Paciente recusou assinar | {Binding RecusarCommand} |
| 71 | Button | Cancelar |  |
| 74 | Button | Confirmar assinatura | AoConfirmar |
| 86 | TextBlock | O TERMO |  |
| 87 | TextBlock | {Binding Corpo} |  |
| 96 | Button | Marcar todas como Sim | {Binding ConfirmarTodasCommand} |
| 100 | TextBlock | DECLARAÇÕES DO PACIENTE |  |
| 105 | TextBlock | Leia cada uma com o paciente. Responder NÃO não impede nada: o termo é emitido do mesmo jeito, com a resposta escrita, e quem decide adiar o procedimento é quem vai fazê-lo. |  |
| 119 | TextBlock | {Binding Descricao} |  |
| 121 | TextBlock | {Binding Detalhe} |  |
| 130 | RadioButton | Sim |  |
| 133 | RadioButton | Não |  |
| 162 | TextBlock | ALERGIAS — pergunte ANTES de o paciente assinar |  |
| 169 | TextBlock | Não foi possível conferir a lista de problemas agora — pergunte ao paciente e registre abaixo. |  |
| 178 | TextBlock | {Binding} |  |
| 186 | TextBlock | Nenhuma alergia registrada até aqui — pergunte ao paciente antes de colher a assinatura. |  |
| 192 | TextBlock | PACIENTE COM ALERGIA — ele sai daqui com a PULSEIRA DE ALERGIA no braço. |  |
| 205 | TextBox | {Binding NovaAlergia, UpdateSourceTrigger=PropertyChanged} |  |
| 208 | Button | Registrar alergia | {Binding RegistrarAlergiaCommand} |
| 214 | TextBlock | O registro vai para a lista de problemas do paciente — é ela que alerta o médico e a enfermagem em toda prescrição, hoje e nas próximas vezes. |  |
| 225 | TextBlock | ASSINATURA DO PACIENTE |  |
| 235 | TextBlock | Documento conferido |  |
| 236 | TextBox | {Binding DocumentoConferido, UpdateSourceTrigger=PropertyChanged} |  |
| 241 | Button | Limpar assinatura | AoLimparTraco |
| 256 | Button | Enviar para o celular… | {Binding EnviarPeloCelularCommand} |
| 261 | Button |  |  |
| 276 | Button | Cancelar envio | {Binding CancelarEnvioCelularCommand} |
| 284 | TextBlock | Sem tela de assinatura no balcão? O paciente pode ler e assinar no próprio celular, pelo WhatsApp. |  |
| 293 | TextBlock | O termo foi para o WhatsApp do paciente. Pode FECHAR esta janela: quando ele assinar, a assinatura fica guardada e a linha dele na agenda do dia passa a dizer “Termo assinado — conferir”. |  |
| 306 | TextBlock | Assinatura recebida do celular do paciente: |  |
| 323 | TextBlock | Peça ao paciente para assinar na área abaixo — com o dedo ou a caneta na tela de assinatura, ou com o mouse quando o balcão tem uma tela só. |  |
| 334 | TextBlock | Assine aqui |  |
| 353 | TextBlock | O paciente está assinando na tela dele |  |
| 355 | TextBlock | O termo apareceu no monitor virado para o paciente, com as declarações como você as marcou aqui. O botão Confirmar acende assim que ele assinar. |  |
| 360 | TextBlock | Esta é uma assinatura eletrônica SIMPLES, colhida presencialmente. Ela não usa certificado digital do paciente — o que a sustenta é o registro de quem assinou, quando, diante de quem e com que documento conferido. |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/BuscaCidWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/BuscaCidWindow.xaml) · 66 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Procurar CID-10 |  |
| 11 | TextBlock | Procurar CID-10 |  |
| 13 | TextBlock | Procure pelo código ("M54") ou pela palavra ("lombar"). Esta é a lista dos códigos que a clínica usa, não a CID-10 inteira — o campo continua aceitando qualquer código que você digitar. |  |
| 18 | Button | Fechar |  |
| 20 | Button | Usar este código | {Binding EscolherCommand} |
| 24 | TextBlock | {Binding Resumo} |  |
| 28 | Button | Limpar | {Binding LimparCommand} |
| 31 | TextBox | {Binding Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 44 | TextBlock | {Binding Codigo} |  |
| 47 | TextBlock | {Binding Descricao} |  |
| 50 | TextBlock | {Binding Grupo} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/BuscaDePacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/BuscaDePacienteView.xaml) · 140 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 9 | UserControl |  |  |
| 15 | TextBlock | {Binding Pergunta, ElementName=Raiz} |  |
| 18 | TextBlock | {Binding Explicacao, ElementName=Raiz} |  |
| 32 | TextBox | {Binding Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 67 | TextBlock | {Binding ResumoDaLista} |  |
| 122 | TextBlock | {Binding Convite, ElementName=Raiz} |  |
| 131 | TextBlock | Ninguém tem horário marcado para hoje. Digite o nome ou o CPF, ou veja todos os pacientes. |  |
| 135 | TextBlock | {Binding Erro} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/Cadastro/CadastroPacienteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/Cadastro/CadastroPacienteWindow.xaml) · 130 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Cadastro do paciente |  |
| 8 | Window |  |  |
| 19 | Button | Tirar foto | {Binding CapturarFotoCommand} |
| 20 | Button | Remover foto | {Binding RemoverFotoCommand} |
| 26 | TextBlock | {Binding Titulo} |  |
| 27 | TextBlock | Campos com * são obrigatórios. |  |
| 28 | TextBlock | Carregando cadastro… |  |
| 36 | TextBlock | Identificação |  |
| 37 | TextBlock | Nome completo * |  |
| 38 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 43 | TextBlock | CPF * |  |
| 44 | TextBox | {Binding Documento, UpdateSourceTrigger=PropertyChanged} |  |
| 45 | TextBlock | {Binding ErroDocumento} |  |
| 49 | TextBlock | Data de nascimento |  |
| 53 | TextBlock | Sexo |  |
| 54 | ComboBox |  |  |
| 58 | TextBlock | Contato e endereço |  |
| 62 | TextBlock | Telefone / WhatsApp |  |
| 63 | TextBox | {Binding Telefone, UpdateSourceTrigger=PropertyChanged} |  |
| 66 | TextBlock | E-mail |  |
| 67 | TextBox | {Binding Email, UpdateSourceTrigger=PropertyChanged} |  |
| 70 | TextBlock | Endereço residencial (opcional) |  |
| 71 | TextBox | {Binding Endereco, UpdateSourceTrigger=PropertyChanged} |  |
| 73 | TextBlock | {Binding ErroEndereco} |  |
| 75 | TextBlock | Rua, número (ou s/n), bairro, cidade e UF. Acrescente o complemento quando houver. |  |
| 78 | TextBlock | Convênio e atendimento |  |
| 79 | TextBlock | Convênio ou particular * |  |
| 80 | ComboBox |  |  |
| 81 | TextBlock | {Binding ExplicacaoDoConvenio} |  |
| 85 | TextBlock | Número da carteirinha |  |
| 86 | TextBox | {Binding Carteirinha, UpdateSourceTrigger=PropertyChanged} |  |
| 89 | TextBlock | Validade da carteirinha |  |
| 93 | CheckBox | Tem o aplicativo do convênio |  |
| 94 | TextBlock | Modalidade habitual |  |
| 95 | ComboBox |  |  |
| 96 | Expander | Opções de faturamento |  |
| 98 | TextBlock | Categoria (semáforo) |  |
| 99 | ComboBox |  |  |
| 100 | TextBlock | Sugerida pelo convênio e aplicativo; ajuste somente quando necessário. |  |
| 104 | TextBlock | Origem e observações |  |
| 108 | TextBlock | Como conheceu a clínica |  |
| 109 | ComboBox |  |  |
| 112 | TextBlock | Quem indicou |  |
| 113 | TextBox | {Binding IndicadoPor, UpdateSourceTrigger=PropertyChanged} |  |
| 116 | TextBlock | Observações do cadastro |  |
| 117 | TextBox | {Binding Observacoes, UpdateSourceTrigger=PropertyChanged} |  |
| 123 | Button | Cancelar |  |
| 124 | Button | Salvar cadastro | {Binding SalvarCommand} |
| 127 | TextBlock | {Binding Mensagem} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/CatalogoEnfermagemWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/CatalogoEnfermagemWindow.xaml) · 141 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | {Binding Titulo} |  |
| 32 | Window |  |  |
| 46 | Button | Fechar | Fechar |
| 48 | TextBlock | O que você escolhe entra na hora, na aba de trás. Não há “Salvar” aqui — quem grava é o registro da passagem. |  |
| 56 | TextBlock | {Binding Titulo} |  |
| 58 | TextBlock | {Binding Explicacao} |  |
| 62 | TextBox | {Binding Busca, UpdateSourceTrigger=PropertyChanged} |  |
| 66 | TextBlock | {Binding Resumo} |  |
| 88 | TextBlock | {Binding Titulo} |  |
| 92 | TextBlock | {Binding Detalhe} |  |
| 104 | TextBlock | ✓ já no plano |  |
| 112 | Button | Acrescentar | {Binding DataContext.AdicionarCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 117 | Button |  |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/CatalogoPacotesWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/CatalogoPacotesWindow.xaml) · 82 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Catálogo de pacotes |  |
| 23 | TextBlock | Catálogo |  |
| 24 | TextBlock | O que está à venda. A venda copia estes dados — mudar o preço aqui não reescreve o que alguém já comprou. |  |
| 29 | Button | Novo pacote | {Binding NovoPacoteCommand} |
| 48 | TextBlock | {Binding Nome} |  |
| 50 | TextBlock | {Binding Resumo} |  |
| 56 | TextBlock | {Binding ValorFormatado} |  |
| 61 | Button | Editar | {Binding DataContext.EditarDoCatalogoCommand,
                                                          RelativeSource={RelativeSource AncestorType=ListBox}} |
| 68 | Button | Excluir | {Binding DataContext.ExcluirDoCatalogoCommand,
                                                          RelativeSource={RelativeSource AncestorType=ListBox}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/CobrancaDoPacienteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/CobrancaDoPacienteWindow.xaml) · 134 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Receber no balcão |  |
| 10 | Window |  |  |
| 18 | TextBlock | Receber no balcão |  |
| 19 | TextBlock | {Binding Paciente} |  |
| 21 | TextBlock | As contas vencidas deste paciente. Receber aqui grava no caixa pelo mesmo caminho do Financeiro — com a forma de pagamento que o paciente está usando agora. |  |
| 24 | TextBlock | {Binding Resumo} |  |
| 32 | TextBlock | As contas não puderam ser lidas. O que está abaixo não é “nada a receber”. |  |
| 39 | Button | Cobrar pelo WhatsApp | {Binding CobrarCommand} |
| 44 | Button | Fechar |  |
| 61 | TextBlock | {Binding Mensagem} |  |
| 79 | TextBlock | {Binding Descricao} |  |
| 83 | TextBlock | {Binding Vencimento} |  |
| 86 | TextBlock | {Binding Atraso} |  |
| 89 | TextBlock | {Binding ValorTexto} |  |
| 96 | ComboBox |  |  |
| 100 | ComboBox |  |  |
| 111 | Button | Receber | {Binding DataContext.ReceberCommand, RelativeSource={RelativeSource AncestorType=Window}} |
| 115 | Button |  |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/ConsultaContextualWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsultaContextualWindow.xaml) · 17 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window |  |  |
| 7 | Button | Voltar |  |
| 10 | TextBlock | {Binding Title, RelativeSource={RelativeSource AncestorType=Window}} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemView.xaml) · 64 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 22 | UserControl |  |  |
| 34 | CheckBox | Consulta de enfermagem (processo completo — COFEN 358/2009) | AoClicarNaConsulta |
| 39 | TextBlock | Marcar abre uma janela com as cinco etapas — dá para maximizar e completar depois. |  |
| 50 | TextBlock | {Binding EtapasEmFalta} |  |
| 56 | Button | Abrir a consulta de enfermagem… | AbrirConsulta |

## Clinica · src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsultaDeEnfermagemWindow.xaml) · 80 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Consulta de enfermagem — COFEN 358/2009 |  |
| 37 | Window |  |  |
| 49 | Button | Fechar | Fechar |
| 51 | TextBlock | O que você escreve aqui entra na hora, no registro de trás. Não há “Salvar” nesta janela — quem grava é o Registrar da passagem. |  |
| 59 | TextBlock | {Binding Paciente} |  |
| 61 | TextBlock | As cinco etapas da Resolução COFEN 358/2009. Você pode preencher o que tem agora e completar depois — o histórico na admissão, a avaliação no fim. |  |
| 73 | TextBlock | {Binding EtapasEmFalta} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/ConsumosPacoteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ConsumosPacoteWindow.xaml) · 104 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Sessões do pacote |  |
| 12 | TextBlock | Sessões do pacote |  |
| 13 | TextBlock | {Binding Titulo} |  |
| 15 | TextBlock | Devolver uma sessão é CANCELAR COM MOTIVO, nunca apagar: o consumo continua no histórico, marcado. Apagar a linha devolveria "o paciente diz que sobrou uma sessão" à palavra de um contra a do outro. |  |
| 19 | TextBlock | Saldo hoje: |  |
| 21 | TextBlock | {Binding Saldo} |  |
| 25 | TextBlock | {Binding Resumo} |  |
| 32 | Button | Fechar |  |
| 36 | TextBlock | {Binding Mensagem} |  |
| 38 | TextBlock |  |  |
| 64 | TextBlock | {Binding Data} |  |
| 66 | TextBlock | {Binding Origem} |  |
| 71 | TextBlock | {Binding Situacao} |  |
| 74 | TextBlock |  |  |
| 90 | Button | Devolver ao saldo | {Binding DataContext.DevolverCommand,
                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/DadosClinicaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DadosClinicaView.xaml) · 21 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 3 | TextBlock | Dados da clínica e do prestador |  |
| 4 | TextBlock | Dados usados nos documentos e guias. As regras TISS são configuradas em seu próprio bloco. |  |
| 6 | TextBlock | Razão social |  |
| 6 | TextBox | {Binding RazaoSocial, UpdateSourceTrigger=PropertyChanged} |  |
| 7 | TextBlock | Nome fantasia |  |
| 7 | TextBox | {Binding NomeFantasia, UpdateSourceTrigger=PropertyChanged} |  |
| 8 | TextBlock | CNPJ |  |
| 8 | TextBox | {Binding Cnpj, UpdateSourceTrigger=PropertyChanged} |  |
| 9 | TextBlock | CNES |  |
| 9 | TextBox | {Binding Cnes, UpdateSourceTrigger=PropertyChanged} |  |
| 10 | TextBlock | Endereço |  |
| 10 | TextBox | {Binding Endereco, UpdateSourceTrigger=PropertyChanged} |  |
| 11 | TextBlock | Telefone |  |
| 11 | TextBox | {Binding Telefone, UpdateSourceTrigger=PropertyChanged} |  |
| 12 | TextBlock | E-mail |  |
| 12 | TextBox | {Binding Email, UpdateSourceTrigger=PropertyChanged} |  |
| 13 | TextBlock | Chave Pix |  |
| 13 | TextBox | {Binding ChavePix, UpdateSourceTrigger=PropertyChanged} |  |
| 14 | TextBlock | Cidade |  |
| 14 | TextBox | {Binding Cidade, UpdateSourceTrigger=PropertyChanged} |  |
| 15 | TextBlock | Código do prestador na operadora |  |
| 15 | TextBox | {Binding CodigoNaOperadora, UpdateSourceTrigger=PropertyChanged} |  |
| 16 | TextBlock | Registro ANS da operadora |  |
| 16 | TextBox | {Binding RegistroAnsOperadora, UpdateSourceTrigger=PropertyChanged} |  |
| 18 | TextBlock | {Binding Mensagem} |  |
| 19 | Button | Salvar dados da clínica | {Binding SalvarCommand} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/DadosRecebimentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DadosRecebimentoView.xaml) · 25 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 1 | UserControl |  |  |
| 2 | TextBlock | {Binding Titulo} |  |
| 3 | TextBlock | {Binding Descricao} |  |
| 4 | TextBlock | {Binding Valor} |  |
| 5 | TextBlock | Valor pago ou recebido agora |  |
| 6 | TextBox | {Binding ValorInformado, UpdateSourceTrigger=PropertyChanged} |  |
| 7 | TextBlock | Vencimento do saldo restante (se houver) |  |
| 9 | TextBlock | Na baixa parcial, o restante continua em aberto. Somente o valor confirmado entra no caixa. |  |
| 11 | TextBlock | Data do pagamento |  |
| 13 | TextBlock | Forma de pagamento |  |
| 14 | ComboBox |  |  |
| 16 | TextBlock | Maquininha / adquirente |  |
| 17 | TextBox | {Binding Adquirente, UpdateSourceTrigger=PropertyChanged} |  |
| 18 | TextBlock | Bandeira |  |
| 19 | TextBox | {Binding Bandeira, UpdateSourceTrigger=PropertyChanged} |  |
| 20 | TextBlock | Parcelas no crédito |  |
| 21 | TextBox | {Binding Parcelas, UpdateSourceTrigger=PropertyChanged} |  |
| 22 | TextBlock | A taxa e o prazo de depósito seguem o cadastro do Financeiro. |  |
| 24 | TextBlock | {Binding Mensagem} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/DetalheSessaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DetalheSessaoWindow.xaml) · 129 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Campos da sessão |  |
| 10 | Window |  |  |
| 17 | TextBlock | Os campos separados desta sessão |  |
| 19 | TextBlock | A folha da tela de trás continua sendo a evolução. Estes campos são para quem quer separar — e são eles que o relatório do convênio imprime por assunto. Todos são opcionais. |  |
| 32 | TextBlock | O que você escrever aqui é gravado pelo “Salvar sessão” da tela de trás. |  |
| 35 | Button | Fechar |  |
| 42 | TextBlock | Queixa principal |  |
| 43 | TextBox | {Binding QueixaPrincipal, UpdateSourceTrigger=PropertyChanged} |  |
| 48 | TextBlock | História da doença atual |  |
| 49 | TextBox | {Binding HistoriaDoencaAtual, UpdateSourceTrigger=PropertyChanged} |  |
| 54 | TextBlock | Exame físico |  |
| 55 | TextBox | {Binding ExameFisico, UpdateSourceTrigger=PropertyChanged} |  |
| 60 | TextBlock | Hipótese diagnóstica |  |
| 61 | TextBox | {Binding HipoteseDiagnostica, UpdateSourceTrigger=PropertyChanged} |  |
| 73 | TextBlock | CID (opcional) |  |
| 74 | TextBox | {Binding CidSessao, UpdateSourceTrigger=PropertyChanged} |  |
| 78 | Button | Buscar CID… | {Binding BuscarCidCommand} |
| 87 | TextBlock | {Binding DescricaoCid} |  |
| 93 | TextBlock | Conduta (pontos, técnica, tempo) |  |
| 94 | TextBox | {Binding Conduta, UpdateSourceTrigger=PropertyChanged} |  |
| 98 | TextBlock | Orientações ao paciente |  |
| 99 | TextBox | {Binding Orientacoes, UpdateSourceTrigger=PropertyChanged} |  |
| 107 | TextBlock | Plano terapêutico |  |
| 108 | TextBox | {Binding PlanoTerapeutico, UpdateSourceTrigger=PropertyChanged} |  |
| 114 | TextBlock | Por que voltar |  |
| 115 | TextBox | {Binding RetornoSugeridoNota, UpdateSourceTrigger=PropertyChanged} |  |
| 121 | TextBlock | Encaminhamento |  |
| 122 | TextBox | {Binding Encaminhamento, UpdateSourceTrigger=PropertyChanged} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/DocumentoWindow.xaml) · 416 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Documento clínico |  |
| 10 | Window |  |  |
| 17 | TextBlock | {Binding TituloJanela} |  |
| 25 | TextBlock | {Binding Subtitulo} |  |
| 35 | CheckBox | Assinar digitalmente (ICP-Brasil) |  |
| 39 | Button | Fechar |  |
| 41 | Button | Emitir e imprimir | {Binding EmitirCommand} |
| 49 | TextBlock | {Binding DestinoDoQr} |  |
| 69 | TextBlock | {Binding Mensagem} |  |
| 81 | TextBlock | ATENÇÃO — esta prescrição bate com uma alergia registrada |  |
| 86 | TextBlock | {Binding} |  |
| 90 | CheckBox | Eu vi o alerta e assumo esta prescrição |  |
| 105 | TextBlock | O que a lei exige e ainda falta neste documento |  |
| 110 | TextBlock | {Binding} |  |
| 129 | TextBlock | O prontuário deste paciente registra |  |
| 134 | TextBlock | {Binding} |  |
| 155 | TextBlock | Tipo de documento |  |
| 156 | ComboBox |  |  |
| 161 | TextBlock | Data |  |
| 166 | TextBlock | Quem assina |  |
| 167 | ComboBox |  |  |
| 174 | TextBlock | {Binding EnderecoDaReceita} |  |
| 179 | TextBlock | Buscar um modelo |  |
| 183 | Button | Usar modelo | {Binding AplicarModeloCommand} |
| 185 | Expander | Prévia do modelo |  |
| 186 | TextBlock | {Binding PreviaModelo} |  |
| 191 | TextBlock | {Binding RotuloCorpo} |  |
| 194 | TextBlock | Escreva ou cole o receituário completo, incluindo o modo de usar. O texto pode ser editado livremente. |  |
| 198 | Expander | Salvar ou atualizar modelo |  |
| 200 | TextBlock | Nome do novo modelo |  |
| 201 | TextBox |  |  |
| 203 | Button | Salvar novo modelo | {Binding SalvarComoModeloCommand} |
| 204 | Button | Atualizar selecionado | {Binding AtualizarModeloCommand} |
| 205 | Button | Excluir selecionado | {Binding ExcluirModeloCommand} |
| 209 | TextBlock | Título impresso (opcional) |  |
| 210 | TextBox | {Binding Titulo} |  |
| 217 | TextBlock | {Binding RotuloItens} |  |
| 219 | Button | Adicionar linha | {Binding AdicionarItemCommand} |
| 243 | TextBox | {Binding Quantidade} |  |
| 251 | Button | Remover | {Binding DataContext.RemoverItemCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 263 | TextBlock | A indicação clínica da primeira linha veio da hipótese desta sessão. Trocar aqui não muda o prontuário. |  |
| 270 | TextBlock |  |  |
| 283 | TextBlock | Afastamento |  |
| 295 | TextBlock | Dias de afastamento |  |
| 296 | TextBox | {Binding DiasAfastamentoTexto, UpdateSourceTrigger=PropertyChanged} |  |
| 301 | TextBlock | A partir de |  |
| 306 | TextBlock | CID (opcional) |  |
| 308 | Button | Buscar… | {Binding BuscarCidCommand} |
| 313 | TextBox | {Binding Cid, UpdateSourceTrigger=PropertyChanged} |  |
| 321 | TextBox |  |  |
| 337 | TextBlock | {Binding ExplicacaoDoCid} |  |
| 346 | TextBlock | {Binding DescricaoCid} |  |
| 355 | CheckBox | O paciente autorizou imprimir o CID no atestado |  |
| 360 | TextBlock | O CID NÃO vai sair impresso: o diagnóstico é sigilo do paciente e só entra no atestado com autorização expressa dele. |  |
| 370 | TextBlock | Comparecimento |  |
| 382 | TextBlock | Dia do atendimento |  |
| 387 | TextBlock | Chegou às |  |
| 388 | TextBox | {Binding HoraChegadaTexto} |  |
| 392 | TextBlock | Saiu às |  |
| 393 | TextBox | {Binding HoraSaidaTexto} |  |
| 397 | TextBlock | Os horários vieram do carimbo da fila desta sessão. Trocar aqui não muda o prontuário. |  |
| 404 | TextBlock | Horários no formato 14:30. Em branco, a declaração sai sem horário — o que basta para a maioria dos empregadores. |  |
| 410 | TextBlock | Observações (impressas no rodapé do documento) |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EnfermagemView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EnfermagemView.xaml) · 464 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 20 | UserControl |  |  |
| 41 | Button | Atender | {Binding DataContext.AtenderCommand,
                                  RelativeSource={RelativeSource AncestorType=ListBox}} |
| 61 | TextBlock | Enfermagem |  |
| 62 | TextBlock | Todos os pacientes da clínica. Abra alguém para ver e escrever a evolução de enfermagem — sinais vitais, intercorrências e o que foi observado em cada passagem. |  |
| 73 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 96 | TextBlock | {Binding ResumoDaLista} |  |
| 100 | TextBlock | {Binding Seletor.Erro} |  |
| 128 | TextBlock | PACIENTE |  |
| 129 | TextBlock | TELEFONE |  |
| 130 | TextBlock | CONVÊNIO |  |
| 181 | Button |  Voltar | {Binding VoltarCommand} |
| 187 | TextBlock | {Binding Paciente} |  |
| 194 | TextBlock | {Binding Contexto} |  |
| 200 | TextBlock | {Binding Alerta} |  |
| 226 | TextBlock | {Binding Cronometro} |  |
| 230 | Button | {Binding FolhaDeHoje} | {Binding AbrirFolhaCommand} |
| 239 | Button | {Binding TermoPendente} | {Binding ColherTermoCommand} |
| 267 | TextBlock | PLANO DE CUIDADOS DE HOJE |  |
| 273 | TextBlock | {Binding Plano.Resumo} |  |
| 274 | TextBlock |  |  |
| 292 | TextBlock | Hora |  |
| 294 | TextBox | {Binding Plano.Hora, UpdateSourceTrigger=PropertyChanged} |  |
| 317 | TextBlock | {Binding Redacao} |  |
| 324 | TextBlock | {Binding Selo} |  |
| 327 | TextBlock | {Binding Registro} |  |
| 336 | Button | Feito | {Binding DataContext.Plano.MarcarFeitoCommand,
                                                              RelativeSource={RelativeSource AncestorType=UserControl}} |
| 343 | Button | Não feito | {Binding DataContext.Plano.MarcarNaoFeitoCommand,
                                                              RelativeSource={RelativeSource AncestorType=UserControl}} |
| 372 | TextBlock | A hora é a do FATO observado, não a de agora — o relógio do sistema fica gravado ao lado. |  |
| 388 | TextBlock | {Binding Mensagem} |  |
| 393 | Button | Cancelar correção | {Binding Passagem.CancelarCorrecaoCommand} |
| 402 | Button | Imprimir a passagem | {Binding ImprimirFichaCommand} |
| 411 | Button | {Binding Passagem.RotuloDoBotao, FallbackValue=Registrar} | {Binding Passagem.RegistrarCommand} |
| 427 | TabItem | A passagem de hoje |  |
| 441 | TabItem | Passagens do paciente |  |
| 456 | TabItem | Prontuário do paciente |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EscolhaDeConvenioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolhaDeConvenioWindow.xaml) · 103 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Qual é o convênio? |  |
| 10 | Window |  |  |
| 17 | TextBlock | Este paciente ainda não tem convênio |  |
| 24 | TextBlock |  |  |
| 30 | TextBlock | Escolha o convênio do paciente para liberar o lançamento. Quem paga do bolso é lançado no convênio particular do catálogo. |  |
| 37 | TextBlock | {Binding Mensagem} |  |
| 41 | Button | Cancelar |  |
| 44 | Button | Vincular convênio e continuar | {Binding VincularCommand} |
| 50 | TextBlock | Em branco, cada campo preserva o que a ficha já tinha. A carteirinha é o número que vai na guia; vencida, o convênio recusa na hora. |  |
| 66 | TextBlock | Carteirinha (opcional) |  |
| 67 | TextBox | {Binding Carteirinha, UpdateSourceTrigger=PropertyChanged} |  |
| 71 | TextBlock | Validade (opcional) |  |
| 85 | TextBlock | {Binding Nome} |  |
| 87 | TextBlock | {Binding Detalhe} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EscolherCertificadoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherCertificadoWindow.xaml) · 147 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Assinar com certificado digital |  |
| 10 | Window |  |  |
| 18 | TextBlock | Assinar com certificado digital |  |
| 20 | TextBlock | {Binding Assunto} |  |
| 23 | TextBlock | Escolha o SEU certificado: o sistema confere se o CPF que está dentro dele é o mesmo do profissional que assina. |  |
| 36 | Button | Atualizar lista | {Binding AtualizarCommand} |
| 44 | Button | Buscar no SafeID (nuvem) | {Binding BuscarNaNuvemCommand} |
| 53 | Button | Cancelar |  |
| 56 | Button | Assinar | {Binding ConfirmarCommand} |
| 79 | TextBlock | {Binding Mensagem} |  |
| 100 | TextBlock | {Binding Titular} |  |
| 103 | TextBlock |  |  |
| 110 | TextBlock |  |  |
| 123 | TextBlock | {Binding Impedimento} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EscolherPacienteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherPacienteWindow.xaml) · 59 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Escolher paciente |  |
| 9 | Window |  |  |
| 20 | TextBlock | Escolher paciente |  |
| 22 | TextBlock | Busque por nome ou CPF — a lista filtra enquanto você digita. |  |
| 28 | Button | Cancelar |  |
| 30 | Button | Escolher | Escolher_Click |
| 34 | TextBox | {Binding Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 55 | TextBlock | {Binding Erro} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EscolherSessaoDoTermoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherSessaoDoTermoWindow.xaml) · 53 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | A qual sessão? |  |
| 12 | TextBlock | A qual sessão este termo pertence? |  |
| 15 | TextBlock |  |  |
| 25 | Button | Cancelar |  |
| 28 | Button | Colher assinatura… | {Binding ConfirmarCommand} |
| 41 | TextBlock | {Binding Rotulo} |  |
| 43 | TextBlock | {Binding Detalhe} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EscolherSessaoEnfermagemWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherSessaoEnfermagemWindow.xaml) · 19 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Vincular enfermagem à sessão |  |
| 9 | TextBlock |  |  |
| 10 | TextBlock | Escolha a sessão original do dia do atendimento. Sessões concluídas podem receber registros tardios, sem reabrir ou gerar guias novamente. |  |
| 14 | Button | Cancelar |  |
| 15 | Button | Vincular a esta sessão | Confirmar |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EscolherTermoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscolherTermoWindow.xaml) · 68 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Qual termo? |  |
| 10 | Window |  |  |
| 17 | TextBlock | Qual termo o paciente vai assinar? |  |
| 20 | TextBlock |  |  |
| 29 | TextBlock | {Binding Mensagem} |  |
| 33 | Button | Cancelar |  |
| 36 | Button | Colher assinatura… | {Binding EscolherCommand} |
| 50 | TextBlock | {Binding Nome} |  |
| 52 | TextBlock | {Binding Titulo} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EscreverSessaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EscreverSessaoWindow.xaml) · 175 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Sessão do prontuário |  |
| 31 | Window |  |  |
| 42 | Button | Salvar sessão | {Binding SalvarCommand} |
| 46 | Button | Fechar | Fechar |
| 53 | TextBlock | {Binding DicaRodape} |  |
| 61 | TextBlock | {Binding Mensagem} |  |
| 66 | TextBlock |  |  |
| 88 | TextBlock | {Binding Titulo} |  |
| 90 | TextBlock | {Binding Paciente} |  |
| 98 | TextBlock | Quem atendeu |  |
| 99 | ComboBox |  |  |
| 114 | Button | Anexar arquivo… | {Binding AnexarCommand} |
| 122 | TextBlock | Anexos |  |
| 127 | TextBlock | Salve a sessão antes de anexar — o arquivo precisa de uma sessão para se prender. |  |
| 130 | TextBlock |  |  |
| 148 | Button | Retirar… | {Binding DataContext.RemoverAnexoCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 155 | Button | Salvar como… | {Binding DataContext.BaixarAnexoCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 160 | TextBlock | {Binding NomeArquivo} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/EvolucaoEnfermagemWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/EvolucaoEnfermagemWindow.xaml) · 117 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Evolução de enfermagem |  |
| 23 | Window |  |  |
| 31 | TextBlock | {Binding AvisoModalidadeEnfermagem} |  |
| 39 | TextBlock | Evolução de enfermagem |  |
| 41 | TextBlock | {Binding Paciente} |  |
| 72 | TextBlock | {Binding Mensagem} |  |
| 76 | Button | Cancelar correção | {Binding CancelarCorrecaoCommand} |
| 81 | Button | Fechar |  |
| 84 | Button | {Binding RotuloDoBotao} | {Binding RegistrarCommand} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/FolhaDaSessaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/FolhaDaSessaoView.xaml) · 187 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 26 | UserControl |  |  |
| 35 | TextBlock | Reutilizar evolução |  |
| 36 | ComboBox |  |  |
| 39 | Button | Usar texto anterior | {Binding ReutilizarSessaoCommand} |
| 47 | TextBlock | Data da sessão |  |
| 51 | TextBlock | EVA antes |  |
| 52 | ComboBox |  |  |
| 55 | TextBlock | EVA depois |  |
| 56 | ComboBox |  |  |
| 59 | TextBlock | Retorno sugerido |  |
| 67 | Button | Mapa corporal | {Binding AbrirMapaCommand} |
| 70 | Button | Modelos… | {Binding AbrirModelosCommand} |
| 82 | TextBlock | {Binding Contexto, RelativeSource={RelativeSource AncestorType=comp:FolhaDaSessaoView}} |  |
| 92 | Button |  | {Binding AbrirDetalheCommand} |
| 104 | TextBlock | Detalhar em campos separados… |  |
| 106 | TextBlock | queixa · exame · hipótese · conduta · plano · encaminhamento |  |
| 116 | TextBlock | {Binding SeloDetalhe} |  |
| 140 | TextBlock | {Binding Rotulo} |  |
| 144 | TextBox | {Binding Resposta, UpdateSourceTrigger=PropertyChanged} |  |
| 150 | ComboBox |  |  |
| 156 | ComboBox |  |  |
| 164 | TextBlock | {Binding Ajuda} |  |
| 177 | TextBlock | A sessão de hoje |  |
| 179 | TextBox | {Binding TextoEvolucao, UpdateSourceTrigger=PropertyChanged} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/FolhaExecucaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/FolhaExecucaoWindow.xaml) · 355 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Folha de execução |  |
| 10 | Window |  |  |
| 23 | TextBlock | {Binding TextoOperacao} |  |
| 32 | TextBlock |  |  |
| 36 | TextBlock | {Binding Paciente} |  |
| 39 | TextBlock | {Binding Cabecalho} |  |
| 41 | TextBlock | {Binding DiluicaoTotal} |  |
| 43 | TextBlock | {Binding Resumo} |  |
| 49 | TextBlock | {Binding MedicacaoEmUso} |  |
| 61 | TextBlock | {Binding AvisoDoConselho} |  |
| 69 | TextBlock | {Binding} |  |
| 78 | TextBlock | Devolvida pelo médico · revise e crie nova versão |  |
| 79 | TextBlock | {Binding MotivoDevolucao} |  |
| 98 | TextBlock | Data da administração |  |
| 102 | TextBlock | Hora para os itens |  |
| 104 | TextBox | {Binding Hora, UpdateSourceTrigger=PropertyChanged} |  |
| 106 | TextBlock | Escreva o horário em que o item foi de fato administrado. O sistema guarda ao lado a hora em que você registrou. |  |
| 115 | TextBlock | Encerrar fecha a folha: ela sai da sala e não pode mais ser checada. |  |
| 120 | TextBlock | {Binding SituacaoAssinaturaExecucao} |  |
| 128 | Button | Imprimir folha | {Binding ImprimirCommand} |
| 133 | Button | Imprimir registro | {Binding ImprimirRegistroCommand} |
| 143 | Button | Anotar | {Binding AnotarCommand} |
| 149 | Button | Fechar |  |
| 152 | Button | Encerrar execução | {Binding EncerrarCommand} |
| 160 | Button | Validar e assinar como médico | {Binding ValidarMedicoCommand} |
| 163 | Button | Devolver à enfermagem | {Binding DevolverMedicoCommand} |
| 167 | Button | Revisar devolução | {Binding RevisarDevolucaoCommand} |
| 170 | Button | Corrigir horários | {Binding CorrigirHorariosCommand} |
| 174 | Button | Cancelar registro | {Binding CancelarInfusaoCommand} |
| 178 | Button | Assinar execução | {Binding AssinarExecucaoCommand} |
| 203 | TextBlock | {Binding Mensagem} |  |
| 224 | TextBlock | {Binding Ordem} |  |
| 230 | TextBlock | {Binding Descricao} |  |
| 233 | TextBlock | {Binding Detalhe} |  |
| 246 | TextBlock | {Binding AlertaAlergia} |  |
| 250 | TextBlock | {Binding Justificativa} |  |
| 257 | TextBlock | {Binding Executante} |  |
| 268 | TextBlock | {Binding Marca} |  |
| 272 | TextBlock |  |  |
| 295 | Button | ☐ Sim | {Binding DataContext.RealizadoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 302 | Button | ☐ Não | {Binding DataContext.NaoRealizadoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 309 | Button | ☐ Não executável | {Binding DataContext.NaoExecutavelCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 314 | Button | Retificar | {Binding DataContext.RetificarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 326 | Button | Suspender | {Binding DataContext.SuspenderCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/InfusaoExternaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/InfusaoExternaWindow.xaml) · 57 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Registrar infusão realizada |  |
| 12 | TextBlock | Registrando infusão realizada… |  |
| 21 | TextBlock | {Binding Paciente} |  |
| 22 | TextBlock | Registre a infusão já realizada com orientação médica fora do sistema. Sua assinatura registra a execução; a validação fica pendente para o médico responsável. |  |
| 26 | TextBlock | {Binding Mensagem} |  |
| 28 | Button | Cancelar |  |
| 29 | Button | Salvar registro e revisar assinatura | {Binding SalvarCommand} |
| 34 | TextBlock | {Binding Alertas} |  |
| 36 | TextBlock | Data da execução |  |
| 37 | TextBlock | Hora da execução |  |
| 37 | TextBox | {Binding Hora} |  |
| 38 | TextBlock | Data da prescrição |  |
| 39 | TextBlock | Hora da prescrição |  |
| 39 | TextBox | {Binding HoraPrescricao} |  |
| 40 | TextBlock | Diluente |  |
| 40 | TextBox | {Binding Diluente} |  |
| 41 | TextBlock | Volume |  |
| 41 | TextBox | {Binding Volume} |  |
| 42 | TextBlock | Tempo |  |
| 42 | TextBox | {Binding Tempo} |  |
| 44 | TextBlock | Sessão do paciente |  |
| 45 | ComboBox |  |  |
| 46 | TextBlock | Médico responsável pela orientação |  |
| 47 | ComboBox |  |  |
| 48 | TextBlock | Orientação recebida fora do sistema |  |
| 49 | TextBox | {Binding Orientacao, UpdateSourceTrigger=PropertyChanged} |  |
| 50 | TextBlock | Infusão realizada — texto livre |  |
| 51 | TextBox | {Binding Texto, UpdateSourceTrigger=PropertyChanged} |  |
| 52 | CheckBox | Conferi os alertas de alergia deste paciente |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/LegendaFamiliasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/LegendaFamiliasView.xaml) · 62 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 33 | TextBlock | Modalidade informada em cada atendimento |  |
| 41 | TextBlock | traço espesso = encaixe |  |
| 48 | TextBlock | agenda fechada |  |
| 55 | TextBlock |  |  |
| 59 | TextBlock | confirmou na rodada de confirmação |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/LinhaDoTempoClinicaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/LinhaDoTempoClinicaView.xaml) · 226 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 24 | UserControl |  |  |
| 59 | TextBlock | {Binding Resumo} |  |
| 92 | TextBlock | {Binding Data, StringFormat=dd/MM/yyyy} |  |
| 105 | TextBlock | {Binding HoraTexto} |  |
| 111 | TextBlock | {Binding Rotulo} |  |
| 118 | TextBlock | {Binding Titulo} |  |
| 119 | TextBlock | {Binding Detalhe} |  |
| 125 | TextBlock | {Binding Marca} |  |
| 131 | TextBlock | {Binding Autor} |  |
| 153 | TextBlock | INTERCORRÊNCIA |  |
| 165 | Button | Ver | {Binding DataContext.VerCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 180 | Button | {Binding DataContext.RotuloAbrir, RelativeSource={RelativeSource AncestorType=ItemsControl}} | {Binding DataContext.AbrirCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 189 | Button | Cancelar… | {Binding DataContext.CancelarCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/MapaCorporalControl.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MapaCorporalControl.xaml) · 164 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | UserControl |  |  |
| 10 | Button |  | {Binding DataContext.SelecionarPontoCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 14 | Button |  |  |
| 22 | TextBlock | {Binding Numero} |  |
| 40 | TextBlock | Copiar de outra sessão |  |
| 42 | Button | Copiar mapa | {Binding CopiarSessaoCommand} |
| 44 | ComboBox |  |  |
| 47 | TextBlock | {Binding EstadoDoHistorico} |  |
| 50 | TextBlock | Usar um modelo de pontos |  |
| 52 | Button | Usar modelo | {Binding AplicarProtocoloCommand} |
| 54 | ComboBox |  |  |
| 57 | TextBlock | {Binding EstadoDosModelos} |  |
| 60 | Expander | Salvar como modelo / gerenciar modelos |  |
| 62 | TextBlock | Marque os pontos no corpo, dê um nome e salve para reutilizar nas próximas sessões. |  |
| 64 | TextBlock | Nome do modelo |  |
| 66 | Button | Salvar modelo com estes pontos | {Binding SalvarComoProtocoloCommand} |
| 68 | TextBox | {Binding NomeDoModelo, UpdateSourceTrigger=PropertyChanged} |  |
| 72 | Button | Excluir modelo selecionado | {Binding ExcluirProtocoloCommand} |
| 74 | CheckBox | Disponível para toda a clínica |  |
| 86 | TextBlock | Frente |  |
| 97 | TextBlock | Costas |  |
| 117 | TextBlock | Técnica |  |
| 118 | ComboBox |  |  |
| 122 | TextBlock | Próximo ponto (opcional) |  |
| 123 | TextBox | {Binding NomeProximoPonto, UpdateSourceTrigger=PropertyChanged} |  |
| 128 | TextBlock | Pontos desta sessão |  |
| 129 | TextBlock | {Binding Resumo} |  |
| 131 | Button | Desfazer | {Binding DesfazerCommand} |
| 133 | Button | Limpar pontos | {Binding LimparCommand} |
| 139 | Expander | Editar ponto selecionado |  |
| 144 | TextBox | {Binding PontoSelecionado.Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 145 | ComboBox |  |  |
| 146 | Button | Remover | {Binding RemoverPontoCommand} |
| 148 | TextBlock | Observação do ponto (opcional) |  |
| 149 | TextBox | {Binding PontoSelecionado.Observacao, UpdateSourceTrigger=PropertyChanged} |  |
| 153 | TextBlock | Observações do mapa (opcional) |  |
| 154 | TextBox | {Binding Observacoes, UpdateSourceTrigger=PropertyChanged} |  |
| 161 | TextBlock | {Binding Mensagem} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/MapaCorporalWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MapaCorporalWindow.xaml) · 42 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Mapa corporal |  |
| 25 | TextBlock | {Binding Titulo} |  |
| 27 | TextBlock | Copie um mapa ou clique no corpo para marcar. Os pontos serão gravados ao salvar ou finalizar o atendimento. |  |
| 33 | Button | Descartar alterações |  |
| 35 | Button | Usar mapa nesta sessão | Concluir |

## Clinica · src/Clinica.Desktop.Shell/Componentes/MateriaisProcedimentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/MateriaisProcedimentoWindow.xaml) · 48 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Materiais do procedimento |  |
| 8 | TextBlock | {Binding Contexto} |  |
| 9 | TextBlock | Quais materiais foram usados? |  |
| 10 | TextBlock | Informe as quantidades na unidade de consumo e os lotes utilizados. Materiais já registrados aparecem preenchidos para conferência. |  |
| 12 | TextBlock | Buscar produto por nome ou código |  |
| 13 | TextBox | {Binding Busca, UpdateSourceTrigger=PropertyChanged} |  |
| 14 | TextBlock | O filtro mantém as quantidades preenchidas. Use Outro lote quando o mesmo produto veio de mais de um lote. |  |
| 18 | TextBlock | {Binding Situacao} |  |
| 19 | CheckBox | Confirmo que não houve consumo de materiais nesta sessão |  |
| 20 | TextBlock | {Binding Erro} |  |
| 22 | Button | Fechar |  |
| 23 | Button | {Binding RotuloConfirmar} | {Binding ConfirmarCommand} |
| 32 | TextBlock | {Binding Nome} |  |
| 33 | TextBlock | {Binding Saldo} |  |
| 36 | TextBlock | Quantidade utilizada |  |
| 36 | TextBox | {Binding Quantidade, UpdateSourceTrigger=PropertyChanged} |  |
| 37 | TextBlock | Lote utilizado |  |
| 37 | TextBox | {Binding Lote, UpdateSourceTrigger=PropertyChanged} |  |
| 38 | Button | Outro lote | {Binding DataContext.OutroLoteCommand, RelativeSource={RelativeSource AncestorType=Window}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/ModelosEvolucaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ModelosEvolucaoWindow.xaml) · 146 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Modelos de evolução |  |
| 10 | Window |  |  |
| 16 | TextBlock | Modelos de evolução |  |
| 18 | TextBlock | Aplicar COPIA o texto para esta sessão — daí em diante ele é dela. Corrigir o modelo depois não reescreve nenhuma sessão já salva, e nada é gravado até você salvar a evolução. |  |
| 25 | Button | Repetir a última sessão deste paciente | {Binding RepetirCommand} |
| 33 | Button | Fechar |  |
| 36 | Button | Aplicar nesta sessão | {Binding AplicarCommand} |
| 48 | TextBlock | {Binding AvisoDoModelo} |  |
| 65 | TextBlock | {Binding Mensagem} |  |
| 71 | TextBlock | GUARDAR ESTA SESSÃO COMO MODELO |  |
| 81 | TextBox | {Binding NomeNovo, UpdateSourceTrigger=PropertyChanged} |  |
| 85 | CheckBox | Da clínica |  |
| 89 | Button | Guardar | {Binding SalvarComoModeloCommand} |
| 96 | TextBlock | Guarda o que está escrito na evolução agora. Um nome já usado sobrescreve o modelo anterior. |  |
| 106 | Button | Apagar o modelo escolhido | {Binding ApagarCommand} |
| 119 | TextBlock | {Binding Nome} |  |
| 124 | TextBlock | {Binding Dono} |  |
| 127 | TextBlock | {Binding Previa} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PacoteCatalogoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PacoteCatalogoWindow.xaml) · 77 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Pacote do catálogo |  |
| 9 | Window |  |  |
| 15 | TextBlock | {Binding Titulo} |  |
| 16 | TextBlock | O que a clínica vende. A venda COPIA estes dados: mudar o preço aqui em novembro não reescreve o que o paciente comprou em março. |  |
| 19 | TextBlock | Nome |  |
| 20 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 30 | TextBlock | Tipo |  |
| 31 | ComboBox |  |  |
| 36 | TextBlock | Sessões |  |
| 37 | TextBox | {Binding Sessoes, UpdateSourceTrigger=PropertyChanged} |  |
| 41 | TextBlock | Sem número de sessões, o plano é livre dentro da validade — é assim que se cadastra mensalidade. |  |
| 53 | TextBlock | Valor |  |
| 54 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 58 | TextBlock | Validade (dias) |  |
| 59 | TextBox | {Binding ValidadeDias, UpdateSourceTrigger=PropertyChanged} |  |
| 65 | TextBlock | {Binding Mensagem} |  |
| 69 | Button | Cancelar |  |
| 71 | Button | {Binding RotuloDoBotao} | {Binding SalvarCommand} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PacoteVendaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PacoteVendaWindow.xaml) · 242 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Vender pacote |  |
| 9 | Window |  |  |
| 18 | TextBlock | Vender pacote |  |
| 23 | Button | Cancelar |  |
| 25 | Button | Vender | {Binding SalvarCommand} |
| 47 | TextBlock | {Binding Mensagem} |  |
| 52 | TextBlock | Paciente |  |
| 53 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 94 | TextBlock | {Binding Seletor.Selecionado.Nome} |  |
| 100 | TextBlock | Pacote |  |
| 114 | TextBlock | Nenhum pacote à venda no catálogo — é o primeiro passo. O catálogo é o que a clínica oferece ("10 sessões", "mensal") e quanto custa; a venda copia o preço dele, então reajustar depois não muda o que já foi vendido. |  |
| 116 | Button | Cadastrar um pacote no catálogo… | {Binding CadastrarNoCatalogoCommand} |
| 130 | ComboBox |  |  |
| 132 | ComboBox |  |  |
| 150 | TextBlock | Data da compra |  |
| 154 | TextBlock | Valor cobrado |  |
| 155 | TextBox | {Binding ValorCobrado, UpdateSourceTrigger=PropertyChanged} |  |
| 160 | TextBlock | O valor vem do catálogo e continua editável: desconto no balcão é regra, não exceção. Sessões e validade são copiadas do pacote escolhido. |  |
| 167 | TextBlock | Como o paciente paga |  |
| 169 | RadioButton | Pagar agora |  |
| 170 | RadioButton | Cobrar depois |  |
| 183 | TextBlock | Forma |  |
| 184 | ComboBox |  |  |
| 188 | TextBlock | da entrada, se houver |  |
| 195 | TextBlock | Entrada hoje |  |
| 196 | TextBox | {Binding Entrada, UpdateSourceTrigger=PropertyChanged} |  |
| 201 | TextBlock | Parcelas · 1º vencimento |  |
| 208 | TextBox | {Binding Parcelas, UpdateSourceTrigger=PropertyChanged} |  |
| 216 | TextBlock | Maquininha / contrato |  |
| 217 | TextBox | {Binding Adquirente, UpdateSourceTrigger=PropertyChanged} |  |
| 218 | TextBlock | Bandeira |  |
| 219 | TextBox | {Binding Bandeira, UpdateSourceTrigger=PropertyChanged} |  |
| 221 | TextBlock | Parcelas na maquininha |  |
| 222 | TextBox | {Binding ParcelasCartao, UpdateSourceTrigger=PropertyChanged} |  |
| 224 | TextBlock | Use Pagar agora para o total aprovado no cartão, mesmo parcelado. Cobrar depois cria cobranças futuras ao paciente; os dados do cartão se aplicam somente à entrada. |  |
| 231 | TextBlock | {Binding PreviaDasParcelas} |  |
| 235 | TextBlock | Observações |  |
| 236 | TextBox | {Binding Observacoes} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PacotesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PacotesView.xaml) · 181 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 17 | TextBlock | Pacotes, planos e vouchers |  |
| 18 | TextBlock | O que a clínica vende e quanto cada paciente ainda tem para usar. Não confundir com a cota do convênio: aquilo o convênio autoriza, isto a clínica vendeu. |  |
| 21 | TextBlock | {Binding Resumo} |  |
| 26 | Button | Catálogo… | {Binding AbrirCatalogoCommand} |
| 32 | Button | Orçamento… | {Binding OrcarCommand} |
| 58 | TextBlock | Paciente |  |
| 59 | TextBox | {Binding FiltroPaciente, UpdateSourceTrigger=PropertyChanged} |  |
| 64 | CheckBox | Só ativos |  |
| 68 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 78 | TextBlock | {Binding Mensagem} |  |
| 86 | TextBlock | Pacotes vendidos |  |
| 89 | Button | Atualizar | {Binding CarregarCommand} |
| 91 | Button | Vender pacote… | {Binding VenderCommand} |
| 111 | TextBlock | {Binding Paciente} |  |
| 113 | TextBlock | {Binding Nome} |  |
| 116 | TextBlock |  |  |
| 137 | TextBlock | Parcela vencida |  |
| 140 | TextBlock | {Binding Situacao} |  |
| 142 | Button | Usar sessão | {Binding DataContext.ConsumirCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 151 | Button | Sessões… | {Binding DataContext.VerSessoesCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 156 | Button | Cancelar | {Binding DataContext.CancelarPacoteCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PainelDoPacienteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PainelDoPacienteWindow.xaml) · 110 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Assinatura do paciente |  |
| 33 | TextBlock | {Binding Titulo} |  |
| 36 | TextBlock | {Binding PacienteNome} |  |
| 47 | TextBlock | {Binding Corpo} |  |
| 58 | TextBlock | {Binding Resposta} |  |
| 64 | TextBlock | {Binding Descricao} |  |
| 80 | Button | Apagar e assinar de novo | AoLimpar |
| 86 | TextBlock | Assine no espaço abaixo |  |
| 97 | TextBlock | ✎ Assine aqui |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PassagemDeEnfermagemView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PassagemDeEnfermagemView.xaml) · 130 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 29 | UserControl |  |  |
| 42 | TextBlock | {Binding Contexto} |  |
| 47 | TextBlock | Hora do fato e sinais vitais (preencha só o que aferiu) |  |
| 51 | TextBlock | Data do atendimento |  |
| 56 | TextBlock | Hora do fato |  |
| 57 | TextBox | {Binding Hora, UpdateSourceTrigger=PropertyChanged} |  |
| 62 | TextBlock | PA sistólica |  |
| 63 | TextBox | {Binding Sistolica, UpdateSourceTrigger=PropertyChanged} |  |
| 67 | TextBlock | PA diastólica |  |
| 68 | TextBox | {Binding Diastolica, UpdateSourceTrigger=PropertyChanged} |  |
| 72 | TextBlock | FC (bpm) |  |
| 73 | TextBox | {Binding Cardiaca, UpdateSourceTrigger=PropertyChanged} |  |
| 76 | TextBlock | FR (irpm) |  |
| 77 | TextBox | {Binding Respiratoria, UpdateSourceTrigger=PropertyChanged} |  |
| 80 | TextBlock | SpO₂ (%) |  |
| 81 | TextBox | {Binding Saturacao, UpdateSourceTrigger=PropertyChanged} |  |
| 101 | CheckBox | Foi uma intercorrência |  |
| 110 | TextBlock | Alergia observada — a quê? (opcional) |  |
| 112 | TextBox | {Binding AlergiaObservada, UpdateSourceTrigger=PropertyChanged} |  |
| 120 | TextBlock | O que eu observei |  |
| 122 | TextBox | {Binding Texto, UpdateSourceTrigger=PropertyChanged} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PassagensDeEnfermagemView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PassagensDeEnfermagemView.xaml) · 180 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 31 | UserControl |  |  |
| 58 | TextBlock | {Binding Hora} |  |
| 61 | TextBlock |  |  |
| 80 | TextBlock | {Binding Data} |  |
| 86 | TextBlock | {Binding Texto} |  |
| 87 | TextBlock |  |  |
| 101 | TextBlock | {Binding SinaisVitais} |  |
| 107 | TextBlock | {Binding Marca} |  |
| 116 | TextBlock | {Binding Assinatura} |  |
| 131 | TextBlock | DESTA SESSÃO |  |
| 136 | TextBlock | INTERCORRÊNCIA |  |
| 141 | Button | Vincular à sessão | {Binding DataContext.VincularSessaoCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 146 | Button | Corrigir | {Binding DataContext.CorrigirCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 153 | Button | Cancelar | {Binding DataContext.CancelarRegistroCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PrecoParticularWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PrecoParticularWindow.xaml) · 83 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Preço do particular |  |
| 9 | Window |  |  |
| 16 | TextBlock | {Binding Titulo} |  |
| 17 | TextBlock | Quanto a clínica cobra do paciente PARTICULAR por esta sessão. É PROPOSTA: o Finalizar da Recepção e a Conciliação do Financeiro preenchem o valor com ele, e quem está no balcão pode dar desconto no clique. |  |
| 20 | TextBlock | Modalidade (o que foi feito) |  |
| 21 | ComboBox |  |  |
| 24 | TextBlock | Especialidade atendida |  |
| 25 | ComboBox |  |  |
| 27 | TextBlock | É o que diferencia a consulta de psiquiatria da de geriatria. Em branco, o preço vale para qualquer especialidade desta modalidade — o caso normal da acupuntura. Um preço com especialidade VENCE o genérico da modalidade. |  |
| 31 | TextBlock | Valor da sessão |  |
| 32 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 41 | TextBlock | Vigente de |  |
| 45 | TextBlock | Até |  |
| 50 | TextBlock | Reajuste entra como uma linha NOVA, com a data em que passou a valer. A sessão de março continua sendo proposta pelo valor de março, e o que já foi lançado no caixa nunca muda. |  |
| 54 | CheckBox | Ativo |  |
| 71 | TextBlock | {Binding Mensagem} |  |
| 75 | Button | Cancelar |  |
| 77 | Button | Salvar preço | {Binding SalvarCommand} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/PrecosParticularView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/PrecosParticularView.xaml) · 153 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 18 | TextBlock | Preços do particular |  |
| 19 | TextBlock | Quanto a clínica cobra do paciente sem convênio, por modalidade e especialidade atendida. É o valor que o Finalizar propõe ao concluir a sessão de um particular e que a Conciliação do Financeiro usa na aba Particulares — proposta, não imposição: o desconto é dado no clique. |  |
| 38 | TextBlock | {Binding Mensagem} |  |
| 45 | TextBlock | Preços do particular |  |
| 46 | TextBlock | {Binding Resumo} |  |
| 51 | CheckBox | Só o que vale hoje |  |
| 53 | Button | Novo preço | {Binding NovoPrecoCommand} |
| 56 | Button | Atualizar | {Binding CarregarCommand} |
| 61 | TextBlock | {Binding VazioDescricao} |  |
| 63 | TextBlock |  |  |
| 90 | TextBlock | {Binding Modalidade} |  |
| 92 | TextBlock |  |  |
| 99 | TextBlock | {Binding Valor} |  |
| 114 | TextBlock |  |  |
| 115 | TextBlock |  |  |
| 130 | Button | Editar | {Binding DataContext.EditarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 137 | Button | Excluir | {Binding DataContext.ExcluirCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/ProcessoDeEnfermagemView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/ProcessoDeEnfermagemView.xaml) · 294 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 34 | UserControl |  |  |
| 39 | TabItem | 1 · Histórico |  |
| 43 | TextBlock | 1 · Histórico de enfermagem (coleta de dados) |  |
| 45 | TextBox | {Binding Historico, UpdateSourceTrigger=PropertyChanged} |  |
| 59 | TextBlock | Acesso venoso (quando há) |  |
| 68 | TextBox | {Binding AcessoLocal, UpdateSourceTrigger=PropertyChanged} |  |
| 72 | TextBox | {Binding AcessoCalibre, UpdateSourceTrigger=PropertyChanged} |  |
| 80 | TextBlock | Em branco quer dizer “não avaliado”, e não “não há acesso” — se não há, escreva no texto da passagem. |  |
| 84 | TextBlock | Exame físico |  |
| 85 | TextBox | {Binding ExameFisico, UpdateSourceTrigger=PropertyChanged} |  |
| 94 | TabItem | 2 e 3 · Diagnósticos |  |
| 104 | TextBlock | 2 e 3 · Diagnóstico de enfermagem e resultado esperado |  |
| 106 | TextBlock | A redação tem três partes — o problema, o “relacionado a” e o “evidenciado por”. É a terceira que permite avaliar depois se ele foi resolvido. |  |
| 110 | Button | Escrever à mão | {Binding NovoDiagnosticoCommand} |
| 130 | Button | Escolher do catálogo… | AbrirCatalogoDeDiagnosticos |
| 146 | TextBox | {Binding Titulo, UpdateSourceTrigger=PropertyChanged} |  |
| 150 | Button | Remover | {Binding DataContext.RemoverDiagnosticoCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 158 | TextBlock | relacionado a |  |
| 160 | TextBox | {Binding RelacionadoA, UpdateSourceTrigger=PropertyChanged} |  |
| 164 | TextBlock | evidenciado por |  |
| 166 | TextBox | {Binding EvidenciadoPor, UpdateSourceTrigger=PropertyChanged} |  |
| 170 | TextBlock | Resultado esperado (etapa 3 — o planejamento) |  |
| 173 | TextBox | {Binding ResultadoEsperado, UpdateSourceTrigger=PropertyChanged} |  |
| 185 | TabItem | 4 · Cuidados |  |
| 195 | TextBlock | 4 · Prescrição de enfermagem |  |
| 197 | TextBlock | O que a ENFERMAGEM prescreve — os cuidados. Não confunda com a folha de infusão, que é prescrição médica e a enfermagem executa. A frequência é parte do cuidado: “verificar o acesso” sem dizer de quanto em quanto tempo é lembrete, não prescrição. |  |
| 201 | Button | Escrever à mão | {Binding NovoCuidadoCommand} |
| 220 | Button | Escolher do catálogo… | AbrirCatalogoDeCuidados |
| 237 | TextBox | {Binding Descricao, UpdateSourceTrigger=PropertyChanged} |  |
| 242 | TextBlock | sugerido pelo diagnóstico — confira e ajuste |  |
| 251 | TextBox | {Binding Frequencia, UpdateSourceTrigger=PropertyChanged} |  |
| 258 | CheckBox | se necessário |  |
| 263 | Button | Remover | {Binding DataContext.RemoverCuidadoCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 277 | TabItem | 5 · Avaliação |  |
| 281 | TextBlock | 5 · Avaliação de enfermagem |  |
| 283 | TextBlock | O que aconteceu com o que foi prescrito. É a etapa que fecha o processo, e a que mais some dos prontuários — sem ela o plano vira uma lista de intenções que ninguém confere. |  |
| 286 | TextBox | {Binding Avaliacao, UpdateSourceTrigger=PropertyChanged} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/RecebimentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/RecebimentoWindow.xaml) · 25 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | {Binding Titulo} |  |
| 6 | Window |  |  |
| 16 | Button | Cancelar |  |
| 17 | Button |  |  |
| 21 | Button | Confirmar | {Binding ConfirmarCommand} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/RegrasFaturamentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/RegrasFaturamentoView.xaml) · 47 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 3 | TextBlock | Regras de faturamento |  |
| 4 | TextBlock | Os mesmos prazos são utilizados no Gerente e no Faturamento. |  |
| 17 | TextBlock | Alerta de consulta (dias) |  |
| 19 | TextBox | {Binding JanelaAlertaConsulta} |  |
| 22 | TextBlock | Recurso de glosa (dias) |  |
| 24 | TextBox | {Binding PrazoRecursoGlosa} |  |
| 27 | TextBlock | Prazo de decisão (dias) |  |
| 29 | TextBox | {Binding IntervaloRodadaPendencias} |  |
| 33 | TextBlock | O prazo de decisão conta a partir de a guia VIRAR pendente (a data prevista de faturamento), não do atendimento: o 2º código só existe 24 h depois, e contar do atendimento cobraria decisão sobre uma guia que ninguém tinha como tirar. |  |
| 37 | CheckBox | A rodada de pendências também cobra consultas a renovar |  |
| 39 | CheckBox | A rodada de pendências também cobra carteirinhas vencidas |  |
| 42 | Button | Salvar faturamento | {Binding SalvarFaturamentoCommand} |
| 44 | TextBlock | {Binding Mensagem} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/SalaInfusaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/SalaInfusaoView.xaml) · 264 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 25 | TextBlock |  |  |
| 26 | TextBlock | {Binding Situacao} |  |
| 46 | TextBlock | Sala de infusão |  |
| 47 | TextBlock | As prescrições assinadas de hoje. Abrir a folha é onde se checa item a item: ✓ com o horário quando foi feito, horário circulado e justificativa quando não foi. |  |
| 50 | TextBlock | {Binding Resumo} |  |
| 56 | TextBox | {Binding CodigoBusca, UpdateSourceTrigger=PropertyChanged} |  |
| 59 | Button | Abrir pelo código | {Binding AbrirPorCodigoCommand} |
| 63 | CheckBox | Mostrar encerradas |  |
| 65 | Button | Atualizar | {Binding CarregarCommand} |
| 71 | TextBlock | ✓ Concluída · ◉ Etapa atual · ○ Próxima etapa · ! Precisa de revisão |  |
| 73 | Button | Registrar infusão com orientação externa | {Binding RegistrarInfusaoCommand} |
| 82 | TextBlock | Avaliação médica de infusões externas |  |
| 83 | TextBlock | {Binding ResumoValidacoes} |  |
| 91 | Button | Avaliar e assinar | {Binding DataContext.AbrirCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 95 | TextBlock | {Binding Paciente} |  |
| 96 | TextBlock | {Binding Numero} |  |
| 122 | TextBlock | {Binding Mensagem} |  |
| 144 | TextBlock | {Binding Hora} |  |
| 151 | TextBlock | {Binding Paciente} |  |
| 155 | TextBlock |  |  |
| 162 | TextBlock | {Binding Itens} |  |
| 177 | TextBlock | {Binding Progresso} |  |
| 190 | TextBlock | Falta a assinatura eletrônica da enfermagem |  |
| 197 | TextBlock | Assinatura recebida · falta arquivar o registro da execução |  |
| 204 | TextBlock |  |  |
| 216 | Button | Imprimir | {Binding DataContext.ImprimirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 230 | Button | Anotar | {Binding DataContext.AnotarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 239 | Button | Abrir folha | {Binding DataContext.AbrirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Desktop.Shell/Componentes/SessaoDoProntuarioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/SessaoDoProntuarioWindow.xaml) · 160 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Sessão do prontuário |  |
| 22 | Window |  |  |
| 31 | TextBlock | {Binding Titulo} |  |
| 33 | TextBlock | {Binding Paciente} |  |
| 35 | TextBlock | {Binding LinhaDaSessao} |  |
| 50 | TextBlock | {Binding AvisoCancelamento} |  |
| 85 | TextBlock | {Binding Mensagem} |  |
| 88 | TextBlock | {Binding Procedencia} |  |
| 95 | Button | {Binding AnexosTexto} | {Binding VerAnexosCommand} |
| 102 | Button | {Binding CorrecoesTexto} | {Binding VerCorrecoesCommand} |
| 109 | Button | Fechar |  |
| 116 | Button | Imprimir esta sessão | {Binding ImprimirCommand} |
| 138 | TextBlock | {Binding Rotulo} |  |
| 139 | TextBlock | {Binding Texto} |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/TelaComAbas.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/TelaComAbas.xaml) · 44 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |

## Clinica · src/Clinica.Desktop.Shell/Componentes/TrocaSenhaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/TrocaSenhaWindow.xaml) · 37 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Trocar a minha senha |  |
| 12 | TextBlock | Trocar a minha senha |  |
| 13 | TextBlock | Pede a senha atual de propósito: sem ela, qualquer pessoa que encontrasse a sessão aberta no balcão trocaria a senha de quem levantou por um minuto. |  |
| 16 | TextBlock | Senha atual |  |
| 19 | TextBlock | Nova senha |  |
| 22 | TextBlock | Repita a nova senha |  |
| 25 | TextBlock |  |  |
| 30 | Button | Cancelar |  |
| 32 | Button | Trocar a senha | Confirmar |

## Clinica · src/Clinica.Desktop.Shell/Componentes/VersoesEvolucaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Componentes/VersoesEvolucaoWindow.xaml) · 108 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Correções da sessão |  |
| 10 | Window |  |  |
| 19 | TextBlock | {Binding Titulo} |  |
| 21 | TextBlock | {Binding Resumo} |  |
| 27 | TextBlock | {Binding Mensagem} |  |
| 31 | TextBlock | Não há como restaurar uma versão antiga, e isso é decisão: reverter em silêncio uma correção é o gesto que a rastreabilidade existe para impedir. Para voltar atrás, corrija de novo — a correção entra como versão nova. |  |
| 42 | TextBlock | {Binding Titulo} |  |
| 49 | TextBlock | EM VIGOR |  |
| 51 | TextBlock | {Binding Quando} |  |
| 56 | TextBlock | {Binding Motivo} |  |
| 61 | TextBlock | {Binding Eva} |  |
| 63 | TextBlock | Queixa |  |
| 64 | TextBlock | {Binding Queixa} |  |
| 73 | TextBlock | Anamnese, exame e hipótese |  |
| 75 | TextBlock | {Binding Anamnese} |  |
| 79 | TextBlock | Conduta |  |
| 80 | TextBlock | {Binding Conduta} |  |
| 82 | TextBlock | Evolução |  |
| 83 | TextBlock | {Binding Evolucao} |  |
| 85 | TextBlock | Orientações |  |
| 86 | TextBlock | {Binding Orientacoes} |  |
| 89 | TextBlock | Plano terapêutico |  |
| 90 | TextBlock | {Binding Plano} |  |

## Clinica · src/Clinica.Desktop.Shell/Controls/CapturaFotoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Controls/CapturaFotoWindow.xaml) · 62 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Foto do paciente |  |
| 12 | TextBlock | Foto do paciente |  |
| 13 | TextBlock |  |  |
| 14 | TextBlock | Enquadre o rosto no círculo, com o paciente de frente e boa luz. A área mostrada é exatamente a que será gravada. |  |
| 27 | TextBlock | Ligando a câmera… |  |
| 35 | TextBlock | Câmera |  |
| 36 | ComboBox |  |  |
| 41 | TextBlock |  |  |
| 46 | Button | Escolher arquivo… | Arquivo_Click |
| 51 | Button | Cancelar | Cancelar_Click |
| 53 | Button | Repetir | Repetir_Click |
| 55 | Button | Capturar | Capturar_Click |
| 56 | Button | Usar esta foto | Usar_Click |

## Clinica · src/Clinica.Desktop.Shell/Controls/PromptWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Controls/PromptWindow.xaml) · 30 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window |  |  |
| 12 | TextBlock |  |  |
| 13 | TextBlock |  |  |
| 16 | TextBox |  |  |
| 18 | TextBlock |  |  |
| 23 | Button | Cancelar |  |
| 25 | Button | Confirmar | Confirmar |

## Clinica · src/Clinica.Desktop.Shell/Shell/LoginWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/LoginWindow.xaml) · 170 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Entrar |  |
| 63 | TextBlock | Sistema de gestão da clínica |  |
| 67 | TextBlock | Agenda, prontuário, faturamento e financeiro no mesmo lugar. |  |
| 87 | TextBlock |  |  |
| 88 | TextBlock |  |  |
| 93 | TextBlock | Usuário |  |
| 94 | TextBox |  |  |
| 96 | TextBlock | Senha |  |
| 102 | TextBlock | Seu nome |  |
| 103 | TextBox |  |  |
| 105 | TextBlock | Usuário (sem espaços) |  |
| 106 | TextBox |  |  |
| 108 | TextBlock | Senha |  |
| 111 | TextBlock | Repita a senha |  |
| 117 | TextBlock | Nova senha |  |
| 120 | TextBlock | Repita a nova senha |  |
| 129 | TextBlock |  |  |
| 134 | TextBlock |  |  |
| 145 | Button | Entrar | Confirmar |
| 147 | Button | Sair | Cancelar |
| 157 | Button | Entrar com certificado (SafeID) | EntrarComCertificado |
| 161 | TextBlock | Abre a página do SafeID no navegador para você confirmar no celular. Só entra quem já tem acesso cadastrado nesta clínica. |  |

## Clinica · src/Clinica.Desktop.Shell/Shell/SetupWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/SetupWindow.xaml) · 48 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Configuração inicial — Banco de dados |  |
| 18 | TextBlock |  |  |
| 19 | TextBlock | Cole a connection string do PostgreSQL. Também aceita a URI da Neon (postgresql://…) — o sistema converte sozinho. |  |
| 21 | TextBlock | Ela fica criptografada neste computador, para o seu usuário do Windows, e vale para todos os apps da suíte instalados aqui — você só faz isso uma vez. |  |
| 24 | TextBlock | Connection string / URL da Neon |  |
| 25 | TextBox |  |  |
| 30 | Button | Testar conexão | Testar |
| 32 | Button | Salvar e continuar | Salvar |
| 38 | TextBlock |  |  |
| 41 | TextBlock |  |  |
| 44 | TextBlock |  |  |

## Clinica · src/Clinica.Desktop.Shell/Shell/ShellWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Shell/ShellWindow.xaml) · 345 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | {Binding Titulo} |  |
| 13 | Window |  |  |
| 17 | Window |  |  |
| 110 | Button | Treinamento | {Binding AbrirTreinamentoCommand} |
| 115 | TextBox | {Binding TextoPesquisa, UpdateSourceTrigger=PropertyChanged} |  |
| 118 | TextBox |  |  |
| 134 | Button |  | {Binding DataContext.NavegarResultadoCommand,
                                                              RelativeSource={RelativeSource AncestorType=Window}} |
| 139 | TextBlock | {Binding Item.Glifo} |  |
| 144 | TextBlock | {Binding Rotulo} |  |
| 149 | TextBlock | {Binding Caminho, StringFormat=' — em {0}'} |  |
| 167 | Button |  | {Binding AbrirFilaInfusaoCommand} |
| 172 | Button |  |  |
| 182 | TextBlock | Assinaturas |  |
| 184 | TextBlock | {Binding TotalAssinaturasInfusao} |  |
| 188 | Button |  | {Binding AbrirAvisosCommand} |
| 192 | TextBlock |  |  |
| 205 | TextBlock | {Binding Snackbar.NaoLidos} |  |
| 216 | TextBlock | AVISOS DESTA SESSÃO |  |
| 219 | TextBlock | Nenhum aviso por enquanto — o que o sistema disser aparece aqui. |  |
| 223 | TextBlock |  |  |
| 239 | TextBlock |  |  |
| 243 | TextBlock |  |  |
| 260 | TextBlock | {Binding HoraTexto} |  |
| 265 | TextBlock | {Binding Mensagem} |  |
| 283 | MenuItem |  |  |
| 284 | MenuItem |  |  |
| 287 | TextBlock | {Binding UsuarioRotulo} |  |
| 288 | TextBlock | ▾ |  |
| 291 | MenuItem | Minha senha | {Binding TrocarMinhaSenhaCommand} |
| 294 | Button | Trocar usuário | {Binding TrocarUsuarioCommand} |
| 319 | TextBlock |  |  |
| 322 | TextBlock |  |  |
| 339 | TextBlock | {Binding Snackbar.Mensagem} |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Abas.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Abas.xaml) · 123 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/AgendaModeloA.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/AgendaModeloA.xaml) · 85 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Botoes.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Botoes.xaml) · 254 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Campos.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Campos.xaml) · 574 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 24 | TextBlock | {Binding Converter={StaticResource RotuloEnum}} |  |
| 53 | TextBlock | {TemplateBinding ctrl:Ajudantes.Placeholder} |  |
| 167 | TextBlock |  |  |
| 171 | TextBlock | {TemplateBinding ctrl:Ajudantes.Placeholder} |  |
| 239 | TextBlock |  |  |
| 243 | TextBlock | {TemplateBinding ctrl:Ajudantes.Placeholder} |  |
| 327 | TextBlock |  |  |
| 392 | TextBlock |  |  |
| 511 | Button |  |  |
| 514 | Button |  |  |
| 518 | TextBlock |  |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Cartoes.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Cartoes.xaml) · 448 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 93 | TextBlock |  |  |
| 392 | TextBlock |  |  |
| 397 | TextBlock |  |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Feedback.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Feedback.xaml) · 375 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 94 | TextBlock | {TemplateBinding Glifo} |  |
| 97 | TextBlock | {TemplateBinding Titulo} |  |
| 100 | TextBlock | {TemplateBinding Descricao} |  |
| 104 | Button | {TemplateBinding TextoAcao} | {TemplateBinding AcaoCommand} |
| 209 | TextBlock | {TemplateBinding Titulo} |  |
| 220 | TextBlock | {TemplateBinding Quantidade} |  |
| 229 | TextBlock | — |  |
| 276 | TextBlock | {TemplateBinding TextoCarregando} |  |
| 287 | TextBlock | {TemplateBinding TextoNaoVerificado} |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Graficos.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Graficos.xaml) · 54 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 43 | TextBlock | {Binding Rotulo} |  |
| 45 | TextBlock | {Binding ValorRotulo} |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Icones.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Icones.xaml) · 72 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Midia.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Midia.xaml) · 64 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 36 | TextBlock | {Binding Iniciais, RelativeSource={RelativeSource TemplatedParent}} |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Navegacao.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Navegacao.xaml) · 130 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 73 | TextBlock | {TemplateBinding Content} |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Pacientes.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Pacientes.xaml) · 190 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 42 | TextBlock | {Binding Nome} |  |
| 45 | TextBlock | {Binding DocumentoFormatado} |  |
| 62 | TextBlock | Carteirinha vencida |  |
| 81 | TextBlock | sem convênio |  |
| 86 | TextBlock | {Binding ConvenioNome} |  |
| 135 | TextBlock | {Binding Nome} |  |
| 138 | TextBlock | {Binding DocumentoFormatado} |  |
| 144 | TextBlock | {Binding TelefoneFormatado} |  |
| 158 | TextBlock | Carteirinha vencida |  |
| 177 | TextBlock | sem convênio |  |
| 182 | TextBlock | {Binding ConvenioNome} |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Selecao.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Selecao.xaml) · 223 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Sobreposicao.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Sobreposicao.xaml) · 142 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Componentes/Tabelas.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Componentes/Tabelas.xaml) · 109 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 54 | TextBlock |  |  |

## Clinica · src/Clinica.Desktop.Shell/Styles/Suite.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Suite.xaml) · 28 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Theme.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Theme.xaml) · 126 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Styles/Tokens.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Styles/Tokens.xaml) · 197 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Desktop.Shell/Treinamento/TreinamentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop.Shell/Treinamento/TreinamentoView.xaml) · 37 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | TextBlock | Treinamento |  |
| 7 | TextBlock | Aprenda as funções do seu módulo com as telas do sistema. Continue de onde parou ou procure uma tarefa. |  |
| 9 | TextBox |  |  |
| 10 | ComboBox |  |  |
| 11 | ComboBox |  |  |
| 13 | TextBlock |  |  |
| 19 | Button | Fechar aula | Fechar |
| 19 | TextBlock |  |  |
| 20 | TextBlock |  |  |
| 22 | Button | Marcar como concluída | Concluir |
| 22 | Button | Rever desde o início | Reiniciar |
| 23 | Expander | Passo a passo e capítulos |  |
| 23 | Button | {Binding Titulo} | IrCapitulo |
| 23 | TextBlock | {Binding Texto} |  |
| 26 | TextBlock | Nenhuma aula corresponde à busca. Tente outro nome ou escolha Todas as categorias. |  |
| 29 | Button |  | Abrir |
| 30 | TextBlock | {Binding Categoria} |  |
| 30 | TextBlock | {Binding Titulo} |  |
| 30 | TextBlock | {Binding Descricao} |  |
| 30 | TextBlock | {Binding DuracaoTexto} |  |
| 30 | TextBlock | {Binding Estado} |  |

## Clinica · src/Clinica.Desktop/App.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Desktop/App.xaml) · 9 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Financeiro/App.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Financeiro/App.xaml) · 12 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Gerente/App.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Gerente/App.xaml) · 12 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/AnexosSessaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/AnexosSessaoWindow.xaml) · 124 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Anexos da sessão |  |
| 10 | Window |  |  |
| 18 | TextBlock | Anexos da sessão |  |
| 20 | TextBlock | {Binding Sessao} |  |
| 22 | TextBlock | Laudo que voltou do laboratório, imagem do exame, foto da região. O arquivo abre no programa padrão do Windows depois de salvo em disco. |  |
| 35 | TextBlock | {Binding LimiteTexto} |  |
| 41 | Button | Fechar |  |
| 43 | Button | Anexar arquivo | {Binding AnexarCommand} |
| 65 | TextBlock | {Binding Mensagem} |  |
| 86 | TextBlock | {Binding NomeArquivo} |  |
| 88 | TextBlock | {Binding Resumo} |  |
| 96 | Button | Salvar em disco | {Binding DataContext.BaixarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 101 | Button | Remover | {Binding DataContext.RemoverCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/AplicarAvaliacaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/AplicarAvaliacaoWindow.xaml) · 89 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Aplicar avaliação |  |
| 9 | Window |  |  |
| 16 | TextBlock | {Binding Titulo} |  |
| 18 | TextBlock | {Binding Janela} |  |
| 25 | TextBlock | {Binding Ressalva} |  |
| 35 | TextBlock | {Binding Parcial} |  |
| 39 | Button | Cancelar |  |
| 41 | Button | Gravar avaliação | {Binding SalvarCommand} |
| 49 | TextBlock | {Binding Mensagem} |  |
| 55 | TextBlock | Data da aplicação |  |
| 65 | TextBlock | {Binding Rotulo} |  |
| 67 | ComboBox |  |  |
| 77 | TextBlock | Observações do profissional |  |
| 78 | TextBox | {Binding Observacoes, UpdateSourceTrigger=PropertyChanged} |  |
| 83 | TextBlock | {Binding Fonte} |  |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/PrescricaoInternaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/PrescricaoInternaWindow.xaml) · 276 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Prescrição de infusão |  |
| 10 | Window |  |  |
| 23 | TextBlock | {Binding TextoOperacao} |  |
| 32 | TextBlock |  |  |
| 36 | TextBlock | {Binding Paciente} |  |
| 39 | TextBlock | Folha de execução no próprio consultório — não é receita para farmácia. Salvar deixa em rascunho; só a assinatura a envia para a sala de infusão. |  |
| 50 | TextBlock | {Binding} |  |
| 69 | Button | Acrescentar item | {Binding AcrescentarItemCommand} |
| 78 | CheckBox | Assinatura eletrônica da enfermagem ao encerrar |  |
| 86 | Button | Fechar |  |
| 89 | Button | Salvar rascunho | {Binding SalvarRascunhoCommand} |
| 94 | Button | Assinar e enviar à sala | {Binding AssinarCommand} |
| 116 | TextBlock | {Binding Mensagem} |  |
| 124 | TextBlock | Data e hora da prescrição |  |
| 127 | TextBox | {Binding HoraPrescricao, UpdateSourceTrigger=PropertyChanged} |  |
| 135 | TextBlock | Modelos de infusão |  |
| 137 | Expander | Prévia |  |
| 137 | TextBlock | {Binding PreviaModelo} |  |
| 138 | Button | Usar modelo selecionado | {Binding AplicarModeloCommand} |
| 139 | Expander | Salvar ou atualizar modelo |  |
| 141 | TextBlock | Nome do novo modelo |  |
| 142 | TextBox |  |  |
| 144 | Button | Salvar novo modelo | {Binding SalvarComoModeloCommand} |
| 145 | Button | Atualizar selecionado | {Binding AtualizarModeloCommand} |
| 151 | TextBlock | Indicação |  |
| 155 | TextBlock | Prescrição |  |
| 156 | TextBlock | Use um bloco por medicamento. O diluente e o volume total valem para toda a infusão. |  |
| 161 | TextBlock | Diluente da infusão |  |
| 162 | TextBox | {Binding DiluenteGlobal, UpdateSourceTrigger=PropertyChanged} |  |
| 165 | TextBlock | Volume total (mL) |  |
| 166 | TextBox | {Binding VolumeTotal, UpdateSourceTrigger=PropertyChanged} |  |
| 168 | CheckBox | Diluição única para todos os itens |  |
| 187 | TextBlock | Prescrição livre |  |
| 192 | TextBlock | Dose (opcional) |  |
| 193 | TextBox | {Binding Dose, UpdateSourceTrigger=PropertyChanged} |  |
| 201 | Button | Remover | {Binding DataContext.RemoverItemCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 219 | TextBlock | Diluente |  |
| 220 | TextBox | {Binding Diluente, UpdateSourceTrigger=PropertyChanged} |  |
| 224 | TextBlock | Volume |  |
| 225 | TextBox | {Binding Volume, UpdateSourceTrigger=PropertyChanged} |  |
| 229 | TextBlock | Via |  |
| 230 | ComboBox |  |  |
| 237 | TextBlock | Tempo |  |
| 238 | TextBox | {Binding TempoInfusao, UpdateSourceTrigger=PropertyChanged} |  |
| 242 | TextBlock | Previsto |  |
| 243 | TextBox | {Binding HoraPrevista, UpdateSourceTrigger=PropertyChanged} |  |
| 254 | TextBlock | Observação do item (registro anterior) |  |
| 258 | CheckBox | Se necessário (SOS) |  |
| 269 | TextBlock | Observações |  |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/ProblemaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/ProblemaWindow.xaml) · 107 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Problema do paciente |  |
| 10 | Window |  |  |
| 18 | TextBlock | {Binding Titulo} |  |
| 20 | TextBlock | A lista de problemas é do PACIENTE, não da sessão: ela acompanha a pessoa e alimenta os documentos, em vez de ser redigitada a cada papel emitido. |  |
| 26 | Button | Cancelar |  |
| 28 | Button | Gravar | {Binding SalvarCommand} |
| 35 | TextBlock | {Binding Mensagem} |  |
| 42 | TextBlock | Natureza |  |
| 43 | ComboBox |  |  |
| 45 | TextBlock | {Binding ExplicacaoNatureza} |  |
| 51 | TextBlock | Descrição |  |
| 52 | TextBox | {Binding Descricao, UpdateSourceTrigger=PropertyChanged} |  |
| 55 | TextBlock | É esta linha que o próximo profissional lê. Escreva como você diria em voz alta. |  |
| 67 | TextBlock | CID-10 (opcional) |  |
| 69 | Button | Buscar… | {Binding BuscarCidCommand} |
| 74 | TextBox | {Binding Cid, UpdateSourceTrigger=PropertyChanged} |  |
| 81 | TextBlock | {Binding DescricaoCid} |  |
| 90 | TextBlock | Desde (opcional) |  |
| 96 | TextBlock | Observações |  |
| 97 | TextBox | {Binding Observacoes, UpdateSourceTrigger=PropertyChanged} |  |
| 100 | TextBlock | A conduta da alergia (“faz broncoespasmo”), o estágio do diagnóstico, a dose da medicação contínua. |  |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/RegistrarMedidaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/RegistrarMedidaWindow.xaml) · 85 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Registrar medida |  |
| 10 | Window |  |  |
| 25 | TextBlock | Registrar medida |  |
| 26 | TextBlock | {Binding Paciente} |  |
| 31 | Button | Cancelar |  |
| 33 | Button | Registrar | {Binding RegistrarCommand} |
| 40 | TextBlock | {Binding Mensagem} |  |
| 45 | TextBlock | O que |  |
| 46 | ComboBox |  |  |
| 48 | TextBlock | {Binding AjudaDoTipo} |  |
| 59 | TextBlock | Data |  |
| 64 | TextBlock | Valor |  |
| 65 | TextBox | {Binding ValorNovo, UpdateSourceTrigger=PropertyChanged} |  |
| 73 | TextBlock | {Binding RotuloSegundoValor} |  |
| 74 | TextBox | {Binding ValorSecundarioNovo, UpdateSourceTrigger=PropertyChanged} |  |
| 78 | TextBlock | Observação (opcional) |  |
| 79 | TextBox | {Binding ObservacoesNovas, UpdateSourceTrigger=PropertyChanged} |  |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/RegistrarResultadoExameWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/RegistrarResultadoExameWindow.xaml) · 136 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Registrar resultado de exame |  |
| 10 | Window |  |  |
| 20 | TextBlock | Registrar resultado de exame |  |
| 21 | TextBlock | {Binding Paciente} |  |
| 26 | Button | Cancelar |  |
| 28 | Button | Registrar | {Binding RegistrarCommand} |
| 35 | TextBlock | {Binding Mensagem} |  |
| 40 | TextBlock | Exame |  |
| 41 | TextBox | {Binding NomeNovo, UpdateSourceTrigger=PropertyChanged} |  |
| 47 | TextBlock | Responde ao pedido (opcional) |  |
| 48 | ComboBox |  |  |
| 51 | TextBlock | {Binding AvisoPedidos} |  |
| 66 | TextBlock | Data do exame |  |
| 71 | TextBlock | Resultado |  |
| 74 | TextBox | {Binding ValorNovo, UpdateSourceTrigger=PropertyChanged} |  |
| 78 | TextBlock | Unidade (opcional) |  |
| 79 | TextBox | {Binding UnidadeNova, UpdateSourceTrigger=PropertyChanged} |  |
| 90 | TextBlock | Referência do laudo (opcional) |  |
| 91 | TextBox | {Binding ReferenciaNova, UpdateSourceTrigger=PropertyChanged} |  |
| 95 | TextBlock | Laboratório (opcional) |  |
| 96 | TextBox | {Binding LaboratorioNovo, UpdateSourceTrigger=PropertyChanged} |  |
| 99 | TextBlock | Copie a faixa de referência DO LAUDO — cada laboratório tem a sua, e o sistema não inventa uma. |  |
| 105 | TextBlock | Laudo em arquivo (opcional) |  |
| 113 | TextBlock | {Binding LaudoEscolhido, TargetNullValue=Nenhum arquivo escolhido} |  |
| 116 | Button | Escolher arquivo… | {Binding EscolherLaudoCommand} |
| 119 | Button | Tirar | {Binding RemoverLaudoCommand} |
| 125 | TextBlock | PDF ou foto do laudo, até 10 MB. O arquivo fica no prontuário do paciente e não se apaga — o registro se cancela com motivo. |  |
| 129 | TextBlock | Observação (opcional) |  |
| 130 | TextBox | {Binding ObservacoesNovas, UpdateSourceTrigger=PropertyChanged} |  |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/ResultadosDoPedidoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/ResultadosDoPedidoWindow.xaml) · 122 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Resultado do exame |  |
| 9 | Window |  |  |
| 18 | TextBlock |  |  |
| 27 | TextBlock | Paciente |  |
| 28 | TextBlock | {Binding Paciente} |  |
| 31 | TextBlock | Pedido |  |
| 32 | TextBlock | {Binding PedidoTexto} |  |
| 40 | Button | Abrir exames do paciente | {Binding AbrirNoPacienteCommand} |
| 43 | Button | Fechar |  |
| 60 | TextBlock | {Binding Mensagem} |  |
| 73 | TextBlock |  |  |
| 80 | TextBlock |  |  |
| 85 | TextBlock |  |  |
| 91 | TextBlock | {Binding Observacoes} |  |
| 97 | Button | Abrir laudo | {Binding DataContext.AbrirLaudoCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Janelas/ResumoProntuarioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Janelas/ResumoProntuarioWindow.xaml) · 212 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Prontuário |  |
| 9 | Window |  |  |
| 24 | TextBlock | {Binding Paciente} |  |
| 26 | TextBlock | {Binding LinhaIdentificacao} |  |
| 42 | TextBlock | Diagnósticos recentes |  |
| 43 | TextBlock | {Binding DiagnosticosTexto, TargetNullValue=—} |  |
| 47 | TextBlock | Plano terapêutico |  |
| 48 | TextBlock | {Binding PlanoTerapeutico, TargetNullValue=—} |  |
| 52 | TextBlock | Alergias |  |
| 55 | TextBlock | {Binding AlergiasTexto, TargetNullValue=Nenhuma registrada} |  |
| 65 | Button | Abrir prontuário completo | {Binding AbrirCompletoCommand} |
| 67 | Button | 2ª via da anamnese | {Binding SegundaViaCommand} |
| 73 | Button | Fechar |  |
| 90 | TextBlock | {Binding Mensagem} |  |
| 96 | TabItem | Evoluções |  |
| 99 | TextBlock | Nenhuma sessão registrada para este paciente. |  |
| 119 | TextBlock |  |  |
| 126 | TextBlock | {Binding Texto} |  |
| 133 | Button | Abrir sessão | {Binding DataContext.AbrirSessaoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 148 | TabItem | Anamnese |  |
| 151 | TextBlock | A anamnese deste paciente ainda não foi escrita. |  |
| 160 | TextBlock | {Binding Rotulo} |  |
| 161 | TextBlock | {Binding Texto} |  |
| 170 | TabItem | Anexos |  |
| 173 | TextBlock | Nenhum anexo no prontuário deste paciente. |  |
| 184 | TextBlock | {Binding NomeArquivo} |  |
| 187 | TextBlock | {Binding Contexto} |  |
| 196 | TextBlock | Para baixar ou anexar arquivos, abra o prontuário completo — seção Exames e anexos. |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/AcompanhamentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AcompanhamentoView.xaml) · 32 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 22 | TabItem | Evolução da dor |  |
| 25 | TabItem | Medidas |  |
| 28 | TabItem | Avaliações |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/AnamneseView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AnamneseView.xaml) · 202 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 18 | Button | Cancelar | {Binding CancelarEdicaoCommand} |
| 23 | Button | Gravar | {Binding SalvarCommand} |
| 28 | Button | {Binding RotuloDoBotao} | {Binding EditarCommand} |
| 31 | Button |  |  |
| 45 | TextBlock | Anamnese |  |
| 46 | TextBlock | O que se pergunta uma vez e se revisa: antecedentes, história familiar e hábitos. A história da doença atual e o exame físico do dia ficam na sessão, em Atendimento. |  |
| 48 | TextBlock | {Binding Procedencia} |  |
| 70 | TextBlock | {Binding Mensagem} |  |
| 113 | TextBlock | {Binding RotuloDaSecao} |  |
| 115 | TextBlock | {Binding DicaDaSecao} |  |
| 119 | TextBox | {Binding TextoDaSecao, UpdateSourceTrigger=PropertyChanged} |  |
| 160 | TextBlock | O que ela já disse |  |
| 162 | TextBlock | Cada revisão guarda o que a anamnese dizia antes. É o que torna a correção rastreável — corrigir “nega tabagismo” não apaga o fato de a pessoa ter negado. |  |
| 172 | TextBlock | {Binding Titulo} |  |
| 174 | TextBlock | {Binding Quando} |  |
| 176 | TextBlock | {Binding Motivo} |  |
| 179 | TextBlock | {Binding Texto} |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/AnexosPacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AnexosPacienteView.xaml) · 330 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 17 | TextBlock | Exames e anexos |  |
| 18 | TextBlock | Consulte exames, anexe laudos e guarde os arquivos deste paciente. |  |
| 20 | TextBlock | {Binding Resumo} |  |
| 28 | Button | Registrar resultado… | {Binding RegistrarResultadoCommand} |
| 32 | Button | Anexar arquivo à ficha… | {Binding AnexarArquivoDaFichaCommand} |
| 53 | TextBlock | {Binding Mensagem} |  |
| 111 | TextBlock | {Binding ExameRotulo} |  |
| 113 | TextBlock |  |  |
| 123 | Button | Anexar laudo… | {Binding DataContext.AnexarLaudoCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 149 | TextBlock | RESULTADOS DE EXAME |  |
| 163 | TextBlock | {Binding Nome} |  |
| 165 | TextBlock | — |  |
| 166 | TextBlock | {Binding Valor} |  |
| 171 | TextBlock | {Binding Data} |  |
| 172 | TextBlock | · |  |
| 174 | TextBlock | {Binding Contexto} |  |
| 178 | TextBlock | {Binding Observacoes} |  |
| 188 | Button | Abrir laudo | {Binding DataContext.AbrirLaudoCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 197 | Button | Cancelar… | {Binding DataContext.CancelarResultadoCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 223 | TextBlock | ARQUIVOS DA FICHA |  |
| 237 | TextBlock | {Binding Titulo} |  |
| 239 | TextBlock | {Binding Contexto} |  |
| 241 | TextBlock | {Binding Observacoes} |  |
| 248 | Button | Abrir | {Binding DataContext.AbrirArquivoDaFichaCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 255 | Button | Cancelar… | {Binding DataContext.CancelarArquivoDaFichaCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 292 | TextBlock | {Binding NomeArquivo} |  |
| 295 | TextBlock | {Binding Contexto} |  |
| 298 | TextBlock | {Binding Descricao} |  |
| 306 | Button | Salvar… | {Binding DataContext.BaixarCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/AtendimentoEnfermagemView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AtendimentoEnfermagemView.xaml) · 376 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 43 | UserControl |  |  |
| 51 | TextBlock | {Binding Passagem.AvisoModalidadeEnfermagem} |  |
| 54 | Button | Registrar infusão | {Binding RegistrarInfusaoCommand} |
| 57 | Button | Consultar ficha | {Binding DataContext.ConsultarFichaCommand, RelativeSource={RelativeSource AncestorType={x:Type views:PacienteWorkspaceView}}} |
| 60 | Button | Exames e anexos | {Binding DataContext.ConsultarExamesCommand, RelativeSource={RelativeSource AncestorType={x:Type views:PacienteWorkspaceView}}} |
| 76 | Button | {Binding FolhaDeHoje} | {Binding AbrirFolhaCommand} |
| 85 | Button | {Binding TermoPendente} | {Binding ColherTermoCommand} |
| 129 | TextBlock | {Binding AvisoDeLeitura} |  |
| 144 | TextBlock | Não foi possível conferir o termo do dia e a folha de infusão deste paciente. Confira na Sala de infusão antes de administrar qualquer coisa. |  |
| 168 | TextBlock | PLANO DE CUIDADOS DE HOJE |  |
| 170 | TextBlock | {Binding Resumo} |  |
| 171 | TextBlock |  |  |
| 186 | TextBlock | Hora |  |
| 188 | TextBox | {Binding Hora, UpdateSourceTrigger=PropertyChanged} |  |
| 209 | TextBlock | {Binding Redacao} |  |
| 216 | TextBlock | {Binding Selo} |  |
| 219 | TextBlock | {Binding Registro} |  |
| 228 | Button | Feito | {Binding DataContext.MarcarFeitoCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 235 | Button | Não feito | {Binding DataContext.MarcarNaoFeitoCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 264 | TextBlock | A hora é a do FATO observado, não a de agora — o relógio do sistema fica gravado ao lado. |  |
| 280 | TextBlock | {Binding Mensagem} |  |
| 285 | Button | Cancelar correção | {Binding Passagem.CancelarCorrecaoCommand} |
| 294 | Button | Imprimir a passagem | {Binding ImprimirFichaCommand} |
| 301 | Button | {Binding Passagem.RotuloDoBotao} | {Binding Passagem.RegistrarCommand} |
| 323 | TabItem | A passagem de hoje |  |
| 346 | TabItem | Passagens do paciente |  |
| 364 | TabItem | Conduta médica e infusões |  |
| 367 | TextBlock | O que o médico registrou e o que foi prescrito para a sala. Escolha a seção nos botões abaixo. |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/AtendimentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AtendimentoView.xaml) · 433 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 11 | UserControl |  |  |
| 38 | TextBlock | {Binding Texto} |  |
| 40 | TextBlock |  |  |
| 70 | TextBlock | {Binding DicaRodape} |  |
| 86 | TextBlock | {Binding Mensagem} |  |
| 94 | Button | Imprimir a sessão | {Binding ImprimirFichaCommand} |
| 99 | Button | Salvar sessão | {Binding SalvarCommand} |
| 113 | Button | {Binding RotuloBsv} | {Binding IndicarBsvCommand} |
| 117 | Button | Consultar ficha | {Binding DataContext.ConsultarFichaCommand, RelativeSource={RelativeSource AncestorType={x:Type views:PacienteWorkspaceView}}} |
| 120 | Button | Exames e anexos | {Binding DataContext.ConsultarExamesCommand, RelativeSource={RelativeSource AncestorType={x:Type views:PacienteWorkspaceView}}} |
| 123 | Button | Receitas, documentos e infusão | {Binding DataContext.EmitirDocumentosCommand, RelativeSource={RelativeSource AncestorType={x:Type views:PacienteWorkspaceView}}} |
| 184 | TextBlock | Há registros deste dia sem vínculo. Confira o histórico e selecione o registro desta sessão. |  |
| 186 | ComboBox |  |  |
| 188 | Button | Vincular registro existente | {Binding VincularRegistroCommand} |
| 221 | TabItem | A sessão de hoje |  |
| 277 | Button | Colher termo… | {Binding ColherTermoCommand} |
| 280 | Button |  |  |
| 314 | TabItem | Sessões anteriores |  |
| 317 | TextBlock | As últimas sessões deste paciente. O prontuário inteiro — com busca no texto, os anexos e as correções — está na seção Histórico. |  |
| 343 | TextBlock | {Binding Data} |  |
| 346 | TextBlock | {Binding Eva} |  |
| 358 | TextBlock | {Binding Retorno} |  |
| 376 | TextBlock | {Binding Rotulo} |  |
| 381 | TextBlock | {Binding Valor} |  |
| 418 | TabItem | Enfermagem e infusões |  |
| 421 | TextBlock | O que a sala registrou neste paciente: as aferições da enfermagem e as folhas de infusão. |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/AvaliacoesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/AvaliacoesView.xaml) · 197 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 19 | Button | Aplicar | {Binding AplicarCommand} |
| 48 | TextBlock | {Binding Mensagem} |  |
| 60 | CheckBox | Todas as escalas |  |
| 63 | TextBlock | {Binding Instrumento.Descricao} |  |
| 73 | TextBlock | Evolução do escore |  |
| 81 | TextBlock | PRIMEIRA |  |
| 82 | TextBlock | {Binding EscalaPrimeira} |  |
| 86 | TextBlock | ATUAL |  |
| 87 | TextBlock | {Binding EscalaAtual} |  |
| 91 | TextBlock | VARIAÇÃO |  |
| 92 | TextBlock | {Binding EscalaGanho} |  |
| 96 | TextBlock | FAIXA ATUAL |  |
| 97 | TextBlock | {Binding EscalaFaixa} |  |
| 102 | TextBlock | {Binding LeituraCurva} |  |
| 135 | TextBlock | {Binding Data} |  |
| 140 | TextBlock | {Binding Instrumento} |  |
| 142 | TextBlock | {Binding Observacoes} |  |
| 151 | TextBlock | {Binding Alerta} |  |
| 156 | TextBlock | {Binding Pontuacao} |  |
| 169 | TextBlock | {Binding Faixa} |  |
| 173 | Button | Cancelar… | {Binding DataContext.CancelarAplicacaoCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/DocumentosPacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/DocumentosPacienteView.xaml) · 17 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | Button | Atualizar documentos | {Binding AtualizarDocumentosCommand} |
| 9 | TextBlock | Emita e consulte os documentos deste paciente. |  |
| 13 | TabItem | Receitas e documentos |  |
| 14 | TabItem | Infusões |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/EmissoesNoAtendimentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/EmissoesNoAtendimentoView.xaml) · 177 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 16 | TextBlock | ENTREGAR AGORA |  |
| 18 | Button | Prescrever infusão | {Binding PrescreverInfusaoCommand} |
| 31 | TextBlock | JÁ SAIU HOJE |  |
| 50 | TextBlock | {Binding Rotulo} |  |
| 52 | TextBlock |  |  |
| 64 | Button | 2ª via | {Binding DataContext.SegundaViaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 69 | Button |  |  |
| 90 | TextBlock | Todos os papéis dele estão na seção Prescrições e documentos. |  |
| 134 | TextBlock | {Binding Rotulo} |  |
| 136 | TextBlock | {Binding Heranca} |  |
| 138 | TextBlock |  |  |
| 154 | TextBlock | {Binding Pendencia} |  |
| 162 | Button | Emitir | {Binding DataContext.EntregarFolhaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/EvolucaoDorView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/EvolucaoDorView.xaml) · 192 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 17 | Button | Exportar CSV | {Binding ExportarCommand} |
| 19 | Button | Atualizar | {Binding CarregarCommand} |
| 25 | TextBlock | {Binding Mensagem} |  |
| 32 | TextBlock | {Binding Tendencia} |  |
| 55 | TextBlock | DOR AO CHEGAR |  |
| 56 | TextBlock | {Binding DorInicial} |  |
| 58 | TextBlock | na primeira sessão medida |  |
| 65 | TextBlock | DOR AO SAIR |  |
| 66 | TextBlock | {Binding DorAtual} |  |
| 71 | TextBlock | {Binding GanhoAcumulado} |  |
| 78 | TextBlock | ALÍVIO MÉDIO |  |
| 79 | TextBlock | {Binding AlivioMedio} |  |
| 81 | TextBlock | por sessão com o par medido |  |
| 90 | TextBlock | {Binding Tendencia} |  |
| 92 | TextBlock |  |  |
| 110 | TextBlock | dor ao chegar |  |
| 114 | TextBlock | dor ao sair |  |
| 148 | TextBlock | {Binding Data} |  |
| 151 | TextBlock |  |  |
| 173 | TextBlock | {Binding Variacao} |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/ExamesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/ExamesView.xaml) · 209 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 22 | TextBlock | Exames |  |
| 23 | TextBlock | Pedidos e resultados dos seus pacientes — a situação segue os resultados REGISTRADOS no sistema. |  |
| 25 | TextBlock | {Binding MotivoDaLista} |  |
| 32 | Button |  | {Binding NovoPedidoCommand} |
| 37 | TextBlock |  |  |
| 39 | TextBlock | Novo pedido de exame |  |
| 58 | TextBlock | {Binding Mensagem} |  |
| 61 | TextBlock | {Binding Resumo} |  |
| 83 | TextBlock | Paciente |  |
| 84 | TextBlock | Exame |  |
| 85 | TextBlock | Pedido em |  |
| 86 | TextBlock | Situação |  |
| 111 | TextBlock | {Binding Paciente} |  |
| 119 | TextBlock | {Binding Profissional} |  |
| 127 | TextBlock | {Binding ExameRotulo} |  |
| 133 | TextBlock | {Binding Data, StringFormat=dd/MM/yyyy} |  |
| 145 | TextBlock | {Binding SituacaoRotulo} |  |
| 152 | TextBlock | {Binding SituacaoRotulo} |  |
| 159 | TextBlock | {Binding SituacaoRotulo} |  |
| 166 | Button | Anexar laudo | {Binding DataContext.RegistrarResultadoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 176 | Button | Ver resultados | {Binding DataContext.VerResultadosCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 183 | Button | Detalhes | {Binding DataContext.AbrirCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/MedidasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MedidasView.xaml) · 286 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 22 | Button | Prontuário | {Binding VerProntuarioCommand} |
| 26 | Button | Registrar medida | {Binding RegistrarCommand} |
| 33 | TextBlock | {Binding Mensagem} |  |
| 72 | TextBlock | {Binding Rotulo} |  |
| 74 | TextBlock | {Binding Valor} |  |
| 76 | TextBlock | {Binding Detalhe} |  |
| 101 | TextBlock | Acompanhar |  |
| 132 | Button | Exportar CSV | {Binding ExportarCommand} |
| 150 | TextBlock | PRIMEIRA |  |
| 151 | TextBlock | {Binding Primeiro} |  |
| 155 | TextBlock | ATUAL |  |
| 156 | TextBlock | {Binding Atual} |  |
| 160 | TextBlock | VARIAÇÃO |  |
| 161 | TextBlock | {Binding Variacao} |  |
| 162 | TextBlock |  |  |
| 178 | TextBlock | {Binding LeituraSerie} |  |
| 198 | TextBlock | Colheitas |  |
| 206 | TextBlock | {Binding HistoricoSoDoConsultorio} |  |
| 224 | TextBlock | {Binding Data} |  |
| 231 | TextBlock | {Binding Valor} |  |
| 234 | TextBlock | {Binding Faixa} |  |
| 239 | TextBlock |  |  |
| 255 | TextBlock | {Binding Observacoes} |  |
| 261 | Button | Cancelar… | {Binding DataContext.CancelarMedidaCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/MeuDiaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MeuDiaView.xaml) · 350 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 9 | UserControl |  |  |
| 31 | TextBlock | Meu dia |  |
| 32 | TextBlock | {Binding Profissional} |  |
| 40 | TextBlock | {Binding MotivoDaLista} |  |
| 50 | TextBlock | {Binding AgendaFechada} |  |
| 62 | Button | < | {Binding DiaAnteriorCommand} |
| 65 | Button | > | {Binding DiaSeguinteCommand} |
| 67 | Button | Hoje | {Binding HojeCommand} |
| 69 | Button | Atualizar | {Binding CarregarCommand} |
| 78 | Button | {Binding RotuloPendentes} | {Binding AbrirPendentesCommand} |
| 84 | Button |  |  |
| 120 | TextBlock | {Binding Mensagem} |  |
| 157 | DataGrid |  |  |
| 163 | DataGrid |  |  |
| 186 | DataGrid |  |  |
| 191 | TextBlock | {Binding Hora} |  |
| 212 | TextBlock | Encaixe |  |
| 214 | TextBlock | {Binding Paciente} |  |
| 217 | TextBlock | {Binding Contexto} |  |
| 221 | TextBlock | {Binding Observacoes} |  |
| 246 | TextBlock | {Binding Status} |  |
| 249 | TextBlock | {Binding StatusDetalhe} |  |
| 300 | TextBlock | {Binding Prontuario} |  |
| 330 | Button | {Binding RotuloRegistro} | {Binding DataContext.AtenderCommand,
                                                          RelativeSource={RelativeSource AncestorType=UserControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/MeusNumerosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MeusNumerosView.xaml) · 215 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 41 | TextBlock | Meus números |  |
| 42 | TextBlock |  |  |
| 47 | TextBlock | {Binding MotivoDaLista} |  |
| 53 | TextBlock | Então estes são os números da clínica inteira — não os seus. |  |
| 63 | ComboBox |  |  |
| 66 | Button | Atualizar | {Binding CarregarCommand} |
| 73 | TextBlock | {Binding Mensagem} |  |
| 84 | TextBlock | ATENDIMENTO |  |
| 87 | TextBlock | {Binding Atendidos} |  |
| 91 | TextBlock | sessões atendidas |  |
| 93 | TextBlock |  |  |
| 99 | TextBlock | {Binding Pacientes} |  |
| 100 | TextBlock | pacientes distintos |  |
| 102 | TextBlock |  |  |
| 108 | TextBlock | {Binding Faltas} |  |
| 109 | TextBlock | faltas |  |
| 111 | TextBlock |  |  |
| 117 | TextBlock | {Binding NoShow} |  |
| 118 | TextBlock | taxa de falta |  |
| 123 | TextBlock |  |  |
| 129 | TextBlock | {Binding Ocupacao} |  |
| 130 | TextBlock | ocupação da agenda |  |
| 135 | TextBlock |  |  |
| 145 | TextBlock | PRONTUÁRIO |  |
| 148 | TextBlock | {Binding Completude} |  |
| 149 | TextBlock | completude do prontuário |  |
| 159 | TextBlock |  |  |
| 165 | TextBlock | {Binding Evolucoes} |  |
| 166 | TextBlock | evoluções escritas |  |
| 168 | TextBlock |  |  |
| 174 | TextBlock | {Binding MelhoraDor} |  |
| 175 | TextBlock | melhora média da dor (EVA) |  |
| 177 | TextBlock |  |  |
| 184 | TextBlock | {Binding LeituraCompletude} |  |
| 190 | TextBlock | {Binding DividaProntuario} |  |
| 193 | Button | Ver e escrever | {Binding AbrirPendentesCommand} |
| 199 | TextBlock | Estes números são só os seus: a tela não compara profissionais. Métrica sem base de cálculo aparece como — , nunca como zero. |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/MinhaSemanaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/MinhaSemanaView.xaml) · 270 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 10 | UserControl |  |  |
| 22 | Button |  | {Binding DataContext.AbrirCommand,
                              RelativeSource={RelativeSource AncestorType=UserControl}} |
| 26 | Button |  |  |
| 27 | TextBlock |  |  |
| 47 | TextBlock | {Binding PacienteNome} |  |
| 50 | TextBlock | {Binding Modalidade} |  |
| 58 | TextBlock | Sem evolução |  |
| 63 | TextBlock | Não aconteceu |  |
| 93 | TextBlock | {Binding Bloqueio} |  |
| 113 | TextBlock | {Binding Rotulo} |  |
| 150 | TextBlock |  |  |
| 151 | TextBlock |  |  |
| 169 | TextBlock | {Binding Resumo} |  |
| 186 | TextBlock | Minha semana |  |
| 187 | TextBlock |  |  |
| 192 | TextBlock | {Binding Resumo} |  |
| 195 | TextBlock | {Binding MotivoDaLista} |  |
| 205 | Button | < | {Binding SemanaAnteriorCommand} |
| 208 | Button | > | {Binding ProximaSemanaCommand} |
| 210 | Button | Esta semana | {Binding SemanaAtualCommand} |
| 212 | Button | Atualizar | {Binding CarregarCommand} |
| 233 | TextBlock | {Binding Mensagem} |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/PacienteCapaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteCapaView.xaml) · 131 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | UserControl |  |  |
| 12 | TextBlock | Carregando ficha… |  |
| 14 | TextBlock | {Binding Mensagem} |  |
| 20 | TextBlock | Nascimento |  |
| 21 | TextBlock | {Binding Nascimento} |  |
| 24 | TextBlock | Sexo |  |
| 25 | TextBlock | {Binding Sexo} |  |
| 28 | TextBlock | Documento |  |
| 29 | TextBlock | {Binding Documento} |  |
| 32 | TextBlock | Telefone |  |
| 33 | TextBlock | {Binding Telefone} |  |
| 36 | TextBlock | E-mail |  |
| 37 | TextBlock | {Binding Email} |  |
| 40 | TextBlock | Convênio |  |
| 41 | TextBlock | {Binding Convenio} |  |
| 44 | TextBlock | Carteirinha |  |
| 45 | TextBlock | {Binding Carteirinha} |  |
| 48 | TextBlock | Validade da carteirinha |  |
| 49 | TextBlock | {Binding ValidadeCarteirinha} |  |
| 52 | TextBlock | Tratamento |  |
| 53 | TextBlock | {Binding EmTratamento} |  |
| 56 | TextBlock | Modalidade habitual |  |
| 57 | TextBlock | {Binding Modalidade} |  |
| 60 | TextBlock | Endereço |  |
| 61 | TextBlock | {Binding Endereco} |  |
| 62 | Expander | Observações do cadastro |  |
| 63 | TextBlock | {Binding ObservacoesCadastro} |  |
| 69 | Button | Adicionar registro | {Binding NovoProblemaCommand} |
| 71 | TextBlock | Alergias e problemas |  |
| 73 | CheckBox | Mostrar também os resolvidos e descartados |  |
| 75 | TextBlock | {Binding ResumoProblemas} |  |
| 82 | TextBlock | {Binding Rotulo} |  |
| 83 | TextBlock |  |  |
| 93 | TextBlock |  |  |
| 98 | TextBlock | {Binding Observacoes} |  |
| 100 | Button | Editar | {Binding DataContext.EditarProblemaCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 102 | Button | Resolvido | {Binding DataContext.ResolverProblemaCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 105 | Button | Reabrir | {Binding DataContext.ReabrirProblemaCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 107 | Button |  |  |
| 111 | Button | Descartar | {Binding DataContext.DescartarProblemaCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 113 | Button |  |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/PacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteView.xaml) · 28 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 5 | UserControl |  |  |
| 10 | Button | WhatsApp | {Binding Administrativo.WhatsAppCommand} |
| 11 | Button | Editar cadastro | {Binding Administrativo.EditarCommand} |
| 13 | Button | Atualizar ficha | {Binding AtualizarFichaCommand} |
| 16 | TextBlock | {Binding Capa.AtualizacaoFicha} |  |
| 19 | TabItem | Dados e alertas |  |
| 20 | TabItem | Sessões e guias |  |
| 20 | TabItem | Sessões e guias |  |
| 20 | TabItem | Autorizações e validade do convênio |  |
| 21 | TabItem | Anamnese |  |
| 22 | TabItem | Agenda e pendências |  |
| 23 | TabItem | Relacionamento |  |
| 24 | TabItem | Privacidade e consentimentos |  |
| 25 | TabItem | Termos do paciente |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/PacienteWorkspaceView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PacienteWorkspaceView.xaml) · 545 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 50 | UserControl |  |  |
| 171 | Button |  | {Binding VoltarCommand} |
| 177 | TextBlock |  |  |
| 180 | TextBlock | {Binding RotuloVoltar} |  |
| 202 | Button | Iniciar atendimento | {Binding IniciarSessaoCommand} |
| 213 | Button | Reabrir atendimento | {Binding ReabrirSessaoCommand} |
| 220 | Button | Concluir sessão | {Binding FinalizarSessaoCommand} |
| 224 | Button | Registrar materiais | {Binding RegistrarMateriaisCommand} |
| 227 | Button | Trocar paciente | {Binding TrocarPacienteCommand} |
| 279 | TextBlock | {Binding SituacaoSessao} |  |
| 283 | TextBlock |  |  |
| 294 | TextBlock | {Binding Cronometro} |  |
| 315 | TextBlock | {Binding Paciente} |  |
| 322 | TextBlock | {Binding Cabecalho.Linha} |  |
| 331 | TextBlock |  |  |
| 335 | TextBlock |  |  |
| 360 | TextBlock | {Binding Cabecalho.AlergiasTexto} |  |
| 362 | TextBlock |  |  |
| 372 | TextBlock |  |  |
| 411 | Button |  | {Binding Atendimento.ColherTermoCommand} |
| 427 | Button |  |  |
| 438 | TextBlock |  |  |
| 443 | TextBlock | {Binding Atendimento.TermoPendenteRotulo} |  |
| 467 | TextBlock | {Binding Rotulo} |  |
| 481 | TextBlock | {Binding AvisoConclusaoAutomatica} |  |
| 496 | TextBlock | {Binding MensagemSessao} |  |
| 516 | TabItem | Atendimento |  |
| 519 | TabItem | Atendimento de enfermagem |  |
| 524 | TabItem | Ficha do paciente |  |
| 527 | TabItem | Histórico |  |
| 530 | TabItem | Exames e anexos |  |
| 533 | TabItem | Prescrições e documentos |  |
| 538 | TabItem | Acompanhamento |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/PrescricaoInfusaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PrescricaoInfusaoView.xaml) · 204 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 39 | TextBlock | Prescrição de infusão |  |
| 40 | TextBlock | A folha de vários itens que a equipe executa aqui dentro, com checagem de enfermagem. Não é receita para farmácia. |  |
| 43 | TextBlock | {Binding Paciente} |  |
| 53 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged, Delay=200} |  |
| 75 | Button | Nova prescrição | {Binding NovaCommand} |
| 96 | TextBlock | {Binding Mensagem} |  |
| 117 | TextBlock |  |  |
| 123 | TextBlock |  |  |
| 132 | TextBlock | {Binding Execucao} |  |
| 141 | TextBlock | Cancelada |  |
| 150 | Button | Editar | {Binding DataContext.EditarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 156 | Button | Abrir | {Binding DataContext.AbrirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 161 | Button | Imprimir | {Binding DataContext.ImprimirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 166 | Button | Execução | {Binding DataContext.ImprimirExecucaoCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 172 | Button | Cancelar | {Binding DataContext.CancelarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/PrescricoesClinicasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/PrescricoesClinicasView.xaml) · 318 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 9 | UserControl |  |  |
| 68 | TextBlock | Prescrições |  |
| 69 | TextBlock | Receita, atestado, comparecimento e pedido de exame — emitidos por quem atende. |  |
| 71 | TextBlock | {Binding Paciente} |  |
| 86 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged, Delay=200} |  |
| 122 | Button | Prescrição de infusão | {Binding IrParaInfusaoCommand} |
| 142 | Button | {Binding Rotulo} | {Binding DataContext.EmitirCommand,
                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 169 | TextBlock | {Binding Mensagem} |  |
| 190 | TextBlock |  |  |
| 196 | TextBlock |  |  |
| 208 | TextBlock | {Binding Sessao} |  |
| 221 | TextBlock | {Binding Situacao} |  |
| 230 | TextBlock | {Binding Situacao} |  |
| 237 | TextBlock | {Binding Link} |  |
| 259 | Button | Assinar | {Binding DataContext.AssinarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 271 | Button | 2ª via | {Binding DataContext.ImprimirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 275 | Button |  |  |
| 286 | Button | ⋯ | AoAbrirMenuDoDocumento |

## Clinica · src/Clinica.Modulo.Clinico/Views/ProntuarioClinicoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/ProntuarioClinicoView.xaml) · 141 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 13 | Button | Atualizar histórico | {Binding CarregarCommand} |
| 18 | TextBlock | {Binding Mensagem} |  |
| 25 | TextBox | {Binding TermoSessao, UpdateSourceTrigger=PropertyChanged} |  |
| 28 | TextBlock | {Binding ResumoSessoes} |  |
| 34 | TabItem | Evoluções médicas |  |
| 49 | TextBlock |  |  |
| 58 | TextBlock |  |  |
| 64 | TextBlock |  |  |
| 70 | TextBlock |  |  |
| 76 | TextBlock |  |  |
| 86 | Button | Abrir sessão | {Binding DataContext.AbrirSessaoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 93 | Button | {Binding AnexosTexto} | {Binding DataContext.VerAnexosCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 97 | Button |  |  |
| 110 | Button | {Binding CorrecoesTexto} | {Binding DataContext.VerCorrecoesCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 134 | TabItem | Enfermagem e infusões |  |

## Clinica · src/Clinica.Modulo.Clinico/Views/ProntuariosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/ProntuariosView.xaml) · 235 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 18 | TextBlock | Prontuários |  |
| 19 | TextBlock | Evoluções e anamneses dos seus atendimentos — e as sessões que ainda estão sem registro. |  |
| 21 | TextBlock | {Binding MotivoDaLista} |  |
| 34 | Button |  | {Binding NovoProntuarioCommand} |
| 39 | TextBlock |  |  |
| 41 | TextBlock | Novo prontuário |  |
| 61 | TextBlock | {Binding Mensagem} |  |
| 69 | TextBox | {Binding Termo, UpdateSourceTrigger=PropertyChanged, Delay=200} |  |
| 72 | TextBlock | {Binding Resumo} |  |
| 96 | TextBlock | Paciente |  |
| 97 | TextBlock | Data |  |
| 98 | TextBlock | Especialidade |  |
| 99 | TextBlock | Tipo |  |
| 100 | TextBlock | Situação |  |
| 126 | TextBlock | {Binding Paciente} |  |
| 134 | TextBlock | {Binding Profissional} |  |
| 142 | TextBlock | {Binding Data, StringFormat=dd/MM/yyyy} |  |
| 146 | TextBlock | {Binding Detalhe} |  |
| 152 | TextBlock | {Binding TipoRotulo} |  |
| 163 | TextBlock | {Binding SituacaoRotulo} |  |
| 170 | TextBlock | {Binding SituacaoRotulo} |  |
| 177 | TextBlock | {Binding SituacaoRotulo} |  |
| 184 | TextBlock | {Binding SituacaoRotulo} |  |
| 191 | Button | Escrever | {Binding DataContext.EscreverCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 200 | Button | Assinar | {Binding DataContext.AssinarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 209 | Button | Abrir | {Binding DataContext.AbrirCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/RegistrosPendentesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/RegistrosPendentesView.xaml) · 142 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 18 | TextBlock | Sessões sem evolução |  |
| 19 | TextBlock | {Binding Resumo} |  |
| 22 | Button | Atualizar | {Binding CarregarCommand} |
| 27 | TextBlock | {Binding MotivoDaLista} |  |
| 34 | TextBlock | Esta é a dívida de EVOLUÇÃO da clínica inteira — o registro de quem consulta. A passagem da enfermagem é escrita em “Atendimento de enfermagem”, dentro do paciente. |  |
| 56 | TextBlock | Paciente |  |
| 57 | TextBox | {Binding FiltroPacienteRegistro, UpdateSourceTrigger=PropertyChanged} |  |
| 64 | TextBlock | Profissional |  |
| 65 | ComboBox |  |  |
| 69 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 79 | TextBlock | {Binding Mensagem} |  |
| 101 | TextBlock | {Binding Paciente} |  |
| 104 | TextBlock |  |  |
| 116 | TextBlock | {Binding Atraso} |  |
| 119 | Button | Escrever evolução | {Binding DataContext.EscreverCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/SessoesEnfermagemView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/SessoesEnfermagemView.xaml) · 58 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 16 | TextBlock | Sessões de enfermagem |  |
| 17 | TextBlock | BSV e BSV + acupuntura · Registre a evolução na sessão original, mesmo após a conclusão médica. |  |
| 20 | TextBox | {Binding Paciente, UpdateSourceTrigger=PropertyChanged} |  |
| 21 | TextBox | {Binding Medico, UpdateSourceTrigger=PropertyChanged} |  |
| 24 | ComboBox |  |  |
| 25 | Button | Filtrar / atualizar | {Binding FiltrarCommand} |
| 26 | Button | Limpar filtros | {Binding LimparCommand} |
| 31 | Button | Anterior | {Binding AnteriorCommand} |
| 32 | Button | Próxima | {Binding ProximaCommand} |
| 34 | TextBlock | {Binding Mensagem} |  |
| 37 | DataGrid |  |  |
| 40 | DataGrid |  |  |
| 42 | Button | Abrir | {Binding DataContext.AbrirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |

## Clinica · src/Clinica.Modulo.Clinico/Views/SessoesPacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Clinico/Views/SessoesPacienteView.xaml) · 46 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | TextBlock | Evolução salva, sessão concluída e guias geradas são etapas diferentes. |  |
| 10 | Button | Anterior | {Binding PaginaAnteriorCommand} |
| 12 | Button | Próxima | {Binding ProximaPaginaCommand} |
| 15 | TextBlock | {Binding ResumoSessoes} |  |
| 24 | TextBlock |  |  |
| 28 | TextBlock | {Binding ProfissionalTexto} |  |
| 30 | TextBlock | {Binding Situacao} |  |
| 31 | TextBlock | {Binding EvolucaoTexto} |  |
| 32 | TextBlock | {Binding GuiasTexto} |  |
| 33 | TextBlock | {Binding Protocolo} |  |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/AvisoPendenciasWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/AvisoPendenciasWindow.xaml) · 55 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Aviso de pendências |  |
| 9 | Window |  |  |
| 21 | TextBlock |  |  |
| 23 | TextBlock | Aviso de pendências |  |
| 27 | TextBlock |  |  |
| 39 | TextBlock |  |  |
| 50 | Button | Fechar | Fechar_Click |
| 52 | Button | Ver painel de pendências | Fechar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/BaixaGuiaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/BaixaGuiaWindow.xaml) · 42 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Dar baixa na guia |  |
| 9 | Window |  |  |
| 15 | TextBlock | Dar baixa na guia |  |
| 16 | TextBlock |  |  |
| 18 | TextBlock | Nº da guia no sistema do convênio |  |
| 19 | TextBox |  |  |
| 22 | TextBlock | Data da baixa |  |
| 25 | TextBlock | Observação (opcional) |  |
| 26 | TextBox |  |  |
| 29 | TextBlock | Baixa é o registro de que a guia foi efetivada no sistema do convênio. Ela sai das pendências e passa a contar como faturada. |  |
| 32 | TextBlock |  |  |
| 36 | Button | Cancelar | Cancelar_Click |
| 38 | Button | Registrar baixa | Confirmar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/BaixaLoteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/BaixaLoteWindow.xaml) · 57 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Baixa em lote |  |
| 9 | Window |  |  |
| 19 | TextBlock | Dar baixa em lote |  |
| 21 | TextBlock | Informe o número da guia gerada no sistema do convênio para cada linha. Linhas sem número são ignoradas. |  |
| 24 | TextBlock | Data da baixa: |  |
| 40 | TextBlock | {Binding Descricao} |  |
| 43 | TextBox | {Binding NumeroGuia, UpdateSourceTrigger=PropertyChanged} |  |
| 53 | Button | Cancelar |  |
| 54 | Button | Confirmar baixas | Confirmar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/ConvenioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/ConvenioWindow.xaml) · 230 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Convênio |  |
| 10 | Window |  |  |
| 18 | TextBlock | As alterações valem quando você clicar em “Salvar configurações”, na tela de trás. |  |
| 21 | Button | Fechar | Fechar_Click |
| 28 | TextBlock | {Binding Nome, Mode=OneWay} |  |
| 30 | TextBlock | Convênio embutido: a regra de faturamento é fixa no sistema. Aqui você pode renomeá-lo, ligar ou desligar as guias e ajustar o número da guia. |  |
| 33 | TextBlock | Convênio cadastrado pela clínica. Ele fatura pela regra da família escolhida abaixo. |  |
| 42 | TextBlock | IDENTIFICAÇÃO |  |
| 59 | TextBlock | Nome do convênio |  |
| 60 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 64 | TextBlock | Segue a regra de faturamento de |  |
| 65 | ComboBox |  |  |
| 69 | ComboBox |  |  |
| 71 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 77 | TextBlock | A regra decide quantas guias saem por atendimento, quando o 2º código libera e a cor do semáforo. Escolha “Personalizado” para configurar tudo isso à mão, aqui embaixo. |  |
| 80 | CheckBox | Convênio ativo (aparece nos cadastros e nos lançamentos) |  |
| 82 | TextBlock | Desligar não apaga nada: o histórico continua, o convênio só some das listas de escolha. |  |
| 86 | TextBlock | GUIAS DESTE CONVÊNIO |  |
| 91 | CheckBox | Gera guia para faturar no convênio |  |
| 93 | TextBlock | Desligue para cadastrar o PARTICULAR — o paciente que vem sem convênio. O atendimento continua sendo registrado e entra nos indicadores, mas nenhuma guia entra no painel de pendências nem na rodada de 10 dias. A cobrança é no caixa. |  |
| 106 | TextBlock | Como é o número da guia aqui |  |
| 109 | ComboBox |  |  |
| 112 | ComboBox |  |  |
| 114 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 120 | TextBlock | Registro ANS desta operadora |  |
| 121 | TextBox | {Binding RegistroAnsOperadora, UpdateSourceTrigger=PropertyChanged} |  |
| 125 | TextBlock | A baixa recusa o número que não seguir esta forma — é o que pega o zero digitado como “O”, que hoje só aparece no retorno da operadora, semanas depois. O registro ANS sai no cabeçalho do lote TISS desta operadora. |  |
| 129 | TextBlock | COMO O FATURAMENTO É GERADO |  |
| 135 | TextBlock | Este convênio fatura pela regra fixa da família escolhida acima — quais códigos saem, quando o 2º libera e a cor do semáforo já estão no sistema e não se editam aqui. |  |
| 137 | TextBlock | Os números dessa regra (validade da consulta, dias até o 2º código e as cores) valem para TODOS os convênios da mesma família e ficam em “Números das regras”, na tela de trás. |  |
| 143 | TextBlock | Como este convênio é personalizado, os ajustes abaixo é que definem o que o sistema gera a cada atendimento. |  |
| 146 | CheckBox | A clínica atende com eletroacupuntura neste convênio |  |
| 148 | CheckBox | Gera um 2º código alguns dias depois do atendimento |  |
| 150 | CheckBox | O 2º código só é possível se o paciente tiver o app do convênio |  |
| 152 | CheckBox | Fatura BSV |  |
| 154 | CheckBox | No BSV, as datas precisam ser invertidas no sistema da operadora |  |
| 168 | TextBlock | Como o 2º código é obtido |  |
| 169 | ComboBox |  |  |
| 171 | ComboBox |  |  |
| 173 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 179 | TextBlock | Dias até o 2º código |  |
| 180 | TextBox | {Binding DiasSegundoCodigo} |  |
| 183 | TextBlock | Validade da consulta, em dias |  |
| 184 | TextBox | {Binding ValidadeConsultaDias, TargetNullValue=''} |  |
| 188 | TextBlock | Deixe a validade da consulta em branco se este convênio não exige renovar a consulta de tempos em tempos. |  |
| 201 | TextBlock | Semáforo quando o paciente TEM app |  |
| 202 | ComboBox |  |  |
| 204 | ComboBox |  |  |
| 206 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 212 | TextBlock | Semáforo quando NÃO tem app |  |
| 213 | ComboBox |  |  |
| 215 | ComboBox |  |  |
| 217 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 223 | TextBlock | É a cor com que a pendência do paciente aparece no painel: verde faz acupuntura + eletro e terá 2º código; amarela faz só acupuntura. |  |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/EnvioLoteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/EnvioLoteWindow.xaml) · 31 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Marcar lote como enviado |  |
| 8 | Window |  |  |
| 14 | TextBlock | Marcar lote como enviado |  |
| 15 | TextBlock | Registre quando o lote foi entregue à operadora (portal/webservice) e o protocolo devolvido por ela. |  |
| 18 | TextBlock | Data do envio |  |
| 21 | TextBlock | Protocolo da operadora (opcional) |  |
| 22 | TextBox |  |  |
| 25 | Button | Cancelar | Cancelar_Click |
| 27 | Button | Marcar enviado | Confirmar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/GlosaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/GlosaWindow.xaml) · 56 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Registrar glosa |  |
| 8 | Window |  |  |
| 14 | TextBlock | Registrar glosa da guia |  |
| 15 | TextBlock |  |  |
| 17 | TextBlock | Data da glosa |  |
| 20 | TextBlock | Motivo (tabela ANS) |  |
| 21 | ComboBox |  |  |
| 29 | TextBlock |  |  |
| 31 | TextBlock | COMO EVITAR |  |
| 32 | TextBlock |  |  |
| 34 | TextBlock | LASTRO DO RECURSO |  |
| 35 | TextBlock |  |  |
| 37 | Button | Ver o guia completo | Guia_Click |
| 42 | TextBlock | Complemento / observação |  |
| 43 | TextBox |  |  |
| 45 | TextBlock |  |  |
| 49 | Button | Cancelar | Cancelar_Click |
| 51 | Button | Registrar glosa | Confirmar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/GuiaGlosasWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/GuiaGlosasWindow.xaml) · 108 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Por que as guias são glosadas |  |
| 10 | Window |  |  |
| 20 | TextBlock | Por que as guias são glosadas |  |
| 21 | TextBlock | O demonstrativo da operadora chega com um código seco. Aqui está o que cada um quer dizer, o que costuma causar, como não repetir — e o que juntar para sustentar o recurso. |  |
| 34 | TextBox |  |  |
| 44 | TextBlock | {Binding Codigo} |  |
| 47 | TextBlock | {Binding Descricao} |  |
| 62 | TextBlock |  |  |
| 65 | TextBlock |  |  |
| 69 | TextBlock |  |  |
| 72 | TextBlock | O QUE SIGNIFICA |  |
| 74 | TextBlock |  |  |
| 77 | TextBlock | COMO EVITAR |  |
| 79 | TextBlock |  |  |
| 82 | TextBlock | LASTRO DO RECURSO |  |
| 84 | TextBlock |  |  |
| 90 | TextBlock |  |  |
| 92 | TextBlock | Neste motivo o recurso costuma não prosperar. Vale mais corrigir a causa (ou tratar como particular) do que gastar o prazo recorrendo. |  |
| 102 | Button | Fechar | Fechar_Click |
| 104 | TextBlock | Códigos da tabela de tipos de glosa do padrão TISS/ANS. O código registrado aqui é o mesmo que sai no XML de recurso. |  |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/HistoricoGuiaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/HistoricoGuiaWindow.xaml) · 99 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Histórico da guia |  |
| 10 | Window |  |  |
| 20 | TextBlock | Histórico da guia |  |
| 21 | TextBlock |  |  |
| 26 | DataGrid |  |  |
| 29 | DataGrid |  |  |
| 33 | TextBlock | {Binding Quando} |  |
| 42 | TextBlock | {Binding Operador} |  |
| 52 | TextBlock | {Binding Acao} |  |
| 62 | TextBlock | {Binding Detalhe} |  |
| 85 | TextBlock |  |  |
| 91 | Button | Fechar | Fechar_Click |
| 94 | TextBlock | A trilha é somente leitura: registro de auditoria que se pode editar não é auditoria. Quem assina cada linha é quem fez login, não o usuário do Windows. |  |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/NaoConformidadeWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/NaoConformidadeWindow.xaml) · 29 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Marcar como não conformidade |  |
| 8 | Window |  |  |
| 14 | TextBlock | Marcar como não conformidade |  |
| 15 | TextBlock | A guia sai das pendências ativas do painel e vai para a aba NC (fica no relatório). Ela volta a ser pendência se for reaberta na aba NC ou quando o paciente retornar. Use quando já se sabe que a guia não será resolvida agora. |  |
| 18 | TextBlock | Justificativa (obrigatória) |  |
| 19 | TextBox |  |  |
| 23 | Button | Cancelar | Cancelar_Click |
| 25 | Button | Marcar não conformidade | Confirmar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/ObservacaoPendenciaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/ObservacaoPendenciaWindow.xaml) · 31 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Observação da pendência |  |
| 8 | Window |  |  |
| 14 | TextBlock | Observação da pendência |  |
| 15 | TextBlock | Se a guia não pôde ser baixada agora, registre o motivo (ex.: portal fora do ar, aguardando o paciente enviar o QR Code). Fica visível na pendência para consultar depois. |  |
| 18 | TextBlock | O que aconteceu? |  |
| 19 | TextBox |  |  |
| 21 | TextBlock |  |  |
| 25 | Button | Cancelar | Cancelar_Click |
| 27 | Button | Salvar observação | Confirmar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/RegrasFamiliaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RegrasFamiliaWindow.xaml) · 58 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Números das regras |  |
| 9 | Window |  |  |
| 21 | TextBlock | Números das regras de faturamento |  |
| 23 | TextBlock | Cada linha é uma FAMÍLIA de regra, não uma operadora: o que você mudar aqui vale para todos os convênios que faturam por essa família. Quais códigos saem a cada atendimento continua sendo fixo no sistema — o que se ajusta são os números. |  |
| 27 | DataGrid |  |  |
| 30 | DataGrid |  |  |
| 50 | TextBlock | As alterações valem quando você clicar em “Salvar configurações”, na tela de trás. |  |
| 53 | Button | Fechar | Fechar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RetornoLoteWindow.xaml) · 78 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Registrar retorno do lote |  |
| 10 | Window |  |  |
| 21 | TextBlock | Registrar retorno |  |
| 22 | TextBlock | Conforme o demonstrativo de análise da operadora, marque as guias GLOSADAS e informe o motivo. As demais serão consideradas aceitas. As glosas entram no controle de glosas com o prazo de recurso correndo. |  |
| 26 | DataGrid |  |  |
| 28 | DataGrid |  |  |
| 36 | ComboBox |  |  |
| 44 | TextBox | {Binding Complemento, UpdateSourceTrigger=PropertyChanged} |  |
| 66 | TextBlock | Data do retorno: |  |
| 68 | TextBlock | Observação: |  |
| 69 | TextBox |  |  |
| 73 | Button | Cancelar | Cancelar_Click |
| 75 | Button | Registrar retorno | Confirmar_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Alertas/RodadaPendenciasWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Alertas/RodadaPendenciasWindow.xaml) · 72 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Rodar pendências |  |
| 9 | Window |  |  |
| 20 | TextBlock | Rodar pendências |  |
| 22 | TextBlock |  |  |
| 26 | TextBlock | Data da baixa: |  |
| 43 | TextBlock | {Binding Descricao} |  |
| 45 | TextBlock | {Binding Situacao} |  |
| 48 | TextBox | {Binding NumeroGuia, UpdateSourceTrigger=PropertyChanged} |  |
| 51 | TextBox | {Binding Justificativa, UpdateSourceTrigger=PropertyChanged} |  |
| 63 | TextBlock | Para cada guia: informe o nº da guia (baixa) OU uma justificativa (não conformidade). |  |
| 66 | Button | Cancelar | Cancelar_Click |
| 68 | Button | Concluir rodada | Concluir_Click |

## Clinica · src/Clinica.Modulo.Faturamento/Styles/Componentes/Tabelas.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Styles/Componentes/Tabelas.xaml) · 63 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Modulo.Faturamento/Styles/Conversores.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Styles/Conversores.xaml) · 30 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Modulo.Faturamento/Views/BaixaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/BaixaView.xaml) · 67 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 5 | UserControl |  |  |
| 9 | TextBlock |  |  |
| 14 | TextBlock | Confirme a guia no sistema do convênio antes de dar baixa. |  |
| 19 | TextBlock | {Binding Codigo.Tipo, Converter={StaticResource EnumDescricao}} |  |
| 20 | TextBlock | {Binding Codigo.Descricao} |  |
| 28 | TextBlock | Por que estava pendente |  |
| 29 | TextBlock | {Binding ObservacaoPendencia} |  |
| 33 | TextBlock | Número da guia gerada (sistema do convênio) |  |
| 34 | TextBox | {Binding NumeroGuia, UpdateSourceTrigger=PropertyChanged} |  |
| 38 | TextBlock | {Binding DicaNumeroGuia} |  |
| 46 | TextBlock | {Binding CriticaNumeroGuia} |  |
| 51 | TextBlock | Data da baixa |  |
| 54 | TextBlock | Observação |  |
| 55 | TextBox | {Binding Observacao} |  |
| 58 | Button | Cancelar | {Binding CancelarCommand} |
| 60 | Button | Confirmar baixa | {Binding ConfirmarCommand} |
| 64 | TextBlock | {Binding Mensagem} |  |

## Clinica · src/Clinica.Modulo.Faturamento/Views/ConsultaGuiasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/ConsultaGuiasView.xaml) · 191 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 5 | UserControl |  |  |
| 15 | TextBlock | Consultar guias |  |
| 22 | TextBlock | Paciente (nome ou CPF) |  |
| 23 | TextBox | {Binding TermoPaciente, UpdateSourceTrigger=PropertyChanged} |  |
| 28 | TextBlock | Nº da guia |  |
| 29 | TextBox | {Binding NumeroGuia, UpdateSourceTrigger=PropertyChanged} |  |
| 32 | TextBlock | Observação da pendência |  |
| 33 | TextBox | {Binding TermoObservacao, UpdateSourceTrigger=PropertyChanged} |  |
| 38 | TextBlock | De (atendimento) |  |
| 42 | TextBlock | Até |  |
| 46 | TextBlock | Status |  |
| 47 | ComboBox |  |  |
| 50 | TextBlock | Convênio |  |
| 51 | ComboBox |  |  |
| 52 | ComboBox |  |  |
| 54 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 60 | TextBlock | Modalidade |  |
| 61 | ComboBox |  |  |
| 62 | ComboBox |  |  |
| 64 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 70 | TextBlock | Especialidade |  |
| 71 | ComboBox |  |  |
| 72 | ComboBox |  |  |
| 74 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 81 | Button |  | {Binding BuscarCommand} |
| 83 | TextBlock |  |  |
| 84 | TextBlock | Buscar |  |
| 87 | Button | Limpar | {Binding LimparCommand} |
| 91 | TextBlock | {Binding Resumo} |  |
| 113 | DataGrid |  |  |
| 114 | DataGrid |  |  |
| 141 | TextBlock | {Binding Path=., Converter={StaticResource StatusCodigo}} |  |
| 158 | TextBlock | {Binding ObservacaoPendencia} |  |
| 167 | Button | Capa | {Binding DataContext.CapaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 176 | Button | Histórico | {Binding DataContext.HistoricoCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |

## Clinica · src/Clinica.Modulo.Faturamento/Views/DashboardView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/DashboardView.xaml) · 572 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | UserControl |  |  |
| 26 | TextBlock | {Binding PacienteNome} |  |
| 27 | TextBlock | {Binding PacienteTelefone} |  |
| 32 | TextBlock | sem telefone no cadastro |  |
| 33 | TextBlock |  |  |
| 72 | TextBlock | Pendências de faturamento |  |
| 74 | Button |  | {Binding AtualizarCommand} |
| 77 | TextBlock |  |  |
| 78 | TextBlock | Atualizar |  |
| 91 | TextBlock |  |  |
| 93 | Button |  | {Binding RodarPendenciasCommand} |
| 96 | TextBlock |  |  |
| 97 | TextBlock | Rodar pendências |  |
| 100 | TextBlock | {Binding RodadaBanner} |  |
| 110 | TextBlock |  |  |
| 112 | Button | Tentar de novo | {Binding AtualizarCommand} |
| 115 | TextBlock | {Binding RodadaAvisoFalha} |  |
| 137 | TextBlock |  |  |
| 138 | TextBlock | Guias em aberto |  |
| 141 | TextBlock | {Binding TotalCodigos} |  |
| 149 | TextBlock |  |  |
| 150 | TextBlock | Urgentes (vermelho) |  |
| 153 | TextBlock | {Binding CodigosUrgentes} |  |
| 170 | TextBlock |  |  |
| 171 | TextBlock | Consultas a renovar |  |
| 174 | TextBlock | {Binding ConsultasARenovar} |  |
| 184 | TextBlock |  |  |
| 185 | TextBlock | Pendências totais |  |
| 189 | TextBlock | {Binding Total} |  |
| 210 | TextBlock |  |  |
| 217 | TextBlock |  |  |
| 227 | TextBlock |  |  |
| 238 | TextBlock |  |  |
| 244 | TextBlock | Código: |  |
| 245 | ComboBox |  |  |
| 247 | ComboBox |  |  |
| 249 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 253 | TextBlock | Modalidade: |  |
| 254 | ComboBox |  |  |
| 256 | ComboBox |  |  |
| 258 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 262 | TextBlock | Especialidade: |  |
| 263 | ComboBox |  |  |
| 265 | ComboBox |  |  |
| 267 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 271 | TextBlock | Convênio: |  |
| 272 | ComboBox |  |  |
| 274 | ComboBox |  |  |
| 276 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 283 | TextBlock | {Binding ResumoRecorte} |  |
| 286 | Button | Limpar filtros | {Binding LimparFiltrosCommand} |
| 292 | Button |  | {Binding DarBaixaEmLoteCommand} |
| 298 | TextBlock |  |  |
| 299 | TextBlock | Baixa em lote |  |
| 306 | TextBlock | Guias e códigos pendentes |  |
| 324 | DataGrid |  |  |
| 328 | DataGrid |  |  |
| 363 | TextBlock | {Binding ObservacaoPendencia} |  |
| 374 | Button | Dar baixa | {Binding DataContext.DarBaixaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 377 | Button |  | {Binding DataContext.WhatsappCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 382 | TextBlock |  |  |
| 384 | Button | {Binding TemObservacao, Converter={StaticResource ObservacaoParaRotulo}} | {Binding DataContext.AnotarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 389 | Button | NC | {Binding DataContext.NaoConformidadeCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 408 | TextBlock | Glosas — prazo de recurso correndo |  |
| 409 | DataGrid |  |  |
| 412 | DataGrid |  |  |
| 448 | Button | Abrir glosas | {Binding DataContext.AbrirGlosasCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 462 | TextBlock | Carteirinhas vencidas ou vencendo (30 dias) |  |
| 463 | DataGrid |  |  |
| 466 | DataGrid |  |  |
| 498 | Button | Abrir ficha | {Binding DataContext.AbrirFichaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 502 | Button |  | {Binding DataContext.WhatsappCarteirinhaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 507 | TextBlock |  |  |
| 520 | TextBlock | Consultas a renovar |  |
| 521 | DataGrid |  |  |
| 524 | DataGrid |  |  |
| 551 | Button | Renovar | {Binding DataContext.RenovarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 555 | Button |  | {Binding DataContext.WhatsappConsultaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 560 | TextBlock |  |  |

## Clinica · src/Clinica.Modulo.Faturamento/Views/FaturadosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/FaturadosView.xaml) · 112 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 4 | UserControl |  |  |
| 14 | TextBlock | Guias faturadas (baixadas) |  |
| 17 | TextBlock | De: |  |
| 19 | TextBlock | Até: |  |
| 21 | Button | Buscar | {Binding BuscarCommand} |
| 22 | Button |  | {Binding ExportarCsvCommand} |
| 25 | TextBlock |  |  |
| 26 | TextBlock | Exportar CSV |  |
| 31 | TextBlock | {Binding Mensagem} |  |
| 43 | TextBlock | Paciente |  |
| 44 | TextBox | {Binding FiltroPaciente, UpdateSourceTrigger=PropertyChanged} |  |
| 48 | TextBlock | Nº da guia |  |
| 49 | TextBox | {Binding FiltroGuia, UpdateSourceTrigger=PropertyChanged} |  |
| 52 | TextBlock | Convênio |  |
| 53 | ComboBox |  |  |
| 56 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 62 | TextBlock | {Binding Resumo} |  |
| 69 | DataGrid |  |  |
| 70 | DataGrid |  |  |
| 84 | Button | Capa | {Binding DataContext.CapaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 89 | Button | Guia | {Binding DataContext.GuiaPdfCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 94 | Button | Glosar | {Binding DataContext.GlosarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 99 | Button | Estornar | {Binding DataContext.EstornarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |

## Clinica · src/Clinica.Modulo.Faturamento/Views/FaturamentoHostView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/FaturamentoHostView.xaml) · 47 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 4 | UserControl |  |  |
| 9 | UserControl |  |  |

## Clinica · src/Clinica.Modulo.Faturamento/Views/GlosasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/GlosasView.xaml) · 152 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 4 | UserControl |  |  |
| 15 | TextBlock | Controle de glosas |  |
| 18 | Button |  | {Binding BuscarCommand} |
| 20 | TextBlock |  |  |
| 21 | TextBlock | Atualizar |  |
| 24 | Button |  | {Binding GerarRecursoXmlCommand} |
| 27 | TextBlock |  |  |
| 28 | TextBlock | Recurso de glosa (.xml) |  |
| 31 | Button |  | {Binding AbrirGuiaCommand} |
| 34 | TextBlock |  |  |
| 35 | TextBlock | Por que as guias são glosadas |  |
| 40 | TextBlock | {Binding Mensagem} |  |
| 52 | TextBlock | Paciente |  |
| 53 | TextBox | {Binding FiltroPaciente, UpdateSourceTrigger=PropertyChanged} |  |
| 57 | TextBlock | Nº da guia |  |
| 58 | TextBox | {Binding FiltroGuia, UpdateSourceTrigger=PropertyChanged} |  |
| 60 | CheckBox | Mostrar só glosas em aberto |  |
| 62 | CheckBox | Só prazo de recurso vencido |  |
| 65 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 71 | TextBlock | {Binding Resumo} |  |
| 81 | TextBlock | O que mais glosa nesta clínica |  |
| 93 | TextBlock | {Binding Rotulo} |  |
| 95 | TextBlock | {Binding ComoEvitar} |  |
| 97 | Button | Entenda | {Binding DataContext.AbrirGuiaCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 109 | DataGrid |  |  |
| 110 | DataGrid |  |  |
| 135 | Button | Reapresentar | {Binding DataContext.ReapresentarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 139 | Button | Recuperada | {Binding DataContext.RecuperarCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |

## Clinica · src/Clinica.Modulo.Faturamento/Views/NaoConformidadesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/NaoConformidadesView.xaml) · 113 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 5 | UserControl |  |  |
| 17 | TextBlock | Não conformidades (NC) |  |
| 18 | Button |  | {Binding AtualizarCommand} |
| 21 | TextBlock |  |  |
| 22 | TextBlock | Atualizar |  |
| 27 | TextBlock | Guias que, ao rodar as pendências, não puderam ser baixadas e foram justificadas. Elas saem da aba Pendências e ficam aqui, documentadas. Reabra quando aparecer uma solução — a guia volta a ser pendência ativa. |  |
| 36 | TextBlock | Paciente ou justificativa |  |
| 37 | TextBox | {Binding FiltroTexto, UpdateSourceTrigger=PropertyChanged} |  |
| 40 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 46 | TextBlock | {Binding Resumo} |  |
| 68 | DataGrid |  |  |
| 72 | DataGrid |  |  |
| 84 | TextBlock | {Binding Justificativa} |  |
| 94 | Button | Ver justificativa | {Binding DataContext.VerJustificativaCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 98 | Button | Reabrir | {Binding DataContext.ReabrirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |

## Clinica · src/Clinica.Modulo.Faturamento/Views/ParametrosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/ParametrosView.xaml) · 269 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 16 | TextBlock | Configurações |  |
| 25 | TabItem | Convênios |  |
| 29 | TextBlock | Convênios atendidos |  |
| 32 | Button | Números das regras… | {Binding AbrirRegrasFamiliaCommand} |
| 35 | Button | + Novo convênio | {Binding NovoConvenioCommand} |
| 40 | TextBlock | Cada linha é uma operadora. Abra uma delas para dar nome, escolher a regra de faturamento que ela segue, definir a forma do número da guia e cadastrar o particular. Lembre de clicar em “Salvar configurações” ao terminar. |  |
| 44 | DataGrid |  |  |
| 48 | DataGrid |  |  |
| 55 | CheckBox |  |  |
| 72 | Button | Abrir | {Binding DataContext.EditarConvenioCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 75 | Button | Excluir | {Binding DataContext.RemoverConvenioCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 98 | TabItem | Prazos |  |
| 101 | TabItem | Modalidades |  |
| 104 | TextBlock | Modalidades de atendimento oferecidas (o que é realizado na visita). 'Nova modalidade' cria uma variante com nome próprio que reutiliza o COMPORTAMENTO de uma modalidade embutida (ex.: 'Auriculoterapia' que fatura como 'Acupuntura (apenas)'). Desative para ocultar dos lançamentos sem apagar o histórico. |  |
| 110 | TextBlock | Modalidades de atendimento |  |
| 111 | Button | + Nova modalidade | {Binding NovaModalidadeCommand} |
| 114 | DataGrid |  |  |
| 118 | DataGrid |  |  |
| 125 | ComboBox |  |  |
| 128 | ComboBox |  |  |
| 130 | TextBlock | {Binding Converter={StaticResource EnumDescricao}} |  |
| 140 | Button | Excluir | {Binding DataContext.RemoverModalidadeCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 151 | TextBlock | As modalidades embutidas não podem ser excluídas nem trocar de comportamento — apenas renomeadas ou desativadas. Lembre de clicar em 'Salvar configurações' ao terminar. |  |
| 158 | TabItem | Especialidades |  |
| 161 | TextBlock | Especialidades das consultas avulsas (discriminam a consulta nos relatórios por especialidade). Adicione as que a clínica atende. Desative para ocultar sem apagar o histórico. |  |
| 167 | TextBlock | Especialidades de consulta |  |
| 168 | Button | + Nova especialidade | {Binding NovaEspecialidadeCommand} |
| 171 | DataGrid |  |  |
| 175 | DataGrid |  |  |
| 181 | Button | Excluir | {Binding DataContext.RemoverEspecialidadeCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 192 | TextBlock | As especialidades embutidas (usadas na rotação da Petrobras) não podem ser excluídas — apenas renomeadas ou desativadas. Lembre de clicar em 'Salvar configurações' ao terminar. |  |
| 199 | TabItem | Clínica / prestador |  |
| 202 | TabItem | Códigos TUSS |  |
| 205 | TextBlock | Código TUSS de cada procedimento, usado no lote TISS. Pode variar conforme a operadora. |  |
| 216 | TextBlock | Acupuntura |  |
| 217 | TextBox | {Binding TussAcupuntura} |  |
| 220 | TextBlock | Eletroacupuntura |  |
| 221 | TextBox | {Binding TussEletro} |  |
| 224 | TextBlock | BSV |  |
| 225 | TextBox | {Binding TussBsv} |  |
| 235 | TextBlock | Consulta |  |
| 236 | TextBox | {Binding TussConsulta} |  |
| 239 | TextBlock | Consulta especialidade |  |
| 240 | TextBox | {Binding TussEspecialidade} |  |
| 251 | Button | Salvar configurações | {Binding SalvarCommand} |
| 254 | TextBlock | {Binding Mensagem} |  |
| 256 | TextBlock |  |  |

## Clinica · src/Clinica.Modulo.Faturamento/Views/RelatoriosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/RelatoriosView.xaml) · 297 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 5 | UserControl |  |  |
| 27 | TextBlock | Relatórios de faturamento |  |
| 39 | TextBlock | De: |  |
| 41 | TextBlock | Até: |  |
| 43 | Button | Gerar | {Binding GerarCommand} |
| 44 | Button |  | {Binding ExportarCsvCommand} |
| 47 | TextBlock |  |  |
| 48 | TextBlock | Exportar CSV |  |
| 51 | Button |  | {Binding GerarFechamentoCommand} |
| 56 | TextBlock |  |  |
| 57 | TextBlock | Fechamento (PDF) |  |
| 62 | TextBlock | {Binding Mensagem} |  |
| 77 | TextBlock |  |  |
| 78 | TextBlock | Códigos gerados |  |
| 81 | TextBlock | {Binding Resumo.TotalCodigos, FallbackValue=0} |  |
| 90 | TextBlock |  |  |
| 91 | TextBlock | Baixados |  |
| 95 | TextBlock | {Binding Resumo.Baixados, FallbackValue=0} |  |
| 105 | TextBlock |  |  |
| 106 | TextBlock | Pendentes |  |
| 110 | TextBlock | {Binding Resumo.Pendentes, FallbackValue=0} |  |
| 119 | TextBlock |  |  |
| 120 | TextBlock | Taxa de baixa |  |
| 125 | TextBlock | {Binding Resumo.TaxaBaixa, FallbackValue=0} |  |
| 127 | TextBlock | % |  |
| 137 | TextBlock |  |  |
| 138 | TextBlock | Taxa de glosa |  |
| 143 | TextBlock | {Binding Resumo.TaxaGlosa, FallbackValue=0} |  |
| 145 | TextBlock | % |  |
| 155 | TextBlock |  |  |
| 156 | TextBlock | Tempo médio de baixa |  |
| 160 | TextBlock | {Binding Resumo.TempoMedioBaixaDias, FallbackValue='—', TargetNullValue='—'} |  |
| 162 | TextBlock | dias |  |
| 178 | TextBlock | Faturamento por convênio (período) |  |
| 184 | DataGrid |  |  |
| 188 | DataGrid |  |  |
| 205 | TextBlock | Consultas por especialidade (período) |  |
| 208 | DataGrid |  |  |
| 212 | DataGrid |  |  |
| 218 | TextBlock | Nenhuma consulta avulsa lançada no período. |  |
| 219 | TextBlock |  |  |
| 237 | TextBlock | Não conformidades (guias justificadas na rodada) |  |
| 242 | DataGrid |  |  |
| 246 | DataGrid |  |  |
| 264 | TextBlock | Pendências em aberto (envelhecimento) |  |
| 266 | DataGrid |  |  |
| 269 | DataGrid |  |  |
| 279 | TextBlock | Evolução mensal (últimos 6 meses) |  |
| 281 | DataGrid |  |  |
| 284 | DataGrid |  |  |

## Clinica · src/Clinica.Modulo.Faturamento/Views/TissView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Faturamento/Views/TissView.xaml) · 92 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 5 | UserControl |  |  |
| 15 | TextBlock | Guias TISS — lotes |  |
| 16 | TextBlock | Gere o lote (XML padrão TISS 4.01) com as guias baixadas ainda não exportadas, marque o envio à operadora (protocolo) e registre o retorno do demonstrativo — glosas entram no Controle de glosas com prazo de recurso. Dados do prestador e códigos TUSS ficam em Configurações. |  |
| 21 | TextBlock | Novo lote (guias baixadas do período, ainda sem lote) |  |
| 23 | TextBlock | De: |  |
| 25 | TextBlock | Até: |  |
| 27 | Button | Gerar lote TISS (.xml) | {Binding ExportarCommand} |
| 30 | TextBlock | {Binding Mensagem} |  |
| 36 | TextBlock | Lotes gerados |  |
| 51 | DataGrid |  |  |
| 52 | DataGrid |  |  |
| 66 | Button | XML | {Binding DataContext.BaixarXmlCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 70 | Button | Marcar enviado | {Binding DataContext.MarcarEnviadoCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 76 | Button | Registrar retorno | {Binding DataContext.RegistrarRetornoCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/CategoriaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/CategoriaWindow.xaml) · 46 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Categoria do plano de contas |  |
| 9 | Window |  |  |
| 15 | TextBlock | Categoria nova |  |
| 16 | TextBlock | É a classificação que responde "para onde foi o dinheiro" no fluxo de caixa. Sem categoria o lançamento não some do total — aparece como "Sem categoria". |  |
| 19 | TextBlock | Código |  |
| 20 | TextBox | {Binding Codigo, UpdateSourceTrigger=PropertyChanged} |  |
| 21 | TextBlock | NÃO muda depois de criado: é a referência que os lançamentos já gravados apontam. Nome, ordem e ativa mudam à vontade. |  |
| 25 | TextBlock | Nome |  |
| 26 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 28 | TextBlock | Tipo |  |
| 29 | ComboBox |  |  |
| 34 | TextBlock | {Binding Mensagem} |  |
| 38 | Button | Cancelar |  |
| 40 | Button | Criar categoria | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/CobrancaPixWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/CobrancaPixWindow.xaml) · 95 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Cobrar por Pix |  |
| 9 | Window |  |  |
| 16 | TextBlock | Cobrar por Pix |  |
| 18 | TextBlock | O código é gerado aqui, sem passar por banco nenhum. Ele NÃO confirma pagamento: quem confere se o dinheiro caiu continua sendo quem olha o extrato. |  |
| 30 | TextBlock | Valor (R$) |  |
| 31 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 35 | TextBlock | Referência (opcional) |  |
| 36 | TextBox | {Binding Referencia, UpdateSourceTrigger=PropertyChanged} |  |
| 41 | TextBlock | A referência é o que permite casar este Pix com a sessão no extrato. Sem ela, o extrato traz só o nome de quem pagou. |  |
| 45 | Button | Gerar código | {Binding GerarCommand} |
| 53 | TextBlock | Pix copia e cola |  |
| 54 | TextBox | {Binding CopiaECola, Mode=OneWay} |  |
| 59 | Button | Copiar | {Binding CopiarCommand} |
| 60 | TextBlock | {Binding Procedencia} |  |
| 86 | TextBlock | {Binding Mensagem} |  |
| 90 | Button | Fechar |  |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/ContaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ContaWindow.xaml) · 92 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Conta a pagar ou a receber |  |
| 9 | Window |  |  |
| 15 | TextBlock | Conta nova |  |
| 16 | TextBlock | Nada nasce pago: a conta entra como PREVISTA e vira realizada na baixa. O sistema sabe que ela vence, não que foi quitada. |  |
| 19 | TextBlock | Descrição |  |
| 20 | TextBox | {Binding Descricao, UpdateSourceTrigger=PropertyChanged} |  |
| 29 | TextBlock | Valor total da obrigação |  |
| 30 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 33 | TextBlock | Primeiro vencimento |  |
| 38 | TextBlock | Fornecedor / cliente |  |
| 39 | TextBox | {Binding Contraparte} |  |
| 40 | TextBlock | Documento de referência (nota, contrato ou cobrança) |  |
| 41 | TextBox | {Binding DocumentoReferencia} |  |
| 42 | TextBlock | Competência |  |
| 44 | TextBlock | Quantidade de parcelas mensais |  |
| 45 | TextBox | {Binding QuantidadeParcelas} |  |
| 46 | TextBlock | Use 1 para pagamento único. O total será dividido entre as parcelas, com vencimentos mensais e centavos distribuídos sem alterar o total. Parcelar a obrigação não registra pagamento. |  |
| 48 | TextBlock | Categoria |  |
| 49 | ComboBox |  |  |
| 53 | RadioButton | A pagar |  |
| 55 | RadioButton | A receber |  |
| 64 | TextBlock | De quem é (opcional) |  |
| 65 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 67 | TextBlock | {Binding Seletor.Erro} |  |
| 80 | TextBlock | {Binding Mensagem} |  |
| 84 | Button | Cancelar |  |
| 86 | Button | Registrar conta | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/ContasFixasWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ContasFixasWindow.xaml) · 90 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Contas fixas |  |
| 23 | TextBlock | Contas fixas |  |
| 25 | Button | Nova conta fixa | {Binding NovaRecorrenteCommand} |
| 33 | TextBlock | Nenhuma conta fixa. Enquanto não houver, o aluguel continua sendo redigitado todo mês. |  |
| 35 | TextBlock |  |  |
| 60 | TextBlock | {Binding Descricao} |  |
| 62 | TextBlock |  |  |
| 69 | TextBlock | {Binding Vigencia} |  |
| 74 | Button | Editar | {Binding DataContext.EditarRecorrenteCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/ExtratoEstoqueWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ExtratoEstoqueWindow.xaml) · 110 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Extrato do item |  |
| 13 | TextBlock | {Binding Titulo} |  |
| 18 | TextBlock | Todos os movimentos, do mais recente para o mais antigo, com o saldo depois de cada um. O motivo escrito da perda e a direção do acerto de inventário aparecem na coluna de detalhe. |  |
| 20 | TextBlock | {Binding Resumo} |  |
| 25 | Button | Fechar |  |
| 43 | TextBlock | DATA |  |
| 44 | TextBlock | TIPO |  |
| 45 | TextBlock | MOVIMENTO |  |
| 46 | TextBlock | SALDO APÓS |  |
| 47 | TextBlock | DETALHE (motivo · lote · validade) |  |
| 48 | TextBlock | QUEM |  |
| 65 | TextBlock | {Binding Data} |  |
| 66 | TextBlock | {Binding Tipo} |  |
| 68 | TextBlock |  |  |
| 82 | TextBlock | {Binding Movimento} |  |
| 84 | TextBlock | {Binding SaldoApos} |  |
| 87 | TextBlock | {Binding Detalhe} |  |
| 90 | TextBlock | {Binding Quem} |  |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/ItemEstoqueWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ItemEstoqueWindow.xaml) · 89 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Item do estoque |  |
| 9 | Window |  |  |
| 15 | TextBlock | {Binding Titulo} |  |
| 16 | TextBlock | Cadastre materiais assistenciais, medicamentos e produtos de rotina. O saldo é atualizado pelas entradas e consumos. |  |
| 19 | TextBlock | Nome |  |
| 20 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 22 | TextBlock | Código interno |  |
| 23 | TextBox | {Binding CodigoInterno} |  |
| 24 | TextBlock | Código de barras |  |
| 25 | TextBox | {Binding CodigoBarras} |  |
| 26 | TextBlock | Grupo |  |
| 27 | ComboBox |  |  |
| 28 | TextBlock | Uso permitido |  |
| 29 | ComboBox |  |  |
| 30 | TextBlock | Fabricante |  |
| 31 | TextBox | {Binding Fabricante} |  |
| 32 | TextBlock | Apresentação / especificação |  |
| 33 | TextBox | {Binding Apresentacao} |  |
| 42 | TextBlock | Unidade de consumo |  |
| 43 | TextBox | {Binding Unidade, UpdateSourceTrigger=PropertyChanged} |  |
| 47 | TextBlock | Estoque mínimo |  |
| 48 | TextBox | {Binding Minimo, UpdateSourceTrigger=PropertyChanged} |  |
| 52 | TextBlock | Sem mínimo, o item não entra no alerta de reposição — deixe em branco quando não fizer sentido vigiar. |  |
| 56 | TextBlock | Estoque máximo (alvo da reposição) |  |
| 57 | TextBox | {Binding Maximo} |  |
| 58 | TextBlock | Unidade de compra (ex.: cx) |  |
| 59 | TextBox | {Binding UnidadeCompra} |  |
| 60 | TextBlock | Unidades de consumo por embalagem (ex.: 100 un por cx) |  |
| 61 | TextBox | {Binding FatorCompra} |  |
| 62 | TextBlock | Local de armazenamento |  |
| 63 | TextBox | {Binding LocalArmazenamento} |  |
| 64 | CheckBox | Exigir lote nas entradas |  |
| 65 | CheckBox | Exigir validade nas entradas |  |
| 66 | TextBlock | Medicamentos sempre exigem lote e validade. Cadastre cada apresentação como um produto distinto. |  |
| 68 | TextBlock | Observações |  |
| 69 | TextBox | {Binding Observacoes} |  |
| 70 | CheckBox | Ativo |  |
| 71 | TextBlock | Inative produtos fora de uso. Itens com movimentação preservam seu histórico e não podem ser excluídos. |  |
| 77 | TextBlock | {Binding Mensagem} |  |
| 81 | Button | Cancelar |  |
| 83 | Button | Salvar item | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/LancamentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/LancamentoWindow.xaml) · 172 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Novo lançamento |  |
| 9 | Window |  |  |
| 19 | TextBlock | Novo lançamento |  |
| 20 | TextBlock | O dinheiro que não vem de guia: aluguel, material, recebimento avulso. O que vem de guia entra pela Conciliação. |  |
| 31 | TextBlock | Tipo |  |
| 32 | ComboBox |  |  |
| 37 | TextBlock | Data |  |
| 42 | TextBlock | Descrição |  |
| 43 | TextBox | {Binding Descricao, UpdateSourceTrigger=PropertyChanged} |  |
| 53 | TextBlock | Valor |  |
| 54 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 55 | TextBlock | Sempre positivo — quem decide entrada ou saída é o tipo. |  |
| 61 | TextBlock | Situação |  |
| 62 | ComboBox |  |  |
| 64 | TextBlock | Previsto = a pagar/a receber; entra só no saldo previsto. |  |
| 78 | TextBlock | Categoria |  |
| 79 | ComboBox |  |  |
| 84 | TextBlock | Forma de pagamento |  |
| 85 | ComboBox |  |  |
| 95 | TextBlock | De quem é (opcional) |  |
| 96 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 98 | TextBlock | {Binding Seletor.Erro} |  |
| 108 | TextBlock | Está pagando qual sessão? |  |
| 109 | ComboBox |  |  |
| 111 | TextBlock | Só as sessões particulares que ainda não têm dinheiro registrado. Amarrar aqui é o que tira a sessão da aba Particulares da Conciliação. |  |
| 114 | TextBlock | {Binding AvisoSessoes} |  |
| 132 | TextBlock | Adquirente |  |
| 133 | TextBox | {Binding Adquirente, UpdateSourceTrigger=PropertyChanged} |  |
| 136 | TextBlock | Bandeira |  |
| 137 | TextBox | {Binding Bandeira, UpdateSourceTrigger=PropertyChanged} |  |
| 140 | TextBlock | Parcelas |  |
| 141 | TextBox | {Binding Parcelas, UpdateSourceTrigger=PropertyChanged} |  |
| 145 | CheckBox | Reter imposto sobre este recebimento |  |
| 152 | TextBlock | {Binding ResumoDeducoes} |  |
| 155 | TextBlock | Observações |  |
| 156 | TextBox | {Binding Observacoes} |  |
| 161 | TextBlock | {Binding Mensagem} |  |
| 165 | Button | Cancelar |  |
| 167 | Button | Salvar lançamento | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/MovimentoEstoqueWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/MovimentoEstoqueWindow.xaml) · 138 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Movimentar estoque |  |
| 8 | Window |  |  |
| 15 | TextBlock | Movimentar estoque |  |
| 16 | TextBlock | {Binding Item} |  |
| 22 | Button | Cancelar |  |
| 24 | Button | Registrar | {Binding SalvarCommand} |
| 30 | TextBlock | {Binding Mensagem} |  |
| 47 | TextBlock | Tipo |  |
| 48 | ComboBox |  |  |
| 53 | TextBlock | Quantidade |  |
| 54 | TextBox | {Binding Quantidade, UpdateSourceTrigger=PropertyChanged} |  |
| 59 | TextBlock | Data |  |
| 68 | TextBlock | Dados da entrada |  |
| 69 | CheckBox | Quantidade e custo na embalagem de compra |  |
| 70 | TextBlock | Marque para usar a unidade e o fator cadastrados no produto. Desmarcado: informe a unidade de consumo. |  |
| 72 | TextBlock | Fornecedor / origem |  |
| 73 | TextBox | {Binding Fornecedor} |  |
| 74 | TextBlock | Documento da entrada (nota, recibo ou referência) |  |
| 75 | TextBox | {Binding DocumentoEntrada} |  |
| 87 | TextBlock | Custo unitário |  |
| 88 | TextBox | {Binding CustoUnitario, UpdateSourceTrigger=PropertyChanged} |  |
| 93 | TextBlock | Validade |  |
| 99 | TextBlock | A validade fica no lote, não no item: o mesmo insumo entra em lotes com vencimentos diferentes, e uma validade só por item apagaria o que vence primeiro. |  |
| 105 | TextBlock | Lote de origem (conforme exigência do produto) |  |
| 106 | TextBox | {Binding Lote} |  |
| 108 | CheckBox | Registrar também a compra no financeiro |  |
| 110 | TextBlock | Fornecedor |  |
| 111 | TextBox | {Binding Fornecedor, UpdateSourceTrigger=PropertyChanged} |  |
| 112 | TextBlock | Vencimento da compra |  |
| 114 | CheckBox | Compra já paga |  |
| 115 | ComboBox |  |  |
| 117 | TextBlock | O valor será a quantidade × custo unitário. Estoque e conta serão gravados juntos. |  |
| 123 | TextBlock | Consumo de rotina — setor de destino |  |
| 124 | TextBox | {Binding SetorDestino} |  |
| 125 | TextBlock | Ex.: limpeza, recepção ou enfermagem. O produto deve permitir uso de rotina. Materiais de uma sessão são registrados no atendimento. |  |
| 128 | TextBlock | Observação |  |
| 129 | TextBox | {Binding Observacao} |  |
| 132 | TextBlock | Perda exige motivo escrito — perda sem motivo vira estoque que não bate. Saída maior que o saldo é recusada: estoque negativo não existe no mundo. |  |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/OrcamentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/OrcamentoWindow.xaml) · 45 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Teto de gasto |  |
| 9 | Window |  |  |
| 15 | TextBlock | {Binding Titulo} |  |
| 17 | TextBlock | O teto vale para ESTE mês e não reescreve os anteriores — reajustar agosto deixa julho com a régua que ele tinha na época. Só categoria de saída tem teto: alvo de receita é meta, e a direção define metas na tela própria. |  |
| 21 | TextBlock | Categoria |  |
| 22 | ComboBox |  |  |
| 25 | TextBlock | Teto do mês (R$) |  |
| 26 | TextBox | {Binding Teto, UpdateSourceTrigger=PropertyChanged} |  |
| 27 | TextBlock | Zero é uma decisão legítima (“não se gasta nada nesta categoria”). Apagar o teto é outra coisa: a categoria volta a não ter régua nenhuma. |  |
| 33 | TextBlock | {Binding Mensagem} |  |
| 37 | Button | Cancelar |  |
| 39 | Button | Salvar teto | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/RecorrenteWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/RecorrenteWindow.xaml) · 93 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Conta fixa |  |
| 9 | Window |  |  |
| 15 | TextBlock | {Binding Titulo} |  |
| 16 | TextBlock | Aluguel, luz, contador, mensalidade de software. O molde não entra em total nenhum: quem entra é a conta prevista que ele gera, e que dali em diante tem vida própria — a luz nunca vem igual. |  |
| 19 | TextBlock | Descrição |  |
| 20 | TextBox | {Binding Descricao, UpdateSourceTrigger=PropertyChanged} |  |
| 29 | TextBlock | Valor |  |
| 30 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 33 | TextBlock | Repete |  |
| 36 | ComboBox |  |  |
| 49 | TextBlock | 1º vencimento |  |
| 53 | TextBlock | Até (opcional) |  |
| 58 | TextBlock | A série sai sempre do 1º vencimento mais N períodos, nunca da ocorrência anterior mais um — encadear faria o aluguel do dia 31 virar aluguel do dia 28 para sempre por causa de fevereiro. Contrato com prazo põe a data final e a série se encerra sozinha. |  |
| 62 | TextBlock | Categoria |  |
| 63 | ComboBox |  |  |
| 67 | RadioButton | A pagar |  |
| 70 | RadioButton | A receber |  |
| 74 | CheckBox | Ativa |  |
| 75 | TextBlock | Desativada para de gerar, mas o que já foi gerado permanece: contas de meses passados não somem porque o contrato acabou. |  |
| 81 | TextBlock | {Binding Mensagem} |  |
| 85 | Button | Cancelar |  |
| 87 | Button | Salvar conta fixa | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/RegraRepasseWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/RegraRepasseWindow.xaml) · 99 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Regra de repasse |  |
| 8 | Window |  |  |
| 14 | TextBlock | Regra de repasse |  |
| 19 | Button | Cancelar |  |
| 21 | Button | Salvar regra | {Binding SalvarCommand} |
| 27 | TextBlock | {Binding Mensagem} |  |
| 33 | TextBlock | Profissional |  |
| 34 | ComboBox |  |  |
| 37 | TextBlock | Base do cálculo |  |
| 38 | ComboBox |  |  |
| 43 | TextBlock | Percentual da receita (%) |  |
| 44 | TextBox | {Binding Percentual, UpdateSourceTrigger=PropertyChanged} |  |
| 45 | TextBlock | Incide sobre a receita que ENTROU dos atendimentos dele no período — não sobre o que foi faturado e ainda não recebido. |  |
| 64 | TextBlock | Valor por atendimento |  |
| 65 | TextBox | {Binding ValorPorAtendimento, UpdateSourceTrigger=PropertyChanged} |  |
| 67 | TextBlock | Valor fixo por sessão realizada, independente do que entrou no caixa. |  |
| 80 | TextBlock | Vigente de |  |
| 84 | TextBlock | Vigente até |  |
| 89 | TextBlock | Deixe o fim em branco para a regra valer daqui em diante. Subir o percentual depois é criar uma regra nova — a apuração antiga continua com a que valia no período. |  |
| 93 | TextBlock | Observações |  |
| 94 | TextBox | {Binding Observacoes} |  |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/RegrasRepasseWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/RegrasRepasseWindow.xaml) · 116 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Regras e apurações de repasse |  |
| 24 | TextBlock | Regras |  |
| 25 | TextBlock | A regra tem vigência: subir o percentual em setembro não reescreve o repasse de agosto, que já foi pago. |  |
| 41 | TextBlock | {Binding Profissional} |  |
| 43 | TextBlock |  |  |
| 51 | Button | Excluir | {Binding DataContext.ExcluirRegraCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 67 | TextBlock | Apurações |  |
| 68 | TextBlock | Cada apuração cria uma saída prevista no caixa e trava o período — repasse pago duas vezes é dinheiro que não volta. |  |
| 86 | TextBlock | {Binding Profissional} |  |
| 88 | TextBlock | {Binding Valor} |  |
| 91 | TextBlock |  |  |
| 99 | Button | Cancelar | {Binding DataContext.CancelarApuracaoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/TaxaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/TaxaWindow.xaml) · 113 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Taxa da maquininha |  |
| 9 | Window |  |  |
| 15 | TextBlock | {Binding Titulo} |  |
| 16 | TextBlock | Use as condições do contrato. Pagamentos de pacientes no cartão exigem uma regra cadastrada; taxa zero também deve ser informada. A regra da bandeira tem prioridade sobre a genérica. |  |
| 26 | TextBlock | Maquininha / contrato |  |
| 27 | TextBox | {Binding Adquirente, UpdateSourceTrigger=PropertyChanged} |  |
| 30 | TextBlock | Bandeira (opcional) |  |
| 31 | TextBox | {Binding Bandeira, UpdateSourceTrigger=PropertyChanged} |  |
| 35 | TextBlock | Modalidade |  |
| 36 | ComboBox |  |  |
| 47 | TextBlock | Parcelas de |  |
| 48 | TextBox | {Binding ParcelasDe, UpdateSourceTrigger=PropertyChanged} |  |
| 51 | TextBlock | Até |  |
| 52 | TextBox | {Binding ParcelasAte, UpdateSourceTrigger=PropertyChanged} |  |
| 63 | TextBlock | Percentual (%) |  |
| 64 | TextBox | {Binding Percentual, UpdateSourceTrigger=PropertyChanged} |  |
| 67 | TextBlock | Primeiro crédito (dias) |  |
| 68 | TextBox | {Binding DiasParaReceber, UpdateSourceTrigger=PropertyChanged} |  |
| 79 | TextBlock | Vigente de |  |
| 83 | TextBlock | Até |  |
| 88 | TextBlock | A adquirente renegocia: o que vale no recebimento de março é o percentual de março. Por isso o valor da taxa é COPIADO na venda, nunca referenciado — reajustar aqui não reescreve o que já foi recebido. |  |
| 93 | CheckBox | Receber uma parcela por mês |  |
| 94 | TextBlock | Marcado: primeiro depósito no prazo informado, demais nos meses seguintes. Desmarcado: valor integral no primeiro depósito. Use nomes distintos para contratos diferentes da mesma maquininha. |  |
| 97 | CheckBox | Ativa |  |
| 101 | TextBlock | {Binding Mensagem} |  |
| 105 | Button | Cancelar |  |
| 107 | Button | Salvar taxa | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/TributoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/TributoWindow.xaml) · 98 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Tributo do regime |  |
| 9 | Window |  |  |
| 15 | TextBlock | {Binding Titulo} |  |
| 16 | TextBlock | Uma linha por guia que a clínica recolhe. Cada tributo é arredondado ao centavo separadamente, porque é o detalhe que vai ao contador. |  |
| 26 | TextBlock | Sigla |  |
| 27 | TextBox | {Binding Sigla, UpdateSourceTrigger=PropertyChanged} |  |
| 30 | TextBlock | Nome (opcional) |  |
| 31 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 42 | TextBlock | Alíquota (%) |  |
| 43 | TextBox | {Binding Percentual, UpdateSourceTrigger=PropertyChanged} |  |
| 46 | TextBlock | Base de cálculo (%) |  |
| 47 | TextBox | {Binding Base, UpdateSourceTrigger=PropertyChanged} |  |
| 51 | TextBlock | Nem todo tributo incide sobre a receita inteira: no Lucro Presumido o IRPJ de 15% incide sobre 32%, e a efetiva é 4,8%. Em branco = 100%, que é o caso normal. |  |
| 55 | TextBlock | Convênio que retém na fonte (opcional) |  |
| 57 | TextBox | {Binding Convenio, UpdateSourceTrigger=PropertyChanged} |  |
| 58 | TextBlock | Preenchido = a operadora retém este tributo antes de depositar. A retenção SUBSTITUI os tributos gerais naquele recebimento, nunca se soma — os dois são o mesmo imposto, e somá-los conta duas vezes. |  |
| 69 | TextBlock | Vigente de |  |
| 73 | TextBlock | Até |  |
| 78 | TextBlock | Reajuste do ISS entra como linha nova: sem vigência, o mês já declarado mudaria junto. |  |
| 82 | CheckBox | Ativo |  |
| 86 | TextBlock | {Binding Mensagem} |  |
| 90 | Button | Cancelar |  |
| 92 | Button | Salvar tributo | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Financeiro/Janelas/ValidadesEstoqueWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Janelas/ValidadesEstoqueWindow.xaml) · 80 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Validades e mínimos |  |
| 10 | Window |  |  |
| 29 | TextBlock | Validades |  |
| 30 | TextBlock | Lotes vencidos ou a vencer nos próximos 60 dias, só dos itens que ainda têm saldo — alertar sobre item zerado seria barulho. |  |
| 34 | TextBlock | Nada vencendo e nada abaixo do mínimo. |  |
| 47 | TextBlock | {Binding Item} |  |
| 50 | TextBlock | {Binding Validade} |  |
| 53 | TextBlock | {Binding Situacao} |  |
| 55 | TextBlock |  |  |

## Clinica · src/Clinica.Modulo.Financeiro/Views/CaixaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/CaixaView.xaml) · 291 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 17 | TextBlock | Caixa |  |
| 18 | TextBlock | Entradas, saídas e saldo do mês. |  |
| 22 | Button | ◀ | {Binding MesAnteriorCommand} |
| 27 | TextBlock | {Binding Mes, StringFormat='{}{0:MMMM/yyyy}'} |  |
| 31 | Button | ▶ | {Binding ProximoMesCommand} |
| 33 | Button | Atualizar | {Binding CarregarCommand} |
| 37 | Button | Exportar CSV | {Binding ExportarCommand} |
| 39 | Button | Cobrar por Pix | {Binding CobrarPixCommand} |
| 41 | Button | Novo lançamento | {Binding NovoLancamentoCommand} |
| 56 | TextBlock |  |  |
| 57 | TextBlock | ENTRADAS |  |
| 60 | TextBlock | {Binding Entradas} |  |
| 69 | TextBlock |  |  |
| 70 | TextBlock | SAÍDAS |  |
| 73 | TextBlock | {Binding Saidas} |  |
| 82 | TextBlock |  |  |
| 83 | TextBlock | RESULTADO LÍQUIDO |  |
| 86 | TextBlock | {Binding Saldo} |  |
| 95 | TextBlock |  |  |
| 96 | TextBlock | PROJEÇÃO BRUTA |  |
| 99 | TextBlock | {Binding Previsto} |  |
| 114 | TextBlock | RECEBIDO LIQUIDO |  |
| 115 | TextBlock | {Binding Liquido} |  |
| 116 | TextBlock | Valor após taxas e tributos registrados. No extrato da maquininha, confira o bruto menos a taxa; tributos provisionados são recolhidos separadamente. |  |
| 121 | TextBlock | TAXAS E IMPOSTOS |  |
| 122 | TextBlock | {Binding Deducoes} |  |
| 147 | TextBlock | Descrição ou categoria |  |
| 148 | TextBox | {Binding FiltroTexto, UpdateSourceTrigger=PropertyChanged} |  |
| 154 | TextBlock | Situação |  |
| 155 | ComboBox |  |  |
| 160 | TextBlock | {Binding ResumoFiltro} |  |
| 165 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 188 | TextBlock | Data |  |
| 189 | TextBlock | Descrição |  |
| 190 | TextBlock | Categoria |  |
| 191 | TextBlock | Situação |  |
| 192 | TextBlock | Valor |  |
| 193 | TextBlock | Ações |  |
| 201 | TextBlock | Mostrando os lançamentos mais recentes deste mês. Os totais acima consideram o mês inteiro. |  |
| 221 | TextBlock | {Binding Data} |  |
| 226 | TextBlock | {Binding Descricao} |  |
| 229 | TextBlock | Originado de guia do faturamento |  |
| 236 | TextBlock | {Binding Categoria} |  |
| 239 | TextBlock | {Binding StatusRotulo} |  |
| 242 | TextBlock | {Binding ValorFormatado} |  |
| 249 | Button | Histórico | {Binding DataContext.HistoricoCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 252 | Button | Realizar | {Binding DataContext.RealizarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 259 | Button | Recibo | {Binding DataContext.EmitirReciboCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 266 | Button | Cancelar | {Binding DataContext.CancelarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/ConciliacaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ConciliacaoView.xaml) · 476 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 17 | TextBlock | Conciliação |  |
| 18 | TextBlock | {Binding Resumo} |  |
| 21 | Button | ◀ | {Binding MesAnteriorCommand} |
| 26 | TextBlock | {Binding Mes, StringFormat='{}{0:MMMM/yyyy}'} |  |
| 30 | Button | ▶ | {Binding ProximoMesCommand} |
| 32 | Button | Atualizar | {Binding CarregarCommand} |
| 39 | TabItem | A lançar |  |
| 48 | TextBlock | Guias que o faturamento já efetivou no convênio e ainda sem receita no caixa. Informe o valor recebido para lançar — a guia sai da lista e o lançamento fica vinculado a ela. |  |
| 77 | TextBlock | Convênio |  |
| 78 | ComboBox |  |  |
| 83 | TextBlock | Paciente |  |
| 84 | TextBox | {Binding FiltroPaciente, UpdateSourceTrigger=PropertyChanged} |  |
| 90 | TextBlock | Nº da guia |  |
| 91 | TextBox | {Binding FiltroGuia, UpdateSourceTrigger=PropertyChanged} |  |
| 98 | CheckBox | Só as sem valor proposto |  |
| 102 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 129 | TextBlock | Baixa |  |
| 130 | TextBlock | Paciente |  |
| 131 | TextBlock | Convênio |  |
| 132 | TextBlock | Guia |  |
| 133 | TextBlock | Valor recebido |  |
| 152 | TextBlock | {Binding DataBaixa} |  |
| 156 | TextBlock | {Binding Paciente} |  |
| 158 | TextBlock | {Binding Tipo} |  |
| 162 | TextBlock | {Binding Convenio} |  |
| 167 | TextBlock | {Binding NumeroGuia} |  |
| 180 | TextBlock | {Binding AvisoGlosa} |  |
| 184 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 187 | Button | Reter? | {Binding DataContext.PreverCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 193 | Button | Lançar | {Binding DataContext.LancarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 203 | TextBlock | {Binding Procedencia} |  |
| 210 | TextBlock | {Binding Retencao} |  |
| 238 | TabItem | Glosadas |  |
| 242 | TextBlock | Guias glosadas pelo convênio que ainda têm receita lançada no caixa. Derrubar a receita NÃO apaga o lançamento: ele fica cancelado com o motivo, e a guia volta para a aba “A lançar” caso o recurso seja aceito. Receita já RECEBIDA não se cancela — se a operadora estornou, lance a devolução como saída. |  |
| 246 | TextBlock | {Binding ResumoGlosadas} |  |
| 254 | TextBlock | Não foi possível conferir as glosas deste mês. A lista abaixo está VAZIA por falha de leitura, não por não haver glosa. |  |
| 273 | TextBlock | Glosa |  |
| 274 | TextBlock | Paciente e motivo |  |
| 275 | TextBlock | Guia |  |
| 276 | TextBlock | Valor |  |
| 277 | TextBlock | Situação |  |
| 278 | TextBlock |  |  |
| 298 | TextBlock | {Binding DataGlosa} |  |
| 303 | TextBlock | {Binding Paciente} |  |
| 305 | TextBlock | {Binding Motivo} |  |
| 309 | TextBlock | {Binding Orientacao} |  |
| 315 | TextBlock | {Binding NumeroGuia} |  |
| 318 | TextBlock | {Binding Valor} |  |
| 323 | TextBlock | {Binding Situacao} |  |
| 325 | TextBlock | {Binding Prazo} |  |
| 331 | Button | Derrubar receita | {Binding DataContext.CancelarReceitaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 353 | TabItem | Particulares |  |
| 357 | TextBlock | Sessões particulares realizadas no mês sem nenhum dinheiro registrado. Informe o valor e diga se foi RECEBIDO (forma de pagamento) ou se fica A RECEBER (vencimento) — nos dois casos a sessão sai da lista e o lançamento fica vinculado a ela. O balcão é avisado na próxima visita de quem está aqui. |  |
| 362 | TextBlock | {Binding ResumoParticulares} |  |
| 370 | TextBlock | Não foi possível conferir as sessões particulares deste mês. A lista abaixo está VAZIA por falha de leitura, não por estar tudo pago. |  |
| 387 | TextBlock | Sessão |  |
| 388 | TextBlock | Paciente |  |
| 389 | TextBlock | Modalidade |  |
| 390 | TextBlock | Como foi paga |  |
| 408 | TextBlock | {Binding Data} |  |
| 412 | TextBlock | {Binding Paciente} |  |
| 414 | TextBlock | {Binding Convenio} |  |
| 418 | TextBlock | {Binding Modalidade} |  |
| 426 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 429 | RadioButton | Recebido |  |
| 431 | RadioButton | A receber |  |
| 436 | ComboBox |  |  |
| 443 | Button | Lançar | {Binding DataContext.LancarSessaoCommand,
                                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 450 | TextBlock | {Binding Procedencia} |  |

## Clinica · src/Clinica.Modulo.Financeiro/Views/ContasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ContasView.xaml) · 260 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 19 | TextBlock | Contas a pagar e a receber |  |
| 20 | TextBlock | O que vence, e quando. O caixa registra o que já aconteceu; aqui está o que ainda precisa acontecer — e o que já passou da data. |  |
| 23 | Button | Contas fixas… | {Binding AbrirContasFixasCommand} |
| 32 | TextBlock | {Binding Mensagem} |  |
| 47 | TextBlock |  |  |
| 48 | TextBlock | A PAGAR VENCIDO |  |
| 53 | TextBlock | {Binding APagarVencido} |  |
| 54 | TextBlock |  |  |
| 72 | TextBlock |  |  |
| 73 | TextBlock | A PAGAR A VENCER |  |
| 76 | TextBlock | {Binding APagarAVencer} |  |
| 85 | TextBlock |  |  |
| 86 | TextBlock | A RECEBER VENCIDO |  |
| 89 | TextBlock | {Binding AReceberVencido} |  |
| 98 | TextBlock |  |  |
| 99 | TextBlock | A RECEBER A VENCER |  |
| 102 | TextBlock | {Binding AReceberAVencer} |  |
| 111 | TextBlock |  |  |
| 112 | TextBlock | SALDO PREVISTO |  |
| 115 | TextBlock | {Binding SaldoPrevisto} |  |
| 116 | TextBlock | a receber menos a pagar |  |
| 128 | TextBlock | Em aberto |  |
| 129 | TextBlock | {Binding Resumo} |  |
| 136 | ComboBox |  |  |
| 139 | TextBlock | Horizonte |  |
| 141 | ComboBox |  |  |
| 145 | Button | Nova conta | {Binding NovaContaCommand} |
| 148 | Button | Gerar fixas | {Binding GerarCommand} |
| 155 | Button | Exportar CSV | {Binding ExportarCommand} |
| 158 | Button | Atualizar | {Binding CarregarCommand} |
| 163 | TextBlock | "Gerar fixas" cria as contas previstas das recorrências ativas até o fim do horizonte. Rodar duas vezes não duplica nada — e nenhuma delas nasce paga: o sistema sabe que a conta vence, não que ela foi quitada. |  |
| 167 | TextBlock | Nada em aberto no horizonte escolhido. |  |
| 168 | TextBlock |  |  |
| 195 | TextBlock | {Binding Descricao} |  |
| 197 | TextBlock |  |  |
| 202 | TextBlock | conta fixa |  |
| 209 | TextBlock | {Binding Valor} |  |
| 216 | TextBlock | {Binding Prazo} |  |
| 218 | TextBlock |  |  |
| 234 | Button | Histórico | {Binding DataContext.HistoricoCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 237 | Button | Baixar | {Binding DataContext.BaixarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 244 | Button | Adiar 7d | {Binding DataContext.AdiarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/EstoqueView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/EstoqueView.xaml) · 295 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 17 | TextBlock | Estoque |  |
| 18 | TextBlock | Materiais assistenciais, medicamentos e produtos de rotina. Acompanhe saldos, lotes, compras e consumos. |  |
| 21 | TextBlock | {Binding Resumo} |  |
| 24 | Button | Validades e mínimos… | {Binding AbrirValidadesCommand} |
| 34 | TextBlock | {Binding Mensagem} |  |
| 41 | TabItem | Itens |  |
| 45 | TextBlock | Itens |  |
| 55 | Button | Lista de compras | {Binding ListaDeComprasCommand} |
| 58 | Button | Novo item | {Binding NovoItemCommand} |
| 61 | Button | Atualizar | {Binding CarregarCommand} |
| 68 | TextBlock | Buscar produto, código de barras, fabricante, grupo ou local |  |
| 69 | TextBox | {Binding Busca, UpdateSourceTrigger=PropertyChanged} |  |
| 86 | TextBlock | {Binding Nome} |  |
| 91 | TextBlock | repor |  |
| 94 | TextBlock | {Binding Cadastro} |  |
| 95 | TextBlock |  |  |
| 113 | Button | Extrato | {Binding DataContext.ExtratoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 118 | Button | Movimentar… | {Binding DataContext.MovimentarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 127 | Button | Inventário… | {Binding DataContext.InventariarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 132 | Button | Editar | {Binding DataContext.EditarItemCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 137 | Button | Excluir | {Binding DataContext.ExcluirItemCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 152 | TabItem | Materiais dos atendimentos |  |
| 156 | TextBlock | Materiais após o atendimento |  |
| 157 | TextBlock | Comece pela gestão e libere para a equipe quando estiver preparada. O registro é opcional e não interfere no atendimento nem nas guias. Atendimentos anteriores à ativação não viram pendências. |  |
| 160 | ComboBox |  |  |
| 163 | Button | Salvar ativação | {Binding SalvarModoMateriaisCommand} |
| 166 | TextBlock | De |  |
| 167 | TextBlock | Até |  |
| 168 | Button | Consultar | {Binding CarregarMateriaisCommand} |
| 170 | TextBlock | {Binding ResumoMateriais} |  |
| 177 | TextBlock | {Binding Paciente} |  |
| 178 | TextBlock | {Binding Data, StringFormat=dd/MM/yyyy HH:mm} |  |
| 179 | TextBlock | {Binding Situacao} |  |
| 180 | TextBlock | {Binding Pendencia} |  |
| 181 | Button | Ver / registrar materiais | {Binding DataContext.RegistrarMateriaisCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 195 | TabItem | Custo por sessão |  |
| 200 | TextBlock | Quanto custa uma sessão |  |
| 201 | TextBlock | Só entra saída ligada a um atendimento — a baixa digitada à mão não pertence a sessão nenhuma, e rateá-la daria a cada sessão um custo que ela não teve. A baixa por sessão sai do fechamento do atendimento, na Recepção. |  |
| 206 | TextBlock | De |  |
| 209 | TextBlock | até |  |
| 212 | Button | Atualizar | {Binding CarregarCustosCommand} |
| 225 | TextBlock | Sessões |  |
| 226 | TextBlock | {Binding CustoSessoes} |  |
| 229 | TextBlock | Custo médio |  |
| 230 | TextBlock | {Binding CustoMedio} |  |
| 233 | TextBlock | Total no período |  |
| 234 | TextBlock | {Binding CustoTotal} |  |
| 238 | TextBlock | {Binding CustoResumo} |  |
| 258 | TextBlock | {Binding Data} |  |
| 260 | TextBlock | {Binding Paciente} |  |
| 263 | TextBlock | {Binding Itens} |  |
| 269 | TextBlock | {Binding Custo} |  |

## Clinica · src/Clinica.Modulo.Financeiro/Views/ExtratoBancoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ExtratoBancoView.xaml) · 172 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | UserControl |  |  |
| 16 | TextBlock | Extrato do banco |  |
| 17 | TextBlock | Importe o arquivo OFX que o banco exporta e confira, linha por linha, o que o sistema diz que entrou contra o que o banco diz que entrou. Nada é conciliado sozinho — o sistema acha o par e você confirma. |  |
| 24 | Button | Abrir extrato (.ofx)… | {Binding AbrirArquivoCommand} |
| 28 | Button | Recruzar | {Binding CruzarCommand} |
| 35 | TextBlock | {Binding Arquivo} |  |
| 40 | TextBlock | {Binding Resumo} |  |
| 57 | TextBlock | {Binding Mensagem} |  |
| 68 | TextBlock | No sistema e NÃO no extrato — dado como recebido/pago aqui e sem correspondência no banco: |  |
| 74 | TextBlock |  |  |
| 104 | TextBlock | {Binding Data} |  |
| 108 | TextBlock | {Binding Valor} |  |
| 113 | TextBlock | {Binding Descricao} |  |
| 114 | TextBlock | {Binding Situacao} |  |
| 122 | ComboBox |  |  |
| 133 | Button | Conferir | {Binding DataContext.ConciliarCommand,
                                                              RelativeSource={RelativeSource AncestorType=UserControl}} |
| 144 | Button | Desfazer | {Binding DataContext.DesfazerCommand,
                                                              RelativeSource={RelativeSource AncestorType=UserControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/FechamentoCaixaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/FechamentoCaixaView.xaml) · 289 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | UserControl |  |  |
| 13 | TextBlock | Fechamento de caixa |  |
| 14 | TextBlock | A conferência da gaveta: o que o sistema diz que entrou em dinheiro vivo contra o que foi contado. Só espécie — cartão e PIX caem na conta dias depois e não passam pela gaveta. |  |
| 20 | TextBlock | {Binding Mensagem} |  |
| 30 | TextBlock | Conferir o dia |  |
| 32 | TextBlock | Dia |  |
| 40 | TextBlock | Entrou em espécie |  |
| 42 | TextBlock | {Binding EntradasEspecie} |  |
| 49 | TextBlock | Saiu em espécie |  |
| 51 | TextBlock | {Binding SaidasEspecie} |  |
| 61 | TextBlock | A gaveta deveria ter |  |
| 63 | TextBlock | {Binding Esperado} |  |
| 68 | TextBlock | {Binding Lancamentos} |  |
| 84 | TextBlock | {Binding JaConferido} |  |
| 99 | TextBlock | Contado na gaveta |  |
| 101 | TextBox | {Binding ValorContado, UpdateSourceTrigger=PropertyChanged} |  |
| 108 | TextBlock | {Binding DiferencaPrevia} |  |
| 110 | TextBlock |  |  |
| 129 | TextBlock | O que aconteceu? |  |
| 131 | TextBox | {Binding Justificativa, UpdateSourceTrigger=PropertyChanged} |  |
| 134 | TextBlock | Obrigatória. Diferença aceita em silêncio é o mesmo que caixa não conferido. |  |
| 139 | Button | Conferir caixa | {Binding ConferirCommand} |
| 148 | TextBlock | Dias não conferidos |  |
| 150 | TextBlock | {Binding Resumo} |  |
| 153 | TextBlock | Dia em que ninguém pagou em dinheiro não entra aqui: cobrar conferência de gaveta vazia treinaria a clínica a fechar no automático. |  |
| 169 | TextBlock | {Binding Data} |  |
| 170 | TextBlock |  |  |
| 177 | Button | Conferir | {Binding DataContext.IrParaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 197 | TextBlock | Conferências dos últimos 60 dias |  |
| 199 | Button | Atualizar | {Binding CarregarCommand} |
| 204 | TextBlock | Nenhuma conferência registrada ainda. |  |
| 205 | TextBlock |  |  |
| 233 | TextBlock |  |  |
| 238 | TextBlock | {Binding Detalhe} |  |
| 243 | TextBlock | {Binding Esperado} |  |
| 246 | TextBlock | {Binding Contado} |  |
| 253 | TextBlock | {Binding Diferenca} |  |
| 256 | TextBlock |  |  |
| 270 | Button | Reabrir | {Binding DataContext.ReabrirCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/FluxoCaixaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/FluxoCaixaView.xaml) · 293 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 15 | TextBlock | Fluxo de caixa |  |
| 16 | TextBlock | A série dos meses e para onde o dinheiro está indo. Um mês ruim pode ser azar; três meses caindo é um problema — e é a série que mostra a diferença. |  |
| 21 | TextBlock | Meses |  |
| 23 | ComboBox |  |  |
| 25 | Button | Exportar | {Binding ExportarCommand} |
| 27 | Button | Atualizar | {Binding CarregarCommand} |
| 34 | TextBlock | {Binding Mensagem} |  |
| 49 | TextBlock |  |  |
| 50 | TextBlock | ENTRADAS |  |
| 53 | TextBlock | {Binding TotalEntradas} |  |
| 54 | TextBlock | {Binding Periodo} |  |
| 63 | TextBlock |  |  |
| 64 | TextBlock | SAÍDAS |  |
| 67 | TextBlock | {Binding TotalSaidas} |  |
| 76 | TextBlock |  |  |
| 77 | TextBlock | RESULTADO |  |
| 82 | TextBlock | {Binding Resultado} |  |
| 83 | TextBlock |  |  |
| 101 | TextBlock |  |  |
| 102 | TextBlock | MARGEM |  |
| 107 | TextBlock | {Binding Margem} |  |
| 108 | TextBlock | do que entrou, quanto sobrou |  |
| 120 | TextBlock | Evolução mensal |  |
| 130 | TextBlock | ENTRADAS |  |
| 136 | TextBlock | RESULTADO (entradas − saídas) |  |
| 148 | TextBlock | {Binding MaiorSaida} |  |
| 156 | TextBlock | Mês a mês |  |
| 158 | TextBlock | O acumulado é a VARIAÇÃO no período consultado, não o saldo em conta: a clínica nunca cadastrou saldo inicial, e chamar isso de saldo daria um número que não bate com o extrato do banco. |  |
| 171 | TextBlock | Mês |  |
| 172 | TextBlock | Entradas |  |
| 174 | TextBlock | Saídas |  |
| 176 | TextBlock | Resultado |  |
| 178 | TextBlock | Previsto |  |
| 180 | TextBlock | Acumulado |  |
| 199 | TextBlock | {Binding Mes} |  |
| 201 | TextBlock | {Binding Entradas} |  |
| 203 | TextBlock | {Binding Saidas} |  |
| 205 | TextBlock | {Binding Resultado} |  |
| 207 | TextBlock |  |  |
| 220 | TextBlock | {Binding Previsto} |  |
| 223 | TextBlock | {Binding Acumulado} |  |
| 243 | TextBlock | De onde veio |  |
| 245 | TextBlock | Nenhuma entrada classificada no período. |  |
| 247 | TextBlock |  |  |
| 266 | TextBlock | Para onde foi |  |
| 268 | CheckBox | Incluir previsto |  |
| 272 | TextBlock | Nenhuma saída no período. |  |
| 274 | TextBlock |  |  |

## Clinica · src/Clinica.Modulo.Financeiro/Views/InadimplenciaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/InadimplenciaView.xaml) · 312 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 16 | TextBlock | Quem me deve |  |
| 17 | TextBlock | Conta de paciente vencida, agrupada por pessoa. Só entra o que tem dono: conta a receber sem paciente (reembolso de convênio, venda de produto) é a receber, não inadimplência — e cobrar quem não deve custa mais do que a sessão em aberto. |  |
| 23 | TextBlock | {Binding Mensagem} |  |
| 38 | TextBlock |  |  |
| 39 | TextBlock | TOTAL EM ATRASO |  |
| 42 | TextBlock | {Binding Total} |  |
| 44 | TextBlock | contas de paciente vencidas |  |
| 54 | TextBlock |  |  |
| 55 | TextBlock | PACIENTES |  |
| 58 | TextBlock | {Binding QuantidadePacientes} |  |
| 60 | TextBlock | pessoas a cobrar |  |
| 69 | TextBlock |  |  |
| 70 | TextBlock | CONTAS |  |
| 73 | TextBlock | {Binding QuantidadeContas} |  |
| 75 | TextBlock | lançamentos em aberto |  |
| 84 | TextBlock |  |  |
| 85 | TextBlock | MÉDIO POR PACIENTE |  |
| 90 | TextBlock | {Binding MedioPorPaciente} |  |
| 92 | TextBlock | quanto deve quem deve |  |
| 115 | TextBlock | Paciente |  |
| 116 | TextBox | {Binding FiltroPaciente, UpdateSourceTrigger=PropertyChanged} |  |
| 121 | CheckBox | Só críticos (mais de 90 dias) |  |
| 126 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 138 | TextBlock | Idade da dívida |  |
| 139 | TextBlock | Não é enfeite de relatório: separa o atraso que um lembrete resolve do que já exige decisão — acordo, ou parar de contar com o dinheiro. |  |
| 158 | TextBlock | Cobrar por |  |
| 163 | ComboBox |  |  |
| 167 | TextBlock | {Binding Resumo} |  |
| 172 | Button | Atualizar | {Binding CarregarCommand} |
| 175 | Button | Exportar | {Binding ExportarCommand} |
| 219 | TextBlock | {Binding Nome} |  |
| 221 | TextBlock | {Binding Resumo} |  |
| 224 | TextBlock | {Binding Faixa} |  |
| 229 | TextBlock | {Binding Total} |  |
| 237 | Button | WhatsApp | {Binding DataContext.CobrarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 260 | TextBlock | {Binding Descricao} |  |
| 264 | TextBlock | {Binding Vencimento} |  |
| 267 | TextBlock | {Binding Valor} |  |
| 271 | TextBlock | {Binding Atraso} |  |
| 275 | Button | Recebi | {Binding DataContext.ReceberCommand,
                                                                          RelativeSource={RelativeSource AncestorType=UserControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/PlanoContasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/PlanoContasView.xaml) · 103 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 16 | TextBlock | Plano de contas |  |
| 17 | TextBlock | As categorias que classificam entradas e saídas. O código NÃO muda depois de criado: ele é a referência que os lançamentos já gravados apontam. |  |
| 19 | TextBlock | {Binding Resumo} |  |
| 25 | TextBlock | {Binding Mensagem} |  |
| 31 | TextBlock | Categorias |  |
| 37 | Button | Nova categoria | {Binding NovaCategoriaCommand} |
| 40 | Button | Atualizar | {Binding CarregarCommand} |
| 60 | TextBlock | {Binding Codigo} |  |
| 65 | TextBlock | {Binding Nome} |  |
| 67 | TextBlock | {Binding Tipo} |  |
| 77 | TextBlock | ativa |  |
| 79 | Button | Ativar / desativar | {Binding DataContext.AlternarAtivaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/ProducaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ProducaoView.xaml) · 155 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 13 | TextBlock | Produção |  |
| 14 | TextBlock | Volume de códigos por mês, efetivados e pendentes. |  |
| 19 | Button | 6 meses | {Binding Ultimos6MesesCommand} |
| 21 | Button | 12 meses | {Binding Ultimos12MesesCommand} |
| 23 | Button | Atualizar | {Binding CarregarCommand} |
| 37 | TextBlock |  |  |
| 38 | TextBlock | CÓDIGOS NO PERÍODO |  |
| 41 | TextBlock | {Binding TotalCodigos} |  |
| 50 | TextBlock |  |  |
| 51 | TextBlock | EFETIVADOS |  |
| 54 | TextBlock | {Binding TotalBaixados} |  |
| 63 | TextBlock |  |  |
| 64 | TextBlock | PENDENTES |  |
| 67 | TextBlock | {Binding TotalPendentes} |  |
| 76 | TextBlock |  |  |
| 77 | TextBlock | TAXA DE EFETIVAÇÃO |  |
| 80 | TextBlock | {Binding TaxaBaixaFormatada} |  |
| 101 | TextBlock | Mês |  |
| 102 | TextBlock | Códigos |  |
| 103 | TextBlock | Efetivados |  |
| 104 | TextBlock | Pendentes |  |
| 105 | TextBlock | Taxa |  |
| 124 | TextBlock | {Binding Rotulo} |  |
| 127 | TextBlock | {Binding TotalCodigos} |  |
| 129 | TextBlock | {Binding Baixados} |  |
| 131 | TextBlock | {Binding Pendentes} |  |
| 136 | TextBlock | {Binding TaxaBaixa, StringFormat='{}{0:0.#}%'} |  |

## Clinica · src/Clinica.Modulo.Financeiro/Views/RecebiveisView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/RecebiveisView.xaml) · 295 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 20 | TextBlock | Recebíveis de cartão |  |
| 21 | TextBlock | Previsão e conferência dos créditos de cada contrato, já descontadas as taxas. |  |
| 26 | TextBlock | Horizonte |  |
| 28 | ComboBox |  |  |
| 30 | Button | Atualizar | {Binding CarregarCommand} |
| 37 | TextBlock | {Binding Mensagem} |  |
| 52 | TextBlock |  |  |
| 53 | TextBlock | A RECEBER |  |
| 56 | TextBlock | {Binding AVencer} |  |
| 57 | TextBlock | líquido, já descontada a taxa |  |
| 67 | TextBlock |  |  |
| 68 | TextBlock | ATRASADO |  |
| 73 | TextBlock | {Binding Atrasado} |  |
| 74 | TextBlock |  |  |
| 92 | TextBlock |  |  |
| 93 | TextBlock | TOTAL |  |
| 96 | TextBlock | {Binding Total} |  |
| 105 | TextBlock |  |  |
| 106 | TextBlock | PRÓXIMO DEPÓSITO |  |
| 109 | TextBlock | {Binding Proximo} |  |
| 116 | TabItem | Previstos |  |
| 120 | TextBlock | Depósitos previstos |  |
| 121 | TextBlock | {Binding Resumo} |  |
| 131 | TextBlock | Caiu em |  |
| 135 | TextBlock | Informe a data em que o crédito entrou na conta, conforme o extrato. |  |
| 140 | TextBlock | Nenhum recebimento de cartão pendente. Vendas em dinheiro e Pix entram inteiras e na hora — não há o que esperar. |  |
| 142 | TextBlock |  |  |
| 170 | TextBlock |  |  |
| 177 | TextBlock | {Binding Detalhe} |  |
| 179 | TextBlock |  |  |
| 195 | TextBlock | {Binding Bruto} |  |
| 198 | TextBlock | {Binding Taxa} |  |
| 201 | TextBlock | {Binding Liquido} |  |
| 205 | Button | Caiu | {Binding DataContext.ConfirmarCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 223 | TabItem | Já caíram |  |
| 227 | TextBlock | Depósitos confirmados |  |
| 228 | TextBlock | As duas datas ficam gravadas separadas — a prevista e a real —, e é por isso que o atraso continua visível depois de o dinheiro entrar. Confirmação lançada no dia errado se desfaz aqui: o lançamento continua no caixa, só a data do crédito é apagada. |  |
| 230 | TextBlock | {Binding ResumoConfirmados} |  |
| 249 | TextBlock |  |  |
| 254 | TextBlock | {Binding Detalhe} |  |
| 256 | TextBlock |  |  |
| 272 | TextBlock | {Binding Liquido} |  |
| 276 | Button | Desfazer | {Binding DataContext.DesfazerCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/RepassesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/RepassesView.xaml) · 125 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 16 | TextBlock | Repasses |  |
| 17 | TextBlock | Quanto cada profissional tem a receber. A produção vem do agendamento (é ele que sabe quem atendeu) e a receita considerada é a que ENTROU — repassar sobre o que não foi recebido faria a clínica pagar dinheiro que não tem. |  |
| 22 | Button | ◀ | {Binding MesAnteriorCommand} |
| 24 | TextBlock | {Binding PeriodoRotulo} |  |
| 27 | Button | ▶ | {Binding ProximoMesCommand} |
| 35 | Button | Exportar CSV | {Binding ExportarCommand} |
| 38 | Button | Atualizar | {Binding CarregarCommand} |
| 43 | Button | Regras e apurações… | {Binding AbrirRegrasCommand} |
| 49 | TextBlock | {Binding Resumo} |  |
| 55 | TextBlock | {Binding Mensagem} |  |
| 62 | TextBlock | A repassar no período |  |
| 79 | TextBlock | {Binding Profissional} |  |
| 81 | TextBlock |  |  |
| 90 | TextBlock | {Binding Situacao} |  |
| 97 | TextBlock | {Binding Valor} |  |
| 101 | Button | Apurar | {Binding DataContext.ApurarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/ResultadoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/ResultadoView.xaml) · 277 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 16 | TextBlock | Resultado do mês |  |
| 17 | TextBlock | {Binding Resumo} |  |
| 21 | Button | ◀ | {Binding MesAnteriorCommand} |
| 26 | TextBlock | {Binding Mes, StringFormat='{}{0:MMMM/yyyy}'} |  |
| 30 | Button | ▶ | {Binding ProximoMesCommand} |
| 32 | Button | Exportar CSV | {Binding ExportarCommand} |
| 34 | Button | Atualizar | {Binding CarregarCommand} |
| 40 | TextBlock | {Binding Mensagem} |  |
| 46 | TabItem | Resultado |  |
| 52 | TextBlock | Regime de CAIXA: entra o que se moveu no mês. Taxa de maquininha e imposto são DEDUÇÃO, não despesa — saem da receita antes de ela existir, e listá-los junto do aluguel faria a clínica achar que pode cortá-los. |  |
| 60 | TextBlock | Não foi possível apurar este mês. Os números estão em “—” por falha de leitura, não por ausência de movimento. |  |
| 76 | TextBlock | Receita bruta |  |
| 77 | TextBlock | {Binding Receita} |  |
| 80 | TextBlock | (−) Deduções |  |
| 81 | TextBlock | {Binding Deducoes} |  |
| 84 | TextBlock | (=) Receita líquida |  |
| 85 | TextBlock | {Binding ReceitaLiquida} |  |
| 88 | TextBlock | (−) Despesas |  |
| 89 | TextBlock | {Binding Despesas} |  |
| 92 | TextBlock | (=) Resultado |  |
| 93 | TextBlock | {Binding Resultado} |  |
| 94 | TextBlock |  |  |
| 107 | TextBlock | Margem |  |
| 108 | TextBlock | {Binding Margem} |  |
| 122 | TextBlock | De onde veio |  |
| 130 | TextBlock | {Binding Categoria} |  |
| 132 | TextBlock | {Binding Valor} |  |
| 148 | TextBlock | Para onde foi |  |
| 156 | TextBlock | {Binding Categoria} |  |
| 158 | TextBlock | {Binding Valor} |  |
| 176 | TabItem | Teto de gasto |  |
| 180 | TextBlock | {Binding ResumoOrcamento} |  |
| 182 | Button | Definir teto | {Binding DefinirTetoCommand} |
| 190 | TextBlock | Não foi possível ler os tetos deste mês. A lista está vazia por falha de leitura, não por ausência de teto. |  |
| 213 | TextBlock | {Binding Categoria} |  |
| 215 | TextBlock | {Binding Situacao} |  |
| 218 | TextBlock |  |  |
| 240 | TextBlock | {Binding Teto} |  |
| 243 | TextBlock | {Binding Gasto} |  |
| 246 | TextBlock | {Binding Saldo} |  |
| 250 | Button | Excluir | {Binding DataContext.ExcluirTetoCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Financeiro/Views/TaxasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Financeiro/Views/TaxasView.xaml) · 501 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 6 | UserControl |  |  |
| 13 | TextBlock | Taxas e impostos |  |
| 14 | TextBlock | O que a maquininha e o fisco descontam de cada recebimento. O caixa continua guardando o valor BRUTO — o que o paciente pagou —, e o líquido é calculado a partir daqui. |  |
| 20 | TextBlock | {Binding Mensagem} |  |
| 31 | TabItem | Maquininha |  |
| 38 | TextBlock | Taxas cadastradas |  |
| 42 | Button | Nova taxa | {Binding NovaTaxaCommand} |
| 47 | TextBlock | Nenhuma taxa cadastrada. Sem taxa, o caixa registra só o bruto — e não inventa desconto. |  |
| 49 | TextBlock |  |  |
| 75 | TextBlock | {Binding Descricao} |  |
| 77 | TextBlock |  |  |
| 96 | TextBlock | {Binding Situacao} |  |
| 101 | Button | Editar | {Binding DataContext.EditarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 108 | Button | Excluir | {Binding DataContext.ExcluirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 127 | TabItem | Regime tributário |  |
| 161 | TextBlock | Alíquota única (modo antigo) |  |
| 163 | TextBlock | É ela que vale enquanto não houver nenhum tributo cadastrado acima. Cadastre o primeiro tributo e esta faixa some — o imposto passa a ser apurado por guia. |  |
| 170 | TextBlock | Alíquota (%) |  |
| 171 | TextBox | {Binding AliquotaImposto, UpdateSourceTrigger=PropertyChanged} |  |
| 174 | Button | Salvar alíquota | {Binding SalvarImpostoCommand} |
| 184 | TextBlock | Tributos cadastrados |  |
| 186 | TextBlock |  |  |
| 190 | TextBlock | {Binding OrigemDaCarga} |  |
| 194 | Button | Novo tributo | {Binding NovoTributoCommand} |
| 200 | TextBlock | Nenhum tributo cadastrado. Enquanto não houver, vale a alíquota única ao lado. |  |
| 202 | TextBlock |  |  |
| 228 | TextBlock | {Binding Descricao} |  |
| 230 | TextBlock |  |  |
| 254 | TextBlock |  |  |
| 255 | TextBlock |  |  |
| 270 | Button | Editar | {Binding DataContext.EditarTributoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 277 | Button | Excluir | {Binding DataContext.ExcluirTributoCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 296 | TabItem | Simulador |  |
| 300 | TextBlock | Quanto sobra e quando cai |  |
| 302 | TextBlock | A pergunta do balcão e a da negociação com a adquirente. Usa exatamente a mesma conta da venda real — não grava nada, e o número simulado é o que vai acontecer. |  |
| 321 | TextBlock | Valor |  |
| 322 | TextBox | {Binding SimValor, UpdateSourceTrigger=PropertyChanged} |  |
| 326 | TextBlock | Forma |  |
| 327 | ComboBox |  |  |
| 332 | TextBlock | Adquirente |  |
| 333 | TextBox | {Binding SimAdquirente, UpdateSourceTrigger=PropertyChanged} |  |
| 337 | TextBlock | Bandeira |  |
| 338 | TextBox | {Binding SimBandeira, UpdateSourceTrigger=PropertyChanged} |  |
| 342 | TextBlock | Parcelas |  |
| 343 | TextBox | {Binding SimParcelas, UpdateSourceTrigger=PropertyChanged} |  |
| 349 | CheckBox | Reter imposto |  |
| 351 | Button | Simular | {Binding SimularCommand} |
| 365 | TextBlock | {Binding SimResultado} |  |
| 372 | TextBlock | Não há taxa cadastrada para essa combinação, então o líquido acima está SEM desconto de maquininha. Cadastre a taxa na aba anterior para o número ficar real. |  |
| 385 | TabItem | Apuração do mês |  |
| 390 | TextBlock | Quanto de cada tributo saiu no mês |  |
| 392 | TextBlock | {Binding ResumoApuracao} |  |
| 396 | Button | ◀ | {Binding MesApuracaoAnteriorCommand} |
| 401 | TextBlock | {Binding MesApuracao, StringFormat='{}{0:MMMM/yyyy}'} |  |
| 405 | Button | ▶ | {Binding MesApuracaoProximoCommand} |
| 407 | Button | Apurar | {Binding ApurarCommand} |
| 408 | Button | Exportar CSV | {Binding ExportarApuracaoCommand} |
| 418 | TextBlock | Não foi possível apurar este mês. A lista está VAZIA por falha de leitura, não por ausência de imposto. |  |
| 427 | TextBlock | {Binding DivergenciaApuracao} |  |
| 445 | TextBlock | Tributo |  |
| 446 | TextBlock | Nome |  |
| 447 | TextBlock | Alíquota |  |
| 448 | TextBlock | Base |  |
| 449 | TextBlock | Valor |  |
| 450 | TextBlock | Quem recolhe |  |
| 469 | TextBlock | {Binding Sigla} |  |
| 473 | TextBlock | {Binding Nome} |  |
| 476 | TextBlock | {Binding Aliquota} |  |
| 479 | TextBlock | {Binding Base} |  |
| 482 | TextBlock | {Binding Valor} |  |
| 485 | TextBlock | {Binding Natureza} |  |

## Clinica · src/Clinica.Modulo.Gerente/Janelas/MetaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Janelas/MetaWindow.xaml) · 104 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Meta do mês |  |
| 9 | Window |  |  |
| 19 | TextBlock | {Binding Titulo} |  |
| 21 | TextBlock | A meta é uma decisão datada: ela vale para o mês em que foi definida e não reescreve os meses anteriores. Corrigir o alvo de um mês em curso substitui o valor; meses já fechados continuam com o alvo que tinham na época. |  |
| 33 | TextBlock | Mês |  |
| 34 | ComboBox |  |  |
| 39 | TextBlock | Ano |  |
| 40 | TextBox | {Binding Ano} |  |
| 44 | TextBlock | Indicador |  |
| 45 | ComboBox |  |  |
| 48 | TextBlock | Alvo |  |
| 54 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 59 | TextBlock | {Binding UnidadeAtual} |  |
| 64 | TextBlock | De quem |  |
| 65 | ComboBox |  |  |
| 67 | TextBlock | A meta da clínica é a de sempre. A do profissional é a exceção que revela quem carrega quem — “a clínica bateu” costuma esconder uma agenda lotada compensando outra vazia. |  |
| 71 | TextBlock | Observações |  |
| 72 | TextBox | {Binding Observacoes} |  |
| 92 | TextBlock | {Binding Mensagem} |  |
| 96 | Button | Cancelar |  |
| 98 | Button | Salvar meta | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Gerente/Janelas/PrecoConvenioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Janelas/PrecoConvenioWindow.xaml) · 98 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Preço por convênio |  |
| 9 | Window |  |  |
| 17 | TextBlock | {Binding Titulo} |  |
| 18 | TextBlock | Quanto a operadora paga por esta guia. É PROPOSTA para a conciliação do Financeiro, não imposição: a operadora pode ter pago menos (glosa parcial), e a linha mostra de onde veio o número. |  |
| 21 | TextBlock | Convênio |  |
| 22 | ComboBox |  |  |
| 25 | TextBlock | Tipo de guia |  |
| 26 | ComboBox |  |  |
| 29 | TextBlock | Especialidade (em branco = todas) |  |
| 37 | ComboBox |  |  |
| 39 | TextBlock | Preencha só quando o valor depender da especialidade. Um preço com especialidade VENCE o genérico do tipo — senão a exceção ficaria cadastrada e a conciliação continuaria propondo o valor da regra geral. |  |
| 43 | TextBlock | Valor da guia |  |
| 44 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 53 | TextBlock | Vigente de |  |
| 57 | TextBlock | Até |  |
| 62 | TextBlock | Reajuste da operadora entra como uma linha NOVA, com a data em que passou a valer. A guia de março continua sendo proposta pelo valor de março, e o que já foi lançado no caixa nunca muda. |  |
| 66 | CheckBox | Ativo |  |
| 86 | TextBlock | {Binding Mensagem} |  |
| 90 | Button | Cancelar |  |
| 92 | Button | Salvar preço | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Gerente/Janelas/UsuarioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Janelas/UsuarioWindow.xaml) · 204 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Usuário |  |
| 9 | Window |  |  |
| 20 | TextBlock | {Binding Titulo} |  |
| 30 | TextBlock | Nome |  |
| 31 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 35 | TextBlock | Usuário (login) |  |
| 36 | TextBox | {Binding Login, UpdateSourceTrigger=PropertyChanged} |  |
| 38 | TextBlock | O login não muda depois de criado — é ele que aparece na auditoria. |  |
| 52 | TextBlock | Perfil |  |
| 53 | ComboBox |  |  |
| 55 | TextBlock | Médico: atendimento e prescrições. Enfermagem: evoluções BSV / BSV + acupuntura e infusões. |  |
| 60 | TextBlock | Profissional vinculado |  |
| 61 | ComboBox |  |  |
| 69 | TextBlock | CPF (para assinatura digital) |  |
| 71 | TextBox | {Binding CpfProfissional, UpdateSourceTrigger=PropertyChanged} |  |
| 73 | TextBlock | {Binding CpfDica} |  |
| 77 | TextBlock | Senha |  |
| 79 | TextBlock |  |  |
| 82 | CheckBox | Exigir troca de senha no próximo acesso |  |
| 85 | CheckBox | Ativo (pode entrar no sistema) |  |
| 96 | TextBlock | O que esta pessoa pode fazer |  |
| 97 | TextBlock | O perfil já marca o padrão da função. Marque ou desmarque só as exceções — sem isso, a exceção vira senha compartilhada, e a auditoria deixa de valer. |  |
| 103 | TextBlock | {Binding ResumoExcecoes} |  |
| 119 | TextBlock | {Binding Name} |  |
| 142 | CheckBox |  |  |
| 146 | TextBlock | {Binding Rotulo} |  |
| 150 | TextBlock | {Binding Explicacao} |  |
| 160 | TextBlock | {Binding Procedencia} |  |
| 193 | TextBlock | {Binding Mensagem} |  |
| 197 | Button | Cancelar |  |
| 199 | Button | Salvar usuário | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Gerente/Views/AcessosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/AcessosView.xaml) · 158 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 19 | TextBlock | Acessos |  |
| 20 | TextBlock | Quem entra na suíte, com qual perfil e o que cada um pode fazer. |  |
| 25 | Button | Atualizar | {Binding CarregarCommand} |
| 27 | Button | Novo usuário | {Binding NovoCommand} |
| 31 | TextBlock | O app de Faturamento não passa por login: ele está congelado e roda num posto só. Recepção, Financeiro e Gerente Geral exigem usuário. |  |
| 53 | TextBlock | {Binding Mensagem} |  |
| 73 | TextBlock | Nome |  |
| 74 | TextBlock | Login |  |
| 75 | TextBlock | Perfil |  |
| 76 | TextBlock | Profissional |  |
| 77 | TextBlock | Último acesso |  |
| 78 | TextBlock | Ações |  |
| 100 | TextBlock | {Binding Nome} |  |
| 102 | TextBlock | {Binding Situacao} |  |
| 106 | TextBlock | {Binding Login} |  |
| 108 | TextBlock | {Binding Perfil} |  |
| 110 | TextBlock | {Binding Profissional} |  |
| 113 | TextBlock | {Binding UltimoAcesso} |  |
| 119 | Button | Editar | {Binding DataContext.EditarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 123 | Button | Redefinir senha | {Binding DataContext.RedefinirSenhaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 128 | Button | Excluir | {Binding DataContext.ExcluirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Gerente/Views/AuditoriaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/AuditoriaView.xaml) · 260 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 16 | TextBlock | Auditoria |  |
| 17 | TextBlock | Quem fez o quê, e quando. Toda ação que mexe em dinheiro ou permissão grava aqui — baixa de guia, estorno, glosa, lote, lançamento, conta, tributo, preço de convênio, criação de usuário e troca de senha. |  |
| 39 | TextBlock | {Binding Mensagem} |  |
| 60 | TextBlock | Prontuário de |  |
| 61 | TextBox | {Binding Paciente.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 66 | CheckBox | Só quem ABRIU o prontuário |  |
| 70 | TextBlock | Sem marcar, a lista traz também o que foi ESCRITO no prontuário desta pessoa. |  |
| 107 | TextBlock | Ação |  |
| 110 | ComboBox | {Binding Acao} |  |
| 114 | TextBlock | Operador |  |
| 115 | TextBox | {Binding Operador, UpdateSourceTrigger=PropertyChanged} |  |
| 119 | TextBlock | No detalhe |  |
| 120 | TextBox | {Binding Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 124 | TextBlock | De |  |
| 128 | TextBlock | Até |  |
| 132 | TextBlock | Limite |  |
| 133 | ComboBox |  |  |
| 138 | Button | Consultar | {Binding CarregarCommand} |
| 140 | Button | Limpar | {Binding LimparCommand} |
| 143 | Button | Exportar | {Binding ExportarCommand} |
| 147 | TextBlock | {Binding Resumo} |  |
| 155 | TextBlock | O limite foi atingido: há mais eventos no período do que os mostrados. Estreite o filtro ou aumente o limite antes de concluir qualquer coisa a partir das contagens abaixo. |  |
| 167 | TextBlock | Por ação |  |
| 176 | TextBlock | Por operador |  |
| 178 | TextBlock | Quem assina é quem fez LOGIN, nunca o usuário do Windows: no balcão duas pessoas dividem a mesma máquina. |  |
| 192 | TextBlock | Somente leitura, e isso é decisão: registro de auditoria que se pode editar ou apagar não é auditoria, é rascunho. |  |
| 196 | TextBlock | Nenhum evento no filtro atual. |  |
| 197 | TextBlock |  |  |
| 216 | TextBlock | Quando |  |
| 217 | TextBlock | Ação |  |
| 218 | TextBlock | Operador |  |
| 219 | TextBlock | Detalhe |  |
| 236 | TextBlock | {Binding Quando} |  |
| 239 | TextBlock | {Binding Acao} |  |
| 242 | TextBlock | {Binding Operador} |  |
| 245 | TextBlock | {Binding Detalhe} |  |
| 246 | TextBlock | {Binding Referencias} |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/CampanhasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/CampanhasView.xaml) · 200 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 19 | TextBlock | Campanhas |  |
| 20 | TextBlock | {Binding IntervaloFormatado, StringFormat='Confirmação, satisfação e recall — mostrando {0}'} |  |
| 25 | ComboBox |  |  |
| 27 | ComboBox |  |  |
| 30 | ComboBox |  |  |
| 32 | Button | Atualizar | {Binding CarregarCommand} |
| 42 | TextBlock | Gerar rodada |  |
| 43 | TextBlock | O sistema descobre quem contatar, respeita o consentimento de comunicação (LGPD) e nunca repete o mesmo paciente na mesma rodada. O envio continua sendo um clique por pessoa — é o WhatsApp da clínica. |  |
| 48 | Button | Abrir confirmações da agenda | {Binding GerarConfirmacoesCommand} |
| 50 | Button | NPS de ontem | {Binding GerarNpsCommand} |
| 52 | Button | Abrir fila de recall | {Binding GerarRecallCommand} |
| 56 | TextBlock | {Binding ResultadoRodada} |  |
| 79 | TextBlock | {Binding Mensagem} |  |
| 100 | TextBlock | Paciente |  |
| 101 | TextBlock | Telefone |  |
| 102 | TextBlock | Campanha |  |
| 103 | TextBlock | Referência |  |
| 104 | TextBlock | Situação |  |
| 105 | TextBlock | Nota |  |
| 106 | TextBlock | Ações |  |
| 129 | TextBlock | {Binding Paciente} |  |
| 131 | TextBlock | {Binding Comentario} |  |
| 136 | TextBlock | {Binding Telefone} |  |
| 139 | TextBlock | {Binding Tipo} |  |
| 142 | TextBlock | {Binding Referencia} |  |
| 145 | TextBlock | {Binding Situacao} |  |
| 148 | TextBlock | {Binding Nota} |  |
| 154 | Button | WhatsApp | {Binding DataContext.EnviarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 159 | Button | Nota | {Binding DataContext.RegistrarNotaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 165 | Button | Respondeu | {Binding DataContext.RegistrarRespostaCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 170 | Button | Dispensar | {Binding DataContext.DispensarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Gerente/Views/CamposPersonalizadosWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/CamposPersonalizadosWindow.xaml) · 170 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Campos do prontuário |  |
| 11 | Window |  |  |
| 19 | TextBlock | Campos do prontuário |  |
| 21 | TextBlock | O que ESTA clínica anota em cada sessão além dos campos do sistema — “nº de agulhas”, “aparelho usado”, “carga do exercício”. Eles aparecem na folha da sessão, entram no prontuário e saem na exportação. Não substituem nenhum campo do sistema. |  |
| 24 | TextBlock | {Binding LimiteTexto} |  |
| 42 | TextBlock | {Binding Mensagem} |  |
| 46 | Button | Fechar |  |
| 49 | Button | Salvar campo | {Binding SalvarCommand} |
| 66 | Button | Novo campo | {Binding NovoCommand} |
| 70 | TextBlock | CADASTRADOS |  |
| 74 | DataGrid |  |  |
| 77 | DataGrid |  |  |
| 86 | Button | Editar | {Binding DataContext.EditarCommand,
                                                              RelativeSource={RelativeSource AncestorType=Window}} |
| 94 | Button |  | {Binding DataContext.AlternarCommand,
                                                              RelativeSource={RelativeSource AncestorType=Window}} |
| 97 | Button |  |  |
| 131 | TextBlock | O CAMPO |  |
| 133 | TextBlock | Nome (o que o profissional lê) |  |
| 134 | TextBox | {Binding Rotulo, UpdateSourceTrigger=PropertyChanged} |  |
| 138 | TextBlock | Tipo |  |
| 139 | ComboBox |  |  |
| 143 | TextBlock | Opções (uma por linha) |  |
| 145 | TextBox | {Binding Opcoes} |  |
| 149 | TextBlock | Onde aparece |  |
| 150 | ComboBox |  |  |
| 152 | TextBlock | Campo que aparece onde não serve é o campo que ninguém preenche — e que faz parar de preencher os outros. |  |
| 156 | TextBlock | Ajuda (o que a clínica entende por isto) |  |
| 158 | TextBox | {Binding Ajuda} |  |
| 160 | TextBlock | Sem isto, dois profissionais preenchem o mesmo campo com coisas diferentes e a coluna deixa de comparar. |  |
| 164 | CheckBox | Ativo (aparece na folha da sessão) |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/ConfiguracoesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/ConfiguracoesView.xaml) · 389 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 2 | UserControl |  |  |
| 9 | TextBlock | Configurações |  |
| 10 | TextBlock | O que vale para a clínica inteira. Cada bloco salva sozinho — são assuntos diferentes, e um botão único gravaria o que ninguém olhou. |  |
| 24 | TextBlock | {Binding Mensagem} |  |
| 27 | TabItem | Clínica |  |
| 34 | TabItem | Atendimento |  |
| 39 | TextBlock | Enfermagem e conclusão das sessões |  |
| 40 | TextBlock | Exigir evolução de enfermagem antes de concluir estas modalidades BSV: |  |
| 49 | CheckBox | {Binding Nome} |  |
| 53 | TextBlock | {Binding SituacaoConclusaoAutomatica} |  |
| 55 | TextBlock | Prazo em horas após a última gravação: |  |
| 56 | TextBox | {Binding PrazoConclusaoHoras, UpdateSourceTrigger=PropertyChanged} |  |
| 58 | TextBlock | O médico usa apenas Salvar sessão. Exemplo: conclusão em 24 horas após a última gravação da evolução médica. Vale para evoluções gravadas após ativar a regra. Nas modalidades marcadas, a falta de evolução de enfermagem mantém a sessão pendente, com aviso. As guias são geradas uma única vez. O sistema não assina documentos nem confirma pagamentos. A conclusão precisa de conexão com o banco; para funcionar com os computadores desligados, o serviço de conclusão deve estar ativo no servidor. |  |
| 59 | Button | Salvar regras de conclusão | {Binding SalvarConclusaoCommand} |
| 61 | TextBlock | Sessões que precisam de atenção |  |
| 62 | TextBlock | {Binding ResumoPendenciasConclusao} |  |
| 63 | Button | Atualizar pendências | {Binding AtualizarPendenciasConclusaoCommand} |
| 69 | TextBlock | {Binding Paciente} |  |
| 70 | TextBlock | {Binding Profissional} |  |
| 71 | TextBlock | {Binding Data, StringFormat=dd/MM/yyyy HH:mm} |  |
| 72 | TextBlock | {Binding AgendamentoId, StringFormat=Sessão {0}} |  |
| 73 | TextBlock | {Binding Motivo} |  |
| 84 | TabItem | Operação |  |
| 89 | TextBlock | Operação e marketing |  |
| 97 | TextBlock | Jornada diária (minutos) |  |
| 98 | TextBox | {Binding JornadaDiariaMinutos} |  |
| 99 | TextBlock | É o DENOMINADOR da taxa de ocupação. Clínica de meio período veria 50% com a agenda cheia se ficasse nos 480 padrão. |  |
| 102 | TextBlock | Recall: dias sem vir |  |
| 103 | TextBox | {Binding DiasInatividadeRecall} |  |
| 104 | TextBlock | A partir de quantos dias parado o paciente entra na lista de recall. |  |
| 108 | TextBlock | Faturamento na marcação |  |
| 109 | CheckBox | A guia nasce quando o atendimento entra no sistema (marcação ou avulso) |  |
| 110 | TextBlock | Ligada, marcar um horário já gera o atendimento e as guias — a faturista pode efetivar no portal com antecedência, e cancelamento/falta suspende as guias sozinho (reabrir devolve). Guia de sessão futura não entra no painel de pendências até a data chegar. ⚠ LIGUE SÓ DEPOIS DE ATUALIZAR TODAS AS MÁQUINAS da clínica: um programa antigo confirmando presença de um horário marcado pelo novo geraria guia em dobro. |  |
| 113 | TextBlock | Carimbadora de tempo (ACT) — opcional |  |
| 114 | TextBox | {Binding CarimbadoraDeTempo} |  |
| 115 | TextBlock | Endereço RFC 3161 de uma autoridade de carimbo do tempo, para as assinaturas ICP-Brasil das prescrições. Em branco a assinatura continua válida — só que a data é a do relógio de quem assinou, e o PDF diz isso. |  |
| 119 | Button | Escrever termos… | AoAbrirTermos |
| 121 | TextBlock | Termos assinados pelo paciente |  |
| 122 | TextBlock | O texto do consentimento e as declarações (jejum, medicações) que o paciente responde e assina na tela dele (o segundo monitor do balcão) antes do procedimento. Diga aqui qual procedimento exige qual termo — o balcão passa a cobrá-lo a cada sessão. |  |
| 127 | Button | Campos do prontuário… | AoAbrirCampos |
| 129 | TextBlock | Campos personalizados do prontuário |  |
| 130 | TextBlock | O que ESTA clínica anota em cada sessão além dos campos do sistema — “nº de agulhas”, “aparelho usado”, “carga do exercício”. Dado dentro do texto livre não se compara entre sessões nem vira coluna de relatório; aqui ele vira campo. |  |
| 135 | TextBlock | Certificado em nuvem (SafeID) — opcional |  |
| 136 | TextBlock | Credenciais da aplicação no portal da Safeweb. Cadastradas aqui uma vez, valem para todas as máquinas da clínica. Em branco, o sistema assina apenas com certificado instalado na máquina (token ou arquivo). |  |
| 138 | TextBlock | Estas credenciais estão vindo de VARIÁVEIS DE AMBIENTE desta máquina (SafeID__ClientId e afins), que têm prioridade sobre o que é salvo aqui. Enquanto elas existirem, os campos abaixo são somente leitura e mostram o que está em vigor. Para voltar a usar a configuração da clínica, apague as variáveis e reabra o sistema. |  |
| 147 | TextBlock | client_id |  |
| 148 | TextBox | {Binding SafeIdClientId} |  |
| 151 | TextBlock | client_secret |  |
| 152 | TextBox | {Binding SafeIdClientSecret} |  |
| 155 | CheckBox | Usar o ambiente de homologação da Safeweb (somente para testes) |  |
| 156 | TextBlock | Marcado, as assinaturas saem com certificado de teste e NÃO têm valor jurídico. Desmarque antes de usar na clínica. |  |
| 158 | Button | Salvar operação | {Binding SalvarOperacaoCommand} |
| 164 | TabItem | Faturamento |  |
| 171 | TabItem | Integrações |  |
| 176 | TextBlock | Publicação de documentos assinados |  |
| 177 | TextBlock | Receita, atestado, pedido de exame e declaração de comparecimento assinados digitalmente ganham um QR Code. O farmacêutico aponta a câmera e o documento abre — sem o paciente precisar enviar arquivo nenhum. Prontuário NUNCA é publicado. |  |
| 178 | TextBlock | {Binding SituacaoPublicacao} |  |
| 180 | TextBlock | Domínio da clínica |  |
| 181 | TextBox | {Binding DominioPublicacao} |  |
| 182 | TextBlock | Ex.: https://documentos.suaclinica.com.br — é o endereço que o farmacêutico vê ao escanear, e por isso precisa ser o domínio da clínica. Peça ao responsável pelo site um registro CNAME apontando para o provedor de armazenamento. Em branco, a publicação fica DESLIGADA. |  |
| 184 | TextBlock | Escolha o domínio com calma: o endereço fica gravado dentro de cada PDF assinado, e trocá-lo depois faz os QR Codes já impressos pararem de funcionar. Documento assinado não pode ser regerado — teria de ser cancelado e emitido de novo. |  |
| 186 | TextBlock | Dias no ar |  |
| 187 | TextBox | {Binding DiasPublicacao} |  |
| 189 | TextBlock | Por quantos dias o documento fica acessível pelo link, de 1 a 365. Receita simples costuma valer 30 dias; uso contínuo, mais. Vencido o prazo, o arquivo sai do ar e a clínica republica com um clique, sem perder o QR já impresso. O registro no sistema continua guardado por 20 anos, sempre. |  |
| 192 | TextBlock | Armazenamento (onde os arquivos ficam) |  |
| 193 | TextBlock | Serve qualquer provedor compatível com S3 (Magalu Cloud, Cloudflare R2, AWS). Cadastrado aqui uma vez, vale para todas as máquinas. IMPORTANTE: o balde não pode permitir LISTAGEM de conteúdo — o endereço secreto de cada documento é a única barreira, e um balde que lista entrega todos de uma vez. |  |
| 195 | TextBlock | Estas credenciais estão vindo de VARIÁVEIS DE AMBIENTE desta máquina (CLINICA_ARMAZENAMENTO_ENDPOINT e afins), que têm prioridade sobre o que é salvo aqui. Enquanto elas existirem, os campos abaixo são somente leitura e mostram o que está em vigor. Para voltar a usar a configuração da clínica, apague as variáveis e reabra o sistema. |  |
| 204 | TextBlock | Endereço do provedor (endpoint) |  |
| 205 | TextBox | {Binding ArmazenamentoEndpoint} |  |
| 208 | TextBlock | Balde (bucket) |  |
| 209 | TextBox | {Binding ArmazenamentoBucket} |  |
| 219 | TextBlock | Chave de acesso |  |
| 220 | TextBox | {Binding ArmazenamentoChave} |  |
| 223 | TextBlock | Chave secreta |  |
| 224 | TextBox | {Binding ArmazenamentoSegredo} |  |
| 228 | TextBlock | Região (opcional) |  |
| 229 | TextBox | {Binding ArmazenamentoRegiao} |  |
| 231 | TextBlock | Em branco, o sistema usa us-east-1, que é o que a maioria dos provedores aceita quando não tem região própria. |  |
| 234 | Button | Salvar publicação | {Binding SalvarPublicacaoCommand} |
| 235 | Button | {Binding RotuloTesteConexao} | {Binding TestarConexaoCommand} |
| 236 | Button | Enviar arquivo de teste | {Binding EnviarExemploCommand} |
| 240 | TextBlock | Arquivo de teste no ar. Abra o endereço abaixo no navegador — ele tem que ABRIR o PDF, não baixar. Depois apague o objeto no painel do provedor. |  |
| 241 | TextBox | {Binding UrlDoExemplo, Mode=OneWay} |  |
| 244 | TextBlock | Testar conexão grava um arquivo de teste no balde e o apaga em seguida — é o mesmo caminho que a publicação de verdade usa. Salve antes de testar. |  |
| 249 | TextBlock | Lembretes por e-mail |  |
| 250 | TextBlock | Na abertura da Recepção e do Gerente, o sistema manda a confirmação da sessão por e-mail a quem tem endereço na ficha — as sessões de hoje, de amanhã e do fim de semana que vier. É a MESMA rodada de confirmação do balcão: quem já foi avisado pelo WhatsApp não recebe e-mail, e quem recebeu aparece na rodada como avisado por e-mail. A mensagem não leva dado clínico. |  |
| 251 | TextBlock | {Binding SituacaoEmail} |  |
| 259 | TextBlock | Servidor de saída (SMTP) |  |
| 260 | TextBox | {Binding EmailSmtpHost} |  |
| 263 | TextBlock | Porta |  |
| 264 | TextBox | {Binding EmailSmtpPorta} |  |
| 274 | TextBlock | Usuário |  |
| 275 | TextBox | {Binding EmailSmtpUsuario} |  |
| 278 | TextBlock | Senha (ou senha de aplicativo) |  |
| 279 | TextBox | {Binding EmailSmtpSenha} |  |
| 289 | TextBlock | Remetente (e-mail que o paciente vê) |  |
| 290 | TextBox | {Binding EmailRemetente} |  |
| 293 | TextBlock | Nome do remetente |  |
| 294 | TextBox | {Binding EmailRemetenteNome} |  |
| 297 | CheckBox | Usar criptografia (TLS) — recomendado; desligue só para servidor interno da clínica |  |
| 298 | TextBlock | Gmail: smtp.gmail.com, porta 587, com SENHA DE APLICATIVO (não a senha da conta — o Google recusa). Outlook / Microsoft 365: smtp.office365.com, porta 587. Hospedagem própria: pergunte ao responsável pelo e-mail da clínica. A resposta do paciente chega no remetente. |  |
| 301 | Button | Salvar lembretes | {Binding SalvarEmailCommand} |
| 303 | TextBlock | Mandar o teste para |  |
| 304 | TextBox | {Binding EmailDestinoTeste} |  |
| 306 | Button | {Binding RotuloTesteEmail} | {Binding TestarEmailCommand} |
| 308 | TextBlock | O teste usa a configuração SALVA — salve antes de testar. Ele manda um único e-mail para o endereço acima; nenhum paciente recebe nada. |  |
| 313 | TextBlock | Tela do paciente |  |
| 314 | TextBlock | O monitor virado para quem assina, como a maquininha do cartão: a recepcionista conduz na tela dela e o paciente lê e assina na dele. Sem escolher nada, a assinatura é colhida na própria janela de quem conduz. |  |
| 321 | TextBlock | Monitor |  |
| 322 | ComboBox |  |  |
| 325 | Button | Salvar | {Binding SalvarTelaDoPacienteCommand} |
| 326 | Button | Testar | {Binding TestarTelaDoPacienteCommand} |
| 330 | TextBlock | {Binding AvisoTelaDoPaciente} |  |
| 332 | TextBlock | Use o Testar antes do primeiro paciente: as telas do Windows se chamam \\.\DISPLAY1 e \\.\DISPLAY2, e o único jeito de saber qual é qual é ver o exemplo aparecer nela. |  |
| 338 | TabItem | Backup |  |
| 343 | TextBlock | Backup da clínica |  |
| 344 | TextBlock | Uma cópia de tudo: pacientes, prontuário, agenda, financeiro, estoque e acessos. Guarde FORA desta máquina — pendrive ou nuvem. Cópia que fica no mesmo computador do banco não é plano B. |  |
| 346 | TextBlock | A cópia automática está DESLIGADA: nenhuma pasta de destino foi escolhida. Enquanto isso, a clínica só tem backup nos dias em que alguém clicar no botão abaixo. |  |
| 348 | TextBlock | Cópia automática |  |
| 349 | TextBlock | Gravada na abertura do Gerente quando o prazo vence. Escolha uma pasta de REDE ou de nuvem sincronizada — o sistema não tem como saber onde a pasta fica fisicamente, e cópia no mesmo computador não protege contra incêndio, furto nem ransomware. |  |
| 355 | TextBox | {Binding PastaBackup} |  |
| 356 | Button | Escolher pasta… | {Binding EscolherPastaBackupCommand} |
| 360 | TextBlock | Copiar a cada (dias) |  |
| 361 | TextBox | {Binding IntervaloBackupDias} |  |
| 364 | TextBlock | Cópias a guardar |  |
| 365 | TextBox | {Binding CopiasBackup} |  |
| 367 | Button | Salvar política | {Binding SalvarPoliticaBackupCommand} |
| 369 | TextBlock | Guardar várias cópias não é excesso: a corrupção que ninguém percebeu na sexta é copiada por cima da única cópia boa no sábado. |  |
| 370 | TextBlock | {Binding SituacaoBackup} |  |
| 373 | Button | Copiar agora | {Binding CopiarAgoraCommand} |
| 374 | Button | Salvar cópia em… | {Binding FazerBackupCommand} |
| 375 | Button | Conferir um backup | {Binding ConferirBackupCommand} |
| 377 | TextBlock | {Binding ResumoBackup} |  |
| 378 | TextBlock | Conferir abre o arquivo e conta o que há dentro dele, sem alterar nada. Faça isso de vez em quando: backup que ninguém sabe se prestou só se descobre no dia em que ele é necessário — que é o único dia em que não dá para descobrir. |  |
| 379 | TextBlock | Para RESTAURAR um backup, fale com o suporte. A restauração substitui a base inteira, só entra numa base vazia e é feita junto com quem acompanha a operação — por isso ela não tem botão aqui. |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/CustoTransacaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/CustoTransacaoView.xaml) · 311 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 21 | TextBlock | Taxas e impostos |  |
| 22 | TextBlock | Quanto a maquininha e o fisco custaram. O Financeiro cadastra as taxas; aqui se vê o que elas comeram — e se a taxa que a clínica de fato paga bate com a que foi contratada. |  |
| 27 | TextBlock | Meses |  |
| 29 | ComboBox |  |  |
| 31 | Button | Exportar | {Binding ExportarCommand} |
| 33 | Button | Atualizar | {Binding CarregarCommand} |
| 56 | TextBlock | {Binding Mensagem} |  |
| 76 | TextBlock |  |  |
| 77 | TextBlock | ENTROU (BRUTO) |  |
| 80 | TextBlock | {Binding Bruto} |  |
| 81 | TextBlock | {Binding Periodo} |  |
| 90 | TextBlock |  |  |
| 91 | TextBlock | MAQUININHA |  |
| 94 | TextBlock | {Binding Taxa} |  |
| 97 | TextBlock | {Binding PercentualTaxa} |  |
| 107 | TextBlock |  |  |
| 108 | TextBlock | IMPOSTO |  |
| 111 | TextBlock | {Binding Imposto} |  |
| 112 | TextBlock | {Binding PercentualImposto} |  |
| 122 | TextBlock |  |  |
| 123 | TextBlock | SOBROU (LÍQUIDO) |  |
| 126 | TextBlock | {Binding Liquido} |  |
| 127 | TextBlock |  |  |
| 140 | TextBlock | Evolução mensal |  |
| 150 | TextBlock | DEDUÇÕES (% DO FATURAMENTO) |  |
| 159 | TextBlock | DEDUÇÕES (R$) |  |
| 167 | TextBlock | {Binding MaisCara} |  |
| 176 | TextBlock | Por origem do recebimento |  |
| 178 | TextBlock | {Binding Resumo} |  |
| 180 | TextBlock | A taxa EFETIVA é o que foi descontado sobre o que passou; a de tabela é a contratada. Elas divergem quando a clínica parcela mais do que imagina — e é essa diferença que se leva à renegociação. |  |
| 185 | TextBlock | Nenhum recebimento no período. |  |
| 186 | TextBlock |  |  |
| 203 | TextBlock | {Binding Rotulo} |  |
| 208 | TextBlock | {Binding ValorRotulo} |  |
| 219 | TextBlock | {Binding Comparacao} |  |
| 221 | TextBlock |  |  |
| 247 | TextBlock | Mês a mês |  |
| 259 | TextBlock | Mês |  |
| 260 | TextBlock | Bruto |  |
| 262 | TextBlock | Maquininha |  |
| 264 | TextBlock | Imposto |  |
| 266 | TextBlock | Líquido |  |
| 268 | TextBlock | Deduções |  |
| 287 | TextBlock | {Binding Mes} |  |
| 289 | TextBlock | {Binding Bruto} |  |
| 291 | TextBlock | {Binding Taxa} |  |
| 294 | TextBlock | {Binding Imposto} |  |
| 297 | TextBlock | {Binding Liquido} |  |
| 299 | TextBlock | {Binding Percentual} |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/DocumentosEmitidosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/DocumentosEmitidosView.xaml) · 330 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 40 | TextBlock | Documentos emitidos |  |
| 41 | TextBlock | Tudo o que saiu da clínica no período — numerado por ano, com código de conferência. Para EMITIR um papel, use a Recepção ou o Consultório: aqui a tela só lê. |  |
| 43 | TextBlock | {Binding Resumo} |  |
| 48 | TextBlock | Período |  |
| 50 | ComboBox |  |  |
| 52 | Button | Atualizar | {Binding CarregarCommand} |
| 63 | TextBlock | Conferir um papel pelo código |  |
| 66 | Button | Conferir | {Binding ConferirCommand} |
| 70 | TextBox | {Binding Codigo, UpdateSourceTrigger=PropertyChanged} |  |
| 78 | TextBlock | {Binding Conferido} |  |
| 82 | TextBlock |  |  |
| 112 | TextBlock |  |  |
| 114 | TextBlock | EMITIDOS |  |
| 118 | TextBlock | {Binding Emitidos} |  |
| 128 | TextBlock |  |  |
| 130 | TextBlock | ASSINADOS C/ e-CPF |  |
| 134 | TextBlock | {Binding Assinados} |  |
| 145 | TextBlock |  |  |
| 147 | TextBlock | CANCELADOS |  |
| 151 | TextBlock | {Binding Cancelados} |  |
| 162 | TextBlock |  |  |
| 164 | TextBlock | PUBLICADOS NO AR |  |
| 168 | TextBlock | {Binding NoAr} |  |
| 178 | TextBlock |  |  |
| 180 | TextBlock | MAIS EMITIDO |  |
| 186 | TextBlock | {Binding MaisEmitido} |  |
| 203 | TextBlock | POR QUEM ASSINA |  |
| 209 | TextBlock | Nenhum papel emitido no período. |  |
| 211 | TextBlock |  |  |
| 227 | TextBlock | {Binding TituloCanceladas} |  |
| 241 | TextBlock |  |  |
| 250 | TextBlock | {Binding Contexto} |  |
| 264 | TextBlock | Nenhum papel cancelado no período. |  |
| 266 | TextBlock |  |  |
| 278 | TextBlock | Cancelar EXIGE motivo e não apaga o documento — é a trilha que responde à auditoria. |  |
| 288 | TextBlock | O QUE SAIU NO PERÍODO |  |
| 292 | DataGrid |  |  |
| 295 | DataGrid |  |  |
| 307 | DataGrid |  |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/FaturamentoGerencialView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/FaturamentoGerencialView.xaml) · 317 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 19 | TextBlock | Faturamento |  |
| 20 | TextBlock | Visão consolidada das guias. Use as abas ao lado para baixas, glosas, não conformidades e lotes. |  |
| 25 | ComboBox |  |  |
| 27 | Button | Atualizar | {Binding CarregarCommand} |
| 31 | Button | Fechamento em PDF | {Binding GerarFechamentoCommand} |
| 63 | TextBlock |  |  |
| 64 | TextBlock | GUIAS NO PERÍODO |  |
| 67 | TextBlock | {Binding TotalGuias} |  |
| 71 | TextBlock |  |  |
| 82 | TextBlock |  |  |
| 83 | TextBlock | TAXA DE BAIXA |  |
| 86 | TextBlock | {Binding TaxaBaixaFormatada} |  |
| 99 | TextBlock |  |  |
| 110 | TextBlock |  |  |
| 111 | TextBlock | TAXA DE GLOSA |  |
| 114 | TextBlock | {Binding TaxaGlosaFormatada} |  |
| 120 | TextBlock |  |  |
| 131 | TextBlock |  |  |
| 132 | TextBlock | ATENDIMENTO → BAIXA |  |
| 135 | TextBlock | {Binding TempoMedioFormatado} |  |
| 136 | TextBlock |  |  |
| 147 | TextBlock |  |  |
| 148 | TextBlock | PENDENTES HOJE |  |
| 151 | TextBlock | {Binding PendenciasEmAberto} |  |
| 175 | TextBlock | {Binding Mensagem} |  |
| 183 | TextBlock | Pendências em aberto por atraso |  |
| 184 | TextBlock | A porcentagem é da fatia de cada faixa no total em aberto. |  |
| 213 | TextBlock | Convênio |  |
| 214 | TextBlock | Guias |  |
| 215 | TextBlock | Baixadas |  |
| 216 | TextBlock | Pendentes |  |
| 217 | TextBlock | Taxa de baixa |  |
| 218 | TextBlock | Glosa |  |
| 219 | TextBlock | Tempo médio |  |
| 241 | TextBlock | {Binding Convenio} |  |
| 244 | TextBlock | {Binding Total} |  |
| 246 | TextBlock | {Binding Baixados} |  |
| 248 | TextBlock | {Binding Pendentes} |  |
| 250 | TextBlock | {Binding TaxaBaixa} |  |
| 252 | TextBlock | {Binding TaxaGlosa} |  |
| 254 | TextBlock | {Binding TempoMedio} |  |
| 263 | TextBlock | Evolução da taxa de baixa |  |
| 289 | TextBlock | {Binding Rotulo} |  |
| 291 | TextBlock | {Binding TotalCodigos, StringFormat='{}{0} guias'} |  |
| 294 | TextBlock | {Binding Baixados, StringFormat='{}{0} baixadas'} |  |
| 297 | TextBlock | {Binding TaxaBaixa, StringFormat='{}{0:0.#}%'} |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/GuardaProntuarioView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/GuardaProntuarioView.xaml) · 222 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 20 | TextBlock | Guarda do prontuário |  |
| 21 | TextBlock | O que está guardado, e até quando. A Lei 13.787/2018 (art. 6º) exige guardar o prontuário por, no mínimo, 20 anos a partir do ÚLTIMO registro — e é esta tela que responde a quem auditar a clínica. |  |
| 43 | TextBlock | {Binding Mensagem} |  |
| 52 | TextBlock | A clínica inteira |  |
| 53 | TextBlock | A conta varre paciente a paciente e pode demorar numa base grande — por isso só roda quando você pede. É leitura de auditoria, feita algumas vezes por ano. |  |
| 56 | Button | Calcular a guarda | {Binding CarregarClinicaCommand} |
| 64 | TextBlock | Prazo legal |  |
| 65 | TextBlock | {Binding AnosDeGuarda} |  |
| 69 | TextBlock | Pacientes |  |
| 70 | TextBlock | {Binding Pacientes} |  |
| 74 | TextBlock | Com prontuário |  |
| 75 | TextBlock | {Binding ComRegistro} |  |
| 79 | TextBlock | Registros guardados |  |
| 80 | TextBlock | {Binding RegistrosGuardados} |  |
| 84 | TextBlock | Já cumpriram o prazo |  |
| 85 | TextBlock | {Binding Elegiveis} |  |
| 89 | TextBlock | Registro mais antigo |  |
| 90 | TextBlock | {Binding MaisAntigo} |  |
| 95 | TextBlock | {Binding ResumoClinica} |  |
| 103 | TextBlock | Um paciente |  |
| 104 | TextBlock | Quando vence a guarda do prontuário de uma pessoa, e o que está guardado nele. |  |
| 107 | TextBox | {Binding Paciente.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 125 | TextBlock | {Binding NomePaciente} |  |
| 127 | TextBlock | {Binding SituacaoPaciente} |  |
| 129 | TextBlock | {Binding DetalhePaciente} |  |
| 133 | Button | Exportar o prontuário desta pessoa | {Binding ExportarPacienteCommand} |
| 143 | TextBlock | Quem abriu este prontuário |  |
| 145 | TextBlock | {Binding ResumoAcessos} |  |
| 163 | TextBlock | {Binding Quando} |  |
| 165 | TextBlock | {Binding Operador} |  |
| 168 | TextBlock | {Binding Porta} |  |
| 183 | TextBlock | Exportar o prontuário |  |
| 184 | TextBlock | Arquivos CSV que qualquer sistema importa — é o que garante que a clínica não fique refém do fornecedor. Saem as sessões, as correções feitas nelas, as avaliações, as medidas, os documentos e a lista de anexos. Os ARQUIVOS anexados em si saem no backup completo, e o LEIA-ME da exportação explica isso. |  |
| 187 | Button | Exportar a clínica inteira | {Binding ExportarTudoCommand} |
| 193 | TextBlock | A exportação leva dado de saúde de todos os pacientes em arquivos que qualquer pessoa abre. Grave em local controlado, entregue só a quem tem direito de receber, e lembre que quem recebe assume junto a obrigação de guardar por 20 anos. |  |
| 200 | TextBlock | Não há botão para ELIMINAR prontuário vencido, e isso é decisão: os 20 anos são um piso de guarda, não um agendamento de destruição. Cumprido o prazo, o prontuário fica elegível — eliminar é decisão da clínica com a comissão de revisão prevista no art. 7º da Lei 13.787/2018, e não tem volta. |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/ImportacaoPacientesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/ImportacaoPacientesView.xaml) · 323 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 27 | TextBlock | Importar pacientes |  |
| 28 | TextBlock | O sistema anterior entra aqui em três passos. Nada é gravado antes da prévia: quem já está cadastrado é completado (só os campos vazios), e o mesmo arquivo importado duas vezes não duplica nada — nem ficha, nem prontuário, nem horário. |  |
| 46 | TextBlock | {Binding Mensagem} |  |
| 65 | TextBlock | {Binding ConferenciaTitulo} |  |
| 69 | TextBlock | {Binding} |  |
| 79 | TextBlock | 1 · O ARQUIVO |  |
| 80 | TextBlock | O Smart Clinic entrega um ZIP com a carteira, o prontuário, a agenda e os cadastros: escolha o ZIP inteiro e tudo o que é dado de paciente entra (fichas, prontuário em texto, agenda futura, e o que não tem campo vai para as observações). Um CSV avulso de pacientes também serve, de qualquer sistema — aí o passo 2 pergunta o que cada coluna significa. |  |
| 83 | Button | Escolher o pacote (ZIP) do Smart Clinic… | {Binding EscolherPacoteCommand} |
| 86 | Button | Ou um CSV de pacientes… | {Binding EscolherArquivoCommand} |
| 93 | Button | Ou o ZIP de arquivos (receitas, laudos)… | {Binding EscolherAnexosCommand} |
| 98 | TextBlock | {Binding ArquivoNome} |  |
| 102 | TextBlock | {Binding ArquivoInfo} |  |
| 110 | TextBlock | ZIP de arquivos reconhecido: cada PDF acha a ficha do paciente pelo id do sistema anterior (gravado na importação da carteira) e entra como ARQUIVO DA FICHA, em "Exames e anexos", com a data do documento. Não há coluna nem convênio a decidir. |  |
| 112 | Button | Gerar a prévia | {Binding GerarPreviaCommand} |
| 134 | TextBlock | 2 · O QUE CADA COLUNA SIGNIFICA |  |
| 137 | TextBlock | Formato do Smart Clinic reconhecido: nome, CPF, celular, nascimento, sexo, endereço em partes, convênio, carteirinha e o ID de lá. O que precisa da sua decisão é só o convênio, abaixo. |  |
| 140 | TextBlock | A sugestão já está marcada pelo nome das colunas — confira. Só o nome é obrigatório. |  |
| 154 | TextBlock | {Binding Rotulo} |  |
| 156 | ComboBox |  |  |
| 160 | TextBlock | {Binding Dica} |  |
| 173 | TextBlock | CONVÊNIOS DO ARQUIVO → CONVÊNIOS DAQUI |  |
| 174 | TextBlock | Aponte o que você sabe (“Unimed” pode ser Padrão ou Intercâmbio, e a diferença é regra de faturamento). O que ficar em “definir depois” entra como convênio a definir: o sistema acusa em vermelho no próximo agendamento ou atendimento, e a escolha é feita na ficha, com o paciente na frente. A lista segue a coluna de convênio escolhida acima. |  |
| 190 | TextBlock | {Binding Texto} |  |
| 194 | TextBlock | {Binding LinhasTexto} |  |
| 197 | ComboBox |  |  |
| 208 | Button | Gerar a prévia | {Binding GerarPreviaCommand} |
| 218 | TextBlock | 3 · O QUE VAI ACONTECER |  |
| 219 | TextBlock | {Binding ResumoPrevia} |  |
| 225 | TextBlock | {Binding} |  |
| 231 | TextBlock | {Binding AvisosGerais} |  |
| 243 | TextBlock | Linha |  |
| 244 | TextBlock | Nome |  |
| 245 | TextBlock | CPF |  |
| 246 | TextBlock | Destino |  |
| 247 | TextBlock | Detalhe |  |
| 266 | TextBlock | {Binding Numero} |  |
| 268 | TextBlock | {Binding Nome} |  |
| 271 | TextBlock | {Binding CpfFormatado} |  |
| 277 | TextBlock | {Binding DestinoRotulo} |  |
| 281 | TextBlock | {Binding DestinoRotulo} |  |
| 285 | TextBlock | {Binding DestinoRotulo} |  |
| 289 | TextBlock | {Binding DestinoRotulo} |  |
| 293 | TextBlock | {Binding Detalhe} |  |
| 294 | TextBlock | {Binding AvisosTexto} |  |
| 307 | Button | {Binding RotuloImportar} | {Binding ImportarCommand} |
| 310 | TextBlock | Linhas “Não entra” ficam de fora; corrija o arquivo ou o mapeamento e gere a prévia de novo. |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/IndicadoresView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/IndicadoresView.xaml) · 331 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 19 | TextBlock | Indicadores |  |
| 20 | TextBlock | {Binding IntervaloFormatado, StringFormat='Ocupação, faltas e produtividade — {0}'} |  |
| 25 | ComboBox |  |  |
| 27 | Button | Atualizar | {Binding CarregarCommand} |
| 30 | Button | Exportar | {Binding ExportarCommand} |
| 51 | TextBlock |  |  |
| 52 | TextBlock | ATENDIDOS |  |
| 55 | TextBlock | {Binding Atendidos} |  |
| 58 | TextBlock | {Binding VariacaoAtendidos} |  |
| 59 | TextBlock |  |  |
| 82 | TextBlock |  |  |
| 83 | TextBlock | FALTAS (NO-SHOW) |  |
| 86 | TextBlock | {Binding NoShowFormatado} |  |
| 89 | TextBlock | {Binding VariacaoNoShow} |  |
| 90 | TextBlock |  |  |
| 113 | TextBlock |  |  |
| 114 | TextBlock | OCUPAÇÃO |  |
| 117 | TextBlock | {Binding OcupacaoFormatada} |  |
| 126 | TextBlock |  |  |
| 127 | TextBlock | PACIENTES ATENDIDOS |  |
| 130 | TextBlock | {Binding PacientesDistintos} |  |
| 138 | TextBlock |  |  |
| 139 | TextBlock | NPS |  |
| 142 | TextBlock | {Binding NpsFormatado} |  |
| 144 | TextBlock | {Binding NpsDetalhe} |  |
| 150 | TextBlock | {Binding JornadaFormatada} |  |
| 172 | TextBlock | {Binding Mensagem} |  |
| 193 | TextBlock | Profissional |  |
| 194 | TextBlock | Atendidos |  |
| 195 | TextBlock | Faltas |  |
| 196 | TextBlock | No-show |  |
| 197 | TextBlock | Horas |  |
| 198 | TextBlock | Evoluções |  |
| 201 | TextBlock | Prontuário |  |
| 202 | TextBlock | Queda média da dor |  |
| 225 | TextBlock | {Binding Nome} |  |
| 228 | TextBlock | {Binding Atendidos} |  |
| 230 | TextBlock | {Binding Faltas} |  |
| 232 | TextBlock | {Binding TaxaNoShow, StringFormat='{}{0:0.#}%'} |  |
| 235 | TextBlock | {Binding HorasOcupadas, StringFormat='{}{0:0.#} h'} |  |
| 238 | TextBlock | {Binding Evolucoes} |  |
| 242 | TextBlock | {Binding CompletudeProntuario, StringFormat='{}{0:0.#}%', TargetNullValue='—'} |  |
| 245 | TextBlock | {Binding MelhoraMediaEva, StringFormat='{}{0:0.#} pontos', TargetNullValue='—'} |  |
| 256 | TextBlock | Ocupação por profissional |  |
| 261 | TextBlock | A barra cheia é a MAIOR ocupação do período, não 100%: numa clínica que nunca passa de 60% todas as barras ficariam curtas e iguais, e a comparação — que é a pergunta — sumiria. O número ao lado diz o valor absoluto. |  |
| 266 | TextBlock | Evolução mensal |  |
| 276 | TextBlock | ATENDIMENTOS |  |
| 282 | TextBlock | NO-SHOW (%) |  |
| 303 | TextBlock | {Binding Rotulo} |  |
| 305 | TextBlock | {Binding Atendidos, StringFormat='{}{0} atendidos'} |  |
| 308 | TextBlock | {Binding TaxaNoShow, StringFormat='no-show {0:0.#}%'} |  |
| 311 | TextBlock | {Binding OcupacaoPercentual, StringFormat='ocupação {0:0.#}%', TargetNullValue='ocupação —'} |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/MetasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/MetasView.xaml) · 187 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 18 | TextBlock | Metas |  |
| 19 | TextBlock | {Binding Resumo} |  |
| 23 | Button | ◀ | {Binding AnoAnteriorCommand} |
| 28 | TextBlock | {Binding Ano} |  |
| 31 | Button | ▶ | {Binding ProximoAnoCommand} |
| 33 | Button | Nova meta | {Binding NovaMetaCommand} |
| 39 | TextBlock | A meta declara o alvo: o painel deixa de comparar só com o mês anterior (“melhorou?”) e passa a medir contra a decisão (“chegamos aonde dissemos?”), avisando quando o ritmo do mês não alcança. |  |
| 62 | TextBlock | {Binding Mensagem} |  |
| 69 | TextBlock | Não foi possível apurar o realizado do mês corrente. A coluna “realizado” está vazia por falha de leitura, e não por falta de movimento. |  |
| 90 | TextBlock | Mês |  |
| 91 | TextBlock | Indicador |  |
| 92 | TextBlock | De quem |  |
| 93 | TextBlock | Alvo |  |
| 94 | TextBlock | Realizado |  |
| 95 | TextBlock | Situação |  |
| 96 | TextBlock |  |  |
| 117 | TextBlock | {Binding Periodo} |  |
| 120 | TextBlock | {Binding Rotulo} |  |
| 123 | TextBlock | {Binding Dono} |  |
| 126 | TextBlock | {Binding Alvo} |  |
| 129 | TextBlock | {Binding Realizado} |  |
| 134 | TextBlock | {Binding Situacao} |  |
| 140 | TextBlock | Batida |  |
| 146 | TextBlock | Em risco |  |
| 152 | Button | Editar | {Binding DataContext.EditarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 156 | Button | Excluir | {Binding DataContext.ExcluirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Gerente/Views/ModelosTermoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/ModelosTermoWindow.xaml) · 241 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Termos do procedimento |  |
| 10 | Window |  |  |
| 17 | TextBlock | Termos do procedimento |  |
| 19 | TextBlock | Escreva o texto que o paciente vai ler e assinar, e as declarações que ele responde (jejum, medicações, alergias). Depois diga qual procedimento passa a exigi-lo. Por padrão o termo vale a partir da assinatura; a validade por sessão é escolha de cada amarração. |  |
| 36 | TextBlock | {Binding Mensagem} |  |
| 40 | Button | Fechar |  |
| 43 | Button | Salvar termo | {Binding SalvarCommand} |
| 59 | Button | Novo termo | {Binding NovoCommand} |
| 68 | Button | Criar os termos do BSV… | {Binding CriarModelosDoBsvCommand} |
| 82 | TextBlock | {Binding Nome} |  |
| 84 | TextBlock | {Binding Resumo} |  |
| 104 | TextBlock | O TEXTO |  |
| 114 | TextBlock | Nome (só a clínica vê) |  |
| 116 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 122 | TextBlock | Título impresso |  |
| 124 | TextBox | {Binding TituloImpresso, UpdateSourceTrigger=PropertyChanged} |  |
| 130 | TextBlock | Texto do termo |  |
| 132 | TextBox | {Binding Corpo, UpdateSourceTrigger=PropertyChanged} |  |
| 137 | TextBlock | O conteúdo é responsabilidade técnica da clínica. O sistema guarda, imprime e prova quem assinou — ele não redige nem confere o texto. |  |
| 145 | TextBlock | DECLARAÇÕES QUE O PACIENTE RESPONDE |  |
| 147 | TextBlock | Cada linha vira um Sim/Não na tela da coleta e sai impressa com a resposta. Responder NÃO não impede o procedimento — acende alerta vermelho para quem vai fazê-lo. |  |
| 151 | Button | Acrescentar | {Binding AcrescentarDeclaracaoCommand} |
| 156 | TextBox | {Binding NovaDeclaracao, UpdateSourceTrigger=PropertyChanged} |  |
| 165 | Button | Remover | {Binding DataContext.RemoverDeclaracaoCommand,
                                                            RelativeSource={RelativeSource AncestorType=Window}} |
| 171 | TextBox | {Binding Texto, UpdateSourceTrigger=PropertyChanged} |  |
| 182 | TextBlock | QUAL PROCEDIMENTO EXIGE ESTE TERMO |  |
| 186 | Button | Passar a exigir | {Binding ExigirCommand} |
| 191 | ComboBox |  |  |
| 197 | CheckBox | Pedir a cada sessão (não vale de um dia para o outro) |  |
| 199 | TextBlock | Desmarcado (o normal), o termo vale a partir da assinatura — dá para colher quando o paciente aparece, semanas antes. Marque só para o termo curto que pergunta o JEJUM: essa declaração é sobre o dia e não se herda. |  |
| 216 | TextBlock | {Binding Modalidade} |  |
| 219 | TextBlock | {Binding Termo} |  |
| 222 | TextBlock | {Binding Situacao} |  |
| 225 | Button | {Binding AcaoRotulo} | {Binding DataContext.AlternarExigenciaCommand,
                                                            RelativeSource={RelativeSource AncestorType=Window}} |

## Clinica · src/Clinica.Modulo.Gerente/Views/OrigensView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/OrigensView.xaml) · 162 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 15 | TextBlock | De onde vêm os pacientes |  |
| 19 | TextBlock | A resposta de “como conheceu a clínica?”, somada. Estreia = primeiro atendimento no período escolhido; quem foi cadastrado e nunca veio conta na base, não nas estreias. |  |
| 21 | TextBlock | {Binding Resumo} |  |
| 26 | TextBlock | Estreias em |  |
| 28 | ComboBox |  |  |
| 30 | Button | Atualizar | {Binding CarregarCommand} |
| 42 | TextBlock | QUEM MAIS INDICA (10 maiores) |  |
| 55 | TextBlock | {Binding Nome} |  |
| 57 | TextBlock | {Binding Quantos} |  |
| 67 | TextBlock | Nenhuma indicação com o nome de quem indicou — o campo fica na ficha do paciente, quando a origem é “Indicação”. |  |
| 69 | TextBlock |  |  |
| 97 | TextBlock | ORIGEM |  |
| 98 | TextBlock | NA BASE |  |
| 100 | TextBlock | % DA BASE |  |
| 102 | TextBlock | ESTREARAM NO PERÍODO |  |
| 119 | TextBlock | {Binding Rotulo} |  |
| 128 | TextBlock | colher no cadastro |  |
| 132 | TextBlock | {Binding NaBase} |  |
| 135 | TextBlock | {Binding Fracao} |  |
| 139 | TextBlock | {Binding Estreias} |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/PainelDirecaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/PainelDirecaoView.xaml) · 378 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 25 | TextBlock | {Binding Saudacao} |  |
| 26 | TextBlock | {Binding DataFormatada} |  |
| 30 | Button | Atualizar | {Binding CarregarCommand} |
| 53 | TextBlock | {Binding Mensagem} |  |
| 60 | TextBlock | {Binding NaoVerificados} |  |
| 75 | TextBlock |  |  |
| 76 | TextBlock | RECEBIDO BRUTO NO MÊS |  |
| 79 | TextBlock | {Binding EntradasMes} |  |
| 84 | TextBlock | {Binding VariacaoEntradas} |  |
| 85 | TextBlock |  |  |
| 110 | TextBlock |  |  |
| 111 | TextBlock | SAIU NO MÊS |  |
| 114 | TextBlock | {Binding SaidasMes} |  |
| 116 | TextBlock | pagamentos registrados |  |
| 125 | TextBlock |  |  |
| 126 | TextBlock | RESULTADO LÍQUIDO DO MÊS |  |
| 129 | TextBlock | {Binding SaldoMes} |  |
| 130 | TextBlock |  |  |
| 143 | TextBlock | {Binding DeducoesMes} |  |
| 161 | TextBlock |  |  |
| 162 | TextBlock | CONTAS VENCIDAS |  |
| 165 | TextBlock | {Binding ContasVencidas} |  |
| 166 | TextBlock | {Binding ContasVencidasDetalhe} |  |
| 176 | TextBlock |  |  |
| 177 | TextBlock | SEM DEPÓSITO |  |
| 180 | TextBlock | {Binding DepositoAtrasado} |  |
| 181 | TextBlock | {Binding DepositoAtrasadoDetalhe} |  |
| 191 | TextBlock |  |  |
| 192 | TextBlock | GUIAS NO PRAZO VENCIDO |  |
| 195 | TextBlock | {Binding PendenciasFaturamento} |  |
| 197 | TextBlock | {Binding PendenciasDetalhe} |  |
| 207 | TextBlock |  |  |
| 208 | TextBlock | A RECEBER (PREVISTO) |  |
| 211 | TextBlock | {Binding AReceberPrevisto} |  |
| 213 | TextBlock | {Binding AReceberDetalhe} |  |
| 231 | TextBlock | Metas do mês |  |
| 232 | TextBlock | Onde a clínica disse que queria chegar, com o realizado ao lado. Sem meta definida, o painel só compara com o mês anterior — que responde “melhorou?”, não “chegamos?”. |  |
| 249 | TextBlock | {Binding Rotulo} |  |
| 254 | TextBlock |  |  |
| 275 | TextBlock | {Binding Situacao} |  |
| 279 | TextBlock |  |  |
| 302 | TextBlock | O que exige ação hoje |  |
| 303 | TextBlock | Em ordem de peso: primeiro o que já é prejuízo, depois o que ainda dá para evitar. Cada linha leva à tela do assunto. |  |
| 308 | TextBlock | Nada exige ação hoje: nenhuma conta vencida, nenhum depósito atrasado, a gaveta conferida e nenhuma guia fora do prazo de decisão. |  |
| 346 | TextBlock | {Binding Titulo} |  |
| 348 | TextBlock | {Binding Detalhe} |  |
| 357 | Button | {Binding DestinoRotulo} | {Binding IrCommand} |

## Clinica · src/Clinica.Modulo.Gerente/Views/PrecosConvenioView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/PrecosConvenioView.xaml) · 199 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 16 | TextBlock | Tabela de preço por convênio |  |
| 18 | TextBlock | Quanto cada operadora paga por tipo de guia. Cadastrado aqui, na direção — quem negocia tabela — e usado na Conciliação do Financeiro, que passa a PROPOR o valor em vez de pedir que ele seja digitado. O preço do PARTICULAR é a aba ao lado. |  |
| 41 | TextBlock | {Binding Mensagem} |  |
| 62 | TextBlock | Convênio |  |
| 63 | ComboBox |  |  |
| 67 | CheckBox | Só o que vale hoje |  |
| 71 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 84 | TextBlock | Preços cadastrados |  |
| 85 | TextBlock | {Binding Resumo} |  |
| 93 | Button | Novo preço | {Binding NovoPrecoCommand} |
| 96 | Button | Atualizar | {Binding CarregarCommand} |
| 103 | TextBlock | {Binding VazioDescricao} |  |
| 105 | TextBlock |  |  |
| 132 | TextBlock | {Binding Convenio} |  |
| 134 | TextBlock |  |  |
| 141 | TextBlock | {Binding Valor} |  |
| 160 | TextBlock |  |  |
| 161 | TextBlock |  |  |
| 176 | Button | Editar | {Binding DataContext.EditarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 183 | Button | Excluir | {Binding DataContext.ExcluirCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Gerente/Views/RentabilidadeConvenioView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/RentabilidadeConvenioView.xaml) · 270 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 21 | TextBlock | Rentabilidade por convênio |  |
| 23 | TextBlock | Qual operadora vale a pena. O faturamento sabe quantas guias saíram e o financeiro sabe quanto entrou — é o encontro dos dois que revela quem paga pouco, quem paga tarde e quem glosa muito. |  |
| 28 | TextBlock | Meses |  |
| 30 | ComboBox |  |  |
| 32 | Button | Exportar | {Binding ExportarCommand} |
| 34 | Button | Atualizar | {Binding CarregarCommand} |
| 57 | TextBlock | {Binding Mensagem} |  |
| 79 | TextBlock |  |  |
| 80 | TextBlock | GUIAS |  |
| 83 | TextBlock | {Binding Guias} |  |
| 84 | TextBlock | {Binding Periodo} |  |
| 93 | TextBlock |  |  |
| 94 | TextBlock | RECEBIDO (BRUTO) |  |
| 97 | TextBlock | {Binding Bruto} |  |
| 106 | TextBlock |  |  |
| 107 | TextBlock | RETIDO NA FONTE |  |
| 110 | TextBlock | {Binding Retido} |  |
| 111 | TextBlock | {Binding PercentualRetido} |  |
| 121 | TextBlock |  |  |
| 122 | TextBlock | SOBROU (LÍQUIDO) |  |
| 125 | TextBlock | {Binding Liquido} |  |
| 134 | TextBlock |  |  |
| 135 | TextBlock | GLOSA |  |
| 138 | TextBlock | {Binding TaxaGlosa} |  |
| 139 | TextBlock | das guias efetivadas |  |
| 148 | TextBlock | {Binding Resumo} |  |
| 152 | TextBlock | {Binding MelhorEPior} |  |
| 154 | TextBlock | O LÍQUIDO POR GUIA é o único número comparável entre operadoras: faturar mais não quer dizer nada se foi preciso o triplo de atendimentos. O PRAZO conta da baixa da guia até o dinheiro cair — quem paga 10% a mais em 90 dias pode ser pior que quem paga menos em 30. |  |
| 169 | TextBlock | Convênio |  |
| 170 | TextBlock | Guias |  |
| 172 | TextBlock | Bruto |  |
| 174 | TextBlock | Retido |  |
| 176 | TextBlock | Líquido |  |
| 178 | TextBlock | Por guia |  |
| 180 | TextBlock | Prazo |  |
| 184 | TextBlock | Nenhuma guia no período. |  |
| 185 | TextBlock |  |  |
| 215 | TextBlock | {Binding Rotulo} |  |
| 218 | TextBlock | {Binding Guias} |  |
| 220 | TextBlock | {Binding Bruto} |  |
| 223 | TextBlock | {Binding Retido} |  |
| 226 | TextBlock | {Binding Liquido} |  |
| 230 | TextBlock | {Binding PorGuia} |  |
| 233 | TextBlock | {Binding Prazo} |  |
| 243 | TextBlock | {Binding Pendencias} |  |
| 245 | TextBlock |  |  |

## Clinica · src/Clinica.Modulo.Gerente/Views/RetencaoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Gerente/Views/RetencaoView.xaml) · 183 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 17 | TextBlock | Quem parou de vir |  |
| 18 | TextBlock | {Binding Resumo} |  |
| 22 | TextBlock | Sem vir há mais de |  |
| 24 | ComboBox |  |  |
| 26 | TextBlock | dias |  |
| 28 | Button | Exportar CSV | {Binding ExportarCommand} |
| 31 | Button | Atualizar | {Binding CarregarCommand} |
| 38 | TextBlock | O recall dispara mensagens por regra de tempo; esta é a LISTA, para olhar caso a caso e decidir quem vale um telefonema de verdade — o paciente de tratamento longo que some vale dez recalls disparados no vazio. Quem já tem horário marcado à frente não aparece: ele não está perdido, só ainda não veio. |  |
| 63 | TextBlock | Paciente |  |
| 64 | TextBox | {Binding FiltroNomeSumido, UpdateSourceTrigger=PropertyChanged} |  |
| 69 | CheckBox | Só quem era frequente |  |
| 73 | CheckBox | Só com pacote em aberto |  |
| 77 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 103 | TextBlock | {Binding Mensagem} |  |
| 125 | TextBlock | {Binding Paciente} |  |
| 130 | TextBlock | Era de tratamento |  |
| 135 | TextBlock | Pacote pago em aberto |  |
| 138 | TextBlock | {Binding Detalhe} |  |
| 148 | TextBlock | {Binding ImpedimentoChamada} |  |
| 156 | TextBlock | {Binding Faixa} |  |
| 160 | Button | Chamar de volta | {Binding DataContext.ChamarCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/AgendamentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/AgendamentoWindow.xaml) · 225 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Horário da agenda |  |
| 9 | Window |  |  |
| 18 | TextBlock | {Binding Titulo} |  |
| 21 | TextBlock | Paciente |  |
| 22 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 36 | TextBlock | {Binding Seletor.Erro} |  |
| 51 | TextBlock | Data |  |
| 56 | TextBlock | Hora |  |
| 57 | TextBox | {Binding Hora, UpdateSourceTrigger=PropertyChanged} |  |
| 61 | TextBlock | Duração (min) |  |
| 62 | TextBox | {Binding Duracao, UpdateSourceTrigger=PropertyChanged} |  |
| 63 | TextBlock | Vazio = padrão do profissional. |  |
| 79 | TextBlock | Profissional |  |
| 80 | ComboBox |  |  |
| 87 | TextBlock | Sem profissional este horário não aparece na agenda de quem atende nem entra no repasse. |  |
| 96 | TextBlock | Sala |  |
| 97 | ComboBox |  |  |
| 111 | TextBlock | Modalidade |  |
| 112 | ComboBox |  |  |
| 119 | TextBlock | Especialidade |  |
| 120 | ComboBox |  |  |
| 125 | TextBlock | Observações |  |
| 126 | TextBox | {Binding Observacoes} |  |
| 137 | TextBlock | {Binding CabecalhoDosAvisos} |  |
| 144 | TextBlock | {Binding Texto} |  |
| 145 | TextBlock |  |  |
| 177 | TextBlock | Atendimento repetido? |  |
| 178 | TextBlock | {Binding AvisoJaLancado} |  |
| 189 | TextBlock | Antes de marcar |  |
| 193 | TextBlock | {Binding} |  |
| 204 | CheckBox | Registrar como encaixe (atender por cima de um horário que já tem paciente) |  |
| 210 | TextBlock | {Binding Mensagem} |  |
| 214 | Button | Cancelar |  |
| 216 | Button | Assumir encaixe | {Binding AssumirEncaixeCommand} |
| 220 | Button | Salvar horário | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/AutorizacaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/AutorizacaoWindow.xaml) · 116 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Autorização de sessões |  |
| 17 | Window |  |  |
| 25 | TextBlock | {Binding Titulo} |  |
| 26 | TextBlock | O que o convênio liberou. É este registro que faz o balcão ser avisado antes de a cota estourar — depois vira glosa 2006, com a sessão já prestada. |  |
| 37 | TextBlock | Senha / número |  |
| 38 | TextBox | {Binding Numero, UpdateSourceTrigger=PropertyChanged} |  |
| 43 | TextBlock | Convênio |  |
| 44 | ComboBox |  |  |
| 58 | TextBlock | Emitida em |  |
| 63 | TextBlock | Válida até |  |
| 76 | TextBlock | Sessões autorizadas |  |
| 77 | TextBox | {Binding QuantidadeAutorizada} |  |
| 81 | TextBlock | Já usadas antes do sistema |  |
| 82 | TextBox | {Binding QuantidadeUtilizadaManual} |  |
| 86 | TextBlock | Deixe zero: o sistema conta sozinho pelos atendimentos. |  |
| 92 | TextBlock | Observações |  |
| 93 | TextBox | {Binding Observacoes} |  |
| 95 | CheckBox | Encerrada (não conta mais como vigente) |  |
| 100 | TextBlock | {Binding Mensagem} |  |
| 105 | Button | Cancelar |  |
| 108 | Button | Salvar autorização | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/BloqueioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/BloqueioWindow.xaml) · 93 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Fechar a agenda |  |
| 9 | Window |  |  |
| 15 | TextBlock | Fechar a agenda |  |
| 16 | TextBlock | Férias, feriado, congresso, folga, sala em manutenção. A agenda passa a RECUSAR marcação no período — o encaixe continua furando, como no resto do sistema, e fica registrado como encaixe. |  |
| 19 | TextBlock | Motivo |  |
| 20 | TextBox | {Binding Motivo, UpdateSourceTrigger=PropertyChanged} |  |
| 21 | TextBlock | Obrigatório: em dezembro ninguém lembra por que aquela terça está fechada, e o horário se perde por medo de mexer. |  |
| 25 | CheckBox | Dia inteiro |  |
| 34 | TextBlock | De |  |
| 38 | TextBlock | Hora |  |
| 39 | TextBox | {Binding InicioHora, UpdateSourceTrigger=PropertyChanged} |  |
| 51 | TextBlock | Até |  |
| 55 | TextBlock | Hora |  |
| 56 | TextBox | {Binding FimHora, UpdateSourceTrigger=PropertyChanged} |  |
| 61 | TextBlock | Profissional (em branco = a clínica inteira) |  |
| 63 | ComboBox |  |  |
| 66 | TextBlock | Sala (em branco = todas) |  |
| 67 | ComboBox |  |  |
| 70 | TextBlock | Sem profissional e sem sala, o bloqueio vale para todo mundo — é assim que se cadastra feriado. Escolher errado aqui é o erro mais caro: um feriado cadastrado como folga de uma pessoa deixa a agenda dos outros aberta. |  |
| 75 | TextBlock | Fechar a agenda NÃO desmarca quem já está marcado. Se houver sessão dentro do período, ela aparece depois de salvar — remarque com o paciente. |  |
| 81 | TextBlock | {Binding Mensagem} |  |
| 85 | Button | Cancelar |  |
| 87 | Button | Fechar a agenda | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/ConciliacaoAgendaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ConciliacaoAgendaWindow.xaml) · 176 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Conferir atendimentos |  |
| 10 | Window |  |  |
| 18 | TextBlock | Conferir atendimentos |  |
| 19 | TextBlock | Horários cuja data já passou e que ninguém resolveu. Enquanto o check-in pela agenda não é usado, é o estado normal de quem foi atendido por fora — mas o horário parado infla a ocupação, põe no “Meu dia” do médico um paciente que não vem, e fica com a evolução da sessão de verdade. |  |
| 23 | Button | Atualizar | {Binding CarregarCommand} |
| 25 | TextBlock | {Binding Periodo} |  |
| 29 | TextBlock | {Binding Resumo} |  |
| 37 | TextBlock | A conciliação não pôde ser lida. O que está abaixo não é “nada pendente”. |  |
| 44 | Button | Fechar |  |
| 60 | TextBlock | {Binding Mensagem} |  |
| 81 | TextBlock | {Binding Cabecalho} |  |
| 85 | TextBlock | {Binding Origem} |  |
| 88 | TextBlock | {Binding Parado} |  |
| 91 | TextBlock | {Binding Profissional} |  |
| 94 | TextBlock | {Binding Situacao} |  |
| 100 | TextBlock | A sessão deste dia já está lançada — lançar de novo criaria um segundo jogo de guias. Use “Já foi lançada — encerrar”: o horário aponta para a sessão e sai da agenda sem contar como falta nem cancelamento. |  |
| 106 | TextBlock | {Binding Desfecho} |  |
| 114 | Button | Foi falta | {Binding DataContext.MarcarFaltaCommand,
                                                          RelativeSource={RelativeSource AncestorType=Window}} |
| 121 | Button | Aconteceu — lançar | {Binding DataContext.LancarRetroativoCommand,
                                                          RelativeSource={RelativeSource AncestorType=Window}} |
| 130 | Button | Já foi lançada — encerrar | {Binding DataContext.SubstituirCommand,
                                                          RelativeSource={RelativeSource AncestorType=Window}} |
| 147 | TextBlock | MARCADOS COMO REALIZADOS, SEM ATENDIMENTO |  |
| 150 | TextBlock | Estes horários dizem “Finalizado” e não apontam para atendimento nenhum: não há guia, e o repasse do profissional não conta a sessão. Não há ação automática — anote o caso e confira com quem atendeu. |  |
| 161 | TextBlock | {Binding Cabecalho} |  |
| 163 | TextBlock | {Binding Profissional} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/ConfirmacoesWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ConfirmacoesWindow.xaml) · 17 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Confirmar sessões |  |
| 10 | Window |  |  |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/DetalheHorarioWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/DetalheHorarioWindow.xaml) · 172 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Horário |  |
| 8 | Window |  |  |
| 16 | TextBlock | {Binding Paciente} |  |
| 18 | TextBlock | {Binding Faixa} |  |
| 22 | TextBlock | {Binding Contexto} |  |
| 29 | TextBlock | Encaixe |  |
| 34 | TextBlock | Da lista de espera |  |
| 40 | TextBlock | Retorno do 2º código |  |
| 45 | TextBlock | {Binding StatusRotulo} |  |
| 54 | TextBlock | Telefone |  |
| 55 | TextBlock | {Binding Telefone, TargetNullValue='—'} |  |
| 60 | TextBlock | Observações |  |
| 63 | TextBlock | {Binding Observacoes} |  |
| 72 | TextBlock | Lançamento |  |
| 73 | TextBlock | {Binding Lancamento} |  |
| 80 | TextBlock | O que fazer com este horário |  |
| 95 | Button | Remarcar | AoRemarcar |
| 98 | Button | Confirmar pelo WhatsApp | AoConfirmar |
| 103 | Button | Comprovante | AoComprovante |
| 107 | Button | Quem chamar? | AoQuemChamar |
| 112 | Button | Marcar falta | AoMarcarFalta |
| 116 | Button | Cancelar horário | AoCancelar |
| 132 | TextBlock | Este horário está fechado. Reabrir devolve o paciente à agenda — escolha a data e a hora, que podem ser as mesmas. |  |
| 135 | Button | Reabrir este horário | AoRemarcar |
| 144 | Button | Cancelar o resto da série | AoCancelarSerie |
| 153 | TextBlock | Este horário já saiu do fluxo do dia — não dá para remarcar nem cancelar. Ele continua aqui porque a linha do dia precisa mostrar que ele existiu. |  |
| 155 | TextBlock |  |  |
| 167 | Button | Fechar | AoFechar |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/EstornoAtendimentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/EstornoAtendimentoWindow.xaml) · 83 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Estornar atendimento |  |
| 10 | Window |  |  |
| 17 | TextBlock | {Binding Titulo} |  |
| 19 | TextBlock | {Binding Subtitulo} |  |
| 25 | TextBlock | {Binding Impedimento} |  |
| 28 | TextBlock | {Binding Guias} |  |
| 33 | CheckBox | {Binding RotuloCaixa} |  |
| 36 | CheckBox | Devolver a sessão ao saldo do pacote |  |
| 39 | CheckBox | {Binding RotuloInsumo} |  |
| 47 | TextBlock | {Binding ConsultaRenovada} |  |
| 51 | TextBlock | Por que está estornando? |  |
| 52 | TextBox | {Binding Motivo, UpdateSourceTrigger=PropertyChanged} |  |
| 54 | TextBlock | Obrigatório: é o que fica na trilha de auditoria para quem for conferir depois. |  |
| 71 | TextBlock | {Binding Mensagem} |  |
| 75 | Button | Cancelar |  |
| 77 | Button | Estornar atendimento | {Binding EstornarCommand} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/FechamentoSessaoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/FechamentoSessaoWindow.xaml) · 193 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Concluir atendimento |  |
| 8 | Window |  |  |
| 15 | TextBlock | Concluir atendimento |  |
| 16 | TextBlock |  |  |
| 27 | Button | Cancelar |  |
| 30 | Button | Fechar |  |
| 33 | Button | Concluir sessão | {Binding ConfirmarCommand} |
| 39 | TextBlock | {Binding ResumoDoQueVaiAcontecer} |  |
| 46 | TextBlock | {Binding Mensagem} |  |
| 49 | TextBlock | Montando a proposta… |  |
| 61 | CheckBox | Debitar 1 sessão do pacote |  |
| 63 | TextBlock | {Binding PacoteRotulo} |  |
| 65 | TextBlock | É o pacote que vence primeiro — o mesmo que o sistema debitaria sozinho. Sessão comprada já foi paga: não cobre de novo no caixa. |  |
| 75 | TextBlock | Insumos consumidos |  |
| 76 | TextBlock | Sugerido a partir do que a última sessão gastou. Corrija o que for diferente; em branco não baixa nada. |  |
| 89 | TextBlock | {Binding Nome} |  |
| 90 | TextBlock | {Binding SaldoRotulo} |  |
| 94 | TextBox | {Binding Quantidade, UpdateSourceTrigger=PropertyChanged} |  |
| 109 | CheckBox | Registrar como esta sessão foi paga |  |
| 115 | TextBlock | Atendimento PARTICULAR, sem guia: o dinheiro desta sessão só entra no sistema por aqui. Sem registro, ela fica na Conciliação do Financeiro (aba Particulares) e o balcão é avisado na próxima visita. |  |
| 121 | TextBlock | Seu usuário não tem permissão para lançar no caixa — o Financeiro lança pela Conciliação. |  |
| 129 | RadioButton | Pago agora |  |
| 131 | RadioButton | Fica a receber |  |
| 142 | TextBlock | Valor |  |
| 143 | TextBox | {Binding Valor, UpdateSourceTrigger=PropertyChanged} |  |
| 145 | TextBlock | {Binding ProcedenciaDoValor} |  |
| 154 | TextBlock | Forma de pagamento |  |
| 155 | ComboBox |  |  |
| 160 | TextBlock | Vence em |  |
| 162 | TextBlock | Vira conta a receber do paciente; vencida, aparece em “Quem me deve” e no aviso do balcão. |  |
| 171 | TextBlock | Data do pagamento |  |
| 174 | TextBlock | Maquininha / adquirente |  |
| 175 | TextBox | {Binding Adquirente, UpdateSourceTrigger=PropertyChanged} |  |
| 176 | TextBlock | Bandeira |  |
| 177 | TextBox | {Binding Bandeira, UpdateSourceTrigger=PropertyChanged} |  |
| 178 | TextBlock | Parcelas no crédito |  |
| 179 | TextBox | {Binding Parcelas, UpdateSourceTrigger=PropertyChanged} |  |
| 183 | TextBlock | Categoria |  |
| 184 | ComboBox |  |  |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/HorariosProfissionalWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/HorariosProfissionalWindow.xaml) · 64 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Jornada, bloqueios e travas |  |
| 6 | Window |  |  |
| 8 | TextBlock | Jornada, bloqueios e travas |  |
| 10 | Button | Salvar configuração | {Binding SalvarCommand} |
| 11 | Button | Fechar |  |
| 12 | TextBlock | {Binding Mensagem} |  |
| 13 | TextBlock |  |  |
| 21 | TextBlock | Configure a agenda de cada profissional. A alteração vale em todos os computadores. |  |
| 22 | TextBlock | Profissional |  |
| 24 | Button | Atualizar lista | {Binding CarregarCommand} |
| 25 | ComboBox |  |  |
| 27 | TextBlock | Proteção da agenda |  |
| 28 | CheckBox | Ativar trava para este profissional |  |
| 29 | TextBlock | A trava impede dois atendimentos simultâneos deste profissional, marcação fora da jornada e nos períodos bloqueados. Encaixe também respeita a trava. |  |
| 30 | TextBlock | Jornada de atendimento |  |
| 31 | TextBlock | Dias de atendimento |  |
| 34 | CheckBox | {Binding Nome} |  |
| 36 | TextBlock | Nenhum dia selecionado = todos os dias. |  |
| 38 | TextBlock | Das (HH:mm) |  |
| 38 | TextBox | {Binding Das} |  |
| 39 | TextBlock | Até (HH:mm) |  |
| 39 | TextBox | {Binding Ate} |  |
| 41 | TextBlock | Das e Até vazios = sem limite de expediente. A duração de cada atendimento também é considerada ao verificar conflitos. |  |
| 42 | TextBlock | Consultas já marcadas continuam na agenda. Confira e remarque eventuais conflitos anteriores com os pacientes. |  |
| 45 | Button | Fechar agenda… | {Binding FecharAgendaCommand} |
| 48 | Button | Atualizar bloqueios | {Binding CarregarBloqueiosCommand} |
| 50 | TextBlock | Bloqueios e exceções |  |
| 52 | TextBlock | {Binding ResumoBloqueios} |  |
| 56 | TextBlock | {Binding} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaPainelWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaPainelWindow.xaml) · 105 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Lista de espera |  |
| 11 | Window |  |  |
| 20 | TextBlock | {Binding TituloEspera} |  |
| 22 | TextBlock | Quando um horário vaga, é aqui que está quem chamar. |  |
| 26 | Button | Adicionar à lista | {Binding NovoPedidoEsperaCommand} |
| 30 | Button | Ver a lista inteira | {Binding VerListaInteiraCommand} |
| 38 | Button | Fechar | AoFechar |
| 58 | TextBlock | {Binding Paciente} |  |
| 65 | TextBlock | Prioritário |  |
| 68 | TextBlock | {Binding Preferencias} |  |
| 72 | TextBlock | {Binding Desde} |  |
| 80 | Button | Chamar | {Binding DataContext.ChamarDaEsperaCommand,
                                                          RelativeSource={RelativeSource AncestorType=Window}} |
| 84 | Button | Sair da lista | {Binding DataContext.RemoverDaEsperaCommand,
                                                          RelativeSource={RelativeSource AncestorType=Window}} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaWindow.xaml) · 105 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Lista de espera |  |
| 9 | Window |  |  |
| 18 | TextBlock | Entrar na lista de espera |  |
| 19 | TextBlock | Quanto mais preferências você registrar, mais fácil achar quem serve para o horário que vagar. |  |
| 22 | TextBlock | Paciente |  |
| 23 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 45 | TextBlock | Profissional desejado |  |
| 46 | ComboBox |  |  |
| 48 | TextBlock | Em branco = qualquer um (chama mais rápido). |  |
| 55 | TextBlock | Modalidade |  |
| 56 | ComboBox |  |  |
| 71 | TextBlock | Turno |  |
| 72 | ComboBox |  |  |
| 76 | TextBlock | Pode vir a partir de |  |
| 81 | TextBlock | Até |  |
| 86 | CheckBox | Prioritário (entra na frente da fila) |  |
| 89 | TextBlock | Observações |  |
| 90 | TextBox | {Binding Observacoes} |  |
| 94 | TextBlock | {Binding Mensagem} |  |
| 98 | Button | Cancelar |  |
| 100 | Button | Entrar na lista | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/OrcamentoWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/OrcamentoWindow.xaml) · 184 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Orçamento |  |
| 8 | Window |  |  |
| 15 | TextBlock | Orçamento |  |
| 16 | TextBlock | {Binding Paciente} |  |
| 21 | Button | Fechar |  |
| 23 | Button | Emitir e imprimir | {Binding EmitirCommand} |
| 29 | TextBlock | {Binding Mensagem} |  |
| 36 | TextBlock | Total do orçamento |  |
| 38 | TextBlock | {Binding TotalGeral} |  |
| 56 | TextBlock | Para quem |  |
| 58 | TextBox | {Binding Destinatario, UpdateSourceTrigger=PropertyChanged} |  |
| 61 | TextBlock | Nasce com o nome do paciente. Troque se quem paga for outro (pai, empresa, plano). |  |
| 67 | TextBlock | CPF/CNPJ (opcional) |  |
| 69 | TextBox | {Binding DocumentoDestinatario, UpdateSourceTrigger=PropertyChanged} |  |
| 84 | TextBlock | Data |  |
| 89 | TextBlock | Válido até |  |
| 94 | TextBlock | Título (opcional) |  |
| 96 | TextBox | {Binding Titulo, UpdateSourceTrigger=PropertyChanged} |  |
| 107 | TextBlock | O que está sendo orçado |  |
| 109 | Button | Adicionar linha | {Binding AdicionarItemCommand} |
| 123 | TextBlock | Descrição |  |
| 124 | TextBlock | Qtd. |  |
| 125 | TextBlock | Valor unit. |  |
| 126 | TextBlock | Total |  |
| 141 | TextBox | {Binding Descricao, UpdateSourceTrigger=PropertyChanged} |  |
| 143 | TextBox | {Binding Quantidade, UpdateSourceTrigger=PropertyChanged} |  |
| 145 | TextBox | {Binding ValorUnitario, UpdateSourceTrigger=PropertyChanged} |  |
| 147 | TextBlock | {Binding Total} |  |
| 149 | Button |  | {Binding DataContext.RemoverItemCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 161 | Button | Recalcular total | {Binding RecalcularCommand} |
| 169 | TextBlock | Observações (opcional) |  |
| 171 | TextBox | {Binding Observacoes, UpdateSourceTrigger=PropertyChanged} |  |
| 176 | TextBlock | O orçamento é numerado e os valores ficam gravados na emissão — a segunda via sai idêntica à que o paciente levou. Não se apaga: cancela-se com motivo e emite-se outro. |  |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/ProfissionalWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ProfissionalWindow.xaml) · 180 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Profissional |  |
| 9 | Window |  |  |
| 18 | TextBlock | {Binding Titulo} |  |
| 20 | TextBlock | Nome |  |
| 21 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 31 | TextBlock | Nome curto (agenda) |  |
| 32 | TextBox | {Binding NomeCurto} |  |
| 36 | TextBlock | Conselho e registro |  |
| 37 | TextBox | {Binding RegistroConselho} |  |
| 39 | TextBlock | CPF |  |
| 40 | TextBox | {Binding Cpf} |  |
| 41 | TextBlock | Necessário para assinar documentos com certificado digital: o sistema compara este CPF com o que está dentro do certificado, e é assim que impede alguém de assinar no lugar de outra pessoa. Deixe em branco para quem não assina. |  |
| 56 | TextBlock | Especialidade principal |  |
| 57 | ComboBox |  |  |
| 62 | TextBlock | Duração padrão (min) |  |
| 63 | TextBox | {Binding DuracaoPadrao} |  |
| 64 | TextBlock | Vazio = padrão da clínica (30 min). |  |
| 71 | TextBlock | Atendimentos e especialidades habilitados |  |
| 72 | TextBlock | O gerente escolhe o que a recepção pode marcar para este profissional. Consultas são separadas por especialidade. |  |
| 78 | CheckBox | {Binding Rotulo} |  |
| 90 | TextBlock | Dias e horário de atendimento |  |
| 92 | CheckBox | seg |  |
| 93 | CheckBox | ter |  |
| 94 | CheckBox | qua |  |
| 95 | CheckBox | qui |  |
| 96 | CheckBox | sex |  |
| 97 | CheckBox | sáb |  |
| 98 | CheckBox | dom |  |
| 108 | TextBlock | Das |  |
| 109 | TextBox | {Binding AtendeDas} |  |
| 113 | TextBlock | Até |  |
| 114 | TextBox | {Binding AtendeAte} |  |
| 118 | TextBlock | Em branco = qualquer dia e horário, como sempre foi. Para impedir marcações fora da jornada, ative a trava em Agenda → Horários e travas. A trava também vale para encaixes. |  |
| 131 | TextBlock | Telefone |  |
| 132 | TextBox | {Binding Telefone} |  |
| 136 | TextBlock | E-mail |  |
| 137 | TextBox | {Binding Email} |  |
| 149 | TextBlock | Cor na agenda (#RRGGBB) |  |
| 150 | TextBox | {Binding Cor} |  |
| 154 | TextBlock | Ordem nas listas |  |
| 155 | TextBox | {Binding Ordem} |  |
| 159 | CheckBox | Ativo (recebe horários novos; os já marcados seguem na agenda, marcados como de inativo) |  |
| 160 | TextBlock | Desativar preserva toda a agenda passada — é o caminho certo para quem saiu da clínica. |  |
| 164 | TextBlock | Observações |  |
| 165 | TextBox | {Binding Observacoes} |  |
| 169 | TextBlock | {Binding Mensagem} |  |
| 173 | Button | Cancelar |  |
| 175 | Button | Salvar profissional | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/ProximasVagasWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/ProximasVagasWindow.xaml) · 56 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Próximas vagas |  |
| 17 | Window |  |  |
| 23 | TextBlock | Próximas vagas |  |
| 24 | TextBlock | {Binding Criterio} |  |
| 30 | Button | Fechar |  |
| 36 | TextBlock | Nenhuma vaga LIVRE com esta duração nos próximos 60 dias. Confira a jornada e os bloqueios de quem atende — ou escolha a data e a hora à mão: marcar sobre um horário que já tem paciente é permitido, e a tela avisa. |  |
| 45 | Button | {Binding Rotulo} | Vaga_Click |

## Clinica · src/Clinica.Modulo.Recepcao/Janelas/SalaWindow.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Janelas/SalaWindow.xaml) · 61 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | Window | Sala |  |
| 8 | Window |  |  |
| 17 | TextBlock | {Binding Titulo} |  |
| 19 | TextBlock | Nome |  |
| 20 | TextBox | {Binding Nome, UpdateSourceTrigger=PropertyChanged} |  |
| 30 | TextBlock | Capacidade |  |
| 31 | TextBox | {Binding Capacidade} |  |
| 32 | TextBlock | Quantos atendimentos simultâneos cabem. É isto que decide quando a agenda acusa choque de sala. |  |
| 38 | TextBlock | Ordem nas listas |  |
| 39 | TextBox | {Binding Ordem} |  |
| 43 | CheckBox | Ativa (recebe horários novos; os já marcados seguem na agenda, marcados como de inativa) |  |
| 45 | TextBlock | Observações |  |
| 46 | TextBox | {Binding Observacoes} |  |
| 50 | TextBlock | {Binding Mensagem} |  |
| 54 | Button | Cancelar |  |
| 56 | Button | Salvar sala | {Binding SalvarCommand} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/AcompanhamentoView.xaml) · 129 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 13 | TextBlock | {Binding Mensagem} |  |
| 19 | Button | Recall · pacientes sem retorno | {Binding TrocarAbaCommand} |
| 20 | Button | Novos BSV · primeira sessão | {Binding TrocarAbaCommand} |
| 21 | Button | Atualizar | {Binding RecarregarCommand} |
| 22 | Button | Configurar acompanhamento | {Binding ConfigurarCommand} |
| 24 | TextBlock | {Binding Titulo} |  |
| 28 | TextBlock | Buscar pacientes sem retornar há |  |
| 29 | TextBox | {Binding DiasRecall, UpdateSourceTrigger=PropertyChanged} |  |
| 30 | TextBlock | dias ou mais |  |
| 31 | Button | Buscar pacientes | {Binding GerarCommand} |
| 35 | Button | {Binding AAssumirTexto} | {Binding AtalhoCommand} |
| 35 | Button | {Binding HojeTexto} | {Binding AtalhoCommand} |
| 36 | Button | {Binding AtrasadosTexto} | {Binding AtalhoCommand} |
| 37 | Button | {Binding SemContatoTexto} | {Binding AtalhoCommand} |
| 42 | TextBox | {Binding Busca, UpdateSourceTrigger=PropertyChanged} |  |
| 43 | ComboBox |  |  |
| 44 | ComboBox |  |  |
| 45 | Button | Mais filtros | {Binding AbrirFiltrosCommand} |
| 46 | Button | Limpar filtros | {Binding LimparFiltrosCommand} |
| 48 | TextBlock | {Binding FiltrosAtivos} |  |
| 49 | TextBlock | {Binding Resumo} |  |
| 53 | DataGrid |  |  |
| 55 | DataGrid |  |  |
| 56 | TextBlock | {Binding Paciente} |  |
| 56 | TextBlock | {Binding ModalidadeTexto} |  |
| 56 | TextBlock | {Binding Convenio} |  |
| 56 | TextBlock | {Binding Telefone} |  |
| 57 | TextBlock | {Binding Situacao} |  |
| 57 | TextBlock |  |  |
| 57 | TextBlock | {Binding ProximoContato, StringFormat='Próximo contato: {0:dd/MM/yyyy}'} |  |
| 57 | TextBlock | {Binding ProximoPasso} |  |
| 57 | TextBlock | {Binding Marcadores} |  |
| 57 | TextBlock | {Binding AgendadoPara, StringFormat='Sessão: {0:dd/MM/yyyy HH:mm}'} |  |
| 58 | TextBlock | {Binding Responsavel} |  |
| 58 | TextBlock | {Binding DiasTexto} |  |
| 58 | TextBlock | {Binding Tentativas, StringFormat='{}{0} tentativas'} |  |
| 58 | TextBlock | {Binding UltimoContato, StringFormat='Último: {0:dd/MM HH:mm}'} |  |
| 59 | Button | {Binding AcaoTexto} | {Binding DataContext.AbrirCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 65 | Button | Aplicar filtros e voltar | {Binding VoltarCommand} |
| 65 | TextBlock | Filtros do acompanhamento |  |
| 65 | TextBlock | {Binding Resumo} |  |
| 67 | ComboBox |  |  |
| 68 | ComboBox |  |  |
| 69 | ComboBox |  |  |
| 70 | ComboBox |  |  |
| 71 | ComboBox |  |  |
| 72 | TextBox | {Binding DiasMinimos, UpdateSourceTrigger=PropertyChanged} |  |
| 73 | TextBox | {Binding DiasMaximos, UpdateSourceTrigger=PropertyChanged} |  |
| 74 | ComboBox |  |  |
| 75 | TextBox | {Binding TentativasMinimas, UpdateSourceTrigger=PropertyChanged} |  |
| 79 | CheckBox | Somente meus pacientes |  |
| 79 | Button | Limpar filtros | {Binding LimparFiltrosCommand} |
| 80 | TextBlock | Estes filtros organizam os acompanhamentos existentes. Para buscar pacientes por dias sem retornar, use Buscar pacientes na tela principal. Contato atrasado significa que a data de contato venceu; é diferente de estar há muitos dias sem realizar uma sessão. |  |
| 81 | Expander | Entenda as situações e os próximos passos |  |
| 82 | TextBlock | A contatar: primeira tentativa • Sem resposta: tentar novamente no prazo • Retornar na data combinada: respeitar a data • Aguardando plano: acompanhar autorização • Pronto para agendar: escolher a data • Agendado: fora dos pendentes • Cancelou / Faltou: retomar contato • Conferir comparecimento: verificar a sessão passada na agenda • Encerrado: justificativa registrada. Contato atrasado: prazo vencido • Pacote com saldo: há sessões disponíveis • Sem telefone / Contato não autorizado: regularizar o cadastro. Cores sempre acompanham textos. |  |
| 84 | Expander | Visão da gestão — responsáveis e motivos |  |
| 85 | TextBlock | {Binding} |  |
| 90 | Button | Voltar à lista | {Binding VoltarCommand} |
| 91 | TextBlock | {Binding Selecionado.Paciente} |  |
| 92 | TextBlock | {Binding Selecionado.ModalidadeTexto} |  |
| 93 | TextBlock | {Binding Selecionado.Situacao} |  |
| 94 | TextBlock | {Binding Selecionado.Marcadores} |  |
| 95 | TextBlock | {Binding Selecionado.MotivoEncerramento} |  |
| 96 | Button | Assumir acompanhamento | {Binding AssumirCommand} |
| 96 | Button | Abrir WhatsApp | {Binding WhatsAppCommand} |
| 96 | Button | Agendar sessão | {Binding AgendarCommand} |
| 98 | TextBlock | {Binding ResponsavelContato} |  |
| 99 | ComboBox |  |  |
| 100 | ComboBox |  |  |
| 102 | ComboBox |  |  |
| 105 | TextBox | {Binding Observacao, UpdateSourceTrigger=PropertyChanged} |  |
| 106 | CheckBox | Encerrar por não conformidade (exige motivo e justificativa) |  |
| 106 | CheckBox | Reabrir acompanhamento encerrado |  |
| 107 | Button | Salvar acompanhamento | {Binding SalvarCommand} |
| 108 | TextBlock | Histórico de ações e contatos |  |
| 109 | TextBlock |  |  |
| 109 | TextBlock | {Binding Resultado} |  |
| 109 | TextBlock | {Binding Observacao} |  |
| 109 | TextBlock | {Binding ProximoContato, StringFormat='Próximo contato: {0:dd/MM/yyyy}'} |  |
| 114 | Button | Voltar à lista | {Binding VoltarCommand} |
| 115 | TextBlock | Configurar acompanhamento |  |
| 116 | TextBlock | Escolha o cadastro do Gustavo. Os novos casos ficam A assumir na fila compartilhada com recepção, faturamento e gerente. Cada contato registra quem assumiu o acompanhamento. |  |
| 117 | TextBlock | {Binding ConfiguracaoTexto} |  |
| 118 | ComboBox |  |  |
| 119 | Button | Salvar configuração | {Binding SalvarConfiguracaoCommand} |
| 120 | TextBlock | Motivos e legendas especiais |  |
| 121 | TextBlock | Exemplos: plano negou autorização, paciente pediu adiamento, desistência informada, telefone desatualizado. Cadastre rótulos que ajudem a equipe a decidir o próximo passo. |  |
| 122 | TextBox | {Binding NovoMotivo} |  |
| 123 | Button | Cadastrar motivo | {Binding AdicionarMotivoCommand} |
| 124 | TextBlock | {Binding Nome} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/AgendaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/AgendaView.xaml) · 323 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 11 | UserControl |  |  |
| 31 | Button |  | {Binding DataContext.AbrirHorarioCommand,
                              RelativeSource={RelativeSource AncestorType=UserControl}} |
| 43 | TextBlock |  |  |
| 51 | TextBlock | {Binding TituloPlanejamento} |  |
| 56 | TextBlock | {Binding Contexto} |  |
| 67 | TextBlock | Encaixe |  |
| 76 | TextBlock | Não confirmou |  |
| 82 | TextBlock | 2º código |  |
| 114 | Button |  | {Binding DataContext.AgendarNaFaixaCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 118 | Button |  |  |
| 142 | TextBlock | {Binding Rotulo} |  |
| 148 | Button | + | {Binding DataContext.AgendarNaFaixaCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 152 | Button |  |  |
| 180 | TextBlock | Agenda |  |
| 181 | TextBlock | {Binding ResumoPlanejamento} |  |
| 184 | Button | Horários e travas | {Binding ConfigurarHorariosCommand} |
| 186 | Button | Agendar | {Binding MarcarAtendimentoCommand} |
| 192 | TextBlock | Profissional |  |
| 193 | ComboBox |  |  |
| 196 | TextBlock | Atendimento |  |
| 197 | ComboBox |  |  |
| 200 | TextBlock | Duração (min) |  |
| 201 | TextBox | {Binding DuracaoPlanejamento, UpdateSourceTrigger=PropertyChanged} |  |
| 204 | TextBlock | Sala |  |
| 205 | ComboBox |  |  |
| 207 | Button | Próximas vagas | {Binding BuscarVagasPlanejamentoCommand} |
| 210 | Expander | Operações da agenda |  |
| 212 | Button | {Binding EsperaResumo} | {Binding AbrirEsperaCommand} |
| 213 | Button | Confirmar amanhã… | {Binding ConfirmarSessoesCommand} |
| 214 | Button | Conferir atendimentos… | {Binding ConciliarAgendaCommand} |
| 215 | Button | Imprimir folha | {Binding ImprimirAgendaCommand} |
| 216 | Button | Fechar agenda… | {Binding FecharAgendaCommand} |
| 217 | Button | Novo horário | {Binding NovoHorarioCommand} |
| 223 | Button | Tentar novamente | {Binding CarregarCommand} |
| 225 | TextBlock | Disponibilidade não verificada |  |
| 226 | TextBlock | {Binding UltimaLeitura} |  |
| 230 | TextBlock | {Binding Mensagem} |  |
| 232 | TextBlock | {Binding UltimaLeitura} |  |
| 239 | Button | Ir para hoje | {Binding HojeCommand} |
| 240 | TextBlock | {Binding ProfissionalEmFocoNome} |  |
| 241 | TextBlock | {Binding JornadaPlanejamento} |  |
| 242 | TextBlock | {Binding TravaPlanejamento} |  |
| 244 | TextBlock | Disponibilidade |  |
| 245 | TextBlock | Ocupado: bloco com duração completa. Disponível: cabe a duração consultada. Bloqueado ou fora do expediente: identificado por texto. |  |
| 246 | TextBlock | {Binding SalaPlanejamento} |  |
| 254 | Button | ‹ | {Binding DiaAnteriorCommand} |
| 256 | Button | › | {Binding ProximoDiaCommand} |
| 257 | Button | Dia | {Binding VerDiaPlanejamentoCommand} |
| 258 | Button | Semana | {Binding VerSemanaPlanejamentoCommand} |
| 259 | CheckBox | Por sala |  |
| 260 | Button | Atualizar | {Binding CarregarCommand} |
| 263 | TextBlock | {Binding Resumo} |  |
| 271 | TextBlock | {Binding Rotulo} |  |
| 279 | TextBlock | {Binding Nome} |  |
| 279 | TextBlock | {Binding Resumo} |  |
| 300 | TextBlock | Próximas vagas |  |
| 301 | TextBlock | {Binding ResumoPlanejamento} |  |
| 302 | TextBlock | {Binding SalaPlanejamento} |  |
| 305 | TextBlock | {Binding EstadoVagas} |  |
| 306 | Button | Ver mais dias e horários | {Binding BuscarVagasPlanejamentoCommand} |
| 311 | Button |  | {Binding DataContext.EscolherVagaPlanejamentoCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 314 | TextBlock | {Binding Inicio, StringFormat='{}{0:ddd dd/MM · HH:mm}'} |  |
| 314 | TextBlock | {Binding Fim, StringFormat='Até {0:HH:mm} · escolher horário'} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/ConfirmacoesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ConfirmacoesView.xaml) · 131 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 2 | UserControl |  |  |
| 11 | TextBlock | Confirmar sessões |  |
| 12 | TextBlock | Confirmar a própria sessão é transacional: o paciente pediu o horário, e avisar sobre ele não é marketing — não exige consentimento. Gerar é o trabalho automático; enviar é um clique por paciente, porque o número é o WhatsApp da clínica. |  |
| 20 | TextBlock | Dia |  |
| 23 | Button | Amanhã | {Binding AmanhaCommand} |
| 26 | Button | Gerar rodada | {Binding GerarCommand} |
| 30 | Button | Enviar e-mails | {Binding EnviarEmailsCommand} |
| 34 | Button | Atualizar | {Binding CarregarCommand} |
| 39 | TextBlock | {Binding Resumo} |  |
| 57 | TextBlock | {Binding Mensagem} |  |
| 75 | TextBlock | {Binding Horario} |  |
| 80 | TextBlock | {Binding Paciente} |  |
| 83 | TextBlock |  |  |
| 92 | TextBlock | Sem telefone no cadastro — ligue ou complete a ficha. |  |
| 96 | TextBlock |  |  |
| 111 | Button | WhatsApp | {Binding DataContext.EnviarCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 116 | Button | Confirmou | {Binding DataContext.ConfirmouCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/ConsultasView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ConsultasView.xaml) · 194 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 14 | TextBlock | Consultas |  |
| 15 | TextBlock | A consulta do convênio tem validade. Renove com o paciente na frente — descobrir que ela venceu na hora de faturar é ligar para quem já foi embora. |  |
| 22 | TextBlock | {Binding Resumo} |  |
| 25 | Button | Atualizar | {Binding RecarregarCommand} |
| 49 | TextBlock | Situação |  |
| 50 | ComboBox |  |  |
| 55 | TextBlock | Convênio |  |
| 56 | ComboBox |  |  |
| 61 | TextBlock | Paciente |  |
| 62 | TextBox | {Binding FiltroPaciente, UpdateSourceTrigger=PropertyChanged} |  |
| 67 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 77 | TextBlock | {Binding Mensagem} |  |
| 81 | TextBlock | {Binding Mensagem} |  |
| 119 | TextBlock | PACIENTE / SITUAÇÃO |  |
| 120 | TextBlock | CONVÊNIO |  |
| 121 | TextBlock | VENCIMENTO |  |
| 143 | TextBlock | {Binding Paciente} |  |
| 145 | TextBlock | {Binding Situacao} |  |
| 151 | TextBlock | {Binding Convenio} |  |
| 153 | TextBlock | {Binding Vencimento} |  |
| 155 | Button | Renovar | {Binding DataContext.RenovarCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 159 | Button |  |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/ConvenioPacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ConvenioPacienteView.xaml) · 73 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 2 | UserControl |  |  |
| 5 | TextBlock | {Binding ValidadeConsulta} |  |
| 6 | Button | Renovar validade da consulta | {Binding RenovarConsultaCommand} |
| 7 | TextBlock | {Binding Mensagem} |  |
| 11 | TextBlock | As sessões que o convênio autorizou. O consumo é contado sozinho pelos atendimentos dentro da vigência — não se digita. |  |
| 12 | Button | Nova autorização | {Binding NovaAutorizacaoCommand} |
| 17 | TextBlock | Não foi possível ler as autorizações deste paciente — a lista abaixo NÃO significa que ele não tem senha. |  |
| 39 | TextBlock | {Binding Autorizacao.Numero, TargetNullValue='(sem número)'} |  |
| 40 | TextBlock | {Binding Resumo} |  |
| 44 | TextBlock | {Binding Autorizacao.DataEmissao, StringFormat='emitida {0:dd/MM/yyyy}'} |  |
| 45 | TextBlock | {Binding Autorizacao.DataValidade, StringFormat='válida até {0:dd/MM/yyyy}'} |  |
| 49 | Button | Editar | {Binding DataContext.EditarAutorizacaoCommand,                                                                       RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 50 | Button | Excluir | {Binding DataContext.ExcluirAutorizacaoCommand,                                                                       RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/DocumentosView.xaml) · 701 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 20 | TextBlock | Documentos |  |
| 21 | TextBlock | Todas as folhas que a clínica emite, num lugar só — numeradas por ano, com código de conferência. Documento NÃO se apaga: cancela-se com motivo, e a segunda via sai idêntica à emitida. |  |
| 27 | TextBlock | {Binding Mensagem} |  |
| 41 | TabItem | Emitir |  |
| 74 | TextBlock |  |  |
| 76 | TextBlock |  |  |
| 88 | TextBlock | 1 · QUEM |  |
| 112 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 127 | TextBlock | {Binding RotuloDeHoje} |  |
| 140 | Button |  | {Binding DataContext.EscolherDeHojeCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 149 | TextBlock |  |  |
| 176 | TextBlock | {Binding Seletor.Erro} |  |
| 204 | Button | Trocar | {Binding TrocarPacienteCommand} |
| 216 | TextBlock | {Binding Seletor.Selecionado.Nome} |  |
| 223 | TextBlock | {Binding IdentidadeDoPaciente} |  |
| 244 | TextBlock | 2 |  |
| 247 | TextBlock | 2 · QUAL PAPEL |  |
| 260 | TextBlock | Receita, atestado e pedido de exame são emitidos por QUEM ASSINA, no Consultório. Aqui eles aparecem na aba “O que já saiu”, para segunda via. |  |
| 289 | TextBlock | 3 |  |
| 293 | TextBlock | 3 · O QUE VAI SAIR |  |
| 303 | Button | {Binding Previa.AcaoRotulo} | {Binding GerarCommand} |
| 311 | TextBlock | {Binding Previa.Rotulo} |  |
| 313 | TextBlock | {Binding Previa.ParaQuem} |  |
| 321 | TextBlock | {Binding Previa.Descricao} |  |
| 326 | TextBlock | {Binding Previa.OQueAcontece} |  |
| 333 | TextBlock | {Binding Previa.OQueFalta} |  |
| 347 | TextBlock | {Binding Previa.Garantias} |  |
| 391 | TextBlock | {Binding Rotulo} |  |
| 393 | TextBlock | {Binding Descricao} |  |
| 395 | TextBlock | {Binding Pendencia} |  |
| 408 | Button |  | {Binding DataContext.EscolherFolhaCommand,
                                                          RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 413 | Button |  |  |
| 425 | TextBlock | {Binding Grupo} |  |
| 429 | TextBlock | {Binding Rotulo} |  |
| 434 | TextBlock | {Binding Pendencia} |  |
| 452 | TabItem | O que já saiu |  |
| 459 | TextBlock | {Binding Resumo} |  |
| 471 | TextBlock | De |  |
| 475 | TextBlock | Até |  |
| 479 | Button | Atualizar | {Binding CarregarCommand} |
| 505 | TextBlock | Conferir pelo código |  |
| 508 | TextBox | {Binding Codigo, UpdateSourceTrigger=PropertyChanged} |  |
| 510 | Button | Conferir | {Binding ConferirCommand} |
| 513 | Button | Segunda via | {Binding ReimprimirConferidoCommand} |
| 524 | TextBlock | {Binding Conferido} |  |
| 526 | TextBlock |  |  |
| 550 | TextBlock | Número |  |
| 551 | TextBlock | Folha |  |
| 552 | TextBlock | Para |  |
| 553 | TextBlock | Data |  |
| 571 | TextBlock | {Binding Numero} |  |
| 574 | TextBlock | {Binding Folha} |  |
| 579 | TextBlock | {Binding Para} |  |
| 581 | TextBlock | {Binding Detalhe} |  |
| 588 | TextBlock | {Binding Situacao} |  |
| 591 | TextBlock |  |  |
| 607 | TextBlock | {Binding Link} |  |
| 610 | TextBlock |  |  |
| 622 | TextBlock | {Binding Data} |  |
| 645 | Button | Assinar | {Binding DataContext.AssinarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=UserControl}} |
| 658 | Button | 2ª via | {Binding DataContext.ReimprimirCommand,
                                                                  RelativeSource={RelativeSource AncestorType=UserControl}} |
| 662 | Button |  |  |
| 673 | Button | ⋯ | AoAbrirMenuDoDocumento |

## Clinica · src/Clinica.Modulo.Recepcao/Views/EquipeView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/EquipeView.xaml) · 288 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 16 | TextBlock | Profissionais e salas |  |
| 17 | TextBlock | Quem atende e onde se atende. É este cadastro que dá colunas à agenda — e é dele que vão depender o repasse por profissional e a produtividade nos relatórios. |  |
| 23 | TextBlock | {Binding Mensagem} |  |
| 29 | TabItem | Profissionais |  |
| 36 | TextBlock | Profissionais |  |
| 38 | Button | Novo profissional | {Binding NovoProfissionalCommand} |
| 56 | TextBlock | {Binding Nome} |  |
| 58 | TextBlock |  |  |
| 73 | Button | Editar | {Binding DataContext.EditarProfissionalCommand,
                                                                  RelativeSource={RelativeSource AncestorType=UserControl}} |
| 77 | Button | Excluir | {Binding DataContext.ExcluirProfissionalCommand,
                                                                  RelativeSource={RelativeSource AncestorType=UserControl}} |
| 102 | TabItem | Salas |  |
| 106 | TextBlock | Salas |  |
| 108 | Button | Nova sala | {Binding NovaSalaCommand} |
| 112 | TextBlock | Nenhuma sala cadastrada. |  |
| 113 | TextBlock |  |  |
| 139 | TextBlock | {Binding Nome} |  |
| 141 | TextBlock |  |  |
| 152 | Button | Editar | {Binding DataContext.EditarSalaCommand,
                                                                  RelativeSource={RelativeSource AncestorType=UserControl}} |
| 156 | Button | Excluir | {Binding DataContext.ExcluirSalaCommand,
                                                                  RelativeSource={RelativeSource AncestorType=UserControl}} |
| 173 | TabItem | Bloqueios |  |
| 178 | TextBlock | Agenda fechada |  |
| 179 | TextBlock | Férias, feriado, folga, sala em manutenção. |  |
| 186 | Button | Fechar agenda… | {Binding NovoBloqueioCommand} |
| 193 | TextBlock | Nada bloqueado daqui para a frente. Enquanto não houver, o que segura a marcação em cima da folga é a memória de quem está no balcão. |  |
| 195 | TextBlock |  |  |
| 221 | TextBlock | {Binding Alvo} |  |
| 224 | TextBlock | {Binding Periodo} |  |
| 228 | TextBlock | {Binding Motivo} |  |
| 234 | TextBlock | {Binding Situacao} |  |
| 237 | TextBlock |  |  |
| 260 | Button | Empurrar sessões | {Binding DataContext.RemarcarBloqueioCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 268 | Button | Reabrir | {Binding DataContext.ExcluirBloqueioCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/FilaView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/FilaView.xaml) · 580 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 27 | UserControl |  |  |
| 152 | TextBlock | Agenda do dia |  |
| 154 | Button | ◀ | {Binding DiaAnteriorCommand} |
| 161 | TextBlock | {Binding Dia, StringFormat='{}{0:dd/MM/yyyy}'} |  |
| 170 | TextBlock | hoje |  |
| 174 | Button | ▶ | {Binding ProximoDiaCommand} |
| 175 | Button | Hoje | {Binding HojeCommand} |
| 176 | Button | Atualizar | {Binding CarregarCommand} |
| 183 | TextBlock |  |  |
| 187 | TextBlock | {Binding Atendidos} |  |
| 189 | TextBlock | atendidos |  |
| 197 | TextBlock |  |  |
| 201 | TextBlock | {Binding EmSala} |  |
| 203 | TextBlock | em atendimento |  |
| 217 | TextBlock |  |  |
| 221 | TextBlock | {Binding FaltasCancelamentos} |  |
| 224 | TextBlock | falta · cancelamento |  |
| 230 | Button | Marcar atendimento | {Binding MarcarAtendimentoCommand} |
| 232 | Button | Novo horário | {Binding NovoHorarioCommand} |
| 248 | Button | {Binding Rotulo} | {Binding DataContext.FiltrarCommand,
                                              RelativeSource={RelativeSource AncestorType=UserControl}} |
| 284 | DataGrid |  |  |
| 290 | DataGrid |  |  |
| 320 | DataGrid |  |  |
| 329 | TextBlock |  |  |
| 337 | TextBlock | {Binding Horario} |  |
| 365 | TextBlock | Encaixe |  |
| 367 | TextBlock | {Binding Paciente} |  |
| 370 | TextBlock | {Binding ContextoDaLista} |  |
| 373 | TextBlock | {Binding Profissional, StringFormat='Profissional: {0}'} |  |
| 374 | TextBlock | {Binding Sala, StringFormat='Sala: {0}'} |  |
| 378 | TextBlock | {Binding Observacoes} |  |
| 403 | TextBlock | {Binding Texto} |  |
| 419 | TextBlock | {Binding Profissional} |  |
| 423 | TextBlock |  |  |
| 450 | TextBlock | {Binding Status} |  |
| 453 | TextBlock | {Binding StatusDetalhe} |  |
| 515 | Button | {Binding ProximoPasso} | {Binding DataContext.AvancarCommand,
                                                          RelativeSource={RelativeSource AncestorType=UserControl}} |
| 522 | Button |  |  |
| 552 | Button | Editar | {Binding DataContext.EditarHorarioCommand,
                                                          RelativeSource={RelativeSource AncestorType=UserControl}} |
| 567 | Button | ⋯ | AoAbrirMenuDaLinha |

## Clinica · src/Clinica.Modulo.Recepcao/Views/LancamentosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/LancamentosView.xaml) · 209 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 24 | UserControl |  |  |
| 32 | TextBlock | Lançamentos |  |
| 33 | TextBlock | Os atendimentos lançados no período — a conferência antes de fechar o balcão. Quem lançou, quantas guias saíram e o que ainda não liberou. |  |
| 45 | TextBlock | De |  |
| 49 | TextBlock | Até |  |
| 53 | TextBlock | Convênio |  |
| 54 | ComboBox |  |  |
| 58 | TextBlock | Modalidade |  |
| 59 | ComboBox |  |  |
| 63 | TextBlock | Quem lançou |  |
| 64 | ComboBox |  |  |
| 67 | CheckBox | Só com guia por liberar |  |
| 71 | Button | Atualizar | {Binding BuscarCommand} |
| 76 | Button | Limpar filtros | {Binding LimparFiltrosCommand} |
| 83 | TextBlock | {Binding Mensagem} |  |
| 86 | TextBlock |  |  |
| 102 | TextBlock | {Binding Resumo} |  |
| 128 | DataGrid |  |  |
| 134 | DataGrid |  |  |
| 143 | TextBlock | {Binding Paciente} |  |
| 145 | TextBlock | {Binding Modalidade} |  |
| 147 | TextBlock | {Binding Lancamento} |  |
| 157 | TextBlock | {Binding Guias} |  |
| 160 | TextBlock | {Binding Pendencia} |  |
| 180 | TextBlock | {Binding Convenio} |  |
| 195 | Button | Estornar… | {Binding DataContext.EstornarCommand,
                                                      RelativeSource={RelativeSource AncestorType=UserControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/NovoAtendimentoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/NovoAtendimentoView.xaml) · 1271 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 38 | UserControl |  |  |
| 85 | TextBlock | {Binding TituloTela} |  |
| 86 | TextBlock | {Binding SubtituloTela} |  |
| 103 | TextBlock | 1 · QUEM |  |
| 156 | TextBlock | {Binding PacienteSelecionado.Nome} |  |
| 162 | TextBlock | {Binding MetaPaciente} |  |
| 168 | TextBlock | CONVÊNIO |  |
| 169 | TextBlock | {Binding ConvenioPaciente} |  |
| 170 | TextBlock |  |  |
| 186 | TextBlock | CARTEIRINHA |  |
| 187 | TextBlock | {Binding CarteirinhaPaciente} |  |
| 194 | TextBlock | CATEGORIA |  |
| 195 | TextBlock | {Binding CategoriaPaciente} |  |
| 199 | Button | Trocar | {Binding TrocarPacienteCommand} |
| 214 | TextBlock | {Binding TituloPassoQuando} |  |
| 222 | TextBlock | Data |  |
| 226 | TextBlock | Hora |  |
| 230 | TextBox | {Binding Hora, UpdateSourceTrigger=PropertyChanged} |  |
| 242 | TextBlock | Quem atende |  |
| 243 | ComboBox |  |  |
| 247 | TextBlock | Aparece no app de quem atende e entra no repasse. |  |
| 251 | TextBlock | Nenhum profissional ativo cadastrado — a sessão sai sem dono e não aparece no app de quem atende. Cadastre em Profissionais e salas. |  |
| 258 | TextBlock | Duração (min) |  |
| 259 | TextBox | {Binding Duracao, UpdateSourceTrigger=PropertyChanged} |  |
| 265 | TextBlock | Sala |  |
| 266 | ComboBox |  |  |
| 275 | Button | Próximas vagas… | {Binding ProximasVagasCommand} |
| 297 | TextBlock | Sem profissional: o horário não aparece no quadro de ninguém e fica fora do repasse. Escolha em "Quem atende", na linha acima — o sistema não sorteia. |  |
| 309 | TextBlock | {Binding CabecalhoDosAvisos} |  |
| 339 | TextBlock | {Binding Texto} |  |
| 349 | CheckBox | Registrar como encaixe (atender por cima de um horário que já tem paciente) |  |
| 355 | TextBlock | {Binding TituloPasso2} |  |
| 387 | Button |  | {Binding DataContext.EscolherModalidadeCommand,
                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 395 | Button |  |  |
| 421 | TextBlock |  |  |
| 430 | TextBlock | {Binding Nome} |  |
| 440 | TextBlock | o de sempre dele |  |
| 446 | TextBlock | {Binding QuantasGuias} |  |
| 452 | TextBlock | {Binding Quando} |  |
| 527 | TextBlock | Especialidade da consulta |  |
| 528 | ComboBox |  |  |
| 531 | TextBlock | Fica registrada na guia e nos relatórios. |  |
| 537 | TextBlock | Qual código libera primeiro? |  |
| 538 | ComboBox |  |  |
| 541 | TextBlock | O outro vira o 2º código, previsto para +24h. |  |
| 546 | TextBlock | Observações |  |
| 547 | TextBox | {Binding Observacoes} |  |
| 564 | TextBlock | {Binding TituloResultado} |  |
| 566 | TextBlock | {Binding ResumoBaixas} |  |
| 570 | Button | Capa inicial (PDF) | {Binding GerarCapaCommand} |
| 579 | TextBlock | Guias geradas |  |
| 594 | TextBlock | {Binding TipoRotulo} |  |
| 595 | TextBlock | {Binding Situacao} |  |
| 602 | TextBlock | {Binding EspecialidadeRotulo} |  |
| 604 | TextBlock | {Binding OrdemRotulo} |  |
| 605 | TextBlock | {Binding FaturarEm} |  |
| 606 | TextBlock | {Binding ComoObter} |  |
| 630 | TextBlock | {Binding} |  |
| 662 | TextBlock | {Binding Mensagem} |  |
| 665 | TextBlock |  |  |
| 683 | CheckBox | O atendimento já aconteceu — registrar como realizado |  |
| 687 | Button | {Binding RotuloLancar} | {Binding LancarCommand} |
| 691 | Button |  |  |
| 704 | Button | {Binding RotuloOutro} | {Binding NovoLancamentoCommand} |
| 707 | Button | Limpar | {Binding NovoLancamentoCommand} |
| 739 | TextBlock | ANTES DE LANÇAR |  |
| 755 | TextBlock | Sem convênio vinculado — o atendimento não gera guia e não pode ser lançado. Escolha o convênio do paciente (particular, para quem paga do bolso). |  |
| 758 | Button | Escolher convênio… | {Binding EscolherConvenioCommand} |
| 776 | TextBlock | {Binding AvisoJaLancado} |  |
| 794 | TextBlock | {Binding AvisoHorarioDoDia} |  |
| 797 | CheckBox | Criar um encaixe separado (o horário continua na agenda) |  |
| 810 | TextBlock | {Binding AvisoCarteirinha} |  |
| 832 | TextBlock | {Binding AvisoConsulta} |  |
| 857 | TextBlock | {Binding SaldoAutorizacao} |  |
| 869 | TextBlock | {Binding AvisoPendencias} |  |
| 897 | TextBlock | {Binding Descricao} |  |
| 902 | Button | Receber… | {Binding DataContext.ReceberDividaCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 962 | TextBlock | O PAGAMENTO |  |
| 963 | TextBlock | {Binding ValorPrevisto} |  |
| 971 | Button | Vender pacote… | {Binding VenderPacoteCommand} |
| 981 | TextBlock | A GUIA |  |
| 984 | Button | {Binding RotuloVerGuia} | {Binding AlternarGuiaCommand} |
| 988 | TextBlock | {Binding ResumoCurtoPrevia} |  |
| 1016 | TextBlock | PRÉVIA — NADA FOI GRAVADO AINDA |  |
| 1023 | TextBlock | sem convênio |  |
| 1040 | TextBlock | CONVÊNIO |  |
| 1062 | TextBlock | {Binding ConvenioPaciente} |  |
| 1063 | TextBlock |  |  |
| 1078 | TextBlock | CARTEIRINHA |  |
| 1082 | TextBlock | {Binding CarteirinhaPaciente} |  |
| 1096 | TextBlock | BENEFICIÁRIO |  |
| 1100 | TextBlock | {Binding PacienteSelecionado.Nome} |  |
| 1106 | TextBlock | VALIDADE |  |
| 1123 | TextBlock | {Binding ValidadePaciente} |  |
| 1124 | TextBlock |  |  |
| 1147 | TextBlock | EXECUTANTE |  |
| 1168 | TextBlock | {Binding ExecutanteGuia} |  |
| 1169 | TextBlock |  |  |
| 1184 | TextBlock | Nº DA GUIA |  |
| 1191 | TextBlock | ao baixar |  |
| 1199 | TextBlock | {Binding ResumoPrevia} |  |
| 1203 | TextBlock | CÓDIGOS |  |
| 1218 | TextBlock |  |  |
| 1221 | TextBlock | {Binding Especialidade} |  |
| 1226 | TextBlock | {Binding Nota} |  |
| 1262 | TextBlock | {Binding NotaGuiaNaMarcacao} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/PacientesView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PacientesView.xaml) · 165 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 24 | UserControl |  |  |
| 40 | TextBlock | Pacientes |  |
| 41 | TextBlock | Cadastro, elegibilidade, prontuário e LGPD. Abra alguém para ver a ficha completa. |  |
| 46 | TextBlock | Mostrar |  |
| 47 | ComboBox |  |  |
| 60 | TextBox | {Binding Seletor.Termo, UpdateSourceTrigger=PropertyChanged} |  |
| 64 | TextBlock | {Binding Resumo} |  |
| 76 | Button | Ver todos | {Binding Seletor.DesligarSugestaoCommand} |
| 80 | Button |  |  |
| 91 | Button | Novo paciente | {Binding NovoPacienteCommand} |
| 96 | TextBlock | {Binding Seletor.Erro} |  |
| 143 | TextBlock | PACIENTE |  |
| 144 | TextBlock | TELEFONE |  |
| 145 | TextBlock | CONVÊNIO |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/PagamentosView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PagamentosView.xaml) · 56 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 7 | UserControl |  |  |
| 14 | TextBlock | Pagamentos na recepção |  |
| 15 | TextBlock | {Binding Paciente} |  |
| 17 | Button | Trocar paciente | {Binding TrocarPacienteCommand} |
| 18 | Button | Atualizar | {Binding CarregarCommand} |
| 20 | TextBlock | {Binding Resumo} |  |
| 21 | TextBlock | {Binding Mensagem} |  |
| 27 | DataGrid |  |  |
| 28 | DataGrid |  |  |
| 40 | Button | Receber | {Binding DataContext.ReceberCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |
| 44 | Button | Recibo | {Binding DataContext.ReciboCommand, RelativeSource={RelativeSource AncestorType=DataGrid}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/PainelView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PainelView.xaml) · 419 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 8 | UserControl |  |  |
| 17 | TextBlock | Painel da recepção |  |
| 18 | TextBlock | Como está o dia no balcão: quantos faltam atender, quem está na sala e quanto cada profissional tem na agenda. |  |
| 23 | Button | ◀ | {Binding DiaAnteriorCommand} |
| 29 | TextBlock | {Binding Dia, StringFormat='{}{0:dd/MM/yyyy}'} |  |
| 34 | Button | ▶ | {Binding ProximoDiaCommand} |
| 36 | Button | Hoje | {Binding HojeCommand} |
| 38 | Button | Atualizar | {Binding CarregarCommand} |
| 45 | TextBlock | {Binding Mensagem} |  |
| 69 | TextBlock |  |  |
| 70 | TextBlock | Agendados |  |
| 73 | TextBlock | {Binding Agendados} |  |
| 79 | TextBlock |  |  |
| 90 | TextBlock |  |  |
| 94 | TextBlock | A atender |  |
| 97 | TextBlock | {Binding AAtender} |  |
| 105 | TextBlock |  |  |
| 106 | TextBlock | Em atendimento |  |
| 109 | TextBlock | {Binding EmAtendimento} |  |
| 117 | TextBlock |  |  |
| 118 | TextBlock | Atendidos |  |
| 121 | TextBlock | {Binding Atendidos} |  |
| 129 | TextBlock |  |  |
| 152 | TextBlock |  |  |
| 153 | TextBlock | Faltas |  |
| 156 | TextBlock | {Binding Faltas} |  |
| 158 | TextBlock |  |  |
| 169 | TextBlock |  |  |
| 170 | TextBlock | Taxa de falta |  |
| 173 | TextBlock | {Binding TaxaFalta} |  |
| 181 | TextBlock |  |  |
| 192 | TextBlock |  |  |
| 193 | TextBlock | Encaixes |  |
| 196 | TextBlock | {Binding Encaixes} |  |
| 204 | TextBlock |  |  |
| 205 | TextBlock | Na lista de espera |  |
| 208 | TextBlock | {Binding ListaDeEspera} |  |
| 223 | TextBlock | Ocupação por profissional |  |
| 224 | TextBlock | Barra sobre uma jornada de referência de 8 horas. |  |
| 228 | TextBlock | Nenhum horário marcado para este dia. |  |
| 230 | TextBlock |  |  |
| 247 | TextBlock | {Binding Nome} |  |
| 249 | TextBlock |  |  |
| 275 | TextBlock | Guias pendentes de quem vem hoje |  |
| 276 | TextBlock | O paciente está no balcão: é a hora barata de pedir o documento. Depois vira telefonema do faturamento. |  |
| 286 | TextBlock | Não foi possível verificar as guias pendentes agora. Isto NÃO quer dizer que não há pendências — tente Atualizar. |  |
| 290 | TextBlock | Nenhuma guia pendente entre os pacientes de hoje. |  |
| 292 | TextBlock |  |  |
| 321 | TextBlock | {Binding Paciente} |  |
| 323 | TextBlock |  |  |
| 332 | Button | WhatsApp | {Binding DataContext.CobrarGuiaCommand,
                                                                  RelativeSource={RelativeSource AncestorType=UserControl}} |
| 354 | TextBlock | Aniversariantes da semana |  |
| 355 | TextBlock | Do dia e dos próximos seis — a clínica não abre todo dia, e sem a janela todo aniversário de domingo se perderia. Quem vem hoje aparece primeiro: o parabéns é dado no balcão. |  |
| 362 | TextBlock | Não foi possível ler os aniversariantes. A lista está vazia por falha de leitura, não por não haver ninguém. |  |
| 378 | TextBlock | {Binding Paciente} |  |
| 380 | TextBlock | {Binding Detalhe} |  |
| 390 | TextBlock | Vem hoje |  |
| 392 | Button | Parabenizar | {Binding DataContext.ParabenizarCommand,
                                                                  RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/PrivacidadePacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/PrivacidadePacienteView.xaml) · 89 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 2 | UserControl |  |  |
| 7 | TextBlock | Consentimentos (LGPD) |  |
| 8 | TextBlock | Quem responde é o PACIENTE, no termo que ele assina. Cada resposta vira um registro datado, e revogar não apaga o anterior — ele continua provando o consentimento do período já tratado. |  |
| 19 | TextBlock | Termo de consentimento assinado pelo paciente |  |
| 20 | TextBlock | {Binding TermoLgpdSituacao} |  |
| 23 | Button | Colher assinatura… | {Binding ColherTermoLgpdCommand} |
| 38 | TextBlock | {Binding Rotulo} |  |
| 39 | TextBlock | {Binding Situacao} |  |
| 44 | TextBlock | Vigente |  |
| 48 | Button | Revogar | {Binding DataContext.RevogarCommand,                                                                   RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 49 | Button |  |  |
| 75 | TextBlock | Direitos do titular |  |
| 76 | TextBlock | Pedido de acesso e pedido de eliminação (LGPD, art. 18). O prontuário NÃO se apaga: a guarda é obrigação legal do profissional de saúde e a lei a preserva (art. 16, II) — anonimizar tira a identificação e mantém o histórico. |  |
| 79 | Button | Exportar meus dados | {Binding ExportarDadosCommand} |
| 80 | Button | Anonimizar cadastro | {Binding AnonimizarCommand} |
| 83 | TextBlock | Anonimizar não tem volta e por isso pede permissão própria — o balcão exporta, a direção elimina. |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/ProntuarioView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ProntuarioView.xaml) · 226 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 18 | UserControl |  |  |
| 45 | Button |  Pacientes | {Binding VoltarCommand} |
| 50 | TextBlock | {Binding Paciente} |  |
| 51 | TextBlock | Prontuário — evolução clínica e EVA, sessão a sessão. |  |
| 55 | Button | Nova sessão | {Binding NovaSessaoCommand} |
| 63 | TextBlock | {Binding Mensagem} |  |
| 70 | TextBlock | Evolução da dor (EVA) |  |
| 77 | TextBlock | DOR INICIAL |  |
| 78 | TextBlock | {Binding DorInicial} |  |
| 81 | TextBlock | DOR ATUAL |  |
| 82 | TextBlock | {Binding DorAtual} |  |
| 85 | TextBlock | GANHO ACUMULADO |  |
| 86 | TextBlock | {Binding GanhoAcumulado} |  |
| 89 | TextBlock | ALÍVIO MÉDIO |  |
| 90 | TextBlock | {Binding AlivioMedio} |  |
| 93 | TextBlock | {Binding ResumoEva} |  |
| 98 | TextBlock | {Binding ResumoEscalas} |  |
| 107 | TextBlock | Sessões |  |
| 115 | TextBox | {Binding TermoSessao, UpdateSourceTrigger=PropertyChanged, Delay=300} |  |
| 120 | TextBlock | {Binding ResumoSessoes} |  |
| 125 | TextBlock | Nenhuma sessão registrada ainda. |  |
| 126 | TextBlock |  |  |
| 152 | TextBlock | {Binding Data} |  |
| 153 | TextBlock | {Binding Profissional} |  |
| 158 | TextBlock | {Binding Resumo} |  |
| 160 | TextBlock | {Binding Eva} |  |
| 162 | TextBlock | {Binding Anexos} |  |
| 177 | Button | Ver | {Binding DataContext.VerSessaoCommand,
                                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 186 | Button | Editar | {Binding DataContext.EditarSessaoCommand,
                                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 193 | Button | Cancelar… | {Binding DataContext.ExcluirSessaoCommand,
                                                                      RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RelacionamentoPacienteView.xaml) · 56 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 2 | UserControl |  |  |
| 7 | TextBlock | Relacionamento (CRM) |  |
| 8 | TextBlock | De onde o paciente veio e as últimas conversas da clínica com ele. Quem dispara a campanha é o Gerente; o resultado aparece aqui, onde alguém vai atendê-lo. |  |
| 11 | TextBlock | ORIGEM |  |
| 12 | TextBlock | {Binding Origem} |  |
| 15 | TextBlock | Nenhum contato de campanha registrado. |  |
| 16 | TextBlock |  |  |
| 38 | TextBlock | {Binding Data} |  |
| 40 | TextBlock | {Binding Tipo} |  |
| 41 | TextBlock | {Binding Detalhe} |  |
| 44 | TextBlock | {Binding Situacao} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/ResumoAdministrativoPacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/ResumoAdministrativoPacienteView.xaml) · 97 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 2 | UserControl |  |  |
| 7 | TextBlock | Pode ser atendido hoje? |  |
| 8 | TextBlock | Carteirinha, cota de sessões do convênio e consentimento LGPD. Conferir aqui evita a glosa que só apareceria depois da sessão. |  |
| 12 | TextBlock | Não foi possível conferir a elegibilidade agora. Isto NÃO quer dizer que está tudo certo — tente Atualizar. |  |
| 31 | TextBlock | Sem pendências: carteirinha, cota e consentimento em ordem. |  |
| 53 | TextBlock | {Binding Descricao} |  |
| 55 | Button | Receber… | {Binding DataContext.ReceberDividaCommand, RelativeSource={RelativeSource AncestorType=UserControl}} |
| 67 | TextBlock | Próximos horários |  |
| 68 | TextBlock | O que este paciente tem marcado daqui em diante, os cinco mais próximos. Abrir leva à agenda daquele dia. |  |
| 70 | TextBlock | Não foi possível ler a agenda deste paciente agora. Isto NÃO quer dizer que ele não tem horário — tente Atualizar. |  |
| 72 | TextBlock | Nenhum horário marcado daqui em diante. |  |
| 78 | Button | Abrir na agenda | {Binding DataContext.AbrirNaAgendaCommand,                                                               RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 80 | TextBlock | Encaixe |  |
| 83 | TextBlock | {Binding Rotulo} |  |
| 84 | TextBlock | {Binding Contexto} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/RetornoView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RetornoView.xaml) · 203 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 19 | UserControl |  |  |
| 28 | TextBlock | Retorno de pacientes |  |
| 29 | TextBlock | Quem parou de vir e não tem horário marcado. Gerar é o trabalho automático; chamar é um clique por paciente, porque o número é o WhatsApp da clínica. |  |
| 37 | TextBlock | Sem vir há |  |
| 39 | TextBox | {Binding DiasSemVir, UpdateSourceTrigger=LostFocus} |  |
| 41 | TextBlock | dias ou mais |  |
| 47 | Button | Atualizar | {Binding RecarregarCommand} |
| 51 | Button | Gerar rodada do mês | {Binding GerarCommand} |
| 58 | TextBlock | {Binding Resumo} |  |
| 81 | TextBlock | Paciente |  |
| 82 | TextBox | {Binding FiltroNomeRetorno, UpdateSourceTrigger=PropertyChanged} |  |
| 87 | CheckBox | Esconder já chamados |  |
| 91 | CheckBox | Só com telefone |  |
| 95 | Button | Limpar filtro | {Binding LimparFiltroCommand} |
| 116 | TextBlock | {Binding Mensagem} |  |
| 147 | TextBlock | {Binding DiasSemVir, StringFormat='{}{0} dias'} |  |
| 149 | TextBlock | sem vir |  |
| 156 | TextBlock | {Binding Paciente} |  |
| 158 | TextBlock |  |  |
| 171 | TextBlock | {Binding Impedimento} |  |
| 181 | Button | WhatsApp | {Binding DataContext.ChamarCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |
| 187 | Button | Respondeu | {Binding DataContext.RespondeuCommand,
                                                              RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Modulo.Recepcao/Views/RetornosAMarcarView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/RetornosAMarcarView.xaml) · 158 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 14 | UserControl |  |  |
| 20 | TextBlock | Retornos a marcar |  |
| 21 | TextBlock | Quem saiu do atendimento com pedido de retorno e ainda não tem horário marcado. A linha some sozinha quando o horário é marcado; passados 60 dias da data sugerida, o paciente sai daqui e passa a ser assunto de Quem parou de vir. |  |
| 28 | Button | Atualizar | {Binding CarregarCommand} |
| 33 | TextBlock | {Binding Resumo} |  |
| 37 | TextBlock | {Binding Mensagem} |  |
| 40 | TextBlock |  |  |
| 72 | DataGrid |  |  |
| 78 | DataGrid |  |  |
| 84 | Button | Marcar horário | {Binding DataContext.MarcarCommand,
                                                          RelativeSource={RelativeSource AncestorType=UserControl}} |
| 92 | Button | WhatsApp | {Binding DataContext.WhatsAppCommand,
                                                          RelativeSource={RelativeSource AncestorType=UserControl}} |
| 107 | TextBlock | {Binding RetornoEm, StringFormat='dd/MM/yyyy'} |  |
| 109 | TextBlock | {Binding Situacao} |  |
| 126 | TextBlock | {Binding Paciente} |  |
| 128 | TextBlock | {Binding Telefone, TargetNullValue='sem telefone no cadastro'} |  |
| 138 | TextBlock |  |  |
| 145 | TextBlock | {Binding Nota} |  |

## Clinica · src/Clinica.Modulo.Recepcao/Views/TermosPacienteView.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Modulo.Recepcao/Views/TermosPacienteView.xaml) · 60 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 1 | UserControl |  |  |
| 2 | UserControl |  |  |
| 11 | Button | Colher um termo… | {Binding ColherTermoAvulsoCommand} |
| 12 | TextBlock | Termo do procedimento |  |
| 15 | TextBlock | {Binding ResumoTermos} |  |
| 29 | TextBlock | {Binding Nome} |  |
| 30 | TextBlock | {Binding Procedimento} |  |
| 33 | TextBlock | {Binding Situacao} |  |
| 34 | TextBlock |  |  |
| 48 | Button | Colher assinatura… | {Binding DataContext.ColherTermoCommand,                                                               RelativeSource={RelativeSource AncestorType=ItemsControl}} |

## Clinica · src/Clinica.Recepcao/App.xaml

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Recepcao/App.xaml) · 12 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |

## Clinica · src/Clinica.Web/Paginas.cs

[Fonte congelada](https://github.com/lucaszaous-creator/Clinica/blob/e6dec9abddb6242b1faaec79a48cc62c054ca79c/src/Clinica.Web/Paginas.cs) · 370 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 95 | a | {T(p.Titulo)} | {p.Rota} |
| 105 | input |  |  |
| 107 | button | Sair |  |
| 133 | h1 | Entrar |  |
| 138 | input |  |  |
| 139 | input |  |  |
| 140 | input |  |  |
| 141 | button | Entrar |  |
| 177 | h1 | O dia |  |
| 182 | input |  |  |
| 183 | button | Ver |  |
| 223 | h1 | O mês |  |
| 257 | a | {T(p.Nome)} | /paciente/{p.Id} |
| 279 | h1 | Pacientes |  |
| 281 | input |  |  |
| 282 | button | Buscar |  |
| 314 | h1 | {T(paciente.Nome)} |  |
| 318 | h2 | Próximas sessões |  |
| 333 | h2 | Prontuário |  |
| 350 | h3 | {e.Data:dd/MM/yyyy}{T(eva)}{T(cancelada)} |  |
| 358 | h2 | Prontuário |  |

## clinica-site · conteudo/404.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/404.html) · 39 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 14 | h1 | Esta página não existe |  |
| 22 | h3 |  |  |
| 22 | a | Início | / |
| 26 | h3 |  |  |
| 26 | a | Especialidades | /especialidades/ |
| 30 | h3 |  |  |
| 30 | a | Convênios | /convenios/ |
| 34 | h3 |  |  |
| 34 | a | Contato | /contato/ |

## clinica-site · conteudo/a-clinica.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/a-clinica.html) · 15 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 10 | section |  |  |
| 10 | h1 | A Clínica SemDor, em Macaé |  |
| 11 | section |  |  |
| 11 | h2 | Clínica da Dor Macaé, CDM e Clínica SemDor |  |
| 11 | a | atendimentos disponíveis | /especialidades/ |
| 11 | a | convênios | /convenios/ |
| 11 | a | contatos da recepção | /contato/ |
| 11 | a | @clinicadadormacae | https://www.instagram.com/clinicadadormacae/ |
| 12 | section |  |  |
| 12 | h2 | Áreas de atendimento |  |
| 12 | a | Conheça as especialidades e os serviços | /especialidades/ |
| 13 | section |  |  |
| 13 | h2 | Direção técnica |  |
| 13 | a | Conheça o Dr. Gustavo Lacerda | /dr-gustavo-lacerda/ |
| 14 | section |  |  |
| 14 | h2 | Atendimento em Imbetiba |  |
| 14 | a | Veja o endereço no mapa e os contatos | /contato/ |
| 15 | section |  |  |
| 15 | h2 | Informação e privacidade |  |
| 15 | a | política de privacidade | /politica-de-privacidade/ |
| 15 | a | {{ email_privacidade }} | mailto:{{ email_privacidade }} |

## clinica-site · conteudo/acessibilidade.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/acessibilidade.html) · 11 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 9 | section |  |  |
| 9 | h1 | Acessibilidade |  |
| 9 | h2 | Acesso ao local |  |
| 9 | a | endereço e o mapa | /contato/ |
| 10 | section |  |  |
| 10 | h2 | Recursos do site |  |
| 11 | section |  |  |
| 11 | h2 | Encontrou uma dificuldade? |  |
| 11 | a | {{ email }} | mailto:{{ email }} |
| 11 | a | {{ telefone }} | tel:{{ telefone_link }} |
| 11 | a | WhatsApp | {{ whatsapp_link }} |

## clinica-site · conteudo/acupuntura-medica.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/acupuntura-medica.html) · 70 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 14 | h1 | Acupuntura médica em Macaé |  |
| 17 | a | Consultar agenda pelo WhatsApp | {{ whatsapp_link }} |
| 17 | a | Endereço e telefone | /contato/ |
| 20 | section |  |  |
| 22 | h2 | Acupuntura no cuidado da dor |  |
| 23 | a | NCCIH/NIH resume essas evidências | https://www.nccih.nih.gov/health/acupuncture-effectiveness-and-safety |
| 24 | a | consulta para dor persistente | /clinica-da-dor/ |
| 27 | section |  |  |
| 29 | h2 | Como são as sessões de acupuntura e eletroacupuntura |  |
| 34 | section |  |  |
| 36 | h2 | O que considerar antes do tratamento |  |
| 42 | section |  |  |
| 44 | h2 | Dúvidas sobre acupuntura |  |
| 49 | a | convênios atendidos | /convenios/ |
| 54 | section |  |  |
| 56 | h2 | Acupuntura por convênio ou particular em Macaé |  |
| 57 | a | as orientações para usar seu convênio | /convenios/ |
| 58 | a | como chegar à clínica | /contato/ |
| 58 | a | como solicitar o agendamento | /atendimento/ |
| 60 | a | clínica da dor | /clinica-da-dor/ |
| 63 | section |  |  |
| 65 | h2 | Informações de referência |  |
| 66 | a | Acupuncture: Effectiveness and Safety, do NCCIH/NIH | https://www.nccih.nih.gov/health/acupuncture-effectiveness-and-safety |
| 67 | a | o que levar à primeira consulta | /primeira-consulta/ |
| 67 | a | os registros do diretor técnico | /dr-gustavo-lacerda/ |
| 70 | section |  |  |
| 70 | h2 | Agendar uma avaliação |  |
| 70 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 70 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/atendimento.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/atendimento.html) · 13 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 10 | section |  |  |
| 10 | h1 | Agendamento e atendimento |  |
| 10 | a | Agendar pelo WhatsApp | {{ whatsapp_link }} |
| 10 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |
| 11 | section |  |  |
| 11 | h2 | Como organizar o agendamento |  |
| 11 | h3 | Informe o atendimento que procura |  |
| 11 | h3 | Confira a forma de atendimento |  |
| 11 | a | convênios atendidos | /convenios/ |
| 11 | h3 | Guarde a confirmação |  |
| 11 | a | o que levar à consulta | /primeira-consulta/ |
| 12 | section |  |  |
| 12 | h2 | Alterações, retorno e procedimentos |  |
| 13 | section |  |  |
| 13 | h2 | Endereço e contato |  |
| 13 | a | Veja como chegar, estacionamento e acessibilidade | /contato/ |
| 13 | a | {{ email }} | mailto:{{ email }} |

## clinica-site · conteudo/bloqueio-simpatico-venoso.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/bloqueio-simpatico-venoso.html) · 59 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 10 | section |  |  |
| 13 | h1 | Bloqueio simpático venoso (BSV) |  |
| 18 | section |  |  |
| 20 | h2 | A decisão é feita em consulta |  |
| 22 | a | orientações para a primeira consulta | /primeira-consulta/ |
| 25 | section |  |  |
| 27 | h2 | Preparo para o dia agendado |  |
| 38 | section |  |  |
| 40 | h2 | Consentimento e acompanhamento |  |
| 43 | h2 | Riscos e cuidados após a sessão |  |
| 48 | section |  |  |
| 50 | h2 | Dúvidas sobre o BSV |  |
| 55 | a | página de convênios | /convenios/ |
| 59 | section |  |  |
| 59 | h2 | Converse com a equipe |  |
| 59 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 59 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/clinica-da-dor.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/clinica-da-dor.html) · 160 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 12 | section |  |  |
| 15 | h1 | Clínica da Dor em Macaé |  |
| 19 | a | Consultar agenda pelo WhatsApp | {{ whatsapp_link }} |
| 19 | a | Endereço e telefone | /contato/ |
| 23 | section |  |  |
| 25 | h2 | Quando procurar uma clínica da dor |  |
| 35 | a | fibromialgia | /fibromialgia/ |
| 42 | section |  |  |
| 44 | h2 | Como é o tratamento |  |
| 48 | h3 | 1. Avaliação |  |
| 55 | h3 | 2. Plano |  |
| 57 | a | acupuntura médica | /acupuntura/ |
| 57 | a | reabilitação | /especialidades/ |
| 61 | h3 | 3. Reavaliação |  |
| 78 | section |  |  |
| 81 | h2 | Dúvidas sobre o tratamento da dor |  |
| 87 | a | o mapa e as orientações para chegar | /contato/ |
| 94 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 94 | a | como funciona o agendamento | /atendimento/ |
| 132 | a | orientações sobre avaliação e acompanhamento da fibromialgia | /fibromialgia/ |
| 140 | section |  |  |
| 142 | h2 | Como preparar sua consulta em Macaé |  |
| 144 | a | guia da primeira consulta | /primeira-consulta/ |
| 144 | a | formação e os registros do Dr. Gustavo Lacerda | /dr-gustavo-lacerda/ |
| 145 | h3 | Convênio, particular e localização |  |
| 146 | a | confirmada com a recepção | /convenios/ |
| 146 | a | o mapa e as orientações de acesso | /contato/ |
| 151 | section |  |  |
| 153 | h2 | Marcar uma avaliação |  |
| 156 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 157 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/contato.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/contato.html) · 90 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 12 | section |  |  |
| 15 | h1 | Clínica SemDor em Macaé: contato e como chegar |  |
| 20 | h2 | Endereço |  |
| 26 | h2 | Atendimento |  |
| 28 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |
| 29 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 30 | a | {{ email }} | mailto:{{ email }} |
| 35 | a | Agendar pelo WhatsApp | {{ whatsapp_link }} |
| 43 | a | Abrir no Google Maps | {{ maps }} |
| 51 | section |  |  |
| 54 | h2 | Como chegar e ser atendido |  |

## clinica-site · conteudo/convenios.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/convenios.html) · 116 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 12 | section |  |  |
| 15 | h1 | Convênios atendidos em Macaé |  |
| 30 | section |  |  |
| 32 | h2 | Como funciona na prática |  |
| 36 | h3 | Antes de marcar |  |
| 43 | h3 | Autorização |  |
| 50 | h3 | Número de sessões |  |
| 56 | h3 | Particular |  |
| 66 | section |  |  |
| 69 | h2 | O que perguntam sobre o convênio |  |
| 106 | section |  |  |
| 108 | h2 | Conferir a cobertura do seu plano |  |
| 110 | a | consulta para avaliação da dor | /clinica-da-dor/ |
| 110 | a | sessões de acupuntura | /acupuntura/ |
| 110 | a | demais atendimentos disponíveis | /especialidades/ |
| 112 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 113 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/dr-gustavo-lacerda.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/dr-gustavo-lacerda.html) · 65 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 12 | section |  |  |
| 16 | h1 | {{ medico_nome }} |  |
| 27 | section |  |  |
| 35 | section |  |  |
| 37 | h2 | Avaliação e acompanhamento na Clínica SemDor |  |
| 42 | a | clínica da dor | /clinica-da-dor/ |
| 42 | a | acupuntura médica | /acupuntura/ |
| 42 | a | como se preparar para a primeira consulta | /primeira-consulta/ |
| 46 | section |  |  |
| 46 | h2 | Agendar com o Dr. Gustavo Lacerda em Macaé |  |
| 46 | a | os convênios atendidos | /convenios/ |
| 46 | a | o endereço completo, o mapa e os contatos | /contato/ |
| 48 | section |  |  |
| 50 | h2 | Perfis públicos do Dr. Gustavo Lacerda |  |
| 56 | section |  |  |
| 58 | h2 | Marcar uma consulta |  |
| 61 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 62 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/endocrinologia.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/endocrinologia.html) · 42 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 13 | h1 | Endocrinologia em Macaé |  |
| 16 | a | Consultar agenda pelo WhatsApp | {{ whatsapp_link }} |
| 16 | a | Endereço e telefone | /contato/ |
| 18 | section |  |  |
| 19 | h2 | O que faz o endocrinologista |  |
| 20 | a | Sociedade Brasileira de Endocrinologia e Metabologia | https://www.endocrino.org.br/2021/08/11/10-coisas-que-voce-precisa-saber-sobre-o-endocrinologista-2/ |
| 23 | section |  |  |
| 24 | h2 | Informações úteis para levar à endocrinologia |  |
| 28 | section |  |  |
| 29 | h2 | Primeira consulta e continuidade do acompanhamento |  |
| 33 | section |  |  |
| 34 | h2 | Perguntas antes da consulta |  |
| 36 | a | primeira consulta | /primeira-consulta/ |
| 37 | a | convênios atendidos | /convenios/ |
| 41 | section |  |  |
| 41 | h2 | Endereço da consulta em Macaé |  |
| 41 | a | os contatos e as orientações para chegar | /contato/ |
| 42 | section |  |  |
| 42 | h2 | Agendar consulta de endocrinologia |  |
| 42 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 42 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/especialidades.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/especialidades.html) · 112 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 12 | section |  |  |
| 15 | h1 | Especialidades e serviços em Macaé |  |
| 20 | h2 | Áreas de atendimento médico |  |
| 28 | a | avaliação e acompanhamento de fibromialgia | /fibromialgia/ |
| 29 | h2 | Consultas em Macaé para quem mora na região |  |
| 30 | a | como chegar à clínica | /contato/ |
| 34 | section |  |  |
| 36 | h2 | Outros serviços da clínica |  |
| 49 | section |  |  |
| 52 | h2 | O que perguntam antes de escolher |  |
| 94 | a | página de convênios | /convenios/ |
| 103 | section |  |  |
| 105 | h2 | Não sabe qual consulta marcar? |  |
| 108 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 109 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/fibromialgia.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/fibromialgia.html) · 50 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 13 | h1 | Fibromialgia em Macaé |  |
| 16 | a | Consultar agenda pelo WhatsApp | {{ whatsapp_link }} |
| 16 | a | Endereço e telefone | /contato/ |
| 18 | section |  |  |
| 19 | h2 | O que é fibromialgia |  |
| 21 | a | NIAMS, instituto de saúde dos Estados Unidos, apresenta uma visão geral da fibromialgia | https://www.niams.nih.gov/health-topics/fibromyalgia |
| 23 | section |  |  |
| 24 | h2 | Como é feita a avaliação |  |
| 26 | a | como organizar os documentos para a primeira consulta | /primeira-consulta/ |
| 28 | section |  |  |
| 29 | h2 | Tratamento e continuidade do cuidado |  |
| 31 | a | NIAMS explica a investigação e as diferentes frentes do tratamento | https://www.niams.nih.gov/health-topics/fibromyalgia/diagnosis-treatment-and-steps-to-take |
| 31 | a | consulta de acompanhamento da dor | /clinica-da-dor/ |
| 31 | a | serviços disponíveis | /especialidades/ |
| 33 | section |  |  |
| 34 | h2 | Dúvidas sobre a consulta de fibromialgia |  |
| 38 | a | as orientações sobre convênios | /convenios/ |
| 42 | section |  |  |
| 43 | h2 | Onde fica a clínica e como organizar sua visita |  |
| 44 | a | o mapa, os contatos e as informações de acesso | /contato/ |
| 46 | section |  |  |
| 47 | h2 | Agendar avaliação de fibromialgia em Macaé |  |
| 49 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 49 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/geriatria.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/geriatria.html) · 43 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 13 | h1 | Geriatria em Macaé |  |
| 16 | a | Consultar agenda pelo WhatsApp | {{ whatsapp_link }} |
| 16 | a | Endereço e telefone | /contato/ |
| 18 | section |  |  |
| 19 | h2 | O papel do geriatra no acompanhamento |  |
| 20 | a | Sociedade Brasileira de Geriatria e Gerontologia explica esses conceitos em seu guia sobre envelhecimento | https://sbgg.org.br/wp-content/uploads/2023/11/1700223168_Guia_para_jornalistas_na_cobertura_do_envelhecimento.pdf |
| 23 | section |  |  |
| 24 | h2 | O que conversar na consulta de geriatria |  |
| 26 | a | atendimento para avaliação da dor | /clinica-da-dor/ |
| 28 | section |  |  |
| 29 | h2 | Como a pessoa idosa e o acompanhante podem se preparar |  |
| 33 | section |  |  |
| 34 | h2 | Dúvidas sobre o agendamento |  |
| 38 | a | as informações de acessibilidade | /acessibilidade/ |
| 39 | a | lista de convênios | /convenios/ |
| 42 | section |  |  |
| 42 | h2 | Consulta de geriatria em Imbetiba |  |
| 42 | a | como chegar à Clínica SemDor | /contato/ |
| 43 | section |  |  |
| 43 | h2 | Agendar consulta de geriatria |  |
| 43 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 43 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/ginecologia.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/ginecologia.html) · 42 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 13 | h1 | Ginecologia em Macaé |  |
| 16 | a | Consultar agenda pelo WhatsApp | {{ whatsapp_link }} |
| 16 | a | Endereço e telefone | /contato/ |
| 18 | section |  |  |
| 19 | h2 | Assuntos para conversar com o ginecologista |  |
| 21 | a | Ministério da Saúde reúne orientações sobre saúde da mulher | https://www.gov.br/saude/pt-br/assuntos/saude-de-a-a-z/s/saude-da-mulher |
| 23 | section |  |  |
| 24 | h2 | Como se preparar para a consulta de ginecologia |  |
| 28 | section |  |  |
| 29 | h2 | Consulta, exames e retornos |  |
| 33 | section |  |  |
| 34 | h2 | Dúvidas sobre ginecologia na clínica |  |
| 36 | a | planos atendidos | /convenios/ |
| 37 | a | a orientação para a primeira consulta | /primeira-consulta/ |
| 41 | section |  |  |
| 41 | h2 | Localização do atendimento |  |
| 41 | a | como chegar e falar com a recepção | /contato/ |
| 42 | section |  |  |
| 42 | h2 | Agendar consulta de ginecologia |  |
| 42 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 42 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/inicio.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/inicio.html) · 79 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 9 | section |  |  |
| 13 | h1 | Clínica SemDor |  |
| 16 | a | clínica da dor | /clinica-da-dor/ |
| 16 | a | acupuntura médica | /acupuntura/ |
| 17 | a | Conheça a clínica | /a-clinica/ |
| 21 | h2 | Sua próxima consulta |  |
| 23 | a | Agendar uma consulta | /atendimento/ |
| 24 | a | {{ telefone }} | tel:{{ telefone_link }} |
| 27 | a | Endereço e como chegar | /contato/ |
| 35 | a |  | /primeira-consulta/ |
| 36 | a |  | /convenios/ |
| 37 | a |  | /contato/ |
| 40 | section |  |  |
| 42 | h2 | Cuidado com a dor. |  |
| 44 | h3 |  |  |
| 44 | a | Clínica da Dor | /clinica-da-dor/ |
| 44 | a | Conheça o atendimento | /clinica-da-dor/ |
| 45 | h3 |  |  |
| 45 | a | Acupuntura médica | /acupuntura/ |
| 45 | a | Sobre as sessões | /acupuntura/ |
| 46 | h3 |  |  |
| 46 | a | Especialidades e serviços | /especialidades/ |
| 46 | a | Psiquiatria | /psiquiatria/ |
| 46 | a | Geriatria | /geriatria/ |
| 46 | a | Endocrinologia | /endocrinologia/ |
| 46 | a | Ginecologia | /ginecologia/ |
| 46 | a | Ver todos os atendimentos | /especialidades/ |
| 48 | a | fibromialgia em Macaé | /fibromialgia/ |
| 52 | section |  |  |
| 55 | h2 | Quem responde |  |
| 55 | a | Registro profissional e formação | /dr-gustavo-lacerda/ |
| 55 | a | Orientações de acesso | /contato/ |
| 59 | section |  |  |
| 61 | h2 | Consulta por convênio ou particular |  |
| 63 | a | os convênios atendidos em Macaé | /convenios/ |
| 63 | a | como agendar uma avaliação | /atendimento/ |
| 67 | section |  |  |
| 69 | h2 | O que você precisa saber. |  |
| 69 | a | Acesse o guia do paciente | /para-pacientes/ |
| 71 | a | a identificação e os atendimentos da clínica | /a-clinica/ |
| 72 | a | WhatsApp | {{ whatsapp_link }} |
| 72 | a | {{ telefone }} | tel:{{ telefone_link }} |
| 72 | a | as orientações de agendamento | /atendimento/ |
| 73 | a | os convênios e documentos necessários | /convenios/ |
| 74 | a | como chegar à Clínica SemDor | /contato/ |
| 79 | section |  |  |
| 79 | h2 | Fale com a recepção. |  |
| 79 | a | Conversar pelo WhatsApp | {{ whatsapp_link }} |
| 79 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/mapa-do-site.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/mapa-do-site.html) · 8 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 8 | section |  |  |
| 8 | h1 | Mapa do site |  |

## clinica-site · conteudo/para-pacientes.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/para-pacientes.html) · 12 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 9 | section |  |  |
| 9 | h1 | Guia do paciente |  |
| 9 | h2 |  |  |
| 9 | a | Agendamento | /atendimento/ |
| 9 | a | Organizar o atendimento | /atendimento/ |
| 9 | h2 |  |  |
| 9 | a | Primeira consulta | /primeira-consulta/ |
| 9 | a | Veja o que levar | /primeira-consulta/ |
| 9 | h2 |  |  |
| 9 | a | Convênios e particular | /convenios/ |
| 9 | a | Conferir os convênios | /convenios/ |
| 9 | h2 |  |  |
| 9 | a | Como chegar | /contato/ |
| 9 | a | Planejar o deslocamento | /contato/ |
| 10 | section |  |  |
| 10 | h2 | Portal do paciente |  |
| 10 | a | Acessar o portal do paciente | {{ portal_paciente }} |
| 11 | section |  |  |
| 11 | h2 | Consulta e procedimento têm orientações diferentes |  |
| 11 | a | página sobre o BSV | /bloqueio-simpatico-venoso/ |
| 11 | a | as informações sobre as sessões | /acupuntura/ |
| 12 | section |  |  |
| 12 | h2 | Precisa de apoio para a visita? |  |
| 12 | a | recursos de acessibilidade do site e do local | /acessibilidade/ |
| 12 | a | política de privacidade | /politica-de-privacidade/ |
| 12 | a | Falar com a recepção | {{ whatsapp_link }} |

## clinica-site · conteudo/politica-de-privacidade.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/politica-de-privacidade.html) · 43 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 13 | h1 | Política de Privacidade |  |
| 17 | section |  |  |
| 18 | h2 | Quem responde pelos seus dados |  |
| 21 | a | {{ email_privacidade }} | mailto:{{ email_privacidade }} |
| 22 | h2 | Ao visitar este site |  |
| 27 | h2 | Ao entrar em contato |  |
| 31 | section |  |  |
| 32 | h2 | Dados do atendimento |  |
| 35 | h2 | Armazenamento e conservação |  |
| 38 | h2 | Como exercer seus direitos |  |
| 41 | h2 | Dúvidas e atualizações |  |
| 42 | a | {{ email_privacidade }} | mailto:{{ email_privacidade }} |

## clinica-site · conteudo/primeira-consulta.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/primeira-consulta.html) · 94 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 14 | h1 | A primeira consulta |  |
| 19 | h2 | O que levar |  |
| 30 | section |  |  |
| 32 | h2 | Como é a avaliação |  |
| 54 | section |  |  |
| 57 | h2 | O que perguntam antes de vir |  |
| 68 | a | informações sobre o preparo do BSV | /bloqueio-simpatico-venoso/ |
| 85 | section |  |  |
| 87 | h2 | Marcar a primeira consulta |  |
| 90 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 91 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · conteudo/psiquiatria.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/conteudo/psiquiatria.html) · 43 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | section |  |  |
| 13 | h1 | Psiquiatria em Macaé |  |
| 16 | a | Consultar agenda pelo WhatsApp | {{ whatsapp_link }} |
| 16 | a | Endereço e telefone | /contato/ |
| 18 | section |  |  |
| 19 | h2 | O que conversar com o psiquiatra |  |
| 21 | a | NIMH, instituto de saúde mental dos Estados Unidos | https://www.nimh.nih.gov/health/topics/caring-for-your-mental-health |
| 23 | section |  |  |
| 24 | h2 | Como organizar as informações para a consulta |  |
| 26 | a | o que levar à primeira consulta | /primeira-consulta/ |
| 28 | section |  |  |
| 29 | h2 | Psiquiatra ou psicólogo: qual é a diferença? |  |
| 31 | a | Associação Americana de Psiquiatria explica essas diferenças | https://www.psychiatry.org/patients-families/what-is-psychiatry |
| 31 | a | psicologia e os demais serviços | /especialidades/ |
| 34 | section |  |  |
| 35 | h2 | Dúvidas antes de agendar psiquiatria |  |
| 38 | a | planos atendidos | /convenios/ |
| 42 | section |  |  |
| 42 | h2 | Onde fica o atendimento |  |
| 42 | a | endereço, contatos e como chegar | /contato/ |
| 43 | section |  |  |
| 43 | h2 | Agendar consulta de psiquiatria |  |
| 43 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 43 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |

## clinica-site · modelos/base.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/modelos/base.html) · 73 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 45 | a | Pular para o conteúdo | #conteudo |
| 49 | a | {{ nome }} — página inicial | / |
| 59 | a | Agendar consulta | /atendimento/ |

## clinica-site · modelos/rodape.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/modelos/rodape.html) · 57 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | a | Como chegar | /contato/ |
| 16 | h2 | Contato |  |
| 18 | a | Ligar: {{ telefone }} | tel:{{ telefone_link }} |
| 19 | a | WhatsApp {{ whatsapp }} | {{ whatsapp_link }} |
| 20 | a | {{ email }} | mailto:{{ email }} |
| 27 | h2 | Atendimento |  |
| 29 | a | A clínica | /a-clinica/ |
| 30 | a | Clínica da Dor em Macaé | /clinica-da-dor/ |
| 31 | a | Acupuntura médica | /acupuntura/ |
| 32 | a | Especialidades | /especialidades/ |
| 33 | a | Convênios | /convenios/ |
| 34 | a | Guia do paciente | /para-pacientes/ |
| 35 | a | Portal do paciente | {{ portal_paciente }} |
| 36 | a | Agendamento | /atendimento/ |
| 37 | a | Dr. Gustavo Lacerda | /dr-gustavo-lacerda/ |
| 52 | a | Política de Privacidade | /politica-de-privacidade/ |
| 53 | a | Acessibilidade | /acessibilidade/ |
| 54 | a | Mapa do site | /mapa-do-site/ |

## clinica-site · portal/index.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/index.html) · 20 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 13 | a | Clínica SemDor — voltar ao site | https://clinicasemdormacae.com.br/ |
| 13 | a | Voltar ao site | https://clinicasemdormacae.com.br/ |
| 14 | a | Consultório · atendimento pelo tablet | /profissional/ |
| 16 | section | Conferência de acesso |  |

## clinica-site · portal/portal.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/portal.js) · 219 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 30 | h1 | Acesso protegido |  |
| 30 | button | Entrar |  |
| 61 | h1 | Vamos reconectar |  |
| 61 | button | Tentar novamente |  |
| 86 | h2 | Vamos reconectar |  |
| 86 | button | Tentar novamente |  |
| 98 | section | Situação das assinaturas |  |
| 98 | h2 | ${completos?'Termos assinados':'Acompanhe os termos'} |  |
| 98 | h3 | ${esc(t.nome)} |  |
| 98 | a | Abrir via assinada | /api/documentos/${Number(t.documentoId)}/via |
| 101 | h3 | ${esc(d.titulo)} |  |
| 101 | a | Abrir via assinada | /api/documentos/${d.id}/via |
| 101 | button | Retomar arquivamento |  |
| 104 | section |  |  |
| 104 | h1 | Portal do paciente |  |
| 104 | input |  |  |
| 104 | input |  |  |
| 104 | input |  |  |
| 104 | button | Entrar com segurança |  |
| 109 | h1 | Termos de hoje |  |
| 109 | button | Sair |  |
| 109 | input |  |  |
| 109 | button | Buscar |  |
| 109 | button | BSV de hoje |  |
| 109 | h2 | BSV de hoje |  |
| 120 | button | ${agenda?` |  |
| 126 | h1 | ${esc(p.nome)} |  |
| 126 | button | Trocar paciente |  |
| 126 | h2 | Antes de entregar |  |
| 126 | section |  |  |
| 126 | h2 | Preparar nova coleta |  |
| 126 | input |  |  |
| 126 | input |  |  |
| 126 | h3 | Documentos para assinatura |  |
| 126 | input |  |  |
| 126 | button | !m.coberto)?'disabled':''}>Entregar tablet ao paciente |  |
| 155 | h2 | ${esc(d.nome)} |  |
| 155 | button | Chamar a enfermeira |  |
| 155 | section |  |  |
| 155 | h1 | ${esc(d.titulo)} |  |
| 155 | h2 | Suas respostas |  |
| 155 | input | ${r} |  |
| 155 | textarea |  |  |
| 155 | h2 | Sua rubrica |  |
| 155 | button | Limpar rubrica |  |
| 155 | input |  |  |
| 155 | button | Confirmar e assinar |  |
| 155 | button | Não desejo assinar |  |
| 191 | section |  |  |
| 191 | h1 | ${recusa?'Você pode recusar a assinatura.':'Chame a enfermeira.'} |  |
| 191 | textarea |  |  |
| 191 | button | ${recusa?'Registrar recusa e encerrar':'Encerrar leitura e chamar equipe'} |  |
| 191 | button | Continuar lendo |  |
| 196 | section |  |  |
| 196 | h1 | ${esc(titulo)} |  |
| 196 | button | Retornar à equipe |  |
| 202 | section |  |  |
| 202 | h1 | ${concluido?'Assinado e arquivado':falha?'Assinatura recebida':'Assinaturas recebidas'} |  |
| 202 | h2 | ${esc(c.titulo\|\|'Termo assinado')} |  |
| 202 | button | Retornar à equipe |  |
| 202 | button | Conferir situação |  |

## clinica-site · portal/profissional/clinico.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/clinico.js) · 395 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 88 | section |  |  |
| 88 | h1 | Seu consultório, onde você atende. |  |
| 88 | input |  |  |
| 88 | input |  |  |
| 88 | input |  |  |
| 88 | button | Entrar no consultório |  |
| 108 | h1 | Meu dia |  |
| 108 | button | Hoje |  |
| 108 | input |  |  |
| 108 | button | Atualizar |  |
| 108 | section | Seus atendimentos |  |
| 108 | h2 | ${esc(h.nome)} |  |
| 108 | button | ${estado.permissoes?.atender===false?'Ver ficha':h.finalizado?'Ver atendimento':'Atender'} |  |
| 108 | h2 | Agenda livre nesta data |  |
| 108 | section | Áreas de trabalho |  |
| 108 | h2 | Continue seu trabalho |  |
| 108 | button | ${iconesAtalho.fila} |  |
| 108 | button | ${iconesAtalho.documentos} |  |
| 108 | button | ${iconesAtalho.pacientes} |  |
| 114 | button | ← Meu dia |  |
| 114 | h1 | ${esc(r.paciente.nome)} |  |
| 114 | button | Ficha completa |  |
| 114 | button | ${r.novoBsv.indicado?'Em acompanhamento BSV':'Novo paciente de BSV'} |  |
| 114 | section |  |  |
| 114 | button | ${t} |  |
| 114 | section | ${conteudoAba()} |  |
| 114 | button | Salvar e concluir atendimento |  |
| 114 | button | Registrar materiais |  |
| 120 | textarea | ${esc(estado.rascunho[k])} |  |
| 123 | button | Copiar última evolução |  |
| 123 | select |  |  |
| 123 | input | EVA ${t.toLowerCase()}, de zero a dez |  |
| 123 | input |  |  |
| 124 | h2 | Evoluções anteriores |  |
| 124 | h3 | ${esc(h.profissional\|\|'Profissional não informado')} |  |
| 124 | button | Copiar para esta sessão |  |
| 124 | button | Copiar mapa corporal |  |
| 127 | h2 | Prescrições e documentos |  |
| 127 | button | Nova prescrição |  |
| 127 | h3 | ${dia(d.data)} · ${d.situacao==='Devolvida'?'Devolvida à enfermagem':d.assinadoEm?'Assinado':'Não assinado'} |  |
| 127 | button | ${d.classe==='infusao'?rotuloPrescricao(d):'Abrir PDF'} |  |
| 127 | button | Corrigir rascunho |  |
| 127 | button | Cancelar rascunho |  |
| 127 | button | Copiar |  |
| 127 | button | a.papel==='Executante'&&a.registroArquivado))?'disabled':''}>${d.origemEnfermagem?'Validar e assinar como médico':'Assinar com SafeID'} |  |
| 127 | h3 | Nenhuma prescrição registrada. |  |
| 133 | h2 | ${titulo} |  |
| 133 | button | Fechar |  |
| 134 | button | Voltar |  |
| 134 | button | '+esc(acao)+' |  |
| 182 | select | Documento |  |
| 182 | input |  |  |
| 182 | button | Salvar texto como modelo |  |
| 182 | textarea | Prescrição em texto livre |  |
| 182 | input | Adicionar orientações |  |
| 182 | input | Adicionar observações |  |
| 182 | input |  |  |
| 182 | input |  |  |
| 182 | select | ${vias.map(([v,t],i)=>` |  |
| 182 | input |  |  |
| 182 | input |  |  |
| 182 | input |  |  |
| 182 | input | A enfermagem assina eletronicamente a execução. |  |
| 182 | input |  |  |
| 182 | button | Voltar |  |
| 182 | button | Emitir documento |  |
| 208 | button | Fechar imagem |  |
| 226 | button | Página anterior |  |
| 226 | button | Próxima página |  |
| 226 | button | Fechar documento |  |
| 226 | button | Imprimir PDF |  |
| 265 | input | ${tipo==='execucao'?'Revisei a execução registrada e confirmo minha assinatura.':'Revisei o conteúdo e as alergias registradas e confirmo esta prescrição.'} |  |
| 265 | button | Voltar |  |
| 265 | button | Autorizar no SafeID |  |
| 274 | input |  |  |
| 274 | button | Salvar endereço no cadastro |  |
| 280 | button | Conferir PDF atualizado |  |
| 305 | h2 | Mapa corporal |  |
| 305 | select | ${tecnicas.map((t,i)=>` |  |
| 305 | input |  |  |
| 305 | select |  |  |
| 305 | button | Guardar pontos como modelo |  |
| 305 | textarea | ${esc(m.observacoes)} |  |
| 305 | h3 | ${m.pontos.length} ponto(s) marcado(s) |  |
| 305 | button | Remover ${esc(p.nome\|\|'ponto '+(i+1))} |  |
| 305 | select |  |  |
| 305 | input |  |  |
| 305 | input |  |  |
| 305 | button | Adicionar ponto |  |
| 312 | input |  |  |
| 312 | button | Voltar |  |
| 312 | button | Guardar modelo |  |

## clinica-site · portal/profissional/coleta.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/coleta.html) · 21 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 14 | a | Clínica SemDor — voltar ao site | https://clinicasemdormacae.com.br/ |
| 14 | a | Voltar ao site | https://clinicasemdormacae.com.br/ |
| 15 | a | Consultório · atendimento pelo tablet | /profissional/ |
| 17 | section | Conferência de acesso |  |

## clinica-site · portal/profissional/execucao-direta.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/execucao-direta.js) · 41 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 6 | section | Conferência da execução |  |
| 6 | h2 | Execução de enfermagem |  |
| 6 | input |  |  |
| 6 | input | Conferi paciente, prescrição e alergias. |  |
| 7 | input | Hora de ${esc(i.descricao)} |  |
| 7 | input | ${t} |  |
| 34 | h3 | ${esc(i.descricao)} |  |
| 34 | textarea |  |  |
| 34 | button | Voltar |  |
| 34 | button | Salvar justificativa |  |

## clinica-site · portal/profissional/ficha-edicao.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/ficha-edicao.js) · 71 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 12 | textarea | ${esc(rotulo)} |  |
| 13 | input | ${esc(rotulo)} |  |
| 16 | button | Voltar |  |
| 16 | button | '+botao+' |  |
| 26 | h3 | Versão ${esc(v.versao)} · ${dia(v.substituidaEm)} |  |
| 26 | h4 | ${t} |  |
| 26 | button | Fechar histórico |  |
| 29 | select | Medida |  |
| 36 | select | Natureza |  |
| 39 | select | Nova situação |  |
| 50 | input | Houve intercorrência |  |
| 57 | input | Exigir assinatura eletrônica da enfermagem |  |
| 57 | select | Via |  |
| 57 | input | Se necessário |  |

## clinica-site · portal/profissional/fila-enfermagem.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/fila-enfermagem.js) · 46 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 17 | select | Situação |  |
| 18 | h1 | ${historico?'Sessões anteriores':'Sessões de hoje'} |  |
| 19 | section |  |  |
| 20 | button | Hoje |  |
| 21 | button | Sessões anteriores |  |
| 24 | input |  |  |
| 25 | button | Filtrar sessões |  |
| 25 | button | Limpar filtros |  |
| 26 | section |  |  |
| 26 | h2 | ${esc(s.paciente)} |  |
| 26 | button | ${s.registrada?'Ver registros':s.chegadaRegistrada?'Registrar após aplicação':'Registrar chegada'} |  |
| 26 | button | Ver sessões anteriores |  |
| 27 | button | Anterior |  |
| 27 | button | Próxima |  |

## clinica-site · portal/profissional/index.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/index.html) · 48 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 28 | a | Pular para o conteúdo | #conteudo |
| 30 | a | Clínica SemDor — meu dia | /profissional/ |
| 32 | button | Áreas |  |
| 34 | button |  |  |
| 34 | button |  |  |
| 34 | button |  |  |
| 34 | button |  |  |
| 35 | button |  |  |
| 35 | button |  |  |
| 35 | button |  |  |
| 36 | a |  | / |
| 37 | a |  | /profissional/treinamento/ |
| 39 | button | Sair |  |
| 44 | section | Acesso protegido |  |
| 44 | h1 | Seu atendimento está protegido. |  |
| 44 | button | Continuar atendimento |  |
| 45 | dialog |  |  |

## clinica-site · portal/profissional/materiais.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/materiais.js) · 60 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 31 | input |  |  |
| 31 | input |  |  |
| 31 | button | Outro lote deste produto |  |
| 33 | input |  |  |
| 33 | input | Confirmo que não houve consumo de materiais nesta sessão |  |
| 33 | button | Fechar |  |
| 33 | button | ${dados.registrado?'Tentar baixa novamente':'Registrar materiais'} |  |

## clinica-site · portal/profissional/modelos-documento.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/modelos-documento.js) · 82 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 11 | h3 | Buscar um modelo |  |
| 11 | input |  |  |
| 11 | button | Usar modelo |  |
| 11 | button | Acrescentar ao texto |  |
| 19 | input |  |  |
| 19 | input |  |  |
| 19 | input | Se necessário |  |
| 19 | textarea |  |  |
| 22 | textarea |  |  |
| 52 | textarea | ${esc(indicacao)} |  |
| 52 | textarea | ${esc(observacoes)} |  |
| 54 | textarea | ${esc(item.descricao)} |  |
| 54 | input |  |  |
| 54 | select | ${vias.map(([v,t],i)=>` |  |
| 54 | input | Se necessário |  |
| 54 | textarea | ${esc(item.observacoes)} |  |

## clinica-site · portal/profissional/modelos.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/modelos.js) · 89 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 19 | h1 | Modelos |  |
| 20 | section |  |  |
| 20 | button | ${esc(nome)} |  |
| 38 | section | Modelos disponíveis |  |
| 38 | input |  |  |
| 38 | button | Novo modelo |  |
| 39 | section | Editor de modelo |  |
| 51 | h2 | ${m?'Editar modelo':'Novo modelo'} |  |
| 52 | input |  |  |
| 53 | textarea | ${esc(m?.texto)} |  |
| 54 | input | Compartilhar com a equipe |  |
| 54 | textarea | ${esc(m?.[id])} |  |
| 55 | button | Cancelar |  |
| 55 | button | Salvar modelo |  |
| 55 | button | Arquivar |  |
| 80 | select | ${[['receita','Receituário'],['infusao','Prescrição de infusão'],['atestado','Atestado'],['exame','Pedido de exame'],['comparecimento','Declaração de comparecimento'],['relatorio','Relatório clínico'],['anamnese','Anamnese']].map(([id,nome])=>` |  |
| 81 | input |  |  |
| 81 | button | Salvar novo modelo |  |
| 82 | textarea |  |  |
| 83 | select | ${vias.map(([id,nome],i)=>` |  |
| 83 | input |  |  |
| 83 | input |  |  |
| 83 | input |  |  |

## clinica-site · portal/profissional/observacoes-enfermagem.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/observacoes-enfermagem.js) · 155 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 9 | button | ← Voltar à agenda |  |
| 10 | h1 | ${esc(nome)} |  |
| 11 | section |  |  |
| 13 | button | Evolução |  |
| 14 | button | Termos de consentimento |  |
| 16 | section |  |  |
| 17 | section |  |  |
| 19 | h2 | Momento do atendimento |  |
| 20 | button | 1 · Chegada |  |
| 20 | button | 2 · Após aplicação |  |
| 21 | h2 | Sinais vitais e horário |  |
| 22 | h2 | Evolução de enfermagem |  |
| 22 | button | Gerenciar modelos de evolução |  |
| 22 | input |  |  |
| 22 | button | Aplicar modelo |  |
| 23 | button | Copiar evolução da chegada |  |
| 25 | h2 | Complementos |  |
| 25 | button | Adicionar intercorrência |  |
| 26 | input | Sim |  |
| 26 | input | Não |  |
| 28 | button | Adicionar observação |  |
| 30 | button | Salvar chegada |  |
| 111 | button | Remover |  |

## clinica-site · portal/profissional/posto.js

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/posto.js) · 247 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 12 | h3 | ${esc(t)} |  |
| 23 | section |  |  |
| 23 | h2 | Acompanhamento da infusão |  |
| 27 | section |  |  |
| 27 | h2 | Revisar e reenviar |  |
| 29 | section |  |  |
| 29 | h2 | Próxima etapa |  |
| 32 | section |  |  |
| 32 | h2 | Próxima etapa |  |
| 35 | section |  |  |
| 35 | h2 | Próxima etapa da enfermagem |  |
| 40 | section |  |  |
| 40 | h2 | Como executar esta infusão |  |
| 110 | h1 | ${externa?'Infusão com orientação externa':'Pacientes'} |  |
| 110 | section |  |  |
| 110 | input |  |  |
| 110 | button | Buscar paciente |  |
| 111 | h2 | ${esc(p.nome)} |  |
| 111 | button | ${externa?'Registrar infusão':'Abrir ficha'} |  |
| 132 | button | ← Pacientes |  |
| 132 | h1 | ${esc(p.nome)} |  |
| 132 | button | Atender agora |  |
| 132 | button | Emitir documento |  |
| 132 | button | Registrar infusão externa realizada |  |
| 132 | button | Atualizar ficha |  |
| 132 | section |  |  |
| 132 | button | ${t} |  |
| 132 | section | ${conteudoFicha(f,e.abaFicha)} |  |
| 132 | button | Mais recentes |  |
| 132 | button | Mais antigos |  |
| 141 | button | Colher / revisar anamnese |  |
| 141 | button | Registrar problema ou alergia |  |
| 141 | button | Registrar medida |  |
| 141 | button | Histórico da anamnese |  |
| 141 | h2 | Dados cadastrais |  |
| 141 | h2 | Anamnese |  |
| 141 | h2 | Problemas, alergias e medicações |  |
| 141 | button | ${a.tipo==='Alergia'?'Editar alergia':'Editar registro'} |  |
| 141 | button | ${a.tipo==='Alergia'?'Revisar / remover alerta':'Alterar situação'} |  |
| 141 | h2 | Medidas e avaliações |  |
| 141 | button | Cancelar medida incorreta |  |
| 142 | h2 | Sessões e atendimentos |  |
| 142 | h3 | ${esc(s.modalidade)} · ${esc(s.situacao)} |  |
| 142 | button | ${s.situacao==='Realizado'?'Ver atendimento':'Continuar atendimento'} |  |
| 143 | h2 | Histórico clínico |  |
| 143 | h3 | ${esc(h.profissional\|\|'Profissional não informado')} |  |
| 143 | button | Ver mapa corporal |  |
| 144 | h2 | Resultados de exames |  |
| 144 | button | Registrar resultado de exame |  |
| 144 | h3 | ${esc(x.nome)} |  |
| 144 | h2 | Avaliações clínicas |  |
| 144 | h3 | ${esc(x.instrumentoNome)} |  |
| 145 | h2 | Documentos do prontuário |  |
| 145 | h3 | ${dia(d.data)} · ${d.canceladaEm?'Cancelada':d.situacao==='Devolvida'?'Devolvida à enfermagem':d.pacienteAssinadoEm?'Assinado pelo paciente':d.assinadoEm?'Assinado':'Não assinado'} |  |
| 145 | button | ${d.classe==='infusao'?rotuloPrescricao(d):'Abrir PDF'} |  |
| 145 | button | ${d.situacao==='Devolvida'?'Revisar devolução':'Revisar ou cancelar'} |  |
| 145 | button | Corrigir rascunho |  |
| 145 | button | Cancelar rascunho |  |
| 145 | button | Copiar |  |
| 145 | button | a.papel==='Executante'&&a.registroArquivado))?'disabled':''}>${d.origemEnfermagem?'Validar e assinar como médico':'Assinar com SafeID'} |  |
| 146 | h2 | Registros de enfermagem |  |
| 146 | button | Registrar evolução de enfermagem |  |
| 146 | h3 | ${esc(n.autorNome)} · ${esc(n.autorConselho)} |  |
| 146 | button | Vincular à sessão |  |
| 146 | h3 | Diagnóstico: ${esc(d.titulo)} |  |
| 146 | h3 | Cuidado: ${esc(c.descricao)} |  |
| 146 | button | Retificar evolução |  |
| 147 | h2 | Anexos |  |
| 147 | button | Enviar anexo |  |
| 147 | h3 | ${esc(a.titulo\|\|a.nomeArquivo)} |  |
| 147 | button | Abrir anexo PDF |  |
| 147 | button | Abrir imagem |  |
| 152 | select | ${opcoes.map(m=>` |  |
| 152 | select |  |  |
| 152 | textarea |  |  |
| 152 | button | Voltar |  |
| 152 | button | Iniciar atendimento |  |
| 190 | button | Todas as etapas |  |
| 190 | button | ${rotulo} · ${Number(valor)\|\|0} |  |
| 194 | section | ${titulo} |  |
| 194 | h2 | ${titulo} |  |
| 194 | h3 | ${esc(p.paciente)} |  |
| 194 | button | ${acaoDaFila(p)} |  |
| 197 | h1 | Fila de infusões |  |
| 197 | button | Atualizar fila |  |
| 197 | button | Localizar paciente · orientação externa |  |
| 197 | section |  |  |
| 197 | button | Anterior |  |
| 197 | button | Próxima |  |
| 208 | button | ← ${e.origemInfusao==='pendencias'?'Minhas pendências':'Fila de infusões'} |  |
| 208 | h1 | ${esc(p.paciente)} |  |
| 208 | button | Ficha completa |  |
| 208 | button | ${rotuloPrescricao(p)} |  |
| 208 | button | Folha de checagens da enfermagem |  |
| 208 | button | Corrigir horários |  |
| 208 | button | Revisar devolução |  |
| 208 | button | Cancelar registro |  |
| 208 | section | ${passosInfusao(p,e)}${assinaturasInfusao(p,esc,dia,hora)} |  |
| 208 | h2 | ${esc(i.descricao)} |  |
| 208 | button | Retificar registro |  |
| 208 | button | Avaliar e assinar com SafeID |  |
| 208 | button | Devolver à enfermagem |  |
| 208 | button | Encerrar execução |  |
| 208 | button | Assinar execução com SafeID |  |
| 210 | h3 | ${esc(i.descricao)} |  |
| 210 | select |  |  |
| 210 | input |  |  |
| 210 | input |  |  |
| 210 | textarea |  |  |
| 210 | textarea |  |  |
| 210 | input |  |  |
| 210 | input | Conferi paciente, prescrição e alergias e estou registrando o que ocorreu. |  |
| 210 | button | Voltar |  |
| 210 | button | Salvar registro |  |
| 218 | input |  |  |
| 218 | input |  |  |
| 218 | input |  |  |
| 218 | input |  |  |
| 218 | textarea |  |  |
| 218 | button | Voltar |  |
| 218 | button | Salvar correção |  |
| 218 | textarea |  |  |
| 218 | button | Voltar |  |
| 218 | button | Devolver à enfermagem |  |
| 218 | textarea |  |  |
| 218 | button | Voltar |  |
| 218 | button | Cancelar registro |  |
| 218 | button | Fechar mapa |  |
| 222 | h1 | Documentos para assinar |  |
| 222 | button | Atualizar |  |
| 222 | button | Localizar paciente |  |
| 222 | section |  |  |
| 222 | h2 | ${esc(d.paciente)} |  |
| 222 | button | Abrir este documento |  |
| 222 | button | Anterior |  |
| 222 | button | Próxima |  |
| 227 | h1 | Minhas pendências |  |
| 227 | button | Atualizar |  |
| 227 | section |  |  |
| 227 | h2 | Atendimentos sem conclusão clínica |  |
| 227 | h3 | ${esc(s.paciente)} |  |
| 227 | button | Conferir atendimento |  |
| 227 | h2 | Documentos sem assinatura |  |
| 227 | h3 | ${esc(d.paciente)} |  |
| 227 | button | Conferir este documento |  |
| 227 | h2 | Conferência da recepção |  |
| 227 | h3 | ${esc(a.paciente)} |  |
| 227 | button | Ver protocolo e guias |  |
| 227 | h2 | Enfermagem |  |
| 227 | button | Ver infusões para executar ou assinar |  |
| 227 | button | Anterior |  |
| 227 | button | Próxima |  |
| 240 | button | ← Voltar à agenda |  |
| 240 | button | Imprimir registro |  |
| 240 | h1 | Registro de enfermagem |  |
| 240 | section |  |  |
| 240 | h2 | ${esc(f.paciente.nome)} |  |

## clinica-site · portal/profissional/treinamento/index.html

[Fonte congelada](https://github.com/lucaszaous-creator/clinica-site/blob/cc45636a9c8894605b81c39fffe84333e6065553/portal/profissional/treinamento/index.html) · 44 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 13 | a | Pular para as aulas | #conteudo |
| 15 | a | Voltar ao consultório | /profissional/ |
| 16 | a | ← Voltar ao consultório | /profissional/ |
| 20 | h1 | Aprenda cada função do consultório. |  |
| 26 | input |  |  |
| 30 | section |  |  |
| 32 | h2 | Selecione uma aula |  |
| 37 | h3 | O que você vai aprender |  |
| 38 | a | Abrir o consultório | /profissional/ |

## semdor-crm · src/Clinica.Crm/wwwroot/app.js

[Fonte congelada](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/app.js) · 815 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 68 | button | Abrir atendimento de ${esc(name(c))} |  |
| 74 | button | Responder a esta mensagem |  |
| 75 | input | Concluir ${esc(t.title)} |  |
| 91 | h3 | ${esc(t)} |  |
| 95 | button | Ver tarefas → |  |
| 101 | button | Ver ${cs.length} atendimento(s) → |  |
| 101 | button | Editar |  |
| 102 | section |  |  |
| 102 | h2 | ${esc(s)} |  |
| 102 | button | ${esc(name(c))} |  |
| 102 | select | Etapa de ${esc(name(c))} |  |
| 103 | input | ${t.done?'Reabrir':'Concluir'} ${esc(t.title)} |  |
| 103 | button | ${esc(name(c))} → |  |
| 103 | button | Editar |  |
| 106 | h3 | ${esc(r.title.includes(' · ')?r.title.split(' · ').slice(1).join(' · '):r.title)} |  |
| 106 | button | Editar |  |
| 106 | button | ${r.archived?'Restaurar':'Arquivar'} |  |
| 107 | button |  |  |
| 109 | h2 | WhatsApp |  |
| 109 | h2 | Instagram |  |
| 109 | h2 | Messenger |  |
| 149 | h3 | ${esc(r.name)} |  |
| 149 | button | Editar |  |
| 149 | button | ${r.enabled?'Pausar':'Ativar'} |  |
| 162 | button | Remover anexo |  |
| 163 | button | Cancelar citação |  |
| 166 | a |  | ${url} |
| 167 | a | Baixar áudio | ${url}?download=true |
| 168 | a | Baixar vídeo | ${url}?download=true |
| 169 | a | ${icon('note')} | ${url} |
| 188 | input | ${esc(q)} |  |
| 212 | h4 | Personalizar mensagem |  |
| 212 | input |  |  |
| 221 | button |  |  |
| 233 | button | Atualizar consulta |  |
| 266 | h2 | ${esc(agent(t.id))} |  |
| 266 | button | Editar perfil e folgas |  |
| 297 | button | Revisar conversa → |  |
| 302 | button | Mensagem ${c.messages.findIndex(m=>m.id===id)+1} ↗ |  |
| 306 | select | Avaliação: ${esc(k.title)} |  |
| 306 | textarea | ${esc(a?.reason\|\|'')} |  |
| 306 | input |  |  |
| 310 | button |  |  |
| 312 | h3 | ${r.score===null?'Sem nota concluída':r.score+'% na amostra avaliada'} · ${esc(qualityOutcomes[r.outcome])} |  |
| 312 | section |  |  |
| 312 | h3 | ${esc(d.criteria.find(k=>k.id===a.criterion)?.title)} — ${esc(qualityGrade(a.score))} |  |
| 312 | h3 | Devolutiva |  |
| 312 | h3 | Plano de ação |  |
| 329 | button | Abrir atendimento → |  |
| 330 | button | Tentar sincronizar novamente |  |
| 331 | input |  |  |
| 331 | input |  |  |
| 331 | button | × |  |
| 331 | input |  |  |
| 331 | select |  |  |
| 331 | select |  |  |
| 331 | button | Renomear |  |
| 331 | button | + Nova especialidade |  |
| 331 | select |  |  |
| 331 | input | ${esc(a.name)} |  |
| 331 | input |  |  |
| 334 | textarea | ${esc(p.greeting)} |  |
| 334 | button | + Opção nesta etapa |  |
| 339 | select | ${receptionDays.map((d,i)=>` |  |
| 339 | input |  |  |
| 339 | input |  |  |
| 339 | button | Remover intervalo |  |
| 392 | input | ${esc(recallLabel(r.kind))} |  |
| 392 | input | dias |  |
| 395 | button | Abrir conversa → |  |
| 396 | button | Ver conversa |  |
| 396 | button | Sincronizar registro |  |
| 407 | button | Atender → |  |
| 408 | button | Conversa |  |
| 408 | button | Registrar solução |  |
| 415 | button | Conferir atendimento → |  |
| 472 | button |  |  |
| 472 | h3 | ${esc(t.title)} |  |
| 472 | h3 | Campos da mensagem |  |
| 472 | button | Copiar texto |  |
| 472 | button | Preencher ${t.automation==='recall'?'recall':'lembrete'} |  |
| 498 | section |  |  |
| 498 | h3 | '+title+' |  |
| 501 | button | Preparar acompanhamento |  |
| 501 | button | Consultar vagas e remarcar |  |
| 502 | button | Preparar acompanhamento |  |
| 503 | button | Abrir tarefa |  |
| 509 | select | Paciente nesta consulta |  |
| 551 | button |  |  |
| 551 | button |  |  |
| 566 | button |  |  |
| 572 | button | Abrir atendimento → |  |
| 573 | button | Abrir conversa |  |
| 573 | button | Registrar solução |  |
| 573 | button | Retomar sincronização |  |
| 574 | h2 | ${esc(label)} |  |
| 574 | button | Fechar |  |
| 586 | button |  |  |
| 587 | button | Revisar → |  |
| 588 | button | Abrir → |  |
| 589 | button | Atendimento → |  |
| 616 | button |  |  |
| 658 | h3 | ${esc(k.title)} |  |
| 658 | a | Consultar fonte | ${esc(k.sourceUrl)} |
| 658 | button | Editar |  |
| 658 | button | ${k.status==='archived'?'Restaurar rascunho':'Arquivar'} |  |
| 663 | h3 | ${esc(k.question)} |  |
| 663 | button | Revisar aprendizado |  |
| 663 | button | Descartar |  |
| 663 | button | Ver conhecimento |  |
| 669 | h3 | ${esc(t.title)} |  |
| 669 | button | Editar |  |
| 669 | button | Testar exemplo |  |
| 669 | button | ${t.status==='archived'?'Restaurar':'Arquivar'} |  |
| 672 | button | Resposta adequada |  |
| 672 | button | Precisa melhorar |  |
| 672 | button | Criar exemplo a partir do teste |  |
| 673 | button | Ver atendimento |  |
| 738 | button | Abrir atendimento |  |
| 746 | dialog |  |  |
| 747 | h2 | Remarcar pela clínica |  |
| 747 | button | Fechar remarcação |  |
| 750 | input |  |  |
| 750 | button | Consultar vagas |  |
| 751 | select |  |  |
| 751 | input | Confirmei o paciente e combinei este horário. |  |
| 751 | button | Confirmar remarcação na clínica |  |
| 762 | button | Preparar mensagem ao paciente |  |
| 762 | button | Conferir resultado do pedido |  |

## semdor-crm · src/Clinica.Crm/wwwroot/booking.js

[Fonte congelada](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/booking.js) · 153 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 7 | h2 | Agendar nesta conversa |  |
| 7 | button | Voltar à conversa |  |
| 11 | select |  |  |
| 12 | select |  |  |
| 12 | select |  |  |
| 13 | select |  |  |
| 13 | select |  |  |
| 15 | input |  |  |
| 15 | input |  |  |
| 16 | input |  |  |
| 16 | input |  |  |
| 17 | input |  |  |
| 18 | button | Manhã |  |
| 18 | button | Tarde |  |
| 18 | button | Após 17h |  |
| 19 | input | ${d} |  |
| 20 | button | Buscar horários disponíveis |  |
| 22 | section | Vagas encontradas |  |
| 23 | section |  |  |
| 23 | h3 | Reservas e agendamentos desta conversa |  |
| 24 | section |  |  |
| 24 | h3 | Calendário |  |
| 24 | select | Período do calendário |  |
| 24 | button | Atualizar |  |
| 91 | section |  |  |
| 91 | h4 | ${esc(day.toLocaleDateString('pt-BR',{timeZone:'America/Sao_Paulo',weekday:'short',day:'2-digit',month:'2-digit'}))} |  |
| 103 | input | Confirmei o paciente e combinei este horário. |  |
| 103 | button | Confirmar no Clínico |  |
| 103 | button | Liberar vaga |  |
| 104 | button | Conferir resultado |  |
| 127 | button | ${esc(clinicTime(t))} |  |

## semdor-crm · src/Clinica.Crm/wwwroot/conversation-workflow.js

[Fonte congelada](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/conversation-workflow.js) · 85 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 10 | dialog |  |  |
| 10 | h2 | Precisa da minha atenção |  |
| 10 | button | Fechar atenção |  |
| 10 | dialog |  |  |
| 10 | h2 | Retomar conversa |  |
| 10 | button | Fechar lembrete |  |
| 10 | input |  |  |
| 10 | button | Cancelar |  |
| 10 | button | Salvar lembrete |  |
| 10 | dialog |  |  |
| 10 | h2 | Recusar transferência |  |
| 10 | button | Fechar recusa |  |
| 10 | textarea |  |  |
| 10 | button | Voltar |  |
| 10 | button | Confirmar recusa |  |
| 11 | button | Minha atenção |  |
| 13 | select |  |  |
| 13 | button | Lembrar de retomar |  |
| 14 | select |  |  |
| 17 | button | ${text} |  |
| 23 | button | Abrir conversa |  |
| 52 | button | Desafixar nota |  |
| 55 | button | ${c.pinnedNoteIds?.includes(note.id)?'Desafixar nota':'Fixar nota'} |  |

## semdor-crm · src/Clinica.Crm/wwwroot/flow-editor.js

[Fonte congelada](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/flow-editor.js) · 55 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 6 | button | Atendimento |  |
| 6 | button | Expediente |  |
| 6 | button | Agenda e lembretes |  |
| 10 | h3 | Mensagem inicial |  |
| 13 | h3 | Opções e especialidades |  |
| 22 | h2 | Visão do paciente |  |
| 22 | button | Fechar prévia |  |

## semdor-crm · src/Clinica.Crm/wwwroot/index.html

[Fonte congelada](https://github.com/lucaszaous-creator/semdor-crm/blob/33c9ffcb836286c27a61bf1f91640a62acb5b70c/src/Clinica.Crm/wwwroot/index.html) · 144 linhas.

| Linha | Tipo | Rótulo | Ação/vínculo |
| --- | --- | --- | --- |
| 3 | a | Ir para o conteúdo | #workspace |
| 4 | a |  | / |
| 5 | button | Atendimentos |  |
| 6 | button | Meu dia |  |
| 7 | button | Agenda |  |
| 8 | button | Contatos |  |
| 9 | button | Recall |  |
| 10 | button | Tarefas |  |
| 11 | button | Funil |  |
| 12 | button | Envios do Clínico |  |
| 15 | button | Visão geral |  |
| 16 | button | Operação |  |
| 17 | button | Relatórios |  |
| 18 | button | IA e treinamento |  |
| 19 | button | Auditoria |  |
| 21 | button | Canais |  |
| 21 | button | Configurações |  |
| 22 | button | Buscar na central |  |
| 22 | button | Ativar notificações neste navegador |  |
| 22 | button |  |  |
| 22 | button | Sair |  |
| 23 | section |  |  |
| 23 | h1 | Clínica SemDor |  |
| 23 | h2 | Entrar na central |  |
| 23 | input |  |  |
| 23 | input |  |  |
| 23 | select |  |  |
| 23 | button | Entrar |  |
| 24 | button | Atualizar |  |
| 24 | button | + Conversas de exemplo |  |
| 25 | section |  |  |
| 25 | h1 | Atendimentos |  |
| 25 | button | Novo atendimento |  |
| 25 | button | Novos |  |
| 25 | button | Meus |  |
| 25 | button | Todos |  |
| 25 | button | Encerrados |  |
| 25 | input | Buscar atendimento |  |
| 25 | select | Filtrar fila |  |
| 25 | select | Filtrar número |  |
| 25 | select | Filtrar clínica |  |
| 25 | select | Filtrar especialidade |  |
| 25 | input |  |  |
| 25 | select | Filtrar acompanhamento |  |
| 25 | select | Filtrar prioridade |  |
| 25 | select | Filtrar necessidade de resposta |  |
| 25 | select | Ordenar conversas |  |
| 25 | button | Limpar |  |
| 25 | button | Próximo atendimento → |  |
| 25 | h3 | Nenhum atendimento |  |
| 26 | h2 | Uma conversa. Todo o contexto. |  |
| 26 | button | Ver o que precisa de atenção hoje → |  |
| 27 | section | Conversa selecionada |  |
| 27 | button | Voltar à lista |  |
| 27 | h2 |  |  |
| 27 | button | Assumir |  |
| 27 | button | Transferir |  |
| 27 | button | Reabrir |  |
| 27 | button | Concluir |  |
| 27 | button | Auditar |  |
| 27 | button | Buscar na conversa |  |
| 27 | button | Ficha do contato |  |
| 27 | button | Agendar |  |
| 27 | button | Agenda e retornos |  |
| 27 | button | Assumir humano |  |
| 27 | button | Devolver à IA |  |
| 27 | button | Histórico da IA |  |
| 27 | button | Responder mensagem pendente |  |
| 27 | button | Simular pedido humano |  |
| 27 | input | Buscar no histórico |  |
| 27 | input |  |  |
| 27 | button | Testar como paciente |  |
| 27 | button | Descartar |  |
| 27 | button | Aprovar e inserir no rascunho |  |
| 27 | button | Responder |  |
| 27 | button | Nota interna |  |
| 27 | button | Sugerir resposta |  |
| 27 | button | Respostas rápidas |  |
| 27 | textarea |  |  |
| 27 | button | ＋ Anexar |  |
| 27 | input |  |  |
| 27 | button | ◉ Gravar áudio |  |
| 27 | button | Oferecer atendimento humano |  |
| 27 | button | Modelos WhatsApp |  |
| 27 | button | Enviar |  |
| 28 | h3 | Ficha do contato |  |
| 28 | button | Fechar ficha do contato |  |
| 28 | button | Editar |  |
| 28 | h3 |  |  |
| 28 | h3 | Jornada do atendimento |  |
| 28 | select |  |  |
| 28 | select |  |  |
| 28 | h3 | Próximos passos |  |
| 28 | button | Adicionar tarefa para este contato |  |
| 28 | h3 | Especialidade do atendimento |  |
| 28 | select | Especialidade do atendimento |  |
| 28 | button | Salvar especialidade |  |
| 29 | section |  |  |
| 30 | h1 | Meu dia |  |
| 32 | h2 | Meus retornos |  |
| 32 | button | Ver todos → |  |
| 33 | h2 | Aguardando resposta |  |
| 33 | button | Ver fila → |  |
| 34 | h2 | Consultas de hoje |  |
| 34 | button | Abrir agenda → |  |
| 36 | section |  |  |
| 37 | h1 | IA e treinamento |  |
| 37 | button | Verificar conexão |  |
| 40 | h2 | Jade, nossa assistente virtual. |  |
| 41 | input | Mensagem fictícia para testar o atendimento |  |
| 41 | button | Enviar no teste |  |
| 42 | button | Base de conhecimento |  |
| 42 | button | Ensinar e treinar |  |
| 42 | button | Aprender com a recepção |  |
| 42 | button | Simular conversa |  |
| 42 | button | Comportamento e atendimento humano |  |
| 43 | section |  |  |
| 43 | h2 | O que a IA pode responder |  |
| 43 | button | Adicionar conhecimento |  |
| 43 | input | Buscar conhecimento |  |
| 43 | select | Situação do conhecimento |  |
| 44 | section |  |  |
| 45 | h2 | Cada atendimento pode ensinar |  |
| 45 | button | Ativar coleta |  |
| 46 | input | Buscar aprendizado |  |
| 46 | select | Situação do aprendizado |  |
| 47 | h3 | A equipe decide o que vira conhecimento |  |
| 48 | section |  |  |
| 49 | h2 | Do jeito que a clínica atende |  |
| 49 | button | Executar testes publicados |  |
| 49 | button | Adicionar exemplo |  |
| 51 | input | Buscar exemplos |  |
| 51 | select | Situação dos exemplos |  |
| 52 | h3 | Como este treinamento funciona |  |
| 54 | section |  |  |
| 54 | h2 | Converse antes de ativar |  |
| 54 | textarea |  |  |
| 54 | button | Documentos |  |
| 54 | button | Pedir uma pessoa |  |
| 54 | button | Dúvida clínica |  |
| 54 | button | Testar resposta |  |
| 54 | button | Nova simulação |  |
| 54 | h3 | Uma resposta para conferir |  |
| 54 | h2 | Histórico de testes e atendimento automático |  |
| 55 | section |  |  |
| 55 | h2 | Comportamento da IA |  |
| 55 | select |  |  |
| 55 | select |  |  |
| 55 | input |  |  |
| 55 | textarea |  |  |
| 55 | textarea |  |  |
| 55 | input |  |  |
| 55 | textarea |  |  |
| 55 | select |  |  |
| 55 | textarea |  |  |
| 55 | button | Salvar comportamento |  |
| 55 | h2 | Atendimento humano tem prioridade |  |
| 55 | h3 | Infraestrutura separada |  |
| 55 | h2 | Histórico de configuração |  |
| 57 | section |  |  |
| 57 | h1 | Painel de atendimento |  |
| 57 | h2 | Distribuição por especialidade |  |
| 57 | h2 | Jornada dos contatos |  |
| 57 | h2 | Equipe de atendimento |  |
| 58 | section |  |  |
| 58 | h1 | Contatos |  |
| 58 | button | Novo contato |  |
| 58 | input | Buscar contatos |  |
| 58 | select | Filtrar carteira de contatos |  |
| 59 | section |  |  |
| 59 | h1 | Funil de relacionamento |  |
| 59 | select | Filtrar responsável no funil |  |
| 60 | section |  |  |
| 60 | h1 | Tarefas e retornos |  |
| 60 | button | Nova tarefa |  |
| 60 | button | Pendentes |  |
| 60 | button | Minhas |  |
| 60 | button | Concluídas |  |
| 60 | input | Buscar tarefas |  |
| 60 | select | Filtrar prazo |  |
| 60 | select | Filtrar prioridade da tarefa |  |
| 61 | section |  |  |
| 61 | h1 | Respostas salvas |  |
| 61 | button | Adicionar respostas da clínica |  |
| 61 | button | Criar resposta |  |
| 61 | input | Buscar respostas |  |
| 61 | select | Categoria das respostas |  |
| 61 | input | Mostrar arquivadas |  |
| 62 | section |  |  |
| 63 | h1 | Envios do Clínico |  |
| 63 | button | Atualizar envios |  |
| 65 | input | Buscar envio |  |
| 68 | section |  |  |
| 69 | h1 | Agenda e confirmações |  |
| 69 | button | Configurar lembretes |  |
| 69 | button | Sincronizar |  |
| 72 | input |  |  |
| 72 | select |  |  |
| 72 | input |  |  |
| 72 | select |  |  |
| 73 | h2 | Próximos agendamentos |  |
| 74 | h2 | Histórico da automação |  |
| 74 | section |  |  |
| 75 | h1 | Recall de pacientes |  |
| 75 | button | Configurar recall |  |
| 75 | button | Sincronizar |  |
| 78 | h2 | Regras de contato |  |
| 79 | input |  |  |
| 79 | input |  |  |
| 79 | input |  |  |
| 79 | input |  |  |
| 80 | input | Somente segunda a sexta · horário de Brasília |  |
| 81 | select |  |  |
| 81 | input |  |  |
| 81 | input |  |  |
| 81 | button | Consultar modelos disponíveis |  |
| 82 | input | Ativar rotina automática de recall |  |
| 82 | button | Salvar configuração |  |
| 83 | h2 | Modelo de mensagem |  |
| 83 | h3 | Antes de cada envio |  |
| 84 | h2 | Pacientes para acompanhamento |  |
| 84 | input |  |  |
| 84 | select |  |  |
| 84 | select |  |  |
| 85 | h2 | Histórico de recall |  |
| 86 | section |  |  |
| 86 | h1 | Operação e consumo |  |
| 86 | section | Diagnóstico da operação |  |
| 86 | h2 | Saúde da operação |  |
| 86 | section | Passagem de plantão |  |
| 86 | h2 | Atendimentos com responsável desconectado |  |
| 86 | section |  |  |
| 86 | h2 | Pacientes aguardando resposta |  |
| 86 | section |  |  |
| 86 | h2 | Pedidos de remarcação |  |
| 86 | section |  |  |
| 86 | h2 | Estimativa de custo |  |
| 86 | button | Configurar tarifas |  |
| 86 | select |  |  |
| 86 | input |  |  |
| 86 | input |  |  |
| 86 | input |  |  |
| 86 | input |  |  |
| 86 | button | Salvar tarifas da simulação |  |
| 87 | dialog |  |  |
| 87 | h2 | Concluir pedido de remarcação |  |
| 87 | button | Fechar |  |
| 87 | textarea |  |  |
| 87 | button | Registrar conclusão |  |
| 88 | section |  |  |
| 88 | h1 | Canais |  |
| 88 | h2 | Números e canais |  |
| 89 | section |  |  |
| 89 | h1 | Automação do atendimento |  |
| 89 | button | Nova regra |  |
| 91 | h2 | Expediente da recepção |  |
| 91 | input | Aplicar expediente |  |
| 91 | button | + Intervalo semanal |  |
| 91 | textarea |  |  |
| 91 | textarea |  |  |
| 91 | button | Salvar expediente |  |
| 92 | h2 | Menu de boas-vindas |  |
| 92 | input | Menu ativo |  |
| 93 | input | Usar botões e lista de seleção |  |
| 93 | textarea |  |  |
| 93 | h3 | Opções e encaminhamentos |  |
| 93 | button | Menu de especialidades SemDor |  |
| 93 | button | + Opção |  |
| 94 | h2 | Confirmação 24 horas antes |  |
| 94 | input | Lembretes ativos |  |
| 94 | select |  |  |
| 94 | input |  |  |
| 94 | input |  |  |
| 94 | button | Consultar modelos da Infobip |  |
| 95 | button | Salvar fluxo de atendimento |  |
| 95 | section |  |  |
| 95 | h1 | Relatórios de atendimento |  |
| 95 | button | Exportar CSV |  |
| 95 | input |  |  |
| 95 | input |  |  |
| 95 | select |  |  |
| 95 | select |  |  |
| 95 | select |  |  |
| 95 | h2 | Resultado dos atendimentos |  |
| 95 | h2 | Desempenho por atendente |  |
| 96 | section |  |  |
| 96 | h1 | Equipe e permissões |  |
| 96 | h2 | Responsabilidades de cada perfil |  |
| 96 | section |  |  |
| 96 | h1 | Auditoria de conversas |  |
| 96 | button | Atualizar auditoria |  |
| 96 | button | Exportar seleção |  |
| 96 | h2 | Acesso reservado à gestão |  |
| 96 | input |  |  |
| 96 | select |  |  |
| 96 | select |  |  |
| 96 | input |  |  |
| 96 | input |  |  |
| 96 | h2 | Fila de revisão |  |
| 96 | h2 | Acompanhamento por atendente |  |
| 96 | section |  |  |
| 97 | h1 | Configurações |  |
| 99 | button |  |  |
| 100 | button |  |  |
| 101 | button |  |  |
| 102 | button |  |  |
| 103 | button |  |  |
| 104 | button |  |  |
| 105 | button |  |  |
| 107 | section |  |  |
| 108 | section |  |  |
| 108 | h2 | Regras de recall |  |
| 108 | button | Ver pacientes |  |
| 109 | section |  |  |
| 109 | h2 | Modelos WhatsApp |  |
| 109 | button | Baixar pacote |  |
| 109 | select |  |  |
| 109 | select |  |  |
| 109 | button | Consultar aprovação |  |
| 110 | section |  |  |
| 111 | section |  |  |
| 112 | section |  |  |
| 112 | h2 | Sistema da clínica |  |
| 112 | button | Consultar agenda |  |
| 113 | section |  |  |
| 113 | h2 | Tarifas de mensagens |  |
| 113 | button | Ver consumo |  |
| 116 | dialog |  |  |
| 116 | h2 | Concluir atendimento |  |
| 116 | button | Fechar conclusão |  |
| 116 | select |  |  |
| 116 | textarea |  |  |
| 116 | input |  |  |
| 116 | input |  |  |
| 116 | select |  |  |
| 116 | button | Continuar atendimento |  |
| 116 | button | Registrar conclusão |  |
| 117 | dialog |  |  |
| 117 | h2 | Transferir atendimento |  |
| 117 | button | Fechar |  |
| 117 | select |  |  |
| 117 | select |  |  |
| 117 | textarea |  |  |
| 117 | button | Cancelar |  |
| 117 | button | Transferir |  |
| 118 | dialog |  |  |
| 118 | h2 | Ficha do contato |  |
| 118 | button | Fechar |  |
| 118 | input |  |  |
| 118 | input |  |  |
| 118 | input |  |  |
| 118 | input |  |  |
| 118 | input |  |  |
| 118 | select |  |  |
| 118 | input |  |  |
| 118 | button | Cancelar |  |
| 118 | button | Salvar contato |  |
| 119 | dialog |  |  |
| 119 | h2 | Nova tarefa |  |
| 119 | button | Fechar |  |
| 119 | input |  |  |
| 119 | textarea |  |  |
| 119 | select |  |  |
| 119 | input |  |  |
| 119 | select |  |  |
| 119 | select |  |  |
| 119 | button | Cancelar |  |
| 119 | button | Salvar tarefa |  |
| 120 | dialog |  |  |
| 120 | h2 | Resposta rápida |  |
| 120 | button | Fechar |  |
| 120 | input |  |  |
| 120 | textarea |  |  |
| 120 | button | Cancelar |  |
| 120 | button | Salvar resposta |  |
| 121 | dialog |  |  |
| 121 | h2 | Inserir resposta rápida |  |
| 121 | button | Fechar |  |
| 121 | input | Buscar resposta rápida |  |
| 121 | select | Categoria da resposta rápida |  |
| 122 | dialog |  |  |
| 122 | h2 | Regra de encaminhamento |  |
| 122 | button | Fechar |  |
| 122 | input |  |  |
| 122 | input |  |  |
| 122 | input |  |  |
| 122 | select |  |  |
| 122 | select |  |  |
| 122 | select |  |  |
| 122 | input | Ativar esta regra |  |
| 122 | button | Cancelar |  |
| 122 | button | Salvar regra |  |
| 122 | dialog |  |  |
| 122 | h2 | Novo atendimento |  |
| 122 | button | Fechar |  |
| 122 | input |  |  |
| 122 | input |  |  |
| 122 | select |  |  |
| 122 | button | Cancelar |  |
| 122 | button | Abrir atendimento |  |
| 123 | dialog |  |  |
| 124 | h2 | Escolher mensagem |  |
| 124 | button | Fechar modelos |  |
| 127 | section | Biblioteca de modelos |  |
| 128 | input |  |  |
| 128 | select | Origem dos modelos |  |
| 128 | select | Finalidade do modelo |  |
| 130 | section | Revisar mensagem |  |
| 130 | h3 | Uma mensagem para cada momento |  |
| 131 | h3 |  |  |
| 134 | button | Gerenciar modelos |  |
| 134 | button | Voltar |  |
| 134 | button | Enviar mensagem |  |
| 136 | dialog |  |  |
| 136 | h2 | Agenda e retornos |  |
| 136 | button | Fechar agenda e retornos |  |
| 136 | button | Atualizar dados |  |
| 136 | button | Voltar à conversa |  |
| 137 | dialog |  |  |
| 137 | h2 | Gravar áudio |  |
| 137 | button | Cancelar gravação |  |
| 137 | button | Descartar |  |
| 137 | button | Parar e revisar |  |
| 138 | dialog |  |  |
| 138 | h2 | Editar integrante |  |
| 138 | button | Fechar |  |
| 138 | select |  |  |
| 138 | textarea |  |  |
| 138 | input |  |  |
| 138 | input | Disponível para receber novos atendimentos |  |
| 138 | button | Cancelar |  |
| 138 | button | Salvar alterações |  |
| 138 | dialog |  |  |
| 138 | h2 | Auditar atendimento |  |
| 138 | button | Fechar auditoria |  |
| 138 | section |  |  |
| 138 | h3 | Conversa e autoria |  |
| 138 | select |  |  |
| 138 | select |  |  |
| 138 | textarea |  |  |
| 138 | textarea |  |  |
| 138 | input |  |  |
| 138 | input | Plano de ação concluído |  |
| 138 | button | Salvar rascunho |  |
| 138 | button | Concluir avaliação |  |
| 139 | dialog |  |  |
| 139 | h2 | Avaliação preservada |  |
| 139 | button | Fechar histórico |  |
| 139 | dialog |  |  |
| 139 | h2 | Buscar na central |  |
| 139 | button | Fechar busca |  |
| 139 | input |  |  |
| 140 | dialog |  |  |
| 140 | h2 | Adicionar conhecimento |  |
| 140 | button | Fechar |  |
| 140 | input |  |  |
| 140 | input |  |  |
| 140 | textarea |  |  |
| 140 | textarea |  |  |
| 140 | input |  |  |
| 140 | textarea |  |  |
| 140 | input | Conferi que o texto é uma orientação administrativa geral, sem dados do paciente ou instrução clínica individual. |  |
| 140 | button | Voltar |  |
| 140 | button | Salvar rascunho |  |
| 140 | button | Aprovar e publicar |  |
| 141 | dialog |  |  |
| 141 | h2 | Adicionar exemplo |  |
| 141 | button | Fechar exemplo |  |
| 141 | input |  |  |
| 141 | textarea |  |  |
| 141 | input |  |  |
| 141 | textarea |  |  |
| 141 | textarea |  |  |
| 141 | select |  |  |
| 141 | textarea |  |  |
| 141 | button | Voltar |  |
| 141 | button | Salvar rascunho |  |
| 141 | button | Aprovar e publicar exemplo |  |
| 142 | dialog |  |  |
| 142 | h2 | Histórico da IA neste atendimento |  |
| 142 | button | Fechar histórico da IA |  |
| 143 | dialog |  |  |
| 143 | h2 | Editar especialidade |  |
| 143 | button | Fechar edição de especialidade |  |
| 143 | input |  |  |
| 143 | button | Cancelar |  |
| 143 | button | Salvar especialidade |  |
