using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Atom))]
[CanEditMultipleObjects]
public class AtomEditor : Editor {
  public override void OnInspectorGUI() {
    DrawDefaultInspector();

    Atom atom = (Atom)target;

    if (GUILayout.Button("Save")) {
      atom.Save();
    }
  }
}
