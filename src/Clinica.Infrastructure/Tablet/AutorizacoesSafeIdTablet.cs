using System.Security.Cryptography;
using Clinica.Application.Assinatura.SafeID;
using Clinica.Application.Tablet;

namespace Clinica.Infrastructure.Tablet;

/// <summary>Segredos temporários somente na memória do servidor; reinício exige nova autorização.</summary>
public sealed class AutorizacoesSafeIdTablet(TimeProvider tempo)
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Autorizacao> pendentes = [];
    public sealed class Autorizacao
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Estado { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        public DesafioPkce Pkce { get; } = DesafioPkce.Gerar();
        public required string Sessao { get; init; }
        public required int Agendamento { get; init; }
        public required int Documento { get; init; }
        public required string Tipo { get; init; }
        public required string ConteudoHash { get; init; }
        public required long ExpiraEm { get; init; }
        public bool ConfirmouAlergia { get; init; }
        public string? Codigo { get; private set; }
        public string Situacao { get; private set; } = "aguardando";
        public void Receber(string? codigo, string? erro)
        {
            if (Situacao != "aguardando") throw new RecursoClinicoIndisponivel();
            Codigo = codigo; Situacao = string.IsNullOrEmpty(erro) && !string.IsNullOrEmpty(codigo) ? "autorizado" : "recusado";
        }
        public void Consumir() { if(Situacao != "autorizado") throw new ConflitoClinicoTablet("A autorização ainda não está disponível ou já foi utilizada."); Situacao = "assinando"; }
        public void Concluir(bool sucesso) {Codigo = null; Situacao = sucesso ? "concluido" : "falha";}
    }
    public Autorizacao Criar(string sessao, int agendamento, int documento, string tipo, string hash, bool alergia)
    {
        lock(gate)
        {
            var agora = tempo.GetUtcNow().ToUnixTimeMilliseconds();
            foreach(var id in pendentes.Where(p => p.Value.ExpiraEm <= agora).Select(p => p.Key).ToArray()) pendentes.Remove(id);
            if(pendentes.Count >= 100) throw new InvalidOperationException("Há muitas autorizações em andamento. Aguarde um instante.");
            if(pendentes.Values.Any(p=>p.Sessao==sessao && p.Situacao is "aguardando" or "autorizado" or "assinando"))
                throw new ConflitoClinicoTablet("Conclua a autorização anterior ou aguarde sua expiração antes de pedir outra.");
            var a = new Autorizacao {Sessao = sessao, Agendamento = agendamento, Documento = documento,
                Tipo = tipo, ConteudoHash = hash, ConfirmouAlergia = alergia, ExpiraEm = agora + 300_000};
            pendentes.Add(a.Id, a); return a;
        }
    }
    public Guid Receber(string estado, string? codigo, string? erro)
    {
        if(estado.Length != 64 || codigo?.Length > 4096 || erro?.Length > 200) throw new RecursoClinicoIndisponivel();
        lock(gate)
        {
            var a = pendentes.Values.SingleOrDefault(p=>p.Estado==estado && p.ExpiraEm>tempo.GetUtcNow().ToUnixTimeMilliseconds())
                ?? throw new RecursoClinicoIndisponivel();
            a.Receber(codigo,erro); return a.Id;
        }
    }
    public Autorizacao Obter(Guid id, string sessao, bool consumir = false)
    {
        lock(gate)
        {
            if(!pendentes.TryGetValue(id,out var a) || a.Sessao!=sessao || a.ExpiraEm<=tempo.GetUtcNow().ToUnixTimeMilliseconds())
                throw new RecursoClinicoIndisponivel();
            if(consumir) a.Consumir(); return a;
        }
    }
    public void Concluir(Autorizacao a, bool sucesso) {lock(gate) a.Concluir(sucesso);}
}
