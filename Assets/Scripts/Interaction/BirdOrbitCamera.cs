using UnityEngine;

namespace ImmersiveMapInterface.Interaction
{
	/// <summary>
	/// Simple editor/runtime orbit camera for Bird PoV around a target (Ground center).
	/// - Right mouse drag: orbit
	/// - Mouse wheel: zoom
	/// - Middle drag: pan (optional)
	/// </summary>
	[ExecuteAlways]
	public class BirdOrbitCamera : MonoBehaviour
	{
		[SerializeField] private Transform target;
		[SerializeField] private Vector3 targetOffset = Vector3.zero;
		[SerializeField] private float distance = 12f;
		[SerializeField] private float minDistance = 4f;
		[SerializeField] private float maxDistance = 60f;
		[SerializeField] private float orbitSpeed = 120f;
		[SerializeField] private float zoomSpeed = 4f;
		[SerializeField] private float panSpeed = 0.1f;

		[SerializeField] private float yaw = 30f;
		[SerializeField] private float pitch = 30f;
		[SerializeField] private float minPitch = 5f;
		[SerializeField] private float maxPitch = 85f;

		private void LateUpdate()
		{
			if (target == null)
			{
				var ground = GameObject.Find("Ground");
				if (ground != null) target = ground.transform;
			}

			if (Application.isPlaying)
			{
				UpdateInput();
			}

			UpdateCamera();
		}

		private void UpdateInput()
		{
			if (Input.GetMouseButton(1))
			{
				float dx = Input.GetAxis("Mouse X");
				float dy = Input.GetAxis("Mouse Y");
				yaw += dx * orbitSpeed * Time.deltaTime;
				pitch -= dy * orbitSpeed * Time.deltaTime;
				pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
			}

			float scroll = Input.mouseScrollDelta.y;
			if (Mathf.Abs(scroll) > 0.0001f)
			{
				distance *= Mathf.Exp(-scroll * zoomSpeed * Time.deltaTime);
				distance = Mathf.Clamp(distance, minDistance, maxDistance);
			}

			if (Input.GetMouseButton(2))
			{
				float dx = Input.GetAxis("Mouse X");
				float dy = Input.GetAxis("Mouse Y");
				// pan on camera plane
				var right = transform.right;
				var up = Vector3.up;
				targetOffset -= (right * dx + up * dy) * panSpeed * distance * Time.deltaTime;
			}
		}

		private void UpdateCamera()
		{
			if (target == null) return;

			Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
			Vector3 focus = target.position + targetOffset;
			Vector3 camPos = focus + rot * new Vector3(0f, 0f, -distance);
			transform.position = camPos;
			transform.rotation = rot;
		}
	}
}

