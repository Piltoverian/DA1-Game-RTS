using System;
using UnityEditor;
using UnityEngine;

// Editor orchestration only; extractor, geometry baker and theme ownership live outside UI.
public sealed class TerrainAtlasWindow : EditorWindow
{
    Texture2D atlas;
    TerrainTheme theme;
    Texture2D preview;
    string output = "Assets/_Project/Data/Terrain/Generated/CustomTerrain";
    string status = "Chọn atlas vật liệu 3×2. Unity tạo mặt dimetric và cập nhật cùng SO.";
    Vector2 scroll;
    [MenuItem("RTS/Terrain/Material Atlas")]
    public static void Open(){var window=GetWindow<TerrainAtlasWindow>("Terrain Material Atlas");window.minSize=new Vector2(450,460);}
    void OnDisable(){if(preview)DestroyImmediate(preview);}
    void OnGUI()
    {
        scroll=EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Atlas → mặt dimetric → TerrainTheme",EditorStyles.boldLabel);
        atlas=(Texture2D)EditorGUILayout.ObjectField("Atlas 3×2",atlas,typeof(Texture2D),false);
        output=EditorGUILayout.TextField("Output folder",output);
        var selected=(TerrainTheme)EditorGUILayout.ObjectField("Cùng SO khi import lại",theme,typeof(TerrainTheme),false);
        if(selected!=theme) { theme=selected;if(theme)output=System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(theme)).Replace('\\','/'); }
        if(atlas)
        {
            float width=Mathf.Min(position.width-30,720),height=width*2/3;
            var rect=GUILayoutUtility.GetRect(width,height,GUILayout.ExpandWidth(false));GUI.DrawTexture(rect,atlas,ScaleMode.StretchToFill);
            for(int i=0;i<6;i++) { var slot=new Rect(rect.x+(i%3)*width/3,rect.y+(i/3)*height/2,width/3,22);EditorGUI.DrawRect(slot,new Color(0,0,0,.7f));GUI.Label(slot,TerrainAtlasAuthoring.Slots[i],EditorStyles.whiteLabel); }
            EditorGUILayout.LabelField($"{atlas.width}×{atlas.height} — góc ảnh trên trái; không stretch khi import");
            if(!atlas.isReadable)
            {
                EditorGUILayout.HelpBox("Bật Read/Write để cắt vật liệu. Chỉ cấu hình importer, không sửa pixel nguồn.",MessageType.Warning);
                if(GUILayout.Button("Enable Read/Write")) { var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(atlas)) as TextureImporter;if(importer){importer.isReadable=true;importer.SaveAndReimport();} }
            }
        }
        using(new EditorGUI.DisabledScope(!atlas||!atlas.isReadable||EditorApplication.isPlayingOrWillChangePlaymode))
        if(GUILayout.Button("Import / cập nhật cùng SO"))
        {
            try { if(theme)Undo.RecordObject(theme,"Import terrain material atlas");theme=TerrainAtlasAuthoring.Import(atlas,output,theme);Selection.activeObject=theme;
                status=theme.IsRenderReady(out string reason)?"Import hoàn tất. Cùng SO; preview để kiểm tra seam/cao độ.":"Override được giữ. Chưa render được: "+reason+". Xem conflicts.txt."; }
            catch(Exception error){status=error.Message;Debug.LogException(error);}
        }
        EditorGUILayout.HelpBox(status,MessageType.Info);
        using(new EditorGUI.DisabledScope(!theme||EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if(GUILayout.Button("Preview terrain atlas"))
            {
                try { var nextPreview=TerrainAtlasPreview.Render(theme);if(preview)DestroyImmediate(preview);preview=nextPreview;status="Preview atlas đã cập nhật."; }
                catch(Exception e){status=e.Message;Debug.LogException(e);}
            }
            if(GUILayout.Button("Gán theme cho TerrainMapRenderer đã chọn"))
            {
                var target=Selection.activeGameObject?Selection.activeGameObject.GetComponent<TerrainMapRenderer>():null;
                if(!target)status="Chọn GameObject có TerrainMapRenderer trong Hierarchy.";
                else { Undo.RecordObject(target,"Assign terrain theme");target.theme=theme;EditorUtility.SetDirty(target);status="Đã gán theme; scene chưa được tự lưu. Chạy Play để xem."; }
            }
        }
        if(preview){EditorGUILayout.LabelField("Preview terrain",EditorStyles.boldLabel);float width=Mathf.Min(position.width-30,720);var area=GUILayoutUtility.GetRect(width,width*preview.height/preview.width,GUILayout.ExpandWidth(false));GUI.DrawTexture(area,preview,ScaleMode.ScaleToFit);}
        EditorGUILayout.HelpBox("Phong cách: JadeSlate painterly. Ground yên, ramp ấm, slate xanh xám. Nguồn phẳng không có bóng đổ. Prompt/layout: Docs/TerrainVisualizer/11.",MessageType.None);
        EditorGUILayout.EndScrollView();
    }
}
