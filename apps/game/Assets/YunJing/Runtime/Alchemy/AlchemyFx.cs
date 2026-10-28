using UnityEngine;

namespace YunJing.Alchemy
{
    // Procedural placeholder audio and particles: no recorded or licensed assets exist in the project yet.
    public static class AlchemyFx
    {
        const int Rate = 22050;

        public static AudioClip Crackle()
        {
            var data = new float[Rate * 2]; var rng = new System.Random(7); float low = 0;
            for (int i = 0; i < data.Length; i++)
            {
                low = low * .96f + (float)(rng.NextDouble() * 2 - 1) * .04f;
                float pop = rng.NextDouble() < .0009 ? (float)(rng.NextDouble() * 2 - 1) : 0;
                data[i] = low * .8f + pop * .9f;
            }
            return Make("FireCrackle", data);
        }

        public static AudioClip Chime(float pitch = 1)
        {
            var data = new float[(int)(Rate * 1.6f)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate, env = Mathf.Exp(-t * 2.6f);
                data[i] = env * (.5f * Mathf.Sin(2 * Mathf.PI * 660 * pitch * t) + .3f * Mathf.Sin(2 * Mathf.PI * 990 * pitch * t) + .2f * Mathf.Sin(2 * Mathf.PI * 1320 * pitch * t));
            }
            return Make("Chime", data);
        }

        public static AudioClip Thud()
        {
            var data = new float[Rate / 3]; var rng = new System.Random(3);
            for (int i = 0; i < data.Length; i++) { float t = i / (float)Rate; data[i] = Mathf.Exp(-t * 18) * (Mathf.Sin(2 * Mathf.PI * 110 * t) * .7f + (float)(rng.NextDouble() - .5) * .3f); }
            return Make("Thud", data);
        }

        public static AudioClip Grind()
        {
            var data = new float[Rate / 4]; var rng = new System.Random(5);
            for (int i = 0; i < data.Length; i++) { float t = i / (float)Rate; data[i] = Mathf.Sin(Mathf.PI * t * 4) * (float)(rng.NextDouble() * 2 - 1) * .35f; }
            return Make("Grind", data);
        }

        static AudioClip Make(string name, float[] data) { var clip = AudioClip.Create(name, data.Length, 1, Rate, false); clip.SetData(data, 0); return clip; }

        // The material comes from a generated asset so the URP particle shader survives player-build stripping.
        public static ParticleSystem Particles(string name, Transform parent, Vector3 local, Color color, float size, float speed, float life, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.startColor = color; main.startSize = size; main.startSpeed = speed; main.startLifetime = life;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 300;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12; shape.radius = .05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(color.a, .2f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 1.8f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            var emission = ps.emission; emission.rateOverTime = 0;
            ps.Play();
            return ps;
        }
    }
}
