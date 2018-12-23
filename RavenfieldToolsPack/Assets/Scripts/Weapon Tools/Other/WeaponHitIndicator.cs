using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponHitIndicator : MonoBehaviour {

	const float LIFETIME = 10f;

	// Use this for initialization
	void Start () {
		Invoke("Cleanup", LIFETIME);
	}

	void Cleanup() {
		Destroy(this.gameObject);
	}
}
