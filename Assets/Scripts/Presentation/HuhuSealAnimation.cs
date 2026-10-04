using UnityEngine;

namespace SealGugu
{
    /// <summary>Editable clips, sampled on the game/song clock. Never owns judgement or movement timing.</summary>
    [ExecuteAlways, RequireComponent(typeof(Animator))]
    public sealed class HuhuSealAnimation : MonoBehaviour
    {
        [Header("Animation clip output (edit the clips in Animation window)")]
        public float bodyScale=1, roll, bob, frame;
        [Header("Breathing deformation")]
        [Range(0,.5f)] public float bellyExpansion=.32f;
        [Header("Instant input feedback")]
        [Range(0,.2f)] public float inputPulse=.018f;
        public string CurrentState { get; private set; }
        Animator animator;
        public void Sample(string state,double seconds,bool loop=true)
        {
            if(!animator)animator=GetComponent<Animator>();
            if(!animator||!animator.runtimeAnimatorController)return;
            animator.speed=0;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            float length=1;
            foreach(var clip in animator.runtimeAnimatorController.animationClips)if(clip.name==state){length=Mathf.Max(.001f,clip.length);break;}
            float t=(float)(seconds/length);t=loop?t-Mathf.Floor(t):Mathf.Clamp(t,0,.99999f);
            animator.Play(state,0,t);animator.Update(0);CurrentState=state;
        }
    }
}
