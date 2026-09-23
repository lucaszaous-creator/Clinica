# Navegação e interface — implementação completa

**Estado:** implementado e validado localmente na branch `codex/navegacao-unificada`. Este relatório não comprova atualização instalada nos computadores da clínica nem publicação em produção.

## Escopo

32 achados do relatório original tratados nos cinco aplicativos: Recepção, Consultório, Financeiro, Faturamento e Gerente. O cadastro integra esse trabalho. A referência visual permanece o Modelo A e o workspace do paciente aprovado.

## Funções preservadas e localização

| Função | Local |
|---|---|
| Marcar atendimento completo | Gestão → Agenda → Marcar atendimento; botão no Dia e na Grade |
| Horários e travas / bloqueios / vagas / impressão | Agenda → Grade |
| Evolução de chegada e saída | Agenda → Enfermagem → sessão; permissões do perfil |
| CPF e endereço obrigatórios | Ficha única → Editar cadastro / Novo paciente |
| Autorizações e renovação de convênio | Ficha → Sessões e guias → Autorizações e validade |
| Baixa, glosa, recurso XML, NC e lotes | Financeiro → Faturamento de guias |

## Verificação

- **Regras de domínio e aplicação:** 2.849 testes aprovados, 0 falhas.
- **Fronteira HTTP das assinaturas:** 15 testes aprovados, 0 falhas.
- **Navegação, contexto e formulários:** 268 verificações aprovadas: seis perfis/composições; autorização de rotas, paciente correto, retorno, CPF/endereço, atualização da ficha após edição, bloqueio de consultas clínicas para perfis administrativos e preservação cruzada de TUSS/Pix.
- **Layout Windows completo:** 625 combinações de tela/aba/largura verificadas (880, 960, 1024, 1366 e 1920 px), sem cortes detectados pelo verificador.
- **Gestão e operações financeiras:** Rotina Windows --gestao aprovada: saldos, alertas, permissões, pagamentos e compras com dados fictícios.
- **Compilação nativa:** Clinica.sln: 0 erros. Avisos de compilador registrados no log.
- **Compilação-sombra:** C# de 11 projetos WPF aprovado.
- **Design system:** 216 arquivos XAML, 10 projetos e 136 construtores conferidos; 33 cores do CSS/XAML em correspondência.
- **Vínculos das telas:** Logs WPF de bindings sem erros nas rodadas registradas.
- **Cadastro expandido:** Abre maximizado, sem limites fixos de monitor. Janelas de 600×450, 960×720, 1366×768 e 1920×1080 verificadas; botões acessíveis, rolagem e preservação dos campos aprovadas.

## Revisão com Jev

Consultas reais à TypeSafe, modelo retornado `jev-1.13.0`. Jev analisou o mapa e a descrição técnica fornecidos: recomendou workspace com administrativo; classificou o escopo como navegação completa; destacou validação de perfis e contexto. A revisão do Jev é apoio de arquitetura; os testes Windows e do repositório fornecem a evidência executável. Nenhum dado de paciente ou senha foi enviado.

## Matriz dos 32 achados

### 01. Ficha do paciente

**Antes:** Cadastro abre FichaPacienteViewModel da Recepção; Em tratamento abre PacienteWorkspaceViewModel/Capa; Faturamento mantém outra FichaPacienteViewModel.

**Implementado:** Uma ficha baseada no workspace aprovado, com dados/alertas, sessões/guias, anamnese e áreas administrativas no mesmo contexto.

**Destino:** Pacientes → escolher paciente → Ficha do paciente

**Evidência:** `src/Clinica.Modulo.Clinico/Views/PacienteView.xaml:19`.

### 02. Busca e listas de pacientes

**Antes:** Cadastro e Em tratamento mantêm listas e jornadas distintas; Faturamento também usa sua própria lista.

**Implementado:** Lista e busca comuns, filtro mantido ao voltar; o paciente escolhido abre a mesma fábrica de ficha.

**Destino:** Paciente → Pacientes → Todos / Em tratamento / Cadastro incompleto

**Evidência:** `src/Clinica.Modulo.Recepcao/ViewModels/PacientesViewModel.cs:46`.

### 03. Agenda e marcação

