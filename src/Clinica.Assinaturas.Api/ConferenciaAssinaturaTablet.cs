using Clinica.Application.Abstracoes;
using Clinica.Domain;
using Clinica.Application.Assinatura;
using Clinica.Application.Servicos;
using Clinica.Application.Tablet;
using Clinica.Domain.Entities;
using Clinica.Infrastructure.Tablet;

namespace Clinica.Assinaturas.Api;

public sealed class ConferenciaAssinaturaTablet(IClinicaRepositorio repo, PrescricaoInternaService prescricoes)
{
    public async Task<string> Conferir(string tipo,int id,bool confirmou,CancellationToken ct)
    {
        var prestador=await new ParametrosService(repo).ObterPrestadorAsync(ct);
        if(tipo=="execucao")
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct)??throw new RecursoClinicoIndisponivel();
            if(!p.AguardaAssinaturaDaExecucao || !p.ExecucaoCompleta || !p.OrigemEnfermagem && p.AssinaturaDoPrescritor?.ArquivoId is null)
                throw ErroFormularioTablet.Criar("Encerre a execução e confira a via assinada pelo prescritor antes de assinar.");
            var alerta=await prescricoes.ConferirParaAssinaturaAsync(id,ct);
            return ContratoTablet.Hash(ContratoTablet.Serializar(new {Conteudo=ConteudoPrescricao(p),Cadastro=Cadastro(p.Paciente,p.Profissional),Prestador=prestador,
                Alergias=alerta.Alergias.OrderBy(a=>a.Id).Select(a=>new {a.Id,a.Descricao,a.AtualizadoEm})}));
        }
        if(tipo=="infusao")
        {
            var p=await repo.ObterPrescricaoInternaAsync(id,ct) ?? throw new RecursoClinicoIndisponivel();
            if((!p.PodeEditar && !(p.OrigemEnfermagem && p.AguardaValidacaoMedica && p.AssinaturaDaExecucao?.ArquivoId!=null)) || p.Itens.Count==0)
                throw ErroFormularioTablet.Criar("Confira o rascunho ou a execução assinada pela enfermagem antes de validar.");
            var alerta=await prescricoes.ConferirParaAssinaturaAsync(id,ct);
            if(alerta.ExigeConfirmacao && !confirmou) throw ErroFormularioTablet.Criar("Confira as alergias e confirme a revisão antes de assinar.");
            return ContratoTablet.Hash(ContratoTablet.Serializar(new {Conteudo=ConteudoPrescricao(p),
                Cadastro=Cadastro(p.Paciente,p.Profissional),Prestador=prestador,
                Alergias=alerta.Alergias.OrderBy(a=>a.Id).Select(a=>new {a.Id,a.Descricao,a.AtualizadoEm})}));
        }
        var d=await repo.ObterDocumentoAsync(id,ct) ?? throw new RecursoClinicoIndisponivel();
        if(d.AssinadoEletronicamente || d.Cancelado) throw ErroFormularioTablet.Criar("O documento já foi assinado ou cancelado.");
        var faltas=ConformidadeDocumentoClinico.Conferir(d,assinaturaEletronica:true).Where(f=>f.Impeditiva).ToList();
        if(faltas.Count>0) throw ErroFormularioTablet.Criar(string.Join(" ",faltas.Select(f=>f.Descricao))
            + " Corrija as informações indicadas, confira o PDF atualizado e tente autorizar novamente.");
        return ContratoTablet.Hash(ContratoTablet.Serializar(new {d.PacienteId,d.ProfissionalId,d.AgendamentoId,d.Data,d.Numero,
            Cadastro=Cadastro(d.Paciente,d.Profissional),Prestador=prestador,
            d.Tipo,d.Titulo,d.Corpo,d.CorpoFormatado,d.Observacoes,d.ObservacoesFormatadas,d.DiasAfastamento,d.Cid,d.CidAutorizado,
            d.PeriodoInicio,d.PeriodoFim,d.HoraChegada,d.HoraSaida,d.CodigoVerificacao,
            Itens=d.Itens.OrderBy(i=>i.Ordem).ThenBy(i=>i.Id).Select(i=>new {i.Id,i.Ordem,i.Codigo,i.Descricao,i.DescricaoFormatada,i.Detalhe,i.DetalheFormatado,i.Quantidade,i.Desenho})}));
    }
    // Abrange também o registro de execução, que pode ser selado no mesmo ato da prescrição.
    // Ordem e desempate seguem o PDF; não serializamos navegações circulares das entidades.
    private static object ConteudoPrescricao(PrescricaoInterna p)=>new {
        p.Id,p.PacienteId,p.ProfissionalId,p.AgendamentoId,p.Numero,p.CodigoVerificacao,p.Data,p.Hora,p.CriadoEm,
        p.Situacao,p.EncerradaEm,p.DevolvidaEm,p.MotivoDevolucao,p.RetificaPrescricaoId,p.CanceladaEm,p.MotivoCancelamento,
        p.OrigemEnfermagem,p.OrientacaoExterna,p.RegistradaPorUsuarioId,p.ExigeAssinaturaEletronicaDaExecucao,
        p.DiluicaoUnica,p.DiluenteGlobal,p.VolumeTotal,p.Indicacao,p.IndicacaoFormatada,p.Observacoes,p.ObservacoesFormatadas,
        Itens=p.Itens.OrderBy(i=>i.Ordem).ThenBy(i=>i.Id).Select(i=>new {
            i.Id,i.Ordem,i.Descricao,i.DescricaoFormatada,i.Dose,i.Diluente,i.Volume,i.Via,i.TempoInfusao,i.HoraPrevista,i.SeNecessario,
            i.Observacoes,i.ObservacoesFormatadas,i.SuspensoEm,i.MotivoSuspensao,i.SuspensoPor,
            Checagens=i.Checagens.OrderBy(c=>c.Id).Select(c=>new {c.Id,c.Situacao,c.NaoExecutavel,c.DataRealizacao,c.HoraRealizacao,
                c.Justificativa,c.ExecutanteUsuarioId,c.ExecutanteNome,c.ExecutanteConselho,c.RegistradoEm,c.RetificaChecagemId,c.MotivoRetificacao})}),
        Evolucoes=p.EvolucoesEnfermagem.OrderBy(e=>e.Data).ThenBy(e=>e.Hora).ThenBy(e=>e.Id).Select(e=>new {
            e.Id,e.Data,e.Hora,e.Texto,e.FaseAtendimento,e.Historico,e.ExameFisico,e.Avaliacao,e.Intercorrencia,
            e.PressaoSistolica,e.PressaoDiastolica,e.FrequenciaCardiaca,e.FrequenciaRespiratoria,e.Temperatura,e.SaturacaoOxigenio,e.Dor,
            e.AutorUsuarioId,e.AutorNome,e.AutorConselho,e.RegistradoEm,e.RetificaEvolucaoId,e.MotivoRetificacao,e.CanceladaEm,e.MotivoCancelamento,
            e.AcessoLocal,e.AcessoCalibre,e.AcessoPuncionadoEm,
            Diagnosticos=e.Diagnosticos.OrderBy(x=>x.Ordem).ThenBy(x=>x.Id).Select(x=>new {x.Id,x.Ordem,x.Codigo,x.Titulo,x.RelacionadoA,x.EvidenciadoPor,x.ResultadoEsperado}),
            Cuidados=e.Cuidados.OrderBy(x=>x.Ordem).ThenBy(x=>x.Id).Select(x=>new {x.Id,x.Ordem,x.Codigo,x.Descricao,x.Frequencia,x.SeNecessario})}),
        Assinaturas=p.Assinaturas.OrderBy(a=>a.Id).Select(a=>new {a.Id,a.Papel,a.Tipo,a.ArquivoId,a.ArquivoRegistroId,a.HashConteudo,
            a.NomeAssinante,a.RegistroConselho,a.CpfAssinante,a.AssinadoEm,a.CertificadoTitular,a.CertificadoEmissor,a.CertificadoSerie,a.CarimboTempoEm,a.CarimboTempoAutoridade})
    };
    public static object Cadastro(Paciente? p,Profissional? profissional)=>new {
        Paciente=p is null ? null : new {p.Id,p.Nome,p.Documento,p.DataNascimento,p.Endereco,p.Telefone,p.Email,p.Carteirinha,p.ConvenioCodigo},
        Profissional=profissional is null ? null : new {profissional.Id,profissional.Nome,profissional.Cpf,profissional.RegistroConselho,profissional.EspecialidadeCodigo,profissional.Ativo}
    };
}
