using UnityEngine;

namespace BotsBolts.Presentation
{
    internal enum RobotMotionTransition { None, Leap, Land }

    // Owns only Animator playback. Movement and cosmetic spring forces stay
    // independent of clip timing and never write to the gameplay controller.
    internal sealed class RobotAnimationPlayback
    {
        private const float ReferenceRunSpeed = 1.5f; // 1.2 m stride / 0.8 s clip.
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int CycleRate = Animator.StringToHash("CycleRate");
        private static readonly int Leap = Animator.StringToHash("Robot Motion.Leap");
        private static readonly int Land = Animator.StringToHash("Robot Motion.Land");
        private static readonly int Fall = Animator.StringToHash("Robot Motion.Fall");
        private static readonly int Locomotion = Animator.StringToHash("Locomotion");
        private readonly Animator animator;

        public bool IsFalling { get; private set; }

        public RobotAnimationPlayback(Animator animator) => this.animator = animator;

        public void Reset()
        {
            IsFalling = false;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.Rebind();
            animator.SetFloat(Speed, 0);
            animator.SetFloat(CycleRate, 1);
        }

        public RobotMotionTransition Update(float speed, float actualSpeed, bool airborne,
            bool wasAirborne, bool launched, float verticalVelocity)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return RobotMotionTransition.None;

            animator.SetFloat(Speed, speed);
            animator.SetFloat(CycleRate, speed > .2f ? actualSpeed / ReferenceRunSpeed : 1);
            RobotMotionTransition transition = RobotMotionTransition.None;
            if (!wasAirborne && airborne)
            {
                animator.CrossFadeInFixedTime(launched ? Leap : Fall, .05f);
                IsFalling = !launched;
                if (launched) transition = RobotMotionTransition.Leap;
            }
            if (airborne && !IsFalling && verticalVelocity < -.6f)
            {
                animator.CrossFadeInFixedTime(Fall, .1f);
                IsFalling = true;
            }
            if (wasAirborne && !airborne)
            {
                animator.CrossFadeInFixedTime(Land, .04f);
                transition = RobotMotionTransition.Land;
            }
            return transition;
        }

        public float GaitPhase(float fallback, float speed)
        {
            if (animator == null || animator.runtimeAnimatorController == null || speed <= .5f)
                return fallback;
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == Locomotion ? state.normalizedTime * Mathf.PI * 2 : fallback;
        }
    }
}
