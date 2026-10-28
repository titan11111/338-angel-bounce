using UnityEditor;
using UnityEngine;

// Persist shader references so native/Web player stripping cannot remove parcel shaders.
[InitializeOnLoad]
public static class AngelVisualAssets
{
    static AngelVisualAssets() { EditorApplication.delayCall += Ensure; }
    public static void Ensure()
    {
        foreach (string name in new[] {"ParcelPaper", "ParcelRibbon", "ParcelGold"})
        {
            string path="Assets/Resources/"+name+".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
            var shader=Shader.Find("Standard");
            if (!shader) throw new System.InvalidOperationException("Built-in Standard shader is required.");
            var mat=new Material(shader);mat.name=name;
            AssetDatabase.CreateAsset(mat,path);
        }
        AssetDatabase.SaveAssets();
    }
}
