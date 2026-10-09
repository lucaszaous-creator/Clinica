export type RotaFinanceira = { chave: string; rotulo: string };
export type GrupoFinanceiro = { chave: string; rotulo: string; rotas: RotaFinanceira[] };

// Agrupa apenas as rotas autorizadas pelo host. Rotas futuras continuam acessíveis.
export function agruparRotas(rotas: RotaFinanceira[]): GrupoFinanceiro[] {
  const grupos = [
    { chave: 'caixa', rotulo: 'Caixa', chaves: ['caixa', 'fechamento-caixa', 'fluxo-caixa'] },
    { chave: 'contas', rotulo: 'Contas', chaves: ['contas', 'inadimplencia', 'plano-contas'] },
    { chave: 'recebimentos', rotulo: 'Recebimentos', chaves: ['recebiveis', 'conciliacao', 'extrato-banco', 'taxas'] },
    { chave: 'gestao', rotulo: 'Gestão', chaves: ['pacotes', 'estoque', 'repasses'] },
    { chave: 'analises', rotulo: 'Análises', chaves: ['resultado', 'producao'] },
  ];
  const conhecidas = new Set(grupos.flatMap(g => g.chaves));
  const disponiveis = new Map(rotas.map(r => [r.chave, r]));
  const saida = grupos.map(g => ({ chave: g.chave, rotulo: g.rotulo,
    rotas: g.chaves.flatMap(chave => disponiveis.has(chave) ? [disponiveis.get(chave)!] : []) }));
  const outras = [...disponiveis.values()].filter(r => !conhecidas.has(r.chave));
  if (outras.length) saida.push({ chave: 'outros', rotulo: 'Mais', rotas: outras });
  return saida.filter(g => g.rotas.length > 0);
}
