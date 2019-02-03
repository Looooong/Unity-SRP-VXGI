using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Electron : MonoBehaviour {
  public float force = 1f;

  void FixedUpdate() {
    foreach (var electron in FindObjectsOfType<Electron>()) {
      if (GameObject.ReferenceEquals(gameObject, electron.gameObject)) continue;
      var d = electron.transform.position - transform.position;
      var rg = electron.GetComponent<Rigidbody>();
      rg.AddForce(force * d.normalized / d.sqrMagnitude);
    }
  }
}
