using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Collections.Generic;

public class Weapon : MonoBehaviour {

	const float AIM_AMOUNT_SPEED = 6f;

	public enum Effectiveness {No, Yes, Preferred};

	public Transform thirdPersonTransform;
	public Vector3 thirdPersonOffset = Vector3.zero;
	public Vector3 thirdPersonRotation = new Vector3(0f, 0f, -90f);
	public GameObject[] cullInThirdPerson;

	public float thirdPersonScale = 1f;
	public Configuration configuration;
	public AudioSource reverbAudio;
	public AudioSource reloadAudio;

	public Sprite uiSprite;
	public SkinnedMeshRenderer arms;
	public bool allowArmMeshReplacement = true;

	new protected AudioSource audio;

	protected Animator animator;
	protected List<Renderer> renderers;
	protected List<Renderer> nonScopeRenderers;
	int currentMuzzle = 0;
	Dictionary<Transform, ParticleSystem> muzzleFlash;

	float weaponVolume;
	float aimAmount = 0f;
	bool showScopeObject = false;
	Action stopFireLoop = new Action(0.12f);
	bool fireLoopPlaying = false;

	Action followupSpreadStayAction;
	float followupSpreadMagnitude = 0f;
	float followupSpreadDissipateRate = 1f;

	Action heatStayAction;
	float heat = 0f;
	bool isOverheating = false;
	protected bool holdingFire = false;

	bool reportAmmoToAnimator = false;
	bool reportRandomToAnimator = false;

	bool aiming = false;
	[System.NonSerialized] public bool reloading = false;
	[System.NonSerialized] public bool unholstered = true;
	[System.NonSerialized] public int ammo;

	protected float lastFiredTimestamp = 0f;

	int currentReloadMotion;

	protected virtual void Awake() {
		this.animator = this.GetComponent<Animator>();
		this.audio = GetComponent<AudioSource>();
		this.ammo = this.configuration.ammo;

		if(this.reverbAudio != null) {
			this.reverbAudio.volume *= 0.2f;
		}

		this.muzzleFlash = new Dictionary<Transform, ParticleSystem>(this.configuration.muzzles.Length);
		foreach(Transform muzzle in this.configuration.muzzles) {
			ParticleSystem muzzleFlashParticles = muzzle.GetComponent<ParticleSystem>();

			if(muzzleFlashParticles == null) {
				muzzleFlashParticles = muzzle.GetComponentInChildren<ParticleSystem>();
			}

			if(muzzleFlashParticles != null) {
				this.muzzleFlash.Add(muzzle, muzzleFlashParticles);
				muzzleFlashParticles.Stop();
			}
		}

		FindRenderers();

		this.audio.loop = this.configuration.auto;
		this.weaponVolume = this.audio.volume;

		if(this.animator != null) {
			this.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

			foreach(AnimatorControllerParameter parameter in this.animator.parameters) {
				if(parameter.nameHash == Animator.StringToHash("loaded ammo") && parameter.type == AnimatorControllerParameterType.Int) {
					this.reportAmmoToAnimator = true;
				}
				else if(parameter.nameHash == Animator.StringToHash("random") && parameter.type == AnimatorControllerParameterType.Float) {
					this.reportRandomToAnimator = true;
				}
			}

			if(this.reportAmmoToAnimator) {
				this.animator.SetInteger("loaded ammo", this.ammo);
			}
		}

		this.followupSpreadStayAction = new Action(this.configuration.followupSpreadStayTime);
		this.followupSpreadDissipateRate = 1f/this.configuration.followupSpreadDissipateTime;
		this.heatStayAction = new Action(this.configuration.cooldown*1.1f);
	}

	protected virtual void Start() {
		Unholster();
	}

	public virtual void FindRenderers() {
		this.renderers = new List<Renderer>(this.GetComponentsInChildren<Renderer>());

		foreach(Renderer renderer in this.renderers) {
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.receiveShadows = false;
		}

		if(this.arms != null) {
			this.arms.updateWhenOffscreen = true;
		}

		if(HasScopeObject()) {
			this.nonScopeRenderers = new List<Renderer>(this.renderers);

			foreach(Renderer renderer in this.configuration.scopeAimObject.GetComponentsInChildren<Renderer>()) {
				this.nonScopeRenderers.Remove(renderer);
			}
		}
	}

	protected bool IsMuzzleTransform(Transform t) {
		for(int i = 0; i < this.configuration.muzzles.Length; i++) {
			if(this.configuration.muzzles[i] == t) return true;
		}
		return false;
	}

