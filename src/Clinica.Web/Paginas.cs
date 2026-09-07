using System.Net;
using System.Security.Claims;
using System.Text;
using Clinica.Application.Modelos;
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Web;

/// <summary>
/// As páginas, escritas à mão em HTML.
///
/// Por que não há framework de tela aqui
/// -------------------------------------
/// São TRÊS páginas de leitura. Um SPA (ou Blazor, ou Razor com camada de componentes)
/// custaria build de front, dependências que se atualizam sozinhas e uma segunda cópia do
/// design system para manter — e o que se ganharia é interatividade que uma tela de
/// leitura não tem. É a mesma decisão do gráfico desenhado com os tokens em vez de
/// biblioteca (parcela 5): dependência nova de UI é risco desproporcional ao que ela
/// resolve.
///
/// ⚠️ <b>TODO texto que vem do banco passa por <see cref="T"/>.</b> Nome de paciente,
/// observação de horário e queixa são texto que uma PESSOA digitou: interpolá-los crus no
/// HTML é injeção de script na tela de quem lê o prontuário. A CSP do
/// <c>Program</c> é a segunda tranca, nunca a primeira.
/// </summary>
public static class Paginas
{
    /// <summary>Escapa. O nome curto é de propósito: ele aparece em toda interpolação.</summary>
    private static string T(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);

    public static IResult Html(string corpo) => Results.Content(corpo, "text/html; charset=utf-8");

    private const string Estilo = """
        <style>
          :root { color-scheme: light; }
          * { box-sizing: border-box; }
          body { margin:0; font:15px/1.5 "Segoe UI",system-ui,sans-serif;
                 background:#f5f6f8; color:#1f2430; }
          header { background:#fff; border-bottom:1px solid #e3e6ec; padding:12px 20px;
                   display:flex; gap:18px; align-items:center; flex-wrap:wrap; }
          header .marca { font-weight:600; }
          header nav a { color:#2b5cd9; text-decoration:none; margin-right:14px; }
          header nav a.atual { font-weight:600; color:#1f2430; }
          header form { margin-left:auto; }
          main { padding:20px; max-width:1100px; margin:0 auto; }
          h1 { font-size:22px; margin:0 0 4px; }
          .suave { color:#5b6273; font-size:13px; }
          table { width:100%; border-collapse:collapse; background:#fff;
                  border:1px solid #e3e6ec; border-radius:8px; overflow:hidden; }
          th, td { text-align:left; padding:9px 12px; border-bottom:1px solid #eef0f4;
                   vertical-align:top; }
          th { background:#fafbfc; font-size:12px; text-transform:uppercase;
               letter-spacing:.03em; color:#5b6273; }
          tr:last-child td { border-bottom:none; }
          .cartoes { display:flex; flex-wrap:wrap; gap:12px; margin:0 0 18px; }
          .cartao { background:#fff; border:1px solid #e3e6ec; border-radius:8px;
                    padding:12px 16px; min-width:180px; }
          .cartao .rotulo { font-size:12px; color:#5b6273; }
          .cartao .valor { font-size:24px; font-weight:600; }
          .alerta { background:#fff8e6; border:1px solid #f0d9a0; border-radius:8px;
                    padding:10px 14px; margin:0 0 10px; }
          .alerta.grave { background:#fdecec; border-color:#f0b5b5; }
          .aviso { background:#fff; border:1px solid #e3e6ec; border-radius:8px;
                   padding:14px 16px; }
          .apagado { color:#9aa1b1; }
          form.busca { display:flex; gap:8px; margin:0 0 16px; }
          input[type=text], input[type=password], input[type=date] {
            padding:9px 11px; border:1px solid #cfd4de; border-radius:6px; font:inherit;
            min-width:220px; }
          button { padding:9px 16px; border:none; border-radius:6px; background:#2b5cd9;
                   color:#fff; font:inherit; cursor:pointer; }
          button.leve { background:#eef1f6; color:#1f2430; }
          .login { max-width:360px; margin:12vh auto; background:#fff; padding:26px;
                   border:1px solid #e3e6ec; border-radius:10px; }
          .login input { width:100%; margin-bottom:10px; }
          .login button { width:100%; }
          .erro { background:#fdecec; border:1px solid #f0b5b5; border-radius:6px;
                  padding:9px 12px; margin-bottom:12px; font-size:14px; }
          .sessao { background:#fff; border:1px solid #e3e6ec; border-radius:8px;
                    padding:12px 16px; margin-bottom:10px; }
          .sessao h3 { margin:0 0 6px; font-size:15px; }
          .sessao p { margin:2px 0; }
        </style>
        """;

