from pathlib import Path
import html
import re
base=Path(__file__).resolve().parent
output=base/'Lab5_FlatY_Cliff_Ramp_Bake.html'
engine='\n'.join((base/p).read_text(encoding='utf-8') for p in ['lab5-bake.js','lab5-connectivity.js','lab5-visual-tiles.js','lab5-engine.js','lab5-clusters.js','lab5-overview.js'])
inner=(base/'lab5-view.fragment.html').read_text(encoding='utf-8').replace('/* ENGINE */',engine)
outer=output.read_text(encoding='utf-8')
outer,count=re.subn(r'data-srcdoc="[\s\S]*?"',lambda _: 'data-srcdoc="'+html.escape(inner,quote=True)+'"',outer,count=1)
if count!=1:
    raise ValueError('Missing standalone iframe wrapper')
output.write_text(outer,encoding='utf-8')
print('Built '+output.name)

