using UnityEngine;
namespace OldGames.SpaceCadet
{
    public sealed class SpaceCadetSpinner : MonoBehaviour
    {
        public Transform blade;
        float speed;
        public void Spin(float impulse) { speed=Mathf.Clamp(speed+impulse*150,0,1600); }
        void Update()
        {
            if(blade==null)return;
            blade.Rotate(Vector3.right,speed*Time.deltaTime,Space.Self);
            speed=Mathf.MoveTowards(speed,0,Time.deltaTime*230);
        }
    }
}
