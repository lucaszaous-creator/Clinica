using System.Linq;
using P = Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Clinico.Web;
/// <summary>Campos e comandos explicitamente associados às mesmas propriedades das telas clínicas.</summary>
public static partial class ClinicoWebRegistro
{
    private static P.Secao MeuDia() => new("MeuDia", "Meu Dia", null,
        [new("Dia", "Dia", "data")],
        ["Profissional|Profissional", "MotivoDaLista|Motivo Da Lista", "AgendaFechada|Agenda Fechada"],
        [
            new("Sessoes", "Sessoes", ["Sessoes"], vm => ((Clinica.Clinico.ViewModels.MeuDiaViewModel)vm).Sessoes.Cast<object>(), [new("Hora", "Hora"), new("Paciente", "Paciente"), new("Contexto", "Contexto"), new("Observacoes", "Observacoes"), new("Status", "Status"), new("GrupoSituacao", "Grupo da situação"), new("StatusDetalhe", "Status Detalhe"), new("Prontuario", "Prontuario")], [], [new("Atender", "Atender", Guarda: "vm:PodeVerProntuario")]),
        ], [new("DiaAnterior", "<"), new("DiaSeguinte", ">"), new("Hoje", "Hoje"), new("Carregar", "Atualizar"), new("AbrirPendentes", "Abrir Pendentes", Guarda: "PodeVerProntuario")]);

    private static P.Secao RegistrosPendentes() => new("RegistrosPendentes", "Registros Pendentes", null,
        [new("FiltroPacienteRegistro", "Paciente", "texto"), new("FiltroProfissionalRegistro", "Profissional", "selecao", Opcoes: "ProfissionaisDaLista")],
        ["Resumo|Resumo", "MotivoDaLista|Motivo Da Lista"],
        [
            new("Pendentes", "Pendentes", ["Pendentes"], vm => ((Clinica.Clinico.ViewModels.RegistrosPendentesViewModel)vm).Pendentes.Cast<object>(), [new("Paciente", "Paciente"), new("Quando", "Quando"), new("Modalidade", "Modalidade"), new("Atraso", "Atraso")], [], [new("Escrever", "Escrever evolução")]),
        ], [new("Carregar", "Atualizar"), new("LimparFiltro", "Limpar filtro", Guarda: "FiltroAtivo")]);

    private static P.Secao MinhaSemana() => new("MinhaSemana", "Minha Semana", null,
        [new("Referencia", "Referencia", "data")],
        ["Profissional|Profissional", "Periodo|Periodo", "Resumo|Resumo", "MotivoDaLista|Motivo Da Lista"],
        [
            new("Cabecalhos", "Cabecalhos", ["Cabecalhos"], vm => ((Clinica.Clinico.ViewModels.MinhaSemanaViewModel)vm).Cabecalhos.Cast<object>(), [new("Titulo", "Titulo"), new("Subtitulo", "Subtitulo"), new("Resumo", "Resumo")], [], []),
            new("Faixas", "Faixas", ["Faixas"], vm => ((Clinica.Clinico.ViewModels.MinhaSemanaViewModel)vm).Faixas.Cast<object>(), [new("Rotulo", "Rotulo")], [], []),
        ], [new("SemanaAnterior", "<"), new("ProximaSemana", ">"), new("SemanaAtual", "Esta semana"), new("Carregar", "Atualizar")]);

