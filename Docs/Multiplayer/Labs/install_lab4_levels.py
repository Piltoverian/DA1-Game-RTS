from pathlib import Path
p=Path(__file__).with_name('lab4-engine.js');s=p.read_text(encoding='utf-8')
s=s.replace("  const clamp=", "  const Tiers=typeof module!=='undefined'&&module.exports?require('./lab4-levels.js'):host.RTSTiers;\n  const clamp=")
a=s.index('  function canStep(');b=s.index('  function dijkstra(',a)
s=s[:a]+'''  function canStep(walk,W,H,x,y,nx,ny){
    if(x<0||y<0||x>=W||y>=H||nx<0||ny<0||nx>=W||ny>=H||Math.abs(nx-x)>1||Math.abs(ny-y)>1||!walk[y*W+x]||!walk[ny*W+nx])return false;
    const T=W*H,i=y*W+x,j=ny*W+nx;
    const cardinal=(a,b)=>!walk.levels||walk.levels[a]===walk.levels[b]||!!walk.rampLinks?.has(Tiers.key(a,b,T));
    if(x===nx||y===ny)return cardinal(i,j);
    const a=y*W+nx,b=ny*W+x;return !!(walk[a]&&walk[b]&&cardinal(i,a)&&cardinal(a,j)&&cardinal(i,b)&&cardinal(b,j));
  }
''' +s[b:]
s=s.replace('rampEnabled:input.rampEnabled!==false,', 'levelStep:clamp(Number(input.levelStep??1),.5,3),rampsPerBorder:clamp(Math.round(Number(input.rampsPerBorder??2)),1,4),rampEnabled:input.rampEnabled!==false,')
s=s.replace('if(cfg.rampEnabled)for(const b of bases)', 'if(false)for(const b of bases)')
s=s.replace('    let {walk,clearance}=compileWalk(physical,W,H,cfg.radius);', '    let {walk,clearance}=compileWalk(physical,W,H,cfg.radius);\n    const tierData=Tiers.build(walk,height,W,H,cfg,seed,components);\n    for(let i=0;i<T;i++)if(tierData.rampCells[i]>=0)kind[i]=3;\n    for(const b of bases)b.level=tierData.levels[b.y*W+b.x]*cfg.levelStep;')
s=s.replace('({walk,clearance}=compileWalk(physical,W,H,cfg.radius));','({walk,clearance}=compileWalk(physical,W,H,cfg.radius));tierData.attach(walk);')
s=s.replace("if(role!=='starter'&&bases.some(o=>rampSample(p.x,p.y,o).d<cfg.rampWidth/2+1))continue;", "if(ids.some(i=>tierData.rampCells[i]>=0)||ids.some(i=>tierData.levels[i]!==tierData.levels[ids[0]]))continue;")
s=s.replace('let islands=components(walk,W,H),pruned=0;', 'let islands=components(walk,W,H),pruned=0;')
# Pruning must not erase a disconnected tier just because its economic anchors are absent.
s=s.replace('if(walk[i]&&!keep.has(islands.labels[i]))', 'if(false&&walk[i]&&!keep.has(islands.labels[i]))')
s=s.replace('kind,protectedMask,expectedPlacements,height,slope,', 'kind,protectedMask,expectedPlacements,height,rawHeight:height,levels:tierData.levels,cellHeight:tierData.surface,ramps:tierData.ramps,rampCells:tierData.rampCells,rampLinks:tierData.links,regionCount:tierData.regionCount,slope,')
s=s.replace('for(const h of height)digest=', 'for(const n of [cfg.levelStep*256,cfg.rampsPerBorder])digest=Math.imul(digest^n,16777619);for(const link of tierData.links)digest=Math.imul(digest^(link|0),16777619);for(const h of height)digest=')
s=s.replace('canStep,dijkstra,Noise};', 'canStep,dijkstra,components,Noise};')
p.write_text(s,encoding='utf-8')
b=Path(__file__).with_name('build_lab4.py');s=b.read_text(encoding='utf-8');s=s.replace("(base / 'lab4-engine.js').read_text(encoding='utf-8')", "(base / 'lab4-levels.js').read_text(encoding='utf-8') + '\\n' + (base / 'lab4-engine.js').read_text(encoding='utf-8')");b.write_text(s,encoding='utf-8')
