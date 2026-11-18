using UnityEngine;
namespace OldGames.SpaceCadet
{
    public sealed class SpaceCadetTarget : MonoBehaviour
    {
        public SpaceCadetGame game;
        public Transform visual;
        public bool dropTarget;
        public string bank;
        public bool hit;
        Vector3 home;
        float raisedAt;
        void Awake() { if(visual!=null)home=visual.localPosition; }
        public bool Register()
        {
            if(hit)return false;
            hit=true;raisedAt=Time.time+.18f;return true;
        }
        public void ResetTarget() { hit=false; }
        void Update()
        {
            if(visual==null)return;
            Vector3 target=home;
            if(hit&&dropTarget)target.y-=.42f;
            else if(Time.time<raisedAt)target.z+=.04f;
            visual.localPosition=Vector3.MoveTowards(visual.localPosition,target,Time.deltaTime*4);
        }
    }
}
