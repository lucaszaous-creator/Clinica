using Clinica.Domain;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Regras do formulário de cadastro, compartilhadas por todos os módulos.</summary>
public static class ValidacaoCadastroPaciente
{
    public static string? ErroCpf(string? documento)
        => string.IsNullOrWhiteSpace(documento)
            ? "Informe o CPF do paciente."
            : !Cpf.Valido(documento)
                ? "CPF inválido. Confira os 11 números do documento."
                : null;

    public static string? ErroEndereco(string? endereco)
        => string.IsNullOrWhiteSpace(endereco)
            ? "Informe o endereço residencial do paciente."
            : endereco.Trim().Length > 300
                ? "O endereço deve ter até 300 caracteres."
                : null;

    public static void Exigir(Paciente paciente)
    {
        if (string.IsNullOrWhiteSpace(paciente.Nome))
            throw new ArgumentException("Informe o nome do paciente.");
        if (ErroCpf(paciente.Documento) is { } cpf)
            throw new ArgumentException(cpf);
        if (ErroEndereco(paciente.Endereco) is { } endereco)
            throw new ArgumentException(endereco);
    }
}
