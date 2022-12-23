using System;
using VRage.Plugins;
using VRage.Utils;

namespace Digi.ParticleEditor
{
    public class ParticleEditorPlugin : IPlugin
    {
        public static readonly Version Version = new Version(1, 0, 3);

        Editor Editor;

        public override string ToString() => $"Particle Editor Plugin v{Version.ToString()}";

        public void Init(object gameInstance)
        {
            Log.Name = "ParticleEditorPlugin";
            MyLog.Default.WriteLine($"{this.ToString()} loaded.");

            try
            {
                Editor = new Editor();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void Dispose()
        {
            try
            {
                Editor?.Dispose();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void Update()
        {
            try
            {
                Editor?.Update();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
