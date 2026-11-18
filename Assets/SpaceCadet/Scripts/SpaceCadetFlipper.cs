using UnityEngine;
namespace OldGames.SpaceCadet
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SpaceCadetFlipper : MonoBehaviour
    {
        public bool left;
        public float restAngle, raisedAngle, length=1.15f;
        public SpaceCadetGame game;
        Rigidbody body;
        bool held, previousHeld;
        int stroke;
        int lastAssistedStroke=-1;
        float angularSpeed;
        void Awake() { body=GetComponent<Rigidbody>(); }
        public void Drive(bool pressed,float dt)
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body == null) return;
            held=pressed;
            if(held&&!previousHeld)stroke++;
            previousHeld=held;
            float current=body.rotation.eulerAngles.y;
            float target=held?raisedAngle:restAngle;
            float next=Mathf.MoveTowardsAngle(current,target,(held?1000:650)*dt);
            angularSpeed=Mathf.DeltaAngle(current,next)*Mathf.Deg2Rad/dt;
            body.MoveRotation(Quaternion.Euler(0,next,0));
        }
        void OnCollisionEnter(Collision hit) { Strike(hit); }
        void OnCollisionStay(Collision hit) { Strike(hit); }
        void Strike(Collision hit)
        {
            if(game==null || hit.rigidbody!=game.ball || !held || lastAssistedStroke==stroke || Mathf.Abs(angularSpeed)<1) return;
            Vector3 offset=hit.GetContact(0).point-body.worldCenterOfMass;
            Vector3 velocity=Vector3.Cross(Vector3.up*angularSpeed,offset);
            velocity.y=0;
            if(velocity.z<=0)return;
            // Only assist an actual moving-flipper contact, once per stroke.
            float desired=Mathf.Clamp(velocity.magnitude*.9f,7,17);
            Vector3 direction=(velocity.normalized+Vector3.forward*.7f).normalized;
            float relative=Vector3.Dot(game.ball.linearVelocity,direction);
            if(relative<desired)game.ball.AddForce(direction*(desired-relative),ForceMode.VelocityChange);
            lastAssistedStroke=stroke;
        }
    }
}
