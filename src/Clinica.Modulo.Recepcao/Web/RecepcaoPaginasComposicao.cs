using System.Globalization;
using Clinica.Desktop.Shell.Web;
using Clinica.Domain.Entities;
using Clinica.Recepcao.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using P = Clinica.Desktop.Shell.Web.PaginasWebController;
namespace Clinica.Recepcao.Web;
public static partial class RecepcaoWebRegistro
{
    public static IEnumerable<P.Pagina> CriarPaginas()
    {
        yield return PaginaPrecosParticular();
        var estados = new System.Runtime.CompilerServices.ConditionalWeakTable<IServiceProvider, Dictionary<string, object>>();
        P.Pagina Preservar(P.Pagina pagina) => pagina with { Fabrica = sp => { var cache = estados.GetOrCreateValue(sp); if (!cache.TryGetValue(pagina.Chave, out var vm)) cache[pagina.Chave] = vm = pagina.Fabrica!(sp); return vm; } };
        foreach(var pagina in CriarPaginasBase()) {
            var p = AjustarPagina(pagina);
            yield return Preservar(p);
            if(p.Chave == "pacientes-recepcao") yield return Preservar(p with { Chave = "consultorio-pacientes", Titulo = "Pacientes em tratamento", Permissao = Permissao.VerProntuario,
                Fabrica = sp => { var vm = sp.GetRequiredService<PacientesViewModel>(); vm.SecaoInicial = 1; return PrepararPagina(sp, vm, "consultorio-pacientes"); } });
            if(p.Chave == "pacientes-recepcao") yield return Preservar(p with { Chave = "prontuario", Titulo = "Prontuário por paciente", Permissao = Permissao.VerProntuario,
                Fabrica = sp => { var vm = sp.GetRequiredService<PacientesViewModel>(); vm.SecaoInicial = 3; return PrepararPagina(sp, vm, "prontuario"); } });
        }
    }
    private static P.Pagina AjustarPagina(P.Pagina p)
    {
        // IDs e parâmetros permanecem objetos atuais do ViewModel, nunca IDs de banco vindos do navegador.
        if(p.Chave == "agenda-recepcao") p = p with { Secoes = [
            new("horarios", "Horários da agenda", null, [], [], [
                new P.Tab("horarios", "Horários", ["ColunasPlanejamento"], vm => ((AgendaViewModel)vm).ColunasPlanejamento.SelectMany(c => c.Blocos).Where(b => b.Cartao is not null).Select(b => (object)b.Cartao!).Distinct(),
                    [new("DataHora", "Início"), new("Fim", "Fim"), new("Paciente", "Paciente"), new("Profissional", "Profissional"), new("Sala", "Sala"), new("Modalidade", "Modalidade"), new("SituacaoDaFila", "Situação"), new("ConfirmacaoRotulo", "Confirmação"), new("Lancamento", "Lançamento"), new("Observacoes", "Observações")], [], [new("AbrirHorario", "Abrir horário")],
                    Celula: (_, row, campo) => campo switch { "DataHora" => ((CartaoAgenda)row).DataHora.ToString("O"), "Fim" => ((CartaoAgenda)row).Fim.ToString("O"), _ => null }, TipoLinha: typeof(CartaoAgenda)),
                new P.Tab("faixas", "Faixas disponíveis e encaixes", ["ColunasPlanejamento"], vm => ((AgendaViewModel)vm).ColunasPlanejamento.SelectMany(c => c.Blocos).SelectMany(b => new[]{b.Celula,b.Encaixe}).OfType<CelulaAgenda>().Cast<object>().Distinct(),
                    [new("Quando", "Horário"), new("ProfissionalId", "Profissional"), new("SalaId", "Sala"), new("Bloqueio", "Bloqueio"), new("ForaDoExpediente", "Expediente"), new("DicaDoVao", "Disponibilidade")], [], [new("AgendarNaFaixa", "Agendar neste horário",Permissao.EditarAgenda)],
                    Celula: (vm,row,campo) => campo switch { "Quando" => ((CelulaAgenda)row).Quando.ToString("O"), "ProfissionalId" => ((AgendaViewModel)vm).FiltroProfissionais.FirstOrDefault(x=>x.Id==((CelulaAgenda)row).ProfissionalId)?.Nome ?? "Sem profissional", "SalaId" => ((AgendaViewModel)vm).FiltroSalas.FirstOrDefault(x=>x.Id==((CelulaAgenda)row).SalaId)?.Nome ?? "Sem sala", _ => null }, TipoLinha: typeof(CelulaAgenda))
            ], []),
            p.Secoes.Single(s=>s.Chave=="VagasPlanejamento")
        ] };
        if(p.Chave=="agenda-recepcao")
        {
            var principais = new[]{"Dia","FiltroProfissional","FiltroSala"};
            var acoesPrincipais = new[]{"MarcarAtendimento","Hoje","DiaAnterior","ProximoDia","VerDiaPlanejamento","VerSemanaPlanejamento"};
            var planejamento = new P.Secao("planejamento","Planejamento e organização", "ResumoPlanejamento",
                p.Campos.Where(c=>!principais.Contains(c.Propriedade) && c.Tipo!="leitura").ToArray(),
                ["JornadaPlanejamento|Expediente","TravaPlanejamento|Conflitos","SalaPlanejamento|Disponibilidade"],[],
                p.Acoes.Where(a=>!acoesPrincipais.Contains(a.Comando)).Select(a=>a.Comando=="AbrirEspera"?a with{Rotulo="Lista de espera"}:a).ToArray());
            p=p with { Campos=p.Campos.Where(c=>principais.Contains(c.Propriedade)).ToArray(),
                Acoes=p.Acoes.Where(a=>acoesPrincipais.Contains(a.Comando)).Select(a=>a.Comando=="MarcarAtendimento"?a with{Estilo="primario"}:a).ToArray(),
                Secoes=[p.Secoes[0] with { Descricao="UltimaLeitura" },planejamento,p.Secoes[1] with{Titulo="Próximas vagas",Descricao="EstadoVagas"}],
                Indicadores=[] };
        }
        if(p.Chave=="fila") p=p with { Secoes=p.Secoes.Select(s=>s.Chave!="Linhas"?s:s with { Tabelas=s.Tabelas.Select(t=>t with { Colunas=[..t.Colunas, new("ConfirmacaoRotulo","Confirmação"),new("ProximoPasso","Próximo passo"),new("Detalhe","Pendências")], Acoes=[..t.Acoes,
            new("AbrirFicha","Ficha do paciente",Permissao.VerFichaPaciente),
            new("ColherTermo","Colher ou conferir termo",Permissao.ColherAssinaturaPaciente),
            new("FecharSessao","Fechar sessão (pacote, insumo, caixa)",Permissao.EditarAgenda,Visivel:"PodeFechar"),
            new("ConferirElegibilidade","Conferir convênio e cota",Permissao.VerFichaPaciente,Visivel:"EmAberto"),
            new("MarcarFalta","Marcar falta",Permissao.EditarAgenda,Visivel:"EmAberto"),
            new("Cancelar","Cancelar horário",Permissao.EditarAgenda,Visivel:"EmAberto"),
            new("AbrirNaGrade","Abrir na grade")
        ] }).ToArray() }).ToArray() };
        if(p.Chave=="pacientes-recepcao") p=p with { Secoes=[new("pacientes", "Pacientes encontrados", null, [], [], [PT("Seletor.Resultados", [new("Nome","Nome"),new("Documento","CPF / documento"),new("Telefone","Telefone")],[],[new("AbrirPaciente","Abrir ficha")])],[])] };
        if(p.Chave=="documentos") p=p with {
            Acoes=p.Acoes.Select(a=>a.Comando=="Gerar"?a with{Comando="EmitirFolhaSelecionada",Rotulo="Emitir documento"}:a).ToArray(),
            Campos=[..p.Campos, new("SoDoPaciente","Só deste paciente","booleano",Visivel:"TemPacienteEscolhido")],
            Secoes=p.Secoes.Select(s=>s.Chave!="Emitidas"?s:s with{ Tabelas=s.Tabelas.Select(t=>t with{ Acoes=[
                new("Reimprimir","Imprimir a 2ª via"), new("Assinar","Assinar com certificado",Visivel:"PodeAssinar"),
                new("Enviar","Enviar documento",Visivel:"PodeEnviar"),new("RenovarLink","Renovar link",Visivel:"PodeRenovarLink"),
                new("TirarDoAr","Retirar link do ar",Visivel:"PodeTirarDoAr"),new("Cancelar","Cancelar documento",Visivel:"PodeCancelar")
            ]}).ToArray() }).ToArray() };
        if(p.Chave=="consultas") p=p with { Permissao=Permissao.LancarAtendimento };
        if(p.Chave=="retornos-a-marcar") p=p with { Permissao=Permissao.VerAgenda };
        if(p.Chave=="retorno-pacientes") p=p with { Autorizado=()=>SessaoUsuario.Atual.PodeAlgum(Permissao.GerenciarCampanhas|Permissao.VerFaturamento),
            Acoes=p.Acoes.Select(a=>a.Comando=="Voltar"?a with{Visivel=null}:a.Comando=="LimparFiltros"?a with{Visivel=null}:a.Comando=="Atalho"?a with{Rotulo=a.Parametro?.ToString() switch {"assumir"=>"A assumir","hoje"=>"Contatos de hoje","atrasados"=>"Atrasados",_=>"Sem primeiro contato"}}:a).ToArray() };
        if(p.Campos.Any(c=>c.Propriedade=="Seletor.Termo"))
            p=p with { Acoes=[..p.Acoes,new("Seletor.LigarSugestao","Com horário hoje",Visivel:"Seletor.TemSugestao")] };
        return p with { Secoes=p.Secoes.Select(s=>s with { Visivel=s.Visivel ?? (s.Campos.Length==0 && s.Acoes.Length==0 && s.Tabelas.Length==1 ? s.Tabelas[0].Visivel : null) }).ToArray(), Campos=p.Campos.Select(c=>c with{Visivel=Presenca(c.Visivel)}).ToArray(), Acoes=p.Acoes.Select(a=>a with{Visivel=Presenca(a.Visivel)}).ToArray() };
    }
    private static string? Presenca(string? expressao) => expressao is null ? null : string.Join("&",expressao.Split('&').Select(p=> p is "Previa" or "Seletor.Selecionado" ? "presente:"+p : p));
}
