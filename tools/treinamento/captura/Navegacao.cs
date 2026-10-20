using System.IO;
using System.Text.Json;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

// A NAVEGAÇÃO REAL dos cinco apps (out/2026), para as videoaulas. As telas são Blazor e a
// gravação acontece na prévia (Linux), onde o shell WPF não existe: o menu do topo, as abas
// das telas compostas e QUAL componente cada chave abre só o ShellViewModel sabe — com as
// regras de OrganizacaoNavegacao, a dedupe por chave e o filtro de permissão. Escrever isso
// à mão no roteiro seria uma segunda definição do menu, que diverge na primeira mudança.
static class Navegacao
{
    sealed record App(string Id, string Titulo, IModuloApp[] Modulos, string? Abertura, PerfilAcesso Perfil);

    public static void Gravar(IServiceProvider services, UsuarioSistema gerente, UsuarioSistema enfermeira, string output)
    {
        IModuloApp Rec() => new Clinica.Recepcao.Modulo.ModuloRecepcao();
        IModuloApp Cli() => new Clinica.Clinico.Modulo.ModuloClinico();
        App[] apps =
        [
            new("recepcao", "Recepção — Clínica SemDor", [Rec(), new ModuloContextual(Cli())], null, PerfilAcesso.Gerente),
            new("consultorio", "Consultório — Clínica SemDor", [Cli(), new ModuloContextual(Rec())], ChavesSuite.ConsultorioMeuDia, PerfilAcesso.Gerente),
            new("consultorio-enfermagem", "Consultório — Clínica SemDor", [Cli(), new ModuloContextual(Rec())], ChavesSuite.ConsultorioMeuDia, PerfilAcesso.Enfermagem),
            new("financeiro", "Financeiro — Clínica SemDor", [new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new ModuloContextual(Rec()), new ModuloContextual(Cli())], null, PerfilAcesso.Gerente),
            new("faturamento", "Faturamento", [new Clinica.Desktop.ModuloFaturamentoAplicativo()], null, PerfilAcesso.Gerente),
            new("gerente", "Gerente Geral — Clínica SemDor", [Rec(), Cli(), new Clinica.Financeiro.Modulo.ModuloFinanceiro(), new Clinica.Faturamento.Modulo.ModuloFaturamento(), new Clinica.Gerente.Modulo.ModuloGerente()], null, PerfilAcesso.Gerente),
        ];

        var sessao = services.GetRequiredService<SessaoUsuario>();
        var saida = new List<object>();
        foreach (var app in apps)
        {
            sessao.Entrar(app.Perfil == PerfilAcesso.Enfermagem ? enfermeira : gerente);
            try
            {
                var vm = new ShellViewModel(app.Titulo, app.Modulos, services, app.Abertura);
                var itens = vm.Itens.Select(i => new
                {
                    chave = i.Chave,
                    rotulo = i.Rotulo,
                    descricao = string.IsNullOrWhiteSpace(i.DescricaoMenu) ? null : i.DescricaoMenu,
                    oculto = i.Oculto,
                    abas = i.Abas.Where(a => vm.Itens.Any(x => x.Chave == a.Chave)).Select(a => new { rotulo = a.Rotulo, chave = a.Chave }).ToArray(),
                    componente = i.Abas.Count > 0 ? null : Componente(app.Modulos, i.Chave, services),
                }).ToArray();
                saida.Add(new
                {
                    app = app.Id,
                    nome = vm.NomeDoApp,
                    perfil = app.Perfil.ToString(),
                    barra = vm.Superiores.Select(g => new
                    {
                        rotulo = g.NomeNavegacao,
                        direto = g.Direto?.Chave,
                        itens = g.Itens.Select(i => i.Chave).ToArray(),
                    }).ToArray(),
                    telasTreinamento = vm.TelasDoTreinamento.OrderBy(k => k).ToArray(),
                    itens,
                });
            }
            catch (Exception e)
            {
                saida.Add(new { app = app.Id, erro = e.ToString() });
            }
        }
        File.WriteAllText(Path.Combine(output, "navegacao.json"),
            JsonSerializer.Serialize(saida, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>O componente Razor que a chave abre, pelo MESMO CriarTela do app.</summary>
    static string? Componente(IModuloApp[] modulos, string chave, IServiceProvider services)
    {
        foreach (var m in modulos.Where(m => m.Itens.Any(i => i.Chave == chave)))
        {
            try
            {
                if (m.CriarTela(chave, services) is ITelaDoNavegador t) return t.Componente.Name;
            }
            catch { /* tela que exige contexto (paciente em foco) — o nome vem do próximo módulo, ou fica nulo */ }
        }
        return chave == "treinamento" ? "Treinamento" : null;
    }
}
