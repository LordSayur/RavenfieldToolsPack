using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ExplodingProjectile : Projectile {

	public ExplosionConfiguration explosionConfiguration;
	public float smokeTime = 8f;

	public Renderer[] renderers;
	public ParticleSystem trailParticles;
	public ParticleSystem impactParticles;

	protected override void Hit (Vector3 point, Vector3 normal)
	{

		this.transform.position = point;
		this.transform.rotation = Quaternion.LookRotation(normal);

		foreach(Renderer renderer in this.renderers) {
			renderer.enabled = false;
		}

		if(this.trailParticles != null) {
			this.trailParticles.Stop();
		}


		this.impactParticles.Play();

		Invoke("Cleanup", this.smokeTime);

		this.travelling = false;
		this.enabled = false;

		WeaponUser.RegisterHit(point);
	}

	void Cleanup() {
		Destroy(this.gameObject);
	}

	[System.Serializable]
	public class ExplosionConfiguration {
		public float damage = 300f;
		public float balanceDamage = 300f;
		public float force = 500f;
		public float damageRange = 6f;
		public AnimationCurve damageFalloff;
		public float balanceRange = 9f;
		public AnimationCurve balanceFalloff;
	}
}
