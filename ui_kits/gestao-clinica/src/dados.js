/* Dados fictícios em pt-BR — mesma massa da tela de referência.
   Em produção, trocar por uma camada de dados real (README do handoff). */
export const MESES=['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez'];
export const OCUPACAO=[3.1,3.4,3.9,4.4,5.1,5.7,5.4,5.9,6.3,6.1,6.8,7.2];
export const SIMILARES=[2.9,3.0,3.2,3.5,3.7,3.9,3.8,4.0,4.1,4.0,4.3,4.4];
export const VOLUME=[72,78,84,91,88,97,103,99,112,118,126];
export const CAIXA=[{entrada:7.4,saida:5.5},{entrada:6.1,saida:4.2},{entrada:8.3,saida:3.9},{entrada:5.2,saida:6.1},{entrada:9.1,saida:4.4},{entrada:7.7,saida:5.0},{entrada:4.3,saida:2.2},{entrada:8.8,saida:6.3},{entrada:6.9,saida:4.8},{entrada:7.1,saida:3.6},{entrada:9.6,saida:5.9},{entrada:5.8,saida:4.1},{entrada:8.0,saida:6.6},{entrada:6.4,saida:3.2},{entrada:7.9,saida:5.1}];
export const CONSULTAS=[
{h:'08:00',p:'Maria da Silva',prof:'Dr. Otávio Lins',esp:'Acupuntura',st:'Confirmada',conv:'Unimed Intercâmbio'},
{h:'08:40',p:'Carlos Nunes',prof:'Dra. Helena Braga',esp:'Fisiatria',st:'Em atendimento',conv:'Amil'},
{h:'09:20',p:'João Pereira',prof:'Dr. Otávio Lins',esp:'Acupuntura',st:'Aguardando',conv:'Amil'},
{h:'10:00',p:'Rita Campos',prof:'Dra. Helena Braga',esp:'BSV',st:'Confirmada',conv:'Petrobras'},
{h:'10:40',p:'Ana Souza',prof:'Dr. Paulo Vidal',esp:'Psiquiatria',st:'Aguardando',conv:'Petrobras'},
{h:'11:20',p:'Sérgio Antunes',prof:'Dr. Otávio Lins',esp:'Eletroacupuntura',st:'Confirmada',conv:'Unimed Costa do Sol'}];
export const PACIENTES=[
{nome:'Maria da Silva',cpf:'123.456.789-00',conv:'Unimed Intercâmbio',ult:'14/07/2026',cart:'Vencida'},
{nome:'Carlos Nunes',cpf:'987.654.321-00',conv:'Amil',ult:'15/07/2026',cart:'Em dia'},
{nome:'João Pereira',cpf:'456.789.123-00',conv:'Amil',ult:'15/07/2026',cart:'Em dia'},
{nome:'Rita Campos',cpf:'321.654.987-00',conv:'Petrobras',ult:'16/07/2026',cart:'Vence em 12 dias'},
{nome:'Ana Souza',cpf:'741.852.963-00',conv:'Petrobras',ult:'16/07/2026',cart:'Em dia'}];
export const AGENDA_PROFS=['Dr. Otávio Lins','Dra. Helena Braga','Dr. Paulo Vidal'];
export const SEMANAS=['S1','S2','S3','S4','S5','S6','S7','S8','S9','S10','S11','S12'];
export const ATEND_SEMANA=[9,12,14,11,15,13,10,14,16,12,15,17];
export const ESPECIALIDADES=[
{label:'Acupuntura',value:'186 sessões',fr:.52},
{label:'Fisiatria',value:'94 consultas',fr:.26},
{label:'Eletroacupuntura',value:'78 sessões',fr:.22}];
export const RECEB_CONV=[
{label:'Unimed Intercâmbio',value:'R$ 38.200',fr:.46},
{label:'Amil',value:'R$ 27.900',fr:.34},
{label:'Petrobras',value:'R$ 16.300',fr:.20}];
export const GUIAS=[
{g:'G-2026-0791',p:'Maria da Silva',conv:'Unimed Intercâmbio',v:'R$ 460,00',venc:'17/07/2026',urg:'Vence amanhã',tom:'erro'},
{g:'G-2026-0784',p:'Carlos Nunes',conv:'Amil',v:'R$ 320,00',venc:'19/07/2026',urg:'3 dias',tom:'aviso'},
{g:'G-2026-0779',p:'Rita Campos',conv:'Petrobras',v:'R$ 540,00',venc:'22/07/2026',urg:'6 dias',tom:'aviso'},
{g:'G-2026-0771',p:'João Pereira',conv:'Amil',v:'R$ 320,00',venc:'28/07/2026',urg:'12 dias',tom:'neutro'},
{g:'G-2026-0765',p:'Sérgio Antunes',conv:'Unimed Costa do Sol',v:'R$ 780,00',venc:'30/07/2026',urg:'14 dias',tom:'neutro'}];
export const CARTEIRINHAS=[
{nome:'Maria da Silva',conv:'Unimed Intercâmbio',st:'Vencida',tom:'erro'},
{nome:'Rita Campos',conv:'Petrobras',st:'Vence em 12 dias',tom:'aviso'},
{nome:'Sérgio Antunes',conv:'Unimed Costa do Sol',st:'Vence em 20 dias',tom:'aviso'}];
export const CONFIRMACOES=[
{h:'08:00',p:'Helena Duarte',tel:'(22) 99812-4470',prof:'Dr. Otávio Lins',esp:'Acupuntura',st:'Sem resposta',tom:'aviso'},
{h:'08:40',p:'Marcos Vieira',tel:'(22) 98123-9834',prof:'Dra. Helena Braga',esp:'Fisiatria',st:'Não confirmada',tom:'neutro'},
{h:'09:20',p:'Luciana Prado',tel:'(22) 99655-2210',prof:'Dr. Paulo Vidal',esp:'Psiquiatria',st:'Sem resposta',tom:'aviso'},
{h:'10:00',p:'Roberto Faria',tel:'(21) 98877-1043',prof:'Dr. Otávio Lins',esp:'Eletroacupuntura',st:'Não confirmada',tom:'neutro'},
{h:'11:20',p:'Sônia Ramos',tel:'(22) 99340-8861',prof:'Dra. Helena Braga',esp:'BSV',st:'Recusou',tom:'erro'},
{h:'14:00',p:'Pedro Amaral',tel:'(22) 98011-5529',prof:'Dr. Paulo Vidal',esp:'Psiquiatria',st:'Não confirmada',tom:'neutro'},
{h:'15:20',p:'Clara Mendes',tel:'(22) 99733-0912',prof:'Dr. Otávio Lins',esp:'Acupuntura',st:'Sem resposta',tom:'aviso'}];
export const PRONTUARIOS=[
{p:'Maria da Silva',data:'16/07/2026',esp:'Acupuntura',tipo:'Evolução',st:'A assinar',tom:'aviso'},
{p:'João Pereira',data:'16/07/2026',esp:'Acupuntura',tipo:'Evolução',st:'A assinar',tom:'aviso'},
{p:'Sérgio Antunes',data:'15/07/2026',esp:'Eletroacupuntura',tipo:'Anamnese',st:'A assinar',tom:'aviso'},
{p:'Carlos Nunes',data:'15/07/2026',esp:'Fisiatria',tipo:'Evolução',st:'Assinado',tom:'sucesso'},
{p:'Ana Souza',data:'14/07/2026',esp:'Psiquiatria',tipo:'Evolução',st:'Assinado',tom:'sucesso'},
{p:'Rita Campos',data:'14/07/2026',esp:'BSV',tipo:'Anamnese',st:'Assinado',tom:'sucesso'}];
export const EXAMES=[
{p:'Maria da Silva',exame:'Ressonância — coluna lombar',ped:'10/07/2026',st:'Resultado disponível',tom:'info'},
{p:'Carlos Nunes',exame:'Raio-X — joelho direito',ped:'12/07/2026',st:'Aguardando resultado',tom:'aviso'},
{p:'Sérgio Antunes',exame:'Hemograma completo',ped:'14/07/2026',st:'Resultado disponível',tom:'info'},
{p:'Ana Souza',exame:'Eletroneuromiografia',ped:'15/07/2026',st:'Agendado',tom:'neutro'},
{p:'João Pereira',exame:'Ultrassom — ombro esquerdo',ped:'16/07/2026',st:'Aguardando resultado',tom:'aviso'}];
export const PRESCRICOES=[
{p:'Maria da Silva',data:'16/07/2026',med:'Pregabalina 75mg · 2x ao dia · 30 dias',tipo:'Controle especial'},
{p:'Carlos Nunes',data:'16/07/2026',med:'Ciclobenzaprina 10mg · 1x à noite · 15 dias',tipo:'Simples'},
{p:'João Pereira',data:'15/07/2026',med:'Duloxetina 30mg · 1x ao dia · 60 dias',tipo:'Controle especial'},
{p:'Ana Souza',data:'15/07/2026',med:'Amitriptilina 25mg · 1x à noite · 30 dias',tipo:'Controle especial'},
{p:'Rita Campos',data:'14/07/2026',med:'Dipirona 1g · até 4x ao dia · 7 dias',tipo:'Simples'}];
export const PROFISSIONAIS=[
{nome:'Dr. Otávio Lins',esp:'Acupuntura',crm:'CRM-RJ 52.884',ag:'Seg–Sex',ocup:.91,at:186},
{nome:'Dra. Helena Braga',esp:'Fisiatria',crm:'CRM-RJ 61.230',ag:'Seg, Qua, Qui',ocup:.84,at:94},
{nome:'Dr. Paulo Vidal',esp:'Psiquiatria',crm:'CRM-RJ 48.917',ag:'Ter, Qui',ocup:.78,at:62},
{nome:'Dra. Marina Costa',esp:'Eletroacupuntura',crm:'CRM-RJ 70.442',ag:'Sex, Sáb',ocup:.66,at:78}];
export const CONVENIOS=[
{nome:'Unimed Intercâmbio',ans:'ANS 30.171-2',prazo:'30 dias',tabela:'TUSS 2024',receita:'R$ 38.200',pac:912,st:'Ativo',tom:'sucesso'},
{nome:'Amil',ans:'ANS 32.694-0',prazo:'45 dias',tabela:'TUSS 2024',receita:'R$ 27.900',pac:764,st:'Ativo',tom:'sucesso'},
{nome:'Petrobras (APS)',ans:'ANS 41.983-1',prazo:'60 dias',tabela:'Própria',receita:'R$ 16.300',pac:388,st:'Ativo',tom:'sucesso'},
{nome:'Unimed Costa do Sol',ans:'ANS 30.884-6',prazo:'30 dias',tabela:'TUSS 2024',receita:'R$ 8.400',pac:214,st:'Em renegociação',tom:'aviso'}];
export const RECEBER=[
{origem:'Unimed Intercâmbio · lote L-118',venc:'20/07/2026',valor:'R$ 9.840',st:'No prazo',tom:'sucesso'},
{origem:'Amil · lote L-116',venc:'24/07/2026',valor:'R$ 6.230',st:'No prazo',tom:'sucesso'},
{origem:'Petrobras · lote L-112',venc:'15/07/2026',valor:'R$ 4.560',st:'Vencido há 1 dia',tom:'erro'},
{origem:'Particular · parcelamentos',venc:'—',valor:'R$ 2.980',st:'Recorrente',tom:'neutro'},
{origem:'Unimed Costa do Sol · lote L-109',venc:'28/07/2026',valor:'R$ 1.220',st:'No prazo',tom:'sucesso'}];
export const LOTES=[
{lote:'L-119',conv:'Unimed Intercâmbio',guias:18,valor:'R$ 8.120',envio:'16/07/2026',st:'Em preparação',tom:'neutro'},
{lote:'L-118',conv:'Unimed Intercâmbio',guias:24,valor:'R$ 9.840',envio:'12/07/2026',st:'Enviado',tom:'info'},
{lote:'L-117',conv:'Amil',guias:15,valor:'R$ 5.310',envio:'10/07/2026',st:'Aceito',tom:'sucesso'},
{lote:'L-116',conv:'Amil',guias:19,valor:'R$ 6.230',envio:'08/07/2026',st:'Aceito',tom:'sucesso'},
{lote:'L-112',conv:'Petrobras (APS)',guias:12,valor:'R$ 4.560',envio:'28/06/2026',st:'Pago parcial',tom:'aviso'}];
export const GLOSAS=[
{guia:'G-2026-0712',p:'Helena Duarte',conv:'Amil',motivo:'Código 2801 — sessão além do limite mensal',valor:'R$ 320,00',st:'Recurso enviado',tom:'info'},
{guia:'G-2026-0698',p:'Marcos Vieira',conv:'Unimed Intercâmbio',motivo:'Carteirinha vencida na data do atendimento',valor:'R$ 460,00',st:'A recursar',tom:'aviso'},
{guia:'G-2026-0684',p:'Sônia Ramos',conv:'Petrobras (APS)',motivo:'Falta do 2º código de autorização',valor:'R$ 540,00',st:'A recursar',tom:'aviso'},
{guia:'G-2026-0671',p:'Pedro Amaral',conv:'Amil',motivo:'Divergência de tabela TUSS',valor:'R$ 180,00',st:'Recurso aceito',tom:'sucesso'},
{guia:'G-2026-0655',p:'Clara Mendes',conv:'Unimed Costa do Sol',motivo:'Guia sem assinatura do profissional',valor:'R$ 240,00',st:'Perda aceita',tom:'erro'}];
export const GLOSAS_MOTIVOS=[
{label:'Autorização / 2º código',value:'R$ 1.240',fr:.38},
{label:'Limite de sessões',value:'R$ 980',fr:.30},
{label:'Cadastro e carteirinha',value:'R$ 640',fr:.20},
{label:'Tabela e códigos',value:'R$ 380',fr:.12}];
