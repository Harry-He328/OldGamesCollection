using UnityEngine;
namespace OldGames.SpaceCadet
{
    public enum TableZoneKind { Lane, RampEntry, RampExit, Wormhole, BlackHole, Hyperspace, Drain }
    public sealed class SpaceCadetZone : MonoBehaviour
    {
        public SpaceCadetGame game;
        public TableZoneKind kind;
        public string id;
        public int points=500;
        public Transform destination;
        public Vector3 ejectVelocity=new Vector3(0,.8f,9);
        public SpaceCadetZone pairedEntry;
        float nextEntry;
        void OnTriggerEnter(Collider other)
        {
            if(game==null || other.attachedRigidbody!=game.ball || Time.time<nextEntry)return;
            nextEntry=Time.time+.8f;
            game.Enter(this);
        }
        public void Suppress(float seconds) { nextEntry=Time.time+seconds; }
    }
}
