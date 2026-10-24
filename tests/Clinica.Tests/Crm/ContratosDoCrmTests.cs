using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// O contrato da API do CRM é o MESMO arquivo nos dois repositórios: aqui
/// <c>src/Clinica.Application/Modelos/Crm/ContratosDoCrm.cs</c>, no semdor-crm
/// <c>src/Clinica.Crm/ApiParaOsApps.cs</c>. Os dois têm este teste, com a MESMA constante.
///
/// ⚠️ Quando este teste falhar: alguém mudou o contrato deste lado. NÃO troque a constante para
/// "passar". Faça a MESMA mudança no ApiParaOsApps.cs do CRM (campo novo no FIM do record, com
/// valor padrão; nunca renomear nem remover), troque a constante nos dois testes pelo hash novo e
/// suba os dois PRs juntos. Constante trocada só de um lado é o app desserializando, em produção,
/// uma resposta que o CRM já não manda.
/// </summary>
public sealed class ContratosDoCrmTests
{
    const string Sha256DoContrato = "ff97010885fcd28d8c9de2a681a8025bf839eb6c6a6cda8edea6f335ad3504c7";

    [Fact] public void O_contrato_e_identico_ao_do_CRM()
    {
        var caminho = Path.Combine(Raiz(), "src", "Clinica.Application", "Modelos", "Crm", "ContratosDoCrm.cs");
        // O checkout do Windows pode trazer \r\n: o hash é do texto, não do fim de linha.
        var texto = File.ReadAllText(caminho, new UTF8Encoding(false)).Replace("\r\n", "\n");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();

        Assert.Equal(Sha256DoContrato, hash);
    }

    static string Raiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Clinica.sln"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("Clinica.sln não encontrado.");
    }
}
