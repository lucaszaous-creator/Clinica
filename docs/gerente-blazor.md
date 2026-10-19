# O Gerente em Blazor — etapa 6 (out/2026)

> Pedido da direção: *"remover o WPF e implementar full Blazor hybrid"* no Gerente. O passo a
> passo de como se faz uma tela está em `docs/telas-blazor.md`; o método da lista de paridade é o
> do Faturamento (`docs/faturamento-blazor.md`). Aqui fica o que foi decidido para o MÓDULO
> Gerente, tela a tela, e por quê.

## 1. O que mudou de lugar

| Antes (WPF) | Agora (Blazor) | Lote |
|---|---|---|
| `IndicadoresView` + `IndicadoresViewModel` | `Telas/Gestao/IndicadoresDaClinica` + `IndicadoresNaTela` (Application) | 1 |
| `RentabilidadeConvenioView` + `RentabilidadeConvenioViewModel` (+ `LinhaConvenio`) | `Telas/Gestao/RentabilidadePorConvenio` + `RentabilidadeNaTela` | 1 |
| `CustoTransacaoView` + `CustoTransacaoViewModel` (+ `LinhaAdquirente`, `LinhaMesCusto`) | `Telas/Gestao/CustoDeTaxasEImpostos` + `CustoTransacaoNaTela` | 1 |
| `PrecosConvenioView` + `PrecoConvenioWindow` | `PrecosPorConvenio` + `Formularios/Gestao/PrecoConvenioForm` + `PrecosConvenioNaTela` | 2 |
| `CampanhasView` / `OrigensView` | `CampanhasDaClinica` / `OrigensDosPacientes` + `CampanhasNaTela` / `OrigensNaTela` | 2 |
| `AuditoriaView` / `GuardaProntuarioView` | `TrilhaDeAuditoria` / `GuardaDoProntuario` + `TrilhaDeAuditoriaNaTela` / `GuardaDoProntuarioNaTela` | 2 |
| `DocumentosEmitidosView` | `DocumentosEmitidos` + `DocumentosEmitidosNaTela` + `ConferenciaPeloCodigo` | 2 |
| `AcessosView` + `UsuarioWindow` | `AcessosDaClinica` + `UsuarioForm` + `SenhaProvisoriaForm` + `AcessosNaTela` | 2 |
| `ConfiguracoesView` + `CamposPersonalizadosWindow` + `ModelosTermoWindow` | `ConfiguracoesDaClinica` + `CamposPersonalizadosForm` + `ModelosTermoForm` + `ConfiguracoesDaClinicaNaTela` | 2 |
| `ImportacaoPacientesView` | `ImportacaoDePacientes` + `ImportacaoDePacientesNaTela` | 2 |

**O módulo Gerente não tem mais tela WPF.** O único caso WPF que sobra no `CriarTela` é
`PrecosParticularView`, que é do SHELL e é publicada também pela Recepção — migrá-la é
trabalho da Recepção, senão a mesma chave teria duas telas. `PeriodoGerencial` (WPF) saiu:
a definição única do período é `ResumoFaturamentoNaTela.Periodos/Intervalo`.

Peças comuns que mudaram no lote 2: `IHostDaTela` ganhou `ListarMonitoresAsync` e
`MostrarExemploNaTelaDoPacienteAsync` (com implementação padrão; reais no `HostDaTelaWpf` e
na prévia); o `GravarArquivoAsync` do host WPF cria a pasta de destino (a exportação do
prontuário grava numa subpasta datada); a Application ganhou `ICopiaDeSeguranca` e
`IPendenciasDeConclusao` (adaptadores na Infrastructure, `PortasDasConfiguracoes.cs`); o
texto do NPS foi para `MensagensDeContato.PesquisaDeSatisfacao` (o `Whatsapp` do shell só
o chama).

