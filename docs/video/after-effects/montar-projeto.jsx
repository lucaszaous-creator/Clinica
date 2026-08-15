/**
 * montar-projeto.jsx — monta o projeto do vídeo de entrega da Clínica SemDor
 * ---------------------------------------------------------------------------
 * COMO RODAR
 *   After Effects → Arquivo → Scripts → Executar arquivo de script… → este .jsx
 *
 * O QUE ELE CRIA
 *   · Composição mestre 1920×1080 @ 30fps, 90s
 *   · 7 espaços de gravação, já com a duração de cada cena e cross-dissolve
 *     de 0,4s nas emendas
 *   · Marcador na régua no início de cada cena, com o nome
 *   · Faixa de LEGENDAS com a locução inteira, já cronometrada
 *   · Pasta de sólidos com as cores da marca (Tokens.xaml)
 *
 * O QUE ELE NÃO FAZ, E POR QUÊ
 *   Não importa mídia. O script não sabe onde estão os seus arquivos, e um
 *   caminho chutado quebraria na primeira máquina diferente. Grave as cenas
 *   com cenas-animadas.html, importe, e arraste cada clipe para cima do
 *   espaço correspondente com Alt pressionado (substitui mantendo o tempo).
 *
 * ExtendScript é ES3: sem let/const, sem arrow, sem template literal. Não
 * "modernize" este arquivo — o AE recusa e a mensagem de erro não ajuda.
 */

