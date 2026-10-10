using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Imports the checked-in material library; offline generation tooling has been retired.
public static class TerrainMaterialAuthoring
{
    public const string ThemePath="Assets/_Project/Data/Theme/SO/JadeSlateTerrainTheme.asset";
    public const string Folder="Artifacts/TerrainVisual";
    [MenuItem("RTS/Terrain/Import Jade Slate Material Library")]
    public static void Build(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before import.");
        Directory.CreateDirectory(Folder);
        var theme=AssetDatabase.LoadAssetAtPath<TerrainTheme>(ThemePath);
        if(!theme){theme=Object.Instantiate(StockIsometricSpriteValidation.LoadTheme());theme.name="JadeSlateTerrainTheme";AssetDatabase.CreateAsset(theme,ThemePath);}
        Texture2D Load(string name){
            string path="Assets/_Project/Data/Terrain/Sprites/JadeSlate/"+name+".png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);if(!importer)throw new Exception("Import missing "+path);
            importer.textureType=TextureImporterType.Default;importer.isReadable=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        for(int m=0;m<16;m++)theme.SetTopSprite(m,Load("top-"+m.ToString("X")));
        for(int s=0;s<2;s++)foreach(int m in new[]{4,8,12,13,14})theme.SetWallSprite(s,m,Load("cliff-"+s+"-"+m.ToString("X")));
        theme.flatGroundVariants=new Texture2D[32];for(int i=0;i<32;i++)theme.flatGroundVariants[i]=Load("ground-"+i);
        theme.mountainCaps=new Texture2D[16];for(int i=0;i<16;i++)theme.mountainCaps[i]=Load("mountain-cap-"+i.ToString("X"));
        theme.fringeDecorations=new Texture2D[4];for(int i=0;i<4;i++)theme.fringeDecorations[i]=Load("decor-"+i);
        theme.smoothTextureSampling=true;theme.groundPatternColumns=4;theme.groundVisualSeed=40;theme.groundRegionCells=18;theme.mountainVisualTiers=6;theme.fringeDecorationDensity=.06f;
        theme.groundArtwork=null;theme.rampArtwork=null;theme.cliffArtwork=null;theme.groundSpriteCorners=null;theme.rampSpriteCorners=null;theme.cliffSpriteCorners=null;theme.mountainArtwork=null;
        EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
        CanonicalTerrainValidation.RunWithTheme(theme);
        foreach(string name in new[]{"terrain.png","atlas.png","render.txt"})File.Copy("Artifacts/TerrainValidation/Canonical/"+name,Folder+"/"+name,true);
        File.WriteAllText(Folder+"/authoring.txt","PASS: imported 78 canonical PNGs and validated JadeSlate theme.\n");
    }
    [MenuItem("RTS/Terrain/Use Validated Jade Slate In Main")]
    public static void Promote(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play before assigning theme.");
        if(!File.ReadAllText(Folder+"/render.txt").StartsWith("PASS"))throw new Exception("Validate theme first.");
        var renderer=Object.FindFirstObjectByType<TerrainMapRenderer>();if(!renderer||renderer.gameObject.scene.path!="Assets/_Project/Scenes/Main.unity")throw new Exception("Open Main first.");
        if(renderer.gameObject.scene.isDirty)throw new Exception("Main has unrelated unsaved edits; cannot save automatically.");
        File.Copy(renderer.gameObject.scene.path,Folder+"/Main-before-materials.unity",true);
        Undo.RecordObject(renderer,"Use Jade Slate terrain materials");renderer.theme=AssetDatabase.LoadAssetAtPath<TerrainTheme>(ThemePath);EditorUtility.SetDirty(renderer);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);if(!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(renderer.gameObject.scene))throw new Exception("Unable to save Main.");
        File.WriteAllText(Folder+"/promotion.txt","PASS JadeSlate assigned to Main. Previous scene backed up.\n");
    }
}
