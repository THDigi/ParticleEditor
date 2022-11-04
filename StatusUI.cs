using System;
using System.Collections;
using System.Reflection;
using Digi.ParticleEditor.GameData;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Input;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.ParticleEditor
{
    public class StatusUI
    {
        readonly EditorUI EditorUI;

        public VerticalControlsHost Host;

        bool DrawPivot = true;
        bool DrawEmitters = false;
        bool DrawLights = false;

        float? GravityOverride = null;

        bool ShowHUD = false;

        ParticleHandler SelectedParticle => EditorUI.SelectedParticle;

        const string ElapsedPrefix = "Elapsed: ";
        const string LoopPrefix = "Loop: ";

        MyGuiControlLabel LabelElapsedTime;
        MyGuiControlLabel LabelLoopTime;

        MyGuiControlCheckbox CbParentParticle;

        public StatusUI(EditorUI editorUI)
        {
            EditorUI = editorUI;

            SelectedParticle.Changed += SelectedParticle_Changed;
        }

        public void RecreateControls()
        {
            MyHud.MinimalHud = !ShowHUD;

            Host = new VerticalControlsHost(EditorUI, EditorUI.TopLeftPos, new Vector2(0.3f, 0.3f), drawBackground: true);

            Host.InsertSlider("Particle placement distance",
                "Affects your preview of the particle, nothing on the particle itself.", 0f, 50f, SelectedParticle.ParentDistance, 10f, 1,
                (value) =>
                {
                    SelectedParticle.ParentDistance = value;
                });

            Host.InsertSlider("Gravity override",
                "Override gravity acceleration for particles only, relative to character's down.", -1f, 9.81f, (GravityOverride.HasValue ? GravityOverride.Value : -1), -1f, 2,
                (value) =>
                {
                    if(value < -0.5f)
                        GravityOverride = null;
                    else
                        GravityOverride = Math.Max(0, value);
                },
                valueWriter: (value) =>
                {
                    return (value < -0.5f ? "OFF" : Math.Max(0, value).ToString());
                });

            (MyGuiControlParent cbShowHud, _, _) = Host.InsertCheckbox("Show HUD",
                "Toggles if the game HUD is shown while this editor is open.",
                ShowHUD, (value) =>
                {
                    ShowHUD = value;
                    MyHud.MinimalHud = !ShowHUD;
                });

            Host.UndoLastVerticalShift();

            (MyGuiControlParent cbShowParticleNames, _, _) = Host.InsertCheckbox("Other particle names",
                "Show names of particles that you see in world." +
                "\nHint: you can pause the game if they vanish too quickly" +
                "\n        and use spectator camera to look around freely during pause too.",
                EditorUI.Editor.LastSeenParticles.ShowLiveNames, (value) =>
                {
                    EditorUI.Editor.LastSeenParticles.ShowLiveNames = !EditorUI.Editor.LastSeenParticles.ShowLiveNames;
                });

            Host.UndoLastVerticalShift();

            Host.PositionControlsNoSize(cbShowHud, cbShowParticleNames);

            (_, CbParentParticle, _) = Host.InsertCheckbox("Particle parented",
                "Toggles particle parenting to character." +
                "\nAffects some things, like gravity awareness in lights." +
                "\nHotkey: R",
                SelectedParticle.UseParent, (value) =>
                {
                    SelectedParticle.UseParent = value;
                });

            (MyGuiControlParent cbPivot, _, _) = Host.InsertCheckbox("Pivot",
                "Draw the red/green/blue lines that indicate effect origin and orientation." +
                "\nThe axis point towards the positive values of X/Y/Z, blue pointing back because -Z is forward in SE.",
                DrawPivot, (value) =>
                {
                    DrawPivot = value;
                });

            Host.UndoLastVerticalShift();

            (MyGuiControlParent cbEmitters, _, _) = Host.InsertCheckbox("Emitters",
                "Draw emitters' approximate position." +
                "\nDoes not read it from render so it does not have the random variance.",
                DrawEmitters, (value) =>
                {
                    DrawEmitters = value;
                });

            Host.UndoLastVerticalShift();

            (MyGuiControlParent cbLights, _, _) = Host.InsertCheckbox("Lights",
                "Draw lights' approximate position." +
                "\nDoes not read it from render so it does not have the random variance.",
                DrawLights, (value) =>
                {
                    DrawLights = value;
                });

            Host.UndoLastVerticalShift();

            Host.PositionControlsNoSize(cbPivot, cbEmitters, cbLights);

            LabelElapsedTime = Host.CreateLabel($"{ElapsedPrefix} N/A       ");
            LabelLoopTime = Host.CreateLabel($"{LoopPrefix} N/A       ");

            Host.PositionControlsNoSize(LabelElapsedTime, LabelLoopTime);

            Host.ResizeY();

            // TODO: toggle sunlight?
            // TODO: offer to spawn the particle multiple times to debug performance?
        }

        public void Update()
        {
            if(SelectedParticle.SpawnedEffect != null)
            {
                bool canReadInputs = EditorUI.CanReadInputs();

                if(canReadInputs && MyInput.Static.IsNewKeyPressed(MyKeys.F))
                {
                    SelectedParticle.Refresh(false);
                }

                if(canReadInputs && MyInput.Static.IsNewKeyPressed(MyKeys.R))
                {
                    SelectedParticle.UseParent = !SelectedParticle.UseParent;

                    CbParentParticle.IsChecked = SelectedParticle.UseParent; // update in GUI
                }


                //var focusScreen = MyScreenManager.GetScreenWithFocus();
                //MyRenderProxy.DebugDrawText2D(new Vector2(400, 10), $"focusScreen: {focusScreen}; canReadInputs={canReadInputs}", new Color(255, 0, 255), 0.75f);


                // TODO distance checking, not worky
                //if(!Particle.Data.Loop)
                //{
                //    Vector3D particlePos = Particle.WorldMatrix.Translation;
                //    if(ParentParticle)
                //        particlePos = Vector3D.Transform(particlePos, character.WorldMatrix);
                //
                //    if(Vector3D.DistanceSquared(particlePos, MyAPIGateway.Session.Camera.Position) > (Particle.Data.DistanceMax * Particle.Data.DistanceMax))
                //    {
                //        MyAPIGateway.Utilities.ShowNotification("[Warning: Particle would not spawn at this range]", 17, MyFontEnum.Debug);
                //    }
                //}

                if(DrawPivot)
                {
                    const float LineLength = 1f;
                    const float LineThick = 0.01f;
                    const float TextScale = 0.6f;
                    const float PointRadius = 0.05f;
                    const MyBillboard.BlendTypeEnum Blend = MyBillboard.BlendTypeEnum.PostPP;

                    MatrixD localMatrix = SelectedParticle.SpawnedEffect.WorldMatrix; // NOTE: this is local if particle is parented
                    MatrixD worldMatrix = localMatrix;

                    MyCharacter character = MySession.Static.LocalCharacter;
                    uint parentId = VersionSpecificInfo.NoParentId;

                    if(character != null && SelectedParticle.UseParent)
                    {
                        parentId = character.Render.GetRenderObjectID();
                        worldMatrix *= character.WorldMatrix;
                    }

                    float alpha = 1f;
                    float distance = (float)Vector3D.Distance(worldMatrix.Translation, MySector.MainCamera.Position);
                    distance = Math.Max(0, distance - 0.5f);
                    if(distance <= 1f)
                        alpha = distance;

                    if(alpha > 0)
                    {
                        MyTransparentGeometry.AddLocalLineBillboard(EditorUI.MaterialLaser, Color.Red * alpha, localMatrix.Translation, parentId, localMatrix.Right, LineLength, LineThick, Blend);
                        MyTransparentGeometry.AddLocalLineBillboard(EditorUI.MaterialLaser, Color.Lime * alpha, localMatrix.Translation, parentId, localMatrix.Up, LineLength, LineThick, Blend);
                        MyTransparentGeometry.AddLocalLineBillboard(EditorUI.MaterialLaser, Color.Blue * alpha, localMatrix.Translation, parentId, localMatrix.Backward, LineLength, LineThick, Blend);
                    }

                    if(DrawEmitters || DrawLights)
                    {
                        MyRenderProxy.DebugDrawText3D(worldMatrix.Translation, "Pivot", Color.White, TextScale, true, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                    }

                    if(DrawEmitters)
                    {
                        foreach(MyParticleGPUGenerationData emitter in SelectedParticle.Data.GetGenerations())
                        {
                            MatrixD m = MatrixD.CreateTranslation(emitter.Offset.GetValue());

                            m *= worldMatrix;

                            MyTransparentGeometry.AddPointBillboard(EditorUI.MaterialDot, new Color(5, 25, 45), m.Translation, PointRadius, 0, blendType: Blend);
                            MyRenderProxy.DebugDrawText3D(m.Translation, emitter.Name, Color.SkyBlue, TextScale, true, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                        }
                    }

                    if(DrawLights)
                    {
                        foreach(MyParticleLightData light in SelectedParticle.Data.GetParticleLights())
                        {
                            light.Position.GetInterpolatedValue(GetElapsedTime(), out Vector3 pos);

                            MatrixD m = MatrixD.CreateTranslation(pos);

                            m *= worldMatrix;

                            MyTransparentGeometry.AddPointBillboard(EditorUI.MaterialDot, new Color(45, 25, 5), m.Translation, PointRadius, 0, blendType: Blend);
                            MyRenderProxy.DebugDrawText3D(m.Translation, light.Name, new Color(255, 175, 30), TextScale, true, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                        }
                    }
                }
            }

            if(LabelElapsedTime != null)
            {
                LabelElapsedTime.Text = $"{ElapsedPrefix} N/A";
                LabelLoopTime.Text = $"{LoopPrefix} N/A";

                if(SelectedParticle.SpawnedEffect != null)
                {
                    // TODO: cache this or something...
                    ReflectOnMistakes();
                    //GetGetters();

                    if(GetElapsedTime != null)
                        LabelElapsedTime.Text = $"{ElapsedPrefix} {GetElapsedTime.Invoke():0.###}";

                    if(GetLoopTime != null)
                        LabelLoopTime.Text = $"{LoopPrefix} {GetLoopTime.Invoke():0.###}";
                }
            }
        }

        void SelectedParticle_Changed(MyParticleEffectData oldParticle, MyParticleEffectData newParticle)
        {
            ReflectOnMistakes();
            //GetGetters();
        }

        Func<float> GetElapsedTime;
        Func<float> GetLoopTime;

        void ReflectOnMistakes()
        {
            try
            {
                Assembly VRageRenderAssembly = typeof(MyDX11Render).Assembly;

                FieldInfo effectsField = VRageRenderAssembly.GetType("VRage.Render11.Particles.MyRenderParticlesManager")
                                         ?.GetField("m_effects", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);

                object managerInstance = VRageRenderAssembly.GetType("VRage.Render11.Common.MyManagers")
                                         ?.GetField("ParticleEffectsManager", BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField)
                                         ?.GetValue(null);

                Type effectType = typeof(MyDX11Render)?.Assembly?.GetType("VRage.Render11.Particles.MyRenderParticleEffect");
                FieldInfo dataField = effectType?.GetField("m_data", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);

                if(effectsField == null || dataField == null)
                    return;

                IDictionary dict = effectsField?.GetValue(managerInstance) as IDictionary;
                if(dict == null || dataField == null)
                    return;

                // FIXME: dictionary can change while this iterates it, any way to threadsafe it?

                object theEffectInstance = null;
                foreach(object effectInstance in dict.Values)
                {
                    object data = dataField.GetValue(effectInstance);
                    if(object.ReferenceEquals(data, SelectedParticle.Data))
                    {
                        theEffectInstance = effectInstance;
                        break;
                    }
                }

                if(theEffectInstance == null)
                    return;

                GetElapsedTime = effectType?.GetMethod("GetElapsedTime", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod)?.CreateDelegate<Func<float>>(theEffectInstance);
                GetLoopTime = effectType?.GetMethod("GetLoopTime", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod)?.CreateDelegate<Func<float>>(theEffectInstance);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        //void GetGetters()
        //{
        //    try
        //    {
        //        Assembly VRageRenderAssembly = typeof(MyDX11Render).Assembly;

        //        MethodInfo getByIdMethod = VRageRenderAssembly.GetType("VRage.Render11.Particles.MyRenderParticlesManager")
        //                                 ?.GetMethod("Get", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);

        //        object managerInstance = VRageRenderAssembly.GetType("VRage.Render11.Common.MyManagers")
        //                                 ?.GetField("ParticleEffectsManager", BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField)
        //                                 ?.GetValue(null);

        //        Type effectType = typeof(MyDX11Render)?.Assembly?.GetType("VRage.Render11.Particles.MyRenderParticleEffect");

        //        if(getByIdMethod == null || managerInstance == null || effectType == null)
        //            return;

        //        object renderParticleEffectInstance = getByIdMethod.Invoke(managerInstance, new object[] { SelectedParticle.SpawnedEffect.Id });

        //        if(renderParticleEffectInstance == null)
        //            return;

        //        GetElapsedTime = effectType?.GetMethod("GetElapsedTime", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod)?.CreateDelegate<Func<float>>(renderParticleEffectInstance);
        //        GetLoopTime = effectType?.GetMethod("GetLoopTime", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod)?.CreateDelegate<Func<float>>(renderParticleEffectInstance);
        //    }
        //    catch(Exception e)
        //    {
        //        Log.Error(e);
        //    }
        //}

        public Vector3? RenderGravityOverride(Vector3D point)
        {
            if(GravityOverride.HasValue)
            {
                MyCharacter chr = MySession.Static?.LocalCharacter;
                if(chr != null)
                    return chr.WorldMatrix.Down * GravityOverride.Value;
            }

            return null; // do not override
        }





    }
}
