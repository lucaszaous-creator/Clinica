using Clinica.Desktop.Shell.Modulos;
using System.Collections;
using System.Runtime.CompilerServices;
using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Componentes;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using P = Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Clinico.Web;

public static partial class ClinicoWebRegistro
{
    private sealed class ContextoPaciente { public PacienteWorkspaceViewModel? Atual; public int Usuario; }
    private static readonly ConditionalWeakTable<IServiceProvider, ContextoPaciente> Contextos = new();
    private static PacienteWorkspaceViewModel Paciente(IServiceProvider servicos, string chave)
    {
        SessaoUsuario.Atual.ExigirAlgum(Permissao.VerProntuario | Permissao.VerFichaPaciente, "abrir a ficha do paciente");
        var foco = servicos.GetRequiredService<PacienteEmFoco>();
        var contexto = Contextos.GetOrCreateValue(servicos);
        if (contexto.Atual is null || contexto.Usuario != SessaoUsuario.Atual.UsuarioId || !contexto.Atual.Corresponde(foco))
        {
            contexto.Atual?.AoSairDeCena();
            contexto.Atual = new PacienteWorkspaceViewModel(servicos, foco, ModuloClinico.AbaDe(chave), ModuloClinico.SubAbaDe(chave));
            contexto.Usuario = SessaoUsuario.Atual.UsuarioId;
        }
        contexto.Atual.AbaAtual = ModuloClinico.AbaDe(chave);
        contexto.Atual.SubAbaAcompanhamento = ModuloClinico.SubAbaDe(chave);
        return contexto.Atual;
    }
    public static IEnumerable<P.Pagina> CriarPaginas(IEnumerable<P.Secao>? secoesAdministrativas = null)
    {
        yield return Tela<SalaInfusaoViewModel>(ModuloClinico.ChaveSalaInfusao,"Sala de infusão",Permissao.VerProntuario,SalaInfusaoCompleta());
        yield return Tela<EnfermagemViewModel>(ModuloClinico.ChaveEnfermagem,"Enfermagem",Permissao.RegistrarEvolucaoEnfermagem,EnfermagemCompleta()) with { AoAbrir=async vm=>{var e=(EnfermagemViewModel)vm;await e.CarregarAsync();e.AoEntrarEmCena();},AoFechar=vm=>((EnfermagemViewModel)vm).AoSairDeCena() };
        yield return Tela<MeuDiaViewModel>(ModuloClinico.ChaveMeuDia,"Meu dia",Permissao.VerAgenda,MeuDia()) with {
            AoAbrir = vm => { ((MeuDiaViewModel)vm).AoEntrarEmCena(); return Task.CompletedTask; }, AoFechar = vm => ((MeuDiaViewModel)vm).AoSairDeCena() };
        yield return Tela<MinhaSemanaViewModel>(ModuloClinico.ChaveMinhaSemana,"Minha semana",Permissao.VerAgenda,MinhaSemana());
        yield return Tela<RegistrosPendentesViewModel>(ModuloClinico.ChaveRegistrosPendentes,"Sessões sem evolução",Permissao.VerProntuario,RegistrosPendentes());
        yield return Tela<ProntuariosViewModel>(ModuloClinico.ChaveProntuarios,"Prontuários",Permissao.VerProntuario,Prontuarios());
        yield return Tela<ExamesViewModel>(ModuloClinico.ChaveExames,"Exames",Permissao.VerProntuario,Exames());
        yield return Tela<PrescricoesClinicasViewModel>(ModuloClinico.ChavePrescricoes,"Receitas e documentos",Permissao.VerProntuario,Prescricoes());
        yield return Tela<PrescricaoInfusaoViewModel>(ModuloClinico.ChavePrescricaoInfusao,"Prescrição de infusão",Permissao.Prescrever,Infusoes());
        yield return Tela<MeusNumerosViewModel>(ModuloClinico.ChaveMeusNumeros,"Meus números",Permissao.VerAgenda,MeusNumeros());
        yield return Tela<SessoesEnfermagemViewModel>(ModuloClinico.ChaveSessoesEnfermagem,"Sessões de enfermagem",Permissao.RegistrarEvolucaoEnfermagem,SessoesEnfermagem()) with { AoAbrir=vm=>((SessoesEnfermagemViewModel)vm).CarregarAsync() };
        yield return Tela<MedicamentosViewModel>(ModuloClinico.ChaveMedicamentos,"Medicamentos",Permissao.Prescrever,
            new("Catalogo","Catálogo compartilhado",null,[new("Busca","Buscar medicamento")],[],[
                new("Medicamentos","Medicamentos","Medicamentos".Split(','),vm=>((MedicamentosViewModel)vm).Medicamentos.Cast<object>(),[new("Nome","Nome"),new("PrincipioAtivo","Princípio ativo"),new("Apresentacao","Apresentação"),new("Fabricante","Fabricante"),new("Fonte","Fonte"),new("Ativo","Ativo")],[],[new("Editar","Editar")])],
                [new("Novo","Novo medicamento"),new("Carregar","Atualizar"),new("Importar","Importar CSV"),new("ExportarModelo","Modelo de planilha")]),
            new("Edicao","Cadastro do medicamento",null,[new("Nome","Nome do medicamento"),new("PrincipioAtivo","Princípio ativo"),new("Apresentacao","Apresentação / concentração"),new("Fabricante","Fabricante"),new("Ativo","Ativo nas sugestões","booleano")],[],[],[new("Salvar","Salvar cadastro",Estilo:"primario")]));
        foreach (var (chave,titulo,secoes) in new (string,string,P.Secao[])[] {
            (ModuloClinico.ChaveAtendimento,"Evolução da sessão",[ComPrefixo(Atendimento(),"Atendimento"),ComPrefixo(HistoricoConsulta(),"Atendimento.HistoricoConsulta")]),
            (ModuloClinico.ChaveAtendimentoEnfermagem,"Atendimento de enfermagem",[ComPrefixo(Enfermagem(),"Enfermagem")]),
            (ModuloClinico.ChavePaciente,"Ficha do paciente",[ComPrefixo(Capa(),"Capa"),ComPrefixo(Anamnese(),"Anamnese"),ComPrefixo(SessoesDoPaciente(),"Capa"),..secoesAdministrativas??[]]),
            (ModuloClinico.ChaveProntuario,"Histórico clínico",[ComPrefixo(Prontuario(),"Prontuario")]),
            (ModuloClinico.ChaveExamesDoPaciente,"Exames e anexos",[ComPrefixo(Anexos(),"Anexos")]),
            (ModuloClinico.ChaveEvolucaoDor,"Evolução da dor",[ComPrefixo(Dor(),"Dor")]),
            (ModuloClinico.ChaveMedidas,"Medidas",[ComPrefixo(Medidas(),"Medidas")]),
            (ModuloClinico.ChaveAvaliacoes,"Avaliações",[ComPrefixo(Avaliacoes(),"Avaliacoes")]) })
        {
            var pagina = Tela<PacienteWorkspaceViewModel>(chave,titulo,chave==ModuloClinico.ChavePaciente?Permissao.Nenhuma:Permissao.VerProntuario,secoes) with {
                Fabrica=sp=>Paciente(sp,chave), Subtitulo="Paciente", Autorizado=()=>SessaoUsuario.Atual.Pode(Permissao.VerProntuario)||SessaoUsuario.Atual.Pode(Permissao.VerFichaPaciente),
                AoAbrir=async vm=>{var paciente=(PacienteWorkspaceViewModel)vm;paciente.AoEntrarEmCena();if(chave==ModuloClinico.ChavePaciente && paciente.Administrativo is not null)await paciente.Administrativo.AtualizarAsync();},AoFechar=vm=>((PacienteWorkspaceViewModel)vm).AoSairDeCena(),
                Graficos=GraficosPaciente, Campos=[new("FotoWeb","Foto do paciente","imagem-leitura"),new("Paciente","Paciente","leitura"),new("Contexto","Contexto","leitura"),new("Cabecalho.Linha","Identificação","leitura"),new("Cabecalho.CarteirinhaTexto","Carteirinha","leitura"),new("Cabecalho.AlergiasTexto","Alergias","leitura"),new("Cabecalho.ProblemasTexto","Problemas ativos","leitura"),new("Cabecalho.DiagnosticosTexto","Diagnósticos","leitura"),new("SituacaoSessao","Atendimento","leitura"),new("Cronometro","Tempo de atendimento","leitura"),new("MensagemSessao","Mensagem do atendimento","leitura"),new("AvisoConclusaoAutomatica","Conclusão da sessão","leitura")],
                Acoes=[new("Voltar","Voltar"),new("VerAtendimentoWeb","Evolução da sessão",Permissao.EditarProntuario),new("VerEnfermagemWeb","Enfermagem",Permissao.RegistrarEvolucaoEnfermagem),new("VerFichaWeb","Ficha"),new("VerHistoricoWeb","Histórico",Permissao.VerProntuario),new("VerExamesWeb","Exames",Permissao.VerProntuario),new("VerDorWeb","Dor",Permissao.VerProntuario),new("VerMedidasWeb","Medidas",Permissao.VerProntuario),new("VerAvaliacoesWeb","Avaliações",Permissao.VerProntuario),new("EmitirDocumentos","Documentos",Permissao.VerProntuario),new("Atendimento.EmitirTipo","Receita",Permissao.Prescrever,Parametro:"receita",Chave:"emitir-receita"),new("Atendimento.EmitirTipo","Atestado médico",Permissao.Prescrever,Parametro:"atestado",Chave:"emitir-atestado"),new("Atendimento.EmitirTipo","Declaração de comparecimento",Permissao.EditarPaciente,Parametro:"comparecimento",Chave:"emitir-comparecimento"),new("Atendimento.EmitirTipo","Pedido de exame",Permissao.Prescrever,Parametro:"pedido-exame",Chave:"emitir-exame"),new("Atendimento.PrescreverInfusao","Prescrição de infusão",Guarda:"Atendimento.PodePrescreverInfusao"),new("AtualizarFicha","Atualizar ficha"),new("ConsultarFicha","Consultar ficha"),new("ConsultarExames","Consultar exames"),new("DocumentosPacienteWeb","Prescrições do paciente",Permissao.VerProntuario),new("IniciarSessao","Iniciar atendimento",Guarda:"PodeIniciar"),new("ReabrirSessao","Reabrir atendimento",Guarda:"PodeReabrir"),new("RegistrarMateriais","Registrar materiais",Guarda:"MostrarMateriais"),new("Atendimento.IndicarBsv","Indicação de procedimento",Guarda:"Atendimento.PodeIndicarBsv"),new("Atendimento.ColherTermo","Colher termo",Guarda:"Atendimento.PodeColherTermo"),new("FinalizarSessao","Concluir atendimento",Guarda:"PodeFinalizarSessao")] };
            yield return pagina;
        }
    }
    private static P.Secao SessoesDoPaciente()=>new("SessoesDoPaciente","Sessões e guias",null,[],["ResumoSessoes|Resumo das sessões"],[
        new("Sessoes","Sessões do paciente",["Sessoes"],vm=>((PacienteCapaViewModel)vm).Sessoes.Cast<object>(),[new("DataTexto","Data"),new("ModalidadeTexto","Modalidade"),new("ProfissionalTexto","Profissional"),new("Situacao","Situação"),new("EvolucaoTexto","Evolução"),new("GuiasTexto","Guias"),new("Protocolo","Protocolo")],[],[])],
        [new("PaginaAnterior","Página anterior",Guarda:"PodePaginaAnterior"),new("ProximaPagina","Próxima página",Guarda:"PodeProximaPagina")]);
    private static P.Pagina Tela<T>(string chave,string titulo,Permissao permissao,params P.Secao[] secoes)
        => new(chave,titulo,typeof(T),[],[],secoes.Select(Aprimorar).ToArray(),[],Subtitulo:typeof(T).GetProperty("Resumo") is null?null:"Resumo",Permissao:permissao,Fabrica:sp=>ActivatorUtilities.GetServiceOrCreateInstance<T>(sp)!);
    private static object Objeto(object vm,string prefixo)
    { foreach(var parte in prefixo.Split('.')) vm=vm.GetType().GetProperty(parte)?.GetValue(vm)??throw new InvalidOperationException("Contexto clínico indisponível: "+prefixo);return vm; }
    private static P.Secao ComPrefixo(P.Secao s,string prefixo)
    {
        s=Aprimorar(s);
        string? Caminho(string? p)=>p is null?null:prefixo+"."+p;
        string? Condicao(string? p)=>p is null?null:string.Join('&',p.Split('&').Select(g=>(g.StartsWith('!')?"!":"")+prefixo+"."+g.TrimStart('!')));
        P.Campo Campo(P.Campo c)=>c with {Propriedade=Caminho(c.Propriedade)!,Opcoes=Caminho(c.Opcoes),Guarda=Condicao(c.Guarda),Visivel=Condicao(c.Visivel),Formato=Caminho(c.Formato)};
        P.Acao Acao(P.Acao a)=>a with {Comando=Caminho(a.Comando)!,Selecao=Caminho(a.Selecao),Visivel=Condicao(a.Visivel),Guarda=a.Guarda?.StartsWith("vm:")==true?"vm:"+Condicao(a.Guarda[3..]):Condicao(a.Guarda)};
        return s with {Chave=prefixo+"."+s.Chave,Descricao=Caminho(s.Descricao),Campos=s.Campos.Select(Campo).ToArray(),Indicadores=s.Indicadores.Select(i=>prefixo+"."+i).Append(prefixo+".Mensagem|Mensagem").ToArray(),Acoes=s.Acoes.Select(Acao).ToArray(),Visivel=Condicao(s.Visivel),
            Tabelas=s.Tabelas.Select(t=>t with {Chave=prefixo+"."+t.Chave,Origens=t.Origens.Select(o=>Caminho(o)!).ToArray(),Linhas=vm=>t.Linhas(Objeto(vm,prefixo)),Acoes=t.Acoes.Select(Acao).ToArray(),Campos=t.Campos.Select(c=>c with {Opcoes=c.Opcoes?.StartsWith("vm:")==true?"vm:"+Caminho(c.Opcoes[3..]):c.Opcoes}).ToArray(),Visivel=Condicao(t.Visivel)}).ToArray() };
    }
}