    private static P.Secao MeusNumeros() => new("MeusNumeros", "Meus Numeros", null,
        [new("PeriodoEscolhido", "Periodo Escolhido", "selecao", Opcoes: "Periodos")],
        ["Profissional|Profissional", "Intervalo|Intervalo", "MotivoDaLista|Motivo Da Lista", "Atendidos|Atendidos", "VariacaoAtendidos.Texto|Texto", "VariacaoAtendidos.Rotulo|Rotulo", "Pacientes|Pacientes", "VariacaoPacientes.Texto|Texto", "VariacaoPacientes.Rotulo|Rotulo", "Faltas|Faltas", "VariacaoFaltas.Texto|Texto", "VariacaoFaltas.Rotulo|Rotulo", "NoShow|No Show", "VariacaoNoShow.Texto|Texto", "VariacaoNoShow.Rotulo|Rotulo", "Ocupacao|Ocupacao", "VariacaoOcupacao.Texto|Texto", "VariacaoOcupacao.Rotulo|Rotulo", "Completude|Completude", "VariacaoCompletude.Texto|Texto", "VariacaoCompletude.Rotulo|Rotulo", "Evolucoes|Evolucoes", "VariacaoEvolucoes.Texto|Texto", "VariacaoEvolucoes.Rotulo|Rotulo", "MelhoraDor|Melhora Dor", "VariacaoMelhoraDor.Texto|Texto", "VariacaoMelhoraDor.Rotulo|Rotulo", "LeituraCompletude|Leitura Completude", "DividaProntuario|Divida Prontuario"],
        [
        ], [new("Carregar", "Atualizar"), new("AbrirPendentes", "Ver e escrever")]);

    private static P.Secao Prontuarios() => new("Prontuarios", "Prontuarios", null,
        [new("Termo", "Buscar prontuários por paciente", "texto")],
        ["MotivoDaLista|Motivo Da Lista", "Resumo|Resumo"],
        [
            new("Linhas", "Linhas", ["Linhas"], vm => ((Clinica.Clinico.ViewModels.ProntuariosViewModel)vm).Linhas.Cast<object>(), [new("Paciente", "Paciente"), new("Profissional", "Profissional"), new("Data", "Data"), new("Detalhe", "Detalhe"), new("TipoRotulo", "Tipo Rotulo"), new("SituacaoRotulo", "Situacao Rotulo")], [], [new("Escrever", "Escrever", Guarda: "vm:PodeEscrever"), new("Assinar", "Assinar", Guarda: "vm:PodeAssinarDocumento"), new("Abrir", "Abrir")]),
        ], [new("NovoProntuario", "Novo Prontuario", Guarda: "PodeEscrever")]);

    private static P.Secao Exames() => new("Exames", "Exames", null,
        [],
        ["MotivoDaLista|Motivo Da Lista", "Resumo|Resumo"],
        [
            new("Pedidos", "Pedidos", ["Pedidos"], vm => ((Clinica.Clinico.ViewModels.ExamesViewModel)vm).Pedidos.Cast<object>(), [new("Paciente", "Paciente"), new("Profissional", "Profissional"), new("ExameRotulo", "Exame Rotulo"), new("Data", "Data"), new("SituacaoRotulo", "Situacao Rotulo")], [], [new("RegistrarResultado", "Anexar laudo", Guarda: "vm:PodeRegistrarResultado"), new("VerResultados", "Ver resultados"), new("Abrir", "Detalhes")]),
        ], [new("NovoPedido", "Novo Pedido", Guarda: "PodeEmitirPedido")]);

