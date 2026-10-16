# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Não há um usuário principal fixo: **quem decide os conflitos de design é a direção do
projeto** (confirmado no `/impeccable init`, out/2026). Cada aplicativo da suíte serve um
público, e uma tela é julgada pelo público dela:

- **Recepção / balcão** — a secretária com o paciente na frente: marca e lança
  atendimento, confere convênio, carteirinha e termos, recebe o particular. Trabalha num
  monitor de 1366×768, com muitas tarefas por minuto e interrupções.
- **Médicos e enfermagem** (app Consultório) — registram a sessão durante ou logo após o
  atendimento (S-O-A-P do lado médico; as cinco etapas da COFEN 358/2009 do lado da
  enfermagem), prescrevem, checam a sala de infusão.
- **Faturista** — dá baixa nas guias no portal da operadora, trata glosa e lote TISS.
- **Financeiro** — caixa, contas, conciliação, estoque, repasse.
- **Direção / gerente** — visão da operação inteira, indicadores, auditoria e acessos.

## Product Purpose

Sistema de gestão de uma clínica médica de acupuntura (com psiquiatria, geriatria,
neurocirurgia, endocrinologia e clínica da dor), em cinco aplicativos sobre o mesmo
banco: Recepção, Consultório, Faturamento, Financeiro e Gerente, mais uma web de
leitura. Nasceu do faturamento: impedir que o **2º código/guia** — obtido 24 h depois do
atendimento — seja esquecido. Sucesso é a clínica faturar o que atendeu, ter o
prontuário completo e auditável, e o balcão resolver com o paciente presente o que fica
caro depois.

## Positioning

O produto modela os fluxogramas reais dos convênios desta clínica e mantém cada guia
pendente visível, com semáforo e prazo, até a baixa — e é um prontuário que **não
apaga** registro clínico (cancela com motivo, versiona a correção), por compromisso
escrito com a LGPD e a Lei 13.787/2018.

## Operating Context

- Desktop Windows na clínica; o banco é PostgreSQL numa VPS no Brasil. Duas pessoas
  costumam dividir a mesma máquina no balcão, por isso toda ação é assinada pelo login.
- A secretária efetiva a guia no portal da operadora e retipa dados de lá para cá.
- Documentos saem em papel e em PDF: receita, atestado, laudo, termos assinados pelo
  paciente (no balcão ou pelo celular), folha de infusão assinada com e-CPF ICP-Brasil.
- Comparativo de mercado citado pela própria clínica: Smart Clinic (sistema anterior,
  de onde a carteira foi importada) e iClinic.

## Capabilities and Constraints

- **Stack de telas:** toda tela é **Blazor Hybrid** (HTML/CSS em Razor, `Clinica.Ui.Blazor`,
  hospedado num navegador por janela). Financeiro e Faturamento já foram migrados; os
  demais módulos seguem o mesmo caminho e **o WPF não recebe tela nova** — o XAML que
  resta é legado em migração. Guia: `docs/telas-blazor.md`.
- O que a tela afirma é decidido na camada Application, com teste; a tela formata e
  desenha.
- Todo texto, código e UI em português do Brasil; dinheiro e data em pt-BR fixo.
- Faturamento ≠ recebíveis: "baixa" é efetivar a guia no convênio.
- Assinatura digital (SafeID/ICP-Brasil) é área congelada — cada tentativa é cobrada.
- Permissão granular por ato, com dado de saúde separado de dado cadastral (LGPD art. 5º, II).
- **Em aberto:** se o sistema permanece sob medida para esta clínica ou vira produto
  oferecido a outras clínicas. Até decidir, não assumir marca de produto nem neutralidade
  de multi-cliente.

## Brand Commitments

- Voz: direta e honesta, em pt-BR, nas palavras da clínica — nunca o nome técnico do
  dado (enum, código) na tela.
- **Não prometer garantia que o código não dá**: o rodapé de um documento diz o nível
  real da assinatura; falha nunca aparece como sucesso; ausência de dado nunca aparece
  como zero.
- Design system próprio já existente (tokens em `tokens/`, documentação em
  `docs/design-system/`), com regra de leiaute escrita no `README.md` depois de sete
  reprovações da cliente: nada de telas picadas em caixas empilhadas.

## Evidence on Hand

- No sistema antigo, de 139 faturas possíveis apenas 36 eram efetivadas, porque o 2º
  código era esquecido (`README.md`).
- Carteira importada do Smart Clinic: ~2.238 fichas, a maioria sem convênio definido.
- Mockups aprovados pela direção em `docs/mockups/`; manuais por módulo em
  `docs/manuais/`; mapa de conformidade em `docs/conformidade-lgpd.md`.
- Não há depoimentos, clientes adicionais, preços ou métricas de mercado — não inventar.

## Product Principles

- **O fato irreversível não depende do passo opcional:** a guia nasce quando o
  atendimento entra no sistema; caixa, pacote e estoque vêm depois e não a desfazem.
- **Avisa, não impede** — exceto onde a lei ou o dinheiro exigem recusa; e todo aviso
  tem a porta para resolvê-lo no mesmo app.
- **Registro clínico não se apaga**; corrigir guarda o que estava antes.
- **Uma pergunta por tela:** o que se faz de vez em quando é botão ou janela; o que se
  vê o tempo todo ocupa a tela.
- **A tela nunca mente:** vazio por erro tem cara de erro, não de "nada aqui".

## Accessibility & Inclusion

- Uso diário em monitor 1366×768, inclusive com escala do Windows em 125–150%; telas
  são conferidas em 1024 e 1366 de largura.
- Atalhos de teclado e foco documentados em `docs/design-system/atalhos.md` e
  `docs/design-system/acessibilidade.md`.