As chaves de navegação (`indicadores`, `rentabilidade-convenio`, `custo-transacao`) **não
mudaram**: chave é contrato entre módulos (a Mesa da direção e o composto "Produção e margem"
navegam por elas).

Peça comum nova: **`Componentes/GraficoLinha`** — o `GraficoLinha` do WPF em SVG com tokens
(valor nulo é buraco, eixo ancorado no zero, número do SVG em cultura invariante). Indicadores e
Custo o usam; a próxima tela com série também.

## 2. Paridade, tela a tela

### 2.1 Indicadores (`IndicadoresDaClinica`)

| WPF | Blazor |
|---|---|
| Período (Este mês · Últimos 3 meses · Este ano) — `PeriodoGerencial` | Segmentado com as MESMAS opções e a MESMA conta (`ResumoFaturamentoNaTela.Periodos/Intervalo`, a cópia que já estava na Application) |
| Botão Atualizar | Atualizar + **F5** |
| Exportar CSV (sem barreira) | Exportar CSV + **Ctrl+P**, com as duas barreiras (`VerIndicadores`, o bit do item) — CSV é saída de dado |
| Cabeçalhos do CSV (`Tipo; Nome; Atendidos; Faltas; Cancelados; No-show %; Ocupacao %; Prontuario %`), "—" sem base | Iguais, byte a byte (`IndicadoresNaTela.Csv`, com teste) |
| KPIs Atendidos (com variação), Faltas no-show (variação em PONTOS), Ocupação, Pacientes atendidos, NPS (+ detalhe) | Os mesmos cinco, com as mesmas regras de "—" e de cor da variação |
| `Agendados`, `Encaixes`, `HorasOcupadas` calculados e **não mostrados** | Viraram as notas dos cartões ("de N horário(s) agendado(s)", "N encaixe(s)", "N h ocupadas") — campo calculado sem leitor |
| Jornada ("Ocupação medida sobre 8 h…") | Linha abaixo dos cartões |
| Produtividade (8 colunas) | Mesma tabela, mesmas colunas |
| Barras de ocupação normalizadas pela MAIOR | Iguais (`IndicadoresNaTela.Barras`) |
| Gráficos de linha Atendimentos e No-show | `GraficoLinha` ×2, com buraco no mês sem horário fechado |
| Lista mensal abaixo do gráfico | Tabela mês a mês |
| `EstadoDaTela` (carregando / não verificado) | Esqueleto / `FalhaDeLeitura` + `Diagnostico` |
| Contador de geração | Contador de geração, publicação depois do último `await` |
| — | Volta à aba relê (`AoVoltarAsync`) |

**Diferenças (correções):** a coluna No-show da produtividade e da lista mensal mostrava
`0%` quando não havia horário fechado (o KPI e o CSV já diziam "—"); agora as três dizem "—".
"1 pontos" virou "1 ponto". Sem série (um mês só) a tela diz por que não há gráfico em vez de
desenhar um ponto solto.

### 2.2 Rentabilidade por convênio (`RentabilidadePorConvenio`)

| WPF | Blazor |
|---|---|
| Janela 1 · 3 · 6 · 12 meses (padrão 6), mês corrente inteiro | Iguais (`RentabilidadeNaTela.Janelas/Intervalo`, com teste) |
| Exportar (sem barreira) · Atualizar | Exportar CSV (**Ctrl+P**, duas barreiras `VerFinanceiro`, o bit do item) · Atualizar (**F5**) |
| CSV `Convenio; Guias; Bruto; Retido; Liquido; Liquido/guia; Prazo medio; Pendencias` | Igual |
| KPIs Guias (+período), Recebido, Retido (+%), Líquido, Glosa | Iguais |
| Resumo ("N convênio(s) · M com guia SEM receita") e "rende mais · rende menos" | Iguais |
| Linha: convênio, guias, bruto, retido ("—" se zero), líquido, por guia, prazo, barra, pendências em vermelho | Iguais; a barra some abaixo de 1200 px (o líquido ao lado já diz o tamanho) |
| Texto explicativo do líquido por guia e do prazo | Rodapé da lista |
| Erro em faixa vermelha | `FalhaDeLeitura` (terceiro estado) |