    private static P.Secao Prescricoes() => new("Prescricoes", "Prescricoes Clinicas", null,
        [new("Seletor.Termo", "Termo", "texto"), new("Seletor.Selecionado", "Selecionado", "selecao", Opcoes: "Seletor.Resultados", RotuloOpcao: "Nome")],
        ["Seletor.ResumoDaLista|Resumo Da Lista", "Seletor.Erro|Erro", "Paciente|Paciente"],
        [
            new("Seletor.Resultados", "Resultados", ["Seletor.Resultados"], vm => ((Clinica.Clinico.ViewModels.PrescricoesClinicasViewModel)vm).Seletor.Resultados.Cast<object>(), [new("Nome", "Nome")], [], []),
            new("FolhasParaEmitir", "Folhas Para Emitir", ["FolhasParaEmitir"], vm => ((Clinica.Clinico.ViewModels.PrescricoesClinicasViewModel)vm).FolhasParaEmitir.Cast<object>(), [new("Rotulo", "Rotulo")], [], [new("Emitir", "Emitir")]),
            new("Documentos", "Documentos", ["Documentos"], vm => ((Clinica.Clinico.ViewModels.PrescricoesClinicasViewModel)vm).Documentos.Cast<object>(), [new("Tipo", "Tipo"), new("Numero", "Numero"), new("Data", "Data"), new("Profissional", "Profissional"), new("Codigo", "Codigo"), new("Sessao", "Sessao"), new("Situacao", "Situacao"), new("Link", "Link")], [], [new("Assinar", "Assinar"), new("Imprimir", "2ª via")]),
        ], [new("Seletor.LigarSugestao", "Ligar Sugestao"), new("Seletor.DesligarSugestao", "Todos os pacientes"), new("IrParaInfusao", "Prescrição de infusão")]);

    private static P.Secao Infusoes() => new("Infusoes", "Prescricao Infusao", null,
        [new("Seletor.Termo", "Termo", "texto"), new("Seletor.Selecionado", "Selecionado", "selecao", Opcoes: "Seletor.Resultados", RotuloOpcao: "Nome")],
        ["Seletor.ResumoDaLista|Resumo Da Lista", "Seletor.Erro|Erro", "Paciente|Paciente"],
        [
            new("Seletor.Resultados", "Resultados", ["Seletor.Resultados"], vm => ((Clinica.Clinico.ViewModels.PrescricaoInfusaoViewModel)vm).Seletor.Resultados.Cast<object>(), [new("Nome", "Nome")], [], []),
            new("Prescricoes", "Prescricoes", ["Prescricoes"], vm => ((Clinica.Clinico.ViewModels.PrescricaoInfusaoViewModel)vm).Prescricoes.Cast<object>(), [new("Numero", "Numero"), new("Resumo", "Resumo"), new("Data", "Data"), new("Situacao", "Situacao"), new("Codigo", "Codigo"), new("Execucao", "Execucao")], [], [new("Editar", "Editar"), new("Abrir", "Abrir"), new("Imprimir", "Imprimir"), new("ImprimirExecucao", "Registro histórico"), new("Cancelar", "Cancelar")]),
        ], [new("Seletor.LigarSugestao", "Ligar Sugestao"), new("Seletor.DesligarSugestao", "Todos os pacientes"), new("Nova", "Nova prescrição", Guarda: "PodeCriarPrescricao"), new("CopiarUltimaPrescricao", "Copiar última prescrição deste paciente", Guarda: "PodeCriarPrescricao")]);

    private static P.Secao SessoesEnfermagem() => new("SessoesEnfermagem", "Sessoes Enfermagem", null,
        [new("Paciente", "Paciente", "texto"), new("Medico", "Médico responsável", "texto"), new("Inicio", "De (opcional)", "data"), new("Fim", "Até (opcional)", "data"), new("Situacao", "Situação", "selecao", Opcoes: "Situacoes", RotuloOpcao: "Nome", ValorOpcao: "Codigo")],
        [],
        [
            new("Sessoes", "Sessoes", ["Sessoes"], vm => ((Clinica.Clinico.ViewModels.SessoesEnfermagemViewModel)vm).Sessoes.Cast<object>(), [new("DataHora", "Data / hora"), new("Paciente", "Paciente"), new("Medico", "Médico"), new("ModalidadeTexto", "Modalidade"), new("Situacao", "Enfermagem"), new("Atendimento", "Atendimento médico")], [], [new("Abrir", "Abrir")]),
        ], [new("Filtrar", "Filtrar / atualizar"), new("Limpar", "Limpar filtros"), new("Anterior", "Anterior"), new("Proxima", "Próxima", Guarda: "Mais")]);

