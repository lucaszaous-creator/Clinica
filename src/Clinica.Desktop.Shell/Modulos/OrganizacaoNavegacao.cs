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
                abas = [new("Enfermagem", "consultorio-sessoes-enfermagem"), new("Dia", "fila"), new("Grade", ChavesSuite.AgendaRecepcao),
                    new("Semana", ChavesSuite.ConsultorioSemana), new("Marcar atendimento", "marcar-horario"),
                    new("Confirmações", "agenda-confirmacoes"), new("Retornos solicitados", "retornos-a-marcar"), new("Conferir atendimentos", "lancamentos"),
                    new("Consultas de convênio", "consultas")];
                break;
            case "consultorio-agenda":
                rotulo = "Agenda";
                abas = [new("Enfermagem", "consultorio-sessoes-enfermagem"), new("Dia", ChavesSuite.ConsultorioMeuDia), new("Grade", ChavesSuite.AgendaRecepcao),
                    new("Semana", ChavesSuite.ConsultorioSemana), new("Marcar atendimento", "marcar-horario"),
                    new("Evoluções pendentes", "consultorio-registros-pendentes")];
                break;
            case "atendimento": oculto = true; break;
            case "pacientes":
                abas = [new("Lista de pacientes", ChavesSuite.PacientesRecepcao)];
                break;
            case "consultorio-pacientes": oculto = true; break;
            case "pagamentos-recepcao": rotulo = "Recebimentos de pacientes"; grupo = GrupoSidebar.Financeiro; break;
            case "particular": rotulo = "Particular e pacotes"; grupo = GrupoSidebar.Financeiro; break;
            case "pacotes": case "precos-particular": grupo = GrupoSidebar.Financeiro; break;
            case "estoque": grupo = GrupoSidebar.Gestao; break;
            case "acessos": rotulo = "Usuários e permissões"; grupo = GrupoSidebar.Gestao; break;
            case "configuracoes": case "auditoria": case "conformidade":
            case "consultorio-modelos": case "importar-pacientes": case "guarda-prontuario": case "documentos-emitidos": grupo = GrupoSidebar.Gestao; break;
            case "marketing": rotulo = "Relacionamento"; grupo = GrupoSidebar.Gestao; break;
            case "retorno-pacientes": rotulo = "Recall de pacientes"; grupo = GrupoSidebar.Gestao; break;
            case "retornos-a-marcar": rotulo = "Retornos solicitados pelo profissional"; grupo = GrupoSidebar.Gestao; break;
            case "marcar-horario": rotulo = "Marcar atendimento"; grupo = GrupoSidebar.Gestao; break;
            case "lancamentos": rotulo = "Conferir atendimentos"; grupo = GrupoSidebar.Gestao; break;
            case "producao": case "consultorio-meus-numeros": grupo = GrupoSidebar.Inteligencia; break;
            case "resultado": grupo = GrupoSidebar.Inteligencia; rotulo = "Resultado e teto de despesas"; break;
            case "conciliacao": rotulo = "Conciliação de receitas"; break;
            case "extrato-banco": rotulo = "Conciliação bancária"; break;
        }
        return new ItemMenuModulo { Chave = item.Chave, Rotulo = rotulo, Grupo = grupo,
            Glifo = item.Glifo, Icone = item.Icone, Requer = requer, RequerAlgum = requerAlgum, PerfilExclusivo = item.PerfilExclusivo,
            Inicial = item.Inicial, Oculto = oculto, Abas = abas };
    }
}
