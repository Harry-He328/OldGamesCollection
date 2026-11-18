using UnityEngine;
namespace OldGames.SpaceCadet
{
    public sealed class SpaceCadetContact : MonoBehaviour
    {
        public SpaceCadetGame game;
        public int points;
        public float kick;
        public string label;
        public SpaceCadetTarget target;
        public SpaceCadetSpinner spinner;
        void OnCollisionEnter(Collision hit)
        {
            if(game!=null && hit.rigidbody==game.ball)game.Hit(this,hit);
        }
    }
}