**Diferença (correção):** com EMPATE no líquido por guia o serviço devolve a mesma operadora
nas duas pontas, e a tela escrevia "rende mais: X · rende menos: X". A frase agora só aparece
quando as duas pontas diferem (com teste).

### 2.3 Custo de taxas e impostos (`CustoDeTaxasEImpostos`)

| WPF | Blazor |
|---|---|
| Janela 3 · 6 · 12 · 24 meses (padrão 12) | Iguais (`CustoTransacaoNaTela.Janelas`) |
| Exportar (sem barreira) · Atualizar | Exportar CSV (**Ctrl+P**, duas barreiras `VerFinanceiro`) · Atualizar (**F5**) |
| CSV `Tipo; Nome; Bruto; Taxa; Imposto; Liquido; Deducoes %` (meses e adquirentes) | Igual |
| KPIs Bruto (+período), Maquininha (+%), Imposto (+%), Líquido (+ deduções %) | Iguais |
| Gráficos Deduções % (buraco no mês sem faturamento) e Deduções R$ | `GraficoLinha` ×2 |
| "Adquirente que mais descontou" | Igual |
| Por adquirente: barra, valor, efetiva × tabela, vermelho acima do TETO | Igual (`Comparacao`/`AcimaDaTabela`, com teste) |
| Mês a mês (6 colunas, "—" sem base) | Igual |

Sem diferença de comportamento.


## 2b. Lote 2 — paridade e diferenças, tela a tela

Cada frente leu o ViewModel WPF inteiro e devolveu a lista completa (comando, filtro,
coluna, exportação, permissão, atalho, mensagem). Abaixo, o essencial e **toda** diferença.
Em todas: contador de geração, terceiro estado (`FalhaDeLeitura` + `Diagnostico`), F5 relê,
voltar à aba relê em silêncio, diálogos HTML (cancelar = nada gravado).

**Preços por convênio** — Novo/Editar/Excluir com as duas barreiras (`EditarFinanceiro`),
filtro de convênio, "só o que vale hoje", Limpar, resumo e vazio que dizem o filtro, o mesmo
`LeituraDeValores.TentarLerDecimal`. Correções: as **observações** voltavam nulas e eram
apagadas na edição (lugar 3); convênio **desativado** sumia do combo e travava o Salvar →
fica "(desativado)"; preço apagado em outra máquina abre dizendo isso; o selo diz POR QUE
está fora de vigência (inativo / ainda não vale / encerrada — o `Situacao` era calculado sem
leitor). Mantido por paridade: "1250.00" é lido como 125000 pela `LeituraDeValores`.

**Campanhas** — filtros Tipo/Situação/Janela (mesma conta de datas), Gerar NPS de ontem
(`GerenciarCampanhas`), resultado da rodada na tela, WhatsApp (só com telefone e contato
aberto; registra o envio só se abriu — senão copia o link e DIZ que não registrou), Nota
(0–10 + comentário), Respondeu, Dispensar (motivo obrigatório). Correções: a permissão é
conferida ANTES de abrir o WhatsApp (o WPF abria e só depois recusava o registro); guarda de
leitura na própria tela; um estado vazio em vez de dois.
**De onde vêm os pacientes** — período (padrão Este ano), tabela Origem/Na base/%/Estrearam,
selo "colher no cadastro" no Não perguntado, top-10 de quem indica. Sem diferença.

