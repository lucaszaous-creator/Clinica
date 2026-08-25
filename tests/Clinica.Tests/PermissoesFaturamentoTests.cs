using Clinica.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Clinica.Tests;

/// <summary>
/// As permissões granulares do faturamento (parcela 45) — o pedido da cliente: "permitir
/// usuários no módulo faturamento com permissões granulares, assim a gerente consegue
/// auditar o que está sendo feito e liberar ou não permissões".
///
/// O que estes testes protegem é o PADRÃO de cada perfil — o que a direção decidiu que
/// cada função faz. Ele foi refeito na parcela 49, quando ela apontou o buraco: "não
/// adianta ter permissão granular se todo perfil nasce podendo tudo".
///
/// Cada linha aqui é uma decisão de negócio, não uma verificação de enum. Quando alguém
/// mudar um padrão, é aqui que aparece — e a pergunta passa a ser "a direção decidiu
/// isso?", que é a pergunta certa.
/// </summary>
public class PermissoesFaturamentoTests
{
    /// <summary>
    /// O que o faturista PRECISA para tocar o dia: ler, dar baixa, glosar, mandar o lote e
    /// lançar atendimento. Se alguém tirar um destes, o trabalho para — e é aqui que
    /// aparece.
    /// </summary>
    [Theory]
    [InlineData(Permissao.VerFaturamento)]
    [InlineData(Permissao.BaixarGuia)]
    [InlineData(Permissao.RegistrarGlosa)]
    [InlineData(Permissao.GerenciarLotesTiss)]
    [InlineData(Permissao.LancarAtendimento)]
    [InlineData(Permissao.VerAgenda)]
    [InlineData(Permissao.VerFichaPaciente)]
    [InlineData(Permissao.EditarPaciente)]
    public void Faturista_faz_o_trabalho_do_dia(Permissao permissao)
        => PerfisAcesso.Padrao(PerfilAcesso.Faturista).HasFlag(permissao).Should().BeTrue();

    /// <summary>
    /// O que o faturista NÃO recebe por padrão (parcela 49) — decisão da direção, com a
    /// razão de cada um:
    ///
    /// · <b>EstornarBaixa</b> — desfazer apaga o trabalho de outra pessoa e desfaz o elo
    ///   com a conciliação. Errar a baixa é acidente; estornar é decisão.
    /// · <b>MarcarNaoConformidade</b> — é a ÚNICA permissão que faz uma pendência sumir do
    ///   painel sem a guia ter sido faturada. Quem a tem pode zerar o alarme do sistema
    ///   sobre o próprio trabalho que ele existe para cobrar.
    /// · <b>VerIndicadores</b> — guarda os relatórios gerenciais (e o BI). Operar o dia é
    ///   uma coisa; ler o desempenho da clínica é outra.
    /// · <b>VerProntuario / EditarProntuario</b> — faturar não exige ler a evolução de
    ///   ninguém. O que a baixa usa é a FICHA (convênio, carteirinha, autorização), e essa
    ///   ele tem.
    /// · <b>ConfigurarFaturamento</b> — muda a regra para todo mundo, não o registro de
    ///   uma guia.
    /// · <b>EditarAgenda</b> (parcela 58) — quem MARCA horário é o balcão, com o paciente
    ///   na frente. O faturista continua com <c>VerAgenda</c>, porque olhar o dia é parte
    ///   de faturá-lo; o que ele perde é abrir horário na agenda dos médicos. A direção
    ///   pediu isso depois de o sistema materializar a pendência do 2º código como
    ///   AGENDAMENTO (a mesma parcela): guia não é atendimento, e um horário aberto do
    ///   lado do faturamento aparece na fila do balcão e na agenda de quem atende.
    ///
    /// Nenhum é proibição absoluta: a direção concede qualquer um a uma pessoa específica
    /// em Acessos, sem mexer no perfil dos outros. O padrão é o que ela NÃO precisa
    /// decidir todo dia.
    /// </summary>
    [Theory]
    [InlineData(Permissao.EstornarBaixa)]
    [InlineData(Permissao.MarcarNaoConformidade)]
    [InlineData(Permissao.VerIndicadores)]
    [InlineData(Permissao.VerProntuario)]
    [InlineData(Permissao.EditarProntuario)]
    [InlineData(Permissao.ConfigurarFaturamento)]
    [InlineData(Permissao.EditarAgenda)]
    public void Faturista_nao_recebe_o_que_e_da_direcao(Permissao permissao)
        => PerfisAcesso.Padrao(PerfilAcesso.Faturista).HasFlag(permissao).Should().BeFalse();

