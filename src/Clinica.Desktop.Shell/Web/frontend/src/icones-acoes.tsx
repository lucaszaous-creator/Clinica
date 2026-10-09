import {createElement} from 'react';
import {Save,Printer,History,Copy,FileText,Files,FileCheck,ClipboardList,FlaskConical,Droplet,RefreshCw,FolderOpen,Package,PenLine,Check,Play,RotateCcw,UserRound,CalendarPlus,Plus,Trash2,Search,X,ArrowLeft,Download,Upload,Eye,type IconNode} from 'lucide';

const icones:Record<string,IconNode>={
 'Atendimento.Salvar':Save,'Atendimento.ImprimirFicha':Printer,'Atendimento.HistoricoConsulta.Alternar':History,
 'Atendimento.CopiarUltimaEvolucao':Copy,'Atendimento.AbrirModelos':Files,'Atendimento.AbrirDetalhe':ClipboardList,'Atendimento.AbrirMapa':UserRound,
 EmitirDocumentos:Files,'emitir-receita':FileText,'emitir-atestado':FileCheck,'emitir-comparecimento':FileText,'emitir-exame':FlaskConical,
 'Atendimento.PrescreverInfusao':Droplet,DocumentosPacienteWeb:FolderOpen,AtualizarFicha:RefreshCw,ConsultarFicha:FolderOpen,ConsultarExames:FlaskConical,
 RegistrarMateriais:Package,'Atendimento.IndicarBsv':ClipboardList,'Atendimento.ColherTermo':PenLine,
 IniciarSessao:Play,ReabrirSessao:RotateCcw,FinalizarSessao:Check,Lancar:Check
};
const comuns:Record<string,IconNode>={
 salvar:Save,salvarrascunho:Save,salvarconfiguracao:Save,confirmar:Check,concluir:Check,assinar:PenLine,
 imprimir:Printer,imprimirficha:Printer,segundavia:Printer,atualizar:RefreshCw,carregar:RefreshCw,recarregarmedicamentos:RefreshCw,
 novo:Plus,nova:Plus,adicionar:Plus,acrescentaritem:Plus,criarinfusao:Plus,novolancamento:Plus,
 novohorario:CalendarPlus,marcar:CalendarPlus,marcaratendimento:CalendarPlus,agendar:CalendarPlus,
 editar:PenLine,abrir:FolderOpen,ver:Eye,excluir:Trash2,remover:Trash2,removeritem:Trash2,removerinfusao:Trash2,
 buscar:Search,pesquisar:Search,filtrar:Search,limparfiltro:X,limparfiltros:X,fechar:X,cancelar:X,voltar:ArrowLeft,
 exportar:Download,exportarcsv:Download,exportarexcel:Download,importar:Upload
};

export function iconeDoComando(comando:string){
 return icones[comando]??comuns[(comando.split('.').at(-1)??'').replace(/Command$/,'').toLowerCase()];
}

export function IconeAcao({comando}:{comando:string}){
 const icone=iconeDoComando(comando);
 return icone?<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{icone.map(([tag,attrs],i)=>createElement(tag,{...attrs,key:i}))}</svg>:null;
}
