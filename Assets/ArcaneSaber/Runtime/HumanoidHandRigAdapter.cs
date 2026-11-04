using UnityEngine;

namespace ArcaneSaber
{
    // Optional authored-avatar bridge. Enable IK Pass on the Animator controller layer.
    // The standalone demo uses the same finger solver on its generated joint hierarchy.
    [RequireComponent(typeof(Animator))]
    public sealed class HumanoidHandRigAdapter : MonoBehaviour
    {
        public MagicHandController Magic;
        public GameFeelSettings Settings;
        [Range(0,1)] public float IKWeight=1;
        Animator animator; MagicFingerAnimator fingers; float age=10;
        void Awake()
        {
            animator=GetComponent<Animator>();
            if(!animator.isHuman) { Debug.LogWarning("Arcane hand adapter requires a Humanoid avatar.",this); enabled=false; return; }
            HumanBodyBones[,] ids={
                {HumanBodyBones.LeftThumbProximal,HumanBodyBones.LeftThumbIntermediate,HumanBodyBones.LeftThumbDistal},
                {HumanBodyBones.LeftIndexProximal,HumanBodyBones.LeftIndexIntermediate,HumanBodyBones.LeftIndexDistal},
                {HumanBodyBones.LeftMiddleProximal,HumanBodyBones.LeftMiddleIntermediate,HumanBodyBones.LeftMiddleDistal},
                {HumanBodyBones.LeftRingProximal,HumanBodyBones.LeftRingIntermediate,HumanBodyBones.LeftRingDistal},
                {HumanBodyBones.LeftLittleProximal,HumanBodyBones.LeftLittleIntermediate,HumanBodyBones.LeftLittleDistal}};
            var bones=new Transform[5][];
            for(int f=0;f<5;f++)
            {
                bones[f]=new Transform[3];
                for(int b=0;b<3;b++) { bones[f][b]=animator.GetBoneTransform(ids[f,b]); if(!bones[f][b]) { enabled=false; Debug.LogWarning("Avatar is missing required finger bones.",this); return; } }
            }
            fingers=new MagicFingerAnimator(bones);
            if(Magic) Magic.Pressed+=OnRelease;
        }
        void OnRelease(int direction) { age=0; }
        void OnAnimatorIK(int layerIndex)
        {
            if(!Magic) return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,IKWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,IKWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand,Magic.Palm);
            animator.SetIKRotation(AvatarIKGoal.LeftHand,Magic.WorldRotation);
        }
        void LateUpdate()
        { age+=Time.deltaTime; if(fingers!=null && Magic && Settings) fingers.Step(age,Magic.Velocity.magnitude,Settings,Time.deltaTime); }
        void OnDestroy() { if(Magic) Magic.Pressed-=OnRelease; }
    }
}
