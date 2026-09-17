using UnityEngine;

namespace VoxelSurvival
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        public VoxelWorld world;
        public Camera eyes;
        public float walkSpeed = 4.5f, sprintSpeed = 7f, jumpHeight = 1.25f, sensitivity = 2f;
        public bool Paused { get; private set; }
        public bool InputEnabled { get; set; } = true;
        public bool CanAct => InputEnabled && !Paused && Cursor.lockState == CursorLockMode.Locked;
        public CharacterController Controller { get; private set; }
        private float pitch, verticalSpeed;
        private Vector3 spawn;
        private void Awake() => Controller = GetComponent<CharacterController>();
        public void Initialize(Vector3 position)
        {
            spawn = position; Teleport(position); SetPaused(false);
        }
        public void SetPaused(bool value)
        {
            Paused = value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            Time.timeScale = value ? 0 : 1;
        }
        public void Teleport(Vector3 position)
        {
            Controller.enabled = false; transform.position = position; Controller.enabled = true;
            verticalSpeed = 0;
        }
        public void Move(Vector2 input, bool jump, bool sprint, float delta)
        {
            Vector3 horizontal = transform.TransformDirection(new Vector3(input.x, 0, input.y));
            horizontal = Vector3.ClampMagnitude(horizontal, 1) * (sprint ? sprintSpeed : walkSpeed);
            // A missing chunk is a temporary barrier, never a hole the player can fall through.
            var next = Vector3Int.FloorToInt(transform.position + horizontal * delta);
            if (!world.IsLoaded(next)) horizontal = Vector3.zero;
            if (Controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (Controller.isGrounded && jump) verticalSpeed = Mathf.Sqrt(jumpHeight * 2 * 24);
            verticalSpeed = Mathf.Max(verticalSpeed - 24 * delta, -45);
            Controller.Move((horizontal + Vector3.up * verticalSpeed) * delta);
        }
        private void Update()
        {
            if (InputEnabled && Input.GetKeyDown(KeyCode.Escape)) { SetPaused(!Paused); return; }
            if (!InputEnabled || Paused) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0)) SetPaused(false);
                return;
            }
            transform.Rotate(0, Input.GetAxisRaw("Mouse X") * sensitivity, 0);
            pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * sensitivity, -88, 88);
            eyes.transform.localEulerAngles = new Vector3(pitch, 0, 0);
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0);
            Move(new Vector2(x,z), Input.GetKeyDown(KeyCode.Space), Input.GetKey(KeyCode.LeftShift), Time.deltaTime);
            if (transform.position.y < -10) { world.LoadSpawn(spawn); Teleport(spawn); }
        }
        private void OnApplicationFocus(bool focus) { if (!focus && InputEnabled) SetPaused(true); }
        private void OnDestroy() { Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
