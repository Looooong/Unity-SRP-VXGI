using UnityEditor;
using UnityEngine;

namespace VXGIEditor {
  [CustomEditor(typeof(VXGIMipmapDebug))]
  public class VXGIMipmapDebugEditor : Editor {
    public override void OnInspectorGUI() {
      DrawDefaultInspector();
    }
  }
}