**Antes:** Agenda Dia/Grade/Semana, Minha agenda Hoje/Semana/Sem evolução, Marcar e agenda do Faturamento possuem entradas próprias.

**Implementado:** Agenda consolidada. Marcar mantém o fluxo completo; Grade conserva horários e travas, bloqueios, vagas e impressão.

**Destino:** Gestão → Agenda → Dia / Grade / Semana / Marcar atendimento

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:17`.

### 04. Prontuário e evolução médica

**Antes:** Ficha da Recepção, Prontuário por paciente, Registros e pendências e Histórico clínico apresentam acessos diferentes; existem comandos próprios para abrir/escrever sessões.

**Implementado:** Histórico e sessão no workspace canônico. A entrada antiga do prontuário usa a mesma lista/ficha; os editores de correção e os vínculos originais permanecem.

**Destino:** Paciente → ficha → Histórico; Agenda → Evoluções pendentes

**Evidência:** `src/Clinica.Modulo.Recepcao/Modulo/ModuloRecepcao.cs:515`.

### 05. Documentos e prescrições

**Antes:** Central Documentos, documentos da ficha, Prescrições clínicas e Documentos emitidos apresentam listas com contextos diferentes. Parte das ações já é compartilhada.

**Implementado:** Prescrições dentro da ficha usam o mesmo componente clínico do acesso global. Catálogo, editor, assinatura, impressão e envio continuam compartilhados; recibos e orçamentos mantêm finalidade financeira.

**Destino:** Paciente → Prescrições e documentos; Central documental

**Evidência:** `src/Clinica.Modulo.Clinico/Views/DocumentosPacienteView.xaml:13`.

### 06. Enfermagem e infusão

**Antes:** Sessões de enfermagem, Passagens, Atendimento de enfermagem e Sala de infusão são portas diferentes; prescrever infusão e registrar execução são atos diferentes.

**Implementado:** Fila disponível na Agenda do perfil de enfermagem. Chegada, saída e execução da infusão continuam vinculadas à sessão; prescrição e execução são atos distintos.

**Destino:** Agenda → Enfermagem → sessão → Atendimento de enfermagem

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:18`.

### 07. Pagamentos e caixa

**Antes:** Pagamentos aparece no grupo Paciente; Caixa e Contas no Financeiro; a ficha oferece recebimento contextual via componente compartilhado.

**Implementado:** Recebimentos deixam de ficar dispersos no grupo Paciente. A cobrança contextual abre o formulário comum, conservando o paciente e o saldo.

**Destino:** Financeiro → Recebimentos de pacientes; ficha → cobrança

**Evidência:** `src/Clinica.Desktop.Shell/Componentes/CobrancaDoPacienteViewModel.cs:166`.

### 08. Particular, pacotes e preços

**Antes:** Pacotes é compartilhado, mas aparece em Paciente na Recepção e Financeiro no outro módulo; preços do particular também aparecem em agrupamentos diferentes.

**Implementado:** Grupo consistente para pacotes e preços; componentes de venda e consulta de saldo preservados.

**Destino:** Financeiro → Particular e pacotes

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:35`.

### 09. Faturamento, guias e TISS

**Antes:** Aplicativo Faturamento mantém seu próprio shell e telas. O Gerente usa FaturamentoTissViewModel com visão, pendências, glosas, NC e lotes.

**Implementado:** Novo módulo operacional compartilhado pelo Gerente e pelo aplicativo Faturamento. Resumo, pendências, guias, faturados, glosas, NC e TISS no mesmo conjunto.

**Destino:** Financeiro → Faturamento de guias

**Evidência:** `src/Clinica.Modulo.Faturamento/Modulo/ModuloFaturamento.cs:9`.

### 10. Exames, anexos e acompanhamento

**Antes:** Exames possui lista global e área dentro do paciente; medidas, avaliações e dor ficam agrupadas em acompanhamento, com chaves próprias.

**Implementado:** Seções do mesmo workspace e contexto local do paciente; consultas rápidas reutilizam os componentes de anexos. Dor, medidas e avaliações preservadas.

**Destino:** Paciente → Exames e anexos / Acompanhamento

**Evidência:** `src/Clinica.Modulo.Clinico/ViewModels/PacienteWorkspaceViewModel.cs:195`.

### 11. Retornos e relacionamento

**Antes:** Retornos a marcar é fila assistencial. Recall tem sobreposição entre Retorno de pacientes e Campanhas (detalhada no achado recall); retenção mede abandono.

**Implementado:** Pedidos clínicos de retorno têm nome e lugar distintos das campanhas de reativação. Histórico de contato fica na ficha.

**Destino:** Agenda → Retornos solicitados; Gestão → Relacionamento

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:20`.

