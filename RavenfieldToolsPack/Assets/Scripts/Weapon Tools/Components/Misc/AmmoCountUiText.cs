using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public class AmmoCountUiText : MonoBehaviour {

	public bool spareAmmo = false;

	Text text;
	Weapon weapon;

	// Use this for initialization
	void Start () {
		this.text = GetComponent<Text>();
		this.weapon = GetComponentInParent<Weapon>();
	}
	
	// Update is called once per frame
	void Update () {
		if(this.spareAmmo) {
			this.text.text = this.weapon.GetSpareAmmo().ToString();
		}
		else {
			this.text.text = this.weapon.ammo.ToString();
		}
	}
}
