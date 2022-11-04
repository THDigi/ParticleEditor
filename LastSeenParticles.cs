using System.Collections.Generic;
using Sandbox.Game.World;
using VRage.Game;

namespace Digi.ParticleEditor
{
    public class LastSeenParticles : EditorComponentBase
    {
        public readonly Dictionary<string, int> Recent = new Dictionary<string, int>();

        public LastSeenParticles(Editor editor) : base(editor)
        {
            AlwaysUpdate = true;
            MySession.AfterLoading += SessionLoaded;
        }

        public override void Dispose()
        {
            MySession.AfterLoading -= SessionLoaded;
        }

        void SessionLoaded()
        {
            Recent.Clear();
        }

        public override void Update()
        {
            int tick = MySession.Static.GameplayFrameCounter;
            foreach(MyParticleEffect effect in MyParticlesManager.Effects)
            {
                Recent[effect.Data.Name] = tick;
            }
        }
    }
}