	public virtual void CullFpsObjects() {

		if(this.animator != null) {
			Destroy(this.animator);
			this.animator = null;
		}

		if(this.cullInThirdPerson != null) {
			foreach(GameObject go in this.cullInThirdPerson) {
				GameObject.Destroy(go);
			}
		}

		bool useAlternativeMuzzles = this.configuration.optionalThirdPersonMuzzles != null && this.configuration.optionalThirdPersonMuzzles.Length > 0;

		if(useAlternativeMuzzles) {
			this.configuration.muzzles = this.configuration.optionalThirdPersonMuzzles;
		}


		this.thirdPersonTransform.transform.parent = this.transform;

		for(int i = 0; i < this.transform.childCount; i++) {
			Transform child = this.transform.GetChild(i);
			if(child != this.thirdPersonTransform) {
				if(IsMuzzleTransform(child)) {
					child.transform.localPosition = this.thirdPersonTransform.localPosition;
					this.thirdPersonTransform.localRotation = Quaternion.identity;
				}
				else {
					GameObject.Destroy(child.gameObject);
				}
			}
		}
	}

	protected virtual void Update() {
		if(!this.stopFireLoop.Done()) {
			float ratio = (1f-this.stopFireLoop.Ratio());
			this.audio.volume = ratio*this.weaponVolume;

			if(this.stopFireLoop.TrueDone()) {
				this.audio.Stop();
			}
		}

		this.animator.SetBool("tuck", Input.GetKey(KeyCode.LeftShift));

		if(HasScopeObject()) {
			this.aimAmount = Mathf.MoveTowards(this.aimAmount, aiming ? 1f : 0f, Time.deltaTime*AIM_AMOUNT_SPEED);

			bool wasShowingScopeObject = this.showScopeObject;
			this.showScopeObject = this.aimAmount >= 0.95f;

			this.configuration.scopeAimObject.SetActive(this.showScopeObject);

			if(!wasShowingScopeObject && this.showScopeObject) {
				foreach(Renderer renderer in this.nonScopeRenderers) {
					renderer.enabled = false;
				}
			}
			else if(wasShowingScopeObject && !this.showScopeObject) {
				foreach(Renderer renderer in this.nonScopeRenderers) {
					renderer.enabled = true;
				}
			}
		}

		if(this.followupSpreadStayAction.TrueDone()) {
			this.followupSpreadMagnitude = Mathf.MoveTowards(this.followupSpreadMagnitude, 0f, this.followupSpreadDissipateRate*Time.deltaTime);
		}

		if(this.configuration.applyHeat) {
			UpdateHeat();
		}

		if(HasActiveAnimator() && this.reportRandomToAnimator) {
			this.animator.SetFloat("random", Random.Range(0f, 100f));
		}
	}

	void UpdateHeat() {
		if(this.heatStayAction.TrueDone()) {
			this.heat = Mathf.Clamp01(this.heat - this.configuration.heatDrainRate*Time.deltaTime);
		}

		if(this.isOverheating) {
			this.isOverheating = this.heat > 0f;
		}

		this.animator.SetBool("overheat", this.isOverheating);

		if(this.configuration.heatMaterial.HasTarget()) {
			this.configuration.heatMaterial.Get().SetColor("_EmissionColor", Color.Lerp(Color.black, this.configuration.heatColor, this.configuration.heatColorGain.Evaluate(this.heat)));
		}
	}

	public virtual void Fire(Vector3 direction, bool useMuzzleDirection) {
		if(CanFire()) {
			if(this.configuration.auto && (!this.audio.isPlaying || !this.stopFireLoop.Done())) {
				StartFireLoop();
			}
			Shoot(direction, useMuzzleDirection);

			if(this.configuration.applyHeat) {
				this.heat = Mathf.Clamp01(this.heat+this.configuration.heatGainPerShot);
				this.heatStayAction.Start();

				this.isOverheating = this.heat == 1f;

				if(this.isOverheating) {
					StopFire();
					if(this.configuration.overheatParticles != null) {
						this.configuration.overheatParticles.Play();
					}
					if(this.configuration.overheatSound != null) {
						this.configuration.overheatSound.Play();
					}

				}
			}

			if(this.ammo == 0) {
				StopFire();
			}

		}

		this.holdingFire = true;
	}

	void StartFireLoop() {
		this.audio.volume = this.weaponVolume;
		this.audio.Play();
		this.stopFireLoop.Stop();
		this.fireLoopPlaying = true;
	}

	void StopFireLoop() {
		if(this.fireLoopPlaying) {
			this.stopFireLoop.Start();
			this.fireLoopPlaying = false;
		}
	}

	public void StopFire() {
		if(this.configuration.auto) {
			StopFireLoop();
		}
		this.holdingFire = false;
	}

	public virtual void SetAiming(bool aiming) {
		this.aiming = aiming;
		if(HasActiveAnimator()) {
			this.animator.SetBool("aim", aiming);
		}
	}

