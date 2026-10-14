using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RTS/Terrain Theme")]
public class TerrainTheme : ScriptableObject
{
    public const int CanvasPixels = 128;
    public const int TierPixels = 32;
    public const float CameraPitch = 30;
    public const float CameraYaw = 45;

    [Header("Ground")]
    [InspectorName("Flat ground (mask 0)")]
    [Tooltip("top-0.png; 128x128 readable PNG. No raised corners.") ]
    public Texture2D topFlatMask0;
    [Tooltip("Presentation-only seed. Does not consume the terrain or resource generator RNG.")]
    public int groundVisualSeed = 40;
    [Min(2), Tooltip("Scale of coherent lush/sparse ground patches in grid cells.")]
    public float groundRegionCells = 16;
    [Header("Ramps - raised corners")]
    [InspectorName("00 high (mask 1)")]
    [Tooltip("top-1.png; 128x128 readable PNG. Raised corners: 00.") ]
    public Texture2D topHigh00Mask1;
    [InspectorName("10 high (mask 2)")]
    [Tooltip("top-2.png; 128x128 readable PNG. Raised corners: 10.") ]
    public Texture2D topHigh10Mask2;
    [InspectorName("00 + 10 high (mask 3)")]
    [Tooltip("top-3.png; 128x128 readable PNG. Raised corners: 00 + 10.") ]
    public Texture2D topHigh00_10Mask3;
    [InspectorName("11 high (mask 4)")]
    [Tooltip("top-4.png; 128x128 readable PNG. Raised corners: 11.") ]
    public Texture2D topHigh11Mask4;
    [InspectorName("00 + 11 high (mask 5)")]
    [Tooltip("top-5.png; 128x128 readable PNG. Raised corners: 00 + 11.") ]
    public Texture2D topHigh00_11Mask5;
    [InspectorName("10 + 11 high (mask 6)")]
    [Tooltip("top-6.png; 128x128 readable PNG. Raised corners: 10 + 11.") ]
    public Texture2D topHigh10_11Mask6;
    [InspectorName("00 + 10 + 11 high (mask 7)")]
    [Tooltip("top-7.png; 128x128 readable PNG. Raised corners: 00 + 10 + 11.") ]
    public Texture2D topHigh00_10_11Mask7;
    [InspectorName("01 high (mask 8)")]
    [Tooltip("top-8.png; 128x128 readable PNG. Raised corners: 01.") ]
    public Texture2D topHigh01Mask8;
    [InspectorName("00 + 01 high (mask 9)")]
    [Tooltip("top-9.png; 128x128 readable PNG. Raised corners: 00 + 01.") ]
    public Texture2D topHigh00_01Mask9;
    [InspectorName("10 + 01 high (mask A)")]
    [Tooltip("top-A.png; 128x128 readable PNG. Raised corners: 10 + 01.") ]
    public Texture2D topHigh10_01MaskA;
    [InspectorName("00 + 10 + 01 high (mask B)")]
    [Tooltip("top-B.png; 128x128 readable PNG. Raised corners: 00 + 10 + 01.") ]
    public Texture2D topHigh00_10_01MaskB;
    [InspectorName("11 + 01 high (mask C)")]
    [Tooltip("top-C.png; 128x128 readable PNG. Raised corners: 11 + 01.") ]
    public Texture2D topHigh11_01MaskC;
    [InspectorName("00 + 11 + 01 high (mask D)")]
    [Tooltip("top-D.png; 128x128 readable PNG. Raised corners: 00 + 11 + 01.") ]
    public Texture2D topHigh00_11_01MaskD;
    [InspectorName("10 + 11 + 01 high (mask E)")]
    [Tooltip("top-E.png; 128x128 readable PNG. Raised corners: 10 + 11 + 01.") ]
    public Texture2D topHigh10_11_01MaskE;
    [Header("Top - all corners raised")]
    [InspectorName("00 + 10 + 11 + 01 high (mask F)")]
    [Tooltip("top-F.png; 128x128 readable PNG. Raised corners: 00 + 10 + 11 + 01.") ]
    public Texture2D topHigh00_10_11_01MaskF;

    [Header("Cliff -Z - endpoint masks")]
    [InspectorName("HighB (mask 4)")]
    [Tooltip("cliff-0-4.png; endpoint bits lowA/lowB/highB/highA. HighB.") ]
    public Texture2D cliffNegativeZHighBMask4;
    [InspectorName("HighA (mask 8)")]
    [Tooltip("cliff-0-8.png; endpoint bits lowA/lowB/highB/highA. HighA.") ]
    public Texture2D cliffNegativeZHighAMask8;
    [InspectorName("Full wall (mask C)")]
    [Tooltip("cliff-0-C.png; endpoint bits lowA/lowB/highB/highA. HighB + HighA.") ]
    public Texture2D cliffNegativeZHighBHighAMaskC;
    [InspectorName("LowA + HighB + HighA (mask D)")]
    [Tooltip("cliff-0-D.png; endpoint bits lowA/lowB/highB/highA. LowA + HighB + HighA.") ]
    public Texture2D cliffNegativeZLowAHighBHighAMaskD;
    [InspectorName("LowB + HighB + HighA (mask E)")]
    [Tooltip("cliff-0-E.png; endpoint bits lowA/lowB/highB/highA. LowB + HighB + HighA.") ]
    public Texture2D cliffNegativeZLowBHighBHighAMaskE;

