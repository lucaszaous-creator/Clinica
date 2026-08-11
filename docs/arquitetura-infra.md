# Infraestrutura e features — o sistema inteiro num lugar só

> Retrato do que existe hoje na `main` (`3dec6ae`, ago/2026). Este documento é **descritivo**:
> ele conta o que está construído, não o que se planeja. Quando um número aparece aqui, ele
> foi contado no repositório — não estimado.
>
> Para as **decisões** por trás de cada peça, o lugar é o `CLAUDE.md`; para o **leiaute**, o
> `README.md`; para **quem pode o quê**, `docs/permissoes-por-perfil.md`; para a **LGPD**,
> `docs/conformidade-lgpd.md`.

---

## 1. O que o produto é

Sistema de **faturamento e gestão** para uma clínica de acupuntura, em .NET 8 / WPF, rodando
em produção. O coração do produto é impedir que o **2º código/guia** — obtido +24h depois do
atendimento — seja esquecido. Dali ele cresceu para cobrir o ciclo TISS completo, a recepção,
o consultório, o financeiro e a direção.

**Faturamento ≠ recebíveis.** "Baixa" significa que a secretária efetivou a guia no sistema do
convênio. Não há campos de dinheiro no faturamento; dinheiro é assunto do módulo Financeiro.

---

## 2. Números

| | |
|---|---|
| Projetos na solução | **14** (13 em `src/`, 1 em `tests/`) |
| Executáveis | **5** |
| Módulos da suíte | **4** |
| Serviços de aplicação | **80** |
| Tabelas (DbSet) | **59** |
| Migrations | **52** |
| Permissões (bits) | **25**, em **6** perfis |
| Testes | **1393**, em 106 arquivos |
| Linhas de C# | ~210.000 |
| Linhas de XAML | ~28.500 |
| Checagens estáticas locais | **32** (`verificar-suite.py`) |

---

## 3. As camadas

```
Clinica.Domain            entidades · enums · motor de regras de convênio · escalas
      ▲
Clinica.Application       80 serviços de caso de uso · IClinicaRepositorio · assinatura
      ▲
Clinica.Infrastructure    EF Core + Npgsql · ClinicaDbContext · migrations · S3 · backup
      ▲
      ├── Clinica.Desktop         ← o app de FATURAMENTO (design system próprio)
      └── Clinica.Desktop.Shell   ← o shell da SUÍTE (design system próprio)
              ▲
              ├── Clinica.Modulo.Recepcao
              ├── Clinica.Modulo.Clinico
              ├── Clinica.Modulo.Financeiro
              └── Clinica.Modulo.Gerente
```

**Ponto único de acesso a dados:** `Clinica.Application/Abstracoes/IClinicaRepositorio`, com uma
única implementação (`ClinicaRepositorio`). Nenhuma tela fala com o `DbContext`.

⚠️ **`Clinica.Desktop` NÃO referencia `Clinica.Desktop.Shell`**, e isso é permanente. Os dois
declaram tipos no namespace `Clinica.Desktop.Controls`, e as referências ficariam ambíguas.
O preço é **dois design systems** e algum código duplicado; foi o que cancelou a Fase 4
(transformar o faturamento em módulo da suíte). O que os liga é o **banco** — e
`SessaoUsuario`, que subiu para o `Domain` justamente para compartilhar a *decisão* de acesso
sem compartilhar a janela.

---

## 4. Os cinco executáveis

Cada `.exe` é uma **casca** que escolhe uma lista de módulos. O shell tem o design system, a
janela genérica, o contrato `IModuloApp`, o login e a navegação.

| Executável | Carrega | Para quem | Canal Velopack |
|---|---|---|---|
| `Clinica.Desktop` | — (app próprio) | Faturista | `win` (padrão, **nunca muda**) |
| `Clinica.Recepcao` | Recepção | Balcão | canal próprio |
| `Clinica.Clinico` | Clínico | Médico / fisioterapeuta | canal próprio |
| `Clinica.Financeiro` | Financeiro | Administrativo / caixa | canal próprio |
| `Clinica.Gerente` | **os quatro** | Direção | canal próprio |

O Gerente carrega tudo — é por isso que a sidebar dele é o caso extremo, e foi o que motivou o
**rail + flyout** (56 px) com sub-abas: 46 itens viraram 24.

**A sidebar é agrupada por TEMA, não por módulo:** `GESTÃO · PACIENTE · FINANCEIRO ·
INTELIGÊNCIA`. Um item declara em qual grupo aparece (`Grupo`) e quem sabe construir a tela
(`ModuloNome`) — são duas coisas diferentes.

