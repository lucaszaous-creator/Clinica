# Gerente: a Mesa da direção e o menu da direção (out/2026)

Primeira etapa da reorganização do Gerente aprovada pela direção. O mockup aprovado é
[`mockups/gerente-mesa-da-direcao-v2.html`](mockups/gerente-mesa-da-direcao-v2.html), com a
logo da clínica e os temas claro e escuro. O menu fica **no topo**, como nos outros apps,
porque a clínica usa notebook.

## O que mudou

- **Mesa da direção** (`Telas/Gestao/MesaDaDirecao.razor`, Blazor). Substitui o painel WPF da
  direção e é a tela de abertura do Gerente. Ela responde a duas perguntas.
  - **Como o mês está.** Uma régua mostra o recebido, a projeção e a meta de faturamento.
    Abaixo ficam o resultado no caixa, as saídas, as contas a pagar vencidas e quanto os
    pacientes devem. Os quatro números do mês são sessões realizadas com a ocupação, guias por
    liberar, sessões sem evolução, e quem não vem ou não respondeu.
  - **O que precisa de você hoje.** Os alertas de todos os módulos aparecem numa pauta só,
    pelo que custa esperar. Cada um tem o botão que leva à tela que o resolve. A pauta mostra
    seis itens, e os demais ficam a um clique.
  - A mesa mostra também a agenda de hoje por profissional, os convênios do mês (bruto e
    líquido) e as metas do mês.
- **Menu do topo, grupo DIREÇÃO, primeiro na barra.** Ele tem:
  - **Mesa da direção**;
  - **Dinheiro**: Resultado do mês e Metas;
  - **Produção e margem**: Indicadores, Tendências, Produção e Rentabilidade por convênio;
  - **Pacientes e relacionamento**: De onde vêm os pacientes, Comissão de recuperação e
    Campanhas;
  - **Preços e regras**: Convênios, Particular e Custo de taxas e impostos;
  - **Conformidade e acessos**;
  - **Configurações** e **Importar pacientes**.
- **O que mais mudou no menu:**
  - Saíram os itens "Painel" (três abas), "Relatórios / BI", "Rentabilidade e custos" e
    "Marketing / Recall". Nenhuma tela sumiu: cada uma virou aba da entrada que responde a
    pergunta dela.
  - O Início da Recepção e o Meu dia do Consultório voltaram para os grupos dos módulos deles.
  - O Acompanhamento continua só em Relacionamento, para não haver dois caminhos para a
    mesma tela.

## O que ainda não está na mesa, e quando entra

O mockup mostra três números que **não existem** no sistema hoje. Eles ficaram de fora, em
vez de aparecer como zero:

- **resultado por competência**: entrou na régua com a etapa "Dinheiro" (ver
  [`gerente-dinheiro.md`](gerente-dinheiro.md));
- **margem por sessão**: existe desde a etapa "Produção e margem", na primeira aba de
  Produção e margem (ver [`gerente-margem.md`](gerente-margem.md)). A Mesa ainda não a
  repete;
- **números do CRM** (conversas, tempo de resposta, custo de SMS): etapa do CRM, caminho A.
  A ponte vai gravar um resumo diário no banco da clínica.

## 🧑‍💻 UX · 🛠️ DX · 🤝 CX

- 🧑‍💻 **UX**:
  - A direção abre o Gerente e vê, numa tela, como o mês está e o que pede decisão.
  - Cada alerta leva à tela que resolve.
  - O menu da direção tem seis entradas, em vez de mais de dez espalhadas por quatro grupos.
  - Funciona em 1024 e 1366 px; abaixo de 1200 px as colunas se empilham.
- 🛠️ **DX**:
  - A tela lê os serviços que já existiam: `PainelDirecaoService`, `IndicadoresService` e
    `RentabilidadeConvenioService`.
  - O que ela afirma (régua, ordem da pauta, destino de cada alerta, frases) mora em
    `MesaDaDirecaoNaTela`, puro, com 12 testes.
  - O grupo novo do menu é `GrupoSidebar.Direcao`, ordenado primeiro por `GruposSidebar.Ordem`.
  - O painel WPF da direção foi removido.
- 🤝 **CX**:
  - Uma tela que parece de gestão, com a cara da clínica.
  - Nada novo no servidor e nenhuma migração de banco.
  - O risco é de hábito: quem procurava "Relatórios / BI" agora encontra as mesmas telas em
    "Produção e margem" e "Dinheiro". A busca do topo continua achando cada tela pelo nome.
