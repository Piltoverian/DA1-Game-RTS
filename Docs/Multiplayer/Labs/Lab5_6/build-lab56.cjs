const fs=require('node:fs'),path=require('node:path');
const scripts=['../Lab5/lab5-bake.js','../Lab5/lab5-connectivity.js','../Lab5/lab5-visual-tiles.js','../Lab5/lab5-engine.js','../Lab6/lab6-engine.js'];
const atlas=fs.readFileSync(path.resolve(__dirname,'../../../../Assets/Art/Terrain/Lab5TextureReview_v17/atlas.png')).toString('base64');
const html=`<!doctype html><html lang="vi"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Lab 5 + 6 — Map và texture</title>
<style>body{margin:0;background:#18221d;color:#edf2ec;font:15px system-ui}main{max-width:1500px;margin:auto;padding:18px}h1{font-size:24px}.controls{display:flex;flex-wrap:wrap;gap:12px;align-items:center}input,select,button{font:inherit;background:#304235;color:white;border:1px solid #60755d;border-radius:5px;padding:6px}input[type=number]{width:90px}button{cursor:pointer}canvas{display:block;width:100%;max-width:1100px;height:auto;touch-action:none;background:#17211d}pre{white-space:pre-wrap;max-height:330px;overflow:auto;background:#223026;padding:12px}.layout{display:flex;gap:16px}.viewer{flex:1;min-width:0}aside{width:320px}p{line-height:1.5}#status{color:#c9e3bc}@media(max-width:850px){.layout{display:block}aside{width:auto}}</style>
<main><h1>Lab 5 + Lab 6 — Terrain, đường đi và tilemap</h1>
<div class="controls"><label>Seed <input id="seed" type="number" value="30000"></label><label>Người chơi <input id="players" type="number" min="2" max="10" value="4"></label><label>Rộng ramp <select id="width"><option>1</option><option selected>3</option><option>5</option><option>7</option></select></label><button id="gen" disabled>Gen map</button></div>
<p id="status">Đang tải atlas…</p><div class="controls"><select id="mode"><option value="texture">Texture Lab 6</option><option value="height">HeightLevel Lab 5</option><option value="walk">Walk mask Lab 5</option></select><label><input id="ramps" type="checkbox" checked> Ô ramp</label><label><input id="grid" type="checkbox"> Lưới + độ cao</label><label><input id="blocked" type="checkbox"> Collision / ô bị chặn</label><button id="fit">Toàn map</button><button id="detail">Vùng 10×10</button><button id="export">Xuất JSON</button></div>
<p>Cuộn để zoom, kéo để di chuyển; click để xem ô. Shift+click hai ô để thử đường đi. Tất cả chế độ dùng cùng map; zoom 418 pixel/ô hiển thị kích thước gốc của sprite.</p>
<div class="layout"><div class="viewer"><canvas id="map" width="1152" height="1152"></canvas></div><aside><h2>Thông tin ô</h2><pre id="inspect">Click một ô trên map.</pre><p id="route"></p><p>Núi màu xám và nước màu xanh là màu tạm. Texture V17 còn cần hoàn thiện đầu cliff–ramp và ô cliff nhiều mảnh. Bản thử nghiệm này tắt resource để tập trung terrain.</p></aside></div></main>
${scripts.map(f=>'<script>'+fs.readFileSync(path.join(__dirname,f),'utf8').replace(/<\/script/gi,'<\\/script')+'</script>').join('\n')}
<script>window.LAB56_ATLAS='data:image/png;base64,${atlas}';</script>
<script>${fs.readFileSync(path.join(__dirname,'lab56-app.js'),'utf8')}</script></html>`;
fs.writeFileSync(path.join(__dirname,'Lab5_6_Combined_Map_Texture.html'),html);
console.log('Built self-contained Lab5_6_Combined_Map_Texture.html');


