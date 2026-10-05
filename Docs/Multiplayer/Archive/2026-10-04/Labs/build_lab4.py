from pathlib import Path
import subprocess
base = Path(__file__).resolve().parent
output = Path(r'C:\Users\Legion\.codex\visualizations\2026\10\02\01a0fb52-0b8c-7821-a195-11d976c05a18\rts-mapgen-lab.html')
fragment = (base / 'lab4-view.fragment.html').read_text(encoding='utf-8').replace('/* ENGINE */', (base / 'lab4-levels.js').read_text(encoding='utf-8') + '\n' + (base / 'lab4-engine.js').read_text(encoding='utf-8'))
output.write_text(fragment, encoding='utf-8')
subprocess.run(['python', r'C:\Users\Legion\.codex\plugins\cache\openai-bundled\visualize\1.0.45\skills\visualize\scripts\render.py', '--force', str(output), str(base / 'Lab4_Height_Noise_Walkability_Balance.html')], check=True)

