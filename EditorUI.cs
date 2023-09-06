using System;
using System.Collections.Generic;
using Digi.ParticleEditor.UIControls;
using Sandbox.Game.Gui;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Input;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender.Animations;

namespace Digi.ParticleEditor
{
    // Sources of SE particles:
    // MyParticleEffectData's properties, MyParticleGPUGenerationData.Start(), MyParticleLightData.Start()
    // https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/master/Sources/VRage.Render/Messages/MyRenderMessageUpdateGPUEmitters.cs#L33

    // TODO: print camera type on screen (as the RMB holding gets confusing xD) ... or better yet, handle spec cam swapping without RMB
    // TODO? add time slider at the bottom of screen to allow playback & pause... maybe even playback at different speeds

    // TODO: keep a log of all actions done per particle and write them as XML comments in the backup

    public partial class EditorUI : UIGamePassthroughScreenBase, IScreenAllowHotkeys
    {
        public static EditorUI Instance;

        public readonly ParticleHandler SelectedParticle = new ParticleHandler();

        public Dictionary<string, MyObjectBuilder_ParticleEffect> OriginalParticleData = new Dictionary<string, MyObjectBuilder_ParticleEffect>();

        VerticalControlsHost Host;

        public readonly Editor Editor;
        public readonly StatusUI StatusUI;

        public static readonly MyParticleEffect DefaultEffect = new MyParticleEffect();
        public static readonly MyParticleEffectData DefaultData = new MyParticleEffectData();
        public static readonly MyParticleGPUGenerationData DefaultEmitter = new MyParticleGPUGenerationData();
        public static readonly MyParticleLightData DefaultLight = new MyParticleLightData();

        public HashSet<IMyConstProperty> SkipProperties = new HashSet<IMyConstProperty>();

        public Vector2 TopLeftPos => -m_position + MyGuiManager.ComputeFullscreenGuiCoordinate(MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, 0, 0);
        public Vector2 ScreenSize => Size.Value;

        public event Action ActuallyClosed;

        public override string GetFriendlyName() => "Particle Editor GUI";

        public const string FileDialogFilterSBC = "SandboxContent file|*.sbc";

        public static readonly Vector2 ScreenPosition = new Vector2(0.5f, 0.5f);

        public static readonly Vector2 ButtonSize = new Vector2(0.06f, 0.03f);
        public static readonly Vector2 ItemSize = new Vector2(0.06f, 0.02f);

        public static readonly Vector4 ColorBg = new Color(41, 54, 62);
        public static readonly Vector4 ColorBgDarker = new Color(37, 46, 53);

        public static readonly MyStringId MaterialLaser = MyStringId.GetOrCompute("WeaponLaser");
        public static readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("Square");
        public static readonly MyStringId MaterialDot = MyStringId.GetOrCompute("WhiteDot");

        public const float EyeballedScrollbarWidth = 0.027f;

        public EditorUI(Editor editor) : base(ScreenPosition, null, null, isTopMostScreen: true)
        {
            base.CanBeHidden = false;
            base.CanHideOthers = false;
            m_canCloseInCloseAllScreenCalls = false;
            m_canShareInput = true;
            m_isTopScreen = true;

            Instance = this;
            Editor = editor;

            StatusUI = new StatusUI(this);

            EditorEmitters = new EditorEmitters(this);
            EditorLights = new EditorLights(this);
            EditorGeneral = new EditorGeneral(this);
            EditorDebug = new EditorDebug(this);

            Align = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER;

            DefaultData.Start(0, "");
            DefaultEmitter.Start(DefaultData);
            DefaultLight.Start(DefaultData);
        }

