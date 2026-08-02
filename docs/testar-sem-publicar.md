# Testar sem publicar para a clínica

> Como rodar o Faturamento, o Consultório e o resto da suíte com o código novo **sem** que
> a clínica receba nada — e sem encostar no banco de produção.

A pergunta que este documento responde é a que aparece toda vez que uma parcela fica
pronta: *"os apps instalados se atualizam sozinhos na próxima abertura; como eu testo
antes disso?"*

## A regra que dá a segurança: quem publica é a TAG

Só isso já resolve metade do problema:

| Ação | Gera artefato de teste? | A clínica recebe? |
|---|---|---|
| `git push` numa branch de trabalho | não (a menos que você peça — abaixo) | **não** |
| Pull request para a `main` | sim (`build-exe.yml`) | **não** |
| Merge na `main` | sim (`build-exe.yml`) | **não** |
| `git tag v1.2.3` / `clinico-v1.0.0` … | — | **SIM** (`release.yml`) |

`release.yml` só dispara em **tag** (`v*`, `recepcao-v*`, `financeiro-v*`, `gerente-v*`,
`clinico-v*`) ou no botão "Run workflow" dele. Nem push nem merge publicam.

E, como cada app tem sua própria tag e seu próprio **canal** Velopack, publicar
`clinico-v1.0.0` **não republica o faturamento**: o app instalado só enxerga o
`releases.<canal>.json` do canal com que foi empacotado.

## 1. Gerar o build de teste (sem PR, sem merge)

`build-exe.yml` tem `workflow_dispatch`, então dá para rodá-lo **em qualquer branch**:

1. GitHub → **Actions** → **Build EXE (Windows)** → **Run workflow**
2. Em *Use workflow from*, escolha a branch (ex.: `claude/doctor-therapist-module-a1rdfb`)
3. Ao fim, baixe os artefatos: `Clinica-Faturamento-win-x64`,
   `Clinica-Consultorio-win-x64`, `Clinica-Recepcao-win-x64`, …

### Por que esse .exe é inofensivo para quem já tem o sistema

O CI publica com `dotnet publish --self-contained -p:PublishSingleFile=true` e **não passa
pelo `vpk pack`**. Sem o empacotamento do Velopack, o app se considera **não instalado** —
os dois atualizadores conferem isso na primeira linha:

```csharp
// AtualizadorSuite.AtualizarNaAberturaAsync  e  UpdateService (faturamento)
if (!mgr.IsInstalled)
    return false;   // exe portátil (artefato do CI)
```

Consequências práticas: o exe de teste **não se auto-atualiza**, **não registra canal**,
**não mexe na instalação existente** e roda de qualquer pasta, lado a lado com o app que a
clínica usa.

> Também dá para gerar localmente, sem CI, num Windows com o .NET 8 SDK:
> `publish-exe.bat` (faturamento) ou `dotnet publish src\Clinica.Clinico\... -o publish-clinico`.

## 2. A armadilha de verdade não é o .exe — é o BANCO

O executável portátil é inócuo. O banco não:

- abrir **qualquer** app novo apontando para a produção **aplica as migrations
  pendentes** (todos chamam `MigrateAsync` na abertura);
- e todo paciente, sessão e avaliação que você criar testando vira dado **real**.

A migration da parcela 36 é puramente aditiva e não quebraria nada — mas "não quebra" é
diferente de "pode fazer". **Teste sempre contra um banco separado.**

### O banco de teste

A clínica usa **Neon**, que faz *branch* de banco: uma cópia instantânea da produção, com
os dados reais, isolada e descartável. Crie uma branch (ex.: `teste-parcela-36`) e use a
connection string dela. Serve qualquer PostgreSQL vazio também — só não terá dado para
olhar.

### Como apontar para ele SEM tocar na configuração da máquina

Use a variável de ambiente. Ela **vence** a configuração salva e — o ponto que importa —
**não grava nada**:

