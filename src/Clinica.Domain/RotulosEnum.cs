using System.Text;
using Clinica.Domain.Entities;
using Clinica.Domain.Regras;

namespace Clinica.Domain;

/// <summary>
/// O nome que cada valor de enum tem NA TELA (parcela 41).
///
/// O defeito que isto corrige
/// --------------------------
/// Meia dúzia de <c>ComboBox</c> da suíte recebia a lista de um enum direto, sem
/// <c>DisplayMemberPath</c> nem template — e o WPF, sem nada melhor para fazer, chama
/// <c>ToString()</c>. O resultado é o identificador do programador escapando para a
/// recepcionista: <b>"PedidoExame"</b>, "RelatorioEvolucao", "CartaoCredito",
/// "PercentualDaReceita", "CreditoAVista".
///
/// Não é só feio. "PedidoExame" num seletor ensina que o sistema é de outra pessoa; e
/// pior, alguns viram armadilha de leitura — "Previsto" aparece em dois enums diferentes
/// (<see cref="TipoLancamento"/> e <see cref="StatusLancamento"/>) significando coisas
/// diferentes, e sem rótulo nada distingue os dois na tela.
///
/// Por que aqui, e não um rotulador por tela
/// -----------------------------------------
/// Porque já havia rotulador — <see cref="TipoDocumentoInfo.Rotular"/> existe desde a
/// parcela 3 e a tela de documento clínico não o usava. Espalhar a decisão faz exatamente
/// isso: alguém escreve o rótulo certo num lugar e a próxima tela nasce sem ele. Aqui é um
/// ponto só, e o conversor de XAML aponta para ele.
///
/// A regra do fallback
/// -------------------
/// Enum sem rótulo declarado cai em <see cref="Humanizar"/>, que quebra o PascalCase e
/// devolve "Pedido exame". Isso é PIOR do que um rótulo escrito à mão e MELHOR do que o
/// identificador cru — e é de propósito que ele não some: o dia em que um enum novo
/// aparecer na tela sem rótulo, a frase estranha é o que faz alguém vir escrevê-lo.
/// </summary>
public static class RotulosEnum
{
    /// <summary>O rótulo de qualquer valor de enum, para a tela.</summary>
    public static string De(object? valor) => valor switch
    {
        null => string.Empty,

        // ---- os que já tinham rotulador e não eram usados pela tela ----
        TipoDocumentoClinico t => TipoDocumentoInfo.Rotular(t),
        ModalidadeCartao m => TaxaCartao.Rotular(m),
        PerfilAcesso p => PerfisAcesso.Rotular(p),
        Permissao p => PerfisAcesso.Rotular(p),

        // Forma do número da guia por convênio (parcela 45). Os identificadores
        // ("SomenteNumeros") escapariam para a tela de Configurações sem isto.
        FormatoNumeroGuia g => g switch
        {
            FormatoNumeroGuia.SomenteNumeros => "Somente números",
            FormatoNumeroGuia.Alfanumerico => "Letras e números",
            _ => "Sem validação"
        },

        // ---- dinheiro ----
        FormaPagamento f => f switch
        {
            FormaPagamento.Dinheiro => "Dinheiro",
            FormaPagamento.Pix => "PIX",
            FormaPagamento.CartaoDebito => "Cartão de débito",
            FormaPagamento.CartaoCredito => "Cartão de crédito",
            FormaPagamento.Transferencia => "Transferência",
            FormaPagamento.Boleto => "Boleto",
            FormaPagamento.Convenio => "Convênio",
            _ => "Outro"
        },

        // "Entrada" e "Saída" são o que a clínica fala. O acento não vem de graça: o
        // identificador é `Saida`, e sem rótulo a tela escreveria "Saida".
        TipoLancamento t => t == TipoLancamento.Entrada ? "Entrada" : "Saída",

        StatusLancamento s => s switch
        {
            StatusLancamento.Previsto => "Previsto",
            StatusLancamento.Realizado => "Realizado",
            _ => "Cancelado"
        },

        BaseRepasse b => b switch
        {
            BaseRepasse.PercentualDaReceita => "Percentual da receita",
            _ => "Valor por atendimento"
        },

        PeriodicidadeRecorrencia p => p switch
        {
            PeriodicidadeRecorrencia.Semanal => "Semanal",
            PeriodicidadeRecorrencia.Quinzenal => "Quinzenal",
            PeriodicidadeRecorrencia.Mensal => "Mensal",
            PeriodicidadeRecorrencia.Bimestral => "Bimestral",
            PeriodicidadeRecorrencia.Trimestral => "Trimestral",
            PeriodicidadeRecorrencia.Semestral => "Semestral",
            _ => "Anual"
        },

        // ---- estoque e pacote ----
        TipoMovimentoEstoque t => t switch
        {
            TipoMovimentoEstoque.Entrada => "Entrada",
            TipoMovimentoEstoque.Saida => "Saída",
            TipoMovimentoEstoque.Perda => "Perda",
            // Acerto de inventário (parcela 30): a contagem física não bateu.
            _ => "Acerto de inventário"
        },

        TipoPacote t => t switch
        {
            TipoPacote.Sessoes => "Pacote de sessões",
            TipoPacote.Plano => "Plano",
            _ => "Voucher"
        },

        // ---- clínico ----
        TecnicaPonto t => t switch
        {
            TecnicaPonto.Agulha => "Agulha",
            TecnicaPonto.Eletroacupuntura => "Eletroacupuntura",
            TecnicaPonto.Moxa => "Moxa",
            TecnicaPonto.Ventosa => "Ventosa",
            TecnicaPonto.Auriculoterapia => "Auriculoterapia",
            TecnicaPonto.Laser => "Laser",
            _ => "Outra"
        },

        Sexo s => s == Sexo.Masculino ? "Masculino" : "Feminino",

        // ---- prescrição interna e checagem de enfermagem (parcela 42) ----
        //
        // A via é o campo mais lido da folha de infusão e o mais abreviado no dia a dia:
        // ninguém na sala fala "endovenosa", fala "EV". A sigla vem junto do nome porque o
        // seletor é do prescritor (que escreve por extenso) e a folha é da técnica (que lê
        // a sigla) — deixar só um dos dois obrigaria metade da equipe a traduzir.
        ViaAdministracao v => v switch
        {
            ViaAdministracao.Endovenosa => "Endovenosa (EV)",
            ViaAdministracao.Intramuscular => "Intramuscular (IM)",
            ViaAdministracao.Subcutanea => "Subcutânea (SC)",
            ViaAdministracao.Intradermica => "Intradérmica (ID)",
            ViaAdministracao.Oral => "Oral (VO)",
            ViaAdministracao.Inalatoria => "Inalatória",
            ViaAdministracao.Topica => "Tópica",
            _ => "Outra"
        },

        SituacaoPrescricao s => s switch
        {
            SituacaoPrescricao.Rascunho => "Rascunho",
            SituacaoPrescricao.Assinada => "Assinada",
            SituacaoPrescricao.Encerrada => "Encerrada",
            _ => "Cancelada"
        },

        SituacaoChecagem s => s == SituacaoChecagem.Realizado ? "Realizado" : "Não realizado",

        SituacaoItemPrescricao s => s switch
        {
            SituacaoItemPrescricao.Pendente => "Aguardando",
            SituacaoItemPrescricao.Realizado => "Realizado",
            SituacaoItemPrescricao.NaoRealizado => "Não realizado",
            _ => "Suspenso"
        },

        PapelAssinatura p => p == PapelAssinatura.Prescritor ? "Prescritor" : "Enfermagem",

        // O rótulo do NÍVEL de assinatura é frase inteira e mora na entidade
        // (AssinaturaDocumento.RotuloDoNivel), porque é o que vai IMPRESSO no rodapé do
        // PDF. Aqui fica a versão curta, do seletor.
        TipoAssinatura t => t switch
        {
            TipoAssinatura.IcpBrasil => "Certificado ICP-Brasil",
            TipoAssinatura.EletronicaAvancada => "Eletrônica no sistema",
            _ => "À mão, na via impressa"
        },

        // ---- faturamento (lido pelo app congelado; rótulo só melhora a tela) ----
        TipoCodigo t => t switch
        {
            TipoCodigo.Consulta => "Consulta",
            TipoCodigo.Acupuntura => "Acupuntura",
            TipoCodigo.Eletroacupuntura => "Eletroacupuntura",
            TipoCodigo.Bsv => "BSV",
            _ => "Consulta de especialidade"
        },

        ModalidadeAtendimento m => CatalogoModalidades.Nome(m.ToString()),
        Convenio c => CatalogoConvenios.Nome(c),

        // "ClinicaDaDor" é o único cujo humanizador não salva: ele devolveria
        // "Clinica da dor", sem o acento, e o nome da especialidade sai impresso em
        // relatório que a direção manda para fora.
        Especialidade e => e switch
        {
            Especialidade.Psiquiatria => "Psiquiatria",
            Especialidade.Geriatria => "Geriatria",
            Especialidade.Ginecologia => "Ginecologia",
            Especialidade.Acupuntura => "Acupuntura",
            Especialidade.ClinicaDaDor => "Clínica da dor",
            _ => "Endocrinologia"
        },

        // Não é enum: devolve o próprio texto, para o conversor poder ser usado sem medo.
        var outro when outro.GetType().IsEnum is false => outro.ToString() ?? string.Empty,

        var enumSemRotulo => Humanizar(enumSemRotulo.ToString()!)
    };

