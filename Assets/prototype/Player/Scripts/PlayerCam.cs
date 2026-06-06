using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCam : MonoBehaviour
{

 public Transform orientation;

 public float sensX;
 public float sensY;

float xRotation;
float yRotation;
 private void Start()
 {
  Cursor.lockState = CursorLockMode.Locked;
  Cursor.visible = false;
 }

 private void Update()
 {
  float mouseX = Input.GetAxis("Mouse X") * Time.deltaTime * sensX;
  float mouseY = Input.GetAxis("Mouse Y") * Time.deltaTime * sensY;
    yRotation += mouseX;
    xRotation -= mouseY;
    xRotation = Mathf.Clamp(xRotation, -90f, 90f);

    transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
    orientation.rotation = Quaternion.Euler(0, yRotation, 0);


}
}