### 12. Relatórios e indicadores

**Antes:** Resultado do mês e Produção ficam em Financeiro no aplicativo dedicado e em Relatórios/BI no Gerente. Meus números integra outra visão.

**Implementado:** Produção e resultado financeiro usam o mesmo grupo temático em todos os aplicativos. Indicadores próprios de cada finalidade continuam separados.

**Destino:** Inteligência → Produção / Resultado e teto / relatórios

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:46`.

### 13. Configurações, acessos e conformidade

**Antes:** Configurações e Conformidade e acessos ficam em Inteligência; Profissionais e salas em Gestão. Faturamento tem acesso e configuração próprios.

**Implementado:** Agrupamento consistente de administração; permissões mantidas na navegação e nas operações.

**Destino:** Gestão → equipe, salas, usuários, configurações e conformidade

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:38`.

### 14. Estoque e materiais da sessão

**Antes:** Estoque está no Financeiro; checagem/consumo de materiais pertencem ao procedimento assistencial. Não são a mesma operação.

**Implementado:** Estoque administrativo realocado para Gestão. Conferência e consumo clínico permanecem dentro da sessão, com as regras existentes.

**Destino:** Gestão → Estoque; sessão → consumo/conferência

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:37`.

### 15. Editar cadastro e foto

**Antes:** Faturamento mantém PacienteEdicaoWindow/PacienteEdicaoViewModel; Recepção usa PacienteWindow/PacienteEdicaoViewModel. Há também duas CapturaFotoWindow.

**Implementado:** Um cadastro e uma captura de foto. CPF válido e endereço obrigatórios; validação nos campos, dados mantidos após erro e proteção contra salvar antes de terminar a carga.

**Destino:** Ficha única → Editar cadastro; lista → Novo paciente

**Evidência:** `src/Clinica.Desktop.Shell/Componentes/Cadastro/CadastroPacienteViewModel.cs:21`.

### 16. Criar e remarcar horário

**Antes:** Dois AgendamentoEdicaoViewModel e dois formulários usam AgendaService.AgendarAsync/RemarcarAsync com conjuntos de campos e fluxos diferentes.

**Implementado:** Faturamento passa a usar a agenda e o editor da Recepção. O editor concorrente foi retirado; encaixe, profissional, sala e validações permanecem.

**Destino:** Agenda → Novo horário / Remarcar

**Evidência:** `src/Clinica.Desktop/App.xaml.cs:381`.

### 17. Autorizações de sessões do convênio

**Antes:** A ficha do Faturamento e a ficha da Recepção abrem editores distintos para AutorizacaoSessoes.

**Implementado:** A mesma listagem e editor de autorizações em todos os aplicativos; sem segunda ficha no Faturamento.

**Destino:** Ficha → Sessões e guias → Autorizações e validade do convênio

**Evidência:** `src/Clinica.Modulo.Clinico/Views/PacienteView.xaml:20`.

### 18. Usuários, permissões e redefinição de senha

**Antes:** Gerente e Faturamento mantêm cópias de AcessosView/UsuarioWindow/UsuarioEdicaoViewModel. Mesmo serviço de acesso com implementação de interface separada.

**Implementado:** Faturamento usa o módulo comum de acessos; lista e editor concorrentes retirados.

**Destino:** Gestão → Usuários e permissões

**Evidência:** `src/Clinica.Desktop/App.xaml.cs:381`.

### 19. Dados da clínica e regras do faturamento

**Antes:** ParametrosViewModel e ConfiguracoesViewModel leem e salvam os mesmos dados do prestador, janela de alerta, prazo de glosa e rodada de pendências, em formulários distintos.

**Implementado:** Dados da clínica e regras de faturamento usam componentes comuns. Configurações em seis abas. Catálogos TUSS e convênios preservados; salvar TUSS não apaga Pix/endereço.

**Destino:** Gestão → Configurações / Regras e catálogos TISS

**Evidência:** `src/Clinica.Desktop.Shell/Componentes/DadosClinicaViewModel.cs:33`.

### 20. Login, troca de usuário e senha

**Antes:** Faturamento possui LoginWindow e TrocaSenhaWindow próprias; demais aplicativos usam Shell. A versão Shell também possui fluxo de certificado.

**Implementado:** Faturamento usa login, troca de usuário e setup do Shell, mantendo migração, backup e atualização do executável.

**Destino:** Entrada, troca de usuário e configuração inicial comuns

**Evidência:** `src/Clinica.Desktop/App.xaml.cs:139`.

### 21. Estrutura visual, busca global e diálogos

**Antes:** Faturamento tem MainWindow/MainViewModel; demais aplicativos usam ShellWindow/ShellViewModel. Há cópias de SetupWindow, PromptWindow e controles de aviso, carregamento e estado vazio.

**Implementado:** Menus superiores, tokens, campos e componentes da suíte. Cópias de controles e paleta do antigo Faturamento retiradas; chaves são resolvidas após conferir autorização.

**Destino:** Shell e design system únicos para os cinco aplicativos

**Evidência:** `src/Clinica.Desktop.Shell/Shell/ShellViewModel.cs:248`.

### 22. Receber dinheiro do paciente

**Antes:** Pagamentos da Recepção usa ReceberPagamentoWindow; Contas usa BaixarLancamentoWindow; ficha/inadimplência usa CobrancaDoPacienteWindow. Mesma família de recebimento, mas saldos, escopos e permissões diferem.

**Implementado:** Valor, saldo, vencimento, data e cartão em uma interface comum. Serviços preservam a diferença entre recebimento do paciente, pagamento de fornecedor e liquidação de cartão.

**Destino:** Receber / pagar → formulário compartilhado

**Evidência:** `src/Clinica.Desktop.Shell/Componentes/RecebimentoWindow.xaml:12`.

### 23. Baixa e observação de guia

**Antes:** Pendências do Faturamento e Pendências gerenciais têm implementações próprias para baixa e anotação. Compartilham FaturamentoService.

**Implementado:** Gerente e Faturamento usam o mesmo componente de guias e baixa; anotação, baixa em lote e conferência continuam disponíveis.

**Destino:** Faturamento de guias → Pendências e baixas

**Evidência:** `src/Clinica.Modulo.Faturamento/ViewModels/DashboardViewModel.cs:361`.

### 24. Glosas e recursos

**Antes:** GlosasViewModel e GlosasGerencialViewModel mantêm listas e comandos de reapresentar/recuperar próprios. Faturamento ainda tem geração de recurso XML.

**Implementado:** Mesma tela, filtros, reapresentação, recuperação, recursos XML e histórico. Tela concorrente do Gerente retirada.

**Destino:** Faturamento de guias → Glosas e recursos

**Evidência:** `src/Clinica.Modulo.Faturamento/ViewModels/GlosasViewModel.cs:40`.

### 25. Não conformidades

**Antes:** NC no Painel do Faturamento e Não conformidades no TISS gerencial mantêm telas diferentes para a mesma fila e reabertura.

**Implementado:** Motivo, auditoria e reabertura na mesma implementação operacional; nome por extenso.

**Destino:** Faturamento de guias → Não conformidades

**Evidência:** `src/Clinica.Modulo.Faturamento/ViewModels/NaoConformidadesViewModel.cs:18`.

### 26. Confirmação de agenda em duas centrais

**Antes:** Confirmar sessões na agenda e Campanhas > Confirmação usam CampanhaService.GerarConfirmacoesAsync e histórico de contatos, com interfaces distintas.

**Implementado:** Página e janela contextual reutilizam ConfirmacoesView. Gerente navega à mesma fila; envio de lembrete não marca presença automaticamente.

**Destino:** Agenda → Confirmações; Relacionamento → atalho

**Evidência:** `src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs:225`.

### 27. Recall em Retorno de pacientes e Campanhas

**Antes:** RetornoViewModel e CampanhasViewModel geram recall pelo mesmo CampanhaService em interfaces diferentes. Quem parou de vir acrescenta análise de retenção.

**Implementado:** Atalho de campanhas abre a fila da Recepção. Consentimento, histórico de contato e análise de retenção preservados.

**Destino:** Gestão → Relacionamento → Recall de pacientes

**Evidência:** `src/Clinica.Modulo.Gerente/ViewModels/CampanhasViewModel.cs:233`.

### 28. Renovação de consulta do convênio

**Antes:** Painel de pendências do Faturamento e Consultas da Recepção chamam ConsultaService.RenovarAsync em ações próprias. O rótulo Consultas pode ser confundido com agenda médica.

**Implementado:** Ficha e filas globais usam RenovacaoConsultaFluxo. Confirmação explica que renovar a validade não registra consulta clínica.

**Destino:** Ficha → Sessões e guias → validade do convênio

**Evidência:** `src/Clinica.Desktop.Shell/Componentes/RenovacaoConsultaFluxo.cs:6`.

### 29. Orçamento do paciente e teto de gasto

**Antes:** Duas OrcamentoWindow: a Recepção emite orçamento de atendimento; Financeiro define teto mensal de categoria de despesas. Finalidades diferentes confirmadas nos campos.

**Implementado:** Funções distintas preservadas e identificadas: proposta ao paciente não é teto de despesas da clínica.

**Destino:** Orçamento do paciente / Inteligência → Resultado e teto de despesas

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:47`.

