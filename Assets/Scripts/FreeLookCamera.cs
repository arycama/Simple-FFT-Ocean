using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeLookCamera : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 5;

    [SerializeField]
    private float rotateSpeed = 5;

    [SerializeField]
    private float boostFactor = 2f;

    [SerializeField]
    private bool requireRightClick = false;

    private Vector3 rotation;

    private void OnEnable()
    {
        rotation = transform.eulerAngles;
    }

    private void Update()
    {
        if(requireRightClick && !Input.GetMouseButton(1))
        {
            return;
        }

        var sway = Input.GetKey(KeyCode.D) ? 1 : 0;
        sway -= Input.GetKey(KeyCode.A) ? 1 : 0;

        var heave = Input.GetKey(KeyCode.E) ? 1 : 0;
        heave -= Input.GetKey(KeyCode.Q) ? 1 : 0;

        var surge = Input.GetKey(KeyCode.W) ? 1 : 0;
        surge -= Input.GetKey(KeyCode.S) ? 1 : 0;

        var boost = Input.GetKey(KeyCode.LeftShift) ? boostFactor : 1;
        
        transform.Translate(new Vector3(sway, heave, surge) * moveSpeed * boost * Time.deltaTime);

        var pitch = -Input.GetAxis("Mouse Y");
        var yaw = Input.GetAxis("Mouse X");

        rotation += new Vector3(pitch, yaw) * rotateSpeed;
        rotation.x = Mathf.Clamp(rotation.x, -90, 90);
        transform.localEulerAngles = rotation;
    }
}