    private static P.Secao Atendimento() => new("Atendimento", "Atendimento", null,
        [new("RegistroSelecionadoParaVincular", "Há registros deste dia sem vínculo. Confira o histórico e selecione o registro desta sessão.", "selecao", Opcoes: "RegistrosParaVincular", RotuloOpcao: "Rotulo"), new("Data", "Data da sessão", "data"), new("EvaAntes", "EVA antes", "selecao", Opcoes: "EscalaEva"), new("EvaDepois", "EVA depois", "selecao", Opcoes: "EscalaEva"), new("RetornoSugeridoEm", "Retorno sugerido", "data"), new("TextoEvolucao", "Evolução da sessão", "texto-rico", Guarda: "PodeEditarProntuario", Formato: "TextoEvolucaoFormatado")],
        ["DicaRodape|Dica Rodape"],
        [
            new("AlertasClinicos", "Alertas Clinicos", ["AlertasClinicos"], vm => ((Clinica.Clinico.ViewModels.AtendimentoViewModel)vm).AlertasClinicos.Cast<object>(), [new("Texto", "Texto"), new("Detalhes", "Detalhes")], [], []),
            new("Alertas", "Alertas", ["Alertas"], vm => ((Clinica.Clinico.ViewModels.AtendimentoViewModel)vm).Alertas.Cast<object>(), [new("Texto", "Texto"), new("Detalhes", "Detalhes")], [], []),
            new("CamposPersonalizados", "Campos Personalizados", ["CamposPersonalizados"], vm => ((Clinica.Clinico.ViewModels.AtendimentoViewModel)vm).CamposPersonalizados.Cast<object>(), [new("Rotulo", "Rotulo"), new("Ajuda", "Ajuda")], [new("Resposta", "Resposta", "texto", Visivel: "EhCaixaDeTexto"), new("Resposta", "Resposta", "selecao", Opcoes: "Opcoes", Visivel: "EhLista"), new("Resposta", "Resposta", "selecao", ValorOpcao: "Content", Visivel: "EhSimNao", Fixas: ["Sim", "Não"])], []),
        ], [new("ImprimirFicha", "Imprimir", Guarda: "PodeImprimirFicha"), new("Salvar", "Salvar sessão", Guarda: "PodeEditarProntuario"), new("HistoricoConsulta.Alternar", "Consultar histórico"), new("VincularRegistro", "Vincular registro existente", Guarda: "PodeEditarProntuario"), new("CopiarUltimaEvolucao", "Copiar última evolução deste paciente", Guarda: "PodeCopiarUltimaEvolucao"), new("AbrirModelos", "Usar modelo", Guarda: "PodeEditarProntuario"), new("AbrirDetalhe", "Campos complementares", Guarda: "PodeEditarProntuario")]);