    /// <summary>O esqueleto: cabeçalho com o menu do que ESTE usuário alcança, e o miolo.</summary>
    public static string Corpo(
        ClaimsPrincipal? usuario, string titulo, string miolo,
        string? rotaAtual = null, string? tokenCsrf = null)
    {
        var menu = new StringBuilder();
        foreach (var p in AcessoWeb.Do(Sessao.Permissoes(usuario)))
            menu.Append($"""<a href="{p.Rota}" class="{(p.Rota == rotaAtual ? "atual" : "")}">{T(p.Titulo)}</a>""");

        var nome = Sessao.Nome(usuario);
        var barra = string.IsNullOrEmpty(nome)
            ? string.Empty
            : $"""
               <header>
                 <span class="marca">Clínica · leitura</span>
                 <nav>{menu}</nav>
                 <form method="post" action="/sair">
                   <input type="hidden" name="__RequestVerificationToken" value="{T(tokenCsrf)}">
                   <span class="suave">{T(nome)}</span>
                   <button class="leve" type="submit">Sair</button>
                 </form>
               </header>
               """;

        return $"""
            <!doctype html>
            <html lang="pt-BR">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{T(titulo)} · Clínica</title>
              {Estilo}
            </head>
            <body>
              {barra}
              <main>{miolo}</main>
            </body>
            </html>
            """;
    }

    // ==================== Entrar ====================

    public static string Login(string? erro, string tokenCsrf) => Corpo(null, "Entrar", $"""
        <div class="login">
          <h1>Entrar</h1>
          <p class="suave">Somente leitura: a web mostra o dia, o mês e a ficha. Lançar,
             marcar e escrever continuam nos aplicativos da clínica.</p>
          {(string.IsNullOrEmpty(erro) ? "" : $"""<div class="erro">{T(erro)}</div>""")}
          <form method="post" action="/entrar">
            <input type="hidden" name="__RequestVerificationToken" value="{T(tokenCsrf)}">
            <input type="text" name="login" placeholder="Usuário" autocomplete="username" required>
            <input type="password" name="senha" placeholder="Senha" autocomplete="current-password" required>
            <button type="submit">Entrar</button>
          </form>
        </div>
        """);

    // ==================== O dia ====================

    public static string Dia(
        ClaimsPrincipal usuario, string tokenCsrf, DateOnly dia, IReadOnlyList<Agendamento> horarios)
    {
        var linhas = new StringBuilder();
        foreach (var a in horarios.OrderBy(a => a.DataHora))
        {
            // Cancelado e falta ficam na lista, APAGADOS — a regra da folha do dia: quem lê
            // às 14h precisa saber que as 15h vagaram, e linha ausente se confunde com
            // horário que nunca existiu.
            var fora = !a.OcupaAgenda;
            var situacao = StatusDaFila.Palavra(a.Status, a.Etapa);
            var agora = DateTime.Now;
            var detalhe = StatusDaFila.Detalhe(
                a.Status, a.Etapa, a.ChegadaEm, a.EsperaMinutos(agora), a.ChamadoHaMinutos(agora),
                a.InicioAtendimentoEm, a.FimAtendimentoEm);

            linhas.Append($"""
                <tr class="{(fora ? "apagado" : "")}">
                  <td>{a.DataHora:HH\:mm}</td>
                  <td>{T(a.Paciente?.Nome ?? "(paciente removido)")}</td>
                  <td>{T(CatalogoModalidades.Nome(a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString()))}</td>
                  <td>{T(a.Profissional?.Rotulo ?? "sem profissional")}</td>
                  <td>{T(situacao)}<br><span class="suave">{T(detalhe)}</span></td>
                </tr>
                """);
        }

        var ocupando = horarios.Count(a => a.OcupaAgenda);
        var miolo = $"""
            <h1>O dia</h1>
            <p class="suave">{ocupando} horário(s) em {dia:dd/MM/yyyy}. Cancelados e faltas
               ficam na lista, apagados — some da lista é o que faz alguém achar que o
               horário nunca existiu.</p>
            <form class="busca" method="get" action="/dia">
              <input type="date" name="data" value="{dia:yyyy-MM-dd}">
              <button type="submit">Ver</button>
            </form>
            {(horarios.Count == 0
                ? """<p class="aviso">Nenhum horário marcado neste dia.</p>"""
                : $"""
                  <table>
                    <tr><th>Hora</th><th>Paciente</th><th>Modalidade</th><th>Profissional</th><th>Situação</th></tr>
                    {linhas}
                  </table>
                  """)}
            """;

        return Corpo(usuario, "O dia", miolo, "/dia", tokenCsrf);
    }