**Auditoria** — filtros (prontuário de, "só quem ABRIU", ação por prefixo, operador,
detalhe, De/Até, limite 100/300/1000), Consultar/Limpar, aviso de LIMITE ATINGIDO, contagens
por ação e por operador, tabela, CSV com as mesmas colunas (+ Ctrl+P; botão apagado sem
`VerAuditoria` — o WPF não tinha a metade visível).
**Guarda do prontuário** — resumo da clínica sob demanda, situação do paciente, contagem por
natureza, "quem abriu este prontuário" (100), exportar um paciente / a clínica inteira
(confirmação de perigo, destino perguntado ANTES, subpasta datada, UTF-8 com BOM) com os
MESMOS registros de acesso (`ExportacaoTitular`, `RegistrarExportacaoDaClinicaAsync`).
Correções: o botão de exportar acendia com `VerAuditoria` e o clique exigia `VerProntuario`
(as duas barreiras discordavam) → agora exige os dois; trocar de paciente LIMPA antes de
ler (o WPF deixava o histórico e o botão do paciente anterior durante a carga).

**Documentos emitidos** — período, cinco cartões (Emitidos, Assinados com e-CPF,
Cancelados, No ar, Mais emitido), por quem assina, cancelados com motivo, tabela, Conferir
pelo código (com o MESMO registro de acesso `Documento`) e o filtro por acesso
(`Efetivas`). Sem botão de emitir nem CSV, como no WPF. Acréscimos: selos "Cancelada" e
"e-CPF" (o "assinado" era calculado e não mostrado); motivo vazio diz "(sem motivo
registrado)".

**Acessos** — lista (nome, situação, login, perfil, profissional, último acesso), Novo /
Editar / Redefinir senha / Excluir, todos com as duas barreiras (`GerenciarUsuarios`) e
`Exigir` no instante do clique; formulário com perfil, profissional, CPF de assinatura,
senha, troca obrigatória, ativo e as permissões agrupadas com a consequência
(`PerfisAcesso.Explicar`) e a procedência. Senha em campo mascarado com repetição (o WPF
usava o diálogo de texto, que mostrava a senha e cortava espaços). Correções: profissional
**inativo** sumia do combo e o Salvar desfazia o vínculo; gravar o CPF não mexe mais nas
habilitações do profissional; bits que não valem para Psicologia ficam apagados com "não
vale aqui"; Excluir apagado com explicação para quem já entrou; a tela dizia que o
Faturamento não tem login (tem, desde a parcela 45).

**Configurações** — as seis abas (Clínica, Atendimento, Operação, Faturamento,
Integrações, Backup) com todos os campos e botões, cada bloco com o seu Salvar e as mesmas
barreiras; a guia no agendamento grava só quando muda (+ confirmação lembrando de atualizar
as máquinas antes); publicação S3 (Salvar, Testar, Enviar exemplo + Copiar/Abrir endereço),
SMTP (Salvar, e-mail de teste), tela do paciente (monitores e Testar pelo host), backup
(pasta, política, copiar agora, salvar cópia em…, conferir), campos personalizados e termos
(com o "Criar os termos do BSV" que substitui). Segredos nunca voltam à tela (vazio mantém o
gravado). Correções: salvar um bloco relia TUDO e apagava o digitado nas outras abas; a
ordem do campo personalizado zerava; a tela do paciente desligada era perdida num Salvar; o
texto do termo tinha limite de 4.000 que travava o TCLE aprovado; "Numero" → "Número".
`SalvarPrestador`/`SalvarFaturamento` do VM não tinham botão (código morto): não portados.

**Importar pacientes** — os três arquivos (CSV, pacote ZIP do Smart Clinic, ZIP do acervo),
informação do arquivo, mapeamento com sugestão, convênios ("definir depois" primeiro),
prévia linha a linha sem gravar, Importar com confirmação de perigo, progresso, resultado e
conferência — `EditarPaciente` nas duas barreiras. Correções: nada fica clicável enquanto
lê, prevê ou grava (o WPF só cobria a lista); a leitura roda fora da thread da tela; o
catálogo de convênios é relido a cada arquivo; escolher outro arquivo apaga a conferência
anterior. Atalhos novos: F5 refaz a prévia, Ctrl+S importa (mesmas barreiras).