    private static P.Secao Enfermagem() => new("Enfermagem", "Atendimento Enfermagem", null,
        [new("Plano.Hora", "Hora", "texto"), new("Passagem.DataDoAtendimento", "Data Do Atendimento", "data"), new("Passagem.Hora", "Hora", "texto"), new("Passagem.Sistolica", "Sistolica", "texto"), new("Passagem.Diastolica", "Diastolica", "texto"), new("Passagem.Cardiaca", "Cardiaca", "texto"), new("Passagem.Respiratoria", "Respiratoria", "texto"), new("Passagem.Saturacao", "Saturacao", "texto"), new("Passagem.ConsultaCompleta", "Consulta Completa", "booleano"), new("Passagem.Intercorrencia", "Intercorrencia", "booleano"), new("Passagem.AlergiaObservada", "Alergia Observada", "texto"), new("Passagem.Texto", "Texto", "textarea")],
        ["Passagem.AvisoModalidadeEnfermagem|Aviso Modalidade Enfermagem", "AvisoDeLeitura|Aviso De Leitura", "Plano.Resumo|Resumo", "Passagem.Contexto|Contexto", "Passagem.EtapasEmFalta|Etapas Em Falta", "LinhaDoTempo.Resumo|Resumo"],
        [
            new("Plano.Cuidados", "Cuidados", ["Plano.Cuidados"], vm => ((Clinica.Clinico.ViewModels.AtendimentoEnfermagemViewModel)vm).Plano.Cuidados.Cast<object>(), [new("Redacao", "Redacao"), new("Selo", "Selo"), new("Registro", "Registro")], [], [new("Plano.MarcarFeito", "Feito", Guarda: "vm:Plano.PodeChecar"), new("Plano.MarcarNaoFeito", "Não feito", Guarda: "vm:Plano.PodeChecar")]),
            new("Passagem.Registros", "Registros", ["Passagem.Registros"], vm => ((Clinica.Clinico.ViewModels.AtendimentoEnfermagemViewModel)vm).Passagem.Registros.Cast<object>(), [new("Hora", "Hora"), new("Data", "Data"), new("Texto", "Texto"), new("SinaisVitais", "Sinais Vitais"), new("Marca", "Marca"), new("Assinatura", "Assinatura")], [], [new("Passagem.VincularSessao", "Vincular à sessão", Guarda: "vm:Passagem.PodeRegistrar"), new("Passagem.Corrigir", "Corrigir", Guarda: "vm:Passagem.PodeRegistrar"), new("Passagem.CancelarRegistro", "Cancelar", Guarda: "vm:Passagem.PodeRegistrar")]),
            new("LinhaDoTempo.Chips", "Chips", ["LinhaDoTempo.Chips"], vm => ((Clinica.Clinico.ViewModels.AtendimentoEnfermagemViewModel)vm).LinhaDoTempo.Chips.Cast<object>(), [new("Rotulo", "Rotulo"), new("Texto", "Texto"), new("Quantidade", "Quantidade")], [], [new("LinhaDoTempo.Escolher", "Escolher")]),
            new("LinhaDoTempo.Itens", "Itens", ["LinhaDoTempo.Itens"], vm => ((Clinica.Clinico.ViewModels.AtendimentoEnfermagemViewModel)vm).LinhaDoTempo.Itens.Cast<object>(), [new("Data", "Data"), new("HoraTexto", "Hora Texto"), new("Rotulo", "Rotulo"), new("Titulo", "Titulo"), new("Detalhe", "Detalhe"), new("Marca", "Marca"), new("Autor", "Autor")], [], [new("LinhaDoTempo.Ver", "Ver"), new("LinhaDoTempo.Abrir", "Abrir"), new("LinhaDoTempo.Cancelar", "Cancelar…")]),
        ], [new("RegistrarInfusao", "Registrar infusão"), new("AbrirFolha", "Abrir Folha", Guarda: "PodeAbrirFolha"), new("ColherTermo", "Colher Termo", Guarda: "PodeColherTermo"), new("Passagem.CancelarCorrecao", "Cancelar correção"), new("ImprimirFicha", "Imprimir a passagem", Guarda: "PodeVerProntuario"), new("Passagem.Registrar", "Registrar", Guarda: "Passagem.PodeRegistrarNova")]);

    private static P.Secao Capa() => new("Capa", "Paciente Capa", null,
        [new("IncluirProblemasEncerrados", "Mostrar também os resolvidos e descartados", "booleano")],
        ["Nascimento|Nascimento", "Sexo|Sexo", "Documento|Documento", "Telefone|Telefone", "Email|Email", "Convenio|Convenio", "Carteirinha|Carteirinha", "ValidadeCarteirinha|Validade Carteirinha", "EmTratamento|Em Tratamento", "Modalidade|Modalidade", "Endereco|Endereco", "ObservacoesCadastro|Observacoes Cadastro", "ResumoProblemas|Resumo Problemas"],
        [
            new("Problemas", "Problemas", ["Problemas"], vm => ((Clinica.Clinico.ViewModels.PacienteCapaViewModel)vm).Problemas.Cast<object>(), [new("Rotulo", "Rotulo"), new("Natureza", "Natureza"), new("Situacao", "Situacao"), new("Periodo", "Periodo"), new("Observacoes", "Observacoes")], [], [new("EditarProblema", "Editar"), new("ResolverProblema", "Resolvido"), new("ReabrirProblema", "Reabrir"), new("DescartarProblema", "Descartar")]),
        ], [new("NovoProblema", "Adicionar registro", Guarda: "PodeNovoProblema")]);

