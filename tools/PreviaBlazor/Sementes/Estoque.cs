// Dados FICTÍCIOS da prévia — nenhuma pessoa, lote ou fornecedor real.
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// O estoque de uma clínica de acupuntura: agulhas por medida, algodão, álcool, luvas,
/// eletrodos, moxa. Tem de tudo o que a tela mostra — um item abaixo do mínimo, um lote
/// VENCIDO e um vencendo, saídas ligadas a sessões (o custo por sessão), uma perda com o
/// motivo escrito, um acerto de inventário e um item inativo.
/// </summary>
public sealed class SementeEstoque : ISemente
{
    public int Ordem => 50;

    public void Semear(ClinicaDbContext db)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var pacientes = db.Pacientes.OrderBy(p => p.Id).Take(4).ToList();

        ItemEstoque Item(string codigo, string nome, string unidade, decimal minimo, GrupoEstoque grupo, UsoEstoque uso,
            string local, decimal? maximo = null, bool lote = false, bool validade = false, string? compra = null,
            decimal fator = 1m, bool ativo = true, string? fabricante = null)
            => db.Add(new ItemEstoque
            {
                CodigoInterno = codigo, Nome = nome, Unidade = unidade, EstoqueMinimo = minimo, EstoqueMaximo = maximo,
                Grupo = grupo, Uso = uso, LocalArmazenamento = local, ExigirLote = lote, ExigirValidade = validade,
                UnidadeCompra = compra, FatorCompra = fator, Ativo = ativo, Fabricante = fabricante, CriadoPor = "previa"
            }).Entity;

        var ag30 = Item("PV-AG2530", "Agulha 0,25 × 30 mm", "un", 200, GrupoEstoque.MaterialAssistencial, UsoEstoque.Procedimentos,
            "Armário 1 — sala de procedimentos", maximo: 1000, lote: true, validade: true, compra: "cx", fator: 100, fabricante: "Agulhas Lótus (fictício)");
        var ag40 = Item("PV-AG2540", "Agulha 0,25 × 40 mm", "un", 200, GrupoEstoque.MaterialAssistencial, UsoEstoque.Procedimentos,
            "Armário 1 — sala de procedimentos", maximo: 800, lote: true, validade: true, compra: "cx", fator: 100, fabricante: "Agulhas Lótus (fictício)");
        var algodao = Item("PV-ALG500", "Algodão hidrófilo 500 g", "pct", 4, GrupoEstoque.MaterialAssistencial, UsoEstoque.Ambos,
            "Almoxarifado");
        var alcool = Item("PV-ALC70", "Álcool 70% — 1 L", "fr", 6, GrupoEstoque.HigieneLimpeza, UsoEstoque.Ambos,
            "Almoxarifado", lote: true, validade: true);
        var luvas = Item("PV-LUVM", "Luvas de procedimento M", "par", 100, GrupoEstoque.ProtecaoIndividual, UsoEstoque.Ambos,
            "Armário 2 — enfermagem", compra: "cx", fator: 50);
        var eletrodo = Item("PV-ELE", "Eletrodo autoadesivo 5 × 5 cm", "un", 20, GrupoEstoque.MaterialAssistencial, UsoEstoque.Procedimentos,
            "Gaveta do eletroestimulador", lote: true, validade: true);
        var moxa = Item("PV-MOXA", "Moxa em bastão", "un", 10, GrupoEstoque.MaterialAssistencial, UsoEstoque.Procedimentos,
            "Armário 1 — sala de procedimentos");
        var esparadrapo = Item("PV-ESP", "Esparadrapo 10 cm × 4,5 m", "rl", 3, GrupoEstoque.MaterialAssistencial, UsoEstoque.Ambos,
            "Armário 2 — enfermagem");
        Item("PV-VENT", "Ventosa de vidro nº 3", "un", 0, GrupoEstoque.Instrumental, UsoEstoque.Procedimentos,
            "Armário 3", ativo: false);
        db.SaveChanges();