	public virtual void Reload(bool overrideHolstered = false) {

		if(this.reloading || this.configuration.spareAmmo < 0) return;

		if(this.fireLoopPlaying) {
			StopFireLoop();
		}

		if(HasActiveAnimator()) {
			this.animator.SetTrigger("reload");
		}

		DisableOverrideLayer();

		if(this.reloadAudio != null) {
			this.reloadAudio.Play();
		}

		this.reloading = true;

		if(this.configuration.advancedReload) {
			StartAdvancedReload();
		}
		else {
			Invoke("ReloadDone", this.configuration.reloadTime);
		}
	}

	public int GetSpareAmmo() {
		return this.configuration.spareAmmo;
	}

	void StartAdvancedReload() {
		this.animator.SetBool("reloading", true);
		AdvancedReloadNextMotion();
	}

	void AdvancedReloadNextMotion() {
		int remainingAmmo = this.configuration.ammo-this.ammo;

		if(remainingAmmo == 0) {
			EndAdvancedReload();
			return;
		}

		this.currentReloadMotion = 0;

		foreach(int availableMotion in this.configuration.allowedReloads) {
			if(availableMotion <= remainingAmmo && availableMotion >= this.currentReloadMotion) {
				this.currentReloadMotion = availableMotion;
			}
		}

		if(this.currentReloadMotion == 0) {
			EndAdvancedReload();
			return;
		}

		this.animator.SetInteger("reload motion", this.currentReloadMotion);
	}

	void EndAdvancedReload() {
		this.animator.SetBool("reloading", false);
	}

	public void MotionDone() {
		int loadedAmmo = this.currentReloadMotion;
		this.ammo = Mathf.Min(this.ammo + loadedAmmo, this.configuration.ammo);

		if(this.reportAmmoToAnimator) {
			this.animator.SetInteger("loaded ammo", this.ammo);
		}

		AdvancedReloadNextMotion();
	}

	protected void ReloadDone() {
		EnableOverrideLayer();


		this.ammo = this.configuration.ammo;

		if(this.reportAmmoToAnimator) {
			this.animator.SetInteger("loaded ammo", this.ammo);
		}

		this.reloading = false;
	}

	void DisableOverrideLayer() {
		if(HasActiveAnimator() && this.animator.layerCount > 1) {
			this.animator.SetLayerWeight(1, 0f);
		}
	}

	void EnableOverrideLayer() {
		if(HasActiveAnimator() && this.animator.layerCount > 1) {
			this.animator.SetLayerWeight(1, 1f);
		}
	}

	public virtual bool CanFire() {
		return this.unholstered && this.ammo != 0 && !this.reloading && (this.configuration.auto || !this.holdingFire) && !CoolingDown() && (!this.isOverheating || !this.configuration.applyHeat);
	}

	public bool CoolingDown() {
		return Time.time-this.lastFiredTimestamp < this.configuration.cooldown;
	}

	protected virtual void Shoot(Vector3 direction, bool useMuzzleDirection) {

		this.followupSpreadMagnitude = Mathf.Clamp(this.followupSpreadMagnitude, 0f, this.aiming ? this.configuration.followupMaxSpreadAim : this.configuration.followupMaxSpreadHip);

		this.lastFiredTimestamp = Time.time;

		if(HasActiveAnimator()) {
			if(!this.configuration.fireFromAllMuzzles && this.configuration.muzzles.Length > 1) {
				this.animator.SetInteger("muzzle", this.currentMuzzle);
			}

			this.animator.SetTrigger("fire");
		}

		if(this.configuration.fireFromAllMuzzles) {
			for(int i = 0; i < this.configuration.muzzles.Length; i++) {
				FireFromMuzzle(i, direction, useMuzzleDirection);
			}
		}
		else {
			FireFromMuzzle(this.currentMuzzle, direction, useMuzzleDirection);
		}

		WeaponUser.instance.ApplyRecoil(this.configuration.kickback*Vector3.back+Random.insideUnitSphere*this.configuration.randomKick);
		WeaponUser.instance.ApplyWeaponSnap(this.configuration.snapMagnitude, this.configuration.snapDuration, this.configuration.snapFrequency);

		if(!this.configuration.auto) {
			this.audio.Play();
		}
		else if(this.ammo == 0) {
			StopFireLoop();
		}

		if(this.reverbAudio != null) {
			PlayReverbAudio(Camera.main.transform.position + Camera.main.transform.forward*50f);
		}

		this.followupSpreadMagnitude = Mathf.Clamp(this.followupSpreadMagnitude+this.configuration.followupSpreadGain, 0f, this.aiming ? this.configuration.followupMaxSpreadAim : this.configuration.followupMaxSpreadHip);
		this.followupSpreadStayAction.StartLifetime(this.configuration.followupSpreadStayTime);
		this.currentMuzzle = (this.currentMuzzle+1)%this.configuration.muzzles.Length;

		if(this.ammo > 0) {
			this.ammo = Mathf.Max(this.ammo-1, 0);
		}

		if(this.reportAmmoToAnimator) {
			this.animator.SetInteger("loaded ammo", this.ammo);
		}
	}