    private static P.Secao Prontuario() => new("Prontuario", "Prontuario Clinico", null,
        [new("TermoSessao", "Termo Sessao", "texto")],
        ["ResumoSessoes|Resumo Sessoes", "LinhaDoTempo.Resumo|Resumo"],
        [
            new("Sessoes", "Sessoes", ["Sessoes"], vm => ((Clinica.Clinico.ViewModels.ProntuarioClinicoViewModel)vm).Sessoes.Cast<object>(), [new("Data", "Data"), new("Eva", "Eva"), new("Profissional", "Profissional"), new("Queixa", "Queixa"), new("Conduta", "Conduta"), new("Evolucao", "Evolucao"), new("Orientacoes", "Orientacoes")], [], [new("AbrirSessao", "Abrir sessão"), new("VerAnexos", "Ver Anexos"), new("VerCorrecoes", "Ver Correcoes")]),
            new("LinhaDoTempo.Chips", "Chips", ["LinhaDoTempo.Chips"], vm => ((Clinica.Clinico.ViewModels.ProntuarioClinicoViewModel)vm).LinhaDoTempo.Chips.Cast<object>(), [new("Rotulo", "Rotulo"), new("Texto", "Texto"), new("Quantidade", "Quantidade")], [], [new("LinhaDoTempo.Escolher", "Escolher")]),
            new("LinhaDoTempo.Itens", "Itens", ["LinhaDoTempo.Itens"], vm => ((Clinica.Clinico.ViewModels.ProntuarioClinicoViewModel)vm).LinhaDoTempo.Itens.Cast<object>(), [new("Data", "Data"), new("HoraTexto", "Hora Texto"), new("Rotulo", "Rotulo"), new("Titulo", "Titulo"), new("Detalhe", "Detalhe"), new("Marca", "Marca"), new("Autor", "Autor")], [], [new("LinhaDoTempo.Ver", "Ver"), new("LinhaDoTempo.Abrir", "Abrir"), new("LinhaDoTempo.Cancelar", "Cancelar…")]),
        ], [new("Carregar", "Atualizar histórico")]);

    private static P.Secao Anexos() => new("Anexos", "Anexos Paciente", null,
        [],
        ["Resumo|Resumo"],
        [
            new("Chips", "Chips", ["Chips"], vm => ((Clinica.Clinico.ViewModels.AnexosPacienteViewModel)vm).Chips.Cast<object>(), [new("Rotulo", "Rotulo"), new("Texto", "Texto"), new("Quantidade", "Quantidade")], [], [new("EscolherSecao", "Escolher Secao")]),
            new("PedidosAguardando", "Pedidos Aguardando", ["PedidosAguardando"], vm => ((Clinica.Clinico.ViewModels.AnexosPacienteViewModel)vm).PedidosAguardando.Cast<object>(), [new("ExameRotulo", "Exame Rotulo"), new("Numero", "Numero"), new("Data", "Data")], [], [new("AnexarLaudo", "Anexar laudo…", Guarda: "vm:PodeRegistrar")]),
            new("Resultados", "Resultados", ["Resultados"], vm => ((Clinica.Clinico.ViewModels.AnexosPacienteViewModel)vm).Resultados.Cast<object>(), [new("Nome", "Nome"), new("Valor", "Valor"), new("Data", "Data"), new("Contexto", "Contexto"), new("Observacoes", "Observacoes")], [], [new("AbrirLaudo", "Abrir laudo"), new("CancelarResultado", "Cancelar…", Guarda: "vm:PodeRegistrar")]),
            new("ArquivosDaFicha", "Arquivos Da Ficha", ["ArquivosDaFicha"], vm => ((Clinica.Clinico.ViewModels.AnexosPacienteViewModel)vm).ArquivosDaFicha.Cast<object>(), [new("Titulo", "Titulo"), new("Contexto", "Contexto"), new("Observacoes", "Observacoes")], [], [new("AbrirArquivoDaFicha", "Abrir"), new("CancelarArquivoDaFicha", "Cancelar…", Guarda: "vm:PodeAnexar")]),
            new("Anexos", "Anexos", ["Anexos"], vm => ((Clinica.Clinico.ViewModels.AnexosPacienteViewModel)vm).Anexos.Cast<object>(), [new("NomeArquivo", "Nome Arquivo"), new("Contexto", "Contexto"), new("Descricao", "Descricao")], [], [new("Baixar", "Salvar…")]),
        ], [new("RegistrarResultado", "Registrar resultado…", Guarda: "PodeRegistrar"), new("AnexarArquivoDaFicha", "Anexar arquivo à ficha…", Guarda: "PodeAnexar")]);