```csharp
// ShellBootstrap.ObterConnectionString() e App.ObterConexao() do faturamento
var env = Environment.GetEnvironmentVariable("ConnectionStrings__Clinica");
if (!string.IsNullOrWhiteSpace(env)) return env;   // não passa pelo ConexaoStore.Salvar
```

No PowerShell, na mesma janela em que você vai abrir o app:

```powershell
$env:ConnectionStrings__Clinica = "Host=ep-xxx-teste.neon.tech;Database=clinica;Username=...;Password=...;SSL Mode=Require"
.\publish-clinico\Clinica.Clinico.exe
```

> ⚠️ **Nunca use a tela de Setup no build de teste.** Diferente da variável de ambiente,
> ela **grava** em `%APPDATA%\ClinicaSemDor` (e o faturamento em
> `%APPDATA%\ClinicaFaturamento`) — que é a mesma pasta que os apps **instalados** leem.
> Salvar a conexão de teste ali apontaria a produção da máquina para o banco de teste.
> A variável de ambiente existe exatamente para não precisar disso.

## 3. O que conferir nesta parcela

### No Faturamento — que nada mudou

O objetivo aqui é **não encontrar diferença**. Abra o exe de teste do faturamento e o
instalado (contra bancos diferentes) e compare:

- **Lançar atendimento → modalidade Consulta → seletor de Especialidade**: tem de listar
  as seis de sempre (Psiquiatria, Geriatria, Ginecologia, Acupuntura, Clínica da Dor,
  Endocrinologia). **Neurocirurgia não pode aparecer** — foi o vazamento que a auditoria
  pegou, e é o teste manual mais importante da parcela.
- **Configurações → Especialidades**: mesma lista.
- **Painel de pendências, baixa, lote TISS, retorno e glosa**: o fluxo inteiro, igual.
- **Exportar um lote**: os avisos do radar de glosas têm de ser os mesmos — nenhum aviso
  novo sobre prontuário (isso ficou de fora de propósito).

Automatizado, isso já é `FaturamentoCongeladoTests` — o teste manual serve para confirmar
na tela o que o teste afirma no código.

### No Consultório — o fluxo novo

1. **Gerente** → Profissionais e salas: cadastre o profissional; em Acessos, ligue o
   usuário dele ao cadastro (sem esse vínculo o Consultório mostra o dia da clínica
   inteira — e diz isso na tela, o que também vale conferir).
2. **Recepção** → marque um horário para ele e confirme a presença.
3. **Consultório** → *Meu dia*: o horário aparece com o selo **"Sem evolução"**.
4. Botão **Atender** → escreva a sessão com EVA antes/depois, marque pontos na aba **Mapa
   corporal**, salve. O selo vira **"Evolução escrita"**.
5. *Evolução da dor*: com três sessões medidas, confira as duas curvas e a leitura da
   tendência.
6. *Avaliações*: aplique um PHQ-9 marcando **só o item 9** — o escore cai em "sintomas
   mínimos" e o **alerta do item** tem de aparecer mesmo assim.
7. **Gerente** → painel: com uma sessão atendida ontem e sem evolução, o alerta
   **"prontuário em aberto"** aparece e o botão leva ao Consultório.
8. **Recepção** → Prontuário do paciente: a linha "Escalas aplicadas" mostra o PHQ-9.

Os passos 3, 7 e 8 são os elos entre módulos — se algum falhar, é chave estrangeira, não
tela.

## 4. Quando for a hora de publicar

Um app por vez, cada um com sua tag:

```bash
git tag clinico-v1.0.0 && git push origin clinico-v1.0.0     # só o Consultório
```

O faturamento **não** é republicado, e as instalações dele continuam no canal `win` sem
receber nada. Quando o faturamento for republicado um dia, aí sim a clínica recebe as
mudanças das camadas compartilhadas — e é por isso que `FaturamentoCongeladoTests` e a
checagem 18 do `verificar-suite.py` existem.

> Ver também: [`atualizacoes.md`](atualizacoes.md) (canais e auto-update) e
> [`arquitetura-multi-exe.md`](arquitetura-multi-exe.md) (por que migration só aditiva).