    /// <summary>
    /// Quebra o PascalCase em frase: <c>PedidoExame</c> vira <c>"Pedido exame"</c>.
    ///
    /// Só a PRIMEIRA palavra fica com maiúscula — "Pedido Exame" pareceria título, e o
    /// item de um seletor não é título. Siglas coladas ("BSV", "PIX") sobrevivem porque
    /// letras maiúsculas em sequência não são quebradas.
    /// </summary>
    public static string Humanizar(string identificador)
    {
        if (string.IsNullOrWhiteSpace(identificador)) return string.Empty;

        var texto = new StringBuilder(identificador.Length + 8);

        for (var i = 0; i < identificador.Length; i++)
        {
            var c = identificador[i];

            // Espaço antes de uma maiúscula que começa palavra nova: ou o anterior é
            // minúscula ("PedidoE"), ou o próximo é minúscula ("BSVCodigo" → "BSV Codigo").
            var comecaPalavra = i > 0 && char.IsUpper(c)
                && (char.IsLower(identificador[i - 1])
                    || (i + 1 < identificador.Length && char.IsLower(identificador[i + 1])));

            if (comecaPalavra) texto.Append(' ');
            texto.Append(i > 0 && !comecaPalavra ? c : char.ToUpperInvariant(c));
        }

        var frase = texto.ToString();

        // Da segunda palavra em diante, minúscula — salvo sigla (tudo maiúsculo).
        var partes = frase.Split(' ');
        for (var i = 1; i < partes.Length; i++)
        {
            if (partes[i].Length > 1 && partes[i].All(char.IsUpper)) continue;
            partes[i] = partes[i].ToLowerInvariant();
        }

        return string.Join(' ', partes);
    }
}