    private static P.Secao Anamnese() => new("Anamnese", "Anamnese", null,
        [new("TextoDaSecao", "Texto Da Secao", "textarea", Guarda: "Editando&PodeEditar")],
        ["Procedencia|Procedencia", "RotuloDaSecao|Rotulo Da Secao", "DicaDaSecao|Dica Da Secao"],
        [
            new("Secoes", "Secoes", ["Secoes"], vm => ((Clinica.Clinico.ViewModels.AnamneseViewModel)vm).Secoes.Cast<object>(), [new("Rotulo", "Rotulo"), new("Texto", "Texto")], [], [new("AbrirSecao", "Abrir Secao")]),
            new("Versoes", "Versoes", ["Versoes"], vm => ((Clinica.Clinico.ViewModels.AnamneseViewModel)vm).Versoes.Cast<object>(), [new("Titulo", "Titulo"), new("Quando", "Quando"), new("Motivo", "Motivo"), new("Texto", "Texto")], [], []),
        ], [new("CancelarEdicao", "Cancelar", Visivel: "Editando"), new("Salvar", "Gravar", Guarda: "Editando&PodeEditar", Visivel: "Editando"), new("Editar", "Editar", Guarda: "PodeEditar", Visivel: "!Editando")]);

    private static P.Secao Dor() => new("Dor", "Evolucao Dor", null,
        [],
        ["Tendencia|Tendencia", "DorInicial|Dor Inicial", "DorAtual|Dor Atual", "GanhoAcumulado|Ganho Acumulado", "AlivioMedio|Alivio Medio"],
        [
            new("Sessoes", "Sessoes", ["Sessoes"], vm => ((Clinica.Clinico.ViewModels.EvolucaoDorViewModel)vm).Sessoes.Cast<object>(), [new("Data", "Data"), new("Antes", "Antes"), new("Depois", "Depois"), new("Variacao", "Variacao")], [], []),
        ], [new("Exportar", "Exportar CSV"), new("Carregar", "Atualizar")]);

    private static P.Secao Medidas() => new("Medidas", "Medidas", null,
        [],
        ["Primeiro|Primeiro", "Atual|Atual", "Variacao|Variacao", "LeituraSerie|Leitura Serie", "HistoricoSoDoConsultorio|Historico So Do Consultorio"],
        [
            new("Cartoes", "Cartoes", ["Cartoes"], vm => ((Clinica.Clinico.ViewModels.MedidasViewModel)vm).Cartoes.Cast<object>(), [new("Rotulo", "Rotulo"), new("Valor", "Valor"), new("Detalhe", "Detalhe")], [], []),
            new("Chips", "Chips", ["Chips"], vm => ((Clinica.Clinico.ViewModels.MedidasViewModel)vm).Chips.Cast<object>(), [new("Nome", "Nome")], [], [new("Acompanhar", "Acompanhar")]),
            new("Historico", "Historico", ["Historico"], vm => ((Clinica.Clinico.ViewModels.MedidasViewModel)vm).Historico.Cast<object>(), [new("Data", "Data"), new("Valor", "Valor"), new("Faixa", "Faixa"), new("Observacoes", "Observacoes")], [], [new("CancelarMedida", "Cancelar…")]),
        ], [new("VerProntuario", "Prontuário"), new("Registrar", "Registrar medida", Guarda: "PodeRegistrarMedida"), new("Exportar", "Exportar CSV", Guarda: "TemSerie")]);

