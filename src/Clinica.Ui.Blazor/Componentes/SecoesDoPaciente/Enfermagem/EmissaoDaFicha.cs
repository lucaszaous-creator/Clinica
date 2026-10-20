using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Ui.Blazor.Hospedagem;
using Microsoft.Extensions.DependencyInjection;

namespace Clinica.Ui.Blazor.Componentes.SecoesDoPaciente.Enfermagem;

/// <summary>
/// A FICHA DO ATENDIMENTO pelas telas Blazor da enfermagem (out/2026). Quem EMITE é o emissor
/// compartilhado da Application (<see cref="FichaDoAtendimentoNaTela"/>: documento numerado, PDF
/// e a trilha <c>ExportacaoClinica</c>) — o mesmo da tela do médico; aqui ficam só as duas
/// metades que são de quem chama: a segunda barreira (<c>VerProntuario</c>) e o "salve antes".
/// O arquivo vai ao HOSPEDEIRO (<see cref="IHostDaTela.SalvarEAbrirAsync"/>).
///
/// ⚠️ NUNCA lança: toda falha vira frase — a recusa de permissão e a de rascunho precisam
/// APARECER para a pessoa saber o que fazer.
/// </summary>
public static class EmissaoDaFicha
{
    public const string SemPaciente = "Escolha um paciente para emitir a ficha do atendimento.";

    public static async Task<(string Frase, bool Erro)> EmitirAsync(
        IServiceScopeFactory escopos, IHostDaTela host, int pacienteId, DateOnly dia,
        bool temRascunhoNaoGravado, string contextoDoLog)
    {
        if (pacienteId == 0) return (SemPaciente, true);
        // Emitir é FATO numerado: imprimir o que ainda não está no prontuário entregaria ao
        // paciente uma versão que o prontuário não tem.
        if (temRascunhoNaoGravado) return (FichaDoAtendimentoNaTela.SalveAntes, true);

        try
        {
            // A ficha IMPRIME o prontuário, não o escreve: o bit é o de LER.
            SessaoUsuario.Atual.Exigir(Permissao.VerProntuario, "imprimir a ficha do atendimento");

            string numero;
            byte[] pdf;
            using (var escopo = escopos.CreateScope())
                (numero, pdf) = await FichaDoAtendimentoNaTela.EmitirAsync(escopo.ServiceProvider, pacienteId,
                    SessaoUsuario.Atual.ProfissionalId, dia, SessaoUsuario.Atual.Operador);

            var erro = await host.SalvarEAbrirAsync(pdf, FichaDoAtendimentoNaTela.NomeDoArquivo(numero), "PDF (*.pdf)|*.pdf", ".pdf");
            return (FichaDoAtendimentoNaTela.Frase(numero, erro), erro is not null);
        }
        catch (Exception ex)
        {
            Clinica.Application.Diagnostico.Registrar($"{contextoDoLog} — ficha do atendimento não pôde ser emitida", ex);
            return (ex.Message, true);
        }
    }
}
