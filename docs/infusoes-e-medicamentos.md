# Prescrição de infusão e medicamentos

Prescrições › Infusão abre uma única infusão. **Criar nova infusão** acrescenta outro preparo. Diluente, volume total, via, tempo e horário pertencem ao grupo; quantidade/dose e observações pertencem ao medicamento. O PDF e a execução no portal identificam os grupos.

**Modelos › Salvar infusão como modelo** guarda a composição selecionada. Indicação, observações do paciente e horário não são copiados. Ao usar um modelo, revise o preparo e as doses antes de liberar para enfermagem.

**Prescrições › Medicamentos**, no Consultório e no Gerente, acessa o mesmo catálogo. Permite criar, editar, desativar sugestões e importar CSV UTF-8 com cabeçalho `Nome;PrincipioAtivo;Apresentacao;Fabricante`. O catálogo não depende do estoque. Importações duplicadas são recusadas antes da gravação.

As sugestões começam com duas letras e ignoram acentos. Os sete produtos informados pela clínica têm prioridade: tramadol, cetoprofeno, dipirona, ondansetrona, lidocaína, glicose hipertônica e hidrocortisona. A seleção preenche somente nome e apresentação. Na lidocaína, **20 mL** descreve o frasco; **Quantidade / dose** fica livre, com indicação de mL. Dados não informados pela clínica não são inferidos.

A base complementar contém nomes DCB publicados pela Anvisa (IN 462/2026). Não representa uma lista de produtos injetáveis nem define concentração, indicação, dose ou compatibilidade. Fonte, filtro e hash estão em `src/Clinica.Application/Recursos/Medicamentos/fonte.json`. Alterações feitas pela clínica ficam no banco compartilhado e prevalecem sobre os cadastros iniciais.

## Publicação

Aplicar a migration aditiva `20261002162759_GruposInfusaoECatalogoMedicamentos` antes de distribuir os novos desktops. O atualizador preserva backup, testa homologação e concede leitura/gravação do catálogo ao papel do desktop. A API recebe e devolve `grupoInfusao`; versões antigas que tentem descartar grupos na correção são recusadas.

O modo de continuidade sem assinatura continua controlado pela configuração existente. Liberação, identificação do responsável, conferência de alergias e checagem de cada item continuam sendo registradas. A impressão é opcional.

## Verificação

- `MedicamentosEGruposTests` e `GruposInfusaoPortalTests`: catálogo, permissões, importação atômica, persistência, modelos, grupos e execução sem assinatura.
- `tools/VerificarInfusao`: abre as telas WPF com banco fictício, verifica salvar/reabrir, modelos, quantidade e liberação; gera capturas reais e PDF.
- Portal: `testar-grupos-infusao.mjs`, `testar-continuidade-sem-a1.mjs`, `testar-modelos-formatados.mjs`.