---

## 5. Como os módulos se falam

**Pelo BANCO.** Não há fila de mensagens, evento nem sincronização: o que um grava, o outro lê,
e a ligação é sempre uma **chave estrangeira**.

```
Recepção ──► Faturamento     FechamentoSessaoService → AgendaService.ConfirmarPresencaAsync
                             → AtendimentoService.LancarAsync (ponto único)
                             cria Atendimento + CodigoFaturamento pelas regras do convênio

Faturamento ──► Financeiro   FinanceiroService.GuiasSemLancamentoAsync (guia baixada sem receita)
                             → LancarReceitaDaGuiaAsync grava CodigoFaturamentoId
                             ← ReceitaGlosadaService: glosa cancela a receita e a guia VOLTA
                               sozinha para a conciliação

Financeiro ──► Gerente       RentabilidadeConvenioService · CustoTransacaoService
                             PainelDirecaoService (não calcula nada: cada número vem do dono)

Gerente ──► todos            cada alerta LEVA à tela dona, por NavegacaoSuite + ChavesSuite

Consultório ──► Gerente      ConsultorioService → AssuntoDirecao.ProntuarioEmAberto
Consultório ──► Recepção     escalas entram no relatório de evolução e no prontuário
```

**A dependência tem um sentido só:** o faturamento continua funcionando sem saber que o
financeiro existe. É por isso que as pontes (`FechamentoSessaoService`, `ReceitaGlosadaService`)
moram **fora** dos serviços compartilhados — dar efeito colateral novo ao `AtendimentoService`
mudaria o comportamento de um app em produção.

`CircuitoCompletoTests` testa isso de **ponta a ponta**, e não por trechos: elo partido aqui não
vira erro, vira **lista vazia** ou **número zerado**, que é indistinguível de um dia fraco.

---

## 6. Infraestrutura externa

| Peça | O que é | Como é configurada |
|---|---|---|
| **Neon (PostgreSQL)** | o banco, único e compartilhado pelos 5 apps | env `ConnectionStrings__Clinica` → `ConexaoStore` (DPAPI em `%APPDATA%\ClinicaFaturamento`) → tela de Setup |
| **Velopack** | auto-update dos 5 apps, um canal por app | tag `vX.Y.Z` dispara `release.yml` |
| **Armazenamento S3-compatível** | publica a receita assinada para o QR do farmacêutico | endpoint + domínio em Configurações; `ForcePathStyle`; validado ao vivo contra Cloudflare R2 |
| **ICP-Brasil (e-CPF A1/A3)** | assinatura qualificada PKCS#7 SHA-256 | certificado local, CPF lido do OID `2.16.76.1.3.1` |
| **SafeID** | certificado em nuvem (alternativa ao token) | OAuth com escuta em loopback |
| **Carimbo do tempo RFC 3161** | opcional | Configurações → Operação |
| **Validador do ITI (saúde)** | `assinaturadigital.iti.gov.br` — o QR leva até lá | nada a configurar; é público |
| **WhatsApp** | confirmação, cobrança, entrega | link `wa.me`, um clique por paciente |
| **Webcam (DirectShow)** | retrato do paciente | AForge; foto partida em miniatura + tabela |

**Migrations são aplicadas na ABERTURA do app** (`MigrateAsync`), inclusive no faturamento em
produção. Duas consequências que valem como regra:

1. **Nada de índice único novo** — a criação falharia se a base já tivesse duplicata, e quem não
   abriria seria o sistema que fatura. Regras como "CPF não se repete" moram na **escrita**,
   onde podem explicar.
2. **Migration no faturamento é só ADITIVA** (checagem 18). Coluna que guarda dado de saúde não
   se renomeia nem se remove: o dado tem de sobreviver 20 anos.

---

## 7. As features, por módulo

### 7.1 Faturamento — `Clinica.Desktop`

O app que fatura a clínica hoje. **Saiu do congelamento na parcela 45**, e o cuidado continua:
ele roda em produção e não tem quem o teste antes do usuário.

- **Painel de pendências** com semáforo — o 2º código com data prevista +24h, consultas a
  renovar, glosas com prazo de recurso, carteirinhas vencidas
- **Rodada bloqueante de pendências** — passado o prazo (padrão 10 dias, por guia), a abertura
  trava numa janela que só fecha com uma decisão por guia: baixa ou **não conformidade**.
  `DispensarRodadaPendencias` isenta quem entra só para conferir
