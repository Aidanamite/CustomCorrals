using UnityEngine;

namespace CustomCorrals
{
    abstract class TransformAnimator<T> : MonoBehaviour
    {
        float current = 0;
        public float AnimationTime = 1;
        T start;
        T target;
        protected T Start => start;
        protected T Target => target;
        protected abstract void UpdateProgress(float progress);
        void Update()
        {
            if (current < AnimationTime)
            {
                current += Time.deltaTime;
                if (current >= AnimationTime)
                    current = AnimationTime;
                UpdateProgress(current / AnimationTime);
            }
        }
        protected void EndAnimation()
        {
            current = AnimationTime;
            UpdateProgress(1);
        }
        protected void ResetAnimation()
        {
            current = 0;
            UpdateProgress(0);
        }
        public void SetTarget(T Target)
        {
            if (target.Equals(Target))
                return;
            start = GetStart();
            target = Target;
            ResetAnimation();
        }
        public void SetImmediate(T Target)
        {
            target = Target;
            EndAnimation();
        }
        protected abstract T GetStart();
    }

    class ScaleAnimator : TransformAnimator<Vector3>
    {
        protected override Vector3 GetStart() => transform.localScale;
        protected override void UpdateProgress(float progress) => transform.localScale = Start + (Target - Start) * progress;
    }

    class RotationAnimator : TransformAnimator<float>
    {
        protected override float GetStart() => transform.localRotation.eulerAngles.z;
        protected override void UpdateProgress(float progress) => transform.localRotation = Quaternion.Lerp(Quaternion.Euler(0, 0, Start), Quaternion.Euler(0, 0, Target), progress);
    }
}