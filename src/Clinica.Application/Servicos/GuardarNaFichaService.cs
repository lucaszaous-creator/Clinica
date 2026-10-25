using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>
/// "GUARDAR NA FICHA" O ARQUIVO QUE O PACIENTE MANDOU (25/10, R9): a carteirinha, o pedido médico e o exame vão
/// do balão do WhatsApp direto aos ARQUIVOS DA FICHA — sem passar pela pasta Downloads do balcão, onde um dado
/// de saúde ficava solto e o médico não o via. As regras são as dos arquivos da ficha, e uma definição só:
/// <list type="bullet">
/// <item><b>A permissão é a das outras portas de anexar</b> (a aba Prontuário da ficha, a Enfermagem, o
/// Consultório e o tablet): <see cref="Permissoes"/>. É dado de SAÚDE (art. 5º, II); o perfil padrão da
/// Recepção não a tem, e quem decide liberar é a direção, em Acessos.</item>
/// <item><b>Só a ficha CONFERIDA</b> (a ligada à conversa): o telefone não é identidade (regra 4 da direção).</item>
/// <item><b>Só foto e PDF que o PACIENTE mandou</b> (<see cref="Cabe"/>, <see cref="ArquivoServe"/>). Figurinha
/// não é documento.</item>
/// <item>Título e data do documento obrigatórios, data nunca futura; o teto, a trilha no MESMO SaveChanges, o
/// operador de quem fez login e o "não se apaga, cancela-se com motivo" são do <see cref="AnexoPacienteService"/>.</item>
/// </list>
/// A nota interna "Guardado na ficha por …" é da tela, depois (a nota no CRM é de quem está com a conversa).
/// </summary>
public sealed class GuardarNaFichaService(AnexoPacienteService anexos, MidiaProntuarioService midia)
{
    /// <summary>Quem anexa arquivo à ficha em todas as portas: médico (EditarProntuario) ou enfermagem.</summary>
    public const Permissao Permissoes = Permissao.EditarProntuario | Permissao.RegistrarEvolucaoEnfermagem;

    /// <summary>A dica do botão apagado: por que esta pessoa não guarda (e com quem falar).</summary>
    public const string SemPermissao = "Guardar na ficha é anexar dado de saúde: pede o acesso de quem escreve no prontuário. Fale com a direção.";

    /// <summary>A dica do botão apagado quando a conversa ainda não tem a ficha conferida.</summary>
    public const string SemFicha = "Confira a ficha desta conversa antes: o arquivo vai para a ficha ligada a ela.";

    /// <summary>Os títulos sugeridos, na ordem do dia a dia do balcão. "Outro" pede o título escrito.</summary>
    public static readonly IReadOnlyList<string> Titulos = ["Carteirinha", "Pedido médico", "Exame", "Outro"];

    public const string Outro = "Outro";

    /// <summary>A mensagem pode ir para a ficha? Foto ou documento RECEBIDOS (a figurinha, não).</summary>
    public static bool Cabe(MidiaDaMensagem m)
        => m.Recebida && (m.Tipo == TipoDeMidia.Imagem && !m.Figurinha || m.Tipo == TipoDeMidia.Documento);

    /// <summary>O arquivo que veio do CRM é foto ou PDF? (O documento só se sabe depois de chegar: pode ser uma planilha.)</summary>
    public static bool ArquivoServe(ArquivoDaMidia arquivo)
        => arquivo.Mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
           || string.Equals(arquivo.Mime, "application/pdf", StringComparison.OrdinalIgnoreCase)
           || string.Equals(Path.GetExtension(arquivo.Nome ?? ""), ".pdf", StringComparison.OrdinalIgnoreCase);

    /// <summary>O título que vai para a ficha: o sugerido, ou o escrito quando é "Outro". Nulo = falta.</summary>
    public static string? Titulo(string? escolhido, string? escrito)
        => string.IsNullOrWhiteSpace(escolhido) ? null
            : escolhido == Outro ? (string.IsNullOrWhiteSpace(escrito) ? null : escrito.Trim())
            : escolhido.Trim();

    /// <summary>O que falta no formulário (a frase inline), ou nulo quando dá para guardar.</summary>
    public static string? Impedimento(string? titulo, DateOnly? data, DateOnly hoje)
        => titulo is null ? "Diga o que é este arquivo."
            : data is not { } d ? "Informe a data do documento (a que está nele)."
            : d > hoje ? "A data do documento não pode ser futura."
            : null;

    /// <summary>A nota interna que fica na conversa: quem guardou, o quê e de quando é o documento.</summary>
    public static string Nota(string quem, string titulo, DateOnly data)
        => $"Guardado na ficha por {quem}: {titulo} (documento de {data:dd/MM/yyyy}).";

    /// <summary>A confirmação passageira: guardado, e se a nota entrou na conversa (ela é de quem está com a conversa, regra do CRM).</summary>
    public static string Feito(string paciente, bool notaNaConversa)
        => notaNaConversa ? $"Guardado na ficha de {paciente}. A nota ficou na conversa."
            : $"Guardado na ficha de {paciente}. A nota não entrou na conversa: só quem está com ela anota.";

    /// <summary>
    /// Guarda o arquivo na ficha CONFERIDA da conversa. Confere de novo o que a tela confere (a permissão, a
    /// ficha, o tipo, o título e a data) — a tela não é a única barreira. Devolve a linha gravada.
    /// </summary>
    /// <param name="fichaDaConversa">O <c>PatientId</c> da conversa: a ficha que a recepção conferiu (nulo = recusa).</param>
    public async Task<AnexoPaciente> GuardarAsync(int? fichaDaConversa, MidiaDaMensagem mensagem, ArquivoDaMidia arquivo,
        string titulo, DateOnly? data, CancellationToken ct = default)
    {
        SessaoUsuario.Atual.ExigirAlgum(Permissoes, "guardar arquivo na ficha do paciente");
        if (fichaDaConversa is not { } paciente) throw new InvalidOperationException(SemFicha);
        if (!Cabe(mensagem) || !ArquivoServe(arquivo))
            throw new InvalidOperationException("Só foto ou PDF que o paciente mandou vão para a ficha por aqui. Baixe o arquivo e anexe pela ficha, se for o caso.");
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var t = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim();
        if (Impedimento(t, data, hoje) is { } falta) throw new InvalidOperationException(falta);
        return await anexos.AnexarAsync(paciente, data!.Value, t!, MidiaDaConversa.NomeParaSalvar(mensagem, arquivo), arquivo.Conteudo,
            tipoConteudo: arquivo.Mime, observacoes: "Recebido pelo WhatsApp da clínica.", operador: SessaoUsuario.Atual.Operador,
            midia: midia, ct: ct);
    }
}
