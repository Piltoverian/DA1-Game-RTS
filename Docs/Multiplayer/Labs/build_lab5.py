from pathlib import Path
import subprocess
base=Path(__file__).resolve().parent
output=Path(r'C:\Users\Legion\.codex\visualizations\2026\10\02\01a0fb52-0b8c-7821-a195-11d976c05a18\rts-cliff-bake-lab.html')
engine='\n'.join((base/p).read_text(encoding='utf-8') for p in ['lab5-bake.js','lab5-connectivity.js','lab5-engine.js','lab5-clusters.js','lab5-overview.js'])
output.write_text((base/'lab5-view.fragment.html').read_text(encoding='utf-8').replace('/* ENGINE */',engine),encoding='utf-8')
subprocess.run(['python',r'C:\Users\Legion\.codex\plugins\cache\openai-bundled\visualize\1.0.45\skills\visualize\scripts\render.py','--force',str(output),str(base/'Lab5_FlatY_Cliff_Ramp_Bake.html')],check=True)
