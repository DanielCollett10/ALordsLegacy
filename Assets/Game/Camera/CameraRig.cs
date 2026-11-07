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
        /// <summary>Middle-drag yaw, degrees per pixel moved horizontally.</summary>
        public float MouseRotateDegPerPixel = 0.25f;
        /// <summary>Middle-drag tilt, degrees per pixel moved vertically (drag up tilts towards the horizon).</summary>
        public float MouseTiltDegPerPixel = 0.2f;
        /// <summary>Lowest orbit pitch in degrees (closest to the horizon). Default 10.</summary>
        public float MinOrbitPitch = 10f;
        /// <summary>Highest orbit pitch in degrees (closest to straight down). Default 85.</summary>
        public float MaxOrbitPitch = 85f;
        /// <summary>Minimum distance kept between camera and ground.</summary>
        public float GroundMargin = 1.5f;
        /// <summary>Distance kept inside the map edge.</summary>
        public float EdgeMargin = 1f;
        public float ScreenEdgePixels = 8f;
        public bool EdgePanEnabled = true;
        /// <summary>Scroll units per notch of the mouse wheel (Input System reports 120 per notch).</summary>
        public float ScrollUnitsPerStep = 120f;

        [Header("Initial pose (applied at Start)")]
        [SerializeField] private Vector2 _startXZ = new Vector2(1024f, 1024f);
        [SerializeField] private float _startHeight = 120f;
        [SerializeField] private float _startPitch = 55f;
        [SerializeField] private float _startYaw;

        private bool _poseSet;
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

        /// <summary>True while a build tool is active; it then owns the left and right mouse buttons (ui.md "Camera vs tools").</summary>
        public bool ToolActive { get; set; }

        /// <summary>
        /// Whether the camera reacts to a mouse button (0 left, 1 right, 2 middle). Left and right are refused while
        /// <see cref="ToolActive"/>; middle always works.
        /// </summary>
        public bool AcceptsMouseButton(int button) => button == 2 || (button != 0 && button != 1) || !ToolActive;

        /// <summary>Sets the terrain to stay above and inside. Null means flat ground, unbounded.</summary>
        public void SetTerrain(IHeightField terrain)
        {
            _terrain = terrain;
            Apply();
        }

        /// <summary>Places the camera. Values are clamped to bounds and zoom limits.</summary>
        public void SetPose(float x, float z, float heightAboveGround, float pitchDeg, float yawDeg)
        {
            _poseSet = true;
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

        /// <summary>Closest focus distance (metres) reachable by zooming.</summary>
        public float MinDistance => MinHeight / Mathf.Sin(NearPitch * Mathf.Deg2Rad);
        /// <summary>Farthest focus distance (metres) reachable by zooming.</summary>
        public float MaxDistance => MaxHeight / Mathf.Sin(FarPitch * Mathf.Deg2Rad);

        /// <summary>
        /// Zooms by scroll steps; positive zooms in. Moves the camera along the view ray towards (or away from) the
        /// ground point at screen centre, and tilts it a little towards the horizon as it gets closer (and back
        /// down when zooming out). Works at any tilt because it scales the focus distance, not the height.
        /// </summary>
        public void Zoom(float steps)
        {
            float d = FocusDistance();
            Vector3 pivot = FocusPoint();
            float d2 = Mathf.Clamp(d * Mathf.Pow(1f - ZoomStep, steps), MinDistance, MaxDistance);
            float before = Mathf.InverseLerp(MinDistance, MaxDistance, d);
            float after = Mathf.InverseLerp(MinDistance, MaxDistance, d2);
            _pitch = Mathf.Clamp(_pitch + Mathf.Lerp(NearPitch, FarPitch, after) - Mathf.Lerp(NearPitch, FarPitch, before), MinOrbitPitch, MaxOrbitPitch);
            Vector3 f = Forward();
            _x = pivot.x - f.x * d2;
            _z = pivot.z - f.z * d2;
            _height = Mathf.Clamp(pivot.y - f.y * d2 - GroundAt(_x, _z), GroundMargin, MaxHeight);
            Apply();
        }

        private Vector3 Forward()
        {
            float p = _pitch * Mathf.Deg2Rad, y = _yaw * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(y) * Mathf.Cos(p), -Mathf.Sin(p), Mathf.Cos(y) * Mathf.Cos(p));
        }

        private float GroundAt(float x, float z) => _terrain != null ? _terrain.HeightAt(x, z) : 0f;

        /// <summary>
        /// Distance along the view direction from the camera to the Sim terrain (the point under the screen centre),
        /// found by marching then bisecting. This is the orbit radius.
        /// </summary>
        public float FocusDistance()
        {
            float h = Mathf.Max(_height, GroundMargin);
            Vector3 o = new Vector3(_x, GroundAt(_x, _z) + h, _z);
            Vector3 f = Forward();
            float lo = 0f, hi = h / Mathf.Max(Mathf.Sin(_pitch * Mathf.Deg2Rad), 0.05f);
            for (int i = 0; i < 32 && o.y + f.y * hi > GroundAt(o.x + f.x * hi, o.z + f.z * hi); i++) { lo = hi; hi *= 1.5f; }
            for (int i = 0; i < 24; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (o.y + f.y * mid > GroundAt(o.x + f.x * mid, o.z + f.z * mid)) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        /// <summary>Ground point under the screen centre.</summary>
        public Vector3 FocusPoint()
        {
            float d = FocusDistance();
            Vector3 f = Forward();
            float x = _x + f.x * d, z = _z + f.z * d;
            return new Vector3(x, GroundAt(x, z), z);
        }

        /// <summary>
        /// Orbits around the ground point under the screen centre at constant distance: yaw turns, pitch tilts
        /// (positive tilts down). Pitch is limited; the camera is held above the terrain and inside the map.
        /// </summary>
        public void Orbit(float deltaYawDeg, float deltaPitchDeg)
        {
            float d = FocusDistance();
            Vector3 pivot = FocusPoint();
            _yaw = Mathf.Repeat(_yaw + deltaYawDeg, 360f);
            _pitch = Mathf.Clamp(_pitch + deltaPitchDeg, MinOrbitPitch, MaxOrbitPitch);
            Vector3 f = Forward();
            _x = pivot.x - f.x * d;
            _z = pivot.z - f.z * d;
            _height = Mathf.Clamp(pivot.y - f.y * d - GroundAt(_x, _z), GroundMargin, MaxHeight);
            Apply();
        }

        private void Start()
        {
            if (!_poseSet) { _x = _startXZ.x; _z = _startXZ.y; _height = _startHeight; _pitch = _startPitch; _yaw = _startYaw; }
            TryAcquireTerrain();
            Apply();
        }

        private SimHost _host;

        /// <summary>Looks the terrain up from SimHost; SimHost may initialise after our Start, so Update retries.</summary>
        private void TryAcquireTerrain()
        {
            if (_terrain != null) return;
            if (_host == null) _host = FindFirstObjectByType<SimHost>();
            if (_host != null && _host.IsInitialized) { _terrain = _host.World.Terrain; Apply(); }
        }

        private void Update()
        {
            TryAcquireTerrain();
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
            }
            if (mouse != null)
            {
                if (AcceptsMouseButton(2) && mouse.middleButton.isPressed)
                {
                    Vector2 md = mouse.delta.ReadValue();
                    if (md != Vector2.zero) Orbit(md.x * MouseRotateDegPerPixel, -md.y * MouseTiltDegPerPixel);
                }
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
