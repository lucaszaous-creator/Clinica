using Clinica.Clinico.Modulo;
using Clinica.Clinico.ViewModels;
using Clinica.Desktop.Shell.Web;
using Clinica.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

static partial class Fluxos
{
    static async Task ValidarFichaAsync(IServiceProvider sp, PaginasWebController pages,
        DialogosWebController dialogs, Func<Task<DialogoWebDto>> esperarDialogo)
    {
        await pages.NavegarAsync(ModuloClinico.ChavePaciente);
        var ficha = (PacienteWorkspaceViewModel)pages.ViewModelAtual!;
        await ficha.Anamnese.CarregarAsync();
        var campo = pages.ObterPagina().Secoes.SelectMany(s => s.Campos)
            .Single(c => c.Chave == "Anamnese.TextoDaSecao");
        Exigir(!campo.Habilitado, "Anamnese deve abrir em leitura.");
        var negou = false;
        try { await pages.AtualizarCampoAsync("Anamnese.TextoDaSecao", J("Escrita sem iniciar edição")); }
        catch (UnauthorizedAccessException) { negou = true; }
        Exigir(negou, "Ponte aceitou escrever anamnese fora da edição.");

        await pages.ExecutarAcaoAsync("Anamnese.Editar");
        var tabela = pages.ObterPagina().Secoes.SelectMany(s => s.Tabelas)
            .Single(t => t.Chave == "Anamnese.Secoes");
        Exigir(tabela.Linhas.Count == 6, "Anamnese perdeu uma das seis seções.");
        foreach (var linha in tabela.Linhas)
        {
            await pages.ExecutarAcaoAsync("Anamnese.AbrirSecao", tabela.Chave, linha.Id);
            await pages.AtualizarCampoAsync("Anamnese.TextoDaSecao", J("Registro sintético " + ficha.Anamnese.SecaoEscolhida));
        }
        await pages.ExecutarAcaoAsync("Anamnese.Salvar");
        Exigir(!ficha.Anamnese.Editando && !ficha.Anamnese.MensagemEhErro, "Anamnese não encerrou a gravação.");
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicaDbContext>();
            var salva = await db.Anamneses.SingleAsync();
            Exigir(new[] { salva.AntecedentesPessoais, salva.AntecedentesFamiliares, salva.HabitosDeVida,
                salva.HistoriaObstetrica, salva.RevisaoDeSistemas, salva.Observacoes }
                .All(t => t?.StartsWith("Registro sintético ") == true), "Uma seção da anamnese não persistiu.");
        }
        await pages.ExecutarAcaoAsync("Anamnese.Editar");
        await pages.AtualizarCampoAsync("Anamnese.TextoDaSecao", J("Revisão que será cancelada"));
        var salvar = pages.ExecutarAcaoAsync("Anamnese.Salvar");
        var pergunta = await esperarDialogo();
        dialogs.Fechar(pergunta.Id);
        await salvar;
        Exigir(ficha.Anamnese.Editando, "Cancelar justificativa descartou a edição em andamento.");
        using (var scope = sp.CreateScope())
            Exigir(!await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().VersoesAnamnese.AnyAsync(),
                "Cancelar revisão criou versão.");
        await pages.ExecutarAcaoAsync("Anamnese.CancelarEdicao");
        Exigir(ficha.Anamnese.TextoDaSecao.StartsWith("Registro sintético "), "Cancelar edição não restaurou anamnese.");
        await pages.ExecutarAcaoAsync("Anamnese.Editar");
        await pages.AtualizarCampoAsync("Anamnese.TextoDaSecao", J("Revisão sintética confirmada"));
        salvar = pages.ExecutarAcaoAsync("Anamnese.Salvar");
        pergunta = await esperarDialogo();
        await dialogs.AtualizarCampoAsync(pergunta.Id, "Texto", J("Correção sintética de QA"));
        await dialogs.ExecutarAcaoAsync(pergunta.Id, "confirmar");
        await salvar;
        using (var scope = sp.CreateScope())
            Exigir(await scope.ServiceProvider.GetRequiredService<ClinicaDbContext>().VersoesAnamnese.CountAsync() == 1,
                "Revisão não preservou a versão anterior.");
        Console.WriteLine("OK ficha: anamnese em leitura, seis seções gravadas, revisão cancelada/confirmada e versão preservada.");

        string LinhaProblema() => pages.ObterPagina().Secoes.SelectMany(s => s.Tabelas)
            .Single(t => t.Chave == "Capa.Problemas").Linhas.Single().Id;
        var resolver = pages.ExecutarAcaoAsync("Capa.ResolverProblema", "Capa.Problemas", LinhaProblema());
        pergunta = await esperarDialogo();
        dialogs.Fechar(pergunta.Id);
        await resolver;
        Exigir(ficha.Capa.Problemas.Count == 1, "Cancelar resolução retirou alerta ativo.");
        resolver = pages.ExecutarAcaoAsync("Capa.ResolverProblema", "Capa.Problemas", LinhaProblema());
        pergunta = await esperarDialogo();
        await dialogs.ExecutarAcaoAsync(pergunta.Id, "confirmar");
        await resolver;
        Exigir(ficha.Capa.Problemas.Count == 0, "Alerta resolvido ainda está nos ativos.");
        await pages.AtualizarCampoAsync("Capa.IncluirProblemasEncerrados", J(true));
        for (var i = 0; i < 100 && ficha.Capa.Problemas.Count == 0; i++) await Task.Delay(20);
        await pages.ExecutarAcaoAsync("Capa.ReabrirProblema", "Capa.Problemas", LinhaProblema());
        await pages.AtualizarCampoAsync("Capa.IncluirProblemasEncerrados", J(false));
        await ficha.Capa.CarregarAsync();
        Exigir(ficha.Capa.Problemas.Count == 1, "Reabrir não restaurou o alerta ativo.");
        Console.WriteLine("OK ficha: resolver alerta exige confirmação, cancelamento preserva e reabertura restaura ativo.");
    }
}
