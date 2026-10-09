import {Group, Paper, Stack, Text, Title} from '@mantine/core';
import {CamposReact, AcoesReact, TabelaReact} from './paginas-react';
import type {Campo, Contexto, Pagina} from './paginas';

const rotulos:Record<string,string>={MetaPaciente:'CPF e telefone',ConvenioPaciente:'Convênio',CarteirinhaPaciente:'Carteirinha',CategoriaPaciente:'Categoria',Duracao:'Duração em minutos',Sala:'Sala (opcional)',Hora:'Horário',Observacoes:'Recado para o atendimento',AvisoConsulta:'Validade da consulta',AvisoPendencias:'Pendências',NotaGuiaNaMarcacao:'Emissão das guias',AvisoCarteirinha:'Carteirinha',ResumoCurtoPrevia:'Resumo',ValidadePaciente:'Validade',ExecutanteGuia:'Executante',ResumoPrevia:'Prévia das guias',ResumoBaixas:'Resultado',SaldoAutorizacao:'Saldo autorizado',CabecalhoDosAvisos:'Conflitos de horário',AvisoJaLancado:'Atendimento lançado',AvisoHorarioDoDia:'Horário de hoje'};
const metadados=['TituloTela','SubtituloTela','TituloPassoQuando','TituloPasso2','TituloResultado'];
/** Composição de agendamento. Cada campo continua usando o mesmo contrato do host. */
export function AgendamentoReact({pagina:p,contexto:c}:{pagina:Pagina;contexto:Contexto}){
 const usados=new Set(metadados);
 const extrair=(chaves:string[])=>p.campos.filter(f=>chaves.includes(f.chave)).map(f=>{usados.add(f.chave);return {...f,rotulo:rotulos[f.chave]??f.rotulo,ajuda:f.chave==='Duracao'?'Em branco: duração padrão do profissional.':f.ajuda}});
 const busca=extrair(['Seletor.Termo','Seletor.Selecionado','Seletor.Erro','Seletor.ResumoDaLista']);
 const paciente=extrair(['PacienteSelecionado.Nome','MetaPaciente','ConvenioPaciente','CarteirinhaPaciente','CategoriaPaciente']);
 const horario=extrair(['Data','Hora','Profissional','Duracao','Sala','ComoEncaixe','CabecalhoDosAvisos']);
 const modalidade=extrair(['EspecialidadeSelecionada','PrimeiroCodigo','AtendimentoJaRealizado','CriarEncaixeSeparado']);
 const observacoes=extrair(['Observacoes']);
 const avisos=extrair(['AvisoJaLancado','AvisoHorarioDoDia','AvisoCarteirinha','AvisoConsulta','SaldoAutorizacao','AvisoPendencias','ValorPrevisto','ResumoCurtoPrevia','ValidadePaciente','ExecutanteGuia','ResumoPrevia','NotaGuiaNaMarcacao','ResumoBaixas']);

 const visivel=(fs:Campo[])=>fs.some(f=>f.visivel!==false);
 const orientacao=p.campos.find(f=>f.chave==='SubtituloTela'&&f.visivel!==false)?.valor;
 const cartoes=p.secoes.find(s=>s.chave==='Cartoes');
 return <div className="agendamento-estacao form-agendamento">
  <div className="agendamento-trabalho">
   <Paper className="agendamento-bloco" withBorder p="lg" radius="md" data-etapa="Paciente">
    <Group justify="space-between" mb="md"><Title order={2} size="h4">Paciente</Title></Group>
    <CamposReact campos={busca} contexto={c}/><div className="agendamento-identidade"><CamposReact campos={paciente.filter(f=>f.chave!=='PacienteSelecionado.Nome')} contexto={c}/></div>{!!orientacao&&<Text size="sm" c="dimmed" mt="sm">{String(orientacao)}</Text>}
   </Paper>
   {visivel(horario)&&<Paper className="agendamento-bloco" withBorder p="lg" data-etapa="Data, horário e profissional">
    <Title order={2} size="h4" mb="md">Data e profissional</Title><CamposReact campos={horario} contexto={c}/>
   </Paper>}
   {(cartoes||visivel(modalidade))&&<Paper className="agendamento-bloco" withBorder p="lg" data-etapa="Modalidade">
    <Title order={2} size="h4" mb={6}>Atendimento</Title><Text size="sm" c="dimmed" mb="md">Escolha o procedimento e confira as guias vinculadas.</Text>
    {cartoes?.tabelas.map(t=>t.chave==='Cartoes'?<div key={t.chave} className="modalidades-procedimentos" data-tabela-container={t.chave} data-testid={`tabela-${t.chave}`}>
     {t.linhas.map(l=><Paper key={l.id} className="modalidade-procedimento" withBorder p="md" data-linha-id={l.id}>
      <Group justify="space-between" align="center" wrap="wrap"><Stack gap={5} className="modalidade-descricao"><Text fw={600}>{l.celulas.Nome??Object.values(l.celulas)[0]}</Text><Group gap="xs">{t.colunas.filter(col=>col.chave!=='Nome').map(col=><Text size="xs" c="dimmed" key={col.chave}>{col.rotulo}: {l.celulas[col.chave]}</Text>)}</Group></Stack><div className="acoes-na-linha"><AcoesReact acoes={l.acoes} contexto={{...c,tabela:t.chave,linha:l.id}}/></div></Group>
      <CamposReact campos={l.campos} contexto={{...c,tabela:t.chave,linha:l.id}}/>
     </Paper>)}{!t.linhas.length&&<Text>{t.vazio}</Text>}
    </div>:<TabelaReact key={t.chave} tabela={t} contexto={c}/>)}
    <CamposReact campos={modalidade} contexto={c}/>
   </Paper>}
   {visivel(observacoes)&&<Paper className="agendamento-bloco" withBorder p="lg" data-etapa="Observações"><CamposReact campos={observacoes} contexto={c}/></Paper>}
   <CamposReact campos={p.campos.filter(f=>!usados.has(f.chave))} contexto={c}/>
  </div>
  {visivel(avisos)&&<aside className="agendamento-contexto" aria-label="Paciente e conferência do agendamento">
   <Paper className="agendamento-conferencia" withBorder p="lg"><Title order={2} size="h5" mb="md">Confira antes de confirmar</Title><CamposReact campos={avisos} contexto={c}/></Paper>
  </aside>}
 </div>;
}
