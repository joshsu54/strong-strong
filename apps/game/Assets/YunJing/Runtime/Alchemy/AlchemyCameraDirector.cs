using System;
using UnityEngine;

namespace YunJing.Alchemy
{
    public enum Shot { Explore, Cabinet, Fuel, Scroll, Table, Brew, Mouth, Reveal }

    // Owns the main camera inside the workshop. Explore hands control back to the existing QingxiOrbitCamera;
    // every other shot blends from the current pose with a smoothstep, keeps clear of colliders, and returns
    // to the exact explore pose it left when cancelled.
    public sealed class AlchemyCameraDirector : MonoBehaviour
    {
        public QingxiOrbitCamera Orbit;
        public float BlendSeconds = .55f;
        public bool ReducedMotion;
        public Shot Current { get; private set; } = Shot.Explore;
        public bool Blending => blend < 1;
        public LayerMask Obstacles = ~0;
        /// Layer of the player stand-in; hidden in close-ups so it never blocks the prop (shown in explore and brew).
        public int PlayerLayer = 2;

        Camera cam; Vector3 fromPos, toPos, focus; Quaternion fromRot, toRot; float fromFov, toFov, blend = 1, duration;
        Func<(Vector3 pos, Vector3 look, float fov)> follow; Vector3 savedPos; Quaternion savedRot; bool hasSaved;
        public Action<Shot> Changed;

        void Awake() { cam = GetComponent<Camera>(); }

        public void Go(Shot shot, Vector3 position, Vector3 lookAt, float fov = 50, float seconds = -1)
        {
            Go(shot, () => (position, lookAt, fov), seconds);
        }

        /// Blend to a shot whose pose may follow a moving target (over-shoulder, reveal dolly).
        public void Go(Shot shot, Func<(Vector3 pos, Vector3 look, float fov)> pose, float seconds = -1)
        {
            if (Current == Shot.Explore) { savedPos = transform.position; savedRot = transform.rotation; hasSaved = true; }
            if (Orbit != null) Orbit.enabled = false;
            follow = pose; Current = shot;
            SetPlayerVisible(shot == Shot.Brew);
            Begin(seconds);
            Changed?.Invoke(shot);
        }

        /// Return to the third-person explore camera at the pose it had before the first close-up.
        public void Explore(float seconds = -1)
        {
            if (Current == Shot.Explore) return;
            Current = Shot.Explore;
            var back = hasSaved ? savedPos : transform.position; var backRot = hasSaved ? savedRot : transform.rotation;
            follow = () => (back, back + backRot * Vector3.forward, 50);
            Begin(seconds);
            Changed?.Invoke(Shot.Explore);
        }

        void Begin(float seconds)
        {
            fromPos = transform.position; fromRot = transform.rotation; fromFov = cam.fieldOfView;
            duration = ReducedMotion ? 0 : Mathf.Clamp(seconds < 0 ? BlendSeconds : seconds, .35f, .8f);
            blend = duration <= 0 ? 1 : 0;
            Resolve(); if (blend >= 1) Apply(1);
        }

        void SetPlayerVisible(bool on) { if (on) cam.cullingMask |= 1 << PlayerLayer; else cam.cullingMask &= ~(1 << PlayerLayer); }

        void Resolve()
        {
            var (pos, look, fov) = follow();
            focus = look; toPos = Clear(look, pos); toRot = Quaternion.LookRotation(look - toPos, Vector3.up); toFov = fov;
        }

        /// Pull the camera toward its focus when a collider sits between them (no clipping into props or walls).
        Vector3 Clear(Vector3 look, Vector3 pos)
        {
            var dir = pos - look; float dist = dir.magnitude; if (dist < .01f) return pos;
            if (Physics.SphereCast(look, .12f, dir / dist, out var hit, dist, Obstacles, QueryTriggerInteraction.Ignore))
                return look + dir / dist * Mathf.Max(.25f, hit.distance - .05f);
            return pos;
        }

        void LateUpdate()
        {
            if (Current == Shot.Explore && blend >= 1) { if (Orbit != null && !Orbit.enabled) Orbit.enabled = true; return; }
            if (follow == null) return;
            Resolve();
            if (blend < 1) { blend = Mathf.Min(1, blend + Time.unscaledDeltaTime / duration); Apply(blend); }
            else Apply(1);
        }

        void Apply(float t)
        {
            float s = t * t * (3 - 2 * t);
            transform.position = Vector3.Lerp(fromPos, toPos, s);
            transform.rotation = Quaternion.Slerp(fromRot, toRot, s);
            cam.fieldOfView = Mathf.Lerp(fromFov, toFov, s);
            if (t >= 1 && Current == Shot.Explore) { follow = null; cam.fieldOfView = 50; SetPlayerVisible(true); if (Orbit != null) Orbit.enabled = true; }
        }
    }
}
