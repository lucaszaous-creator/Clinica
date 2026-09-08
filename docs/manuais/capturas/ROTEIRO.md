# Roteiro de captura das telas reais

> **O que falta para os manuais ficarem prontos.** As figuras de hoje são reproduções em
> HTML/CSS — fiéis nos rótulos e nas cores, mas **não são fotos do sistema**, e cada uma
> sai com um selo laranja dizendo isso. Este roteiro é o que troca as treze por telas de
> verdade, sem ninguém precisar editar o manual.

## Por que isto não pôde ser feito automaticamente

Os cinco aplicativos são **WPF** e têm `TargetFramework` `net8.0-windows` — eles só
compilam e só rodam no **Windows**. O ambiente onde os manuais são gerados é Linux, e o
SDK de Linux nem traz o `Microsoft.NET.Sdk.WindowsDesktop`, então não há como **nem
construir** os `.exe` aqui, quanto mais abri-los para fotografar. É a mesma razão pela
qual o `compilar-sombra.py` existe no projeto.

Capturar exige uma máquina Windows com o sistema rodando. São ~30 minutos.

## ⛔ Regra número 1: não capture com paciente de verdade

Um manual vai por e-mail, é impresso e circula. Print com nome, CPF, carteirinha ou
evolução de paciente real é **vazamento de dado de saúde** — exatamente o que o sistema
existe para evitar. Duas saídas seguras:

| Saída | Como |
|---|---|
| **Recomendada — build portátil contra um banco de teste** | Siga [`docs/testar-sem-publicar.md`](../../testar-sem-publicar.md): Actions → *Build EXE (Windows)* → baixe os `.exe`, e aponte `ConnectionStrings__Clinica` para uma branch de teste do banco. O `.exe` do CI **não se auto-atualiza e não mexe no app instalado**. |
| **Alternativa — dados de demonstração** | Numa base de teste, cadastre 4 ou 5 pacientes fictícios e alguns horários no dia. É o que as figuras atuais mostram. |

⚠️ **Nunca use a tela de Setup no build de teste** — ela grava a conexão em `%APPDATA%`.
A variável de ambiente vence a configuração salva e não grava nada.

## Preparo (uma vez)

1. **Escala do Windows em 100%** (Configurações → Sistema → Tela). Em 125% a letra sai
   maior e as capturas ficam com tamanhos diferentes entre si.
2. **Janela no tamanho padrão** — não maximize. A janela da suíte abre em **1280 × 690**,
   que é a proporção usada nos desenhos atuais; manter isso faz as figuras caberem na
   página sem esticar.
3. **Tema claro** (é o único que o sistema tem).
4. Feche notificações e barras de outros programas que apareçam por cima.

## Como capturar

- **`Alt` + `PrintScreen`** copia **só a janela ativa** (é o que queremos — sem a área de
  trabalho em volta). Depois cole no **Paint** e salve como **PNG**.
- Ou **`Win` + `Shift` + `S`** → *Captura de janela* → salva direto.
- Formato: **PNG** (aceita `.jpg` também). Largura ideal: **1280 px**; menos de 1000 px
  sai borrado no papel.

## A lista — treze arquivos

Salve cada um com **exatamente** este nome, dentro desta pasta (`docs/manuais/capturas/`).

### Manual da Recepção — aplicativo **Recepção**

| Arquivo | Tela | Em que estado |
|---|---|---|
| `recepcao-01-login.png` | Janela de entrada | Com um usuário digitado e a senha em pontinhos |
| `recepcao-02-inicio.png` | **GESTÃO › Início** | Um dia com movimento: contadores preenchidos e a ocupação por profissional visível |
| `recepcao-03-agenda-dia.png` | **GESTÃO › Agenda › aba Dia** | O melhor print do conjunto: um dia com pacientes em estados **diferentes** — um concluído, um chamado, um no local, um marcado e um cancelado |
| `recepcao-04-agenda-grade.png` | **GESTÃO › Agenda › aba Grade** | Duas ou três colunas de profissional, alguns horários ocupados e vãos livres à vista |
| `recepcao-05-lancar.png` | **ATENDIMENTO › Lançar e marcar › aba Lançar** | Com paciente já escolhido, uma modalidade marcada e a **prévia da guia** desenhada na coluna da direita |
| `recepcao-06-fechar-sessao.png` | Janela **Fechar a sessão** | Aberta pelo “⋯ › Fechar sessão” de um paciente **com pacote**, para aparecerem os três blocos (pacote, insumos, dinheiro) |

