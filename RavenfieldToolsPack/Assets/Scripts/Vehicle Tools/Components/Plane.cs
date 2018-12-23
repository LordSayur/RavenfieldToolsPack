using UnityEngine;
using System.Collections;

public class Plane : Vehicle {

	public AnimationCurve liftVsAngleOfAttack;
	public AnimationCurve controlVsAngleOfAttack;

	public Transform thruster;

	public float baseLift = 0.5f;
	public float acceleration = 10f;
	public float accelerationThrottleUp = 13f;
	public float accelerationThrottleDown = 7f;
	public float throttleEngineAudioPitchControl = 0.1f;
	public float autoPitchTorqueGain = -0.001f;
	public float perpendicularDrag = 1f;
	public float pitchSensitivity = 1f;
	public float yawSensitivity = 0.5f;
	public float rollSensitivity = 1f;
	public float liftGainTime = 7f;
	public float controlWhenBurning = 0.1f;
}
