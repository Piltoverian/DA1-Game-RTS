using System;
using System.Collections.Generic;
using Unity.Mathematics;

// Managed port of the verified lab policies. Scratch belongs to the map, never a
// shared static pool: concurrent generators cannot overwrite each other's labels.
public static class TerrainTopologyOptimizer
{
    public static int Next(int i, int d, GridComponent g)
    {
        int x=i%g.width;
        return d==0?(i>=g.width?i-g.width:-1):d==1?(x+1<g.width?i+1:-1):
            d==2?(i+g.width<g.width*g.height?i+g.width:-1):(x>0?i-1:-1);
    }
    static void Scratch(GeneratedTerrain m)
    {
        int n=m.cells.Length;
        if(m.floodQueue==null||m.floodQueue.Length!=n){m.floodQueue=new int[n];m.walkLabels=new int[n];m.cliffLabels=new int[n];}
    }
    public static void Bake(GeneratedTerrain m,TerrainGenerationSettings s)
    {
        int w=m.grid.width,h=m.grid.height;var cells=m.cells;
        for(int i=0;i<cells.Length;i++){cells[i].isCliff=false;cells[i].walkable=!cells[i].isMountain;cells[i].RampId=-1;cells[i].RampDirection=-1;cells[i].RampInfluence=false;}
        m.rampCount=0;
        void Face(int i,int j){
            if(cells[j].isMountain)return;int diff=Math.Abs(cells[i].heightLevel-cells[j].heightLevel);if(diff==0)return;
            int high=cells[i].heightLevel>cells[j].heightLevel?i:j,depth=diff*s.cliffCells;
            if(depth==1){cells[high].isCliff=true;cells[high].walkable=false;return;}
            int x=high%w,y=high/w;
            for(int dy=1-depth;dy<depth;dy++)for(int dx=1-depth;dx<depth;dx++){
                if(Math.Abs(dx)+Math.Abs(dy)>=depth)continue;int xx=x+dx,yy=y+dy;if(xx<0||yy<0||xx>=w||yy>=h)continue;
                int c=yy*w+xx;if(cells[c].isMountain||cells[c].heightLevel!=cells[high].heightLevel)continue;cells[c].isCliff=true;cells[c].walkable=false;
            }
        }
        for(int y=0,i=0;y<h;y++)for(int x=0;x<w;x++,i++)if(!cells[i].isMountain){if(x+1<w)Face(i,i+1);if(y+1<h)Face(i,i+w);}
        for(int y=0,i=0;y<h;y++)for(int x=0;x<w;x++,i++)if(!cells[i].isMountain){
            int level=cells[i].heightLevel,n=y>0?i-w:-1,e=x+1<w?i+1:-1,b=y+1<h?i+w:-1,a=x>0?i-1:-1;
            bool hn=n>=0&&!cells[n].isMountain,he=e>=0&&!cells[e].isMountain,hb=b>=0&&!cells[b].isMountain,ha=a>=0&&!cells[a].isMountain;
            if(hn&&cells[n].heightLevel<level||he&&cells[e].heightLevel<level||hb&&cells[b].heightLevel<level||ha&&cells[a].heightLevel<level)continue;
            if(hn&&he&&cells[n].heightLevel==level&&cells[e].heightLevel==level&&!cells[n+1].isMountain&&cells[n+1].heightLevel<level||
               he&&hb&&cells[e].heightLevel==level&&cells[b].heightLevel==level&&!cells[b+1].isMountain&&cells[b+1].heightLevel<level||
               hb&&ha&&cells[b].heightLevel==level&&cells[a].heightLevel==level&&!cells[b-1].isMountain&&cells[b-1].heightLevel<level||
               ha&&hn&&cells[a].heightLevel==level&&cells[n].heightLevel==level&&!cells[n-1].isMountain&&cells[n-1].heightLevel<level){cells[i].isCliff=true;cells[i].walkable=false;}
        }
    }
    public static int[] Labels(GeneratedTerrain m, bool cliffs, out int count, bool owned=false)
    {
        if(m==null)throw new ArgumentNullException(nameof(m));
        if(m.cells==null)throw new InvalidOperationException("Terrain cells are null");
        Scratch(m);m.componentPasses++;
        int[] labels=cliffs?m.cliffLabels:m.walkLabels;Array.Clear(labels,0,labels.Length);count=0;
        bool Allowed(int i)=>cliffs?!m.cells[i].isMountain&&m.cells[i].isCliff:m.cells[i].walkable;
        for(int i=0;i<labels.Length;i++)if(labels[i]==0&&Allowed(i)){
            int head=0,tail=1;labels[i]=++count;m.floodQueue[0]=i;
            while(head<tail){int a=m.floodQueue[head++];for(int d=0;d<4;d++){
                int b=Next(a,d,m.grid);if(b<0||labels[b]!=0||!Allowed(b)||cliffs&&m.cells[a].heightLevel!=m.cells[b].heightLevel)continue;
                labels[b]=count;m.floodQueue[tail++]=b;
            }}
        }
        return owned?(int[])labels.Clone():labels;
    }
    public static void PrepareBases(GeneratedTerrain m,TerrainGenerationSettings s,uint seed)
    {
        int w=m.grid.width,h=m.grid.height,r=(int)math.floor(s.protectedRadius),min=int.MaxValue,max=int.MinValue;
        foreach(var c in m.cells){min=Math.Min(min,c.heightLevel);max=Math.Max(max,c.heightLevel);}
        var offsets=new List<int2>();for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++)if(x*x+y*y<(r+1)*(r+1))offsets.Add(new int2(x,y));
        var rng=new Unity.Mathematics.Random(math.max(1u,seed^0x947a53e1u));
        float phase=math.PI/4+rng.NextFloat(-.12f,.12f),sector=2*math.PI/s.players;
        m.spawnSectorOffset=phase;m.spawns=new int2[s.players];m.baseCore=new bool[m.cells.Length];
        float2 center=new float2((w-1)*.5f,(h-1)*.5f);
        for(int p=0;p<s.players;p++){
            float best=float.PositiveInfinity;int2 chosen=default;int level=0;bool found=false;
            foreach(float ring in new[]{.35f,.30f,.40f})foreach(float shift in new[]{0f,-.15f,.15f}){
                float angle=phase+p*sector+shift*sector;
                int2 pos=(int2)math.round(center+new float2(math.cos(angle),math.sin(angle))*math.min(w,h)*ring);
                if(pos.x-r<0||pos.y-r<0||pos.x+r>=w||pos.y+r>=h)continue;
                float2 vector=(float2)pos-center;float actual=math.atan2(vector.y,vector.x),delta=math.atan2(math.sin(actual-phase-p*sector),math.cos(actual-phase-p*sector));
                if(math.abs(delta)>sector*.5f||math.length(vector)*math.sin(sector*.5f-math.abs(delta))<r+1)continue;
                bool overlap=false;for(int q=0;q<p;q++)if(math.distance((float2)pos,m.spawns[q])<2*(r+1)){overlap=true;break;}if(overlap)continue;
                var hist=new int[max-min+1];foreach(var o in offsets)hist[m.cells[(pos.y+o.y)*w+pos.x+o.x].heightLevel-min]++;
                int acc=0,median=min;for(int k=0;k<hist.Length;k++){acc+=hist[k];if(acc>offsets.Count/2){median=min+k;break;}}
                float edits=0;for(int k=0;k<hist.Length;k++)edits+=hist[k]*Math.Abs(min+k-median);
                int exits=0;foreach(var d in GridTerrain.RampDirections){int2 q=pos+d*(r+1);if(q.x>=0&&q.y>=0&&q.x<w&&q.y<h&&Math.Abs(m.cells[q.y*w+q.x].heightLevel-median)<=1)exits++;}
                float score=edits/offsets.Count+(4-exits)*.25f;if(score>=best)continue;
                best=score;chosen=pos;level=median;found=true;
            }
            if(!found)throw new InvalidOperationException($"No legal base center in player {p}'s sector (radius {r})");
            m.spawns[p]=chosen;foreach(var o in offsets){int i=(chosen.y+o.y)*w+chosen.x+o.x;SetHeight(m,i,level);m.baseCore[i]=true;}
            int reach=r+Math.Max(level-min,max-level)+1;
            for(int y=Math.Max(0,chosen.y-reach);y<=Math.Min(h-1,chosen.y+reach);y++)for(int x=Math.Max(0,chosen.x-reach);x<=Math.Min(w-1,chosen.x+reach);x++){
                int i=y*w+x;if(m.baseCore[i])continue;float distance=math.distance(new float2(x,y),chosen);if(distance<r+1)continue;
                int step=(int)math.floor(distance-r);SetHeight(m,i,math.clamp(m.cells[i].heightLevel,level-step,level+step));
            }
        }
        ProtectBaseGeometry(m,s);
    }
    static void SetHeight(GeneratedTerrain m,int i,int height){if(m.cells[i].heightLevel!=height)m.baseHeightChanged++;m.cells[i].heightLevel=height;}
    public static void ProtectBaseGeometry(GeneratedTerrain m,TerrainGenerationSettings s)
    {
        if(m.baseCore==null)return;
        int min=int.MaxValue,max=int.MinValue,w=m.grid.width,h=m.grid.height;foreach(var c in m.cells){min=Math.Min(min,c.heightLevel);max=Math.Max(max,c.heightLevel);}
        int radius=Math.Max(1,(max-min)*s.cliffCells+1);
        var bounds=new Dictionary<int,int2>();
        for(int i=0;i<m.cells.Length;i++)if(m.baseCore[i]){
            int x=i%w,y=i/w;
            if(x>0&&x+1<w&&y>0&&y+1<h&&m.baseCore[i-1]&&m.baseCore[i+1]&&m.baseCore[i-w]&&m.baseCore[i+w])continue;
            int level=m.cells[i].heightLevel;
            for(int yy=Math.Max(0,y-radius);yy<=Math.Min(h-1,y+radius);yy++)for(int xx=Math.Max(0,x-radius);xx<=Math.Min(w-1,x+radius);xx++){
                int c=yy*w+xx,step=Math.Max(0,(Math.Max(Math.Abs(xx-x),Math.Abs(yy-y))-1)/s.cliffCells);
                int lo=level-step,hi=level+step;if(bounds.TryGetValue(c,out var previous)){lo=Math.Max(lo,previous.x);hi=Math.Min(hi,previous.y);}
                if(lo>hi||m.baseCore[c]&&(m.cells[c].heightLevel<lo||m.cells[c].heightLevel>hi))throw new InvalidOperationException("Incompatible base transition rings");
                bounds[c]=new int2(lo,hi);
            }
        }
        foreach(var pair in bounds){SetHeight(m,pair.Key,math.clamp(m.cells[pair.Key].heightLevel,pair.Value.x,pair.Value.y));if(pair.Value.x==pair.Value.y)m.cells[pair.Key].isMountain=false;}
    }
    sealed class Lane
    {
        public int start,direction,back,front,slopes;
        public readonly List<int> cells=new List<int>();
    }
    sealed class Door
    {
        public List<Lane> lanes;public int cells,serial;public uint tie;
    }
    sealed class Heap
    {
        readonly List<Door> items=new List<Door>();public int Count=>items.Count;
        static bool Before(Door a,Door b)=>a.cells!=b.cells?a.cells>b.cells:a.tie!=b.tie?a.tie<b.tie:a.serial<b.serial;
        public void Push(Door value){int i=items.Count;items.Add(value);while(i>0){int p=(i-1)/2;if(Before(items[p],value))break;items[i]=items[p];i=p;}items[i]=value;}
        public Door Pop(){var top=items[0];var last=items[items.Count-1];items.RemoveAt(items.Count-1);if(items.Count>0){int i=0;while(2*i+1<items.Count){int c=2*i+1;if(c+1<items.Count&&Before(items[c+1],items[c]))c++;if(Before(last,items[c]))break;items[i]=items[c];i=c;}items[i]=last;}return top;}
    }
    public static void OpenRamps(GeneratedTerrain m,TerrainGenerationSettings s)
    {
        var cliff=Labels(m,true,out int cliffs);var walk=Labels(m,false,out int components);
        var size=new int[cliffs+1];foreach(int id in cliff)size[id]++;var opened=new int[cliffs+1];
        var parent=new int[components+1];for(int i=0;i<parent.Length;i++)parent[i]=i;
        int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
        var lanes=new Dictionary<long,Lane>();
        for(int i=0;i<m.cells.Length;i++)if(m.cells[i].isCliff&&!m.cells[i].isMountain&&!Core(m,i))for(int d=0;d<4;d++){
            int back=Next(i,(d+2)%4,m.grid);if(back<0||!m.cells[back].walkable||m.cells[i].heightLevel-m.cells[back].heightLevel!=1)continue;
            var lane=new Lane{start=i,direction=d,back=back};int j=i,previous=back;bool valid=true;
            // Same-level cliff corridor retains Unity's support for cliffCells > 1.
            while(j>=0&&m.cells[j].isCliff){
                int diff=m.cells[j].heightLevel-m.cells[previous].heightLevel;
                if(m.cells[j].isMountain||Core(m,j)||(diff!=0&&diff!=1)){valid=false;break;}
                if(diff==1&&TerrainGeneration.LowerMask(m,j)!=(1<<((d+2)%4))){valid=false;break;}
                for(int side=0;side<4;side++){
                    int other=Next(j,side,m.grid);if(other<0||!m.cells[other].walkable||m.cells[other].heightLevel==m.cells[j].heightLevel)continue;
                    if(other!=previous){valid=false;break;}
                }
                if(!valid)break;lane.cells.Add(j);if(diff==1)lane.slopes++;int front=Next(j,d,m.grid);
                if(front<0||m.cells[front].isMountain){valid=false;break;}previous=j;j=front;
            }
            if(!valid||j<0||!m.cells[j].walkable||m.cells[j].heightLevel!=m.cells[previous].heightLevel||lane.slopes>s.maximumRampCells)continue;
            lane.front=j;lanes[(long)i*4+d]=lane;
        }
        m.rampCandidateLanes=lanes.Count;
        var seen=new HashSet<long>();var connectivity=new Heap();var quota=new Heap();int serial=0;
        var rng=new Unity.Mathematics.Random(math.max(1u,(s.seed+(uint)m.attempt*7919)^0x7f4a7c15u));
        void Enqueue(List<Lane> row,uint tie){if(row.Count==0)return;var door=new Door{lanes=row,cells=row.Count*row[0].slopes,tie=tie,serial=serial++};var l=row[0];(Root(walk[l.back])!=Root(walk[l.front])?connectivity:quota).Push(door);}
        var keys=new List<long>(lanes.Keys);keys.Sort();
        foreach(long key in keys){var lane=lanes[key];if(seen.Contains(key))continue;
            bool Same(Lane l){
                if(l==null||l.direction!=lane.direction||l.cells.Count!=lane.cells.Count||l.slopes!=lane.slopes||walk[l.back]!=walk[lane.back]||walk[l.front]!=walk[lane.front])return false;
                for(int k=0;k<l.cells.Count;k++)if(m.cells[l.cells[k]].heightLevel!=m.cells[lane.cells[k]].heightLevel)return false;return true;
            }
            Lane first=lane;
            while(true){int k=Next(first.start,(lane.direction+3)%4,m.grid);if(k<0||!lanes.TryGetValue((long)k*4+lane.direction,out var previous)||!Same(previous))break;first=previous;}
            var strip=new List<Lane>();Lane current=first;
            while(Same(current)){strip.Add(current);seen.Add((long)current.start*4+current.direction);int k=Next(current.start,(lane.direction+1)%4,m.grid);if(k<0||!lanes.TryGetValue((long)k*4+lane.direction,out current))break;}
            int width=Math.Max(1,s.maximumRampCells/lane.slopes);
            for(int k=0;k<strip.Count;k+=width)Enqueue(strip.GetRange(k,Math.Min(width,strip.Count-k)),rng.NextUInt());
        }
        while(connectivity.Count>0||quota.Count>0){
            bool primary=connectivity.Count>0;var door=primary?connectivity.Pop():quota.Pop();var first=door.lanes[0];
            if(primary&&Root(walk[first.back])==Root(walk[first.front])){quota.Push(door);continue;}
            var parts=new List<List<Lane>>();var part=new List<Lane>();
            foreach(var lane in door.lanes){bool conflict=false;foreach(int c in lane.cells)if(m.cells[c].RampId>=0){conflict=true;break;}
                if(conflict){if(part.Count>0)parts.Add(part);part=new List<Lane>();}else part.Add(lane);
            }
            if(part.Count>0)parts.Add(part);
            if(parts.Count!=1||parts[0].Count!=door.lanes.Count){foreach(var segment in parts)Enqueue(segment,door.tie);continue;}
            bool needed=Root(walk[first.back])!=Root(walk[first.front]);
            foreach(var lane in door.lanes)foreach(int c in lane.cells)if(opened[cliff[c]]<Math.Max(1,(size[cliff[c]]+11)/12))needed=true;
            if(!needed)continue;
            int id=m.rampCount++;
            foreach(var lane in door.lanes){int previous=lane.back;foreach(int c in lane.cells){
                m.cells[c].walkable=true;m.cells[c].RampId=id;
                // Direction marks actual slopes only; plateau corridor is walkable.
                m.cells[c].RampDirection=m.cells[c].heightLevel-m.cells[previous].heightLevel==1?lane.direction:-1;
                if(m.cells[c].RampDirection>=0){opened[cliff[c]]++;m.cells[previous].RampInfluence=true;int front=Next(c,lane.direction,m.grid);m.cells[front].RampInfluence=true;}previous=c;
            }}
            // Union every touched island, not just the end labels.
            int target=Root(walk[first.back]);foreach(var lane in door.lanes)foreach(int c in lane.cells){
                walk[c]=target;for(int d=0;d<4;d++){int j=Next(c,d,m.grid);if(j>=0&&walk[j]>0)parent[Root(walk[j])]=target;}
            }
        }
        m.rampQuotaShortfall=0;for(int i=1;i<size.Length;i++)m.rampQuotaShortfall+=Math.Max(0,Math.Max(1,(size[i]+11)/12)-opened[i]);
    }
    static bool Core(GeneratedTerrain m,int i)=>m.baseCore!=null&&m.baseCore[i];
    public static bool RepairProtectedPockets(GeneratedTerrain m,TerrainGenerationSettings s)
    {
        if(m.baseCore==null)return false;var labels=Labels(m,false,out int count);int main=labels[m.spawns[0].y*m.grid.width+m.spawns[0].x];
        if(main==0)return false;foreach(var spawn in m.spawns)if(labels[spawn.y*m.grid.width+spawn.x]!=main)return false;
        var affected=new HashSet<int>();for(int i=0;i<labels.Length;i++)if(Core(m,i)&&labels[i]>0&&labels[i]!=main)affected.Add(labels[i]);
        if(affected.Count==0)return false;
        for(int i=0;i<labels.Length;i++)if(affected.Contains(labels[i])){
            int owner=0;float nearest=float.PositiveInfinity;for(int p=0;p<m.spawns.Length;p++){float d=math.distancesq(new float2(i%m.grid.width,i/m.grid.width),m.spawns[p]);if(d<nearest){nearest=d;owner=p;}}
            SetHeight(m,i,m.cells[m.spawns[owner].y*m.grid.width+m.spawns[owner].x].heightLevel);m.cells[i].isMountain=false;m.basePocketRepairedCells++;
        }
        ProtectBaseGeometry(m,s);return true;
    }
}
