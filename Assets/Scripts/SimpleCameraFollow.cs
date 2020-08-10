// Created by Ben Sims 01/06/20

using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Simple script for making one Transform(Eg a Camera) follow another Transform. (Eg a player)
/// </summary>
public class SimpleCameraFollow : MonoBehaviour
{
    [SerializeField, Tooltip("The Transform to follow")]
    private Transform target = null;

    [SerializeField, Tooltip("How quickly the camera matches the target's position")]
    private float positionDamping = 1;

    [SerializeField, Tooltip("How quickly the camera matches the target's rotation")]
    private float rotationSpeed = 1;

    [SerializeField, Tooltip("Where the Camera moves to, relative to the Target")]
    private Vector3 positionOffset = new Vector3(0, 2, -3);

    [SerializeField, Tooltip("Where the Camera attempts to look, relative to the Target")]
    private Vector3 lookOffset = new Vector3(0, 0, 5);

    private Vector3 moveVelocity;

    public Transform Target { get { return target; } set { target = value; } }

    private void OnEnable()
    {
        // Ensure we have a target assigned
        if(!target)
        {
            enabled = false;
            return;
        }

        // Set the position + rotation to the target's position so we don't zoom across the map on start
        var targetPosition = target.position + target.rotation * positionOffset;
        var lookPosition = target.position + target.rotation * lookOffset;
        var targetRotation = Quaternion.LookRotation(Vector3.Normalize(lookPosition - transform.position));

        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void FixedUpdate()
    {
        var targetPosition = target.position + target.rotation * positionOffset;
        var position = Vector3.SmoothDamp(transform.position, targetPosition, ref moveVelocity, positionDamping);

        var lookPosition = target.position + target.rotation * lookOffset;
        var targetRotation = Quaternion.LookRotation(Vector3.Normalize(lookPosition - transform.position));
        var rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        transform.SetPositionAndRotation(position, rotation);
    }
}