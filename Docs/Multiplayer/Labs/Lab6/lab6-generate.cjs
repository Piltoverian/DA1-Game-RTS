const fs=require('node:fs'),path=require('node:path');
const E=require('../Lab5/lab5-engine.js'),L=require('./lab6-engine.js');
const seed=Number(process.argv[2]??30000);
if(!Number.isSafeInteger(seed))throw Error('Seed must be an integer');
const cfg={seed,players:4,size:256,fullMap:true,protectedRadius:6,rampWidthMin:3,rampWidthMax:7,mainCount:0,secondaryCount:0,advantageCount:0,contestedCount:0};
const map=E.generate(cfg),plan=L.compile(map),sample={seed,cfg,mapSize:map.W,attempt:map.attempt,fullTerrainIslandCount:map.islandCount,...L.crop(map,plan)};
const out=path.resolve(__dirname,'../../../../Assets/Art/Terrain/Lab6');fs.mkdirSync(out,{recursive:true});
fs.copyFileSync(path.resolve(out,'../Lab5TextureReview_v17/atlas.png'),path.join(out,'atlas.png'));
fs.writeFileSync(path.join(out,'sample_10x10.json'),JSON.stringify(sample,null,2));
fs.writeFileSync(path.join(out,'render_plan.json'),JSON.stringify({seed,cfg,...plan},null,2));
fs.writeFileSync(path.join(out,'terrain.json'),JSON.stringify({seed,W:map.W,H:map.H,bases:map.bases,kind:Array.from(map.kind),levels:Array.from(map.levels),walk:Array.from(map.walk),mountainMask:Array.from(map.mountainMask)}));
fs.writeFileSync(path.join(__dirname,'lab6-data.js'),'window.LAB6_DATA='+JSON.stringify({sample,plan})+';');
console.log(JSON.stringify({seed,crop:sample.crop,islands:map.islandCount,draws:plan.draws.length,warnings:plan.warnings.length,out}));