    [Header("Cliff -X - endpoint masks")]
    [InspectorName("HighB (mask 4)")]
    [Tooltip("cliff-1-4.png; endpoint bits lowA/lowB/highB/highA. HighB.") ]
    public Texture2D cliffNegativeXHighBMask4;
    [InspectorName("HighA (mask 8)")]
    [Tooltip("cliff-1-8.png; endpoint bits lowA/lowB/highB/highA. HighA.") ]
    public Texture2D cliffNegativeXHighAMask8;
    [InspectorName("Full wall (mask C)")]
    [Tooltip("cliff-1-C.png; endpoint bits lowA/lowB/highB/highA. HighB + HighA.") ]
    public Texture2D cliffNegativeXHighBHighAMaskC;
    [InspectorName("LowA + HighB + HighA (mask D)")]
    [Tooltip("cliff-1-D.png; endpoint bits lowA/lowB/highB/highA. LowA + HighB + HighA.") ]
    public Texture2D cliffNegativeXLowAHighBHighAMaskD;
    [InspectorName("LowB + HighB + HighA (mask E)")]
    [Tooltip("cliff-1-E.png; endpoint bits lowA/lowB/highB/highA. LowB + HighB + HighA.") ]
    public Texture2D cliffNegativeXLowBHighBHighAMaskE;

    [Header("Mountain ranges")]
    [Tooltip("16 imported rock caps, all with the same canvas height.")]
    public Texture2D[] mountainCaps = Array.Empty<Texture2D>();
    [Range(1,6)] public int mountainVisualTiers = 6;
    [Header("Material atlas (RTS/Terrain/Material Atlas)")]
    public Shader materialAtlasShader;
    public Texture2D sourceMaterialAtlas;
    public TerrainSpriteDepth[] spriteDepths = Array.Empty<TerrainSpriteDepth>();
    [Tooltip("Imported manual colors use their own pixels instead of world-space material blending.")]
    public Texture2D[] manualColorOverrides = Array.Empty<Texture2D>();
    [Range(0.1f,1)] public float mountainFadeOpacity = .25f;
    [Range(.1f,3)] public float mountainFadeRadiusCells = 1.5f;
    [Range(1,4),Tooltip("Visual mountain tier scale. Reimport the source atlas after changing this; navigation heights remain unchanged.")]
    public float mountainHeightScale = 4;

    public Texture2D GetTopSprite(int mask) => mask switch
    {
        0 => topFlatMask0,
        1 => topHigh00Mask1,
        2 => topHigh10Mask2,
        3 => topHigh00_10Mask3,
        4 => topHigh11Mask4,
        5 => topHigh00_11Mask5,
        6 => topHigh10_11Mask6,
        7 => topHigh00_10_11Mask7,
        8 => topHigh01Mask8,
        9 => topHigh00_01Mask9,
        10 => topHigh10_01MaskA,
        11 => topHigh00_10_01MaskB,
        12 => topHigh11_01MaskC,
        13 => topHigh00_11_01MaskD,
        14 => topHigh10_11_01MaskE,
        15 => topHigh00_10_11_01MaskF,
        _ => throw new ArgumentOutOfRangeException(nameof(mask), mask, "Top mask must be 0..15")
    };

    public Texture2D GetWallSprite(int side, int mask)
    {
        ValidateWallKey(side, mask);
        return (side * 16 + mask) switch
        {
            0 => null,
            1 => null,
            2 => null,
            3 => null,
            4 => cliffNegativeZHighBMask4,
            5 => null,
            6 => null,
            7 => null,
            8 => cliffNegativeZHighAMask8,
            9 => null,
            10 => null,
            11 => null,
            12 => cliffNegativeZHighBHighAMaskC,
            13 => cliffNegativeZLowAHighBHighAMaskD,
            14 => cliffNegativeZLowBHighBHighAMaskE,
            15 => null,
            16 => null,
            17 => null,
            18 => null,
            19 => null,
            20 => cliffNegativeXHighBMask4,
            21 => null,
            22 => null,
            23 => null,
            24 => cliffNegativeXHighAMask8,
            25 => null,
            26 => null,
            27 => null,
            28 => cliffNegativeXHighBHighAMaskC,
            29 => cliffNegativeXLowAHighBHighAMaskD,
            30 => cliffNegativeXLowBHighBHighAMaskE,
            31 => null,
            _ => throw new ArgumentOutOfRangeException(nameof(mask))
        };
    }

