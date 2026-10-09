using System.IO;
using System.Text.Json;
using Clinica.Desktop.Shell.Web;

/// <summary>Inventário reproduzível; registro de comando não é prova de execução.</summary>
internal static class MapaAcoesQa
{
    internal static void Gravar(PaginasWebController.Pagina[] paginas, DialogosWebController.RegistroDialogo[] dialogos)
    {
        var itens = new List<object>();
        foreach (var p in paginas)
        {
            void Incluir(IEnumerable<PaginasWebController.Acao> acoes, string local)
            {
                foreach (var a in acoes)
                    itens.Add(new { id = "pagina/" + p.Chave + "/" + local + "/" + (a.Chave ?? a.Comando) + (a.Parametro is null ? "" : "/parametro:" + JsonSerializer.Serialize(a.Parametro)), tela = p.Titulo,
                        tipo = p.Tipo.FullName, local, comando = a.Comando, rotulo = a.Rotulo, permissao = a.Permissao.ToString(),
                        guarda = a.Guarda, visivel = a.Visivel, alvoLinha = a.AlvoLinha, parametro = a.Parametro });
            }
            Incluir(p.Acoes, "cabecalho");
            foreach (var s in p.Secoes)
            {
                Incluir(s.Acoes, s.Chave);
                foreach (var t in s.Tabelas) Incluir(t.Acoes, s.Chave + "/" + t.Chave);
            }
        }
        foreach (var d in dialogos)
        {
            void Incluir(IEnumerable<DialogosWebController.Acao> acoes, string local)
            {
                foreach (var a in acoes)
                    itens.Add(new { id = "dialogo/" + d.Chave + "/" + d.Tipo.Name + "/" + local + "/" + a.Chave,
                        tela = d.Definicao.Titulo, tipo = d.Tipo.FullName, local, comando = a.Comando, rotulo = a.Rotulo,
                        permissao = d.Permissao.ToString(), guarda = a.Habilitado, visivel = a.Visivel, alvoLinha = a.HabilitadoLinha });
            }
            Incluir(d.Definicao.Acoes, "rodape");
            foreach (var t in d.Definicao.Tabelas) Incluir(t.Acoes, t.Chave);
        }
        Directory.CreateDirectory("artifacts/mapa-botoes");
        File.WriteAllText("artifacts/mapa-botoes/contratos.json", JsonSerializer.Serialize(new {
            paginas = paginas.Select(p => new { p.Chave, p.Titulo, tipo = p.Tipo.FullName }),
            dialogos = dialogos.Select(d => new { d.Chave, tipo = d.Tipo.FullName }), acoes = itens,
            observacao = "Inventário dos contratos. Não comprova clique, persistência ou cobertura de componentes próprios."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"MAPA: {itens.Count} ações registradas em {paginas.Length} páginas e {dialogos.Length} formulários.");
    }
}
