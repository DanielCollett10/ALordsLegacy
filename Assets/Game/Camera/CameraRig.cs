using LordsLegacy.Sim.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LordsLegacy.Game
{
    /// <summary>
    /// RTS camera: pan, rotate and zoom over the terrain. Movement methods take explicit input values and
    /// delta time so tests do not depend on real frames. Reads terrain from the Sim (<see cref="IHeightField"/>).
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [Header("Zoom (metres above ground)")]
        public float MinHeight = 3f;
        public float MaxHeight = 400f;
        /// <summary>Fraction of height changed per scroll step.</summary>
        public float ZoomStep = 0.12f;

        [Header("Pitch (degrees)")]
        public float NearPitch = 15f;
        public float FarPitch = 70f;

        [Header("Movement")]
        /// <summary>Pan speed in metres/second per metre of height.</summary>
        public float PanSpeedPerHeight = 1.5f;
        public float MinPanSpeed = 8f;
        public float RotateSpeedDegPerSec = 90f;
        public float MouseRotateDegPerPixel = 0.25f;
        /// <summary>Minimum distance kept between camera and ground.</summary>
        public float GroundMargin = 1.5f;
        /// <summary>Distance kept inside the map edge.</summary>
        public float EdgeMargin = 1f;
        public float ScreenEdgePixels = 8f;
        public bool EdgePanEnabled = true;
        /// <summary>Scroll units per notch of the mouse wheel (Input System reports 120 per notch).</summary>
        public float ScrollUnitsPerStep = 120f;

        private IHeightField _terrain;
        private float _x, _z, _height = 100f, _pitch = 60f, _yaw;

        /// <summary>Camera x (metres).</summary>
        public float X => _x;
        /// <summary>Camera z (metres).</summary>
        public float Z => _z;
        /// <summary>Height above the ground under the camera.</summary>
        public float HeightAboveGround => _height;
        /// <summary>Pitch in degrees (0 horizontal, 90 straight down).</summary>
        public float PitchDeg => _pitch;
        /// <summary>Yaw in degrees (0 looks along +z).</summary>
        public float YawDeg => _yaw;

        /// <summary>Sets the terrain to stay above and inside. Null means flat ground, unbounded.</summary>
        public void SetTerrain(IHeightField terrain)
        {
            _terrain = terrain;
            Apply();
        }

        /// <summary>Places the camera. Values are clamped to bounds and zoom limits.</summary>
        public void SetPose(float x, float z, float heightAboveGround, float pitchDeg, float yawDeg)
        {
            _x = x; _z = z; _height = heightAboveGround; _pitch = pitchDeg; _yaw = yawDeg;
            Apply();
        }

        /// <summary>Pans in camera-relative directions. Inputs are -1..1; speed scales with height.</summary>
        public void Move(float right, float forward, float dt)
        {
            float speed = Mathf.Max(MinPanSpeed, PanSpeedPerHeight * _height);
            Vector2 dir = new Vector2(right, forward);
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            float rad = _yaw * Mathf.Deg2Rad;
            Vector2 fwd = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
            Vector2 rgt = new Vector2(fwd.y, -fwd.x);
            Vector2 d = (rgt * dir.x + fwd * dir.y) * (speed * dt);
            _x += d.x; _z += d.y;
            Apply();
        }

        /// <summary>Rotates yaw by degrees.</summary>
        public void Rotate(float deltaYawDeg)
        {
            _yaw = Mathf.Repeat(_yaw + deltaYawDeg, 360f);
            Apply();
        }

        /// <summary>Zooms by scroll steps; positive zooms in (lower).</summary>
        public void Zoom(float steps)
        {
            _height *= Mathf.Pow(1f - ZoomStep, steps);
            _height = Mathf.Clamp(_height, MinHeight, MaxHeight);
            float t = Mathf.InverseLerp(MinHeight, MaxHeight, _height);
            _pitch = Mathf.Lerp(NearPitch, FarPitch, t);
            Apply();
        }

        private void Start()
        {
            if (_terrain == null)
            {
                var host = FindFirstObjectByType<SimHost>();
                if (host != null && host.IsInitialized) _terrain = host.World.Terrain;
            }
            Apply();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;
            if (kb != null)
            {
                float r = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                float f = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                if (EdgePanEnabled && mouse != null && Application.isFocused)
                {
                    Vector2 p = mouse.position.ReadValue();
                    if (p.x <= ScreenEdgePixels) r -= 1; else if (p.x >= Screen.width - ScreenEdgePixels) r += 1;
                    if (p.y <= ScreenEdgePixels) f -= 1; else if (p.y >= Screen.height - ScreenEdgePixels) f += 1;
                }
                if (r != 0 || f != 0) Move(Mathf.Clamp(r, -1, 1), Mathf.Clamp(f, -1, 1), dt);
                float rot = (kb.eKey.isPressed ? 1 : 0) - (kb.qKey.isPressed ? 1 : 0);
                if (rot != 0) Rotate(rot * RotateSpeedDegPerSec * dt);
            }
            if (mouse != null)
            {
                if (mouse.middleButton.isPressed) Rotate(mouse.delta.ReadValue().x * MouseRotateDegPerPixel);
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0) Zoom(scroll / ScrollUnitsPerStep);
            }
        }

        private void Apply()
        {
            _height = Mathf.Clamp(_height, MinHeight, MaxHeight);
            _pitch = Mathf.Clamp(_pitch, 0f, 90f);
            if (_terrain != null)
            {
                Bounds2 b = _terrain.Bounds;
                if (!b.IsEmpty)
                {
                    _x = Mathf.Clamp(_x, b.MinX + EdgeMargin, b.MaxX - EdgeMargin);
                    _z = Mathf.Clamp(_z, b.MinZ + EdgeMargin, b.MaxZ - EdgeMargin);
                }
            }
            float ground = _terrain != null ? _terrain.HeightAt(_x, _z) : 0f;
            float y = ground + Mathf.Max(_height, GroundMargin);
            transform.SetPositionAndRotation(new Vector3(_x, y, _z), Quaternion.Euler(_pitch, _yaw, 0f));
        }
    }
}