    // ==================== O mês ====================

    public static string Painel(ClaimsPrincipal usuario, string tokenCsrf, PainelDirecao p)
    {
        var alertas = new StringBuilder();
        foreach (var a in p.Alertas)
            alertas.Append($"""
                <div class="alerta {(a.Gravidade == GravidadeDirecao.Perigo ? "grave" : "")}">
                  <strong>{T(a.Titulo)}</strong><br><span class="suave">{T(a.Detalhe)}</span>
                </div>
                """);

        // ⚠️ Cada bloco falha sozinho no serviço (`NaoVerificados`), e a web PRECISA dizer
        // isso: um painel que mostra "nada vencido" por causa de uma consulta quebrada é
        // pior do que um painel que não abre (parcela 22).
        var naoVerificados = p.NaoVerificados.Count == 0
            ? string.Empty
            : $"""
               <div class="alerta grave">
                 Alguns números não puderam ser lidos e NÃO estão abaixo:
                 {T(string.Join(", ", p.NaoVerificados))}.
               </div>
               """;

        var miolo = $"""
            <h1>O mês</h1>
            <p class="suave">Os números de {p.Hoje:MM/yyyy}, como o painel da direção os
               mostra. Somente leitura.</p>
            {naoVerificados}
            <div class="cartoes">
              <div class="cartao"><div class="rotulo">Entradas do mês</div>
                <div class="valor">{p.EntradasMes:C}</div></div>
              <div class="cartao"><div class="rotulo">Saídas do mês</div>
                <div class="valor">{p.SaidasMes:C}</div></div>
              <div class="cartao"><div class="rotulo">Pacientes devendo</div>
                <div class="valor">{p.PacientesDevendo}</div>
                <div class="suave">{p.TotalDevidoPorPacientes:C}</div></div>
              <div class="cartao"><div class="rotulo">Guias sem receita</div>
                <div class="valor">{p.GuiasSemReceita}</div></div>
              <div class="cartao"><div class="rotulo">Sessões sem evolução</div>
                <div class="valor">{p.SessoesSemEvolucao}</div>
                <div class="suave">{p.SessoesSemEvolucaoComGuia} já com guia</div></div>
            </div>
            {alertas}
            """;

        return Corpo(usuario, "O mês", miolo, "/painel", tokenCsrf);
    }

    // ==================== Pacientes ====================

    public static string Pacientes(
        ClaimsPrincipal usuario, string tokenCsrf, string? termo, IReadOnlyList<Paciente> achados)
    {
        var linhas = new StringBuilder();
        foreach (var p in achados)
            linhas.Append($"""
                <tr>
                  <td><a href="/paciente/{p.Id}">{T(p.Nome)}</a></td>
                  <td>{T(p.DocumentoFormatado)}</td>
                  <td>{T(p.TelefoneFormatado)}</td>
                  <td>{T(p.ConvenioNome)}</td>
                </tr>
                """);

        // Três estados, e a diferença entre eles importa: quem não pediu nada não recebe
        // "nenhum paciente encontrado" — seria uma afirmação falsa sobre um cadastro de
        // 2.238 fichas, e é ela que leva alguém a cadastrar de novo quem já tem ficha.
        var resultado = string.IsNullOrWhiteSpace(termo)
            ? """<p class="aviso">Digite o nome ou o CPF para buscar.</p>"""
            : achados.Count == 0
                ? """<p class="aviso">Nenhum paciente encontrado com este termo.</p>"""
                : $"""
                  <table>
                    <tr><th>Nome</th><th>Documento</th><th>Telefone</th><th>Convênio</th></tr>
                    {linhas}
                  </table>
                  """;

        var miolo = $"""
            <h1>Pacientes</h1>
            <form class="busca" method="get" action="/pacientes">
              <input type="text" name="q" value="{T(termo)}" placeholder="Nome ou CPF" autofocus>
              <button type="submit">Buscar</button>
            </form>
            {resultado}
            """;

        return Corpo(usuario, "Pacientes", miolo, "/pacientes", tokenCsrf);
    }

