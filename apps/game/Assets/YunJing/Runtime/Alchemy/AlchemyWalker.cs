using UnityEngine;
using UnityEngine.InputSystem;

namespace YunJing.Alchemy
{
    // Single-player stand-in for Cultivator inside the workshop interior: same camera-relative mapping and the
    // shared TouchInput joystick, but no FishNet prediction (the room has no other players or authority needs).
    [RequireComponent(typeof(CharacterController))]
    public sealed class AlchemyWalker : MonoBehaviour
    {
        public float Speed = 2.4f;
        public bool InputLocked;
        /// Scripted input (play-path test); added to keyboard and touch input like another stick.
        public Vector2 ScriptedInput;
        CharacterController controller; float vertical; Transform body;

        void Awake() { controller = GetComponent<CharacterController>(); body = transform.Find("Body"); }

        void Update()
        {
            var input = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null && !InputLocked)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
            }
            if (!InputLocked) input += Cultivator.TouchInput;
            input += ScriptedInput;
            var cam = Camera.main;
            var forward = cam ? cam.transform.forward : Vector3.forward; var right = cam ? cam.transform.right : Vector3.right;
            var dir = Cultivator.CameraRelativeDirection(Vector2.ClampMagnitude(input, 1), forward, right);
            var move = new Vector3(dir.x, 0, dir.y);
            vertical = controller.isGrounded ? -1 : vertical - 9.8f * Time.deltaTime;
            controller.Move((move * Speed + Vector3.up * vertical) * Time.deltaTime);
            if (move.sqrMagnitude > .01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 1 - Mathf.Exp(-12 * Time.deltaTime));
            if (body != null) body.localPosition = new Vector3(0, 1 + Mathf.Abs(Mathf.Sin(Time.time * 9)) * .04f * Mathf.Clamp01(move.magnitude), 0);
        }

        public void Face(Vector3 point)
        {
            var d = point - transform.position; d.y = 0;
            if (d.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }

    /// Left-half virtual joystick for touch screens; writes the shared Cultivator.TouchInput.
    public sealed class TouchStick : MonoBehaviour
    {
        int finger = -1; Vector2 origin; public RectTransform Knob, Pad; public float Radius = 100;
        /// True while a finger is steering, so a tap elsewhere can be told apart from walking.
        public static bool Steering;
        void Update()
        {
            var ts = Touchscreen.current; if (ts == null) return;
            bool found = false;
            foreach (var touch in ts.touches)
            {
                int id = touch.touchId.ReadValue(); var pos = touch.position.ReadValue();
                if (!touch.press.isPressed) continue;
                if (finger < 0 && pos.x < Screen.width * .4f && pos.y < Screen.height * .6f) { finger = id; origin = pos; }
                if (id != finger) continue;
                found = true; float r = Radius * (Pad ? Pad.lossyScale.x : 1); // canvas units, so the stick keeps its physical size on dense screens
                var delta = Vector2.ClampMagnitude(pos - origin, r);
                Cultivator.TouchInput = delta / r;
                if (Pad) Pad.position = origin; if (Knob) Knob.position = origin + delta;
            }
            Steering = found;
            if (!found) { finger = -1; Cultivator.TouchInput = Vector2.zero; if (Knob && Pad) Knob.position = Pad.position; }
        }
    }
}