## 2c. A navegação do Gerente reorganizada (out/2026)

Sete entradas, uma pergunta cada, na ordem do uso da direção. **Nenhuma chave mudou** (contrato
entre módulos); mudaram rótulos e agrupamento.

| Antes | Agora | Por quê |
|---|---|---|
| Mesa da direção | Mesa da direção | — |
| Dinheiro (Resultado, 90 dias, Metas) | **Dinheiro** (Resultado, Próximos 90 dias, Margem por sessão, Por convênio, Custo de receber) | tudo o que é do bit `VerFinanceiro` responde "quanto entra, sai e sobra" |
| Produção e margem (Margem, Indicadores, Tendências, Produção, Rentabilidade) | **Desempenho** (Indicadores, Tendências, Produção, Metas) | metas medem sessões e ocupação, não só dinheiro |
| Pacientes e relacionamento | **Pacientes e campanhas** (De onde vêm, Campanhas, Comissão de recuperação) | rótulo diz o que tem dentro |
| Preços e regras (Convênios, Particular, Custo) | **Tabela de preços** (Convênios, Particular) | custo de taxas é dinheiro, não preço |
| Conformidade e acessos (Auditoria, Documentos, Guarda, Acessos) | **Conformidade** (Auditoria, Documentos emitidos, Guarda) — sem `Requer` | quem tem só `VerDocumentos` alcança a aba dele |
| Configurações · Importar pacientes (soltos) | **Administração** (Configurações, Acessos, Importar pacientes) | o que se mexe raramente e muda a clínica inteira |

Junto: atalhos da Mesa levam às abas novas ("Abrir a agenda", "Ver a rentabilidade"), as telas
ganharam links entre si casando com a organização, e a tela de Configurações ganhou leiaute de
blocos (título + descrição + grade alinhada + Salvar do bloco).

## 3. Como se garantiu que nada se perdeu

1. A lista de paridade acima, montada lendo cada ViewModel INTEIRO.
2. O que as telas afirmam subiu para a Application, puro e com teste:
   lote 1 com 19 casos (`IndicadoresNaTelaTests`, `RentabilidadeECustoNaTelaTests`); lote 2
   com `PrecosConvenioNaTelaTests`, `CampanhasNaTelaTests`, `OrigensNaTelaTests`,
   `TrilhaEGuardaNaTelaTests`, `DocumentosEmitidosNaTelaTests`, `AcessosNaTelaTests`,
   `ConfiguracoesDaClinicaNaTelaTests`, `CopiaDeSegurancaDaClinicaTests` e
   `ImportacaoDePacientesNaTelaTests`.
3. `TelasBlazorAbremTests` abre as doze telas com as sementes e clica cada botão.
4. Sementes próprias da prévia (uma por frente; a do lote 1 é `IndicadoresDaDirecao.cs` (NPS
   respondido, EVA em par, receita de guia com retenção e prazo, taxa de tabela abaixo do efetivo).
5. Fotos em 1024 e 1366 — elas acharam os valores cortados nos cinco cartões em 1024
   ("R$ 9.55…"), corrigidos para 3 + 2 colunas abaixo de 1180 px.

## 4. UX · DX · CX

- 🧑‍💻 **UX** — o Gerente inteiro tem a linguagem visual da Mesa, da Margem e das
  Tendências, num navegador só por janela (sem flash entre as telas); os atalhos chegam; a
  falha de leitura tem cara de falha; formulário é Modal, não janela do sistema.
- 🛠️ **DX** — o módulo não tem mais XAML de tela; toda regra que a tela afirma mora na
  Application com teste; o gráfico de linha virou componente comum; a prévia e o
  `TelasBlazorAbremTests` cobrem as doze telas sozinhos.
- 🤝 **CX** — nenhuma capacidade a menos; o CSV que a direção leva à planilha mantém as colunas;
  a exportação passou a ter a segunda barreira.
