namespace Clinica.Domain.Entities;

// O SEGUNDO FATOR DO PORTAL (nov/2026). O código de cadastro do tablet — um segredo único,
// compartilhado por toda a equipe — saiu; no lugar dele, cada pessoa tem o seu autenticador
// (TOTP, RFC 6238) e pode marcar o aparelho como confiável por 30 dias.
//
// Tabelas PRÓPRIAS, e não colunas em `UsuarioSistema`: o faturamento em produção lê essa
// tabela e aplica migrations na abertura, e migration só aditiva numa tabela nova não
// encosta em nada que ele usa. Datas em milissegundos Unix (UTC), como `SessaoTablet`:
// nenhuma conversa de `Kind` com o Npgsql.

/// <summary>O autenticador de um usuário. Uma linha por usuário; sem linha, o próximo login cadastra.</summary>
public sealed class SegundoFatorPortal
{
    public int UsuarioId { get; set; }
    public UsuarioSistema? Usuario { get; set; }

    /// <summary>O segredo TOTP CIFRADO pela proteção de dados da API (nunca em claro no banco).</summary>
    public string SegredoProtegido { get; set; } = "";

    public long AtivadoEm { get; set; }

    /// <summary>
    /// O último passo de 30 s aceito. Um código vale UMA vez: repetir o mesmo código (ou um
    /// anterior), visto por cima do ombro ou capturado, é recusado.
    /// </summary>
    public long UltimoPasso { get; set; }

    /// <summary>Falhas seguidas de código; ao chegar no teto, a entrada trava como a da senha.</summary>
    public int FalhasSeguidas { get; set; }

    public Guid Versao { get; set; } = Guid.NewGuid();
}

/// <summary>Um aparelho que a pessoa marcou como confiável: nele o código não é pedido até <see cref="ExpiraEm"/>.</summary>
public sealed class DispositivoConfiavelPortal
{
    /// <summary>SHA-256 de "usuário|dispositivo": a confiança é da PESSOA naquele aparelho, não do aparelho.</summary>
    public string Id { get; set; } = "";
    public int UsuarioId { get; set; }
    public long CriadoEm { get; set; }
    public long ExpiraEm { get; set; }

    /// <summary>
    /// A versão da senha quando a confiança foi dada (resumo do hash). Trocar a senha desfaz a
    /// confiança em todos os aparelhos — quem descobriu a senha antiga não herda o aparelho.
    /// </summary>
    public string Credencial { get; set; } = "";

    /// <summary>Endereço de origem no momento em que a confiança foi dada (trilha, nunca regra).</summary>
    public string? Origem { get; set; }
}

/// <summary>Código de recuperação (perdeu o celular). Guardado só o resumo; vale uma vez.</summary>
public sealed class CodigoRecuperacaoPortal
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Resumo { get; set; } = "";
    public long? UsadoEm { get; set; }
}

/// <summary>
/// A LIBERAÇÃO do primeiro acesso (nov/2026, achado da auditoria de segurança): sem ela, quem
/// soubesse a senha de alguém que ainda não cadastrou o autenticador cadastraria o PRÓPRIO celular
/// e trancaria o dono fora. A direção gera o código na tela de Acessos e o entrega à pessoa; ele vale
/// uma vez, por pouco tempo, e cinco erros o invalidam. Guardado só o resumo.
/// </summary>
public sealed class LiberacaoPortal
{
    public int UsuarioId { get; set; }
    public string Resumo { get; set; } = "";
    public long CriadaEm { get; set; }
    public long ExpiraEm { get; set; }
    public string CriadaPor { get; set; } = "";
    public int Falhas { get; set; }
}