        void M(ItemEstoque item, TipoMovimentoEstoque tipo, decimal qtd, int diasAtras, decimal? custo = null,
            string? lote = null, DateOnly? validade = null, string? obs = null, int? atendimentoId = null,
            int? pacienteId = null, bool? paraCima = null, string? fornecedor = null, string? documento = null,
            string? setor = null, string quem = "Bianca Moraes")
            => db.Add(new MovimentoEstoque
            {
                ItemEstoqueId = item.Id, Tipo = tipo, Quantidade = qtd, Data = hoje.AddDays(-diasAtras),
                CustoUnitario = custo, Lote = lote, Validade = validade, Observacao = obs,
                AtendimentoId = atendimentoId, PacienteId = pacienteId, AjusteParaCima = paraCima,
                Fornecedor = fornecedor, DocumentoEntrada = documento,
                DestinoConsumo = tipo == TipoMovimentoEstoque.Saida
                    ? (atendimentoId is not null ? DestinoConsumoEstoque.Procedimento : DestinoConsumoEstoque.Rotina)
                    : DestinoConsumoEstoque.NaoInformado,
                SetorDestino = setor, CriadoPor = quem, CriadoEm = DateTime.Today.AddDays(-diasAtras).AddHours(10)
            });

        // Entradas, com lote e validade.
        M(ag30, TipoMovimentoEstoque.Entrada, 600, 40, 0.32m, "LT-2611", hoje.AddMonths(18), fornecedor: "Distribuidora Ponto Zen (fictícia)", documento: "NF 1042");
        M(ag40, TipoMovimentoEstoque.Entrada, 300, 40, 0.34m, "LT-2612", hoje.AddMonths(18), fornecedor: "Distribuidora Ponto Zen (fictícia)", documento: "NF 1042");
        M(algodao, TipoMovimentoEstoque.Entrada, 10, 30, 14.90m);
        M(alcool, TipoMovimentoEstoque.Entrada, 8, 120, 9.80m, "AL-0425", hoje.AddDays(20));   // vence em 20 dias
        M(luvas, TipoMovimentoEstoque.Entrada, 250, 25, 0.42m);
        M(eletrodo, TipoMovimentoEstoque.Entrada, 40, 200, 1.15m, "EL-0918", hoje.AddDays(-12)); // VENCIDO
        M(moxa, TipoMovimentoEstoque.Entrada, 30, 60, 3.60m);
        M(esparadrapo, TipoMovimentoEstoque.Entrada, 6, 50, 7.40m);
        db.SaveChanges();

        // Sessões que consumiram insumo — o custo por sessão vem daqui.
        var dias = new[] { 18, 12, 7, 3 };
        for (var i = 0; i < dias.Length && i < pacientes.Count; i++)
        {
            var atendimento = db.Add(new Atendimento
            {
                PacienteId = pacientes[i].Id, Data = hoje.AddDays(-dias[i]), Modalidade = ModalidadeAtendimento.AcupunturaSimples,
                LancadoPor = "previa", RealizadoEm = DateTime.Today.AddDays(-dias[i]).AddHours(15)
            }).Entity;
            db.SaveChanges();
            M(ag30, TipoMovimentoEstoque.Saida, 12 + i * 2, dias[i], 0.32m, "LT-2611", atendimentoId: atendimento.Id, pacienteId: pacientes[i].Id);
            M(ag40, TipoMovimentoEstoque.Saida, 60, dias[i], 0.34m, "LT-2612", atendimentoId: atendimento.Id, pacienteId: pacientes[i].Id);
            M(algodao, TipoMovimentoEstoque.Saida, 0.5m, dias[i], 14.90m, atendimentoId: atendimento.Id, pacienteId: pacientes[i].Id);
            if (i % 2 == 0)
                M(eletrodo, TipoMovimentoEstoque.Saida, 4, Math.Max(dias[i], 13), 1.15m, "EL-0918", atendimentoId: atendimento.Id, pacienteId: pacientes[i].Id);
        }

        // Rotina, perda com o motivo escrito e um acerto de inventário.
        M(luvas, TipoMovimentoEstoque.Saida, 40, 10, 0.42m, setor: "Enfermagem");
        M(alcool, TipoMovimentoEstoque.Saida, 2, 9, 9.80m, "AL-0425", setor: "Recepção");
        M(moxa, TipoMovimentoEstoque.Perda, 3, 8, 3.60m, obs: "Bastões úmidos — vazamento no armário 1 durante a chuva");
        M(esparadrapo, TipoMovimentoEstoque.Ajuste, 1, 2, 7.40m, paraCima: false,
            obs: "Inventário: contado 4 rl, sistema tinha 5 — rolo usado na recepção e não lançado", quem: "Dra. Helena Duarte");
        db.SaveChanges();
    }
}
