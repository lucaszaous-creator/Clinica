// Dados FICTÍCIOS da prévia — nenhuma clínica ou servidor real.
using Clinica.Application.Servicos;
using Clinica.Domain;
using Clinica.Domain.Entities;
using Clinica.Infrastructure;

namespace PreviaBlazor.Sementes;

/// <summary>
/// As CONFIGURAÇÕES da clínica (Gerente, out/2026). O prestador e os catálogos já vêm da semente
/// do faturamento; esta acrescenta o que só esta tela e os dois formulários dela mostram:
/// - campos personalizados do prontuário — dois ativos (um de lista) e um DESATIVADO, para a
///   linha apagada e o "Reativar" aparecerem;
/// - um termo do procedimento escrito pela clínica, com declarações, e uma exigência DESLIGADA
///   (desligada para não fazer o balcão de outras telas cobrar termo na prévia);
/// - e-mail, endereço e telefone da clínica (o e-mail é o destino padrão do teste do SMTP).
/// O que NÃO entra, de propósito: servidor SMTP, armazenamento e pasta de backup — o teste das
/// telas clica cada botão, e "Testar conexão"/"Copiar agora" tentariam a rede e o disco de verdade.
/// Os estados DESLIGADOS (publicação, lembrete, cópia automática) são o que a tela mostra sem eles.
/// Sem Id fixo.
/// </summary>
public sealed class SementeConfiguracoesDaClinica : ISemente
{
    public int Ordem => 97;

    public void Semear(ClinicaDbContext db)
    {
        var repo = new ClinicaRepositorio(db);
        var parametros = new ParametrosService(repo);

        var prestador = parametros.ObterPrestadorAsync().GetAwaiter().GetResult();
        prestador.Email ??= "contato@exemplo.invalid";
        prestador.Telefone ??= "(22) 0000-0000";
        prestador.Endereco ??= "Rua Fictícia, 100 — Centro";
        parametros.SalvarPrestadorAsync(prestador).GetAwaiter().GetResult();

        var agora = DateTime.Now;
        db.AddRange(
            new CampoPersonalizadoProntuario
            {
                Rotulo = "Nº de agulhas", Tipo = TipoCampoPersonalizado.Numero, Ordem = 1, Ativo = true,
                ModalidadeCodigo = nameof(ModalidadeAtendimento.AcupunturaSimples),
                Ajuda = "Total de agulhas aplicadas na sessão, contando as auriculares.",
                CriadoEm = agora, CriadoPor = "Direção (prévia)"
            },
            new CampoPersonalizadoProntuario
            {
                Rotulo = "Aparelho usado", Tipo = TipoCampoPersonalizado.Lista, Ordem = 2, Ativo = true,
                Opcoes = "Eletroestimulador A\nEletroestimulador B\nLaser", CriadoEm = agora, CriadoPor = "Direção (prévia)"
            },
            new CampoPersonalizadoProntuario
            {
                Rotulo = "Resposta ao De Qi", Tipo = TipoCampoPersonalizado.Texto, Ordem = 3, Ativo = false,
                CriadoEm = agora, CriadoPor = "Direção (prévia)"
            });

        var termo = new ModeloDocumento
        {
            Tipo = TipoDocumentoClinico.TermoProcedimento,
            Nome = "Termo da infusão (exemplo)",
            Titulo = "Consentimento para infusão endovenosa",
            Corpo = "Texto FICTÍCIO da prévia. Fui informado(a) sobre o procedimento, seus riscos e alternativas, "
                    + "e tive a oportunidade de tirar minhas dúvidas antes de assinar.",
            Ativo = true, CriadoEm = agora, CriadoPor = "Direção (prévia)",
            Itens =
            [
                new ItemModelo { Ordem = 1, Descricao = "Estou em jejum de 2 horas" },
                new ItemModelo { Ordem = 2, Descricao = "Informei as medicações que uso" }
            ]
        };
        db.Add(termo);
        db.SaveChanges();

        db.Add(new ExigenciaTermoProcedimento
        {
            Modalidade = ModalidadeAtendimento.AcupunturaSimples, ModalidadeCodigo = string.Empty,
            ModeloDocumentoId = termo.Id, Ativa = false, SoValeNoDiaDoProcedimento = true,
            CriadoEm = agora, CriadoPor = "Direção (prévia)"
        });
        db.SaveChanges();
    }
}
