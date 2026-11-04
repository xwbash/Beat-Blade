using UnityEngine;

namespace ArcaneSaber
{
    public static class RhythmAssist
    {
        public static bool MatchesIntent(int direction,Vector2 velocity,float strength)
        {
            if(strength<=0 || velocity.magnitude<.16f) return false;
            if(direction==8) return true;
            // Diagonal cuts accept a useful component of the requested motion.
            // Vertical cuts follow the player's upward/downward nudge despite blade inertia.
            float relaxed=(direction==1 || direction==3)?.45f:.12f;
            return Vector2.Dot(velocity.normalized,GestureMath.Direction(direction))>=Mathf.Lerp(.64f,relaxed,strength);
        }
        public static float Window(RhythmNote note,GameFeelSettings settings)
        {
            float strength=!settings?0:note.Lane==0?settings.MagicTimingAssist:settings.SaberAssist;
            return note.HitWindow+(note.Lane==0?.13f:.09f)*strength;
        }
    }
}
