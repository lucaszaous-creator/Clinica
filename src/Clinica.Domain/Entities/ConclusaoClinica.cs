namespace Clinica.Domain.Entities;

/// <summary>A enfermagem registra a própria evolução; a conclusão pertence ao responsável clínico.</summary>
public static class ConclusaoClinica
{
    public static bool Permitida(PerfilAcesso perfil, Permissao permissoes, int? profissionalId)
        => (perfil == PerfilAcesso.Gerente || perfil == PerfilAcesso.Profissional && profissionalId is > 0)
           && (permissoes & (Permissao.EditarProntuario | Permissao.LancarAtendimento))
               == (Permissao.EditarProntuario | Permissao.LancarAtendimento);
}
