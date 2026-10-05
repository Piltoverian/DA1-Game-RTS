(function(host){
function draw(canvas,map,options,color){
const ctx=canvas.getContext('2d'),W=map.W,N=canvas.width,s=N/W,view=options.view,bg=color('--background'),fg=color('--foreground'),grass=color('--viz-series-3'),earth=color('--viz-series-2'),water=color('--viz-series-1'),stone=fg,route=color('--viz-series-4'),lo=Math.min(...map.levels),hi=Math.max(...map.levels);
const mix=(a,b,t)=>a.map((v,i)=>Math.round(v*(1-t)+b[i]*t)),rgb=c=>'rgb('+c.join(',')+')';function hash(x,y){let n=Math.imul(x+map.cfg.seed,374761393)^Math.imul(y,668265263);n=Math.imul(n^(n>>>13),1274126177);return (n>>>0)/4294967296;}
const resourceMask=new Uint8Array(W*W);for(const p of map.resources||[])for(let dy=0;dy<2;dy++)for(let dx=0;dx<2;dx++)resourceMask[(p.y+dy)*W+p.x+dx]=1;
const rampWeight=new Float32Array(W*W);for(const r of map.ramps){for(const i of r.cells){if(r.connectivityRepair){rampWeight[i]=.7;continue;}const x=i%W+.5,y=Math.floor(i/W)+.5,t=Math.abs((x-r.x)*r.dx+(y-r.y)*r.dy)/(r.length/2),side=Math.abs(-(x-r.x)*r.dy+(y-r.y)*r.dx)/(r.width/2);rampWeight[i]=Math.max(0,1-t*t)*Math.max(.2,1-side*side);}}
const image=ctx.createImageData(N,N);for(let py=0;py<N;py++)for(let px=0;px<N;px++){const x=Math.min(W-1,Math.floor(px/s)),y=Math.min(W-1,Math.floor(py/s)),i=y*W+x,l=(map.levels[i]-lo)/Math.max(1,hi-lo),r=map.rampCells[i]>=0,cliff=map.cliffMask[i]&&!r,noise=hash(px,py);let c;
if(view==='walk')c=map.walk[i]?mix(bg,grass,.7):mix(bg,fg,.7);
else if(view==='levels')c=mix(bg,water,.15+l*.75);
else if(view==='bake')c=r?mix(bg,grass,.75):cliff?mix(bg,fg,.8):mix(bg,water,.12);
else{const xx=x/7,yy=y/7,gx=Math.floor(xx),gy=Math.floor(yy),u=xx-gx,v=yy-gy,patch=(hash(gx,gy)*(1-u)+hash(gx+1,gy)*u)*(1-v)+(hash(gx,gy+1)*(1-u)+hash(gx+1,gy+1)*u)*v;c=mix(mix(bg,grass,.52+l*.22),earth,.03+patch*.10);c=mix(c,noise>.5?bg:fg,Math.abs(noise-.5)*.08);if(cliff){c=mix(mix(bg,earth,.2),stone,.3+noise*.16);if(py%s<s*.23)c=mix(c,bg,.16);}else if(r){c=mix(c,mix(bg,earth,.42),rampWeight[i]*.75);c=mix(c,fg,noise*.04);}else if(!map.walk[i]&&!resourceMask[i]){c=mix(bg,fg,.25+noise*.12);}}
image.data.set([...c,255],(py*N+px)*4);}
ctx.putImageData(image,0,0);
if(view==='terrain'){
// Rim highlights and contact shadows use the baked cells, with no displaced geometry.
for(let y=1;y<W-1;y++)for(let x=1;x<W-1;x++){const i=y*W+x;if(map.cliffMask[i]&&map.rampCells[i]<0){for(const [dx,dy]of [[1,0],[0,1],[-1,0],[0,-1]]){const j=(y+dy)*W+x+dx;if(map.levels[j]<=map.levels[i])continue;ctx.strokeStyle=rgb(mix(fg,bg,.3));ctx.lineWidth=Math.max(1,s*.32);ctx.beginPath();if(dx){const xx=(x+(dx>0?1:0))*s;ctx.moveTo(xx,y*s);ctx.lineTo(xx,(y+1)*s);}else{const yy=(y+(dy>0?1:0))*s;ctx.moveTo(x*s,yy);ctx.lineTo((x+1)*s,yy);}ctx.stroke();}}
if((map.mountainMask?.[i]||map.kind?.[i]===4)&&!resourceMask[i]&&hash(x,y)>.58){ctx.fillStyle=rgb(mix(bg,fg,.45));ctx.beginPath();ctx.moveTo((x+.1)*s,(y+.7)*s);ctx.lineTo((x+.35)*s,(y+.15)*s);ctx.lineTo((x+.8)*s,(y+.2)*s);ctx.lineTo((x+1)*s,(y+.8)*s);ctx.closePath();ctx.fill();}}
}
const palette={main:earth,secondary:earth,advantage:color('--viz-series-4'),contested:color('--viz-series-5')};
for(const p of map.resources||[]){const c=palette[p.role];if(view==='terrain'){ctx.fillStyle=rgb(mix(bg,fg,.32));ctx.beginPath();ctx.ellipse((p.x+1)*s,(p.y+1.55)*s,.85*s,.32*s,0,0,Math.PI*2);ctx.fill();for(let k=0;k<3;k++){const xx=(p.x+.45+(k%2)*.8)*s,yy=(p.y+.65+Math.floor(k/2)*.6)*s;ctx.fillStyle=rgb(mix(c,k===1?fg:bg,k===1?.18:.25));ctx.beginPath();ctx.moveTo(xx-.35*s,yy+.3*s);ctx.lineTo(xx-.2*s,yy-.35*s);ctx.lineTo(xx+.2*s,yy-.42*s);ctx.lineTo(xx+.42*s,yy+.15*s);ctx.closePath();ctx.fill();}}else{ctx.fillStyle=rgb(c);ctx.fillRect(p.x*s,p.y*s,2*s,2*s);}}
if(options.debug){ctx.strokeStyle=rgb(mix(bg,fg,.6));ctx.lineWidth=1;ctx.setLineDash([3,4]);for(const c of map.clusters||[]){ctx.beginPath();ctx.arc(c.x*s,c.y*s,c.radius*s,0,Math.PI*2);ctx.stroke();}ctx.setLineDash([]);ctx.fillStyle=rgb(fg);ctx.font='500 14px sans-serif';for(const r of map.ramps)if(r.cells.length)ctx.fillText('R',(r.labelX??r.x)*s,(r.labelY??r.y)*s);}
ctx.lineWidth=3;ctx.strokeStyle=rgb(route);ctx.beginPath();(options.path||[]).forEach((p,i)=>i?ctx.lineTo(p.x*s,p.y*s):ctx.moveTo(p.x*s,p.y*s));ctx.stroke();ctx.font='500 16px sans-serif';for(const b of map.bases){const x=b.x*s,y=b.y*s;if(options.debug){ctx.strokeStyle=rgb(fg);ctx.lineWidth=1;ctx.setLineDash([5,4]);ctx.beginPath();ctx.arc(x,y,(map.cfg.protectedRadius??3)*s,0,Math.PI*2);ctx.stroke();ctx.setLineDash([]);}ctx.fillStyle=rgb(bg);ctx.beginPath();ctx.arc(x,y,14,0,Math.PI*2);ctx.fill();ctx.strokeStyle=rgb(fg);ctx.lineWidth=1.5;ctx.stroke();ctx.fillStyle=rgb(fg);ctx.fillText('P'+(b.id+1),x-10,y+5);}if(options.dot){ctx.fillStyle=rgb(fg);ctx.beginPath();ctx.arc(options.dot.x*s,options.dot.y*s,3,0,Math.PI*2);ctx.fill();}
}
host.RTSOverview={draw};
})(typeof window!=='undefined'?window:globalThis);