- **Baixa de guia** (quatro portas: tela, lote, rodada, fila do Gerente) com validação do
  **formato do número por convênio** (`RegraNumeroGuia`) — pega o "O" digitado no lugar do zero
- **Não conformidade** com justificativa, aba própria, reabertura manual ou automática quando o
  paciente volta
- **Glosas** — registro, recurso com data-limite, recuperação
- **Ciclo TISS 4.01** — lote → XML → validação → envio → retorno (importa o demonstrativo da
  operadora) → glosa → recurso; guia exportada não entra em outro lote
- **Radar de glosas** (`PrevencaoGlosaService`) na exportação: carteirinha vencida, duplicidade,
  taxa histórica
- **Consulta de guias** com filtro por paciente, número, data, status, convênio, **modalidade e
  especialidade**
- **Agenda** (leitura para o faturista desde a parcela 58), ficha do paciente, relatórios,
  **Acessos** (usuários e permissões), Configurações
- **Guia em PDF no leiaute ANS** e capa de faturamento

### 7.2 Recepção — `Clinica.Modulo.Recepcao`

- **Painel do balcão** — o dia, com as guias pendentes **dos pacientes de hoje**
- **Agenda em linha do tempo** — régua de horas, uma coluna por profissional (ou por dia, na
  semana), o vão livre clicável abrindo o formulário já na hora e na coluna; encaixe, série
  (o pacote de dez), bloqueio de férias/feriado, comprovante, folha do dia em PDF
- **Fila / check-in em kanban** de cinco raias (Aguardando · Na recepção · Chamado · Em
  atendimento · Finalizado), com **arrastar e soltar**, tempo de espera e o recado de chamada
  que atravessa do consultório
- **Elegibilidade ANTES** — carteirinha, cota do convênio, pacote, dívida vencida e glosa em
  aberto, no agendamento e no check-in
- **Lista de espera** com candidatos filtrados pelo horário que vagou
- **Fechamento da sessão** — quatro fatos do mesmo ato: a guia nasce, o pacote debita, o insumo
  sai do estoque e o dinheiro entra no caixa (proposta confirmada, nunca automática)
- **Novo atendimento avulso** com **prévia** do que a regra vai gerar antes de gerar
- **Consultas de convênio** (renovação), **autorização de sessões** (a senha da operadora)
- **Pacientes** — lista de largura inteira → ficha em abas (visão geral, convênio, prontuário,
  documentos, relacionamento, LGPD), foto pela webcam
- **Prontuário**, **Prescrições**, **Central de documentos**, **Sala de infusão**
- **Retorno de pacientes** (recall) e **rodada de confirmação** de amanhã
- **LGPD do titular** — exportação e anonimização

### 7.3 Consultório — `Clinica.Modulo.Clinico`

- **Meu dia** em kanban, com "chamar próximo" carimbando o recado para o balcão
- **Minha semana** — sete dias numa consulta só
- **Sessões sem evolução** — a pendência do lado clínico: o que eu atendi e ainda não escrevi
- **Meus números** — produtividade e completude de prontuário, só de quem está logado, sem
  comparar colegas
- **Evolução da dor** (EVA), **mapa corporal** com protocolos, **anexos**, **busca no prontuário**
- **Avaliações** — PHQ-9, GAD-7, Oswestry, Katz, FINDRISC, EVA (escalas em código, copiadas na
  aplicação)
- **Medidas seriadas** — peso, altura, cintura, PA, glicemia, HbA1c, IMC derivado
- **Lista de problemas** com CID opcional e alerta de alergia
- **Prescrições** — receita, atestado, comparecimento, pedido de exame
- **Prescrição de infusão** + **sala de infusão** com checagem de enfermagem (✓ com hora,
  "rodela" com justificativa, retificação por linha nova)
- **Conferência de alergia** na emissão de receita (palavra inteira, piso de 4 caracteres)

### 7.4 Financeiro — `Clinica.Modulo.Financeiro`

- **Caixa** — lançamentos, recibo, **fechamento de caixa** (só espécie, divergência exige
  justificativa escrita)
- **Conciliação** — guia baixada sem receita, com filtro por convênio (por operadora, não por
  família), preço proposto com procedência, glosa marcada
- **Contas a pagar/receber** com três datas (competência · pagamento · vencimento) e
  recorrência idempotente
