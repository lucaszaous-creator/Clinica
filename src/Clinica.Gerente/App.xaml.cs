using Clinica.Application.Servicos;
using System.Windows;
using Clinica.Desktop.Shell;
using Clinica.Desktop.Shell.Configuracao;
using Clinica.Desktop.Shell.Modulos;
using Clinica.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModuloClinico = Clinica.Clinico.Modulo.ModuloClinico;
using ModuloFinanceiro = Clinica.Financeiro.Modulo.ModuloFinanceiro;
using ModuloGerente = Clinica.Gerente.Modulo.ModuloGerente;
using ModuloRecepcao = Clinica.Recepcao.Modulo.ModuloRecepcao;

namespace Clinica.Gerente;

/// <summary>
/// Executável do Gerente Geral: a mesma casca da Recepção e do Financeiro, com a
/// lista de módulos completa. É o que a arquitetura de casca fina prometia — o app
/// que "engloba tudo" não tem nenhuma tela própria nem nenhuma cópia.
///
/// A ordem da lista é a ordem dos grupos na sidebar.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    private readonly IReadOnlyList<IModuloApp> _modulos =
    [
        new ModuloRecepcao(),
        // O consultório vem depois da recepção porque a sidebar é lida na ordem do dia:
        // o paciente chega ao balcão antes de entrar na sala. Aqui ele NÃO é a abertura —
        // o item inicial do Gerente continua sendo o painel da direção, que é o único
        // marcado com `Inicial` neste executável.
        new ModuloClinico(),
        new ModuloFinanceiro(),
        // Último de propósito: as telas da direção são as menos usadas no dia a dia, e
        // a sidebar é lida de cima para baixo na ordem do trabalho.
        new ModuloGerente()
        // A Fase 4 (faturamento como módulo) foi CANCELADA: o Gerente lê o faturamento
        // pela tela própria do ModuloGerente, sem encostar no app em produção.
    ];

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = await SuiteApp.IniciarAsync(this, "Gerente Geral", "Gerente Geral — Clínica SemDor", _modulos);

        if (_host is not null)
        {
            await CopiaDeSegurancaAsync(_host);
            await ExpirarLinksVencidosAsync(_host);
        }
    }

    /// <summary>
    /// Tira do ar os documentos cujo prazo de publicação venceu (parcela 53).
    ///
    /// <b>Aqui pela mesma razão do backup</b>: o sistema é desktop, não tem serviço
    /// residente, e inventar um agendador daria mais uma peça para quebrar em silêncio. O
    /// Gerente é a máquina que abre todo dia na direção.
    ///
    /// <b>Depois do backup, e nunca antes.</b> Guardar a base vem primeiro; tirar arquivo
    /// do ar é a operação que se pode repetir amanhã sem prejuízo.
    ///
    /// Sem publicação configurada isto não faz nada: a consulta não acha documento
    /// publicado nenhum e a varredura termina em zero.
    ///
    /// Falha não derruba a abertura — o link continuar no ar um dia a mais é muito melhor
    /// do que a direção não conseguir abrir o sistema.
    /// </summary>
    private static async Task ExpirarLinksVencidosAsync(IHost host)
    {
        try
        {
            using var escopo = host.Services.CreateScope();
            await escopo.ServiceProvider
                .GetRequiredService<PublicacaoDocumentoService>()
                .ExpirarVencidosAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Gerente — expiração de links publicados", ex);
        }
    }

    /// <summary>
    /// Grava a cópia de segurança se estiver vencida (parcela 52).
    ///
    /// <b>Mora AQUI, e não no <c>SuiteApp</c>, porque a casca é compartilhada</b> pelos
    /// quatro executáveis: rodar o backup na abertura da Recepção faria a máquina do
    /// balcão despejar a base inteira numa pasta de rede no começo do expediente, quatro
    /// vezes por dia, uma por posto. O Gerente é a máquina da direção — é dele a decisão
    /// e é dele o gancho.
    ///
    /// <b>Depois da janela abrir, e sem esperar.</b> A cópia lê a base inteira e leva
    /// tempo; prender a abertura do app a ela faria a direção olhar para uma tela cinza
    /// sem saber por quê. Falha não derruba nada — o serviço registra no log e a tela de
    /// Configurações mostra a situação.
    ///
    /// Sem pasta escolhida em Configurações, isto não faz nada e não reclama: a política
    /// desligada é um estado legítimo, e um aviso a cada abertura ensinaria a fechá-lo
    /// sem ler.
    /// </summary>
    private static async Task CopiaDeSegurancaAsync(IHost host)
    {
        try
        {
            using var escopo = host.Services.CreateScope();
            await escopo.ServiceProvider
                .GetRequiredService<PoliticaBackupService>()
                .ExecutarSeVencidoAsync();
        }
        catch (Exception ex)
        {
            LogSuite.Registrar("Gerente — cópia de segurança automática", ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
