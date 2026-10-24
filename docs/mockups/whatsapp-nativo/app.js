/* O mockup interativo da tela "WhatsApp da clínica" (proposta, out/2026).
   Dados fictícios + renderização em JS puro, para a direção CLICAR antes de o Razor existir.
   No app, cada função daqui tem um dono: o que a tela AFIRMA (selo, janela de 24 h, o que
   pede ação) mora em ConversasNaTela (Application, com teste); as ações da agenda são do
   AgendaService; o CRM só recebe/envia mensagens e guarda a conversa. */
(function () {
  'use strict';
  const $ = (s, r) => (r || document).querySelector(s);
  const h = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const ic = (nome, cls) => `<svg class="icone${cls ? ' ' + cls : ''}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">${ICONES[nome] || ICONES.info}</svg>`;

  // O relógio do mockup: a tela "nasce" às 10:12 de sexta, 24/10/2026, e anda com o relógio real.
  const INICIO = Date.now(); const BASE_MIN = 10 * 60 + 12;
  const agora = () => { const m = BASE_MIN + Math.floor((Date.now() - INICIO) / 60000); return `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`; };
  let ordemSeq = 1000; const proximaOrdem = () => ++ordemSeq;

  const EU = { id: 'ana', nome: 'Ana Paula' };
  const EQUIPE = [{ id: 'ana', nome: 'Ana Paula' }, { id: 'bruna', nome: 'Bruna' }, { id: 'carla', nome: 'Carla' }, { id: 'paulo', nome: 'Paulo (Financeiro)' }];
  const nomeDe = id => (EQUIPE.find(e => e.id === id) || {}).nome || 'Sem responsável';

  const MODELOS = [
    { id: 'lembrete_sessao', categoria: 'Utilidade', texto: 'Olá, {{1}}! Lembrando que você tem {{2}} na Clínica SemDor {{3}}. Responda SIM para confirmar ou NÃO se precisar remarcar.', params: ['Primeiro nome', 'Atendimento', 'Dia e hora'] },
    { id: 'retorno_tratamento', categoria: 'Utilidade', texto: 'Olá, {{1}}! Sentimos sua falta na {{2}}: já são {{3}} dias desde a última sessão. Quer que a gente veja um horário para você?', params: ['Primeiro nome', 'Tratamento', 'Dias'] },
    { id: 'documento_pronto', categoria: 'Utilidade', texto: 'Olá, {{1}}! O seu {{2}} está pronto. Pode retirar na recepção ou responder aqui que enviamos por mensagem.', params: ['Primeiro nome', 'Documento'] },
    { id: 'primeiro_contato', categoria: 'Utilidade', texto: 'Olá, {{1}}! Aqui é a recepção da Clínica SemDor. Você pediu contato sobre {{2}}. Pode falar por aqui?', params: ['Primeiro nome', 'Assunto'] },
  ];
  const RAPIDAS = [
    { titulo: 'Bom dia', texto: 'Bom dia! Aqui é a Ana Paula, da Clínica SemDor. Em que posso ajudar?' },
    { titulo: 'Endereço', texto: 'Estamos na Av. Atlântica, 1.200, sala 304, Macaé. Tem estacionamento no prédio.' },
    { titulo: 'Valor da acupuntura', texto: 'A sessão avulsa de acupuntura é R$ 180,00 e o pacote de 10 sessões sai R$ 1.500,00. Atendemos Unimed e Bradesco Saúde.' },
    { titulo: 'Documentos da 1ª consulta', texto: 'Para a primeira consulta, traga documento com foto, carteirinha do convênio e os exames recentes, se tiver.' },
    { titulo: 'Horário de atendimento', texto: 'Atendemos de segunda a sexta, das 8h às 18h, e aos sábados das 8h às 12h.' },
  ];

  const MARIA = { nome: 'Maria das Graças Silva', numero: '4.512', idade: '58 anos · 12/03/1968', desde: 'paciente desde 2023', telefoneConfere: true, convenio: 'Unimed · 0 064 1234 5678 9', carteirinha: { texto: 'vence em 37 dias', tom: 'aviso' }, documento: '···.···.···-42', autorizacao: 'Autorizou no termo do BSV · 03/09', tratamento: ['Em tratamento · Acupuntura', 'BSV encerrado em jun'], renovar: 'Consulta de convênio a renovar em 14 dias (Unimed). A tela Consultas de convênio já acompanha.' };
  const PEDRO = { nome: 'Pedro Henrique Souza', numero: '2.231', idade: '9 anos · 14/05/2017', desde: 'paciente desde 2025 · responsável: Terezinha Souza', telefoneConfere: true, convenio: 'Particular', carteirinha: null, documento: '···.···.···-07', autorizacao: 'Autorizou no cadastro · 08/08', tratamento: ['Em tratamento · Acupuntura'] };

  const CONVERSAS = [
    {
      id: 'maria', nome: 'Maria das Graças Silva', telefone: '(22) 99876-5432', iniciais: 'MG', tom: 'azul', canal: 'wa', fila: 'Recepção', responsavel: null, naoLidas: 1, hora: '10:09', ordem: 900,
      janela: { aberta: true, ate: 'amanhã às 10:09' }, ficha: { estado: 'conferida', numero: '4.512', quando: '03/09' }, paciente: MARIA,
      situacao: 'nao-vem',
      horarios: [
        { id: 'h1', quando: 'Hoje 14:00', detalhe: 'Acupuntura · Dra. Helena · sala 2 · guia 2 de 10', estado: 'nao-vem' },
        { id: 'h2', quando: 'Qui 06/11 · 14:00', detalhe: 'Acupuntura · Dra. Helena · o lembrete sai 06/11 às 06:00', estado: 'marcado' }],
      confirmacao: [{ hora: '06:00', texto: 'Lembrete enviado pelo WhatsApp da clínica' }, { hora: '07:12', texto: 'Lido pela paciente' }, { hora: '09:58', texto: 'Respondeu <b>Não vem</b> · aviso no sino e no Gerente' }],
      acompanhamento: [{ quando: '17/10', texto: 'Sessão 5 de 10 · compareceu' }, { quando: '10/10', texto: 'Sessão 4 de 10 · compareceu' }],
      mensagens: [
        { tipo: 'dia', texto: 'Ontem · quinta-feira, 23/10' },
        { tipo: 'enviada', autor: 'Bruna', hora: '16:20', estado: 'lida', texto: 'Boa tarde, Maria! Lembrando que amanhã às 14:00 você tem acupuntura com a Dra. Helena. Qualquer coisa é só responder por aqui.' },
        { tipo: 'dia', texto: 'Hoje · sexta-feira, 24/10' },
        { tipo: 'evento', hora: '06:00', texto: 'Lembrete do dia enviado pelo número oficial (modelo <b>lembrete_sessao</b>) · entregue 06:01 · lido 07:12' },
        { tipo: 'recebida', hora: '09:58', texto: 'Bom dia! Não vou conseguir ir hoje, posso remarcar?' },
        { tipo: 'recebida', hora: '09:58', texto: 'Surgiu um compromisso no trabalho 😕' },
        { tipo: 'evento', tom: 'alerta', hora: '09:58', texto: 'A resposta virou o aviso <b>Não vem — remarcar</b> no sino da Recepção e no painel do Gerente' },
        { tipo: 'nota', autor: 'Ana Paula', hora: '10:02', texto: 'Ela prefere o período da tarde. Oferecer terça ou quinta com a Dra. Helena.' },
        { tipo: 'enviada', autor: 'Ana Paula', hora: '10:05', estado: 'lida', texto: 'Bom dia, Maria! Sem problema. Tenho terça 28/10 às 14:00 ou quinta 30/10 às 09:30 com a Dra. Helena. Qual prefere?' },
        { tipo: 'recebida', hora: '10:09', texto: 'Terça às 14h fica ótimo', nova: true }],
    },
    {
      id: 'novo', nome: '(22) 99712-0045', telefone: '(22) 99712-0045', iniciais: '?', tom: 'cinza', canal: 'wa', fila: 'Recepção', responsavel: null, naoLidas: 2, hora: '09:30', ordem: 850,
      janela: { aberta: true, ate: 'amanhã às 09:30' }, ficha: { estado: 'nenhuma' }, paciente: null, situacao: 'novo',
      horarios: [], confirmacao: [], acompanhamento: [],
      mensagens: [
        { tipo: 'dia', texto: 'Hoje · sexta-feira, 24/10' },
        { tipo: 'recebida', hora: '09:29', texto: 'Bom dia, gostaria de saber o valor da acupuntura', nova: true },
        { tipo: 'recebida', hora: '09:30', texto: 'É para a minha mãe, 72 anos, dor no joelho' }],
    },
    {
      id: 'terezinha', nome: 'Terezinha Souza', telefone: '(22) 98811-2233', iniciais: 'TS', tom: 'violeta', canal: 'wa', fila: 'Recepção', responsavel: null, naoLidas: 1, hora: '09:12', ordem: 800,
      janela: { aberta: true, ate: 'amanhã às 09:12' }, ficha: { estado: 'duvida', candidatas: 2 }, paciente: null, situacao: 'fichas',
      horarios: [], confirmacao: [], acompanhamento: [],
      mensagens: [
        { tipo: 'dia', texto: 'Hoje · sexta-feira, 24/10' },
        { tipo: 'evento', hora: '06:00', texto: 'Lembrete da sessão de amanhã (sáb 25/10 09:00 · Pedro Henrique) enviado · lido 08:40' },
        { tipo: 'recebida', hora: '09:12', texto: 'O Pedro vai sim amanhã, obrigada', nova: true }],
    },
    {
      id: 'carlos', nome: 'Carlos Eduardo Mota', telefone: '(22) 99133-7788', iniciais: 'CM', tom: 'teal', canal: 'wa', fila: 'Recepção', responsavel: 'ana', naoLidas: 0, hora: '07:12', ordem: 700,
      janela: { aberta: false, desde: 'qua 22/10 às 11:05' }, ficha: { estado: 'conferida', numero: '3.108', quando: '14/02' },
      paciente: { nome: 'Carlos Eduardo Mota', numero: '3.108', idade: '46 anos · 30/07/1980', desde: 'paciente desde 2024', telefoneConfere: true, convenio: 'Bradesco Saúde · 7788 0001 2', carteirinha: { texto: 'vale até 2027', tom: 'sucesso' }, documento: '···.···.···-15', autorizacao: 'Autorizou no termo do BSV · 14/02', tratamento: ['Em tratamento · BSV e acupuntura'] },
      situacao: 'sem-resposta',
      horarios: [{ id: 'h3', quando: 'Hoje 16:00', detalhe: 'BSV · Dr. Marcos · sala 1 · guia 7 de 10', estado: 'sem-resposta' }],
      confirmacao: [{ hora: '06:00', texto: 'Lembrete enviado pelo WhatsApp da clínica' }, { hora: '07:12', texto: 'Lido pelo paciente' }, { hora: '10:12', texto: 'Sem resposta há 3 h · aviso no sino' }],
      acompanhamento: [{ quando: '17/10', texto: 'Sessão 6 de 10 · compareceu' }],
      mensagens: [
        { tipo: 'dia', texto: 'Quarta-feira, 22/10' },
        { tipo: 'enviada', autor: 'Ana Paula', hora: '10:58', estado: 'lida', texto: 'Bom dia, Carlos! Sua próxima sessão de BSV ficou para sexta 24/10 às 16:00 com o Dr. Marcos, combinado?' },
        { tipo: 'recebida', hora: '11:05', texto: 'Ok, obrigado' },
        { tipo: 'dia', texto: 'Hoje · sexta-feira, 24/10' },
        { tipo: 'evento', hora: '06:00', texto: 'Lembrete do dia enviado (modelo <b>lembrete_sessao</b>) · entregue 06:00 · lido 07:12' },
        { tipo: 'evento', tom: 'alerta', hora: '10:12', texto: 'Três horas sem resposta: virou o aviso <b>Sem resposta</b> no sino' }],
    },
    {
      id: 'joao', nome: 'João Batista Ferreira', telefone: '(22) 99650-1010', iniciais: 'JB', tom: 'ciano', canal: 'wa', fila: 'Recepção', responsavel: 'ana', naoLidas: 0, hora: '09:41', ordem: 820,
      janela: { aberta: true, ate: 'amanhã às 09:40' }, ficha: { estado: 'conferida', numero: '1.977', quando: '2023' },
      paciente: { nome: 'João Batista Ferreira', numero: '1.977', idade: '67 anos · 02/11/1958', desde: 'paciente desde 2023', telefoneConfere: true, convenio: 'Unimed · 0 064 9988 7766 5', carteirinha: { texto: 'vale até 2027', tom: 'sucesso' }, documento: '···.···.···-88', autorizacao: 'Autorizou no termo do BSV · 11/03', tratamento: ['Em tratamento · Acupuntura'] },
      situacao: 'confirmou',
      horarios: [{ id: 'h4', quando: 'Hoje 15:00', detalhe: 'Acupuntura · Dra. Helena · sala 2 · guia 9 de 10', estado: 'confirmado' }],
      confirmacao: [{ hora: '06:00', texto: 'Lembrete enviado pelo WhatsApp da clínica' }, { hora: '09:40', texto: 'Respondeu <b>Confirmou</b>' }],
      acompanhamento: [{ quando: '17/10', texto: 'Sessão 8 de 10 · compareceu' }],
      mensagens: [
        { tipo: 'dia', texto: 'Hoje · sexta-feira, 24/10' },
        { tipo: 'evento', hora: '06:00', texto: 'Lembrete do dia enviado (modelo <b>lembrete_sessao</b>) · entregue 06:00 · lido 09:39' },
        { tipo: 'recebida', hora: '09:40', texto: 'Confirmado, obrigado!' },
        { tipo: 'evento', tom: 'ok', hora: '09:40', texto: 'Confirmou · a agenda do dia já mostra o selo verde' },
        { tipo: 'enviada', autor: 'Ana Paula', hora: '09:41', estado: 'lida', texto: 'Perfeito, João! Até as 15h 🙂' }],
    },
    {
      id: 'roberto', nome: 'Roberto Lima', telefone: '(22) 99240-3344', iniciais: 'RL', tom: 'laranja', canal: 'wa', fila: 'Recepção', responsavel: 'ana', naoLidas: 0, hora: 'ontem', ordem: 600,
      janela: { aberta: true, ate: 'hoje às 17:02' }, ficha: { estado: 'conferida', numero: '2.640', quando: '2024' },
      paciente: { nome: 'Roberto Lima', numero: '2.640', idade: '52 anos · 19/01/1974', desde: 'paciente desde 2024', telefoneConfere: true, convenio: 'Particular', carteirinha: null, documento: '···.···.···-60', autorizacao: 'Autorizou no cadastro · 05/05', tratamento: ['Em tratamento · Acupuntura', 'Pacote 10 sessões · 3 usadas'] },
      situacao: 'aguardando',
      horarios: [{ id: 'h5', quando: 'Qui 30/10 · 09:30', detalhe: 'Acupuntura · Dra. Helena · o lembrete sai 30/10 às 06:00', estado: 'marcado' }],
      confirmacao: [], acompanhamento: [{ quando: 'ontem', texto: 'Contato pelo WhatsApp · Ana Paula (registrado sozinho)' }, { quando: '16/10', texto: 'Sessão 3 de 10 · compareceu' }],
      mensagens: [
        { tipo: 'dia', texto: 'Ontem · quinta-feira, 23/10' },
        { tipo: 'recebida', hora: '16:48', texto: 'Boa tarde, preciso trocar a sessão da semana que vem, dá pra ser de manhã?' },
        { tipo: 'enviada', autor: 'Ana Paula', hora: '16:55', estado: 'lida', texto: 'Boa tarde, Roberto! Tenho quinta 30/10 às 09:30 com a Dra. Helena. Serve?' },
        { tipo: 'recebida', hora: '17:02', texto: 'Serve sim' },
        { tipo: 'evento', tom: 'ok', hora: '17:03', texto: 'Remarcado por Ana Paula: ter 28/10 14:00 → qui 30/10 09:30 · horário de terça liberado' },
        { tipo: 'enviada', autor: 'Ana Paula', hora: '17:03', estado: 'entregue', texto: 'Fica combinado então, Roberto. Até quinta!' }],
    },
    {
      id: 'fernanda', nome: 'Fernanda Nogueira', telefone: '(22) 99877-4455', iniciais: 'FN', tom: 'rosa', canal: 'wa', fila: 'Recepção', responsavel: 'bruna', naoLidas: 0, hora: 'ontem', ordem: 500,
      janela: { aberta: false, desde: 'sáb 04/10 às 10:20' }, ficha: { estado: 'conferida', numero: '2.902', quando: '2025' },
      paciente: { nome: 'Fernanda Nogueira', numero: '2.902', idade: '39 anos · 08/06/1987', desde: 'paciente desde 2025', telefoneConfere: true, convenio: 'Unimed · 0 064 5544 3322 1', carteirinha: { texto: 'vale até 2027', tom: 'sucesso' }, documento: '···.···.···-23', autorizacao: 'Autorizou no termo do BSV · 12/07', tratamento: ['Em tratamento · Acupuntura', 'Sem vir há 20 dias'] },
      situacao: 'retorno',
      horarios: [], confirmacao: [],
      acompanhamento: [{ quando: 'ontem', texto: 'Convite para voltar enviado pelo WhatsApp · Bruna' }, { quando: '04/10', texto: 'Sessão 4 de 10 · compareceu · faltou às duas seguintes' }],
      mensagens: [
        { tipo: 'dia', texto: 'Sábado, 04/10' },
        { tipo: 'recebida', hora: '10:20', texto: 'Obrigada pela sessão de hoje! Até semana que vem' },
        { tipo: 'dia', texto: 'Ontem · quinta-feira, 23/10' },
        { tipo: 'evento', tom: 'alerta', hora: '09:00', texto: 'Entrou na fila do Acompanhamento: 20 dias sem vir (acupuntura)' },
        { tipo: 'enviada', autor: 'Bruna', hora: '15:10', estado: 'entregue', modelo: 'retorno_tratamento', texto: 'Olá, Fernanda! Sentimos sua falta na acupuntura: já são 19 dias desde a última sessão. Quer que a gente veja um horário para você?' }],
    },
    {
      id: 'antonio', nome: 'Antônio Carvalho', telefone: '(22) 99333-9090', iniciais: 'AC', tom: 'teal', canal: 'sms', fila: 'Recepção', responsavel: 'bruna', naoLidas: 0, hora: 'ontem', ordem: 400,
      janela: { aberta: true, ate: 'sem janela (SMS)' }, ficha: { estado: 'conferida', numero: '0.812', quando: '2023' },
      paciente: { nome: 'Antônio Carvalho', numero: '0.812', idade: '74 anos · 21/09/1952', desde: 'paciente desde 2023', telefoneConfere: true, convenio: 'Unimed · 0 064 1122 3344 5', carteirinha: { texto: 'vale até 2026', tom: 'sucesso' }, documento: '···.···.···-04', autorizacao: 'Autorizou no termo do BSV · 02/02', tratamento: ['Em tratamento · BSV'] },
      situacao: 'encerrada', encerrada: { quando: 'ontem às 11:30', por: 'Bruna', resultado: 'Agendado' },
      horarios: [{ id: 'h6', quando: 'Seg 27/10 · 10:00', detalhe: 'BSV · Dr. Marcos · sala 1', estado: 'marcado' }],
      confirmacao: [], acompanhamento: [{ quando: 'ontem', texto: 'Contato por SMS · Bruna (registrado sozinho)' }],
      mensagens: [
        { tipo: 'dia', texto: 'Ontem · quinta-feira, 23/10' },
        { tipo: 'recebida', hora: '11:02', texto: 'Bom dia quero marcar o bloqueio' },
        { tipo: 'enviada', autor: 'Bruna', hora: '11:20', estado: 'entregue', texto: 'Bom dia, Sr. Antonio! Marquei o BSV para segunda 27/10 as 10h com o Dr. Marcos. Chegue 15 min antes.' },
        { tipo: 'recebida', hora: '11:28', texto: 'Ta bom obrigado' },
        { tipo: 'evento', tom: 'ok', hora: '11:30', texto: 'Encerrada por Bruna · Agendado' }],
    },
    {
      id: 'alzira', nome: 'Alzira Pinto', telefone: '(22) 99501-2020', iniciais: 'AP', tom: 'teal', canal: 'wa', fila: 'Recepção', responsavel: 'bruna', naoLidas: 1, hora: 'ontem', ordem: 650, retencao: 'alzira',
      janela: { aberta: true, ate: 'hoje às 17:40' }, ficha: { estado: 'conferida', numero: '1.388', quando: '2024' },
      paciente: { nome: 'Alzira Pinto', numero: '1.388', idade: '71 anos · 03/04/1955', desde: 'paciente desde 2024', telefoneConfere: true, convenio: 'Unimed · 0 064 2211 3344 0', carteirinha: { texto: 'vale até 2027', tom: 'sucesso' }, documento: '···.···.···-77', autorizacao: 'Autorizou no termo do BSV · 15/01', tratamento: ['Em tratamento · BSV', 'Sem vir há 16 dias'] },
      situacao: 'retorno-respondeu',
      horarios: [], confirmacao: [], acompanhamento: [],
      mensagens: [
        { tipo: 'dia', texto: 'Quarta-feira, 22/10' },
        { tipo: 'enviada', autor: 'Bruna', hora: '15:12', estado: 'lida', modelo: 'retorno_tratamento', texto: 'Olá, Alzira! Sentimos sua falta no BSV: já são 14 dias desde a última sessão. Quer que a gente veja um horário para você?' },
        { tipo: 'dia', texto: 'Ontem · quinta-feira, 23/10' },
        { tipo: 'recebida', hora: '17:40', texto: 'Oi Bruna, obrigada por lembrar. Eu quero voltar sim, mas só em novembro, depois da viagem da minha filha', nova: true },
        { tipo: 'evento', tom: 'ok', hora: '17:40', texto: 'Respondeu ao convite · o caso saiu de "Aguardando" e pede o <b>Como foi?</b>' }],
    },
  ];
  const pac = (nome, numero, idade, convenio, autorizou, tratamento) => ({ nome, numero, idade, desde: 'paciente desde 2024', telefoneConfere: true, convenio, carteirinha: convenio === 'Particular' ? null : { texto: 'vale até 2027', tom: 'sucesso' }, documento: '···.···.···-' + numero.slice(-2), autorizacao: autorizou ? 'Autorizou no termo do BSV' : 'Não autorizou mensagens (termo de 2025) · a ação é ligar', tratamento: ['Em tratamento · ' + tratamento] });
  // RETENÇÃO = o Acompanhamento da Recepção (TipoAcompanhamento Recall e NovoBsv), lido do Clinica. Dias sem vir: os do Gerente.
  const GRUPOS = [
    { id: 'respondeu', titulo: 'Responderam · decidir o próximo passo', tom: 'sucesso' },
    { id: '30', titulo: 'Sem vir há 30 dias', tom: 'perigo' }, { id: '20', titulo: 'Sem vir há 20 dias', tom: 'aviso' }, { id: '15', titulo: 'Sem vir há 15 dias', tom: 'neutro' },
    { id: 'novo-bsv', titulo: 'Novos no BSV · como se sentiu?', tom: 'neutro' }, { id: 'retorno-pedido', titulo: 'Retornos pedidos no atendimento', tom: 'neutro' },
    { id: 'aguardando', titulo: 'Aguardando', tom: 'neutro' }, { id: 'fechado', titulo: 'Fechados esta semana', tom: 'neutro' }];
  const RETENCAO = [
    { id: 'seb', nome: 'Sebastião Ramos', iniciais: 'SR', tom: 'azul', telefone: '(22) 99120-4477', tratamento: 'BSV', grupo: '30', ultima: '22/09', dias: 32, sessoes: '6 de 10 · pacote com saldo', autorizou: true, situacao: 'para-hoje', resp: null, contatos: [], paciente: pac('Sebastião Ramos', '1.740', '63 anos · 11/02/1963', 'Unimed · 0 064 3300 1122 9', true, 'BSV') },
    { id: 'helena', nome: 'Helena Marques', iniciais: 'HM', tom: 'rosa', telefone: '(22) 99877-0101', tratamento: 'Acupuntura', grupo: '30', ultima: '24/09', dias: 30, sessoes: '3 de 10', autorizou: false, situacao: 'para-hoje', resp: null, contatos: [], paciente: pac('Helena Marques', '2.015', '49 anos · 27/08/1977', 'Bradesco Saúde · 0101 9988 7', false, 'Acupuntura') },
    { id: 'marcos', nome: 'Marcos Vinícius Teles', iniciais: 'MT', tom: 'violeta', telefone: '(22) 99311-8899', tratamento: 'BSV e acupuntura', grupo: '20', ultima: '03/10', dias: 21, sessoes: 'BSV 4 de 10 · acupuntura 7 de 10', autorizou: true, situacao: 'para-hoje', resp: 'ana', contatos: [{ quando: '17/10', texto: 'Faltou à sessão marcada · sem aviso' }], paciente: pac('Marcos Vinícius Teles', '2.388', '44 anos · 09/12/1981', 'Particular', true, 'BSV e acupuntura') },
    { id: 'fernanda', nome: 'Fernanda Nogueira', iniciais: 'FN', tom: 'rosa', telefone: '(22) 99877-4455', tratamento: 'Acupuntura', grupo: '20', ultima: '04/10', dias: 20, sessoes: '4 de 10', autorizou: true, situacao: 'aguardando', seloAguardo: 'Convite enviado ontem', resp: 'bruna', convId: 'fernanda', contatos: [{ quando: 'ontem', texto: 'Convite para voltar enviado pelo WhatsApp · Bruna' }], paciente: null },
    { id: 'rita', nome: 'Rita de Cássia Lopes', iniciais: 'RL', tom: 'ciano', telefone: '(22) 99455-6677', tratamento: 'Acupuntura', grupo: '15', ultima: '09/10', dias: 15, sessoes: '8 de 10', autorizou: true, situacao: 'para-hoje', resp: null, contatos: [], paciente: pac('Rita de Cássia Lopes', '2.771', '57 anos · 14/06/1969', 'Unimed · 0 064 7766 5544 3', true, 'Acupuntura') },
    { id: 'alzira', nome: 'Alzira Pinto', iniciais: 'AP', tom: 'teal', telefone: '(22) 99501-2020', tratamento: 'BSV', grupo: '15', ultima: '08/10', dias: 16, sessoes: '5 de 10', autorizou: true, situacao: 'respondeu', resp: 'bruna', convId: 'alzira', contatos: [{ quando: 'ontem', texto: 'Respondeu: quer voltar em novembro, depois da viagem da filha' }, { quando: '22/10', texto: 'Convite para voltar enviado pelo WhatsApp · Bruna' }], paciente: null },
    { id: 'felipe', nome: 'Felipe Arantes', iniciais: 'FA', tom: 'laranja', telefone: '(22) 99222-3131', tratamento: 'BSV', grupo: 'novo-bsv', ultima: '21/10', dias: 3, sessoes: '1ª sessão · 21/10 · Dr. Marcos', autorizou: true, situacao: 'para-hoje', resp: null, contatos: [], paciente: pac('Felipe Arantes', '3.402', '38 anos · 05/05/1988', 'Particular', true, 'BSV') },
    { id: 'claudia', nome: 'Cláudia Reis', iniciais: 'CR', tom: 'violeta', telefone: '(22) 99666-0202', tratamento: 'BSV', grupo: 'novo-bsv', ultima: '22/10', dias: 2, sessoes: '1ª sessão · 22/10 · Dr. Marcos', autorizou: true, situacao: 'para-hoje', resp: null, contatos: [], paciente: pac('Cláudia Reis', '3.410', '52 anos · 30/10/1973', 'Unimed · 0 064 9090 1212 3', true, 'BSV') },
    { id: 'paulo', nome: 'Paulo Henrique Dias', iniciais: 'PD', tom: 'azul', telefone: '(22) 99808-5050', tratamento: 'Acupuntura', grupo: 'retorno-pedido', ultima: '25/09', dias: 29, sessoes: 'Pediu retorno em 30 dias · para esta semana', autorizou: true, situacao: 'para-hoje', resp: null, contatos: [{ quando: '25/09', texto: 'Saiu do atendimento com pedido de retorno em 30 dias (Dra. Helena)' }], paciente: pac('Paulo Henrique Dias', '1.902', '61 anos · 17/03/1965', 'Unimed · 0 064 4455 6677 8', true, 'Acupuntura') },
    { id: 'ines', nome: 'Inês Barreto', iniciais: 'IB', tom: 'teal', telefone: '(22) 99700-1234', tratamento: 'Acupuntura', grupo: '20', ultima: '01/10', dias: 23, sessoes: '5 de 10', autorizou: true, situacao: 'fechado', desfecho: 'Voltou · marcado qui 30/10 09:30', resp: 'ana', contatos: [{ quando: 'qua', texto: 'Quer marcar · horário marcado para 30/10 · Ana Paula' }, { quando: 'ter', texto: 'Convite enviado pelo WhatsApp · Ana Paula' }], paciente: null },
    { id: 'geraldo', nome: 'Geraldo Nunes', iniciais: 'GN', tom: 'cinza', telefone: '(22) 99100-9900', tratamento: 'BSV', grupo: '30', ultima: '15/09', dias: 39, sessoes: '2 de 10', autorizou: true, situacao: 'fechado', desfecho: 'Não quer voltar · mudou de cidade', resp: 'bruna', contatos: [{ quando: 'seg', texto: 'Não quer voltar: mudou para Campos · Bruna' }], paciente: null },
  ];
  const itemRet = id => RETENCAO.find(r => r.id === id);
  const CANDIDATAS_TEREZINHA = [
    { id: 'pedro', nome: 'Pedro Henrique Souza', iniciais: 'PS', tom: 'violeta', nasc: '14/05/2017 · 9 anos', doc: 'final 07', resp: 'Terezinha Souza', proxima: 'sáb 25/10 09:00 · Acupuntura', ultima: '17/10' },
    { id: 'terezinha', nome: 'Terezinha Souza', iniciais: 'TS', tom: 'rosa', nasc: '02/08/1965 · 61 anos', doc: 'final 31', resp: null, proxima: 'nenhuma', ultima: '02/09 · BSV' }];
  const FICHAS_BUSCA = [
    { nome: 'Lúcia Andrade', iniciais: 'LA', tom: 'azul', detalhe: '72 anos · Unimed · última sessão 2024', numero: '1.455' },
    { nome: 'Luciana Prado Ramos', iniciais: 'LP', tom: 'teal', detalhe: '41 anos · Particular · nunca veio', numero: '2.990' },
    { nome: 'Lúcia Maria Costa', iniciais: 'LC', tom: 'rosa', detalhe: '68 anos · Bradesco Saúde · última sessão 12/09', numero: '0.633' }];

  const SINO = [
    { id: 's1', conversa: 'maria', tom: 'nao-vem', icone: 'alerta', titulo: '<strong>Maria das Graças Silva</strong> · Não vem — remarcar', meta: 'hoje 14:00 · respondeu às 09:58', naoLida: true },
    { id: 's2', conversa: 'carlos', tom: 'sem-resposta', icone: 'relogio', titulo: '<strong>Carlos Eduardo Mota</strong> · Sem resposta', meta: 'hoje 16:00 · lembrete lido às 07:12, sem resposta há 3 h', naoLida: true },
    { id: 's3', conversa: 'terezinha', tom: 'pedido', icone: 'pessoa', titulo: '<strong>Terezinha Souza</strong> · 2 fichas com este telefone', meta: '09:12 · conferir antes de responder', naoLida: true },
    { id: 's4', conversa: 'alzira', tom: 'confirmou', icone: 'check', titulo: '<strong>Alzira Pinto</strong> · respondeu ao convite para voltar', meta: 'ontem 17:40 · "quero voltar em novembro" · decidir o próximo passo', naoLida: true },
  ];

  // ---------- estado ----------
  const estado = { atual: null, modo: 'conversas', filtroRet: 'hoje', filtro: 'novas', busca: '', soNaoLidas: false, buscaConversa: null, popover: null, fichaAberta: false, mariaRespondeu: false };
  const conv = id => CONVERSAS.find(c => c.id === id);

  // O que a tela AFIRMA sobre cada conversa (no app: ConversasNaTela, com teste).
  const SELOS = {
    'nao-vem': c => ({ tom: 'perigo', texto: 'Não vem — remarcar' }),
    'sem-resposta': c => ({ tom: 'aviso', texto: 'Sem resposta · ' + (c.horarios[0] ? c.horarios[0].quando.replace('Hoje ', '') : '') }),
    'fichas': c => ({ tom: 'info', texto: '2 fichas — conferir' }),
    'novo': c => ({ tom: 'neutro', texto: 'Novo contato · sem ficha' }),
    'retorno': c => ({ tom: 'info', texto: 'Acompanhamento · 20 dias' }),
    'confirmou': c => ({ tom: 'sucesso', texto: 'Confirmou' + (c.horarios.find(x => x.estado === 'confirmado') ? ' · ' + c.horarios.find(x => x.estado === 'confirmado').quando.replace('Hoje ', '') : '') }),
    'aguardando': c => ({ tom: 'neutro', texto: 'Aguardando paciente' }),
    'remarcado': c => ({ tom: 'sucesso', texto: 'Remarcado · ' + ((c.horarios.find(x => x.novo) || {}).quando || '') }),
    'cancelado': c => ({ tom: 'neutro', texto: 'Horário cancelado' }),
    'em-atendimento': c => ({ tom: 'neutro', texto: 'Em atendimento' }),
    'encerrada': c => ({ tom: 'neutro', texto: 'Encerrada · ' + (c.encerrada ? c.encerrada.resultado : '') }),
    'retorno-respondeu': c => ({ tom: 'sucesso', texto: 'Respondeu ao convite · decidir' }),
    'retencao': c => seloRet(itemRet(c.retencao)),
  };
  function seloRet(r) {
    if (!r) return { tom: 'neutro', texto: 'Acompanhamento' };
    if (r.situacao === 'fechado') return { tom: r.desfecho.startsWith('Voltou') ? 'sucesso' : 'neutro', texto: r.desfecho };
    if (r.situacao === 'respondeu') return { tom: 'sucesso', texto: 'Respondeu · decidir o próximo passo' };
    if (r.situacao === 'aguardando') return { tom: 'info', texto: r.seloAguardo || 'Aguardando' };
    if (!r.autorizou) return { tom: 'aviso', texto: 'Sem autorização · ligar' };
    if (r.grupo === 'novo-bsv') return { tom: 'info', texto: 'Perguntar como se sentiu' };
    if (r.grupo === 'retorno-pedido') return { tom: 'info', texto: 'Retorno pedido · convidar' };
    return { tom: 'sucesso', texto: 'Pode escrever · autorizou' };
  }
  const selo = c => (SELOS[c.situacao] || SELOS['em-atendimento'])(c);
  const ultimaMensagem = c => [...c.mensagens].reverse().find(m => m.tipo === 'recebida' || m.tipo === 'enviada' || m.tipo === 'nota');
  const primeiroNome = c => c.paciente ? c.paciente.nome.split(' ')[0] : (c.nome.startsWith('(') ? '' : c.nome.split(' ')[0]);

  // ---------- notificações e modal (os componentes da casa) ----------
  function avisar(texto, tom) {
    const n = document.createElement('div'); n.className = 'notificacao ' + (tom || 'sucesso'); n.setAttribute('role', 'status');
    n.innerHTML = ic(tom === 'erro' ? 'alerta' : tom === 'info' ? 'info' : 'check') + `<span>${texto}</span><button type="button" class="fechar" aria-label="Fechar">×</button>`;
    $('.fechar', n).onclick = () => n.remove(); $('#wa-notificacoes').appendChild(n); setTimeout(() => n.remove(), 4500);
  }
  function abrirModal(o) {
    const raiz = $('#wa-modal'); raiz.hidden = false;
    raiz.innerHTML = `<div class="modal-fundo"><div class="modal ${o.largura || ''}" role="dialog" aria-modal="true" aria-label="${h(o.titulo)}" tabindex="-1">
      <header class="modal-topo"><div><h2>${h(o.titulo)}</h2>${o.subtitulo ? `<p>${o.subtitulo}</p>` : ''}</div>
        <div class="modal-topo-acoes"><button type="button" class="fechar" aria-label="Fechar" data-fechar>×</button></div></header>
      <div class="modal-corpo">${o.corpo}</div>
      ${o.rodape ? `<footer class="modal-rodape">${o.rodape}</footer>` : ''}</div></div>`;
    raiz.querySelectorAll('[data-fechar]').forEach(b => b.onclick = fecharModal);
    raiz.querySelector('.modal-fundo').onkeydown = e => { if (e.key === 'Escape') { e.stopPropagation(); fecharModal(); } };
    raiz.querySelectorAll('.wa-escolha input').forEach(r => r.addEventListener('change', () => raiz.querySelectorAll('.wa-escolha').forEach(l => l.classList.toggle('marcada', l.querySelector('input').checked))));
    if (o.aoAbrir) o.aoAbrir(raiz);
    (raiz.querySelector('input,textarea,select,button.primario') || raiz.querySelector('.modal')).focus();
  }
  function fecharModal() { const raiz = $('#wa-modal'); raiz.hidden = true; raiz.innerHTML = ''; }

  // ---------- eventos da conversa ----------
  function evento(c, texto, tom) { c.mensagens.push({ tipo: 'evento', hora: agora(), texto, tom }); }
  function limparSino(convId) { SINO.forEach(s => { if (s.conversa === convId) s.naoLida = false; }); renderSino(); }

  // ---------- a lista ----------
  const FILTROS = [
    { id: 'novas', rotulo: 'Novas', conta: () => CONVERSAS.filter(c => !c.encerrada && !c.responsavel).length, filtra: c => !c.encerrada && !c.responsavel },
    { id: 'minhas', rotulo: 'Minhas', conta: () => CONVERSAS.filter(c => !c.encerrada && c.responsavel === EU.id).length, filtra: c => !c.encerrada && c.responsavel === EU.id },
    { id: 'todas', rotulo: 'Todas', conta: () => CONVERSAS.filter(c => !c.encerrada).length, filtra: c => !c.encerrada },
    { id: 'encerradas', rotulo: 'Encerradas', conta: null, filtra: c => !!c.encerrada },
  ];
  const FILTROS_RET = [
    { id: 'hoje', rotulo: 'Para hoje', conta: () => RETENCAO.filter(r => r.situacao === 'para-hoje' || r.situacao === 'respondeu').length, filtra: r => r.situacao === 'para-hoje' || r.situacao === 'respondeu' },
    { id: 'aguardando', rotulo: 'Aguardando', conta: () => RETENCAO.filter(r => r.situacao === 'aguardando').length, filtra: r => r.situacao === 'aguardando' },
    { id: 'fechados', rotulo: 'Fechados', conta: () => RETENCAO.filter(r => r.situacao === 'fechado').length, filtra: r => r.situacao === 'fechado' },
  ];
  function renderModo() {
    const hoje = FILTROS_RET[0].conta(); const novas = FILTROS[0].conta();
    $('#wa-modo').innerHTML = `<button type="button" aria-pressed="${estado.modo === 'conversas'}" data-modo="conversas">${ic('chat')}Conversas ${novas ? `<b>${novas}</b>` : ''}</button><button type="button" aria-pressed="${estado.modo === 'retencao'}" data-modo="retencao" title="O Acompanhamento: quem está em tratamento e sumiu, os novos no BSV e os retornos pedidos">${ic('coracao')}Retenção ${hoje ? `<b>${hoje}</b>` : ''}</button>`;
    $('#wa-modo').querySelectorAll('button').forEach(b => b.onclick = () => { estado.modo = b.dataset.modo; estado.busca = ''; $('#wa-busca').value = ''; renderFiltro(); renderLista(); });
    $('#wa-busca').placeholder = estado.modo === 'retencao' ? 'Nome do paciente' : 'Nome ou telefone';
    $('#wa-so-nao-lidas').hidden = estado.modo === 'retencao';
    $('#wa-lista-rodape').innerHTML = estado.modo === 'retencao'
      ? `${ic('info')}<span>Só quem está em tratamento: BSV, acupuntura ou os dois. Dias e filas são os do Gerente (Configurações). Um por um, revisado: aqui não há disparo em massa.</span>`
      : `<span class="wa-ponto" aria-hidden="true"></span><span>(22) 3333-0000 · WhatsApp Business · conectado</span>`;
  }
  function renderFiltro() {
    renderModo();
    if (estado.modo === 'retencao') {
      $('#wa-filtro').innerHTML = FILTROS_RET.map(f => `<button type="button" aria-pressed="${estado.filtroRet === f.id}" data-filtro="${f.id}">${f.rotulo} <b>${f.conta()}</b></button>`).join('');
      $('#wa-filtro').querySelectorAll('button').forEach(b => b.onclick = () => { estado.filtroRet = b.dataset.filtro; renderFiltro(); renderLista(); });
      $('#wa-total').textContent = `${FILTROS_RET[0].conta()} para hoje`;
      return;
    }
    $('#wa-filtro').innerHTML = FILTROS.map(f => `<button type="button" aria-pressed="${estado.filtro === f.id}" data-filtro="${f.id}">${f.rotulo}${f.conta ? ` <b>${f.conta()}</b>` : ''}</button>`).join('');
    $('#wa-filtro').querySelectorAll('button').forEach(b => b.onclick = () => { estado.filtro = b.dataset.filtro; renderFiltro(); renderLista(); });
    $('#wa-total').textContent = `${CONVERSAS.filter(c => !c.encerrada).length} abertas`;
    $('#wa-so-nao-lidas').setAttribute('aria-pressed', estado.soNaoLidas);
  }
  function previa(c) {
    const m = ultimaMensagem(c); if (!m) return `<span class="texto">Sem mensagens ainda</span>`;
    if (m.tipo === 'nota') return `${ic('nota')}<span class="texto">Nota interna: ${h(m.texto)}</span>`;
    if (m.tipo === 'enviada') return `${tick(m.estado)}<span class="texto">${m.autor === EU.nome ? 'Você' : h(m.autor)}: ${h(m.texto)}</span>`;
    return `<span class="texto">${h(m.texto)}</span>`;
  }
  function tick(estado) {
    const nome = estado === 'enviando' ? 'relogio' : estado === 'enviada' ? 'tick' : 'ticks';
    return `<span class="wa-tick ${estado === 'lida' ? 'lida' : ''} ${estado === 'falhou' ? 'falhou' : ''}" title="${{ enviando: 'Enviando', enviada: 'Enviada', entregue: 'Entregue', lida: 'Lida', falhou: 'Falhou' }[estado] || ''}">${ic(estado === 'falhou' ? 'alerta' : nome)}</span>`;
  }
  const avatar = (c, cls) => `<span class="avatar tom-${c.tom} ${cls || ''}" aria-hidden="true">${h(c.iniciais)}${c.canal ? `<span class="wa-canal ${c.canal === 'sms' ? 'sms' : ''}" title="${c.canal === 'sms' ? 'SMS' : 'WhatsApp'}">${ic(c.canal === 'sms' ? 'sms' : 'chat')}</span>` : ''}</span>`;
  function renderListaRet() {
    const f = FILTROS_RET.find(x => x.id === estado.filtroRet); const q = estado.busca.trim().toLowerCase(); const raiz = $('#wa-linhas');
    const itens = RETENCAO.filter(f.filtra).filter(r => !q || r.nome.toLowerCase().includes(q));
    let html = `<div class="wa-resumo">Semana: <b>9</b> contatados · <b>6</b> voltaram (<b>67%</b>) · quem voltou sem contato não entra na conta.</div>`;
    if (!itens.length) html += `<div class="vazio-estado">${ic('check')}<strong>${q ? 'Ninguém com esse nome' : 'Ninguém para hoje'}</strong><span>${q ? 'Confira o nome.' : 'Todo mundo em tratamento veio ou já foi contatado.'}</span></div>`;
    GRUPOS.forEach(g => {
      const do_grupo = itens.filter(r => (estado.filtroRet === 'hoje' ? (r.situacao === 'respondeu' ? 'respondeu' : r.grupo) : estado.filtroRet === 'aguardando' ? 'aguardando' : 'fechado') === g.id);
      if (!do_grupo.length) return;
      html += `<div class="wa-grupo">${h(g.titulo)}<span>${do_grupo.length}</span></div>` + do_grupo.map(r => { const s = seloRet(r); const conv = r.convId ? CONVERSAS.find(c => c.id === r.convId) : null;
        return `<button type="button" role="option" class="wa-linha ${conv && conv.naoLidas ? 'nao-lida' : ''}" data-ret="${r.id}" aria-current="${estado.atual === (conv ? conv.id : 'ret-' + r.id)}">
          <span class="avatar tom-${r.tom}" aria-hidden="true">${h(r.iniciais)}</span>
          <span class="wa-linha-corpo">
            <span class="wa-linha-topo"><span class="wa-linha-nome">${h(r.nome)}</span><span class="wa-dias tom-${g.id === 'novo-bsv' ? 'sucesso' : g.tom}">${r.grupo === 'novo-bsv' ? '1ª sessão há ' + r.dias + ' d' : r.dias + ' dias'}</span></span>
            <span class="wa-linha-tratamento"><b>${h(r.tratamento)}</b> · última ${h(r.ultima)} · ${h(r.sessoes)}</span>
            <span class="wa-linha-rodape"><span class="selo tom-${s.tom}">${h(s.texto)}</span><span class="wa-linha-quem ${r.resp === EU.id ? 'minha' : ''}">${r.resp ? ic('pessoa') : ''}${h(r.resp === EU.id ? 'Você' : nomeDe(r.resp))}</span></span>
          </span></button>`; }).join('');
    });
    raiz.innerHTML = html;
    raiz.querySelectorAll('.wa-linha').forEach(b => b.onclick = () => abrirRetencao(b.dataset.ret));
  }
  function conversaDeRetencao(r) {
    if (r.convId) { const c = CONVERSAS.find(x => x.id === r.convId); if (c) { c.retencao = r.id; return c; } }
    const id = 'ret-' + r.id; let c = CONVERSAS.find(x => x.id === id); if (c) return c;
    c = { id, nome: r.nome, telefone: r.telefone, iniciais: r.iniciais, tom: r.tom, canal: 'wa', fila: 'Recepção', responsavel: r.resp, naoLidas: 0, hora: '', ordem: 0, oculta: true, retencao: r.id,
      janela: { aberta: false, desde: 'sempre: a pessoa ainda não escreveu' }, ficha: { estado: 'conferida', numero: r.paciente.numero, quando: '2024' }, paciente: r.paciente, situacao: 'retencao',
      horarios: [], confirmacao: [], acompanhamento: [], modo: 'responder', rascunho: '', mensagens: [{ tipo: 'dia', texto: 'Hoje · sexta-feira, 24/10' }] };
    CONVERSAS.push(c); return c;
  }
  function abrirRetencao(id) { const r = itemRet(id); const c = conversaDeRetencao(r); abrir(c.id); }
  function renderLista() {
    if (estado.modo === 'retencao') return renderListaRet();
    const f = FILTROS.find(x => x.id === estado.filtro); const q = estado.busca.trim().toLowerCase();
    const lista = CONVERSAS.filter(f.filtra).filter(c => !(c.oculta && !ultimaMensagem(c))).filter(c => !estado.soNaoLidas || c.naoLidas > 0)
      .filter(c => !q || c.nome.toLowerCase().includes(q) || c.telefone.replace(/\D/g, '').includes(q.replace(/\D/g, '') || '§') || selo(c).texto.toLowerCase().includes(q))
      .sort((a, b) => b.ordem - a.ordem);
    const raiz = $('#wa-linhas');
    if (!lista.length) { raiz.innerHTML = `<div class="vazio-estado">${ic('vazio')}<strong>${q ? 'Nenhuma conversa com esse nome' : estado.filtro === 'novas' ? 'Nenhuma conversa nova' : 'Nada por aqui'}</strong><span>${q ? 'Confira o nome ou o número.' : estado.filtro === 'novas' ? 'Toda conversa já tem alguém cuidando dela.' : 'As conversas aparecem aqui assim que chegam.'}</span></div>`; return; }
    raiz.innerHTML = lista.map(c => { const s = selo(c); const quem = c.responsavel === EU.id ? 'Você' : nomeDe(c.responsavel);
      return `<button type="button" role="option" class="wa-linha ${c.naoLidas ? 'nao-lida' : ''}" data-id="${c.id}" aria-current="${estado.atual === c.id}" aria-selected="${estado.atual === c.id}">
        ${avatar(c)}
        <span class="wa-linha-corpo">
          <span class="wa-linha-topo"><span class="wa-linha-nome">${h(c.nome)}</span><span class="wa-linha-hora">${h(c.hora)}</span></span>
          <span class="wa-linha-previa">${previa(c)}${c.naoLidas ? `<b class="wa-nao-lidas" aria-label="${c.naoLidas} não lidas">${c.naoLidas}</b>` : ''}</span>
          <span class="wa-linha-rodape"><span class="selo tom-${s.tom}">${h(s.texto)}</span><span class="wa-linha-quem ${c.responsavel === EU.id ? 'minha' : ''}">${c.responsavel ? ic('pessoa') : ''}${h(quem)}</span></span>
        </span></button>`; }).join('');
    raiz.querySelectorAll('.wa-linha').forEach(b => b.onclick = () => abrir(b.dataset.id));
  }

  // ---------- abrir uma conversa ----------
  function abrir(id) {
    const c = conv(id); if (!c) return;
    estado.atual = id; estado.buscaConversa = null; estado.popover = null;
    c.naoLidas = 0; limparSino(null);
    renderFiltro(); renderLista(); renderConversa(); renderFicha();
    $('#wa-conversa').classList.add('aberta');
    const t = $('#wa-texto'); if (t && window.matchMedia('(min-width: 761px)').matches) t.focus();
  }

  // ---------- a conversa ----------
  function renderConversa() {
    const raiz = $('#wa-conversa'); const c = conv(estado.atual);
    if (!c) { raiz.innerHTML = `<div class="wa-vazia"><div class="vazio-estado">${ic('chat')}<strong>Escolha uma conversa</strong><span>As novas chegam na lista ao lado em até 7 segundos. O sino avisa quem respondeu ao lembrete e precisa de você.</span></div></div>`; return; }
    const s = selo(c); const minha = c.responsavel === EU.id;
    const fichaSelo = c.ficha.estado === 'conferida' ? `<span class="selo tom-sucesso" title="Ficha conferida pela recepção em ${h(c.ficha.quando)}">Ficha ${h(c.ficha.numero)} · conferida</span>`
      : c.ficha.estado === 'duvida' ? `<button type="button" class="selo tom-info" data-acao="conferir-ficha" title="Duas fichas têm este telefone">2 fichas — conferir</button>`
      : `<button type="button" class="selo tom-neutro" data-acao="focar-busca-ficha">Sem ficha — buscar</button>`;
    raiz.innerHTML = `
      <div class="wa-cab">
        <button type="button" class="botao icone fantasma wa-voltar" data-acao="voltar" aria-label="Voltar à lista">${ic('esquerda')}</button>
        ${avatar(c)}
        <div class="wa-cab-quem"><h2>${h(c.nome)}</h2>
          <div class="wa-cab-meta"><span>${h(c.telefone)}</span><span>·</span><span>${c.canal === 'sms' ? 'SMS' : 'WhatsApp'}</span><span>·</span>${fichaSelo}</div></div>
        <div class="wa-cab-acoes">
          ${c.encerrada ? '' : minha ? `<button type="button" class="botao" data-acao="devolver" title="Devolver à fila da Recepção">${ic('pessoa')}<span>Com você</span></button>`
            : `<button type="button" class="botao primario" data-acao="assumir" title="Esta conversa passa a ser sua">${ic('pessoa')}<span>Assumir</span></button>`}
          ${c.encerrada ? '' : `<button type="button" class="botao" data-acao="transferir">${ic('transferir')}<span>Transferir</span></button>`}
          <button type="button" class="botao icone" data-acao="buscar" title="Buscar nesta conversa" aria-label="Buscar nesta conversa" aria-expanded="${estado.buscaConversa !== null}">${ic('busca')}</button>
          ${c.encerrada ? '' : `<button type="button" class="botao icone" data-acao="encerrar" title="Encerrar atendimento" aria-label="Encerrar atendimento">${ic('check')}</button>`}
          <button type="button" class="botao icone wa-so-estreito" data-acao="ficha" title="Ficha, agenda e confirmação" aria-label="Abrir a ficha do paciente" aria-expanded="${estado.fichaAberta}">${ic('ficha')}</button>
        </div>
      </div>
      <div class="wa-faixa">
        <span>Fila <b>${h(c.fila)}</b></span><i></i>
        <span>${c.responsavel ? (minha ? 'Com <b>você</b>' : 'Com <b>' + h(nomeDe(c.responsavel)) + '</b>') : 'Sem responsável'}</span><i></i>
        ${c.canal === 'sms' ? `<span class="selo tom-neutro">${ic('sms')}SMS · sem janela de 24 h</span>` : c.janela.aberta ? `<span class="selo tom-info" title="A Meta aceita texto livre por 24 h depois da última mensagem do paciente">${ic('relogio')}Janela aberta até ${h(c.janela.ate)}</span>` : `<span class="selo tom-neutro" title="Fora da janela de 24 h, só modelo aprovado">${ic('cadeado')}Janela fechada desde ${h(c.janela.desde)}</span>`}
        ${c.horarios.length ? `<i></i><span>${ic('cal')} ${h(c.horarios[0].quando)} · ${h(c.horarios[0].detalhe.split(' · ').slice(0, 2).join(' · '))}</span>` : ''}
        ${c.retencao && itemRet(c.retencao) ? `<i></i><span class="selo tom-${seloRet(itemRet(c.retencao)).tom}">${ic('coracao')}Retenção · ${h(itemRet(c.retencao).tratamento)} · ${itemRet(c.retencao).grupo === 'novo-bsv' ? '1ª sessão ' + h(itemRet(c.retencao).ultima) : 'sem vir há ' + itemRet(c.retencao).dias + ' dias'}</span>` : ''}
      </div>
      ${estado.buscaConversa !== null ? `<div class="wa-busca-conversa"><div class="busca">${ic('busca')}<input class="campo" id="wa-busca-conversa" type="search" placeholder="Buscar nesta conversa e nas notas" aria-label="Buscar nesta conversa" value="${h(estado.buscaConversa)}" autocomplete="off" /></div><small id="wa-busca-conta"></small><button type="button" class="botao icone fantasma" data-acao="buscar" aria-label="Fechar a busca">${ic('fechar')}</button></div>` : ''}
      <div class="wa-mensagens" id="wa-mensagens" role="log" aria-label="Mensagens">${renderMensagens(c)}</div>
      ${renderAcao(c)}
      ${renderEscrever(c)}`;
    raiz.querySelectorAll('[data-acao]').forEach(b => b.addEventListener('click', e => acao(b.dataset.acao, b, e)));
    ligarEscrever(c);
    const m = $('#wa-mensagens'); m.scrollTop = m.scrollHeight;
    if (estado.buscaConversa !== null) { const i = $('#wa-busca-conversa'); i.oninput = () => { estado.buscaConversa = i.value; $('#wa-mensagens').innerHTML = renderMensagens(c); contarAchados(); }; i.focus(); i.setSelectionRange(i.value.length, i.value.length); contarAchados(); }
  }
  function contarAchados() { const n = document.querySelectorAll('#wa-mensagens mark.wa-achado').length; $('#wa-busca-conta').textContent = estado.buscaConversa ? (n ? `${n} ${n === 1 ? 'trecho' : 'trechos'}` : 'Nada encontrado') : ''; }
  function marcar(texto) { const q = (estado.buscaConversa || '').trim(); const t = h(texto); if (!q) return t; const re = new RegExp(q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi'); return t.replace(re, m => `<mark class="wa-achado">${m}</mark>`); }
  function renderMensagens(c) {
    let anterior = null; let divisor = false; const partes = [];
    c.mensagens.forEach((m, i) => {
      if (m.tipo === 'dia') { partes.push(`<span class="wa-dia">${h(m.texto)}</span>`); anterior = null; return; }
      if (m.tipo === 'evento') { partes.push(`<div class="wa-evento ${m.tom || ''}">${ic(m.tom === 'ok' ? 'check' : m.tom === 'alerta' ? 'alerta' : 'info')}<span><time>${h(m.hora)}</time> · ${m.texto}</span></div>`); anterior = null; return; }
      if (m.nova && !divisor) { partes.push(`<div class="wa-novas">${c.naoLidasAoAbrir === 1 ? '1 nova mensagem' : (c.naoLidasAoAbrir || 1) + ' novas mensagens'}</div>`); divisor = true; }
      const seguida = anterior && anterior.tipo === m.tipo && anterior.autor === m.autor && anterior.hora === m.hora;
      const rodape = m.tipo === 'nota' ? '' : `<span class="wa-m-rodape">${m.tipo === 'enviada' && m.autor ? `<span class="wa-autor">${h(m.autor)} ·</span>` : ''}${m.modelo ? `<span class="wa-autor" title="Modelo aprovado ${h(m.modelo)}">modelo ·</span>` : ''}<span>${h(m.hora)}</span>${m.tipo === 'enviada' ? tick(m.estado) : ''}</span>`;
      partes.push(`<div class="wa-m ${m.tipo} ${seguida ? 'seguida' : ''} ${m.animar ? 'nova' : ''}">
        ${m.anexo ? `<span class="wa-anexo">${ic('arquivo')}${h(m.anexo)}</span>` : ''}
        <div class="wa-balao">${m.tipo === 'nota' ? `<b>${ic('nota')}Nota interna · ${h(m.autor)} · ${h(m.hora)}</b>` : ''}${marcar(m.texto)}${rodape}</div></div>`);
      m.animar = false; anterior = m;
    });
    return partes.join('');
  }
  function renderAcao(c) {
    if (c.encerrada) return '';
    const A = {
      'nao-vem': () => { const hor = c.horarios.find(x => x.estado === 'nao-vem'); return hor ? aviso('atencao', 'alerta', `${h(primeiroNome(c))} não vem ${h(hor.quando.toLowerCase())} (${h(hor.detalhe.split(' · ').slice(0, 2).join(' · '))}).`, 'Remarcar pela agenda libera o horário e grava o contato no Acompanhamento.', `<button type="button" class="botao mini primario" data-acao="remarcar" data-horario="${hor.id}">${ic('cal')}Remarcar</button><button type="button" class="botao mini" data-acao="manter" data-horario="${hor.id}">Manter o horário</button>`) : ''; },
      'sem-resposta': () => { const hor = c.horarios.find(x => x.estado === 'sem-resposta'); return hor ? aviso('atencao', 'relogio', `${h(primeiroNome(c))} leu o lembrete das 06:00 e não respondeu (${h(hor.quando.toLowerCase())}).`, 'Ligou? Registre aqui o que ele disse: a agenda do dia e o Gerente veem na hora.', `<button type="button" class="botao mini primario" data-acao="confirmar" data-horario="${hor.id}">${ic('check')}Confirmou por telefone</button><button type="button" class="botao mini contorno-perigo" data-acao="nao-vem" data-horario="${hor.id}">Não vem</button>`) : ''; },
      'fichas': () => aviso('info', 'pessoa', 'Duas fichas têm este telefone.', 'Confira com quem está no telefone antes de responder: a ficha ligada aqui nunca troca sozinha.', `<button type="button" class="botao mini primario" data-acao="conferir-ficha">Conferir a ficha</button>`),
      'novo': () => aviso('info', 'pessoa', 'Novo contato, ainda sem ficha.', 'Se a pessoa já é paciente, ligue a ficha; senão, cadastre pelo app com o telefone preenchido.', `<button type="button" class="botao mini primario" data-acao="focar-busca-ficha">${ic('busca')}Buscar ficha</button><button type="button" class="botao mini" data-acao="cadastrar">Cadastrar paciente</button>`),
      'retorno': () => aviso('info', 'cal', `${h(primeiroNome(c))} está há 20 dias sem vir (acupuntura).`, 'O convite saiu ontem pelo modelo aprovado. Quando ela responder, a janela abre e você escreve livre.', `<button type="button" class="botao mini" data-acao="ret-nao-respondeu">Não respondeu · tentar em 7 dias</button>`),
      'retorno-respondeu': () => avisoRet(c),
      'retencao': () => avisoRet(c),
    };
    return A[c.situacao] ? A[c.situacao]() : '';
  }
  const DESFECHOS = `<button type="button" class="botao mini primario" data-acao="ret-marcar">${'${ic("cal")}'}Quer marcar</button><button type="button" class="botao mini" data-acao="ret-outra-data">Pediu outra data</button><button type="button" class="botao mini" data-acao="ret-plano">Aguardando plano</button><button type="button" class="botao mini" data-acao="ret-nao-respondeu">Não respondeu</button><button type="button" class="botao mini contorno-perigo" data-acao="ret-nao-quer">Não quer voltar</button>`;
  const desfechos = () => DESFECHOS.replace('${ic("cal")}', ic('cal'));
  function avisoRet(c) {
    const r = itemRet(c.retencao); if (!r) return '';
    const nome = h(primeiroNome(c));
    if (r.situacao === 'para-hoje' && !ultimaMensagem(c)) return ''; // o bloco do convite, embaixo, já diz e já tem os botões
    if (r.situacao === 'fechado') return aviso('sucesso', 'check', `Caso fechado: ${h(r.desfecho)}.`, 'Se a pessoa escrever, a conversa continua normalmente.', '');
    if (r.situacao === 'respondeu') return aviso('sucesso', 'check', `${nome} respondeu ao convite. Como foi?`, 'O desfecho grava a etapa do Acompanhamento e o próximo contato; "Quer marcar" abre as vagas da agenda.', desfechos());
    if (r.situacao === 'aguardando') return aviso('info', 'relogio', `Convite enviado · aguardando ${nome}.`, 'Quando a pessoa responder, a janela de 24 h abre e o caso volta para "Para hoje". Sem resposta, o próximo contato é em 7 dias.', `<button type="button" class="botao mini" data-acao="ret-nao-respondeu">Não respondeu · tentar em 7 dias</button><button type="button" class="botao mini" data-acao="ret-liguei">${ic('check')}Já liguei</button>`);
    if (!r.autorizou) return aviso('atencao', 'alerta', `${nome} não autorizou mensagens: a ação é ligar.`, 'Sem a autorização colhida no termo, nenhuma mensagem sai daqui (PARAR vale para os dois canais). Ligou? Registre o que ela disse.', `<button type="button" class="botao mini primario" data-acao="ret-liguei">${ic('check')}Já liguei</button><button type="button" class="botao mini" data-acao="ret-autorizacao">Colher autorização</button>`);
    if (r.grupo === 'novo-bsv') return aviso('info', 'coracao', `${nome} fez a primeira sessão de BSV em ${h(r.ultima)}.`, 'Pergunte como se sentiu e se a próxima já está marcada. O modelo primeiro_contato já vem preenchido.', `<button type="button" class="botao mini primario" data-acao="ret-preparar">${ic('modelo')}Preparar mensagem</button><button type="button" class="botao mini" data-acao="ret-liguei">Já liguei</button>`);
    return aviso('info', 'coracao', `${nome} está há ${r.dias} dias sem vir (${h(r.tratamento)}) e autorizou mensagens.`, 'O convite para voltar é o modelo aprovado retorno_tratamento, já preenchido com nome, tratamento e dias. Você revisa e envia.', `<button type="button" class="botao mini primario" data-acao="ret-preparar">${ic('modelo')}Preparar convite</button><button type="button" class="botao mini" data-acao="ret-liguei">Já liguei</button>`);
  }
  const aviso = (tom, icone, titulo, texto, botoes) => `<div class="aviso ${tom} wa-acao" role="status">${ic(icone)}<div class="wa-acao-texto"><strong>${titulo}</strong>${texto}</div><div class="botoes-linha">${botoes}</div></div>`;

  function renderEscrever(c) {
    if (c.encerrada) return `<div class="wa-encerrada">${ic('check')}<span><strong>Encerrada ${h(c.encerrada.quando)} por ${h(c.encerrada.por)}</strong> · ${h(c.encerrada.resultado)}. Se a pessoa escrever de novo, a conversa reabre sozinha.</span><button type="button" class="botao" data-acao="reabrir">${ic('atualizar')}Reabrir</button></div>`;
    const nota = c.modo === 'nota';
    if (c.retencao && !ultimaMensagem(c) && !nota) { const r = itemRet(c.retencao); return `<div class="wa-escrever-wrap"><div class="wa-convite"><span class="wa-janela-ic">${ic('coracao')}</span>
      <div class="wa-janela-texto"><strong>${r.autorizou ? 'Primeira mensagem desta conversa: um modelo aprovado' : 'Sem autorização de mensagens'}</strong>${r.autorizou ? `${h(primeiroNome(c))} ainda não escreveu, então a Meta só aceita modelo. Depois da resposta, o campo livre abre.` : 'Nada sai daqui até a autorização ser colhida no termo. Ligue e registre como foi.'}</div>
      <div class="botoes-linha">${r.autorizou ? `<button type="button" class="botao primario" data-acao="ret-preparar">${ic('modelo')}Preparar ${r.grupo === 'novo-bsv' ? 'mensagem' : 'convite'}</button>` : `<button type="button" class="botao primario" data-acao="ret-liguei">${ic('check')}Já liguei</button>`}<button type="button" class="botao" data-acao="modo-nota">${ic('nota')}Nota interna</button></div></div><div id="wa-popover"></div></div>`; }
    if (!c.janela.aberta && c.canal !== 'sms' && !nota) return `<div class="wa-escrever-wrap"><div class="wa-janela-fechada"><span class="wa-janela-ic">${ic('cadeado')}</span>
      <div class="wa-janela-texto"><strong>Janela de 24 h fechada desde ${h(c.janela.desde)}</strong>Fora dela a Meta só aceita <b>modelo aprovado</b>. Quando ${h(primeiroNome(c) || 'a pessoa')} responder, o campo livre volta.</div>
      <div class="botoes-linha"><button type="button" class="botao primario" data-acao="modelos" aria-expanded="false">${ic('modelo')}Escolher modelo</button><button type="button" class="botao" data-acao="modo-nota">${ic('nota')}Nota interna</button></div></div><div id="wa-popover"></div></div>`;
    return `<div class="wa-escrever-wrap"><form class="wa-escrever ${nota ? 'nota' : ''}" id="wa-escrever">
      <div class="wa-escrever-abas">
        <div class="segmentado" role="group" aria-label="Tipo de mensagem">
          <button type="button" aria-pressed="${!nota}" data-acao="modo-responder" ${!c.janela.aberta && c.canal !== 'sms' ? 'title="Fora da janela: só modelo"' : ''}>${ic('chat')}Responder</button>
          <button type="button" aria-pressed="${nota}" data-acao="modo-nota">${ic('nota')}Nota interna</button></div>
        <small>${nota ? 'Só a equipe vê. Não vai ao paciente.' : `Vai pelo número oficial para ${h(c.telefone)}`}</small>
      </div>
      ${c.anexo ? `<div class="wa-rascunho-anexo">${ic('arquivo')}<span>${h(c.anexo)}</span><button type="button" class="fechar" data-acao="tirar-anexo" aria-label="Tirar o anexo">×</button></div>` : ''}
      <textarea id="wa-texto" rows="1" placeholder="${nota ? 'Escreva uma nota para a equipe…' : 'Escreva uma mensagem…'}" aria-label="${nota ? 'Nota interna' : 'Mensagem'}">${h(c.rascunho || '')}</textarea>
      <div class="wa-escrever-rodape">
        ${nota ? '' : `<button type="button" class="botao mini" data-acao="anexar">${ic('anexo')}Anexar</button>
        <button type="button" class="botao mini" data-acao="modelos" aria-expanded="false">${ic('modelo')}Modelos</button>
        <button type="button" class="botao mini" data-acao="rapidas" aria-expanded="false">${ic('bolt')}Respostas rápidas</button>`}
        <span class="wa-atalho">Ctrl + Enter ${nota ? 'guarda' : 'envia'}</span>
        <button type="submit" class="botao primario" id="wa-enviar" ${(c.rascunho || c.anexo) ? '' : 'disabled'}>${ic(nota ? 'nota' : 'enviar')}${nota ? 'Guardar nota' : 'Enviar'}</button>
      </div>
      <input type="file" id="wa-arquivo" hidden accept="image/png,image/jpeg,application/pdf,audio/*,video/mp4" />
    </form><div id="wa-popover"></div></div>`;
  }
  function ligarEscrever(c) {
    const f = $('#wa-escrever'); if (!f) return;
    const t = $('#wa-texto'); const env = $('#wa-enviar');
    const ajustar = () => { t.style.height = 'auto'; t.style.height = Math.min(t.scrollHeight, 180) + 'px'; };
    ajustar();
    t.oninput = () => { c.rascunho = t.value; env.disabled = !(t.value.trim() || c.anexo); ajustar(); };
    t.onkeydown = e => { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); enviar(c); } };
    f.onsubmit = e => { e.preventDefault(); enviar(c); };
    const arq = $('#wa-arquivo'); if (arq) arq.onchange = () => { if (arq.files[0]) { c.anexo = `${arq.files[0].name} · ${Math.max(1, Math.round(arq.files[0].size / 1024))} KB`; renderConversa(); } };
  }
  function enviar(c) {
    const texto = (c.rascunho || '').trim(); if (!texto && !c.anexo) return;
    const hora = agora();
    if (c.modo === 'nota') { c.mensagens.push({ tipo: 'nota', autor: EU.nome, hora, texto, animar: true }); c.rascunho = ''; c.modo = 'responder'; renderConversa(); renderLista(); avisar('Nota guardada. Só a equipe vê.', 'info'); return; }
    const m = { tipo: 'enviada', autor: EU.nome, hora, texto: texto || 'Arquivo', estado: 'enviando', anexo: c.anexo, animar: true };
    c.mensagens.push(m); c.rascunho = ''; c.anexo = null; c.hora = hora; c.ordem = proximaOrdem();
    if (!c.responsavel) { c.responsavel = EU.id; evento(c, 'Ana Paula assumiu a conversa ao responder'); }
    if (c.situacao === 'em-atendimento' || c.situacao === 'novo') { /* o selo continua o mesmo */ }
    renderConversa(); renderFiltro(); renderLista();
    setTimeout(() => { m.estado = 'enviada'; atualizarTicks(c); }, 500);
    setTimeout(() => { m.estado = 'entregue'; atualizarTicks(c); }, 1400);
    if (c.id === 'maria' && !estado.mariaRespondeu) { estado.mariaRespondeu = true; setTimeout(() => { m.estado = 'lida'; atualizarTicks(c); }, 2600); setTimeout(() => chegou('maria', 'Obrigada, Ana! Até terça então 🙏'), 4200); }
  }
  function atualizarTicks(c) { if (estado.atual === c.id) { const m = $('#wa-mensagens'); const topo = m.scrollTop; m.innerHTML = renderMensagens(c); m.scrollTop = topo; } renderLista(); }
  function chegou(id, texto) {
    const c = conv(id); const hora = agora(); c.hora = hora; c.ordem = proximaOrdem();
    const m = { tipo: 'recebida', hora, texto, animar: true };
    c.janela = { aberta: true, ate: 'amanhã às ' + hora };
    if (c.retencao && itemRet(c.retencao)) { const r = itemRet(c.retencao); if (r.situacao === 'aguardando') { r.situacao = 'respondeu'; r.contatos.unshift({ quando: 'Hoje', texto: 'Respondeu ao convite: "' + texto + '"' }); c.situacao = 'retencao'; c.mensagens.push(m); c.mensagens.push({ tipo: 'evento', tom: 'ok', hora, texto: 'Respondeu ao convite · o caso pede o <b>Como foi?</b>' }); SINO.unshift({ id: 's' + Date.now(), conversa: c.id, tom: 'confirmou', icone: 'check', titulo: `<strong>${h(c.nome)}</strong> · respondeu ao convite para voltar`, meta: hora + ' · decidir o próximo passo', naoLida: estado.atual !== id }); if (estado.atual === id) { renderConversa(); renderFicha(); } else { c.naoLidas++; c.naoLidasAoAbrir = c.naoLidas; m.nova = true; avisar(`<b>${h(c.nome)}</b> respondeu ao convite: ${h(texto)}`, 'info'); } renderSino(); renderFiltro(); renderLista(); return; } }
    if (estado.atual === id) { c.mensagens.push(m); const r = $('#wa-mensagens'); r.innerHTML = renderMensagens(c); r.scrollTop = r.scrollHeight; }
    else { m.nova = true; c.naoLidas++; c.naoLidasAoAbrir = c.naoLidas; c.mensagens.push(m); avisar(`<b>${h(c.nome)}</b>: ${h(texto)}`, 'info'); }
    renderFiltro(); renderLista();
  }

  // ---------- ações ----------
  function acao(nome, botao) {
    const c = conv(estado.atual);
    switch (nome) {
      case 'voltar': $('#wa-conversa').classList.remove('aberta'); estado.atual = null; renderLista(); break;
      case 'ficha': estado.fichaAberta = !estado.fichaAberta; $('#wa-ficha').classList.toggle('aberta', estado.fichaAberta); botao.setAttribute('aria-expanded', estado.fichaAberta); break;
      case 'fechar-ficha': estado.fichaAberta = false; $('#wa-ficha').classList.remove('aberta'); break;
      case 'assumir': c.responsavel = EU.id; evento(c, 'Ana Paula assumiu a conversa'); renderTudo(); avisar('A conversa é sua. Ela aparece em "Minhas".'); break;
      case 'devolver': c.responsavel = null; evento(c, 'Ana Paula devolveu a conversa à fila da Recepção'); renderTudo(); avisar('Devolvida à fila da Recepção.', 'info'); break;
      case 'transferir': modalTransferir(c); break;
      case 'encerrar': modalEncerrar(c); break;
      case 'reabrir': c.encerrada = null; c.situacao = 'em-atendimento'; c.responsavel = EU.id; evento(c, 'Reaberta por Ana Paula'); estado.filtro = 'minhas'; renderTudo(); avisar('Conversa reaberta e com você.'); break;
      case 'buscar': estado.buscaConversa = estado.buscaConversa === null ? '' : null; renderConversa(); break;
      case 'modo-nota': c.modo = 'nota'; renderConversa(); $('#wa-texto') && $('#wa-texto').focus(); break;
      case 'modo-responder': if (!c.janela.aberta && c.canal !== 'sms') { avisar('Fora da janela de 24 h só vai modelo aprovado. Use "Escolher modelo".', 'info'); } c.modo = 'responder'; renderConversa(); $('#wa-texto') && $('#wa-texto').focus(); break;
      case 'anexar': $('#wa-arquivo').click(); break;
      case 'tirar-anexo': c.anexo = null; renderConversa(); break;
      case 'modelos': popover('modelos', botao, c); break;
      case 'rapidas': popover('rapidas', botao, c); break;
      case 'remarcar': modalRemarcar(c, botao.dataset.horario); break;
      case 'manter': { const hor = c.horarios.find(x => x.id === botao.dataset.horario); hor.estado = 'marcado'; c.situacao = 'em-atendimento'; evento(c, `Horário de ${h(hor.quando.toLowerCase())} mantido por Ana Paula`); limparSino(c.id); renderTudo(); avisar('Horário mantido. O aviso saiu do sino.', 'info'); break; }
      case 'confirmar': { const hor = c.horarios.find(x => x.id === botao.dataset.horario); hor.estado = 'confirmado'; c.situacao = 'confirmou'; c.confirmacao.push({ hora: agora(), texto: 'Confirmou por telefone · registrado por Ana Paula' }); evento(c, `Confirmou por telefone (registrado por Ana Paula) · a agenda do dia mostra o selo verde`, 'ok'); limparSino(c.id); renderTudo(); avisar('Confirmado. A agenda do dia e o Gerente já veem.'); break; }
      case 'nao-vem': { const hor = c.horarios.find(x => x.id === botao.dataset.horario); hor.estado = 'nao-vem'; c.situacao = 'nao-vem'; evento(c, `Não vem (registrado por Ana Paula) · aviso <b>Não vem — remarcar</b> no sino e no Gerente`, 'alerta'); renderTudo(); avisar('Registrado: não vem. Agora remarque ou libere o horário.', 'info'); break; }
      case 'cancelar': modalCancelar(c, botao.dataset.horario); break;
      case 'conferir-ficha': modalConferirFicha(c); break;
      case 'focar-busca-ficha': { estado.fichaAberta = true; $('#wa-ficha').classList.add('aberta'); const i = $('#wa-busca-ficha'); if (i) { i.focus(); } break; }
      case 'cadastrar': avisar('No app, abre o cadastro de paciente com o telefone já preenchido.', 'info'); break;
      case 'acompanhamento': avisar('No app, abre o Acompanhamento no caso dela.', 'info'); break;
      case 'abrir-ficha': avisar('No app, abre a Ficha do balcão deste paciente.', 'info'); break;
      case 'abrir-agenda': avisar('No app, abre a Agenda do dia neste horário.', 'info'); break;
      case 'nova-conversa': modalNovaConversa(); break;
      case 'ret-preparar': { const b = $('[data-acao="ret-preparar"]'); popover('modelos', b, c); const r = itemRet(c.retencao); formModelo(c, MODELOS.find(m => m.id === (r.grupo === 'novo-bsv' ? 'primeiro_contato' : 'retorno_tratamento'))); break; }
      case 'ret-liguei': modalLigacao(c); break;
      case 'ret-autorizacao': avisar('No app, abre a coleta da autorização de mensagens: no tablet do balcão ou pelo link no celular da pessoa.', 'info'); break;
      case 'ret-marcar': modalMarcarRetorno(c); break;
      case 'ret-outra-data': modalOutraData(c); break;
      case 'ret-plano': fecharCaso(c, 'aguardando', 'Aguardando plano do médico', 'Pediu para falar com o médico sobre o plano · próximo contato quando o plano sair', 'Etapa gravada: aguardando plano. O caso volta quando o médico responder.'); break;
      case 'ret-nao-respondeu': fecharCaso(c, 'aguardando', 'Sem resposta · tentar em 7 dias', 'Sem resposta · próximo contato em 7 dias (31/10)', 'Sem resposta. O caso volta para "Para hoje" em 7 dias.'); break;
      case 'ret-nao-quer': modalNaoQuer(c); break;
    }
  }
  function fecharCaso(c, situacao, selo, contato, toast, desfecho) {
    const r = itemRet(c.retencao); if (!r) return;
    r.situacao = situacao; r.seloAguardo = selo; if (desfecho) r.desfecho = desfecho; r.resp = r.resp || EU.id;
    r.contatos.unshift({ quando: 'Hoje', texto: contato + ' · Ana Paula' }); evento(c, contato + ' (Ana Paula) · gravado no Acompanhamento', situacao === 'fechado' && desfecho && desfecho.startsWith('Voltou') ? 'ok' : undefined);
    if (c.situacao === 'retorno-respondeu') c.situacao = 'retencao';
    limparSino(c.id); renderTudo(); avisar(toast, situacao === 'fechado' && desfecho && !desfecho.startsWith('Voltou') ? 'info' : 'sucesso');
  }
  function modalLigacao(c) {
    const opcoes = [['Quer marcar', 'Abre as vagas da agenda'], ['Pediu outra data', 'Volta a falar numa data combinada'], ['Aguardando plano', 'Quer falar com o médico antes'], ['Não atendeu', 'Tentar de novo em 2 dias'], ['Não quer voltar', 'Encerra o caso com o motivo']];
    abrirModal({ titulo: 'Como foi a ligação?', subtitulo: `${h(c.nome)} · ${h(c.telefone)}. O que ela disse grava a etapa do Acompanhamento.`, largura: 'pequeno',
      corpo: `<div class="wa-opcoes" role="radiogroup" aria-label="Como foi"><label class="wa-escolha sem-avatar marcada"><input type="radio" name="wa-lig" value="0" checked /><span class="wa-escolha-texto"><strong>${opcoes[0][0]}</strong><span>${opcoes[0][1]}</span></span></label>${opcoes.slice(1).map((o, i) => `<label class="wa-escolha sem-avatar"><input type="radio" name="wa-lig" value="${i + 1}" /><span class="wa-escolha-texto"><strong>${o[0]}</strong><span>${o[1]}</span></span></label>`).join('')}</div>`,
      rodape: `<button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao primario" id="wa-ok">Registrar</button>`,
      aoAbrir: raiz => { $('#wa-ok', raiz).onclick = () => { const i = +raiz.querySelector('input[name="wa-lig"]:checked').value; fecharModal();
        if (i === 0) modalMarcarRetorno(c, 'Ligação · quer marcar');
        else if (i === 1) modalOutraData(c);
        else if (i === 2) fecharCaso(c, 'aguardando', 'Aguardando plano do médico', 'Ligação · quer falar com o médico sobre o plano', 'Etapa gravada: aguardando plano.');
        else if (i === 3) fecharCaso(c, 'aguardando', 'Não atendeu · tentar em 2 dias', 'Ligação · não atendeu · próximo contato em 2 dias', 'Não atendeu. O caso volta em 2 dias.');
        else modalNaoQuer(c); }; } });
  }
  function modalMarcarRetorno(c, origem) {
    const vagas = [{ q: 'Ter 28/10 · 09:00', d: 'Dr. Marcos · sala 1' }, { q: 'Qua 29/10 · 10:30', d: 'Dr. Marcos · sala 1' }, { q: 'Qui 30/10 · 09:30', d: 'Dra. Helena · sala 2' }, { q: 'Seg 03/11 · 08:30', d: 'Dr. Marcos · sala 1' }];
    let escolhida = 0; const r = itemRet(c.retencao);
    abrirModal({ titulo: `Marcar o retorno de ${h(primeiroNome(c))}`, subtitulo: `${h(r.tratamento)} · as vagas são as da agenda da Recepção, no profissional que trata. O caso fecha como "Voltou" quando a sessão acontecer.`,
      corpo: `<div class="wa-vagas" role="group" aria-label="Vagas">${vagas.map((v, i) => `<button type="button" class="wa-vaga" data-i="${i}" aria-pressed="${i === 0}"><b>${h(v.q)}</b><span>${h(v.d)}</span></button>`).join('')}</div><p class="wa-dica">Outra data? "Ver a agenda" abre a grade da semana no profissional.</p>`,
      rodape: `<button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao primario" id="wa-ok">Marcar</button>`,
      aoAbrir: raiz => { raiz.querySelectorAll('.wa-vaga').forEach(b => b.onclick = () => { escolhida = +b.dataset.i; raiz.querySelectorAll('.wa-vaga').forEach(x => x.setAttribute('aria-pressed', x === b)); });
        $('#wa-ok', raiz).onclick = () => { const v = vagas[escolhida]; fecharModal(); c.horarios.unshift({ id: 'r' + Date.now(), quando: v.q, detalhe: `${r.tratamento} · ${v.d} · o lembrete sai na manhã do dia`, estado: 'marcado', novo: true });
          fecharCaso(c, 'fechado', null, `${origem || 'Quer marcar'} · horário marcado para ${v.q}`, `Marcado para ${h(v.q)}. O caso fecha como "Voltou" quando a sessão acontecer.`, 'Voltou · marcado ' + v.q.toLowerCase());
          if (!c.rascunho && c.janela.aberta) { c.rascunho = `Combinado, ${primeiroNome(c)}! Marquei ${r.tratamento.toLowerCase() === 'bsv' ? 'o BSV' : 'a ' + r.tratamento.toLowerCase()} para ${v.q.toLowerCase().replace(' · ', ' às ')} com ${v.d.split(' · ')[0]}. O lembrete chega na manhã do dia 🙂`; renderConversa(); } }; } });
  }
  function modalOutraData(c) {
    abrirModal({ titulo: 'Pediu outra data', subtitulo: 'O caso sai de "Para hoje" e volta na data combinada.', largura: 'pequeno',
      corpo: `<div class="campo-bloco"><label class="campo-rotulo" for="wa-quando">Voltar a falar em</label><select class="campo" id="wa-quando"><option>7 dias (31/10)</option><option>15 dias (08/11)</option><option selected>30 dias (23/11)</option><option>60 dias (23/12)</option></select></div>`,
      rodape: `<button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao primario" id="wa-ok">Gravar</button>`,
      aoAbrir: raiz => { $('#wa-ok', raiz).onclick = () => { const q = $('#wa-quando', raiz).value; fecharModal(); fecharCaso(c, 'aguardando', 'Retomar em ' + q.split(' (')[0], 'Pediu outra data · próximo contato em ' + q, 'Gravado. O caso volta em ' + q + '.'); }; } });
  }
  function modalNaoQuer(c) {
    abrirModal({ titulo: 'Não quer voltar', subtitulo: 'O caso encerra com o motivo. Se a pessoa pediu para não receber mensagens, o PARAR vale para WhatsApp e SMS, aqui e no Clinica.', largura: 'pequeno',
      corpo: `<div class="campo-bloco"><label class="campo-rotulo" for="wa-nq">Motivo <span class="obrigatorio">*</span></label><select class="campo" id="wa-nq"><option value="">Escolha…</option><option>Melhorou e não precisa</option><option>Mudou de cidade</option><option>Custo</option><option>Foi para outra clínica</option><option>Outro</option></select></div>
        <label class="marcar"><input type="checkbox" id="wa-parar" /> Pediu para não receber mais mensagens (PARAR)</label>`,
      rodape: `<button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao perigo" id="wa-ok" disabled>Encerrar o caso</button>`,
      aoAbrir: raiz => { const sel = $('#wa-nq', raiz); sel.onchange = () => $('#wa-ok', raiz).disabled = !sel.value; $('#wa-ok', raiz).onclick = () => { const parar = $('#wa-parar', raiz).checked; fecharModal(); const r = itemRet(c.retencao); if (parar) { r.autorizou = false; if (c.paciente) c.paciente.autorizacao = 'Pediu PARAR · hoje · vale para WhatsApp e SMS'; evento(c, 'PARAR registrado por Ana Paula · a autorização de mensagens foi revogada no Clinica', 'alerta'); } fecharCaso(c, 'fechado', null, 'Não quer voltar · ' + sel.value, 'Caso encerrado · ' + sel.value + (parar ? ' · PARAR registrado.' : '.'), 'Não quer voltar · ' + sel.value.toLowerCase()); }; } });
  }
  function renderTudo() { renderFiltro(); renderLista(); renderConversa(); renderFicha(); renderSino(); }

  // ---------- popovers: modelos e respostas rápidas ----------
  function popover(tipo, botao, c) {
    const raiz = $('#wa-popover'); if (!raiz) return;
    if (estado.popover === tipo) { estado.popover = null; raiz.innerHTML = ''; botao.setAttribute('aria-expanded', 'false'); return; }
    estado.popover = tipo; document.querySelectorAll('[data-acao="modelos"],[data-acao="rapidas"]').forEach(b => b.setAttribute('aria-expanded', b === botao));
    if (tipo === 'rapidas') {
      raiz.innerHTML = `<div class="wa-popover" role="dialog" aria-label="Respostas rápidas"><div class="wa-popover-topo"><h3>Respostas rápidas</h3><small>Entram no campo para você revisar</small><button type="button" class="botao icone fantasma" data-fechar-popover aria-label="Fechar">${ic('fechar')}</button></div>
        <div class="wa-popover-lista">${RAPIDAS.map((r, i) => `<button type="button" class="wa-opcao" data-i="${i}"><strong>${h(r.titulo)}</strong><span>${h(r.texto)}</span></button>`).join('')}</div></div>`;
      raiz.querySelectorAll('.wa-opcao').forEach(b => b.onclick = () => { const r = RAPIDAS[+b.dataset.i]; c.rascunho = (c.rascunho ? c.rascunho + '\n' : '') + r.texto; estado.popover = null; renderConversa(); const t = $('#wa-texto'); t.focus(); t.setSelectionRange(t.value.length, t.value.length); });
    } else {
      raiz.innerHTML = `<div class="wa-popover" role="dialog" aria-label="Modelos do WhatsApp"><div class="wa-popover-topo"><h3>Modelos aprovados pela Meta</h3><small>${c.janela.aberta ? 'Opcional dentro da janela' : 'Obrigatório fora da janela'}</small><button type="button" class="botao icone fantasma" data-fechar-popover aria-label="Fechar">${ic('fechar')}</button></div>
        <div class="wa-popover-lista" id="wa-modelos-lista">${MODELOS.map(m => `<button type="button" class="wa-opcao" data-id="${m.id}"><strong>${h(m.id)} <span class="selo tom-sucesso">${h(m.categoria)}</span></strong><span>${h(m.texto)}</span></button>`).join('')}</div>
        <div id="wa-modelo-form"></div></div>`;
      raiz.querySelectorAll('.wa-opcao').forEach(b => b.onclick = () => formModelo(c, MODELOS.find(m => m.id === b.dataset.id)));
    }
    raiz.querySelectorAll('[data-fechar-popover]').forEach(b => b.onclick = () => { estado.popover = null; raiz.innerHTML = ''; document.querySelectorAll('[aria-expanded]').forEach(x => x.setAttribute('aria-expanded', 'false')); });
  }
  function formModelo(c, m) {
    const hor = c.horarios[0]; const r = c.retencao ? itemRet(c.retencao) : null;
    const sugestoes = { 'Primeiro nome': primeiroNome(c), 'Atendimento': hor ? hor.detalhe.split(' · ')[0].toLowerCase() : '', 'Dia e hora': hor ? hor.quando.toLowerCase().replace('hoje', 'hoje às') : '',
      'Tratamento': r ? (r.tratamento.startsWith('BSV') ? 'sessão de ' + r.tratamento : r.tratamento.toLowerCase()) : 'acupuntura', 'Dias': r ? String(r.dias) : '20', 'Documento': 'atestado', 'Assunto': r && r.grupo === 'novo-bsv' ? 'a primeira sessão de BSV: como você se sentiu?' : 'acupuntura' };
    const valores = m.params.map(p => sugestoes[p] || '');
    const f = $('#wa-modelo-form'); $('#wa-modelos-lista').hidden = true;
    const previa = () => m.texto.replace(/\{\{(\d)\}\}/g, (_, n) => `<var>${h(valores[+n - 1] || '{{' + n + '}}')}</var>`);
    f.innerHTML = `<div class="wa-modelo-form"><div class="campos">${m.params.map((p, i) => `<div class="campo-bloco"><label class="campo-rotulo" for="wa-p${i}">${h(p)} <span class="obrigatorio">*</span></label><input class="campo" id="wa-p${i}" value="${h(valores[i])}" /></div>`).join('')}</div>
      <div class="wa-modelo-previa" id="wa-modelo-previa">${previa()}</div>
      <div class="botoes-linha"><button type="button" class="botao" id="wa-modelo-voltar">${ic('esquerda')}Outro modelo</button><button type="button" class="botao primario" id="wa-modelo-enviar">${ic('enviar')}Enviar modelo</button></div></div>`;
    m.params.forEach((p, i) => $('#wa-p' + i).oninput = e => { valores[i] = e.target.value; $('#wa-modelo-previa').innerHTML = previa(); $('#wa-modelo-enviar').disabled = valores.some(v => !v.trim()); });
    $('#wa-modelo-enviar').disabled = valores.some(v => !v.trim());
    $('#wa-modelo-voltar').onclick = () => { $('#wa-modelos-lista').hidden = false; f.innerHTML = ''; };
    $('#wa-modelo-enviar').onclick = () => {
      const texto = m.texto.replace(/\{\{(\d)\}\}/g, (_, n) => valores[+n - 1]); const hora = agora();
      const msg = { tipo: 'enviada', autor: EU.nome, hora, texto, estado: 'enviando', modelo: m.id, animar: true };
      c.mensagens.push(msg); c.hora = hora; c.ordem = proximaOrdem(); estado.popover = null;
      if (!c.responsavel) { c.responsavel = EU.id; evento(c, 'Ana Paula assumiu a conversa ao enviar o modelo'); }
      if (c.retencao && itemRet(c.retencao)) { const r = itemRet(c.retencao); r.situacao = 'aguardando'; r.seloAguardo = 'Convite enviado hoje'; r.resp = r.resp || EU.id; c.oculta = false; c.hora = hora; c.ordem = proximaOrdem(); r.contatos.unshift({ quando: 'Hoje', texto: `${m.id === 'primeiro_contato' ? 'Mensagem' : 'Convite para voltar'} enviado pelo WhatsApp · Ana Paula (registrado sozinho)` }); evento(c, 'Contato gravado no Acompanhamento · etapa: aguardando resposta'); if (r.id === 'seb') setTimeout(() => chegou(c.id, 'Oi Ana! Pode ser sim, semana que vem de manhã tem horário?'), 4500); }
      renderConversa(); renderFiltro(); renderLista(); avisar(`Modelo <b>${h(m.id)}</b> enviado. Quando a pessoa responder, a janela de 24 h abre.`);
      setTimeout(() => { msg.estado = 'enviada'; atualizarTicks(c); }, 500); setTimeout(() => { msg.estado = 'entregue'; atualizarTicks(c); }, 1500);
    };
    $('#wa-p0').focus();
  }

  // ---------- formulários (Modal da casa) ----------
  function modalTransferir(c) {
    abrirModal({ titulo: 'Transferir a conversa', subtitulo: `${h(c.nome)} passa a ser de quem você escolher. A pessoa vê a nota ao abrir.`, largura: 'pequeno',
      corpo: `<div class="wa-opcoes" role="radiogroup" aria-label="Para quem">${EQUIPE.filter(e => e.id !== EU.id).map((e, i) => `<label class="wa-escolha sem-avatar ${i === 0 ? 'marcada' : ''}"><input type="radio" name="wa-para" value="${e.id}" ${i === 0 ? 'checked' : ''} /><span class="wa-escolha-texto"><strong>${h(e.nome)}</strong><span>${e.id === 'paulo' ? 'Financeiro · cobrança e recibos' : 'Recepção · online agora'}</span></span></label>`).join('')}</div>
        <div class="campo-bloco"><label class="campo-rotulo" for="wa-nota-transf">Nota para quem recebe <span class="texto-secundario">(opcional)</span></label><textarea class="campo" id="wa-nota-transf" rows="2" placeholder="Ex.: paciente pediu o valor do pacote"></textarea></div>`,
      rodape: `<button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao primario" id="wa-ok">Transferir</button>`,
      aoAbrir: raiz => { $('#wa-ok', raiz).onclick = () => { const para = raiz.querySelector('input[name="wa-para"]:checked').value; const nota = $('#wa-nota-transf', raiz).value.trim(); c.responsavel = para; evento(c, `Transferida por Ana Paula para ${h(nomeDe(para))}${nota ? ' · "' + h(nota) + '"' : ''}`); if (nota) c.mensagens.push({ tipo: 'nota', autor: EU.nome, hora: agora(), texto: nota }); fecharModal(); renderTudo(); avisar(`Transferida para ${h(nomeDe(para))}.`); }; } });
  }
  function modalEncerrar(c) {
    const opcoes = [['Resolvido', 'A pessoa teve o que pediu'], ['Agendado', 'Saiu com horário marcado'], ['Sem resposta', 'Ficou sem retorno depois de 48 h'], ['Encaminhado', 'Seguiu para outro setor ou para a ligação']];
    abrirModal({ titulo: 'Encerrar o atendimento', subtitulo: 'A conversa sai da lista. Se a pessoa escrever de novo, ela reabre sozinha.', largura: 'pequeno',
      corpo: `<div class="wa-opcoes" role="radiogroup" aria-label="Como terminou">${opcoes.map((o, i) => `<label class="wa-escolha sem-avatar ${i === 0 ? 'marcada' : ''}"><input type="radio" name="wa-fim" value="${o[0]}" ${i === 0 ? 'checked' : ''} /><span class="wa-escolha-texto"><strong>${o[0]}</strong><span>${o[1]}</span></span></label>`).join('')}</div>`,
      rodape: `<span class="esquerda">O resultado alimenta o resumo do dia que o Gerente lê.</span><button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao primario" id="wa-ok">Encerrar</button>`,
      aoAbrir: raiz => { $('#wa-ok', raiz).onclick = () => { const r = raiz.querySelector('input[name="wa-fim"]:checked').value; c.encerrada = { quando: 'às ' + agora(), por: EU.nome, resultado: r }; c.situacao = 'encerrada'; evento(c, `Encerrada por Ana Paula · ${h(r)}`, 'ok'); limparSino(c.id); fecharModal(); renderTudo(); avisar(`Encerrada · ${h(r)}.`); }; } });
  }
  function modalRemarcar(c, horarioId) {
    const hor = c.horarios.find(x => x.id === horarioId);
    const vagas = [{ q: 'Ter 28/10 · 14:00', d: 'Dra. Helena · sala 2', s: 'a paciente pediu' }, { q: 'Qui 30/10 · 09:30', d: 'Dra. Helena · sala 2' }, { q: 'Sex 31/10 · 15:00', d: 'Dra. Helena · sala 2' }, { q: 'Seg 03/11 · 14:00', d: 'Dra. Helena · sala 1' }];
    let escolhida = 0;
    abrirModal({ titulo: `Remarcar ${h(hor.quando.toLowerCase())}`, subtitulo: `${h(hor.detalhe)}. As vagas são as da agenda da Recepção, com as regras dela: o horário de ${h(hor.quando.split(' ')[0].toLowerCase())} é liberado e o lembrete sai na manhã do dia novo.`,
      corpo: `<div class="wa-vagas" role="group" aria-label="Vagas">${vagas.map((v, i) => `<button type="button" class="wa-vaga" data-i="${i}" aria-pressed="${i === 0}"><b>${h(v.q)}</b><span>${h(v.d)}</span>${v.s ? `<span class="selo tom-info">${h(v.s)}</span>` : ''}</button>`).join('')}</div>
        <p class="wa-dica">Outra data? No app, "Ver a agenda" abre a grade da semana já no profissional dela.</p>`,
      rodape: `<button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao" id="wa-agenda">${ic('cal')}Ver a agenda</button><button type="button" class="botao primario" id="wa-ok">Remarcar</button>`,
      aoAbrir: raiz => {
        raiz.querySelectorAll('.wa-vaga').forEach(b => b.onclick = () => { escolhida = +b.dataset.i; raiz.querySelectorAll('.wa-vaga').forEach(x => x.setAttribute('aria-pressed', x === b)); });
        $('#wa-agenda', raiz).onclick = () => avisar('No app, abre a grade da agenda na semana da Dra. Helena.', 'info');
        $('#wa-ok', raiz).onclick = () => {
          const v = vagas[escolhida]; const antes = hor.quando;
          hor.estado = 'remarcado'; hor.para = v.q;
          c.horarios.unshift({ id: 'n' + Date.now(), quando: v.q, detalhe: `${hor.detalhe.split(' · ')[0]} · ${v.d} · o lembrete sai na manhã do dia`, estado: 'marcado', novo: true });
          c.situacao = 'remarcado'; evento(c, `Remarcado por Ana Paula: ${h(antes.toLowerCase())} → ${h(v.q.toLowerCase())} · horário de ${h(antes.split(' ')[0].toLowerCase())} liberado · contato gravado no Acompanhamento`, 'ok');
          c.acompanhamento.unshift({ quando: 'Hoje', texto: 'Contato pelo WhatsApp · Ana Paula (registrado sozinho)' });
          if (!c.rascunho) c.rascunho = `Combinado, ${primeiroNome(c)}! Remarquei sua ${hor.detalhe.split(' · ')[0].toLowerCase()} para ${v.q.toLowerCase().replace(' · ', ' às ')} com a ${v.d.split(' · ')[0]}. O lembrete chega na manhã do dia. Até lá 🙂`;
          limparSino(c.id); fecharModal(); renderTudo(); avisar(`Remarcado para ${h(v.q)}. Agora avise ${h(primeiroNome(c))}: a mensagem já está pronta para você revisar.`);
          const t = $('#wa-texto'); if (t) { t.focus(); t.setSelectionRange(t.value.length, t.value.length); }
        }; } });
  }
  function modalCancelar(c, horarioId) {
    const hor = c.horarios.find(x => x.id === horarioId);
    abrirModal({ titulo: `Cancelar ${h(hor.quando.toLowerCase())}`, subtitulo: `${h(hor.detalhe)}. O horário é liberado e as guias do dia ficam suspensas (regra da agenda).`, largura: 'pequeno',
      corpo: `<div class="campo-bloco"><label class="campo-rotulo" for="wa-motivo">Motivo <span class="obrigatorio">*</span></label><select class="campo" id="wa-motivo"><option value="">Escolha…</option><option>Paciente pediu</option><option>Profissional ausente</option><option>Convênio não autorizou</option><option>Outro</option></select></div>
        <div class="campo-bloco"><label class="campo-rotulo" for="wa-motivo-texto">Detalhe <span class="texto-secundario">(opcional)</span></label><textarea class="campo" id="wa-motivo-texto" rows="2"></textarea></div>`,
      rodape: `<button type="button" class="botao" data-fechar>Voltar</button><button type="button" class="botao perigo" id="wa-ok" disabled>Cancelar o horário</button>`,
      aoAbrir: raiz => { const sel = $('#wa-motivo', raiz); sel.onchange = () => $('#wa-ok', raiz).disabled = !sel.value; $('#wa-ok', raiz).onclick = () => { hor.estado = 'cancelado'; hor.motivo = sel.value; c.situacao = c.situacao === 'nao-vem' || c.situacao === 'sem-resposta' ? 'cancelado' : c.situacao; evento(c, `Horário de ${h(hor.quando.toLowerCase())} cancelado por Ana Paula · ${h(sel.value)} · guias do dia suspensas`, 'alerta'); limparSino(c.id); fecharModal(); renderTudo(); avisar('Horário cancelado e liberado na agenda.', 'info'); }; } });
  }
  function modalConferirFicha(c) {
    abrirModal({ titulo: 'Qual ficha é esta pessoa?', subtitulo: `O telefone ${h(c.telefone)} está em duas fichas. Confira com quem está no telefone antes de ligar a conversa.`,
      corpo: `<div class="wa-opcoes" role="radiogroup" aria-label="Fichas com este telefone">${CANDIDATAS_TEREZINHA.map((f, i) => `<label class="wa-escolha ${i === 0 ? 'marcada' : ''}"><input type="radio" name="wa-ficha" value="${f.id}" ${i === 0 ? 'checked' : ''} /><span class="avatar tom-${f.tom}" aria-hidden="true">${f.iniciais}</span>
          <span class="wa-escolha-texto"><strong>${h(f.nome)}</strong>
            <dl class="wa-conferir"><div><dt>Nascimento</dt><dd>${h(f.nasc)}</dd></div><div><dt>Documento</dt><dd>${h(f.doc)}</dd></div>${f.resp ? `<div><dt>Responsável</dt><dd>${h(f.resp)}</dd></div>` : ''}</dl>
            <dl class="wa-conferir"><div><dt>Próxima</dt><dd>${h(f.proxima)}</dd></div><div><dt>Última</dt><dd>${h(f.ultima)}</dd></div></dl></span></label>`).join('')}</div>
        <div class="aviso info">${ic('info')}<span>Pergunte a data de nascimento ou os dois últimos números do documento. A mensagem "O Pedro vai sim amanhã" sugere a ficha do Pedro, mas quem decide é você.</span></div>`,
      rodape: `<span class="esquerda">A ficha ligada aqui nunca troca sozinha: a mãe que divide o celular com o filho continua sendo quem a recepção conferiu.</span><button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao primario" id="wa-ok">Ligar a ficha à conversa</button>`,
      aoAbrir: raiz => { $('#wa-ok', raiz).onclick = () => {
        const id = raiz.querySelector('input[name="wa-ficha"]:checked').value; const f = CANDIDATAS_TEREZINHA.find(x => x.id === id);
        c.ficha = { estado: 'conferida', numero: id === 'pedro' ? '2.231' : '1.102', quando: 'hoje' };
        c.paciente = id === 'pedro' ? PEDRO : { nome: 'Terezinha Souza', numero: '1.102', idade: '61 anos · 02/08/1965', desde: 'paciente desde 2024', telefoneConfere: true, convenio: 'Unimed · 0 064 7788 9900 1', carteirinha: { texto: 'vale até 2027', tom: 'sucesso' }, documento: '···.···.···-31', autorizacao: 'Autorizou no termo do BSV · 02/09', tratamento: ['BSV encerrado em set'] };
        if (id === 'pedro') { c.horarios = [{ id: 'h7', quando: 'Sáb 25/10 · 09:00', detalhe: 'Acupuntura · Dra. Helena · sala 2 · sessão 4 de 10', estado: 'confirmado' }]; c.confirmacao = [{ hora: '06:00', texto: 'Lembrete da sessão de amanhã enviado' }, { hora: '08:40', texto: 'Lido' }, { hora: '09:12', texto: 'Respondeu <b>Confirmou</b> (pela responsável)' }]; c.acompanhamento = [{ quando: '17/10', texto: 'Sessão 3 de 10 · compareceu' }]; c.situacao = 'confirmou'; c.nome = 'Terezinha Souza (resp. de Pedro Henrique)'; }
        else { c.situacao = 'em-atendimento'; }
        evento(c, `Ficha de <b>${h(f.nome)}</b> ligada à conversa por Ana Paula · conferida pelo nascimento`, 'ok');
        limparSino(c.id); fecharModal(); renderTudo(); avisar(`Ficha de ${h(f.nome)} ligada. A agenda e a confirmação apareceram ao lado.`);
      }; } });
  }
  function modalNovaConversa() {
    abrirModal({ titulo: 'Nova conversa', subtitulo: 'Quem começa é a clínica, então a primeira mensagem é um modelo aprovado. Depois que a pessoa responder, o campo livre abre.', largura: 'pequeno',
      corpo: `<div class="campos"><div class="campo-bloco"><label class="campo-rotulo" for="wa-novo-tel">Telefone <span class="obrigatorio">*</span></label><input class="campo" id="wa-novo-tel" placeholder="(22) 9…" inputmode="tel" /></div>
        <div class="campo-bloco"><label class="campo-rotulo" for="wa-novo-nome">Nome</label><input class="campo" id="wa-novo-nome" placeholder="Como a pessoa se chama" /></div></div>
        <p class="wa-dica">Se o telefone estiver numa ficha, ela aparece aqui para você ligar antes de enviar.</p>`,
      rodape: `<button type="button" class="botao" data-fechar>Cancelar</button><button type="button" class="botao primario" id="wa-ok" disabled>Começar</button>`,
      aoAbrir: raiz => { const tel = $('#wa-novo-tel', raiz); tel.oninput = () => $('#wa-ok', raiz).disabled = tel.value.replace(/\D/g, '').length < 10; $('#wa-ok', raiz).onclick = () => {
        const nome = $('#wa-novo-nome', raiz).value.trim(); const t = tel.value.trim();
        const c = { id: 'c' + Date.now(), nome: nome || t, telefone: t, iniciais: nome ? nome.split(' ').map(p => p[0]).slice(0, 2).join('').toUpperCase() : '?', tom: 'cinza', canal: 'wa', fila: 'Recepção', responsavel: EU.id, naoLidas: 0, hora: agora(), ordem: proximaOrdem(), janela: { aberta: false, desde: 'sempre (a pessoa ainda não escreveu)' }, ficha: { estado: 'nenhuma' }, paciente: null, situacao: 'em-atendimento', horarios: [], confirmacao: [], acompanhamento: [], mensagens: [{ tipo: 'dia', texto: 'Hoje · sexta-feira, 24/10' }, { tipo: 'evento', hora: agora(), texto: 'Conversa começada por Ana Paula' }] };
        CONVERSAS.push(c); estado.filtro = 'minhas'; fecharModal(); abrir(c.id); avisar('Conversa criada. Escolha o modelo da primeira mensagem.', 'info');
      }; } });
  }

  // ---------- coluna 3: o paciente (lido do Clinica) ----------
  function renderFicha() {
    const raiz = $('#wa-ficha'); const c = conv(estado.atual);
    const topo = `<div class="wa-ficha-topo"><h2>Do sistema da clínica</h2><button type="button" class="botao icone fantasma wa-ficha-fechar" data-acao="fechar-ficha" aria-label="Fechar a ficha">${ic('fechar')}</button></div>`;
    if (!c) { raiz.innerHTML = topo + `<div class="wa-secao"><p class="wa-dica">A ficha, a agenda e a confirmação do paciente da conversa aparecem aqui, lidas do próprio app, não do CRM.</p></div>`; ligarFicha(); return; }
    let corpo = '';
    if (c.retencao && itemRet(c.retencao)) {
      const r = itemRet(c.retencao); const s = seloRet(r);
      corpo += `<section class="wa-secao wa-caso"><h3>Acompanhamento · ${r.grupo === 'novo-bsv' ? 'Novo BSV' : r.grupo === 'retorno-pedido' ? 'Retorno pedido' : 'Recall'} <button type="button" class="link" data-acao="acompanhamento">Ver caso</button></h3>
        <dl class="wa-pares">
          <dt>Tratamento</dt><dd>${h(r.tratamento)}</dd>
          <dt>${r.grupo === 'novo-bsv' ? '1ª sessão' : 'Última sessão'}</dt><dd>${h(r.ultima)} <span class="wa-dias tom-${r.grupo === 'novo-bsv' ? 'sucesso' : (GRUPOS.find(g => g.id === r.grupo) || {}).tom || 'neutro'}">${r.grupo === 'novo-bsv' ? 'há ' + r.dias + ' dias' : r.dias + ' dias sem vir'}</span></dd>
          <dt>Sessões</dt><dd>${h(r.sessoes)}</dd>
          <dt>Mensagens</dt><dd>${r.autorizou ? '<span class="selo tom-sucesso">autorizou</span>' : '<span class="selo tom-aviso">sem autorização · ligar</span>'}</dd>
          <dt>Situação</dt><dd><span class="selo tom-${s.tom}">${h(s.texto)}</span></dd>
          <dt>Responsável</dt><dd>${h(r.resp === EU.id ? 'Você' : nomeDe(r.resp))}</dd></dl>
        ${r.contatos.length ? `<h4>Contatos</h4><ul class="wa-tempo">${r.contatos.map(x => `<li><time>${h(x.quando)}</time><span>${h(x.texto)}</span></li>`).join('')}</ul>` : '<p class="wa-dica">Nenhum contato ainda neste caso.</p>'}
        </section>`;
    }
    if (c.ficha.estado === 'nenhuma') {
      corpo += `<section class="wa-secao wa-sem-ficha"><h3>Quem é?</h3><p class="wa-dica">${h(c.telefone)} não está em nenhuma ficha. Busque pelo nome ou cadastre.</p>
        <div class="busca">${ic('busca')}<input class="campo" id="wa-busca-ficha" type="search" placeholder="Nome, documento ou nascimento" aria-label="Buscar ficha" autocomplete="off" /></div>
        <div class="wa-candidatos" id="wa-candidatos"></div>
        <button type="button" class="botao" data-acao="cadastrar">${ic('mais')}Cadastrar paciente com este telefone</button></section>`;
    } else if (c.ficha.estado === 'duvida') {
      corpo += `<section class="wa-secao"><h3>Duas fichas com este telefone</h3><p class="wa-dica">Pedro Henrique Souza (9 anos, responsável Terezinha) e Terezinha Souza (61 anos). Confira com quem está no telefone.</p><button type="button" class="botao primario" data-acao="conferir-ficha">${ic('pessoa')}Conferir a ficha</button></section>`;
    } else {
      const p = c.paciente;
      corpo += `<section class="wa-secao"><h3>Paciente <button type="button" class="link" data-acao="abrir-ficha">Abrir ficha</button></h3>
        <div class="wa-pessoa"><span class="avatar tom-${c.tom}" aria-hidden="true">${h(c.iniciais)}</span><div class="wa-pessoa-nome"><strong>${h(p.nome)}</strong><span>${h(p.idade)} · ${h(p.desde)}</span></div></div>
        <dl class="wa-pares">
          <dt>Telefone</dt><dd>${h(c.telefone)} ${p.telefoneConfere ? '<span class="selo tom-sucesso" title="É o mesmo número da ficha">confere</span>' : '<span class="selo tom-aviso">difere da ficha</span>'}</dd>
          <dt>Convênio</dt><dd>${h(p.convenio)} ${p.carteirinha ? `<span class="selo tom-${p.carteirinha.tom}">${h(p.carteirinha.texto)}</span>` : ''}</dd>
          <dt>Documento</dt><dd>${h(p.documento)}</dd>
          <dt>Mensagens</dt><dd>${h(p.autorizacao)}</dd></dl>
        <div class="wa-selos">${p.tratamento.map(t => `<span class="selo tom-${t.startsWith('Em tratamento') ? 'info' : t.startsWith('Sem vir') ? 'aviso' : 'neutro'}">${h(t)}</span>`).join('')}</div></section>`;
    }
    if (c.horarios.length) corpo += `<section class="wa-secao"><h3>Horários <button type="button" class="link" data-acao="abrir-agenda">Agenda</button></h3>${c.horarios.map(hor => renderHorario(c, hor)).join('')}</section>`;
    if (c.confirmacao.length) corpo += `<section class="wa-secao"><h3>Confirmação de hoje</h3><ul class="wa-tempo">${c.confirmacao.map(x => `<li><time>${h(x.hora)}</time><span>${x.texto}</span></li>`).join('')}</ul></section>`;
    if (c.acompanhamento.length) corpo += `<section class="wa-secao"><h3>Acompanhamento <button type="button" class="link" data-acao="acompanhamento">Ver caso</button></h3><ul class="wa-tempo">${c.acompanhamento.map(x => `<li><time>${h(x.quando)}</time><span>${x.texto}</span></li>`).join('')}</ul>${c.paciente && c.paciente.renovar ? `<p class="wa-dica">${h(c.paciente.renovar)}</p>` : ''}</section>`;
    raiz.innerHTML = topo + corpo; ligarFicha();
    const b = $('#wa-busca-ficha'); if (b) { const lista = $('#wa-candidatos'); b.oninput = () => { const q = b.value.trim().toLowerCase(); lista.innerHTML = q.length < 2 ? '' : FICHAS_BUSCA.filter(f => f.nome.toLowerCase().includes(q)).map((f, i) => `<button type="button" class="wa-candidato" data-i="${FICHAS_BUSCA.indexOf(f)}"><span class="avatar tom-${f.tom}" aria-hidden="true">${f.iniciais}</span><span><strong>${h(f.nome)}</strong><span>${h(f.detalhe)}</span></span></button>`).join('') || `<p class="wa-dica">Nenhuma ficha com esse nome.</p>`;
      lista.querySelectorAll('.wa-candidato').forEach(x => x.onclick = () => { const f = FICHAS_BUSCA[+x.dataset.i]; c.ficha = { estado: 'conferida', numero: f.numero, quando: 'hoje' }; c.nome = f.nome; c.iniciais = f.iniciais; c.tom = f.tom; c.paciente = { nome: f.nome, numero: f.numero, idade: f.detalhe.split(' · ')[0], desde: 'paciente desde 2024', telefoneConfere: false, convenio: f.detalhe.split(' · ')[1], carteirinha: null, documento: '···.···.···-19', autorizacao: 'Sem autorização de mensagens registrada', tratamento: ['Sem tratamento em curso'] }; c.situacao = 'em-atendimento'; evento(c, `Ficha de <b>${h(f.nome)}</b> ligada à conversa por Ana Paula · telefone novo gravado na ficha`, 'ok'); renderTudo(); avisar(`Ficha de ${h(f.nome)} ligada à conversa.`); }); }; }
  }
  function renderHorario(c, hor) {
    const E = { 'nao-vem': ['perigo', 'Não vem'], 'sem-resposta': ['aviso', 'Sem resposta'], 'marcado': ['neutro', 'Marcado'], 'confirmado': ['sucesso', 'Confirmou'], 'remarcado': ['neutro', 'Remarcado'], 'cancelado': ['neutro', 'Cancelado'] };
    const [tom, rotulo] = E[hor.estado]; const ativo = !c.encerrada && (hor.estado === 'nao-vem' || hor.estado === 'sem-resposta' || hor.estado === 'marcado' || hor.estado === 'confirmado');
    return `<div class="wa-horario ${hor.estado === 'nao-vem' ? 'tom-perigo' : hor.estado === 'confirmado' ? 'tom-sucesso' : hor.estado === 'remarcado' || hor.estado === 'cancelado' ? 'apagado' : ''}">
      <div class="wa-horario-topo"><b>${h(hor.quando)}</b><span class="selo tom-${tom}">${rotulo}</span></div>
      <small>${hor.estado === 'remarcado' ? 'Foi para <b>' + h(hor.para) + '</b> · ' : ''}${h(hor.detalhe)}${hor.motivo ? ' · ' + h(hor.motivo) : ''}</small>
      ${ativo ? `<div class="botoes-linha">${hor.estado !== 'confirmado' ? `<button type="button" class="botao mini ${hor.estado === 'sem-resposta' ? 'primario' : ''}" data-acao="confirmar" data-horario="${hor.id}">${ic('check')}Confirmar</button>` : ''}<button type="button" class="botao mini ${hor.estado === 'nao-vem' ? 'primario' : ''}" data-acao="remarcar" data-horario="${hor.id}">${ic('cal')}Remarcar</button><button type="button" class="botao mini contorno-perigo" data-acao="cancelar" data-horario="${hor.id}">Cancelar</button></div>` : ''}</div>`;
  }
  function ligarFicha() { $('#wa-ficha').querySelectorAll('[data-acao]').forEach(b => b.onclick = () => acao(b.dataset.acao, b)); }

  // ---------- o sino: a mesma lista do app; aqui o clique abre a conversa ----------
  function renderSino() {
    const n = SINO.filter(s => s.naoLida).length; const cont = $('#sino-contador'); cont.textContent = n; cont.hidden = !n;
    $('#sino-botao').setAttribute('aria-label', `Notificações, ${n} não lidas`);
    const pop = $('#sino-popover'); if (pop.hidden) return;
    pop.innerHTML = `<div class="moldura-veu" data-fechar-sino></div><div class="moldura-popover sino" role="dialog" aria-label="Notificações">
      <div class="sino-cab"><h2>Notificações</h2><button type="button" class="link" id="sino-ler" ${n ? '' : 'disabled'}>Marcar todas como lidas</button></div>
      <div class="sino-lista"><section><header><h3>Respostas ao lembrete e atenção no WhatsApp</h3><span>${n} ${n === 1 ? 'nova' : 'novas'}</span></header>
        ${SINO.map(s => `<button type="button" class="moldura-aviso ${s.naoLida ? 'nao-lida' : ''}" data-sino="${s.id}" title="Abrir a conversa"><span class="sino-ic ${s.tom}">${ic(s.icone)}</span><span class="sino-texto"><span class="sino-tit">${s.titulo}</span><span class="sino-meta">${h(s.meta)}</span></span>${s.naoLida ? '<span class="sino-ponto" title="Não lida"></span>' : '<span></span>'}</button>`).join('')}
        <p class="moldura-situacao">A mesma lista que o painel do Gerente lê. Aqui, o clique abre a conversa.</p></section></div>
      <div class="sino-rodape"><button type="button" class="link">Confirmações de agenda</button><button type="button" class="link">Acompanhamento</button></div></div>`;
    pop.querySelectorAll('[data-sino]').forEach(b => b.onclick = () => { const s = SINO.find(x => x.id === b.dataset.sino); s.naoLida = false; fecharSino(); abrir(s.conversa); });
    pop.querySelectorAll('[data-fechar-sino]').forEach(b => b.onclick = fecharSino);
    $('#sino-ler').onclick = () => { SINO.forEach(s => s.naoLida = false); renderSino(); };
  }
  function fecharSino() { $('#sino-popover').hidden = true; $('#sino-botao').setAttribute('aria-expanded', 'false'); renderSino(); }
  $('#sino-botao').onclick = () => { const pop = $('#sino-popover'); pop.hidden = !pop.hidden; $('#sino-botao').setAttribute('aria-expanded', !pop.hidden); renderSino(); };

  // ---------- amarrações gerais ----------
  $('#wa-busca').oninput = e => { estado.busca = e.target.value; renderLista(); };
  $('#wa-so-nao-lidas').onclick = () => { estado.soNaoLidas = !estado.soNaoLidas; renderFiltro(); renderLista(); };
  $('#wa-nova').onclick = () => modalNovaConversa();
  $('#wa-linhas').addEventListener('keydown', e => {
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return; e.preventDefault();
    const itens = [...document.querySelectorAll('.wa-linha')]; const i = itens.findIndex(x => x.dataset.id === estado.atual); const alvo = itens[Math.min(itens.length - 1, Math.max(0, i + (e.key === 'ArrowDown' ? 1 : -1)))];
    if (alvo) { abrir(alvo.dataset.id); alvo.focus(); }
  });
  document.addEventListener('keydown', e => { if (e.key === 'Escape') { if (estado.popover) { estado.popover = null; const p = $('#wa-popover'); if (p) p.innerHTML = ''; document.querySelectorAll('[data-acao="modelos"],[data-acao="rapidas"]').forEach(b => b.setAttribute('aria-expanded', 'false')); } if (!$('#sino-popover').hidden) fecharSino(); if (estado.fichaAberta) acao('fechar-ficha'); } });
  document.addEventListener('click', e => { if (estado.popover && !e.target.closest('.wa-popover') && !e.target.closest('[data-acao="modelos"],[data-acao="rapidas"],[data-acao="ret-preparar"]')) { estado.popover = null; const p = $('#wa-popover'); if (p) p.innerHTML = ''; document.querySelectorAll('[data-acao="modelos"],[data-acao="rapidas"]').forEach(b => b.setAttribute('aria-expanded', 'false')); } });

  CONVERSAS.forEach(c => { c.naoLidasAoAbrir = c.naoLidas; c.modo = 'responder'; c.rascunho = ''; });
  RETENCAO.forEach(r => { if (!r.paciente && r.convId) r.paciente = (CONVERSAS.find(c => c.id === r.convId) || {}).paciente; });
  renderFiltro(); renderLista(); renderConversa(); renderFicha(); renderSino();
  // A direção abre o mockup já numa conversa (a que mais pede ação); #vazia mostra o estado sem conversa.
  const alvo = location.hash.replace('#', '');
  if (alvo === 'retencao' || alvo === 'retencao-respondeu' || alvo === 'retencao-ligar') { estado.modo = 'retencao'; renderFiltro(); abrirRetencao(alvo === 'retencao-respondeu' ? 'alzira' : alvo === 'retencao-ligar' ? 'helena' : 'seb'); }
  else if (alvo === 'vazia') { /* fica na lista */ } else if (alvo === 'ficha') { abrir('terezinha'); modalConferirFicha(conv('terezinha')); } else if (alvo === 'fechada') { abrir('carlos'); } else if (alvo === 'nova') { abrir('novo'); } else if (alvo && conv(alvo)) { abrir(alvo); } else { abrir('maria'); }
  if (alvo === 'contexto') { estado.fichaAberta = true; $('#wa-ficha').classList.add('aberta'); }
})();
