# Quem pode o quê — o padrão de cada perfil

> Escrito na **parcela 49**, quando a direção apontou o buraco: *"não adianta ter
> permissão granular se todo perfil nasce podendo tudo"*.
>
> Esta página é a decisão de negócio; o código que a implementa é
> `PerfisAcesso.Padrao` em `src/Clinica.Domain/Entities/Acesso.cs`, e cada linha da tabela
> tem um teste em `tests/Clinica.Tests/PermissoesFaturamentoTests.cs`. **Mudar o padrão sem
> mudar o teste é mudar uma decisão de negócio sem que ninguém veja.**

## A causa que não era o padrão

Os padrões nunca deram literalmente tudo. Dois deles davam demais, e por um motivo que não
era escolha — era o **bit sobrecarregado**:

| Até a parcela 48 | Significava |
|---|---|
| `VerProntuario` | abrir a ficha do paciente **E** ler a evolução clínica |
| `EditarProntuario` | cadastrar paciente **E** escrever no prontuário |

Não havia como conceder um sem o outro. A granularidade existia na tela e não no domínio —
e é por isso que a recepcionista, que precisa do cadastro para marcar horário, lia a
evolução inteira de todo mundo. A parcela 49 separou:

| Bit | O que é |
|---|---|
| `VerFichaPaciente` | cadastro, contato, convênio, carteirinha, autorizações, consentimentos, documentos emitidos |
| `EditarPaciente` | cadastrar/editar paciente, colher consentimento, registrar a senha do convênio, emitir declaração e termo |
| `VerDocumentos` | abrir a **central de documentos** — mostra só as folhas que os outros acessos da pessoa alcançam |
| `VerProntuario` | evolução, EVA, mapa corporal, anexos, medidas, escalas, alergias — **dado de saúde** |
| `EditarProntuario` | escrever no prontuário clínico |

O corte é o da **LGPD**: dado de contato de um lado, dado sensível (art. 5º, II) do outro.

## As três perguntas que decidiram cada linha

1. **A pessoa precisa disto para fazer o trabalho dela?** Não é "pode dar sem risco" — é
   "sem isto, ela para". Bit que ninguém usa vira bit que ninguém revisa.
2. **O ato apaga o trabalho de outra pessoa, ou some com uma cobrança do sistema?** Se sim,
   é da chefia.
3. **É dado de saúde?** Se sim, só quem cuida do paciente.

> **`MovimentarFila` (parcela 61)**: mover a fila do dia — chegada, chamada, entrada,
> voltar o cartão — é um ato mais estreito do que "marcar e remarcar", e é o gesto central
> do quadro de quem atende. O profissional o recebe SEM ganhar a agenda do balcão junto. A
> autorização do ato é UMA nos dois quadros: `EditarAgenda` **ou** `MovimentarFila` — quem
> movia a fila ontem continua movendo hoje.

## O padrão

| Permissão | Recepção | Profissional | Enfermagem | Financeiro | Faturista | Gerente |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Ver agenda e fila | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Marcar e remarcar | ✅ | — | — | — | — | ✅ |
| **Chamar e mover a fila do dia** | ✅ | ✅ | — | — | — | ✅ |
| **Ver ficha do paciente** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Cadastrar e editar paciente** | ✅ | — | — | — | ✅ | ✅ |
| **Abrir a central de documentos** | ✅ | ✅ | — | ✅ | — | ✅ |
| **Ver prontuário clínico** | — | ✅ | ✅ | — | — | ✅ |
| **Escrever no prontuário** | — | ✅ | — | — | — | ✅ |
| Prescrever e assinar | — | ✅ | — | — | — | ✅ |
| Checar execução | — | — | ✅ | — | — | ✅ |
| Ver financeiro | — | — | — | ✅ | — | ✅ |
| Lançar no caixa | — | — | — | ✅ | — | ✅ |
| Ver faturamento | — | — | — | — | ✅ | ✅ |
| Dar baixa em guia | — | — | — | — | ✅ | ✅ |
| **Estornar baixa** | — | — | — | — | — | ✅ |
| Registrar glosa | — | — | — | — | ✅ | ✅ |
| Gerar e enviar lote TISS | — | — | — | — | ✅ | ✅ |
| Lançar atendimento | ✅ | — | — | — | ✅ | ✅ |
| **Decidir não faturar (NC)** | — | — | — | — | — | ✅ |
| Configurar o faturamento | — | — | — | — | — | ✅ |
| **Ver indicadores** (e os relatórios) | — | — | — | — | — | ✅ |
| Gerenciar campanhas | ✅ | — | — | — | — | ✅ |
| Cadastrar equipe | — | — | — | — | — | ✅ |
| Gerenciar usuários | — | — | — | — | — | ✅ |
| Ver auditoria | — | — | — | — | — | ✅ |
| **Anonimizar dados (LGPD)** | — | — | — | — | — | ✅ |

## As decisões que a direção pediu, uma a uma

