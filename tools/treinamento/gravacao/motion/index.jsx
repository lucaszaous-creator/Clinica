// A raiz do Remotion: uma composição "Aula", cuja duração sai da linha do tempo gravada.
import React from 'react';
import { Composition, registerRoot } from 'remotion';
import { Aula } from './Aula';
import tempos from './tempos.json';

const Raiz = () => (
  <Composition
    id="Aula"
    component={Aula}
    fps={tempos.fps}
    width={tempos.largura}
    height={tempos.altura}
    durationInFrames={300}
    defaultProps={{ titulo: 'Aula', categoria: 'Recepção', descricao: '', nomeApp: 'Recepção', video: '', duracao: 5, tela: { largura: 1440, altura: 810, escala: 4 / 3 }, passos: [], funcoes: [] }}
    calculateMetadata={({ props }) => ({
      durationInFrames: Math.round((tempos.abertura + props.duracao + tempos.fechamento) * tempos.fps),
    })}
  />
);

registerRoot(Raiz);
