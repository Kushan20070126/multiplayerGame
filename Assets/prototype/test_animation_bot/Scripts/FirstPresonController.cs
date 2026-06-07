using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstPresonController : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float gravity = -9.81f;
    
    private CharacterController _characterController;
    private Transform _mainCameraTransform;
    private Vector3 _velocity;

    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        
        // Grab the main camera transform to align movement relative to where we look
        if (Camera.main != null)
        {
            _mainCameraTransform = Camera.main.transform;
        }
        
        // Lock the hardware cursor to the center of the game screen
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // 1. Handle Grounded State & Gravity
        if (_characterController.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f; 
        }

        // 2. Fetch Basic Movement Inputs
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // 3. Calculate movement direction relative to the camera's orientation
        Vector3 forward = _mainCameraTransform.forward;
        Vector3 right = _mainCameraTransform.right;

        // Flatten directions out on the Y axis so the player doesn't fly or dig into the floor
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 moveDirection = (forward * moveZ) + (right * moveX);

        // 4. Move the Character Controller
        _characterController.Move(moveDirection * moveSpeed * Time.deltaTime);

        // 5. Apply ongoing gravity terminal velocity over time
        _velocity.y += gravity * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }

}
