using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour {
  public Vector3 speed = Vector3.up;
	
	void Update () {
    transform.Rotate(180 * speed * Time.deltaTime, Space.World);
	}
}