- **Fluxo de caixa** — realizado e previsto nunca somados
- **Recebíveis de cartão** — conciliação por **depósito**, não por venda
- **Taxas e impostos** — taxa de cartão com vigência, **regime tributário** por tributo com base
  de cálculo, **retenção na fonte por convênio**
- **Quem me deve** (inadimplência) com aging e mensagem de cobrança
- **Repasses** — apuração por profissional, com vigência e trava do período
- **Estoque** — saldo = soma dos movimentos, validade no lote, **acerto de inventário**
- **Pacotes / sessões** — venda copia o catálogo, situação calculada
- **Resultado do mês** (regime de caixa, e a tela diz isso) e **Produção**

### 7.5 Direção — `Clinica.Modulo.Gerente`

- **Painel da direção** — cada bloco falha sozinho, cada alerta leva ao assunto
- **Metas** e **orçamento** (teto de despesa) — ausência ≠ zero
- **Relatórios / BI** — indicadores, ocupação, produtividade, NPS
- **Rentabilidade por convênio** (líquido **por guia**) e **custo de transação** (taxa efetiva
  contra a de tabela)
- **Tabela de preço por convênio** — a mais específica ganha, com vigência
- **Campanhas** — confirmação, NPS, recall (uma entidade só, com chave de idempotência)
- **Quem parou de vir** (retenção)
- **Acessos** — usuários e permissões, agrupadas por assunto, com a consequência e a
  procedência de cada uma
- **Auditoria** — a trilha somente-leitura de quem fez o quê
- **Guarda do prontuário**, **conformidade LGPD**, **backup com política e rotação**
- **Faturamento (TISS)** em leitura, **Configurações** globais

---

## 8. O motor de regras

Uma classe `Regra<Convenio>` por fluxograma, todas implementando `IRegraConvenio`:

```
Gerar(paciente, atendimento, contexto) → ResultadoFaturamento
                                          códigos · datas previstas · categoria/semáforo
```

`RegistroRegras` resolve a regra pelo convênio. Cinco fluxogramas modelados
(Unimed padrão, Unimed intercâmbio, Petrobras, Amil, consulta avulsa) mais `RegraGenerica` +
`ConfiguracaoRegraGenerica` para convênios personalizados criados em runtime.

**O motor é PURO** — `Gerar` não grava nada. Foi isso que permitiu a **prévia** do Novo
atendimento: mostrar o que a regra vai gerar antes de gerar, com o teste
`Previa_promete_exatamente_o_que_o_lancamento_entrega` fixando que os dois não divergem.

O **número da guia tem forma, e ela é do convênio** — `RegraNumeroGuia` + `ConvenioCadastro.
FormatoNumeroGuia`. A regra mora no **domínio** e é aplicada em `FaturamentoService.
DarBaixaAsync`, porque a baixa tem **quatro portas**: validar na tela cobre uma e deixa três
passando.

---

## 9. Acesso e auditoria

**6 perfis**, **25 bits**. A permissão efetiva é resolvida na **leitura**:
`padrão do perfil + extras − negadas` (negada vence extra). Corrigir o padrão de um perfil
alcança quem já está cadastrado.

**Toda permissão tem DUAS barreiras, e as duas são obrigatórias:**

- `IsEnabled` no botão — a metade **visível**, que explica
- `SessaoUsuario.Atual.Exigir(...)` no comando — a que **impede**

Só desabilitar é enfeite: um atalho de teclado passa direto.

**Quem assina a ação é quem fez LOGIN** (`SessaoUsuario.Atual.Operador`), nunca
`Environment.UserName` — no balcão duas pessoas dividem a máquina. Os **cinco apps pedem
login**.

**A auditoria grava no MESMO `SaveChanges` do ato.** Ação que possa acontecer sem a linha
correspondente é ação sem trilha. Desde a parcela 52 há também a trilha de **leitura**
(`AcessoProntuarioService`): quem abriu o prontuário de quem, quando e **por qual porta** — o
acesso indevido clássico numa clínica é leitura, e é exatamente o caso que a permissão
granular não cobre.

---

## 10. Conformidade — o que o código garante

O compromisso completo está em `docs/conformidade-lgpd.md`. O que a construção não pode violar:

1. **Registro clínico NÃO SE APAGA.** Não há `Remove()` nem `Remover*Async` para evolução,
   anexo, avaliação, medida, documento ou prescrição. Cancela-se com **motivo obrigatório**, e
   a linha fica. `ConformidadeProntuarioTests` falha se um desses métodos voltar à interface.
