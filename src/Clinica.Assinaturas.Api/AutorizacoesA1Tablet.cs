using Clinica.Application.Tablet;

namespace Clinica.Assinaturas.Api;

public sealed class AutorizacoesA1Tablet(TimeProvider tempo)
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Autorizacao> itens = [];
    public sealed class Autorizacao
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public required string Sessao { get; init; }
        public int Agendamento { get; init; }
        public int Documento { get; init; }
        public required string Tipo { get; init; }
        public required string Hash { get; init; }
        public Guid CertificadoVersao { get; init; }
        public DateTimeOffset Expira { get; init; }
        public bool Usado { get; set; }
    }
    public Autorizacao Criar(string sessao, int agenda, string tipo, int documento, string hash, Guid certificado)
    {
        lock (gate)
        {
            foreach (var id in itens.Where(p => p.Value.Expira <= tempo.GetUtcNow() || p.Value.Sessao == sessao).Select(p => p.Key).ToArray()) itens.Remove(id);
            if (itens.Count >= 200) throw ErroFormularioTablet.Criar("Aguarde um instante para autorizar a assinatura.");
            var a = new Autorizacao { Sessao = sessao, Agendamento = agenda, Tipo = tipo, Documento = documento, Hash = hash,
                CertificadoVersao = certificado, Expira = tempo.GetUtcNow().AddMinutes(5) };
            itens.Add(a.Id, a); return a;
        }
    }
    public Autorizacao Consumir(Guid id, string sessao)
    {
        lock (gate)
        {
            if (!itens.TryGetValue(id, out var a) || a.Sessao != sessao || a.Expira <= tempo.GetUtcNow()) throw new RecursoClinicoIndisponivel();
            if (a.Usado) throw new ConflitoClinicoTablet("Esta autorização já foi utilizada. Confira o documento no histórico.");
            a.Usado = true; return a;
        }
    }
}
