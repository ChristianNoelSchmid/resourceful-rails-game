using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rails {
  public class CameraController : MonoBehaviour {
    public float focusDistance = 1f;
    public float moveSpeed = 1f;
    public float rotateSpeed = 1f;
    public float zoomSpeed = 1f;

    public float zoomMinDist = 1f;
    public float zoomMaxDist = 10f;

    private Vector2 _moveInput;
    private Vector2 _rotateInput;
    private float _zoomInput;
    private Vector3 focus;
    private float distance;

		private void Start() {
      _moveInput = Vector2.zero;
      _rotateInput = Vector2.zero;
      focus = transform.position;
      focus.y = 0;
      focus.z += focusDistance;
      transform.LookAt(focus);
		}
		void Update() {
      distance = Vector3.Distance(transform.position, focus);
      Move(_moveInput, distance);
      Rotate(_rotateInput);
      Zoom(_zoomInput, distance);
    }

    #region Input System
    public void OnMove(InputAction.CallbackContext context) {
      _moveInput = context.ReadValue<Vector2>();
    }
    public void OnRotate(InputAction.CallbackContext context) {
      _rotateInput = context.ReadValue<Vector2>();
		}
    public void OnZoom(InputAction.CallbackContext context) {
      _zoomInput = Mathf.Clamp(context.ReadValue<float>(), -1, 1);
    }
    public void OnSelect(InputAction.CallbackContext context) {
      if (context.performed) {
        Select();
			}
		}
		#endregion // Input

		#region Methods

    // Moves the camera across the X-Z plane.
		private void Move(Vector2 input, float distance) {
      Vector3 move = (Vector3.right * input.x * moveSpeed)
          + (Vector3.forward * input.y * moveSpeed);
      Quaternion planarDirection = Quaternion.LookRotation(
        Vector3.ProjectOnPlane(transform.forward, Vector3.up), Vector3.up);

      move = planarDirection * move * distance * Time.deltaTime;
      transform.position += move;
      focus += move;
      return;
    }

    // Rotates the camera around the focus point.
    private void Rotate(Vector2 input) {
      // Rotate horizontally along vertical axis.
      float rot = rotateSpeed * input.x * Time.deltaTime;
      transform.RotateAround(focus, Vector3.up, rot);

      // Rotate vertically along side axis.
      float dot = Vector3.Dot(Vector3.down, transform.forward);
      if ((dot < 0.05f && input.y < 0f) || (dot > 0.99f && input.y > 0f))
        return;
      rot = rotateSpeed * input.y * Time.deltaTime;
      transform.RotateAround(focus, transform.right, rot);
      return;
		}

    // Zooms the camera towards or away from the focus point.
    private void Zoom(float input, float distance) {
			if ((distance < zoomMinDist && input > 0) || (distance > zoomMaxDist && input < 0))
				return;
			float zoomDelta = zoomSpeed * input * distance * Time.deltaTime;
      transform.position = Vector3.MoveTowards(transform.position, focus, zoomDelta);
      return;
		}

    // Gets a position on the map.
    private void Select() {
      Plane plane = new Plane(Vector3.up, 0f);
      Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
      float enter = 0f;

      if (plane.Raycast(ray, out enter)) {
        Debug.DrawLine(transform.position, ray.GetPoint(enter), Color.green, 2f);
        
			}
			else {
        Debug.DrawRay(transform.position, ray.direction, Color.red, 2f);
			}

      return;
		}
		#endregion
	}
}