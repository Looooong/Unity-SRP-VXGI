using System.IO;
using UnityEngine;

public class Atom : MonoBehaviour {
  [Range(.1f, 20f)]
  public float radius = 1f;
  public Transform radiusVisuallization;
  public GameObject electron;
  [Range(1, 32)]
  public int electronCount = 8;

  private int _currentElectronCount = 0;

  public void Save() {
    var electrons = GetComponentsInChildren<Electron>();
    var lines = new string[electrons.Length];

    for (var i = 0; i < electrons.Length; i++) {

      var direction = electrons[i].transform.position.normalized;
      lines[i] = direction.x + ", " + direction.y + ", " + direction.z;
    }

    File.WriteAllLines(Application.dataPath + "/directions.txt", lines);

    print("Data written!");
  }

  void Update() {
    UpdateCount();
  }

  void FixedUpdate() {
    var electrons = GetComponentsInChildren<Electron>();
    float sum = 0f;

    foreach (var e in electrons) {
      var rg = e.GetComponent<Rigidbody>();
      var d = rg.position - transform.position;
      rg.position = transform.position + d.normalized * radius;
      sum += d.magnitude;
    }

    if (electrons.Length > 0) {
      radiusVisuallization.localScale = Vector3.one * 2 * sum / electrons.Length;
    }
  }

  void UpdateCount() {
    int diff = electronCount - _currentElectronCount;

    if (diff > 0) {
      for (var i = 0; i < diff; i++) {
        Instantiate(electron, transform.position + Random.insideUnitSphere * radius, Quaternion.identity, transform);
      }
    } else if (diff < 0) {
      var electrons = GetComponentsInChildren<Electron>();

      for (var i = diff; i < 0; i++) {
        Destroy(electrons[-i - 1].gameObject);
      }
    }

    _currentElectronCount = electronCount;
  }
}
