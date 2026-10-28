using UnityEngine;

namespace YunJing.Alchemy
{
    /// Wires the explore camera to the player and applies command-line options for local runs.
    public sealed class AlchemyBootstrap : MonoBehaviour
    {
        public Transform Target;
        void Awake()
        {
            var api = GetComponent<GameApi>(); var url = AlchemyPlaytest.Arg("-api"); if (api != null && url != null) api.BaseUrl = url;
            var alias = AlchemyPlaytest.Arg("-alchemyAlias"); var ws = GetComponent<AlchemyWorkshop>(); if (alias != null && ws != null) ws.Alias = alias;
        }
        void Start() { var orbit = Camera.main != null ? Camera.main.GetComponent<QingxiOrbitCamera>() : null; if (orbit != null) orbit.SetTarget(Target); }
    }
}
