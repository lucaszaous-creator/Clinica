using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Clinica.Assinaturas.Api;

public static class FalhaPersistenciaTablet
{
    public static string? Codigo(DbUpdateException e) => (e.InnerException as PostgresException)?.SqlState;
    public static bool Conflito(DbUpdateException e) => e is DbUpdateConcurrencyException || Codigo(e) is "40001" or "40P01" or "23505";
    public static int Status(DbUpdateException e) => Conflito(e) ? 409 : 503;
    public static string Mensagem(DbUpdateException e) => Conflito(e)
        ? "Não foi possível confirmar este envio por uma alteração simultânea ou repetida. Preserve o texto e confira os registros antes de reenviar."
        : "Não foi possível gravar por uma falha do serviço. Preserve o texto desta tela e avise o suporte. Não é necessário refazer a evolução.";
}