(function () {
  // ---------------------------------------------------------------- dados
  // Os tempos espelham docs/video/roteiro.md e cenas-animadas.html.
  // Mudou um, mude os três.
  var FPS = 30;
  var LARG = 1920;
  var ALT = 1080;
  var EMENDA = 0.4;          // duração do cross-dissolve entre cenas

  var CENAS = [
    { nome: "Cena 1 · Abertura",      dur: 9 },
    { nome: "Cena 2 · A dor",         dur: 11 },
    { nome: "Cena 3 · O painel",      dur: 16 },
    { nome: "Cena 4 · A rodada",      dur: 12 },
    { nome: "Cena 5 · A suíte",       dur: 15 },
    { nome: "Cena 6 · Conformidade",  dur: 15 },
    { nome: "Cena 7 · Fecho",         dur: 12 }
  ];

  // Legendas: início absoluto, duração e texto. Recortadas em blocos curtos
  // de propósito — legenda de 40 palavras não se lê no tempo em que ela fica
  // no ar, e quem tenta ler para de olhar a tela.
  var LEGENDAS = [
    [ 0.5,  4.0, "Toda clínica de convênio conhece esta cena:" ],
    [ 4.5,  4.5, "o atendimento acabou, o paciente foi embora —\ne a segunda guia ficou para amanhã." ],
    [ 9.3,  2.7, "Amanhã vira semana.\nQuando alguém lembra, o prazo já correu." ],
    [12.0,  4.0, "Não é descuido: é uma tarefa que nasce\nvinte e quatro horas depois," ],
    [16.0,  4.0, "quando o dia já acabou." ],
    [20.3,  3.7, "Por isso o sistema começa pelo fim." ],
    [24.0,  4.0, "A primeira tela do dia é o que está em aberto —\ncada guia com a sua cor." ],
    [28.0,  4.5, "Verde, dentro do prazo. Amarelo, vence hoje.\nVermelho, já passou." ],
    [32.5,  3.5, "Ninguém precisa procurar:\na lista procura por você." ],
    [36.3,  3.7, "E quando o prazo vence, o sistema não avisa.\nEle para." ],
    [40.0,  4.5, "Cada guia exige uma decisão: dar baixa,\nou dizer por que não deu." ],
    [44.5,  3.5, "Nada some sem alguém responder." ],
    [48.3,  3.7, "O que começou no faturamento virou a clínica inteira.\nCinco programas, um banco." ],
    [52.0,  5.0, "A recepção marca, o consultório registra,\no financeiro concilia, a direção enxerga." ],
    [57.0,  6.0, "O que um grava, o outro lê — na hora,\nsem ninguém digitar duas vezes." ],
    [63.3,  3.7, "E o que a lei exige está no código,\nnão no manual." ],
    [67.0,  5.0, "O prontuário não se apaga: corrige-se,\ne a versão anterior fica guardada." ],
    [72.0,  2.5, "Quem abre, fica registrado." ],
    [74.5,  3.5, "E a receita sai assinada em ICP-Brasil,\nconferível no validador do ITI." ],
    [78.3,  3.7, "Cinco aplicativos. Oitenta e quatro serviços." ],
    [82.0,  4.0, "Mil quinhentos e oitenta e dois testes automatizados,\nrodando a cada mudança." ],
    [86.0,  3.5, "Clínica SemDor —\nconstruído tela por tela, com quem usa." ]
  ];

  var MARCA = [
    ["Acento royal",  "#123A9E"],
    ["Acento forte",  "#0A2E86"],
    ["Navy símbolo",  "#07329A"],
    ["Hero fundo",    "#071F5C"],
    ["Semáforo verde","#2E7D32"],
    ["Semáforo âmbar","#F9A825"],
    ["Semáforo vermelho","#C62828"],
    ["Tinta",         "#111827"],
    ["Fundo claro",   "#F8FAFC"]
  ];

  // --------------------------------------------------------------- apoio
  function doHex(hex) {
    // AE trabalha com componentes de 0 a 1, não 0 a 255.
    var h = hex.replace("#", "");
    return [
      parseInt(h.substring(0, 2), 16) / 255,
      parseInt(h.substring(2, 4), 16) / 255,
      parseInt(h.substring(4, 6), 16) / 255
    ];
  }

  function definirFonte(td, tamanho, negrito) {
    td.fontSize = tamanho;
    // O nome PostScript da fonte varia entre máquinas e versões do AE.
    // Falhar aqui não pode derrubar a montagem inteira — sem a fonte o texto
    // sai na padrão, que é feio e corrigível; sem o projeto, não há o que
    // corrigir.
    try {
      td.font = negrito ? "SegoeUI-Bold" : "SegoeUI";
    } catch (e) {
      try { td.font = negrito ? "Arial-BoldMT" : "ArialMT"; } catch (e2) {}
    }
  }

  function fade(camada, entra, sai, subida) {
    var op = camada.property("Transform").property("Opacity");
    op.setValueAtTime(entra, 0);
    op.setValueAtTime(entra + subida, 100);
    op.setValueAtTime(sai - subida, 100);
    op.setValueAtTime(sai, 0);
  }

  // -------------------------------------------------------------- projeto
  if (!app.project) app.newProject();
  var proj = app.project;

  app.beginUndoGroup("Montar vídeo de entrega — SemDor");

  var pastaComps = proj.items.addFolder("01 · Comps");
  var pastaMarca = proj.items.addFolder("02 · Marca");
  var pastaGrav  = proj.items.addFolder("03 · Gravações (importe aqui)");

  var total = 0;
  var i;
  for (i = 0; i < CENAS.length; i++) total += CENAS[i].dur;

  var mestre = proj.items.addComp("SemDor — MASTER 90s", LARG, ALT, 1.0, total, FPS);
  mestre.parentFolder = pastaComps;
  mestre.bgColor = doHex("#071F5C");

  // --- sólidos da marca ---------------------------------------------------
  // Ficam no projeto para virarem fundo, barra ou máscara sem ninguém ter de
  // reencontrar o hexadecimal no meio do Tokens.xaml.
  // Sólido só se cria DENTRO de uma comp — não há API de projeto para isso.
  // Daí a comp descartável: ela nasce, hospeda a criação e sai. Os sólidos são
  // itens do projeto e sobrevivem à remoção dela.
  var compAux = proj.items.addComp("_paleta (auxiliar)", 200, 200, 1.0, 1, FPS);
  for (i = 0; i < MARCA.length; i++) {
    var s = compAux.layers.addSolid(doHex(MARCA[i][1]), MARCA[i][0], LARG, ALT, 1.0);
    s.source.parentFolder = pastaMarca;
  }
  compAux.remove();

  // --- espaços de gravação, um por cena -----------------------------------
  // ORDEM DIRETA, e ela é o que faz o cross-dissolve existir. addSolid insere
  // no TOPO da pilha, então adicionar da cena 1 à 7 deixa a 7 em cima e a 1
  // embaixo — cada cena que entra fica ACIMA da anterior e a cobre enquanto
  // sobe de 0 a 100.
  // Ao contrário: com a cena 1 no topo, a 2 subiria por baixo de uma camada
  // opaca, invisível, e no fim do trecho a 1 sumiria de uma vez. Vira corte
  // seco — e não dá erro nenhum, só sai errado no vídeo.
  var inicio = 0;
  var inicios = [];
  for (i = 0; i < CENAS.length; i++) { inicios.push(inicio); inicio += CENAS[i].dur; }

  var semMarcadores = false;

  for (i = 0; i < CENAS.length; i++) {
    var ini = inicios[i];
    var fim = ini + CENAS[i].dur;

    var vaga = mestre.layers.addSolid(
      doHex(i % 2 === 0 ? "#1E293B" : "#243447"),
      "[SUBSTITUIR] " + CENAS[i].nome,
      LARG, ALT, 1.0
    );
    vaga.source.parentFolder = pastaGrav;

    // Sobra EMENDA no fim para o cross-dissolve ter o que dissolver: sem a
    // sobreposição, a transição revelaria o fundo da comp entre os clipes.
    vaga.inPoint = ini;
    vaga.outPoint = Math.min(fim + EMENDA, total);

    var op = vaga.property("Transform").property("Opacity");
    if (i === 0) {
      op.setValueAtTime(ini, 0);              // abre em fade a partir do preto
      op.setValueAtTime(ini + 0.6, 100);
    } else {
      op.setValueAtTime(ini, 0);
      op.setValueAtTime(ini + EMENDA, 100);
    }
    if (i === CENAS.length - 1) {
      op.setValueAtTime(total - 0.8, 100);    // e fecha em fade
      op.setValueAtTime(total, 0);
    }

    // markerProperty, NÃO markers: só Layer tem .marker; a composição expõe
    // markerProperty, e ela existe a partir do AE CC (12.0). Em versão mais
    // velha o script segue sem marcador em vez de morrer — o projeto montado
    // sem as marcas da régua ainda serve; nenhum projeto não serve.
    if (mestre.markerProperty) {
      mestre.markerProperty.setValueAtTime(ini, new MarkerValue(CENAS[i].nome));
    } else {
      semMarcadores = true;
    }
  }

  // --- faixa de legendas ---------------------------------------------------
  var legendas = [];
  for (i = 0; i < LEGENDAS.length; i++) {
    var t  = LEGENDAS[i][0];
    var d  = LEGENDAS[i][1];
    var tx = LEGENDAS[i][2];

    var cam = mestre.layers.addText(tx);
    cam.name = "LEG " + (i + 1 < 10 ? "0" : "") + (i + 1);

    var td = cam.property("Source Text").value;
    definirFonte(td, 38, false);
    // applyFill/applyStroke vêm ANTES da cor: o AE recusa fillColor com o
    // preenchimento desligado e strokeColor com o contorno desligado.
    td.applyFill = true;
    td.fillColor = [1, 1, 1];
    td.justification = ParagraphJustification.CENTER_JUSTIFY;
    td.applyStroke = true;                    // contorno: a legenda cai tanto
    td.strokeColor = [0, 0, 0];               // sobre o navy quanto sobre o
    td.strokeWidth = 5;                       // branco da tela do app
    td.strokeOverFill = false;                // contorno POR FORA das letras
    cam.property("Source Text").setValue(td);

    cam.property("Transform").property("Position").setValue([LARG / 2, ALT - 96]);
    cam.inPoint = t;
    cam.outPoint = Math.min(t + d, total);
    fade(cam, t, Math.min(t + d, total), 0.18);

    legendas.push(cam);
  }

  // As legendas ficam num rótulo próprio para dar para desligar a faixa
  // inteira de uma vez — é o primeiro pedido de quem for exportar uma versão
  // sem legenda para redes sociais.
  for (i = 0; i < legendas.length; i++) legendas[i].label = 9;

  // --- guias de trilha -----------------------------------------------------
  // Nulos vazios só para marcar onde entram os áudios. Não fazem nada; existem
  // para a próxima pessoa saber onde pôr o quê.
  var guiaVoz = mestre.layers.addNull(total);
  guiaVoz.name = "↓ LOCUÇÃO entra abaixo desta linha";
  guiaVoz.enabled = false;
  guiaVoz.label = 11;

  var guiaMus = mestre.layers.addNull(total);
  guiaMus.name = "↓ TRILHA entra abaixo desta linha (licenciada!)";
  guiaMus.enabled = false;
  guiaMus.label = 11;

  mestre.openInViewer();

  app.endUndoGroup();

  alert(
    "Projeto montado.\n\n" +
    "· Comp: SemDor — MASTER 90s (" + total + "s @ " + FPS + "fps)\n" +
    "· " + CENAS.length + " espaços de gravação" +
      (semMarcadores
        ? ", SEM marcadores (esta versão do AE não expõe markerProperty)"
        : ", com marcador em cada cena") + "\n" +
    "· " + LEGENDAS.length + " legendas já cronometradas\n" +
    "· " + MARCA.length + " sólidos da marca\n\n" +
    "Próximo passo: grave as cenas com cenas-animadas.html, importe os\n" +
    "clipes para a pasta 03, e arraste cada um com ALT sobre o espaço\n" +
    "[SUBSTITUIR] correspondente — o tempo e as transições ficam de pé.\n\n" +
    "A locução manda: se ela não bater com os tempos, mova os marcadores\n" +
    "e estique os clipes. O roteiro é a proposta, não a lei."
  );
})();
