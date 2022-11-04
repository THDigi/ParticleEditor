using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Messages;

namespace Digi.ParticleEditor
{
    public class LastSeenParticles : EditorComponentBase
    {
        public bool ShowLiveNames { get; set; } = false;

        public readonly Dictionary<string, int> Recent = new Dictionary<string, int>();

        FieldInfo Field_State;
        StringBuilder SB = new StringBuilder(512);

        public LastSeenParticles(Editor editor) : base(editor)
        {
            AlwaysUpdate = true;
            MySession.AfterLoading += SessionLoaded;

            try
            {
                Field_State = typeof(MyParticleEffect).GetField("m_state", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
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
            Vector3D cameraPos = MySector.MainCamera.Position;

            foreach(MyParticleEffect effect in MyParticlesManager.Effects)
            {
                if(effect.IsEmittingStopped || effect.IsStopped)
                    continue; // prob not needed but can't hurt

                Recent[effect.Data.Name] = tick;

                if(ShowLiveNames && Field_State != null)
                {
                    Vector3D worldPos = effect.WorldMatrix.Translation;
                    MyParticleEffectState state = (MyParticleEffectState)Field_State.GetValue(effect);
                    uint parentId = state.ParentID;
                    double distance = 0;

                    SB.Clear();
                    SB.Append(effect.GetName()).Append('\n');

                    if(parentId != uint.MaxValue)
                    {
                        MyEntity parent = MyEntities.GetEntityFromRenderObjectID(parentId) as MyEntity;
                        if(parent == null)
                        {
                            SB.Append("(RenderID not found=").Append(parentId).Append(")");
                        }
                        else
                        {
                            worldPos = Vector3D.Transform(worldPos, parent.WorldMatrix);
                            distance = Vector3D.Distance(cameraPos, worldPos);
                        }
                    }
                    else
                    {
                        distance = Vector3D.Distance(cameraPos, worldPos);
                    }

                    float textScale = 0.6f;

                    if(distance > 5)
                    {
                        textScale = 0.5f;

                        if(distance > 1000)
                            SB.Append(Math.Round(distance / 1000, 2)).Append(" km");
                        else
                            SB.Append(Math.Round(distance, 2)).Append(" m");
                    }

                    Color color;

                    if(distance <= 50)
                        color = Color.Lerp(Color.SkyBlue, Color.Yellow, (float)(distance / 50));
                    else if(distance <= 100)
                        color = Color.Lerp(Color.Yellow, Color.OrangeRed, (float)((distance - 50) / 50));
                    else
                        color = Color.OrangeRed;

                    MyRenderProxy.DebugDrawText3D(worldPos, SB.ToString(), color, textScale, false, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                }
            }
        }
    }
}