        public void Dispose()
        {
            SelectedParticle.Despawn();
            EditorDebug?.Dispose();

            if(DrawOnlySelected)
            {
                RestoreEnabled();
                DrawOnlySelected = false;
            }

            // restore original data, downsides doing it this way and to not doing it...
            //foreach(KeyValuePair<string, MyObjectBuilder_ParticleEffect> kv in OriginalParticleData)
            //{
            //    try
            //    {
            //        MyParticleEffectData data = new MyParticleEffectData();
            //        MyParticleEffectDataSerializer.DeserializeFromObjectBuilder(data, kv.Value);

            //        MyParticleEffectsLibrary.Remove(data.Name);
            //        MyParticleEffectsLibrary.Add(data);
            //    }
            //    catch(Exception e)
            //    {
            //        Log.Error(e);
            //    }
            //}

            OriginalParticleData.Clear();
        }

        public void Open()
        {
            //m_backgroundTransition = MySandboxGame.Config.UIBkOpacity; // BG opacity
            //m_guiTransition = MySandboxGame.Config.UIOpacity; // UI opacity

            Rectangle screenRect = MyGuiManager.GetSafeFullscreenRectangle();
            float aspectRatio = (float)screenRect.Width / (float)screenRect.Height;
            Size = new Vector2(aspectRatio * 3.0f / 4.0f, 1.0f); // HACK: seems to work for 4:3 and 16:9, same way MyGuiScreenDebugErrors does it.

            Host = new VerticalControlsHost(this, TopLeftPos + new Vector2(Size.Value.X, 0), new Vector2(0.4f, Size.Value.Y),
                originAlign: MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP, drawBackground: true);

            CanBeHidden = true;
            CanHideOthers = false;
            m_canCloseInCloseAllScreenCalls = true;
            m_canShareInput = true;
            m_isTopScreen = false;
            m_isTopMostScreen = false;

            FocusedControl = null;

            MyGuiScreenGamePlay.DisableInput = false;

            if(SelectedParticle.Name != null)
                SelectedParticle.Spawn(SelectedParticle.Name, false);

            RecreateControls(true);
        }

