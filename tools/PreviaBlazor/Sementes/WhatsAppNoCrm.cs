// Dados FICTÍCIOS da prévia — nenhuma pessoa real.
using Clinica.Application.Servicos;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A aba "WhatsApp" da Recepção (out/2026, docs/crm-na-recepcao.md): só o endereço do CRM, para a
/// tela abrir a moldura em vez do "ainda não foi ligado". O endereço é fictício — na prévia e no
/// teste a moldura fica em branco; o que se vê é a barra e o tamanho dela.
/// </summary>
public sealed class SementeWhatsAppNoCrm : ISemente
{
    public int Ordem => 85;

    public void Semear(ClinicaDbContext db)
    {
        if (db.Configuracoes.Find(ParametrosService.ChaveEnderecoDoCrm) is null)
            db.Configuracoes.Add(new ConfiguracaoGlobal { Chave = ParametrosService.ChaveEnderecoDoCrm, Valor = "https://crm.exemplo.invalid" });
        db.SaveChanges();
    }
}
