using Clinica.Domain.Entities;

namespace Clinica.Application.Servicos;

/// <summary>Progresso assistencial derivado dos registros; não representa uma requisição em andamento.</summary>
public sealed record EtapaInfusao(string Codigo, string Titulo, bool Concluida, bool Atual, bool Atencao = false)
{
    public string Situacao => Concluida ? "Concluída" : Atencao ? "Precisa de revisão" : Atual ? "Etapa atual" : "Próxima etapa";
    public string Marca => Concluida ? "✓" : Atencao ? "!" : Atual ? "◉" : "○";
}

public static class EtapasInfusao
{
    public static IReadOnlyList<EtapaInfusao> Da(PrescricaoInterna p)
    {
        var medico = p.Assinaturas.FirstOrDefault(a => a.Papel == PapelAssinatura.Prescritor);
        var enfermagem = p.AssinaturaDaExecucao;
        var assinadaEnfermagem = enfermagem?.ArquivoId is not null;
        var arquivoExecucao = assinadaEnfermagem;
        var encerrada = p.EncerradaEm is not null || p.Situacao == SituacaoPrescricao.Encerrada;
        var assinadaMedico = p.AssinadaEm is not null;
        var papel = !p.ExigeAssinaturaEletronicaDaExecucao && !p.OrigemEnfermagem && enfermagem is null;
        var assinaturaTitulo = papel ? "Conferir assinatura na via impressa"
            : enfermagem is not null && !arquivoExecucao ? "Documento indisponível"
            : "Assinatura da enfermagem";
        var passos = new List<(string Codigo, string Titulo, bool Feita)>();
        if (p.OrigemEnfermagem)
        {
            passos.Add(("execucao", "Execução registrada", encerrada));
            passos.Add(("enfermagem", assinaturaTitulo, arquivoExecucao));
            if (p.DevolvidaEm is not null)
                passos.Add(("revisao", "Devolvida: revisão da enfermagem", p.Retificacao is not null));
            passos.Add(("medico", "Avaliação e assinatura médica", assinadaMedico));
        }
        else
        {
            passos.Add(("medico", "Prescrição e assinatura médica", assinadaMedico));
            passos.Add(("execucao", p.ExecucaoCompleta && !encerrada ? "Encerrar execução" : "Execução e encerramento", encerrada));
            passos.Add(("enfermagem", assinaturaTitulo, arquivoExecucao));
        }
        passos.Add(("conclusao", "Concluída · prescrição salva", encerrada && arquivoExecucao
            && assinadaMedico && medico?.ArquivoId is not null && p.DevolvidaEm is null && !p.Cancelada));
        bool encontrouAtual = false;
        return passos.Select(e => {
            var atual = !e.Feita && !encontrouAtual && !p.Cancelada;
            if (atual) encontrouAtual = true;
            return new EtapaInfusao(e.Codigo, e.Titulo, e.Feita, atual,
                atual && (e.Codigo == "revisao" || e.Codigo == "enfermagem" && (papel || enfermagem is not null && !arquivoExecucao)));
        }).ToArray();
    }
}