2. **Alterar guarda o que dizia antes** — `VersaoEvolucao`.
3. **Migration em tabela clínica é aditiva.**
4. **Tela que abre prontuário registra o acesso**, na troca de paciente (nunca a cada
   `CarregarAsync` — as telas recarregam a cada tecla).
5. **Permissão separa dado sensível de cadastral** — `VerFichaPaciente`/`EditarPaciente` são
   contato; `VerProntuario`/`EditarProntuario` são saúde (art. 5º, II).
6. **Guarda de 20 anos** contada do **último** registro de qualquer natureza, `const` e não
   configuração — prazo legal editável numa tela alguém baixa para 5 anos.
7. **O sistema não elimina nada** ao vencer o prazo: o prontuário fica *elegível*, e a decisão é
   da clínica.
8. **Não prometa garantia que o código não dá.** Sem LTV, o rodapé diz PAdES-B; sem certificação
   SBIS/CFM, o sistema não substitui o papel. **Garantia aparente é pior que ausência de
   garantia.**

---

## 11. As redes de segurança

O `Clinica.Desktop` e toda a suíte multi-exe **só compilam no Windows** (`net8.0-windows`). Isso
não é desculpa para empurrar sem compilar — há quatro redes, e as três locais rodam antes de
todo push.

| Rede | Cobre | Não cobre |
|---|---|---|
| `dotnet build` + `dotnet test` | Domain, Application, Infrastructure e os 1393 testes | nada das telas |
| `tools/compilar-sombra.py` | **o C# dos 10 projetos WPF**, faturamento incluído | XAML |
| `tools/verificar-suite.py` | XAML, pack URIs, chaves do design system, migration destrutiva, e mais 32 checagens | semântica de C# |
| **CI** (`verificar.yml` + `build-exe.yml`, runner Windows) | o compilador de marcação de verdade (`MC*`) e o empacotamento | — |

`compilar-sombra.py` recompila os mesmos `.cs` num projeto `net8.0` comum contra as *reference
assemblies* do WPF, e substitui o compilador de marcação por um gerador próprio de `.g.cs`.

**As checagens nasceram de defeitos reais**, quase todos achados pelo cliente em produção. As
mais caras:

- **25** — sobreposição posta como irmã desaba a tela inteira
- **27** — `SharedSizeGroup` com valor inválido derruba a tela em runtime
- **29/30** — `EstadoDaTela` que fica visível para sempre por cima da tela funcionando
- **31** — `SharedSizeGroup` sem escopo não alinha nada
- **32** — `WrapPanel` medido com largura infinita nunca dobra a linha

⚠️ **Toda checagem que procura uma marca no texto tem de tirar os comentários antes** — a 31
nasceu cega porque o próprio comentário que explicava a regra satisfazia a busca.

---

## 12. Publicação

```
push na main / PR para a main   →  verificar.yml  (Linux: as três redes)
                                   build-exe.yml  (Windows: compila os 5 apps)

workflow_dispatch (build-exe)   →  5 .exe PORTÁTEIS, em qualquer branch
                                   sem vpk pack → não se auto-atualizam
                                   ⚠️ aponte ConnectionStrings__Clinica para uma branch do Neon
                                      e NUNCA use a tela de Setup no build de teste

tag vX.Y.Z (ou Actions→Release) →  release.yml
                                   Velopack empacota os 5 apps, um canal por app
                                   publica na mesma release; os instalados se auto-atualizam
```

O **faturamento fica no canal padrão `win` e nunca muda** — é o app que já está instalado na
máquina de quem fatura.

---

## 13. O defeito recorrente do projeto

Vale registrar aqui porque é o que mais custou, e ele tem quatro variantes:

1. **Dado gravado sem leitor** — a previsão de recebimento, o `EventoAuditoria`, o consumo de
   pacote
2. **Serviço testado sem chamador em produção** — o custo por sessão, a devolução de sessão ao
   pacote, a sugestão de quem chamar
3. **Capacidade com a porta no módulo de quem não a usa** — o atendimento avulso no faturamento,
   a cota do convênio, as prescrições fora do consultório
4. **Saída sem tela** — o `SnackbarService` chamado 143 vezes numa suíte que nunca renderizou o
   host

Nas quatro, `dotnet test` fica verde e o CI fica verde. **Antes de dar uma feature por pronta:
procure o chamador em produção, procure o leitor em OUTRO módulo, e conte quantos itens de menu
o módulo tem.** Sidebar curta demais para o que o app faz é sintoma, não simplicidade.