    private static P.Secao Avaliacoes() => new("Avaliacoes", "Avaliacoes", null,
        [new("TodosOsInstrumentos", "Todas as escalas", "booleano")],
        ["Instrumento.Descricao|Descricao", "EscalaPrimeira|Escala Primeira", "EscalaAtual|Escala Atual", "EscalaGanho|Escala Ganho", "EscalaFaixa|Escala Faixa", "LeituraCurva|Leitura Curva"],
        [
            new("Chips", "Chips", ["Chips"], vm => ((Clinica.Clinico.ViewModels.AvaliacoesViewModel)vm).Chips.Cast<object>(), [new("Nome", "Nome")], [], [new("Escolher", "Escolher")]),
            new("Aplicacoes", "Aplicacoes", ["Aplicacoes"], vm => ((Clinica.Clinico.ViewModels.AvaliacoesViewModel)vm).Aplicacoes.Cast<object>(), [new("Data", "Data"), new("Instrumento", "Instrumento"), new("Observacoes", "Observacoes"), new("Alerta", "Alerta"), new("Pontuacao", "Pontuacao"), new("Faixa", "Faixa")], [], [new("CancelarAplicacao", "Cancelar…")]),
        ], [new("Aplicar", "Aplicar", Guarda: "PodeAplicar")]);

    private static P.Secao HistoricoConsulta() => new("HistoricoConsulta", "Historico Consulta", null,
        [],
        ["Filtro|Filtro"],
        [
            new("Itens", "Itens", ["Itens"], vm => ((Clinica.Clinico.ViewModels.HistoricoConsultaViewModel)vm).Itens.Cast<object>(), [new("Data", "Data"), new("Rotulo", "Rotulo"), new("Autor", "Autor"), new("HoraTexto", "Hora Texto"), new("Marca", "Marca"), new("Titulo", "Titulo"), new("Detalhe", "Detalhe")], [], [new("Ler", "Ler registro →")]),
        ], [new("Fechar", "Fechar histórico"), new("Filtrar", "Todos", Parametro: "Todos"), new("Filtrar", "Sessões", Parametro: "Sessões"), new("Filtrar", "Portal", Parametro: "Portal"), new("Atualizar", "Atualizar")]);

    private static P.Secao Emissoes() => new("Emissoes", "Emissoes No Atendimento", null,
        [],
        [],
        [
            new("SaiuHoje", "Saiu Hoje", ["SaiuHoje"], vm => ((Clinica.Clinico.ViewModels.AtendimentoViewModel)vm).SaiuHoje.Cast<object>(), [new("Rotulo", "Rotulo"), new("Numero", "Numero"), new("Detalhe", "Detalhe")], [], [new("SegundaVia", "2ª via")]),
            new("Entregar", "Entregar", ["Entregar"], vm => ((Clinica.Clinico.ViewModels.AtendimentoViewModel)vm).Entregar.Cast<object>(), [new("Rotulo", "Rotulo"), new("Heranca", "Heranca"), new("Pendencia", "Pendencia")], [], [new("EntregarFolha", "Emitir")]),
        ], [new("PrescreverInfusao", "Prescrever infusão", Guarda: "PodePrescreverInfusao")]);
}
