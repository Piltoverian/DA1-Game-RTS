using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class TerrainAtlasAuthoring
{
    [Serializable] public sealed class Receipt { public int version=2; public string[] slots; public string sourceHash, sourceOutputHash; public Entry[] generated; }
    [Serializable] public sealed class Entry { public string key, guid, hash, geometryHash; }
    public static readonly string[] Slots = { "ground-lush","ground-sparse","ramp-earth","mountain-slate","cliff-slate","ground-detail" };

    public static TerrainTheme Import(Texture2D atlas,string folder) => Import(atlas,folder,null);
    public static TerrainTheme Import(Texture2D atlas,string folder,TerrainTheme target)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before importing");
        if(!atlas||!atlas.isReadable||atlas.width%3!=0||atlas.height%2!=0||atlas.width/3!=atlas.height/2)
            throw new ArgumentException("Atlas must be readable, exact 3:2, with six equal square panels; no stretching");
        if(string.IsNullOrEmpty(folder)||!folder.StartsWith("Assets/",StringComparison.Ordinal)||folder.Contains("..")||folder.Contains("\\"))
            throw new ArgumentException("Output must be a project Assets subfolder");
        int size=atlas.width/3;
        if(size<16||size>1024)throw new ArgumentException("Panel size must be 16..1024 pixels");
        string themePath=target?AssetDatabase.GetAssetPath(target):folder+"/Theme.asset",receiptPath=folder+"/import.json";
        if(target&&(string.IsNullOrEmpty(themePath)||!themePath.StartsWith(folder+"/",StringComparison.Ordinal)))throw new ArgumentException("Selected SO must be a saved asset in the output folder");
        string sourcePath=AssetDatabase.GetAssetPath(atlas);
        if(sourcePath==folder+"/material-source.png")throw new ArgumentException("Use original atlas, not generated material-source.png; source must remain unchanged");
        var existing=target?target:AssetDatabase.LoadAssetAtPath<TerrainTheme>(themePath);
        var receipt=File.Exists(receiptPath)?JsonUtility.FromJson<Receipt>(File.ReadAllText(receiptPath)):null;
        var previous=new Dictionary<string,Entry>(); if(receipt?.generated!=null)foreach(var entry in receipt.generated)previous[entry.key]=entry;
        var faces=new Dictionary<string,TerrainCanonicalBaker.Face>();var panels=new Texture2D[6];
        var backups=new Dictionary<string,byte[]>();
        TerrainTheme working=null;Texture2D prepared=null;
        TerrainTheme original=existing?Object.Instantiate(existing):null;
        if(original)original.name=existing.name;
        void Write(string path,byte[] bytes)
        {
            if(!backups.ContainsKey(path))backups[path]=File.Exists(path)?File.ReadAllBytes(path):null;
            File.WriteAllBytes(path,bytes);
        }
        var conflicts=new List<string>();
        var preserved=new HashSet<string>();var protectedPaths=new HashSet<string>();
        Texture2D Current(string key)
        {
            if(!existing)return null;var parts=key.Split('-');
            if(parts[0]=="top")return existing.GetTopSprite(Convert.ToInt32(parts[1],16));
            if(parts[0]=="cliff")return existing.GetWallSprite(int.Parse(parts[1]),Convert.ToInt32(parts[2],16));
            if(parts[0]=="mountain"){int index=Convert.ToInt32(parts[2],16);return existing.mountainCaps!=null&&existing.mountainCaps.Length>index?existing.mountainCaps[index]:null;}
            throw new ArgumentException("Unknown generated face: "+key);
        }
        bool Preserve(string key,Texture2D current)
        {
            if(!existing||!current)return false;
            return preserved.Contains(key);
        }
        try
        {
            if(!Application.isBatchMode)EditorUtility.DisplayProgressBar("Terrain atlas","Chuẩn bị vật liệu và mặt canonical",.05f);
            // Full CPU generation before asset writes; source and scene remain untouched.
            prepared=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false);
            for(int s=0;s<6;s++)
            {
                panels[s]=new Texture2D(size,size,TextureFormat.RGBA32,false);
                var pixels=atlas.GetPixels(s%3*size,(1-s/3)*size,size,size);
                // Blend six percent at opposing borders; derived output only, original atlas remains unchanged.
                int band=Math.Max(1,Mathf.RoundToInt(size*.06f));
                var horizontal=(Color[])pixels.Clone();
                for(int y=0;y<size;y++)for(int x=0;x<band;x++)
                {
                    int a=y*size+x,b=y*size+size-1-x;float weight=.5f*(1-x/(float)band);
                    horizontal[a]=Color.Lerp(pixels[a],pixels[b],weight);horizontal[b]=Color.Lerp(pixels[b],pixels[a],weight);
                }
                var seamless=(Color[])horizontal.Clone();
                for(int y=0;y<band;y++)for(int x=0;x<size;x++)
                {
                    int a=y*size+x,b=(size-1-y)*size+x;float weight=.5f*(1-y/(float)band);
                    seamless[a]=Color.Lerp(horizontal[a],horizontal[b],weight);seamless[b]=Color.Lerp(horizontal[b],horizontal[a],weight);
                }
                panels[s].SetPixels(seamless);panels[s].Apply();prepared.SetPixels(s%3*size,(1-s/3)*size,size,size,seamless);
            }
            prepared.Apply();
            for(int mask=0;mask<16;mask++)
            {
                faces["top-"+mask.ToString("X")]=TerrainCanonicalBaker.Top(panels[mask==0||mask==15?0:2],mask);
                int mountainPixels=Mathf.RoundToInt(32*Mathf.Clamp(existing?existing.mountainHeightScale:4,1,4));
                faces["mountain-cap-"+mask.ToString("X")]=TerrainCanonicalBaker.Top(panels[3],mask,mountainPixels);
            }
            for(int side=0;side<2;side++)foreach(int mask in new[]{4,8,12,13,14})faces[$"cliff-{side}-{mask:X}"]=TerrainCanonicalBaker.Wall(panels[4],side,mask);
            // Resolve ownership before writing ANY image: manual references can point at another output slot.
            foreach(string key in faces.Keys)
            {
                var current=Current(key);if(!current)continue;string path=AssetDatabase.GetAssetPath(current);
                bool changed=!previous.TryGetValue(key,out var entry)||AssetDatabase.AssetPathToGUID(path)!=entry.guid
                    ||(!string.IsNullOrEmpty(entry.hash)&&File.Exists(path)&&Hash128.Compute(File.ReadAllBytes(path)).ToString()!=entry.hash);
                var pairedGeometry=existing.GetGeometry(current);
                if(!changed)
                {
                    string geometryPath=pairedGeometry?AssetDatabase.GetAssetPath(pairedGeometry):"";
                    changed=geometryPath!=folder+"/"+key+"-geometry.png"
                        ||(!string.IsNullOrEmpty(entry.geometryHash)&&File.Exists(geometryPath)&&Hash128.Compute(File.ReadAllBytes(geometryPath)).ToString()!=entry.geometryHash);
                }
                if(changed){preserved.Add(key);conflicts.Add(key);protectedPaths.Add(path);protectedPaths.Add(path.Replace(".png","-geometry.png"));
                    var geometry=existing.GetGeometry(current);if(geometry)protectedPaths.Add(AssetDatabase.GetAssetPath(geometry));}
            }
            // A cap bank shares one tier rise. Preserve the entire bank when a manual cap blocks migration.
            if(existing&&existing.mountainCaps!=null&&existing.mountainCaps.Length==16)
            {
                bool capOverride=false;foreach(string key in preserved)if(key.StartsWith("mountain-cap-",StringComparison.Ordinal))capOverride=true;
                if(capOverride)
                {
                    int capHeight=existing.mountainCaps[0]?existing.mountainCaps[0].height:0;
                    for(int mask=0;mask<16;mask++)
                    {
                        string key="mountain-cap-"+mask.ToString("X");var cap=existing.mountainCaps[mask];
                        if(!cap||cap.height!=capHeight)throw new ArgumentException("Manual mountain cap bank has mixed heights; reconcile all 16 caps before importing");
                        if(preserved.Add(key))conflicts.Add(key+" (whole cap bank retained)");
                        string path=AssetDatabase.GetAssetPath(cap);protectedPaths.Add(path);protectedPaths.Add(path.Replace(".png","-geometry.png"));
                        var geometry=existing.GetGeometry(cap);if(geometry)protectedPaths.Add(AssetDatabase.GetAssetPath(geometry));
                        protectedPaths.Add(folder+"/"+key+".png");protectedPaths.Add(folder+"/"+key+"-geometry.png");
                    }
                }
            }
            if(protectedPaths.Contains(sourcePath)&&sourcePath.StartsWith(folder+"/",StringComparison.Ordinal))throw new ArgumentException("Input source aliases a manual output; choose an independent original atlas");
            void CheckDestination(string key,bool geometry)
            {
                string path=folder+"/"+key+(geometry?"-geometry":"")+".png";
                if(!File.Exists(path)||protectedPaths.Contains(path))return;
                if(!previous.TryGetValue(key,out var entry))throw new ArgumentException("Unowned output collision: "+path+". Choose a dedicated empty folder or preserve this asset as an explicit manual slot.");
                string colorPath=folder+"/"+key+".png";
                if(AssetDatabase.AssetPathToGUID(colorPath)!=entry.guid)throw new ArgumentException("Output GUID no longer belongs to importer: "+path);
                string hash=geometry?entry.geometryHash:entry.hash;
                if(!string.IsNullOrEmpty(hash)&&Hash128.Compute(File.ReadAllBytes(path)).ToString()!=hash)throw new ArgumentException("Unreferenced edited output collision: "+path);
            }
            foreach(string key in faces.Keys){CheckDestination(key,false);CheckDestination(key,true);}
            string sourceOutput=folder+"/material-source.png";
            if(File.Exists(sourceOutput)&&(receipt==null||(!string.IsNullOrEmpty(receipt.sourceOutputHash)&&Hash128.Compute(File.ReadAllBytes(sourceOutput)).ToString()!=receipt.sourceOutputHash)))
                throw new ArgumentException("Edited or unowned material-source collision; choose a dedicated output folder");
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            Texture2D Save(Texture2D image,string name,bool linear=false)
            {
                string path=folder+"/"+name+".png";
                if(protectedPaths.Contains(path))return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                string meta=path+".meta";if(!backups.ContainsKey(meta))backups[meta]=File.Exists(meta)?File.ReadAllBytes(meta):null;
                Write(path,image.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.isReadable=true;importer.sRGBTexture=!linear;
                importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency=!linear;importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            var theme=existing?Object.Instantiate(existing):ScriptableObject.CreateInstance<TerrainTheme>();
            if(existing)theme.name=existing.name;
            working=theme;
            if(!existing) { theme.name=Path.GetFileName(folder);theme.mountainVisualTiers=6;theme.groundRegionCells=18; }
            var entries=new List<Entry>();var depths=new List<TerrainSpriteDepth>();var manualColors=new HashSet<Texture2D>();
            int completed=0;
            foreach(var pair in faces)
            {
                if(!Application.isBatchMode)EditorUtility.DisplayProgressBar("Terrain atlas",pair.Key,.2f+.7f*completed++/faces.Count);
                string key=pair.Key;var image=Save(pair.Value.Color,key);var depth=Save(pair.Value.Geometry,key+"-geometry",true);
                var retained=preserved.Contains(key)?Current(key):image;
                var retainedGeometry=preserved.Contains(key)?existing.GetGeometry(retained):depth;
                if(preserved.Contains(key)&&retained)manualColors.Add(retained);
                if(retained&&retainedGeometry)depths.Add(new TerrainSpriteDepth{Color=retained,Geometry=retainedGeometry});
                if(preserved.Contains(key))entries.Add(previous.TryGetValue(key,out var oldEntry)?oldEntry:new Entry{key=key,guid=""});
                else entries.Add(new Entry{key=key,guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image)),hash=Hash128.Compute(File.ReadAllBytes(AssetDatabase.GetAssetPath(image))).ToString(),geometryHash=depth?Hash128.Compute(File.ReadAllBytes(AssetDatabase.GetAssetPath(depth))).ToString():""});
                var parts=key.Split('-');
                if(parts[0]=="top") { int mask=Convert.ToInt32(parts[1],16);if(!Preserve(key,theme.GetTopSprite(mask)))theme.SetTopSprite(mask,image); }
                else if(parts[0]=="cliff") { int side=int.Parse(parts[1]),mask=Convert.ToInt32(parts[2],16);if(!Preserve(key,theme.GetWallSprite(side,mask)))theme.SetWallSprite(side,mask,image); }
                else if(parts[0]=="mountain") { int mask=Convert.ToInt32(parts[2],16);if(theme.mountainCaps==null||theme.mountainCaps.Length!=16)theme.mountainCaps=new Texture2D[16];if(!Preserve(key,theme.mountainCaps[mask]))theme.mountainCaps[mask]=image; }
            }
            theme.sourceMaterialAtlas=Save(prepared,"material-source");theme.spriteDepths=depths.ToArray();theme.manualColorOverrides=new List<Texture2D>(manualColors).ToArray();theme.materialAtlasShader=Shader.Find("RTS/Terrain Material Atlas");
            // Receipts/output validation precede publication of the SO.
            Write(receiptPath,System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Receipt{slots=Slots,sourceHash=Hash128.Compute(atlas.GetRawTextureData()).ToString(),sourceOutputHash=Hash128.Compute(File.ReadAllBytes(sourceOutput)).ToString(),generated=entries.ToArray()},true)));
            Write(folder+"/conflicts.txt",System.Text.Encoding.UTF8.GetBytes(conflicts.Count==0?"No manual overrides":("Preserved manual overrides; color + geometry must match before rendering:\n"+string.Join("\n",conflicts))));
            if(!existing)AssetDatabase.CreateAsset(theme,themePath);
            else {Undo.RecordObject(existing,"Import terrain material atlas");EditorUtility.CopySerialized(theme,existing);Object.DestroyImmediate(theme);working=null;theme=existing;}
            EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
            AssetDatabase.Refresh();return theme;
        }
        catch
        {
            // Restore prior output pixels if any filesystem/import stage failed; source is never written.
            foreach(var entry in backups){if(entry.Value!=null)File.WriteAllBytes(entry.Key,entry.Value);else if(File.Exists(entry.Key))File.Delete(entry.Key);}
            if(existing&&original){EditorUtility.CopySerialized(original,existing);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);}
            else if(AssetDatabase.LoadAssetAtPath<TerrainTheme>(themePath))AssetDatabase.DeleteAsset(themePath);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);throw;
        }
        finally {if(!Application.isBatchMode)EditorUtility.ClearProgressBar();if(original)Object.DestroyImmediate(original);if(prepared)Object.DestroyImmediate(prepared);if(working&&!AssetDatabase.Contains(working))Object.DestroyImmediate(working);foreach(var panel in panels)if(panel)Object.DestroyImmediate(panel);foreach(var face in faces.Values){Object.DestroyImmediate(face.Color);Object.DestroyImmediate(face.Geometry);} }
    }
}
