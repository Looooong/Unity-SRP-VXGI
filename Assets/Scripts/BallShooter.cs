using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BallShooter : MonoBehaviour {
  public GameObject ball;
  public float force = 10f;
  public float intensity = 2f;
  public Vector3 offset = Vector3.forward;

  List<GameObject> _balls;

  void Start() {
    _balls = new List<GameObject>();
  }

  void Update() {
    if (Input.GetMouseButtonDown(0)) {
      if (ball != null) {
        var newBall = GameObject.Instantiate(ball, transform.position + transform.TransformVector(offset), transform.rotation);
        _balls.Add(newBall);

        var rg = newBall.GetComponent<Rigidbody>();

        if (rg != null) {
          rg.AddForce(force * transform.forward, ForceMode.Impulse);
        }

        var renderer = newBall.GetComponent<Renderer>();

        if (renderer != null) {
          renderer.material.SetColor("_EmissionColor", intensity * Random.ColorHSV(0f, 1f, 0f, 1f, 1f, 1f));
        }
      }
    }

    if (Input.GetMouseButtonDown(1)) {
      foreach (var b in _balls) {
        Destroy(b);
      }

      _balls.Clear();
    }
  }
}
