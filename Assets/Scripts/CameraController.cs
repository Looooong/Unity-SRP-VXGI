using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(VXGI))]
public class CameraController : MonoBehaviour {
  static readonly VXGIRenderer.ConeType[] cones = {
    VXGIRenderer.ConeType.All,
    VXGIRenderer.ConeType.Diffuse,
    VXGIRenderer.ConeType.Reflectance,
    VXGIRenderer.ConeType.Transmittance
  };

  static readonly VXGIRenderer.GBufferType[] gBuffers = {
    VXGIRenderer.GBufferType.Diffuse,
    VXGIRenderer.GBufferType.Depth,
    VXGIRenderer.GBufferType.Normal,
    VXGIRenderer.GBufferType.Emission,
    VXGIRenderer.GBufferType.Glossiness,
    VXGIRenderer.GBufferType.Metallic
  };

  static readonly VXGI.Resolution[] resolutions = {
    VXGI.Resolution.Low,
    VXGI.Resolution.Medium,
    VXGI.Resolution.High,
    VXGI.Resolution.VeryHigh
  };

  static readonly VXGIRenderer.MipmapSampler[] samplers = {
    VXGIRenderer.MipmapSampler.Linear,
    VXGIRenderer.MipmapSampler.Point
  };

  static readonly VXGIRenderer.Pass[] passes = {
    VXGIRenderer.Pass.ConeTracing,
    VXGIRenderer.Pass.DiffuseConeTracing,
    // VXGIRenderer.Pass.GBuffer,
    VXGIRenderer.Pass.Mipmap
  };

  public Canvas ui;
  public UnityStandardAssets.Characters.FirstPerson.RigidbodyFirstPersonController fpsController;

  VXGI _vxgi;

  void Start() {
    _vxgi = GetComponent<VXGI>();
  }

  void Update() {
    for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++) {
      if (Input.GetKeyDown((i + 1).ToString())) {
        SceneManager.LoadScene(i);
      }
    }

    if ((ui != null) && Input.GetKeyDown(KeyCode.F)) {
      ui.enabled = !ui.enabled;
    }

    if (Input.GetKeyDown(KeyCode.R)) {
      SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    if (Input.GetKeyDown(KeyCode.C)) {
      CyclePass();
    }

    if (Input.GetKeyDown(KeyCode.Escape)) {
#if UNITY_EDITOR
      // UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }

    if ((fpsController != null) && Input.GetKeyDown(KeyCode.Q)) {
      fpsController.enabled = !fpsController.enabled;

      Cursor.lockState = fpsController.enabled ? CursorLockMode.Locked : CursorLockMode.None;
      Cursor.visible = !fpsController.enabled;
    }

    // if (Input.GetKeyDown(KeyCode.V)) {
    //   switch(_vxgi.pass) {
    //     case VXGIRenderer.Pass.ConeTracing:
    //       CycleCone();
    //       break;

    //     case VXGIRenderer.Pass.GBuffer:
    //       CycleGBuffer();
    //       break;

    //     case VXGIRenderer.Pass.Mipmap:
    //       CycleSampler();
    //       break;
    //   }
    // }
  }

  void CycleCone() {
    int index = -1;

    for (int i = 0; i < cones.Length; i++) {
      if (_vxgi.coneType == cones[i]) {
        index = i;
        break;
      }
    }

    if (++index >= cones.Length) index = 0;

    _vxgi.coneType = cones[index];
  }

  void CycleGBuffer() {
    int index = -1;

    for (int i = 0; i < gBuffers.Length; i++) {
      if (_vxgi.gBufferType == gBuffers[i]) {
        index = i;
        break;
      }
    }

    if (++index >= gBuffers.Length) index = 0;

    _vxgi.gBufferType = gBuffers[index];
  }

  void CycleResolution() {
    int index = -1;

    for (int i = 0; i < resolutions.Length; i++) {
      if (_vxgi.resolution == resolutions[i]) {
        index = i;
        break;
      }
    }

    if (++index >= resolutions.Length) index = 0;

    _vxgi.resolution = resolutions[index];
  }

  void CycleSampler() {
    int index = -1;

    for (int i = 0; i < samplers.Length; i++) {
      if (_vxgi.mipmapSampler == samplers[i]) {
        index = i;
        break;
      }
    }

    if (++index >= samplers.Length) index = 0;

    _vxgi.mipmapSampler = samplers[index];
  }

  void CyclePass() {
    int index = -1;

    for (int i = 0; i < passes.Length; i++) {
      if (_vxgi.pass == passes[i]) {
        index = i;
        break;
      }
    }

    if (++index >= passes.Length) index = 0;

    _vxgi.pass = passes[index];
  }
}