### 30. Conciliações com nomes semelhantes

**Antes:** Conciliação da agenda confere atendimentos; conciliação financeira trata receitas previstas; extrato bancário cruza pagamentos reais.

**Implementado:** Nomes explícitos para conferir atendimentos, conciliar receitas e conciliar extrato bancário; cada serviço e evidência mantidos.

**Destino:** Agenda → Conferir atendimentos; Financeiro → conciliações

**Evidência:** `src/Clinica.Desktop.Shell/Modulos/OrganizacaoNavegacao.cs:49`.

### 31. Termos, assinatura do paciente e assinatura profissional

**Antes:** AssinaturaPacienteWindow conduz coleta; PainelDoPacienteWindow é tela exclusiva para o paciente assinar; EscolherCertificadoWindow autoriza assinatura profissional. Modelos e guarda têm outras finalidades.

**Implementado:** Termos incorporados à ficha única. Coleta dedicada do paciente, assinatura profissional e guarda/exportação conservam permissões e significado próprios.

**Destino:** Ficha → Termos do paciente; Gestão → modelos/conformidade

**Evidência:** `src/Clinica.Modulo.Recepcao/Views/TermosPacienteView.xaml:1`.

### 32. Ficha e documentos dentro de janelas de atendimento

**Antes:** Fila abre PacientesView em ConsultaContextualWindow; workspace abre PacienteView/Documentos/Exames em janelas contextuais. Algumas já reutilizam a view, mas a Recepção ainda abre a ficha alternativa.

**Implementado:** Ficha rápida usa a mesma fábrica; cada workspace copia o contexto do paciente. Voltar restaura instância da tela, filtros e seleção de origem.

**Destino:** Consulta rápida → seção canônica → voltar à origem

**Evidência:** `src/Clinica.Modulo.Clinico/ViewModels/PacienteWorkspaceViewModel.cs:318`.

## Limites da verificação

Os cenários de interface utilizam dados fictícios e banco SQLite isolado. Verificam montagem, permissões, rotas, contexto e layout; não substituem aceite de operação em produção com equipamentos, certificados e banco reais. Os testes financeiros preservam operações distintas: evolução salva, sessão encerrada, guia faturada e dinheiro recebido não são estados equivalentes.

## Reproduzir

```powershell
dotnet build Clinica.sln --no-incremental
dotnet test tests/Clinica.Tests
dotnet test tests/Clinica.Assinaturas.Tests
python tools/verificar-suite.py
python tokens/verificar-espelho.py
python tools/compilar-sombra.py
dotnet run --project tools/Clinica.Verificacao.Desktop -- artifacts/navegacao-desktop
dotnet run --project tools/ValidarLayoutWindows -- --completo
dotnet run --project tools/ValidarLayoutWindows -- --gestao
```