    // ==================== A ficha ====================

    public static string Ficha(
        ClaimsPrincipal usuario, string tokenCsrf, Paciente paciente,
        IReadOnlyList<Agendamento> futuros, IReadOnlyList<Evolucao> sessoes)
    {
        var proximos = new StringBuilder();
        foreach (var a in futuros)
            proximos.Append($"""
                <tr>
                  <td>{a.DataHora:dd/MM/yyyy HH\:mm}</td>
                  <td>{T(CatalogoModalidades.Nome(a.ModalidadeCodigo ?? a.ModalidadePrevista.ToString()))}</td>
                  <td>{T(a.Profissional?.Rotulo ?? "sem profissional")}</td>
                </tr>
                """);

        // O PRONTUÁRIO só aparece com o bit dele — o corte da parcela 49 não afrouxa por a
        // tela ser web. E quem não o alcança não vê nem que existem sessões: anunciar já
        // seria contar que há prontuário daquele paciente (a regra da parcela 59).
        var prontuario = AcessoWeb.MostraProntuario(Sessao.Permissoes(usuario))
            ? Sessoes(sessoes)
            : string.Empty;

        var miolo = $"""
            <h1>{T(paciente.Nome)}</h1>
            <p class="suave">{T(paciente.ConvenioNome)} ·
               {T(paciente.DocumentoFormatado)} · {T(paciente.TelefoneFormatado)}</p>

            <h2>Próximas sessões</h2>
            {(futuros.Count == 0
                ? """<p class="aviso">Nenhuma sessão marcada daqui para a frente.</p>"""
                : $"""<table><tr><th>Quando</th><th>Modalidade</th><th>Profissional</th></tr>{proximos}</table>""")}

            {prontuario}
            """;

        return Corpo(usuario, paciente.Nome, miolo, "/pacientes", tokenCsrf);
    }

    /// <summary>As sessões escritas, da mais recente para a mais antiga.</summary>
    private static string Sessoes(IReadOnlyList<Evolucao> sessoes)
    {
        if (sessoes.Count == 0)
            return """<h2>Prontuário</h2><p class="aviso">Nenhuma sessão registrada.</p>""";

        var blocos = new StringBuilder();
        foreach (var e in sessoes.Take(20))
        {
            var linhas = new StringBuilder();
            Linha(linhas, "Queixa", e.QueixaPrincipal);
            Linha(linhas, "Conduta", e.Conduta);
            Linha(linhas, "Evolução", e.TextoEvolucao);
            Linha(linhas, "Orientações", e.Orientacoes);
            Linha(linhas, "Outros registros", CampoPersonalizadoService.Resumir(e.CamposPersonalizados));

            var eva = e.TemParEva ? $" · EVA {e.EvaAntes}→{e.EvaDepois}" : string.Empty;
            var cancelada = e.Cancelada ? " · CANCELADA" : string.Empty;

            blocos.Append($"""
                <div class="sessao {(e.Cancelada ? "apagado" : "")}">
                  <h3>{e.Data:dd/MM/yyyy}{T(eva)}{T(cancelada)}</h3>
                  <p class="suave">{T(e.Profissional?.Rotulo)}</p>
                  {linhas}
                </div>
                """);
        }

        return $"""
            <h2>Prontuário</h2>
            <p class="suave">As {Math.Min(sessoes.Count, 20)} sessões mais recentes de
               {sessoes.Count}. Este acesso fica registrado na trilha.</p>
            {blocos}
            """;
    }

    private static void Linha(StringBuilder destino, string rotulo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return;
        destino.Append($"""<p><strong>{T(rotulo)}:</strong> {T(valor)}</p>""");
    }
}