    // Offline authoring uses the same named slots as runtime.
    public void SetTopSprite(int mask, Texture2D texture)
    {
        switch (mask)
        {
            case 0: topFlatMask0 = texture; break;
            case 1: topHigh00Mask1 = texture; break;
            case 2: topHigh10Mask2 = texture; break;
            case 3: topHigh00_10Mask3 = texture; break;
            case 4: topHigh11Mask4 = texture; break;
            case 5: topHigh00_11Mask5 = texture; break;
            case 6: topHigh10_11Mask6 = texture; break;
            case 7: topHigh00_10_11Mask7 = texture; break;
            case 8: topHigh01Mask8 = texture; break;
            case 9: topHigh00_01Mask9 = texture; break;
            case 10: topHigh10_01MaskA = texture; break;
            case 11: topHigh00_10_01MaskB = texture; break;
            case 12: topHigh11_01MaskC = texture; break;
            case 13: topHigh00_11_01MaskD = texture; break;
            case 14: topHigh10_11_01MaskE = texture; break;
            case 15: topHigh00_10_11_01MaskF = texture; break;
            default: throw new ArgumentOutOfRangeException(nameof(mask), mask, "Top mask must be 0..15");
        }
    }

    public void SetWallSprite(int side, int mask, Texture2D texture)
    {
        ValidateWallKey(side, mask);
        if (!IsVisibleWallMask(mask))
        {
            if (texture) throw new ArgumentException("Empty wall masks have no sprite field", nameof(texture));
            return;
        }
        switch (side * 16 + mask)
        {
            case 4: cliffNegativeZHighBMask4 = texture; break;
            case 8: cliffNegativeZHighAMask8 = texture; break;
            case 12: cliffNegativeZHighBHighAMaskC = texture; break;
            case 13: cliffNegativeZLowAHighBHighAMaskD = texture; break;
            case 14: cliffNegativeZLowBHighBHighAMaskE = texture; break;
            case 20: cliffNegativeXHighBMask4 = texture; break;
            case 24: cliffNegativeXHighAMask8 = texture; break;
            case 28: cliffNegativeXHighBHighAMaskC = texture; break;
            case 29: cliffNegativeXLowAHighBHighAMaskD = texture; break;
            case 30: cliffNegativeXLowBHighBHighAMaskE = texture; break;
        }
    }
    public Texture2D GetGeometry(Texture2D color)
    {
        if(spriteDepths!=null)foreach(var pair in spriteDepths)if(pair.Color==color)return pair.Geometry;
        return null;
    }
    public bool UsesManualColor(Texture2D color) => manualColorOverrides!=null&&Array.IndexOf(manualColorOverrides,color)>=0;

    public bool IsRenderReady(out string reason)
    {
        reason=null;
        if(!materialAtlasShader||materialAtlasShader.name!="RTS/Terrain Material Atlas"||!sourceMaterialAtlas)
        { reason="Import a 3x2 material atlas with the terrain depth shader";return false; }
        if(mountainCaps==null||mountainCaps.Length!=16||!mountainCaps[0])
        { reason="Mountain caps require all 16 imported masks";return false; }
        bool Valid(Texture2D color)
        {
            var geometry=GetGeometry(color);
            return color&&color.isReadable&&geometry&&geometry.isReadable&&geometry.width==color.width&&geometry.height==color.height;
        }
        for(int mask=0;mask<16;mask++)
        {
            var cap=mountainCaps[mask];
            if(!Valid(GetTopSprite(mask))||!Valid(cap)||cap.width!=CanvasPixels||cap.height!=mountainCaps[0].height)
            { reason="Missing/mismatched color + geometry or mixed mountain cap heights at mask "+mask.ToString("X")+"; reconcile the manual slot or reimport";return false; }
        }
        for(int side=0;side<2;side++)foreach(int mask in new[]{4,8,12,13,14})if(!Valid(GetWallSprite(side,mask)))
        { reason="Missing color + geometry for cliff "+side+"-"+mask.ToString("X")+"; reconcile the manual slot or reimport";return false; }
        return true;
    }

    public static bool IsVisibleWallMask(int mask) => mask == 4 || mask == 8 || mask == 12 || mask == 13 || mask == 14;

    static void ValidateWallKey(int side, int mask)
    {
        if (side < 0 || side > 1) throw new ArgumentOutOfRangeException(nameof(side), side, "Wall side must be 0 (-Z) or 1 (-X)");
        if (mask < 0 || mask > 15) throw new ArgumentOutOfRangeException(nameof(mask), mask, "Wall mask must be 0..15");
    }

    public float ResolveTierRise(float cellSize, Vector3 cameraForward)
    {
        var expected = Quaternion.Euler(CameraPitch, CameraYaw, 0) * Vector3.forward;
        if (!(cellSize > 0) || Vector3.Distance(cameraForward.normalized, expected) > .001f)
            throw new InvalidOperationException("Canonical sprites require positive grid cell size and camera pitch30/yaw45");
        return cellSize * ((float)TierPixels / CanvasPixels) * Mathf.Sqrt(2) / Mathf.Cos(CameraPitch * Mathf.Deg2Rad);
    }
}

[Serializable]
public struct TerrainSpriteDepth
{
    public Texture2D Color;
    public Texture2D Geometry;
}
