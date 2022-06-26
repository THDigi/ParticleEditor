using System;
using VRage.Plugins;

namespace Digi.ParticleEditor
{
    public class ParticleEditorPlugin : IPlugin
    {
        Editor Editor;

        public override string ToString() => "Particle Editor Plugin";

        public void Init(object gameInstance)
        {
            Log.Name = "ParticleEditorPlugin";

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
