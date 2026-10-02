using System.Text.Json;
using System.Globalization;
using Clinica.Application.Abstracoes;
using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

public sealed class MedicamentoCatalogoService(IClinicaRepositorio repo)
{
    public static IReadOnlyList<MedicamentoCadastro> BaseOficial()
    {
        using var stream = typeof(MedicamentoCatalogoService).Assembly.GetManifestResourceStream("Clinica.Application.Recursos.Medicamentos.dcb-2026.json")!;
        return JsonSerializer.Deserialize<MedicamentoCadastro[]>(stream)!;
    }

    public async Task<IReadOnlyList<MedicamentoCadastro>> ListarAsync(CancellationToken ct = default)
    {
        var todos = BaseOficial().ToDictionary(m => m.Codigo);
        using var stream = typeof(MedicamentoCatalogoService).Assembly.GetManifestResourceStream("Clinica.Application.Recursos.Medicamentos.clinica-inicial.json")!;
        foreach (var m in JsonSerializer.Deserialize<MedicamentoCadastro[]>(stream)!) todos[m.Codigo]=m;
        foreach (var m in await repo.MedicamentosAsync(ct)) todos[m.Codigo] = m;
        return todos.Values.OrderBy(m => m.Nome).ThenBy(m => m.Apresentacao).ToArray();
    }

    public Task SalvarAsync(MedicamentoCadastro medicamento, int usuarioId, CancellationToken ct = default)
        => SalvarLoteAsync([medicamento], usuarioId, ct);

    public async Task SalvarLoteAsync(IReadOnlyList<MedicamentoCadastro> medicamentos, int usuarioId, CancellationToken ct = default)
    {
        var u = await repo.ObterUsuarioAsync(usuarioId, ct);
        if (u is null || !u.Ativo || !u.Pode(Permissao.Prescrever))
            throw new InvalidOperationException("Seu acesso não permite alterar o cadastro de medicamentos.");
        if (medicamentos.Count is 0 or > 5000) throw new InvalidOperationException("Importe de 1 a 5.000 medicamentos por vez.");
        var atuais = (await ListarAsync(ct)).ToDictionary(m => m.Codigo);
        var copias = medicamentos.Select(m => new MedicamentoCadastro { Codigo=m.Codigo, Nome=m.Nome.Trim(), PrincipioAtivo=m.PrincipioAtivo?.Trim(), Apresentacao=m.Apresentacao?.Trim(), Fabricante=m.Fabricante?.Trim(), Ativo=m.Ativo }).ToArray();
        foreach (var m in copias)
        {
            if (m.Nome.Length is 0 or > 200 || m.PrincipioAtivo?.Length > 200 || m.Apresentacao?.Length > 200 || m.Fabricante?.Length > 160)
                throw new InvalidOperationException("Informe nome (até 200 caracteres), princípio ativo, apresentação e fabricante dentro dos limites.");
            if (string.IsNullOrWhiteSpace(m.Codigo)) m.Codigo = "clinica:" + Guid.NewGuid().ToString("N");
            else if (!atuais.ContainsKey(m.Codigo)) throw new InvalidOperationException("Medicamento não encontrado. Atualize o cadastro.");
            m.Fonte = atuais.TryGetValue(m.Codigo, out var anterior) && anterior.Fonte.StartsWith("Anvisa") ? "Anvisa DCB / IN 462/2026 · revisado pela clínica" : "Cadastro da clínica";
            atuais[m.Codigo] = m;
        }
        var comparador = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true);
        if (atuais.Values.GroupBy(m => m.Nome + "|" + m.Apresentacao + "|" + m.Fabricante, comparador).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Já existe medicamento com o mesmo nome, apresentação e fabricante. Edite o cadastro existente.");
        foreach (var m in copias)
        {
            m.AtualizadoEm=DateTime.Now; m.AtualizadoPor=u.Login;
            await repo.SalvarMedicamentoAsync(m,ct);
        }
        await repo.RegistrarAuditoriaAsync(new EventoAuditoria { Acao="CatalogoMedicamentosAtualizado", Operador=u.Login, Detalhe=$"{copias.Length} cadastro(s) atualizado(s)." },ct);
        await repo.SalvarAsync(ct);
    }
}
