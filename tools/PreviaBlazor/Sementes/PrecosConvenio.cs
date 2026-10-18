// Dados FICTÍCIOS da prévia — nenhuma operadora contratada, nenhum valor de tabela real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// A Tabela de preço por convênio (etapa 6 do Gerente, out/2026). A semente de Recebimentos já
/// grava três preços sem prazo; esta acrescenta o que a tela existe para mostrar:
/// - um REAJUSTE (o preço antigo encerrado e o novo valendo), que é o histórico que o filtro
///   "só o que vale hoje" esconde;
/// - um reajuste FUTURO ("ainda não vale") e um preço desligado ("inativo");
/// - um preço com ESPECIALIDADE, que vence o genérico do tipo;
/// - uma operadora DESATIVADA no catálogo com preço cadastrado — o caso que o formulário WPF
///   abria com o convênio em branco.
/// Sem Id fixo: o convênio da prévia é achado pelo código.
/// </summary>
public sealed class SementePrecosConvenio : ISemente
{
    public int Ordem => 97;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var inicioDoAno = new DateOnly(hoje.Year, 1, 1);

        if (!db.Set<ConvenioCadastro>().Any(c => c.Codigo == "VidaPreviaAntiga"))
            db.Add(new ConvenioCadastro
            {
                Codigo = "VidaPreviaAntiga", Nome = "Vida Plena Saúde (exemplo)", Familia = Convenio.Personalizado, Ativo = false
            });

        // O reajuste: o valor antigo valeu até ontem do mês passado, o novo vale desde o dia 1.
        var corte = new DateOnly(hoje.Year, hoje.Month, 1);
        db.Add(new PrecoConvenio
        {
            ConvenioCodigo = "SulAmericaPrevia", Tipo = TipoCodigo.Acupuntura, Valor = 120m,
            VigenteDe = inicioDoAno.AddYears(-1), VigenteAte = corte.AddDays(-1), CriadoPor = "Prévia"
        });
        db.Add(new PrecoConvenio
        {
            ConvenioCodigo = "SulAmericaPrevia", Tipo = TipoCodigo.Acupuntura, Valor = 132.4m,
            VigenteDe = corte, CriadoPor = "Prévia"
        });
        // O próximo reajuste, já negociado e ainda não valendo.
        db.Add(new PrecoConvenio
        {
            ConvenioCodigo = "UnimedPadrao", Tipo = TipoCodigo.Acupuntura, Valor = 152m,
            VigenteDe = corte.AddMonths(2), CriadoPor = "Prévia"
        });
        // A especialidade vence o genérico da consulta.
        db.Add(new PrecoConvenio
        {
            ConvenioCodigo = "Amil", Tipo = TipoCodigo.ConsultaEspecialidade, Especialidade = Especialidade.ClinicaDaDor,
            Valor = 285m, VigenteDe = inicioDoAno, VigenteAte = new DateOnly(hoje.Year, 12, 31), CriadoPor = "Prévia",
            Observacoes = "Negociado na renovação anual."
        });
        // Desligado: fica no histórico, a conciliação não o propõe.
        db.Add(new PrecoConvenio
        {
            ConvenioCodigo = "Amil", Tipo = TipoCodigo.Bsv, Valor = 98m, Ativo = false, CriadoPor = "Prévia"
        });
        // A operadora que a clínica desligou depois de cadastrar o preço.
        db.Add(new PrecoConvenio
        {
            ConvenioCodigo = "VidaPreviaAntiga", Tipo = TipoCodigo.Eletroacupuntura, Valor = 58.9m,
            VigenteDe = inicioDoAno.AddYears(-1), CriadoPor = "Prévia"
        });
        db.SaveChanges();
        // O NOME da operadora na tela vem do catálogo em memória: sem recarregá-lo, a desativada
        // apareceria com o nome padrão da família.
        new ConvenioCatalogoService(new ClinicaRepositorio(db)).RecarregarCacheAsync().GetAwaiter().GetResult();
    }
}