    /// <summary>
    /// O faturista continua VENDO a agenda (parcela 58).
    ///
    /// Tirar `EditarAgenda` sem manter `VerAgenda` seria tirar capacidade de quem a usava
    /// — a regra 3 do bloco do faturamento no CLAUDE.md. O que a direção pediu foi
    /// impedir que ele ABRA horário; conferir o dia é parte de faturá-lo.
    /// </summary>
    [Fact]
    public void Faturista_continua_vendo_a_agenda_sem_poder_marcar()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Faturista);

        padrao.HasFlag(Permissao.VerAgenda).Should().BeTrue(
            "conferir o dia é parte de faturá-lo");
        padrao.HasFlag(Permissao.EditarAgenda).Should().BeFalse(
            "quem marca horário é o balcão, com o paciente na frente");
    }

    /// <summary>
    /// A RECEPÇÃO marca horário — é o outro lado da decisão acima.
    ///
    /// Tirar `EditarAgenda` do faturista só faz sentido porque a capacidade continua
    /// inteira em quem é dono dela. Um teste que fixasse só a remoção deixaria passar a
    /// versão em que a agenda ficou sem ninguém que pudesse mexer nela.
    /// </summary>
    [Fact]
    public void Recepcao_marca_horario()
        => PerfisAcesso.Padrao(PerfilAcesso.Recepcao)
            .HasFlag(Permissao.EditarAgenda).Should().BeTrue();

    // ================================================================
    // O PADRÃO DE CADA PERFIL (parcela 49)
    //
    // "Não adianta ter permissão granular se todo perfil nasce podendo tudo." Os testes
    // abaixo são a resposta escrita a esse pedido: cada um fixa uma decisão de quem pode
    // o quê, com a razão no comentário. Mudar um padrão sem mudar o teste é mudar uma
    // decisão de negócio sem que ninguém veja.
    // ================================================================

    /// <summary>
    /// O BALCÃO não lê o prontuário clínico — foi o primeiro exemplo que a direção deu.
    ///
    /// O corte é o da LGPD: telefone e convênio são dado de CONTATO; evolução, EVA e
    /// alergia são dado de SAÚDE (art. 5º, II), e não é preciso lê-los para marcar um
    /// horário. Até a parcela 48 não havia como separar — um bit só abria os dois.
    /// </summary>
    [Fact]
    public void Recepcao_ve_a_ficha_e_nao_o_prontuario_clinico()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Recepcao);

        padrao.HasFlag(Permissao.VerFichaPaciente).Should().BeTrue("o balcão precisa do cadastro");
        padrao.HasFlag(Permissao.EditarPaciente).Should().BeTrue("é o balcão que cadastra");

        padrao.HasFlag(Permissao.VerProntuario).Should().BeFalse("evolução é dado de saúde");
        padrao.HasFlag(Permissao.EditarProntuario).Should().BeFalse();
    }

    /// <summary>
    /// QUEM ATENDE precisa dos dois: a ficha para saber quem é, o prontuário para saber o
    /// que foi feito. Separar os bits não pode ter cortado o acesso de quem trata.
    /// </summary>
    [Theory]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Enfermagem)]
    public void Quem_cuida_do_paciente_ve_ficha_e_prontuario(PerfilAcesso perfil)
    {
        var padrao = PerfisAcesso.Padrao(perfil);
        padrao.HasFlag(Permissao.VerFichaPaciente).Should().BeTrue();
        padrao.HasFlag(Permissao.VerProntuario).Should().BeTrue();
    }

    /// <summary>
    /// A ENFERMAGEM checa e não prescreve — a conferência vale porque são duas pessoas.
    /// E não escreve no prontuário: a checagem já é o registro dela, e é assinada.
    /// </summary>
    [Fact]
    public void Enfermagem_checa_mas_nao_prescreve()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Enfermagem);
        padrao.HasFlag(Permissao.ChecarPrescricao).Should().BeTrue();
        padrao.HasFlag(Permissao.Prescrever).Should().BeFalse();
        padrao.HasFlag(Permissao.EditarProntuario).Should().BeFalse();
    }

    /// <summary>
    /// ⚠️ O BIT DA EVOLUÇÃO DE ENFERMAGEM tem dono, e o teste que fixa isso não existia
    /// (parcela 72): <c>grep RegistrarEvolucaoEnfermagem tests/</c> devolvia ZERO desde
    /// que o bit nasceu.
    ///
    /// Enquanto ele for a porta de escrita de dado de saúde, mexer nele em silêncio muda
    /// quem escreve no prontuário — <b>com todos os testes verdes</b>. É a mesma família
    /// do teste que impede uma atualização de tirar em silêncio o que a pessoa fazia
    /// ontem, só que do lado de conceder.
    ///
    /// O médico NÃO recebe: o registro é assinado com nome e COREN, e evolução de
    /// enfermagem escrita por quem tem CRM é registro assinado com o conselho errado —
    /// pior que a lacuna que resolveria.
    /// </summary>
    [Fact]
    public void Registrar_evolucao_de_enfermagem_e_da_ENFERMAGEM()
    {
        PerfisAcesso.Padrao(PerfilAcesso.Enfermagem)
            .HasFlag(Permissao.RegistrarEvolucaoEnfermagem).Should().BeTrue();

        PerfisAcesso.Padrao(PerfilAcesso.Profissional)
            .HasFlag(Permissao.RegistrarEvolucaoEnfermagem).Should().BeFalse();
        PerfisAcesso.Padrao(PerfilAcesso.Recepcao)
            .HasFlag(Permissao.RegistrarEvolucaoEnfermagem).Should().BeFalse();
    }

    /// <summary>
    /// ⚠️ O XY DA PARCELA 72, fixado: a LEITURA do prontuário é compartilhada entre quem
    /// prescreve e quem executa — as duas telas mostram o mesmo conjunto porque os dois
    /// perfis têm o mesmo bit. É o que garante que a última pessoa entre a alergia e a
    /// veia enxergue a alergia.
    ///
    /// O que os SEPARA é a escrita, e cada lado escreve com o conselho dele.
    /// </summary>
    [Fact]
    public void Medico_e_enfermagem_LEEM_o_mesmo_e_ESCREVEM_coisas_diferentes()
    {
        var medico = PerfisAcesso.Padrao(PerfilAcesso.Profissional);
        var enfermagem = PerfisAcesso.Padrao(PerfilAcesso.Enfermagem);

        // XY — a leitura
        foreach (var bit in new[]
                 {
                     Permissao.VerAgenda, Permissao.VerFichaPaciente,
                     Permissao.VerProntuario, Permissao.ColherAssinaturaPaciente
                 })
        {
            medico.HasFlag(bit).Should().BeTrue($"{bit} é leitura compartilhada (XY)");
            enfermagem.HasFlag(bit).Should().BeTrue($"{bit} é leitura compartilhada (XY)");
        }

        // X — só o médico
        medico.HasFlag(Permissao.EditarProntuario).Should().BeTrue();
        medico.HasFlag(Permissao.Prescrever).Should().BeTrue();
        enfermagem.HasFlag(Permissao.EditarProntuario).Should().BeFalse();
        enfermagem.HasFlag(Permissao.Prescrever).Should().BeFalse();

        // Y — só a enfermagem
        enfermagem.HasFlag(Permissao.ChecarPrescricao).Should().BeTrue();
        enfermagem.HasFlag(Permissao.RegistrarEvolucaoEnfermagem).Should().BeTrue();
        medico.HasFlag(Permissao.ChecarPrescricao).Should().BeFalse();
        medico.HasFlag(Permissao.RegistrarEvolucaoEnfermagem).Should().BeFalse();
    }

    /// <summary>
    /// O FINANCEIRO vê a ficha (a tela de inadimplência mostra dívida de gente que ele
    /// precisa conseguir abrir para achar o telefone) e não vê o prontuário: cobrar não
    /// exige saber o diagnóstico de ninguém.
    /// </summary>
    [Fact]
    public void Financeiro_ve_a_ficha_e_nao_o_prontuario()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Financeiro);
        padrao.HasFlag(Permissao.VerFichaPaciente).Should().BeTrue();
        padrao.HasFlag(Permissao.VerProntuario).Should().BeFalse();
    }

    /// <summary>
    /// ANONIMIZAR é só da direção, em todo perfil. Não tem volta: nome, documento e
    /// telefone não voltam. Quem decide atender ao pedido de eliminação do titular é o
    /// controlador, não o balcão.
    /// </summary>
    [Theory]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Enfermagem)]
    [InlineData(PerfilAcesso.Financeiro)]
    [InlineData(PerfilAcesso.Faturista)]
    public void Ninguem_alem_da_direcao_anonimiza(PerfilAcesso perfil)
        => PerfisAcesso.Padrao(perfil).HasFlag(Permissao.AnonimizarDados).Should().BeFalse();

    /// <summary>
    /// GERENCIAR USUÁRIOS e VER AUDITORIA são da direção, em todo perfil. Quem pode mexer
    /// na própria permissão não tem permissão nenhuma; e a trilha que responde "quem fez
    /// isso?" não pode ser lida por quem ela vigia.
    /// </summary>
    [Theory]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Enfermagem)]
    [InlineData(PerfilAcesso.Financeiro)]
    [InlineData(PerfilAcesso.Faturista)]
    public void Ninguem_alem_da_direcao_mexe_em_acesso_nem_le_a_auditoria(PerfilAcesso perfil)
    {
        var padrao = PerfisAcesso.Padrao(perfil);
        padrao.HasFlag(Permissao.GerenciarUsuarios).Should().BeFalse();
        padrao.HasFlag(Permissao.VerAuditoria).Should().BeFalse();
    }

    /// <summary>
    /// A tela de Acessos passou a existir também no FATURAMENTO, e a direção pediu que só
    /// a gerência a alcance. A trava não é o perfil, é o BIT — é assim que o projeto decide
    /// desde a parcela 45, e é o que permite conceder a uma pessoa específica sem promovê-la
    /// a Gerente Geral.
    ///
    /// O teste fixa os dois lados da frase: o Gerente TEM, e é o ÚNICO que tem por padrão.
    /// Sem a segunda metade, acrescentar o bit a um perfil operacional numa parcela futura
    /// abriria o cadastro de usuários para o balcão sem nada falhar — e quem pode mexer em
    /// permissão pode conceder permissão, inclusive a si mesmo.
    /// </summary>
    [Fact]
    public void So_o_gerente_geral_gerencia_acessos_por_padrao()
    {
        PerfisAcesso.Padrao(PerfilAcesso.Gerente)
            .HasFlag(Permissao.GerenciarUsuarios).Should().BeTrue();

        var outros = Enum.GetValues<PerfilAcesso>()
            .Where(p => p != PerfilAcesso.Gerente)
            .Where(p => PerfisAcesso.Padrao(p).HasFlag(Permissao.GerenciarUsuarios))
            .ToList();

        outros.Should().BeEmpty(
            "quem gerencia acesso pode conceder acesso a si mesmo — o bit é da direção");
    }

    /// <summary>
    /// VER o indicador e DEFINIR a meta são bits diferentes (parcela 64).
    ///
    /// Até aqui a tela de Metas pedia <see cref="Permissao.VerIndicadores"/> para criar e
    /// para APAGAR o alvo do mês — o bit de LEITURA do BI autorizando escrita. O efeito é o
    /// mesmo que a parcela 49 corrigiu entre ficha e prontuário: a direção não conseguia
    /// dar acesso de leitura aos números ("o financeiro pode ver") sem entregar junto o
    /// poder de apagar todas as metas do ano.
    ///
    /// O teste falha se alguém voltar a juntá-los — inclusive pelo caminho discreto, que é
    /// acrescentar `DefinirMetas` ao padrão de um perfil que só deveria ler.
    /// </summary>
    [Fact]
    public void Ver_indicadores_nao_da_o_direito_de_definir_meta()
    {
        Permissao.DefinirMetas.Should().NotBe(Permissao.VerIndicadores);

        foreach (var perfil in Enum.GetValues<PerfilAcesso>())
        {
            var padrao = PerfisAcesso.Padrao(perfil);
            if (padrao.HasFlag(Permissao.DefinirMetas))
                perfil.Should().Be(PerfilAcesso.Gerente,
                    "decidir onde a clínica quer chegar é ato da direção — ver o número não é");
        }
    }

    /// <summary>
    /// Nenhum perfil recebe TUDO, salvo o Gerente Geral. É a tradução direta do pedido: se
    /// um perfil operacional acumular todos os bits, a granularidade virou enfeite.
    /// </summary>
    [Theory]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Enfermagem)]
    [InlineData(PerfilAcesso.Financeiro)]
    [InlineData(PerfilAcesso.Faturista)]
    public void Nenhum_perfil_operacional_pode_tudo(PerfilAcesso perfil)
        => PerfisAcesso.Padrao(perfil).Should().NotBe(PerfisAcesso.Todas);

    /// <summary>
    /// Todo bit tem ASSUNTO e EXPLICAÇÃO. É o que a tela de Acessos usa para agrupar e
    /// para dizer a consequência ao lado da caixinha — sem isso, conceder permissão vira
    /// marcar caixinha com nome bonito.
    /// </summary>
    // ===== A DISPENSA DA RODADA BLOQUEANTE (parcela 57) =====
    //
    // A trava de 10 dias é dirigida a QUEM FATURA. A direção entra no faturamento para
    // conferir, e travar a tela dela com uma fila que ela não vai resolver faz a
    // conferência não acontecer.

    [Fact]
    public void Faturista_continua_travado_pela_rodada_de_pendencias()
        => PerfisAcesso.Padrao(PerfilAcesso.Faturista)
            .HasFlag(Permissao.DispensarRodadaPendencias).Should().BeFalse(
                "a rodada existe para quem fatura — é quem tem o número da guia na mão");

    /// <summary>
    /// O bit é uma DISPENSA, e não uma obrigação, exatamente por isto: o Gerente recebe
    /// `Todas`, então um bit com o sentido invertido ("está sujeito à rodada") chegaria
    /// ligado à direção justamente por ela ter tudo — o oposto do que se quer.
    /// </summary>
    [Fact]
    public void Gerente_geral_entra_no_faturamento_sem_ser_travado()
        => PerfisAcesso.Padrao(PerfilAcesso.Gerente)
            .HasFlag(Permissao.DispensarRodadaPendencias).Should().BeTrue();

    /// <summary>
    /// A dispensa é de FATURAMENTO: quem não entra lá não deve recebê-la de brinde, senão
    /// a tela de Acessos mostra uma caixinha ligada que não quer dizer nada para o perfil.
    /// </summary>
    [Theory]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Financeiro)]
    [InlineData(PerfilAcesso.Enfermagem)]
    public void Quem_nao_entra_no_faturamento_nao_recebe_a_dispensa(PerfilAcesso perfil)
        => PerfisAcesso.Padrao(perfil)
            .HasFlag(Permissao.DispensarRodadaPendencias).Should().BeFalse();

    [Fact]
    public void Toda_permissao_tem_assunto_e_explicacao()
    {
        foreach (var p in PerfisAcesso.Individuais)
        {
            PerfisAcesso.Assunto(p).Should().NotBeNullOrWhiteSpace($"{p} precisa de assunto");
            PerfisAcesso.Explicar(p).Should().NotBeNullOrWhiteSpace($"{p} precisa de explicação");
        }
    }

    [Fact]
    public void Gerente_tem_todas_as_permissoes_novas()
    {
        var gerente = PerfisAcesso.Padrao(PerfilAcesso.Gerente);
        foreach (var p in PerfisAcesso.Individuais)
            gerente.HasFlag(p).Should().BeTrue($"o Gerente Geral recebe {p}");
    }

    /// <summary>
    /// A RECEPÇÃO lança atendimento (parcela 46), quando "Novo atendimento" e "Consultas"
    /// saíram do app de faturamento e vieram para o balcão. Sem este bit as duas telas
    /// nasceriam invisíveis para quem passou a ser dono delas — e a recepção já criava
    /// atendimento com guia pelo caminho da agenda desde a parcela 6, então o bit não
    /// concede nada que ela não fizesse.
    /// </summary>
    [Fact]
    public void Recepcao_lanca_atendimento()
        => PerfisAcesso.Padrao(PerfilAcesso.Recepcao)
            .HasFlag(Permissao.LancarAtendimento).Should().BeTrue();

    /// <summary>
    /// Mas a recepção continua SEM escrever no faturamento: lançar o atendimento cria a
    /// guia, e cuidar dela (baixar, estornar, glosar, decidir não faturar) é outro ofício.
    /// </summary>
    [Fact]
    public void Recepcao_lanca_mas_nao_fatura()
    {
        var padrao = PerfisAcesso.Padrao(PerfilAcesso.Recepcao);
        padrao.HasFlag(Permissao.VerFaturamento).Should().BeFalse();
        padrao.HasFlag(Permissao.BaixarGuia).Should().BeFalse();
        padrao.HasFlag(Permissao.MarcarNaoConformidade).Should().BeFalse();
    }

    /// <summary>
    /// Quem não é do faturamento não ganhou nenhum dos bits novos de tabela. Sem isto, um
    /// bit acrescentado ao padrão errado daria à recepção o poder de estornar baixa sem
    /// ninguém notar.
    /// </summary>
    [Theory]
    [InlineData(PerfilAcesso.Recepcao)]
    [InlineData(PerfilAcesso.Profissional)]
    [InlineData(PerfilAcesso.Enfermagem)]
    [InlineData(PerfilAcesso.Financeiro)]
    public void Perfis_de_fora_do_faturamento_nao_escrevem_no_faturamento(PerfilAcesso perfil)
    {
        var padrao = PerfisAcesso.Padrao(perfil);
        padrao.HasFlag(Permissao.BaixarGuia).Should().BeFalse();
        padrao.HasFlag(Permissao.EstornarBaixa).Should().BeFalse();
        padrao.HasFlag(Permissao.RegistrarGlosa).Should().BeFalse();
        padrao.HasFlag(Permissao.GerenciarLotesTiss).Should().BeFalse();
        padrao.HasFlag(Permissao.MarcarNaoConformidade).Should().BeFalse();
        padrao.HasFlag(Permissao.ConfigurarFaturamento).Should().BeFalse();
    }

    /// <summary>
    /// A direção tira o bit de uma pessoa sem mexer no perfil dela — é literalmente o
    /// "liberar ou não" do pedido. NEGADA vence EXTRA: tirar acesso é a decisão que não
    /// pode ser anulada por engano de configuração.
    /// </summary>
    [Fact]
    public void Direcao_tira_o_estorno_de_uma_faturista_especifica()
    {
        var usuario = new UsuarioSistema
        {
            Perfil = PerfilAcesso.Faturista,
            PermissoesNegadas = Permissao.EstornarBaixa
        };

        usuario.Pode(Permissao.BaixarGuia).Should().BeTrue();
        usuario.Pode(Permissao.EstornarBaixa).Should().BeFalse();
    }

    [Fact]
    public void Direcao_libera_a_configuracao_para_uma_faturista_especifica()
    {
        var usuario = new UsuarioSistema
        {
            Perfil = PerfilAcesso.Faturista,
            PermissoesExtras = Permissao.ConfigurarFaturamento
        };

        usuario.Pode(Permissao.ConfigurarFaturamento).Should().BeTrue();
    }

    /// <summary>
    /// Bits novos não podem COLIDIR com os antigos: <see cref="Permissao"/> é gravada como
    /// inteiro, e dois valores com o mesmo bit fariam uma permissão conceder a outra em
    /// silêncio, em bases que já estão em produção.
    /// </summary>
    [Fact]
    public void Cada_permissao_ocupa_um_bit_proprio()
    {
        var vistos = new HashSet<int>();
        foreach (var p in PerfisAcesso.Individuais)
        {
            var valor = (int)p;
            System.Numerics.BitOperations.PopCount((uint)valor).Should().Be(1,
                $"{p} tem de ser um bit único, não uma combinação");
            vistos.Add(valor).Should().BeTrue($"{p} repete um bit já usado");
        }
    }

    /// <summary>
    /// Todo bit tem rótulo em português. Sem isto a tela de Acessos ofereceria à direção
    /// "GerenciarLotesTiss" — o identificador do programador escapando para quem decide
    /// (o defeito da parcela 41), e aqui na tela onde se dá poder a alguém.
    /// </summary>
    [Fact]
    public void Toda_permissao_tem_rotulo_em_portugues()
    {
        foreach (var p in PerfisAcesso.Individuais)
            PerfisAcesso.Rotular(p).Should().NotBe(p.ToString(),
                $"{p} precisa de rótulo em PerfisAcesso.Rotular");
    }

    /// <summary>
    /// Sem sessão autenticada, LIBERA. A regra é da parcela 5 e continua valendo: uma sessão
    /// vazia negando tudo esconderia as telas em qualquer caminho que não passe pelo login
    /// (teste, janela aberta fora do shell), e tela vazia parece defeito, não segurança.
    /// </summary>
    [Fact]
    public void Sem_sessao_autenticada_libera()
    {
        var sessao = new SessaoUsuario();
        sessao.Autenticado.Should().BeFalse();
        sessao.Pode(Permissao.EstornarBaixa).Should().BeTrue();
    }

    /// <summary>
    /// Com sessão, a permissão vale — e <c>Exigir</c> é a barreira que IMPEDE, com uma
    /// frase que a tela mostra ao usuário.
    /// </summary>
    [Fact]
    public void Com_sessao_exigir_bloqueia_e_explica()
    {
        var sessao = new SessaoUsuario();
        sessao.Entrar(new UsuarioSistema
        {
            Id = 7,
            Nome = "Ana",
            Login = "ana",
            Perfil = PerfilAcesso.Faturista,
            PermissoesNegadas = Permissao.EstornarBaixa
        });

        sessao.Pode(Permissao.BaixarGuia).Should().BeTrue();

        var acao = () => sessao.Exigir(Permissao.EstornarBaixa, "estornar a baixa da guia");
        acao.Should().Throw<InvalidOperationException>()
            .WithMessage("*estornar a baixa da guia*");

        // O operador gravado na auditoria passa a ser o LOGIN, não o usuário do Windows —
        // que é o que faltava para a direção conseguir auditar quem fez o quê.
        sessao.Operador.Should().Be("ana");
    }
}