	void FireFromMuzzle(int muzzleIndex, Vector3 direction, bool useMuzzleDirection) {

		Transform muzzle = this.configuration.muzzles[muzzleIndex];

		if(useMuzzleDirection) {
			direction = muzzle.forward;
		}

		for(int i = 0; i < this.configuration.projectilesPerShot; i++) {
			SpawnProjectile(direction, muzzle.position);
		}

		if(this.muzzleFlash.ContainsKey(muzzle)) {
			this.muzzleFlash[muzzle].Play(true);
		}

		if(this.configuration.casingParticles.Length > 0) {
			this.configuration.casingParticles[muzzleIndex%this.configuration.casingParticles.Length].Play(false);
		}
	}

	public Transform CurrentMuzzle() {
		return this.configuration.muzzles[this.currentMuzzle];
	}

	void PlayReverbAudio(Vector3 position) {
		this.reverbAudio.Stop();
		this.reverbAudio.transform.position = position;
		this.reverbAudio.Play();
	}

	protected bool HasActiveAnimator() {
		return this.animator != null && this.animator.isActiveAndEnabled;
	}

	protected virtual Projectile SpawnProjectile(Vector3 direction, Vector3 position) {
		float spread = this.configuration.spread+this.followupSpreadMagnitude;
		Quaternion rotation = Quaternion.LookRotation(direction+Random.insideUnitSphere*spread);
		Projectile projectile = ((GameObject) GameObject.Instantiate(this.configuration.projectilePrefab, position, rotation)).GetComponent<Projectile>();

		return projectile;
	}

	public virtual void Unholster() {
		this.unholstered = false;
		this.aiming = false;
		if(HasActiveAnimator()) {
			this.animator.SetTrigger("unholster");
		}

		DisableOverrideLayer();
		Invoke("UnholsterDone", this.configuration.unholsterTime);
	}

	public void UnholsterDone() {
		EnableOverrideLayer();
		this.unholstered = true;
	}

	public virtual bool CanBeAimed() {
		return (this.configuration.canBeAimedWhileReloading || !this.reloading) && this.unholstered;
	}

	bool HasScopeObject() {
		return this.configuration.scopeAimObject != null;
	}

	public virtual bool ShouldHaveProjectilePrefab() {
		return true;
	}

	[System.Serializable]
	public class Configuration {
		public bool auto = false;
		public int ammo = 10;
		public int spareAmmo = 50;
		public int resupplyNumber = 10;
		public float reloadTime = 2f;
		public float cooldown = 0.2f;
		public float unholsterTime = 1.2f;
		public float aimFov = 50f;
		public float autoReloadDelay = 0f;

		public bool canBeAimedWhileReloading = false;
		public bool forceAutoReload = false;
		public bool loud = true;
		public bool forceWorldAudioOutput = false;

		public Transform[] muzzles;
		public Transform[] optionalThirdPersonMuzzles;
		public ParticleSystem[] casingParticles;

		public bool fireFromAllMuzzles = false;
		public int projectilesPerShot = 1;
		public GameObject projectilePrefab;

		public GameObject scopeAimObject;

		public float kickback = 0.5f;
		public float randomKick = 0.05f;
		public float spread = 0.001f;

		public float followupSpreadGain = 0.005f;
		public float followupMaxSpreadHip = 0.05f;
		public float followupMaxSpreadAim = 0.02f;
		public float followupSpreadStayTime = 0.2f;
		public float followupSpreadDissipateTime = 1f;

		public float snapMagnitude = 0.3f;
		public float snapDuration = 0.4f;
		public float snapFrequency = 4f;

		public float aiAllowedAimSpread = 1f;
		public Effectiveness effInfantry = Effectiveness.Yes;
		public Effectiveness effInfantryGroup = Effectiveness.No;
		public Effectiveness effUnarmored = Effectiveness.Yes;
		public Effectiveness effArmored = Effectiveness.No;
		public Effectiveness effAir = Effectiveness.No;
		public Effectiveness effAirFastMover = Effectiveness.No;
		public float effectiveRange = 100f;

		public int pose = 0;

		public bool applyHeat = false;
		public MaterialTarget heatMaterial;
		public float heatGainPerShot = 0f;
		public float heatDrainRate = 0.25f;
		public Color heatColor;
		public AnimationCurve heatColorGain;
		public ParticleSystem overheatParticles;
		public AudioSource overheatSound;

		public bool advancedReload = false;
		public int[] allowedReloads;
	}
}
