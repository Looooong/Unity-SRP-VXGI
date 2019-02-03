using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiscoBall : MonoBehaviour {
  [Range(-10f, 10f)]
  public float rotationSpeed = 1f;
  public Vector3 frequency = Vector3.one;
  public Vector3 offset = Vector3.zero;

  Color _color;
  Material _material;

  void Start() {
    _material = GetComponent<Renderer>().material;
    _color = _material.GetColor("_EmissionColor");
  }

	// Update is called once per frame
	void Update () {
		transform.Rotate(Vector3.up, rotationSpeed * 180f * Time.deltaTime);
    
    var color = new Color(
      _color.r * (.5f + .5f * Mathf.Sin(Time.unscaledTime * frequency.x + offset.x)),
      _color.g * (.5f + .5f * Mathf.Sin(Time.unscaledTime * frequency.y + offset.y)),
      _color.b * (.5f + .5f * Mathf.Sin(Time.unscaledTime * frequency.z + offset.z))
    );

    _material.SetColor("_EmissionColor", color);
  }
}
