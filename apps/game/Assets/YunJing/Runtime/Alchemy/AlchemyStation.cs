using UnityEngine;

namespace YunJing.Alchemy
{
    // Interaction point placed on a B-series prop by AlchemyRoomBuilder; the prop front faces -Z local.
    public sealed class AlchemyStation : MonoBehaviour
    {
        public string Id, Label, Verb; public float Radius = 1.25f;
        public Vector3 Point => transform.position;
    }
}