        public override void RecreateControls(bool constructor)
        {
            try
            {
                base.RecreateControls(constructor);
                Host.Reset();

                BackgroundColor = Vector4.Zero;

                StatusUI?.RecreateControls();

                // to verify Size
                //{
                //    var panel = new MyGuiControlParent();
                //    panel.Position = new Vector2(0, 0);
                //    panel.Size = Size.Value;
                //    panel.BorderColor = Color.Red;
                //    panel.BorderEnabled = true;
                //    panel.BorderSize = 1;
                //    Controls.Add(panel);
                //}

                if(!Controls_LoadParticle())
                    return;

                Host.InsertSeparator();

                if(SelectedParticle.Name != null)
                {
                    Controls_EditParticle();
                }
                else
                {
                    Host.PositionAndFillWidth(Host.CreateLabel("Create or select a particle"));
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void Close()
        {
            OnClose();
            CloseScreen();
        }

        void OnClose()
        {
            if(ScrollablePanel != null)
            {
                if(Tab == TabEnum.Emitters)
                    LastScrollEmitters = ScrollablePanel.ScrollbarVPosition;
                else if(Tab == TabEnum.Lights)
                    LastScrollLights = ScrollablePanel.ScrollbarVPosition;
            }

            SelectedParticle.Despawn();

            ActuallyClosed?.Invoke();
        }

        public override bool CloseScreen(bool isUnloading = false)
        {
            bool val = base.CloseScreen(isUnloading);
            OnClose();
            return val;
        }

        public override void CloseScreenNow(bool isUnloading = false)
        {
            base.CloseScreenNow(isUnloading);
            OnClose();
        }

        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            if(MyInput.Static.IsNewKeyPressed(MyKeys.Escape))
            {
                Close();
                return;
            }

            SelectedParticle.Update();

            // allow game control by holding RMB
            if(ComputeGameControlPassThrough(receivedFocusInThisUpdate, Host.Panel.Rectangle, StatusUI.Host.Panel.Rectangle))
                return;

            base.HandleInput(receivedFocusInThisUpdate);
        }

        //public override bool Draw()
        //{
        //    var ret = base.Draw();

        //    Notifications?.Draw();

        //    return ret;
        //}

        bool IScreenAllowHotkeys.IsAllowed(Vector2 mousePosition)
        {
            // mouse over right side GUI, ignore hotkeys
            if(Host.Panel.Rectangle.Contains(mousePosition))
                return false;

            return true;
        }

        public override bool Update(bool hasFocus)
        {
            StatusUI?.Update();

            if(ShowParticlePicker)
                UpdateLoadParticleUI();

            //if(Tab == TabEnum.General)
            //    EditorGeneral?.Update(); 

            if(Tab == TabEnum.Debug)
                EditorDebug?.Update();

            return base.Update(hasFocus);
        }

        public void RefreshUI()
        {
            RecreateControls(false);
        }

        bool _drawOnlySelected;
        public bool DrawOnlySelected
        {
            get => _drawOnlySelected;
            set
            {
                _drawOnlySelected = value;

                if(ButtonResetVis != null)
                    ButtonResetVis.Enabled = !_drawOnlySelected;
            }
        }

        public Dictionary<object, bool> StoredEnabled = new Dictionary<object, bool>();

        public void RefreshShowOnlySelected()
        {
            if(SelectedParticle.Data == null)
                return;

            if(DrawOnlySelected)
            {
                if(StoredEnabled.Count == 0)
                {
                    foreach(MyParticleLightData light in SelectedParticle.Data.GetParticleLights())
                    {
                        StoredEnabled.Add(light, light.Enabled.GetValue());
                    }

                    foreach(MyParticleGPUGenerationData emitter in SelectedParticle.Data.GetGenerations())
                    {
                        StoredEnabled.Add(emitter, emitter.Enabled.GetValue());
                    }
                }

                foreach(MyParticleGPUGenerationData emitter in SelectedParticle.Data.GetGenerations())
                {
                    emitter.Enabled.SetValue(Tab == TabEnum.Emitters && emitter == EditorEmitters?.SelectedEmitter);
                }

                foreach(MyParticleLightData light in SelectedParticle.Data.GetParticleLights())
                {
                    light.Enabled.SetValue(Tab == TabEnum.Lights && light == EditorLights?.SelectedLight);
                }

                EditorEmitters.RefreshEmitterList();
                EditorLights.RefreshLightsList();

                SelectedParticle.Refresh();
            }
            else
            {
                RestoreEnabled();
            }

            RefreshButtonsStatus();
        }

        public void RestoreEnabled()
        {
            if(StoredEnabled.Count > 0)
            {
                foreach(KeyValuePair<object, bool> kv in StoredEnabled)
                {
                    if(kv.Key is MyParticleGPUGenerationData emitter)
                        emitter.Enabled.SetValue(kv.Value);
                    else if(kv.Key is MyParticleLightData light)
                        light.Enabled.SetValue(kv.Value);
                }

                StoredEnabled.Clear();
            }
        }

        public MyGuiControlButton ButtonFullReset;
        public MyGuiControlButton ButtonResetVis;

        public void RefreshButtonsStatus()
        {
            if(SelectedParticle.Name == null)
                return;

            ButtonFullReset.Enabled = false;

            MyObjectBuilder_ParticleEffect effectOB = OriginalParticleData.GetValueOrDefault(SelectedParticle.Name);
            if(effectOB != null)
            {
                if(Tab == TabEnum.Emitters && EditorEmitters?.SelectedEmitter != null)
                {
                    ParticleGeneration emitterOB = effectOB.ParticleGenerations.Find(e => e.Name == EditorEmitters.SelectedEmitter.Name);
                    if(emitterOB != null)
                        ButtonFullReset.Enabled = true;
                }

                if(Tab == TabEnum.Lights && EditorLights?.SelectedLight != null)
                {
                    ParticleLight lightOB = effectOB.ParticleLights.Find(e => e.Name == EditorLights.SelectedLight.Name);
                    if(lightOB != null)
                        ButtonFullReset.Enabled = true;
                }
            }
        }
    }
}
