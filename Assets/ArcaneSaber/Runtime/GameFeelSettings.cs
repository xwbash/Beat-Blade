using UnityEngine;

namespace ArcaneSaber
{
    [CreateAssetMenu(menuName = "Beat and Blade/Game Feel Settings")]
    public sealed class GameFeelSettings : ScriptableObject
    {
        [Header("Flow assistance • 0 = strict, 1 = relaxed")]
        [Range(0,1)] public float SaberAssist = .8f;
        [Range(0,1)] public float MagicTimingAssist = .75f;
        [Header("Magic • stiffness is angular frequency squared; damping is ratio")]
        [Range(20, 1400)] public float MagicHandPositionStiffness = 560;
        [Range(.1f, 2)] public float MagicHandPositionDamping = .68f;
        public float MagicHandRotationStiffness = 420, MagicHandRotationDamping = .72f;
        [Range(0, 1)] public float MagicGestureArcIntensity = .24f;
        [Range(.1f, 1)] public float MagicGestureDistance = .40f;
        [Range(0, 2)] public float MagicGestureOvershoot = .35f;
        public float MagicFingerCurlIntensity = 1, MagicFingerReleaseSpeed = 18;
        public float MagicTrailIntensity = 1, MagicIdleSway = .018f, MagicRecoverySpeed = 3;
        [Header("Saber")]
        public float SaberPositionStiffness = 1050, SaberPositionDamping = .74f;
        public float SaberRotationStiffness = 640, SaberRotationDamping = .62f;
        [Range(0, 2)] public float SaberBladeLag = .9f;
        public float SaberWristInfluence = 1, SaberOvershoot = .3f;
        [Range(.1f, 3)] public float SaberSwingSensitivity = 1;
        public float SaberTrailIntensity = 1;
        [Header("Camera")]
        [Range(0,.35f)] public float BodycamLens = .18f;
        [Range(0,2)] public float MouseCameraMotion = .8f;
        [Range(0, 2)] public float MagicCameraInfluence = .6f, SaberCameraInfluence = .4f;
        public float CameraPositionStiffness = 140, CameraPositionDamping = .85f;
        public float CameraRotationStiffness = 120, CameraRotationDamping = .9f;
        [Range(0, 2)] public float CameraImpactIntensity = 1;
        public float MaximumCameraRoll = 3;
        [Range(0, 2)] public float IdleCameraInfluence = .5f, MusicCameraInfluence = 1;
        [Range(0,1.5f)] public float StageBrightness = 1;
        [Range(0,1)] public float HitSoundVolume = .7f;
        public bool ReducedMotion;
        public const int CurrentVersion=2;
        public int FeelVersion=CurrentVersion;
        // Saves from before a retune keep the player's own preferences but take the new motion.
        public void Restore(string json)
        {
            var saved=CreateInstance<GameFeelSettings>(); saved.FeelVersion=0;
            JsonUtility.FromJsonOverwrite(json,saved);
            if(saved.FeelVersion>=CurrentVersion) JsonUtility.FromJsonOverwrite(json,this);
            else { SaberAssist=saved.SaberAssist; MagicTimingAssist=saved.MagicTimingAssist; SaberSwingSensitivity=saved.SaberSwingSensitivity; }
            DestroyImmediate(saved);
        }
    }

    // Exact constant-target solution of x'' + 2*zeta*omega*x' + omega²(x-target)=0.
    // Unlike explicit Euler this stays stable at low and high refresh rates.
    [System.Serializable]
    public struct Spring3
    {
        public Vector3 Position, Velocity;
        public void Step(Vector3 target, float stiffness, float damping, float dt)
        {
            if (dt <= 0) return;
            float w = Mathf.Sqrt(Mathf.Max(1, stiffness)), z = Mathf.Max(.05f, damping);
            Vector3 y = Position - target;
            if (z < .999f)
            {
                float a = z * w, b = w * Mathf.Sqrt(1 - z*z);
                float e = Mathf.Exp(-a*dt), c = Mathf.Cos(b*dt), s = Mathf.Sin(b*dt);
                Vector3 j = (Velocity + a*y)/b;
                Position = target + e*(y*c+j*s);
                Velocity = e*(Velocity*c - (a*j+b*y)*s);
            }
            else if (z <= 1.001f)
            {
                float e = Mathf.Exp(-w*dt); Vector3 j = Velocity+w*y;
                Position = target + (y+j*dt)*e;
                Velocity = (Velocity-w*j*dt)*e;
            }
            else
            {
                float q = Mathf.Sqrt(z*z-1), r1 = -w*(z-q), r2 = -w*(z+q);
                Vector3 c1 = (Velocity-r2*y)/(r1-r2), c2 = y-c1;
                Position = target+c1*Mathf.Exp(r1*dt)+c2*Mathf.Exp(r2*dt);
                Velocity = c1*r1*Mathf.Exp(r1*dt)+c2*r2*Mathf.Exp(r2*dt);
            }
        }
    }

    public static class GestureMath
    {
        public static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        { float u = 1-t; return u*u*u*a + 3*u*u*t*b + 3*u*t*t*c + t*t*t*d; }
        public static Vector2 Direction(int d)
        {
            switch (d) {
                case 0: return Vector2.up; case 1: return Vector2.left;
                case 2: return Vector2.down; case 3: return Vector2.right;
                case 4: return new Vector2(-1,1).normalized; case 5: return new Vector2(1,1).normalized;
                case 6: return new Vector2(-1,-1).normalized; default: return new Vector2(1,-1).normalized;
            }
        }
        public static string Label(int d) => new[] { "W", "A", "S", "D", "", "", "", "" }[d];
    }
}
