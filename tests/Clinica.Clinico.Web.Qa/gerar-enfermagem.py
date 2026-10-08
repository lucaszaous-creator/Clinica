import runpy
from pathlib import Path
m=runpy.run_path('tests/Clinica.Clinico.Web.Qa/gerar-secoes.py');m['output'].clear()
for view,typ,method in [('EscreverSessaoWindow','EscreverSessaoViewModel','EscreverSessaoCompleta'),('EvolucaoEnfermagemWindow','EvolucaoEnfermagemViewModel','PassagemCompleta'),('ConsultaDeEnfermagemWindow','EvolucaoEnfermagemViewModel','ConsultaCompleta')]:m['generate'](view,'Clinica.Desktop.Shell.Componentes.'+typ,method)
Path('src/Clinica.Desktop.Shell/Web/RegistroCompartilhadoWeb.EnfermagemSecoes.cs').write_text('using System.Linq;\nusing P=Clinica.Desktop.Shell.Web.PaginasWebController;\nnamespace Clinica.Desktop.Shell.Web;\npublic static partial class RegistroCompartilhadoWeb {\n'+'\n\n'.join(m['output'])+'\n}\n',encoding='utf-8')
print('\n'.join(m['issues'][-8:]))
