using Clinica.Domain.Entities;

namespace Clinica.Clinico.Modulo;

/// <summary>
/// DE QUEM É A LISTA que as telas do posto mostram, e para onde o <b>Atender</b> leva
/// (parcela 88).
///
/// O defeito que ela corrige
/// -------------------------
/// As cinco telas de lista do Consultório — o dia, a semana, a carteira, a dívida de
/// prontuário e os números — filtram pelo <c>Profissional</c> vinculado ao login. Faz
/// sentido para quem CONSULTA: os horários são dele.
///
/// ⚠️ Para a ENFERMAGEM não faz, e o resultado era silencioso e caro. Os horários
/// pertencem a quem consulta; a técnica passa por todos eles — a clínica disse a frase
/// que decidiu a parcela 71: <i>"todo paciente precisa passar pela enfermagem"</i>. E ela
/// PRECISA de um <c>Profissional</c> vinculado, porque é de lá que sai o COREN copiado em
/// cada registro (parcela 72). Ou seja: cadastrá-la CERTO fazia as cinco telas abrirem
/// VAZIAS, e tela vazia se lê como sistema quebrado — não como "esta lista não é sua".
/// Foi o que a clínica descreveu: <i>"os enfermeiros podem ver todos os pacientes… em vez
/// de ver só os pacientes dele"</i>.
///
/// Por que UM lugar
/// ----------------
/// Porque a resposta tem de ser a MESMA nas cinco telas. Repetida em cada ViewModel, ela
/// divergiria na primeira correção — e a que ficasse para trás voltaria a devolver a lista
/// vazia, sem erro nenhum, exatamente na tela que ninguém releu (a lição das parcelas 64
/// e 68).
///
/// ⚠️ E por que a DECISÃO não mora aqui
/// ------------------------------------
/// Esta classe é um ADAPTADOR: ela só lê <c>SessaoUsuario.Atual</c> e repassa. Quem
/// decide é <see cref="PerfisAcesso"/>, no DOMÍNIO — este projeto é
/// <c>net8.0-windows</c> e não compila no projeto de teste, e regra que o
/// <c>dotnet test</c> não alcança apodrece sem ninguém notar. É a mesma razão que pôs a
/// grade da semana na Application (parcela 69) e o aviso do registro de execução dentro
/// do domínio do PDF (parcela 68).
/// </summary>
public static class PostoClinico
{
    /// <summary>
    /// O profissional cuja agenda estas telas mostram. <c>null</c> quer dizer <b>a clínica
    /// inteira</b>.
    /// </summary>
    public static int? ProfissionalDaLista()
        => PerfisAcesso.ProfissionalDaListaDoPosto(
            SessaoUsuario.Atual.Efetivas, SessaoUsuario.Atual.ProfissionalId);

    /// <summary>
    /// A lista mostrada é a da clínica, e não a de uma pessoa.
    ///
    /// Ela vale como condição de tela: é por isso que o "Chamar próximo" do quadro fica
    /// desligado neste modo — o primeiro da fila pode ser paciente de outro profissional,
    /// e anunciar um nome pela sala errada é pior do que não anunciar.
    /// </summary>
    public static bool ListaDaClinica() => ProfissionalDaLista() is null;

    /// <summary>POR QUE a lista é a da clínica — <c>null</c> quando ela não é.</summary>
    public static string? MotivoDaListaAmpla()
        => PerfisAcesso.MotivoDaListaDoPosto(
            SessaoUsuario.Atual.Efetivas, SessaoUsuario.Atual.ProfissionalId);

    /// <summary>
    /// Para onde o <b>Atender</b> leva — a seção de escrita de QUEM está clicando.
    ///
    /// ⚠️ É a metade que dá sentido ao pedido da clínica. "Atender" é uma palavra só, e
    /// precisa significar a coisa certa para cada profissional: quem consulta cai na
    /// sessão em S-O-A-P; quem executa cai na passagem de enfermagem, nas cinco etapas da
    /// COFEN. Mandar a técnica para o formulário do médico — que ela não pode gravar —
    /// seria o "botão que não faz nada" da parcela 41 com uma tela inteira em volta.
    ///
    /// As duas caem na MESMA tela do paciente (o mesmo crachá, o mesmo rail, as mesmas
    /// seções de leitura): o que muda é a seção que abre.
    /// </summary>
    public static string ChaveDoAtendimento()
        => PerfisAcesso.EscreveComoEnfermagem(SessaoUsuario.Atual.Efetivas)
            ? ModuloClinico.ChaveAtendimentoEnfermagem
            : ModuloClinico.ChaveAtendimento;
}
