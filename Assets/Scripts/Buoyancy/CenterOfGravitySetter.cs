// Created by Ben Sims 02/06/20
#pragma warning disable 0108

using UnityEngine;

public class CenterOfGravitySetter : MonoBehaviour
{
	[SerializeField]
	private Vector3 centerOfMass = Vector3.zero;

	[SerializeField]
	private bool updateEveryFrame = false;

	private Rigidbody rigidbody;

	private void OnEnable()
	{
		rigidbody = GetComponent<Rigidbody>();
		if(rigidbody)
		{
			rigidbody.centerOfMass = centerOfMass;
		}
	}

	private void Update()
	{
		if(updateEveryFrame)
		{
			rigidbody.centerOfMass = centerOfMass;
		}
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawSphere(transform.TransformPoint(centerOfMass), 0.2f);
	}
}