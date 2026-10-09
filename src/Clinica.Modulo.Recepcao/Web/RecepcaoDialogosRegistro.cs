using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using D = Clinica.Desktop.Shell.Web.DialogosWebController;

namespace Clinica.Recepcao.Web;

/// <summary>Contrato explícito dos formulários da recepção. Somente estes campos e comandos alcançam os ViewModels.</summary>
public static partial class RecepcaoWebRegistro
{
    public static IEnumerable<D.RegistroDialogo> CriarDialogos()
    {
        // src/Clinica.Modulo.Recepcao/Janelas/AgendamentoWindow.xaml
        yield return new("RecepcaoAgendamento", typeof(AgendamentoEdicaoViewModel), new D.Definicao("Horário da agenda", null,
            [DF("Seletor.Termo", "Buscar por nome ou CPF", "texto", null, "Rotulo", null, "Seletor.Editavel", 4000),
             DF("Seletor.Selecionado", "Paciente", "selecao", "Seletor.Resultados", "Rotulo", "Seletor.TemResultados", null, 4000),
             DF("Seletor.Erro", "Erro", "leitura", visivel: null),
             DF("Data", "Data", "data", null, "Rotulo", null, null, 4000),
             DF("Hora", "Hora", "texto", null, "Rotulo", null, null, 5),
             DF("Duracao", "Duração (min)", "texto", null, "Rotulo", null, null, 4),
             DF("Profissional", "Profissional", "selecao", "Profissionais", "Rotulo", null, null, 4000),
             DF("Sala", "Sala", "selecao", "Salas", "Nome", null, null, 4000),
             DF("ModalidadeSelecionada", "Modalidade", "selecao", "Modalidades", "Nome", null, null, 4000),
             DF("EspecialidadeSelecionada", "Especialidade", "selecao", "Especialidades", "Nome", "ModalidadeConsulta", null, 4000),
             DF("Observacoes", "Observações", "textarea", null, "Rotulo", null, null, 4000),
             DF("CabecalhoDosAvisos", "Cabecalho Dos Avisos", "leitura", visivel: "TemConflito"),
             DF("AvisoJaLancado", "Aviso Ja Lancado", "leitura", visivel: "TemAvisoJaLancado"),
             DF("Encaixe", "Registrar como encaixe (atender por cima de um horário que já tem paciente)", "booleano", null, "Rotulo", null, null, 4000)],
            [DA("AssumirEncaixeCommand", "Assumir encaixe", habilitado: "TemConflito", linha: null), DA("SalvarCommand", "Salvar horário", habilitado: null, linha: null)],
            [
                new D.Tabela("Conflitos", "Conflitos", "Conflitos", [new D.Coluna("Texto", "Texto")], [], []),
                new D.Tabela("Elegibilidade", "Elegibilidade", "Elegibilidade", [new D.Coluna(".", "Aviso")], [], []),
            ]), Permissao.EditarAgenda);
        // src/Clinica.Modulo.Recepcao/Janelas/AutorizacaoWindow.xaml
        yield return new("RecepcaoAutorizacao", typeof(AutorizacaoEdicaoViewModel), new D.Definicao("Autorização de sessões", null,
            [DF("Numero", "Como veio da operadora", "texto", null, "Rotulo", null, null, 40),
             DF("ConvenioCodigo", "Convênio", "selecao", "Convenios", "Nome", null, null, 4000, valorOpcao: "Codigo"),
             DF("DataEmissao", "Emitida em", "data", null, "Rotulo", null, null, 4000),
             DF("DataValidade", "Válida até", "data", null, "Rotulo", null, null, 4000),
             DF("QuantidadeAutorizada", "Sessões autorizadas", "texto", null, "Rotulo", null, null, 4),
             DF("QuantidadeUtilizadaManual", "Já usadas antes do sistema", "texto", null, "Rotulo", null, null, 4),
             DF("Observacoes", "Observações", "textarea", null, "Rotulo", null, null, 4000),
             DF("Encerrada", "Encerrada (não conta mais como vigente)", "booleano", null, "Rotulo", null, null, 4000)],
            [DA("SalvarCommand", "Salvar autorização", habilitado: "PodeSalvar", linha: null)],
            [
            ]), Permissao.EditarPaciente);
        // src/Clinica.Modulo.Recepcao/Janelas/BloqueioWindow.xaml
        yield return new("RecepcaoBloqueio", typeof(BloqueioEdicaoViewModel), new D.Definicao("Fechar a agenda", null,
            [DF("Motivo", "Motivo", "texto", null, "Rotulo", null, null, 200),
             DF("DiaInteiro", "Dia inteiro", "booleano", null, "Rotulo", null, null, 4000),
             DF("InicioDia", "De", "data", null, "Rotulo", null, null, 4000),
             DF("InicioHora", "Hora", "texto", null, "Rotulo", null, null, 5),
             DF("FimDia", "Até", "data", null, "Rotulo", null, null, 4000),
             DF("FimHora", "Hora", "texto", null, "Rotulo", null, null, 5),
             DF("Profissional", "Profissional (em branco = a clínica inteira)", "selecao", "Profissionais", "Rotulo", null, null, 4000),
             DF("Sala", "Sala (em branco = todas)", "selecao", "Salas", "Nome", null, null, 4000)],
            [DA("SalvarCommand", "Fechar a agenda", habilitado: null, linha: null)],
            [
            ]), Autorizado: () => SessaoUsuario.Atual.Pode(Permissao.EditarAgenda) || SessaoUsuario.Atual.Pode(Permissao.GerenciarEquipe));
        // src/Clinica.Modulo.Recepcao/Janelas/ConciliacaoAgendaWindow.xaml
        yield return new("RecepcaoConciliacaoAgenda", typeof(ConciliacaoAgendaViewModel), new D.Definicao("Conferir atendimentos", null,
            [DF("Periodo", "Periodo", "leitura", visivel: null),
             DF("Resumo", "Resumo", "leitura", visivel: null)],
            [DA("CarregarCommand", "Atualizar", habilitado: null, linha: null)],
            [
                new D.Tabela("Parados", "Parados", "Parados", [new D.Coluna("Cabecalho", "Cabecalho"), new D.Coluna("Origem", "Origem"), new D.Coluna("Parado", "Parado"), new D.Coluna("Profissional", "Profissional"), new D.Coluna("Situacao", "Situacao"), new D.Coluna("Desfecho", "Desfecho")], [DA("MarcarFaltaCommand", "Foi falta", habilitado: null, linha: "PodeMarcarFalta"), DA("LancarRetroativoCommand", "Aconteceu — lançar", habilitado: null, linha: "PodeLancar"), DA("SubstituirCommand", "Já foi lançada — encerrar", habilitado: null, linha: "PodeSubstituir")], []),
                new D.Tabela("Orfaos", "Orfaos", "Orfaos", [new D.Coluna("Cabecalho", "Cabecalho"), new D.Coluna("Profissional", "Profissional")], [], []),
            ]), Permissao.EditarAgenda, AoAbrir: vm => ((ConciliacaoAgendaViewModel)vm).CarregarAsync());
        // src/Clinica.Modulo.Recepcao/Views/ConfirmacoesView.xaml
        yield return new("RecepcaoConfirmacoes", typeof(ConfirmacoesViewModel), new D.Definicao("Confirmações de agenda", null,
            [DF("Dia", "Dia", "data", null, "Rotulo", null, null, 4000),
             DF("Resumo", "Resumo", "leitura", visivel: null)],
            [DA("AmanhaCommand", "Amanhã", habilitado: null, linha: null), DA("GerarCommand", "Gerar rodada", habilitado: "PodeEditar", linha: null), DA("EnviarEmailsCommand", "Enviar e-mails", habilitado: "PodeEditar", linha: null), DA("CarregarCommand", "Atualizar", habilitado: null, linha: null)],
            [
                new D.Tabela("Contatos", "Contatos", "Contatos", [new D.Coluna("Horario", "Horario"), new D.Coluna("Paciente", "Paciente"), new D.Coluna("Profissional", "Profissional"), new D.Coluna("Situacao", "Situacao")], [DA("EnviarCommand", "Abrir WhatsApp", habilitado: "PodeEditar", linha: "TemTelefone"), DA("ConfirmouCommand", "Confirmar horário", habilitado: "PodeEditar", linha: "PodeConfirmar")], []),
            ]), Permissao.VerAgenda);
        // src/Clinica.Modulo.Recepcao/Janelas/EstornoAtendimentoWindow.xaml
        yield return new("RecepcaoEstornoAtendimento", typeof(EstornoAtendimentoViewModel), new D.Definicao("Estornar atendimento", null,
            [DF("Subtitulo", "Subtitulo", "leitura", visivel: null),
             DF("Impedimento", "Impedimento", "leitura", visivel: "TemImpedimento"),
             DF("Guias", "Guias", "leitura", visivel: null),
             DF("DesfazerCaixa", "Desfazer Caixa", "booleano", null, "Rotulo", "MostrarCaixa", null, 4000),
             DF("DevolverSessaoDoPacote", "Devolver a sessão ao saldo do pacote", "booleano", null, "Rotulo", "MostrarPacote", null, 4000),
             DF("DevolverInsumoAoEstoque", "Devolver Insumo Ao Estoque", "booleano", null, "Rotulo", "MostrarInsumo", null, 4000),
             DF("ConsultaRenovada", "Consulta Renovada", "leitura", visivel: "TemConsulta"),
             DF("Motivo", "Por que está estornando?", "textarea", null, "Rotulo", null, null, 300)],
            [DA("EstornarCommand", "Estornar atendimento", habilitado: "PodeEstornar", linha: null)],
            [
            ]), Permissao.LancarAtendimento, AoAbrir: vm => ((EstornoAtendimentoViewModel)vm).CarregarAsync());
        // src/Clinica.Modulo.Recepcao/Janelas/FechamentoSessaoWindow.xaml
        yield return new("RecepcaoFechamentoSessao", typeof(FechamentoSessaoViewModel), new D.Definicao("Concluir atendimento", null,
            [DF("Paciente", "Paciente", "leitura", visivel: null),
             DF("Data", "Data", "leitura", visivel: null),
             DF("ResumoDoQueVaiAcontecer", "Resumo Do Que Vai Acontecer", "leitura", visivel: "PropostaValida"),
             DF("DebitarPacote", "Debitar 1 sessão do pacote", "booleano", null, "Rotulo", "TemPacote&PropostaValida", null, 4000),
             DF("PacoteRotulo", "Pacote Rotulo", "leitura", visivel: "TemPacote&PropostaValida"),
             DF("GerarLancamento", "Registrar como esta sessão foi paga", "booleano", null, "Rotulo", "PropostaValida", "PodeLancarFinanceiro", 4000),
             DF("PagoAgora", "Pago agora", "booleano", null, "Rotulo", "PropostaValida", "GerarLancamento", 4000),
             DF("FicaAReceber", "Fica a receber", "booleano", null, "Rotulo", "PropostaValida", "GerarLancamento", 4000),
             DF("Valor", "Valor", "texto", null, "Rotulo", "PropostaValida", "GerarLancamento", 12),
             DF("ProcedenciaDoValor", "Procedencia Do Valor", "leitura", visivel: "PropostaValida"),
             DF("Forma", "Forma de pagamento", "selecao", "Formas", "Rotulo", "PagoAgora&PropostaValida", "GerarLancamento", 4000),
             DF("Vencimento", "Vence em", "data", null, "Rotulo", "FicaAReceber&PropostaValida", "GerarLancamento", 4000),
             DF("DataPagamento", "Data do pagamento", "data", null, "Rotulo", "PagoAgora&PropostaValida", "GerarLancamento", 4000),
             DF("Adquirente", "Maquininha / adquirente", "texto", null, "Rotulo", "EhCartao&PagoAgora&PropostaValida", "GerarLancamento", 60),
             DF("Bandeira", "Bandeira", "texto", null, "Rotulo", "EhCartao&PagoAgora&PropostaValida", "GerarLancamento", 40),
             DF("Parcelas", "Parcelas no crédito", "texto", null, "Rotulo", "EhCartao&PagoAgora&PropostaValida", "GerarLancamento", 2),
             DF("Categoria", "Categoria", "selecao", "Categorias", "Nome", "PropostaValida", "GerarLancamento", 4000)],
            [DA("ConfirmarCommand", "Concluir sessão", habilitado: "PropostaValida&NaoConcluida", linha: null)],
            [
                new D.Tabela("Insumos", "Insumos", "Insumos", [new D.Coluna("Nome", "Nome"), new D.Coluna("SaldoRotulo", "Saldo Rotulo")], [], [DF("Quantidade", "Quantidade", "texto", null, "Rotulo", null, null, 8)]),
            ]), Permissao.EditarAgenda);
        // src/Clinica.Modulo.Recepcao/Janelas/HorariosProfissionalWindow.xaml
        yield return new("RecepcaoHorariosProfissional", typeof(HorariosProfissionalViewModel), new D.Definicao("Jornada, bloqueios e travas", null,
            [DF("Profissional", "Profissional para configurar a agenda", "selecao", "Profissionais", "Nome", null, "PodeEditar", 4000),
             DF("AgendaProtegida", "Ativar trava para este profissional", "booleano", null, "Rotulo", null, "PodeEditar", 4000),
             DF("Das", "Início do expediente", "texto", null, "Rotulo", null, "PodeEditar", 5),
             DF("Ate", "Fim do expediente", "texto", null, "Rotulo", null, "PodeEditar", 5),
             DF("ResumoBloqueios", "Resumo Bloqueios", "leitura", visivel: null)],
            [DA("SalvarCommand", "Salvar configuração", habilitado: "PodeSalvar", linha: null), DA("CarregarCommand", "Atualizar lista", habilitado: "PodeEditar", linha: null), DA("FecharAgendaCommand", "Fechar agenda…", habilitado: "PodeEditar&TemFecharAgenda", linha: null), DA("CarregarBloqueiosCommand", "Atualizar bloqueios", habilitado: "PodeEditar", linha: null)],
            [
                new D.Tabela("Dias", "Dias de atendimento", "Dias", [new D.Coluna("Nome", "Dia")], [], [DF("Selecionado", "Selecionado", "booleano", null, "Rotulo", null, null, 4000)]),
                new D.Tabela("BloqueiosDoProfissional", "BloqueiosDoProfissional", "BloqueiosDoProfissional", [new D.Coluna(".", "Aviso")], [], []),
            ]), Permissao.EditarAgenda);
        // src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaWindow.xaml
        yield return new("RecepcaoListaEspera", typeof(ListaEsperaEdicaoViewModel), new D.Definicao("Lista de espera", null,
            [DF("Seletor.Termo", "Buscar por nome ou CPF", "texto", null, "Rotulo", null, null, 4000),
             DF("Seletor.Selecionado", "Paciente", "selecao", "Seletor.Resultados", "Rotulo", "Seletor.TemResultados", null, 4000),
             DF("Profissional", "Profissional desejado", "selecao", "Profissionais", "Rotulo", null, null, 4000),
             DF("Modalidade", "Modalidade", "selecao", "Modalidades", "Nome", null, null, 4000),
             DF("Periodo", "Turno", "selecao", "Periodos", "Rotulo", null, null, 4000),
             DF("DisponivelDe", "Pode vir a partir de", "data", null, "Rotulo", null, null, 4000),
             DF("DisponivelAte", "Até", "data", null, "Rotulo", null, null, 4000),
             DF("Prioritario", "Prioritário (entra na frente da fila)", "booleano", null, "Rotulo", null, null, 4000),
             DF("Observacoes", "Observações", "textarea", null, "Rotulo", null, null, 4000)],
            [DA("SalvarCommand", "Entrar na lista", habilitado: null, linha: null)],
            [
            ]), Permissao.EditarAgenda);
        // src/Clinica.Modulo.Recepcao/Janelas/ListaEsperaPainelWindow.xaml
        yield return new("RecepcaoListaEsperaPainel", typeof(AgendaViewModel), new D.Definicao("Lista de espera", null,
            [DF("TituloEspera", "Titulo Espera", "leitura", visivel: null)],
            [DA("NovoPedidoEsperaCommand", "Adicionar à lista", habilitado: "PodeEditarAgenda", linha: null), DA("VerListaInteiraCommand", "Ver a lista inteira", habilitado: "TemSugestao", linha: null)],
            [
                new D.Tabela("Espera", "Espera", "Espera", [new D.Coluna("Paciente", "Paciente"), new D.Coluna("Preferencias", "Preferencias"), new D.Coluna("Desde", "Desde")], [DA("ChamarDaEsperaCommand", "Chamar", habilitado: null, linha: null), DA("RemoverDaEsperaCommand", "Sair da lista", habilitado: null, linha: null)], []),
            ]), Permissao.VerAgenda);
        // src/Clinica.Modulo.Recepcao/Janelas/OrcamentoWindow.xaml
        yield return new("RecepcaoOrcamento", typeof(OrcamentoViewModel), new D.Definicao("Orçamento", null,
            [DF("Titulo", "Título (opcional)"), DF("Paciente", "Paciente", "leitura", visivel: null),
             DF("TotalGeral", "Total Geral", "leitura", visivel: null),
             DF("Destinatario", "Para quem", "texto", null, "Rotulo", null, null, 120),
             DF("DocumentoDestinatario", "CPF/CNPJ (opcional)", "texto", null, "Rotulo", null, null, 20),
             DF("Data", "Data", "data", null, "Rotulo", null, null, 4000),
             DF("ValidoAte", "Válido até", "data", null, "Rotulo", null, null, 4000),
             DF("Observacoes", "Observações (opcional)", "textarea", null, "Rotulo", null, null, 600)],
            [DA("EmitirCommand", "Emitir e imprimir", habilitado: null, linha: null), DA("AdicionarItemCommand", "Adicionar linha", habilitado: null, linha: null), DA("RecalcularCommand", "Recalcular total", habilitado: null, linha: null)],
            [
                new D.Tabela("Itens", "Itens", "Itens", [new D.Coluna("Total", "Total")], [DA("RemoverItemCommand", "Remover item", habilitado: null, linha: null)], [DF("Descricao", "Descricao", "texto", null, "Rotulo", null, null, 160), DF("Quantidade", "Quantidade", "texto", null, "Rotulo", null, null, 6), DF("ValorUnitario", "Valor Unitario", "texto", null, "Rotulo", null, null, 14)]),
            ]), Autorizado: () => SessaoUsuario.Atual.Pode(Permissao.VenderPacote) || SessaoUsuario.Atual.Pode(Permissao.EditarFinanceiro));
        // src/Clinica.Modulo.Recepcao/Janelas/ProfissionalWindow.xaml
        yield return new("RecepcaoProfissional", typeof(ProfissionalEdicaoViewModel), new D.Definicao("Profissional", null,
            [DF("Nome", "Nome", "texto", null, "Rotulo", null, null, 120),
             DF("NomeCurto", "Nome curto (agenda)", "texto", null, "Rotulo", null, null, 40),
             DF("RegistroConselho", "Conselho e registro", "texto", null, "Rotulo", null, null, 40),
             DF("Cpf", "CPF", "texto", null, "Rotulo", null, null, 14),
             DF("Especialidade", "Especialidade principal", "selecao", "Especialidades", "Nome", null, null, 4000),
             DF("DuracaoPadrao", "Duração padrão (min)", "texto", null, "Rotulo", null, null, 4),
             DF("AtendeSeg", "seg", "booleano", null, "Rotulo", null, null, 4000),
             DF("AtendeTer", "ter", "booleano", null, "Rotulo", null, null, 4000),
             DF("AtendeQua", "qua", "booleano", null, "Rotulo", null, null, 4000),
             DF("AtendeQui", "qui", "booleano", null, "Rotulo", null, null, 4000),
             DF("AtendeSex", "sex", "booleano", null, "Rotulo", null, null, 4000),
             DF("AtendeSab", "sáb", "booleano", null, "Rotulo", null, null, 4000),
             DF("AtendeDom", "dom", "booleano", null, "Rotulo", null, null, 4000),
             DF("AtendeDas", "Início do expediente", "texto", null, "Rotulo", null, null, 5),
             DF("AtendeAte", "Fim do expediente", "texto", null, "Rotulo", null, null, 5),
             DF("Telefone", "Telefone", "texto", null, "Rotulo", null, null, 20),
             DF("Email", "E-mail", "texto", null, "Rotulo", null, null, 120),
             DF("Cor", "Cor na agenda (#RRGGBB)", "texto", null, "Rotulo", null, null, 9),
             DF("Ordem", "Ordem nas listas", "texto", null, "Rotulo", null, null, 4),
             DF("Ativo", "Ativo (recebe horários novos; os já marcados seguem na agenda, marcados como de inativo)", "booleano", null, "Rotulo", null, null, 4000),
             DF("Observacoes", "Observações", "textarea", null, "Rotulo", null, null, 4000)],
            [DA("SalvarCommand", "Salvar profissional", habilitado: null, linha: null)],
            [
                new D.Tabela("Atendimentos", "Atendimentos habilitados", "Atendimentos", [new D.Coluna("Rotulo", "Atendimento")], [], [DF("Habilitado", "Habilitado", "booleano", null, "Rotulo", null, "Permitido", 4000)]),
            ]), Permissao.GerenciarEquipe);
        // src/Clinica.Modulo.Recepcao/Janelas/SalaWindow.xaml
        yield return new("RecepcaoSala", typeof(SalaEdicaoViewModel), new D.Definicao("Sala", null,
            [DF("Nome", "Nome", "texto", null, "Rotulo", null, null, 80),
             DF("Capacidade", "Capacidade", "texto", null, "Rotulo", null, null, 3),
             DF("Ordem", "Ordem nas listas", "texto", null, "Rotulo", null, null, 4),
             DF("Ativa", "Ativa (recebe horários novos; os já marcados seguem na agenda, marcados como de inativa)", "booleano", null, "Rotulo", null, null, 4000),
             DF("Observacoes", "Observações", "textarea", null, "Rotulo", null, null, 4000)],
            [DA("SalvarCommand", "Salvar sala", habilitado: null, linha: null)],
            [
            ]), Permissao.GerenciarEquipe);
        yield return DialogoRecebimento();
        foreach (var escolha in CriarDialogosDeEscolha()) yield return escolha;
    }

    private static D.Campo DF(string caminho, string rotulo, string tipo = "texto", string? opcoes = null, string rotuloOpcao = "Rotulo", string? visivel = null, string? habilitado = null, int maximo = 4000, string? valorOpcao = null) =>
        new(caminho, caminho, rotulo, tipo, opcoes, rotuloOpcao, visivel, habilitado, Maximo: maximo, ValorOpcao: valorOpcao);
    private static D.Acao DA(string comando, string rotulo, string? habilitado = null, string? linha = null) =>
        new(comando, rotulo, comando, comando.Contains("Salvar") || comando.Contains("Confirmar") ? "primario" : "secundario", habilitado, linha);
}