using UnityEditor;

[CustomEditor(typeof(VXGI))]
[CanEditMultipleObjects]
public class VXGIEditor : Editor {
  [ShaderIncludePath]
  public static string[] GetPaths() {
    return new[] {
      "Assets/Shaders"
    };
  }

  public override void OnInspectorGUI() {
    DrawDefaultInspector();

    VXGI vxgi = (VXGI)target;

    EditorGUILayout.LabelField("Volume", (vxgi.volume / 1000f).ToString("0.00k"));
    EditorGUILayout.LabelField("Voxel buffer scale", vxgi.bufferScale.ToString());
    EditorGUILayout.LabelField("Voxel buffer count", (vxgi.volume * vxgi.bufferScale / 1000f).ToString("0.00k"));
  }
}
