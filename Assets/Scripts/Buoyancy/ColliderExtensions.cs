#pragma warning disable 0108

using System;
using UnityEngine;

public static class ColliderExtensions
{
    public static Bounds LocalBounds(this Collider collider)
    {
        switch (collider)
        {
            case BoxCollider boxCollider:
                return new Bounds(boxCollider.center, boxCollider.size);
            case SphereCollider sphereCollider:
                return new Bounds(sphereCollider.center, new Vector3(sphereCollider.radius * 2, sphereCollider.radius * 2, sphereCollider.radius * 2));
            case CharacterController characterController:
                return new Bounds(characterController.center, new Vector3(characterController.radius * 2, characterController.height, characterController.radius * 2));
            case MeshCollider meshCollider:
                return meshCollider.sharedMesh.bounds;
            case CapsuleCollider capsuleCollider:
                switch (capsuleCollider.direction)
                {
                    case 0: return new Bounds(capsuleCollider.center, new Vector3(capsuleCollider.height, capsuleCollider.radius * 2, capsuleCollider.radius * 2));
					case 1: return new Bounds(capsuleCollider.center, new Vector3(capsuleCollider.radius * 2, capsuleCollider.height, capsuleCollider.radius * 2));
					case 2: return new Bounds(capsuleCollider.center, new Vector3(capsuleCollider.radius * 2, capsuleCollider.radius * 2, capsuleCollider.height));
				};
                break;
        }

        throw new NotImplementedException();
    }
}