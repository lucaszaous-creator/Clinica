namespace Clinica.Desktop.Shell.Modulos;

/// <summary>Endereços estáveis por tarefa. Rótulos antigos continuam pesquisáveis pelas chaves.</summary>
public static class OrganizacaoNavegacao
{
    public static ItemMenuModulo Aplicar(ItemMenuModulo item)
    {
        var grupo = item.Grupo;
        var requer = item.Requer;
        var requerAlgum = item.RequerAlgum;
        if (item.Chave is "pacientes" or "pacientes-recepcao") { requer = Clinica.Domain.Entities.Permissao.Nenhuma; requerAlgum = Clinica.Domain.Entities.Permissao.VerFichaPaciente | Clinica.Domain.Entities.Permissao.VerProntuario; }
        var rotulo = item.Rotulo;
        var abas = item.Abas;
        var oculto = item.Oculto;
        switch (item.Chave)
        {
            case "agenda":
                // O DIA primeiro (out/2026, pedido da clínica): é a aba que o clique em
                // "Agenda" abre, e a recepção vive nela a manhã inteira.
                abas = [new("Dia", "fila"), new("Visão geral", ChavesSuite.OcupacaoAgenda)];
                break;
            case "consultorio-agenda":
                // O DIA primeiro, igual à Agenda da Recepção (out/2026, pedido da clínica): o
                // clique em "Agenda" abre a primeira aba, e no Consultório ela abria a Visão geral.
                rotulo = "Agenda";
                abas = [new("Dia", ChavesSuite.ConsultorioMeuDia), new("Visão geral", ChavesSuite.OcupacaoAgenda), new("Enfermagem", "consultorio-sessoes-enfermagem"),
                    new("Semana", ChavesSuite.ConsultorioSemana), new("Marcar atendimento", "marcar-horario"),
                    new("Evoluções pendentes", "consultorio-registros-pendentes")];
                break;
            case "atendimento":
                // Marcar, conferir o que foi lançado e renovar a consulta do convênio. As
                // conversas com o paciente (confirmar, convidar de volta, acompanhar) saíram
                // para "Relacionamento" — out/2026, mockup aprovado.
                rotulo = "Atendimento";
                abas = [new("Marcar", "marcar-horario"), new("Lançamentos", "lancamentos"), new("Consultas de convênio", "consultas")];
                break;
            case "pacientes":
                abas = [new("Lista de pacientes", ChavesSuite.PacientesRecepcao)];
                break;
            case "consultorio-pacientes": oculto = true; break;
            case ChavesSuite.AgendaRecepcao: oculto = true; break;
            // A caixa dos pedidos é ABA de "Relacionamento" (out/2026, pedido da clínica) e
            // o sino continua avisando o pedido novo. Como item solto ela fica escondida:
            // quem a mostra é o composto, e duas entradas para a mesma caixa eram a
            // redundância que a direção mandou tirar.
            case ChavesSuite.PedidosEnfermagem: oculto = true; break;
            case "pagamentos-recepcao": rotulo = "Recebimentos de pacientes"; grupo = GrupoSidebar.Financeiro; break;
            case "particular": rotulo = "Particular e pacotes"; grupo = GrupoSidebar.Financeiro; break;
            case "pacotes": case "precos-particular": grupo = GrupoSidebar.Financeiro; break;
            case "estoque": grupo = GrupoSidebar.Gestao; break;
            case "acessos": rotulo = "Usuários e permissões"; grupo = GrupoSidebar.Gestao; break;
            case "consultorio-modelos": case "auditoria": case "guarda-prontuario": case "documentos-emitidos": grupo = GrupoSidebar.Gestao; break;
            // Configurações, importação e a conformidade são da DIREÇÃO (out/2026): o módulo
            // os declara no grupo dela, e esta camada não os devolve à Gestão.
            // As campanhas e a origem dos pacientes ficam ao lado do Relacionamento do balcão. O
            // Acompanhamento sai daqui: ele é aba do "Relacionamento" — dois compostos com a
            // mesma tela eram dois caminhos para o mesmo lugar.
            case "marketing":
                // A comissão de recuperação (out/2026) é a terceira aba: o valor e a validação
                // ficam com a gestão, ao lado das campanhas que trazem o paciente de volta.
                rotulo = "Campanhas, origem e comissão"; grupo = GrupoSidebar.Relacionamento;
                abas = [.. item.Abas.Where(a => a.Chave != ChavesSuite.RetornoPacientes)];
                break;
            case "retorno-pacientes": rotulo = "Acompanhamento de pacientes"; grupo = GrupoSidebar.Relacionamento; break;
            case "retornos-a-marcar": grupo = GrupoSidebar.Relacionamento; break;
            case ChavesSuite.ConfirmacoesAgenda: rotulo = "Confirmações"; grupo = GrupoSidebar.Relacionamento; break;
            case "producao": case "consultorio-meus-numeros": grupo = GrupoSidebar.Inteligencia; break;
            case "resultado": grupo = GrupoSidebar.Inteligencia; rotulo = "Resultado e teto de despesas"; break;
            case "conciliacao": rotulo = "Conciliação de receitas"; break;
            case "extrato-banco": rotulo = "Conciliação bancária"; break;
        }
        return new ItemMenuModulo { Chave = item.Chave, Rotulo = rotulo, Grupo = grupo,
            Glifo = item.Glifo, Icone = item.Icone, Requer = requer, RequerAlgum = requerAlgum, PerfilExclusivo = item.PerfilExclusivo,
            Inicial = item.Inicial, Oculto = oculto, Abas = abas, PreservarAoNavegar = item.PreservarAoNavegar,
            Principal = item.Principal };
    }
}
