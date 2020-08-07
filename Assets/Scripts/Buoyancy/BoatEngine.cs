#pragma warning disable 0108

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Rigidbody))]
public class BoatEngine : MonoBehaviour
{
    [SerializeField]
    private AnimationCurve torqueCurve = AnimationCurve.Constant(0, 1, 1);

    [SerializeField]
    private float force = 1000f;

    [SerializeField]
    private float maxSteerAngle = 30.0f;

    [SerializeField]
    private float maxSteerSpeed = 15.0f;

    [SerializeField]
    private float steerSmoothTime = 0.1f;

    [SerializeField]
    private Vector3 enginePosition = Vector3.zero;

    [SerializeField, Range(-45, 45)]
    private float engineAngle = -5f;

    [SerializeField]
    private float idleRpm = 800;

    [SerializeField]
    private float maxRpm = 4400;

    [SerializeField]
    private float rpmSmoothing = 0.2f;

    [SerializeField]
    private float rpmMaxSpeed = 4000f;

    [SerializeField]
    private float audioRpm = 2200;

    [SerializeField]
    private float gearRatio = 8;

    [SerializeField]
    private Vector2 minMaxVolume = new Vector2(0.5f, 1f);

    [SerializeField]
    private Text debugText = null;

    private float currentRpm, rpmVelocity;
    private float currentJetPower;
    private Rigidbody rigidbody;
    private float currentSteerAngle, currentSteerVelocity;
    private Vector3 engineDirection;
    private AudioSource audio;

    private void OnEnable()
    {
        audio = GetComponent<AudioSource>();
        rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        currentJetPower = Input.GetAxis("Vertical") * force;

        var newSteerAngle = -Input.GetAxis("Horizontal") * maxSteerAngle;
        currentSteerAngle = Mathf.SmoothDamp(currentSteerAngle, newSteerAngle, ref currentSteerVelocity, steerSmoothTime, maxSteerSpeed);

        var newAngle = currentSteerAngle * Mathf.Deg2Rad;
        var newVector = new Vector3(Mathf.Sin(newAngle), 0f, Mathf.Cos(newAngle));
        var newRotation = Quaternion.LookRotation(newVector) * Quaternion.AngleAxis(engineAngle, Vector3.right);

        // Point the engine behind the transform
        engineDirection = newRotation * transform.forward;
    }

    private void FixedUpdate()
    {
        var forceToAdd = engineDirection;

        //Only add the force if the engine is below sea level
        var position = transform.TransformPoint(enginePosition);
        float waveYPos = Ocean.Instance.GetOceanHeight(position);

        var speed = Vector3.Dot(rigidbody.velocity, transform.forward);
        if (debugText)
        {
            debugText.text = $"Speed: {speed * 3.6f:0} km/h";
        }

        if (position.y < waveYPos)
        {
            currentRpm = Mathf.SmoothDamp(currentRpm, speed * gearRatio, ref rpmVelocity, rpmSmoothing, rpmMaxSpeed);

            var totalPower = torqueCurve.Evaluate(Mathf.InverseLerp(idleRpm, maxRpm, currentRpm)) * currentJetPower;

            rigidbody.AddForceAtPosition(forceToAdd * totalPower, position);
        }
        else
        {
            var targetRpm = Mathf.Lerp(idleRpm, maxRpm, Mathf.Clamp01(Input.GetAxis("Vertical")));
            currentRpm = Mathf.SmoothDamp(currentRpm, targetRpm, ref rpmVelocity, rpmSmoothing, rpmMaxSpeed);
        }

        currentRpm = Mathf.Clamp(currentRpm, idleRpm, maxRpm);

        if(audio)
        {
            audio.pitch = currentRpm / audioRpm;
            audio.volume = Mathf.Lerp(minMaxVolume.x, minMaxVolume.y, Mathf.Clamp01(Input.GetAxis("Vertical")));
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(enginePosition), 0.1f);
    }
}