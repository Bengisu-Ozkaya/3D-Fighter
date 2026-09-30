using UnityEngine;
using UnityEditor;

public class EmaceArtMaterialFixer
{
    [MenuItem("Tools/3D Fighter/EmaceArt Pembe Materyalleri Duzelt")]
    public static void FixMaterials()
    {
        string[] matPaths = new string[]
        {
            "Assets/EmaceArt - raft on the desert/Materials/Material_metal.mat",
            "Assets/EmaceArt - raft on the desert/Materials/Material_organic.mat",
            "Assets/EmaceArt - raft on the desert/Materials/Material_standard.mat",
            "Assets/EmaceArt - raft on the desert/Materials/Material_syntetic.mat",
            "Assets/EmaceArt - raft on the desert/Materials/Material_wood.mat",
            "Assets/EmaceArt - raft on the desert/Materials/Material_decal.mat"
        };

        Shader standardShader = Shader.Find("Standard");
        if (standardShader == null)
        {
            Debug.LogError("[EmaceArtMaterialFixer] Unity 'Standard' shader bulunamadı!");
            return;
        }

        int count = 0;
        foreach (string path in matPaths)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Debug.LogWarning($"[EmaceArtMaterialFixer] Materyal bulunamadı: {path}");
                continue;
            }

            Undo.RecordObject(mat, "Fix EmaceArt Material Shader");

            mat.shader = standardShader;

            if (mat.name.ToLower().Contains("decal"))
            {
                // Decal materyali için Cutout (Alpha Test) modu
                mat.SetFloat("_Mode", 1); // 1 = Cutout
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                mat.SetFloat("_Cutoff", 0.5f);
            }
            else
            {
                // Standart opak materyaller
                mat.SetFloat("_Mode", 0); // 0 = Opaque
                mat.SetOverrideTag("RenderType", "");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                mat.SetInt("_ZWrite", 1);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.DisableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = -1;
                mat.SetFloat("_Glossiness", 0.1f); // Hafif mat, temiz görünüm
            }

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"<color=green>[3D Fighter]</color> {count} adet EmaceArt materyali Standard Shader'a dönüştürüldü ve pembe görünüm düzeltildi!");
    }
}