### Manual do Consultório — aplicativo **Consultório**

| Arquivo | Tela | Em que estado |
|---|---|---|
| `consultorio-01-meu-dia.png` | **GESTÃO › Minha agenda › aba Hoje** | Alguns pacientes, com a coluna **Prontuário** mostrando “escrito” e “pendente” |
| `consultorio-02-tela-do-paciente.png` | Tela do paciente, seção **Atendimento** | Com a folha da sessão preenchida (EVA antes/depois e texto), o crachá do paciente no alto e o rail à esquerda |

### Manual do Financeiro — aplicativo **Financeiro**

| Arquivo | Tela | Em que estado |
|---|---|---|
| `financeiro-01-caixa.png` | **FINANCEIRO › Caixa › aba Caixa** | Um dia com entradas e saídas, formas de pagamento variadas e pelo menos uma linha **Prevista** |

### Manual do Gerente — aplicativo **Gerente Geral**

| Arquivo | Tela | Em que estado |
|---|---|---|
| `gerente-01-painel-direcao.png` | **GESTÃO › Painel › aba Direção** | Com os cartões do mês preenchidos e o bloco **“O que exige ação hoje”** com pelo menos dois alertas |

### Manual do Faturamento — aplicativo **Faturamento**

| Arquivo | Tela | Em que estado |
|---|---|---|
| `faturamento-01-pendencias.png` | **Painel › Pendências** | Com guias em atrasos diferentes, para aparecerem as cores do semáforo |
| `faturamento-02-baixa-guia.png` | Janela **Dar baixa na guia** | Com um número já digitado e a crítica do formato visível abaixo do campo |
| `faturamento-03-rodada.png` | Janela **Rodar pendências** | A janela obrigatória, com duas ou três linhas — uma com número, outra com justificativa |

## Depois de salvar os arquivos

```bash
node tools/gerar-manuais.js
```

O gerador troca sozinho cada desenho pela foto correspondente, **tira o selo laranja** e
diz no fim quantas figuras já têm captura real:

```
manual-recepcao.html  →  pdf/manual-recepcao.pdf  ·  6/6 figura(s) com captura real
```

Não é preciso editar nenhum HTML. Falta uma foto? Aquela figura continua como ilustração,
com o selo — as outras já saem reais.

## Se a tela estiver diferente do que o roteiro descreve

Capture assim mesmo e **avise**: quer dizer que o manual está descrevendo algo que mudou,
e o texto precisa ser corrigido junto. É a razão de o texto falar de regras estáveis
(a guia nasce quando o atendimento entra, prontuário não se apaga, previsto ≠ realizado) e
citar leiaute só onde ele é o assunto.

## Extras — se sobrar disposição

Estas não têm encaixe hoje; mandando as fotos, viram figuras novas no manual do módulo:

| Sugestão | Onde | Por que ajuda |
|---|---|---|
| Ficha do paciente com as abas | Recepção → **PACIENTE › Pacientes** → abrir alguém | O capítulo 8 descreve seis abas sem mostrar nenhuma |
| Janela do horário | Recepção → Grade → clicar num paciente | Onde ficam remarcar, reabrir e comprovante |
| Central de documentos | Recepção → **PACIENTE › Documentos** | Os cartões de cada folha |
| Colher assinatura do termo | Recepção → ficha → *Colher assinatura…* | É a tela que o paciente vê |
| Sala de infusão / folha de execução | Consultório ou Recepção → **Sala de infusão** | O capítulo da enfermagem é todo em texto |
| Conciliação | Financeiro → **Recebimentos › Conciliação** | A ponte faturamento × dinheiro |
| Acessos, com as permissões abertas | Gerente → **Conformidade e acessos › Acessos** → editar alguém | É a tela mais importante da direção |
| Guias TISS — lotes | Faturamento → **Guias TISS** | O fecho do ciclo da guia |
