using UnityEditor;
using UnityEngine;

namespace VXGIEditor {
  [CustomEditor(typeof(VXGI))]
  public class VXGIEditor : Editor {
    MonoScript _script;
    SerializedProperty _antiAliasing;
    SerializedProperty _bound;
    SerializedProperty _center;
    SerializedProperty _diffuseResolutionScale;
    SerializedProperty _followCamera;
    SerializedProperty _indirectDiffuseModifier;
    SerializedProperty _indirectSpecularModifier;
    SerializedProperty _limitRefreshRate;
    SerializedProperty _mipmapFilterMode;
    SerializedProperty _refreshRate;
    SerializedProperty _resolution;

    public override void OnInspectorGUI() {
      serializedObject.Update();

      using (new EditorGUI.DisabledScope(true)) {
        EditorGUILayout.ObjectField("Script", _script, typeof(MonoScript), false);
      }

      EditorGUILayout.PropertyField(_followCamera);

      using (new EditorGUI.DisabledScope(_followCamera.boolValue)) {
        EditorGUILayout.PropertyField(_center);
      }

      EditorGUILayout.PropertyField(_bound);
      EditorGUILayout.PropertyField(_resolution);
      EditorGUILayout.PropertyField(_antiAliasing);
      EditorGUILayout.PropertyField(_mipmapFilterMode);
      EditorGUILayout.PropertyField(_limitRefreshRate);

      if (_limitRefreshRate.boolValue) EditorGUILayout.PropertyField(_refreshRate);

      EditorGUILayout.PropertyField(_indirectDiffuseModifier);
      EditorGUILayout.PropertyField(_indirectSpecularModifier);
      EditorGUILayout.PropertyField(_diffuseResolutionScale);

      serializedObject.ApplyModifiedProperties();
    }

    void OnEnable() {
      _script = MonoScript.FromMonoBehaviour((VXGI)target);
      _antiAliasing = serializedObject.FindProperty("antiAliasing");
      _bound = serializedObject.FindProperty("bound");
      _center = serializedObject.FindProperty("center");
      _diffuseResolutionScale = serializedObject.FindProperty("diffuseResolutionScale");
      _followCamera = serializedObject.FindProperty("followCamera");
      _indirectDiffuseModifier = serializedObject.FindProperty("indirectDiffuseModifier");
      _indirectSpecularModifier = serializedObject.FindProperty("indirectSpecularModifier");
      _limitRefreshRate = serializedObject.FindProperty("limitRefreshRate");
      _mipmapFilterMode = serializedObject.FindProperty("mipmapFilterMode");
      _refreshRate = serializedObject.FindProperty("refreshRate");
      _resolution = serializedObject.FindProperty("resolution");
    }
  }
}