**A recepção não lê o prontuário clínico.** Foi o primeiro exemplo dado. Telefone e
convênio são dado de contato; evolução, EVA e alergia são dado de saúde, e não é preciso
lê-los para marcar um horário. Numa clínica pequena em que a recepcionista também digita a
evolução que o profissional dita, a direção concede `EditarProntuario` **àquela pessoa** em
Acessos — que é exatamente o controle pedido.

**O faturista não reabre uma não conformidade.** Foi o segundo exemplo. A NC é a única
permissão que faz uma pendência sumir do painel **sem a guia ter sido faturada**: quem a
tem pode zerar o alarme do sistema justamente sobre o trabalho que ele existe para cobrar.
Reabrir é da mesma família.

**O faturista não vê os relatórios gerenciais.** Foi o terceiro. Eles passaram a exigir
`VerIndicadores`, que é o mesmo bit do BI do Gerente. Operar o dia é uma coisa; ler o
desempenho da clínica é outra.

**E o estorno de baixa saiu junto.** Não foi pedido nominalmente, mas cai na segunda
pergunta: desfazer apaga o trabalho de outra pessoa e desfaz o elo com a conciliação do
Financeiro. Errar a baixa é acidente; estornar é decisão.

**O faturista não abre horário na agenda** (parcela 58). A direção pediu depois de o
sistema materializar a pendência do 2º código como agendamento: horário aberto do lado do
faturamento aparece na fila do balcão e na agenda de quem atende. Ele **continua vendo** a
agenda — conferir o dia é parte de faturá-lo.

**A recepção não abre os documentos clínicos** (parcela 59). Foi a reclamação seguinte: *"a
recepcionista está conseguindo ver os documentos"*. A correção tem duas metades, e
**nenhuma das duas bastava sozinha**:

1. A central de documentos ganhou **porta própria** (`VerDocumentos`). Antes ela pedia
   `VerFichaPaciente`, que todo perfil de balcão tem.
2. **Cada folha declara o acesso que exige** — porque as dez não são a mesma coisa:

   | Folha | Quem VÊ | Quem EMITE |
   |---|---|---|
   | Receituário · Atestado · Solicitação de exames | `VerProntuario` | `Prescrever` |
   | Relatório de evolução · Ficha de anamnese | `VerProntuario` | `VerProntuario` |
   | Declaração de comparecimento · Termo de consentimento | `VerFichaPaciente` | `EditarPaciente` |
   | Recibo · Orçamento | `VerFinanceiro` | `EditarFinanceiro` |
   | Fechamento do período | `VerIndicadores` | `VerIndicadores` |

   Fechar só a porta obrigaria a direção a escolher entre a recepcionista lendo o
   relatório de evolução de todo mundo e a recepcionista **sem o recibo e a declaração que
   ela entrega dez vezes por dia** — o bit sobrecarregado da parcela 49 reaparecendo numa
   tela.

⚠️ **A regra vale nas TRÊS portas**, não só na central: o Receituário da Recepção e a aba
Documentos da ficha do paciente emitem os mesmos papéis. Por isso a decisão mora no
**catálogo** (`FolhaCatalogo.PermissaoVer` / `PermissaoEmitir`) e não em cada tela — regra
de acesso escrita numa porta só é o defeito recorrente do projeto, com o agravante de
**parecer** coberta.

O efeito para o balcão, na prática: some o item **Receituário** da sidebar, some o botão
"Novo documento…" da ficha, e na central sobram declaração de comparecimento e termo de
consentimento. Recibo e orçamento continuam com o Financeiro, que é quem recebe.

## O que isso NÃO significa

Nenhum item acima é proibição absoluta. **A direção concede qualquer bit a uma pessoa
específica em Acessos, num clique, sem mexer no perfil dos outros** — e a decisão fica
gravada na auditoria. O padrão é o que ela não precisa decidir todo dia.

⚠️ **Esta mudança TIRA capacidade de quem já a usava, e é de propósito.** A regra do
projeto ("não tire função de quem a tinha ontem") vale para efeito colateral de
atualização; aqui a remoção **é** o pedido. Na primeira semana depois de subir, espere
pedidos pontuais — e a resposta a cada um é uma caixinha marcada em Acessos, não uma
versão nova.

## A tela

`Gerente → Acessos → editar usuário` mostra as permissões **agrupadas por assunto**
(Agenda e balcão · Paciente (cadastro) · Clínico (dado sensível) · Financeiro ·
Faturamento · Direção), com:

- a **consequência** escrita ao lado de cada caixinha, e não só o rótulo — *"Estornar baixa
  de guia"* parece inofensivo até alguém dizer que estornar apaga o trabalho de outra
  pessoa;
- a **procedência** de cada decisão: *padrão do perfil*, *LIBERADA para esta pessoa* ou
  *TIRADA desta pessoa*;
- uma linha de resumo dizendo **quanto esta pessoa foge do padrão da função dela**, e para
  que lado.

É o que responde, ao auditar, "isto é o padrão ou alguém mexeu?" — a pergunta que a lista
de vinte e quatro caixinhas soltas não respondia.
