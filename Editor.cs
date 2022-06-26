using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRageMath;
using VRageRender;

namespace Digi.ParticleEditor
{
    // FIXME: world reload causes particle to not refresh on changes nor on F key

    public class Editor
    {
        bool _showEditor = false;
        public bool ShowEditor
        {
            get => _showEditor;
            private set
            {
                _showEditor = value;
                EditorVisibleChanged?.Invoke(value);
            }
        }

        public event Action<bool> EditorVisibleChanged;

        public readonly EditorUI EditorUI;

        int OriginalHUDState;

        public Backup Backup => (Backup)Components[typeof(Backup)];
        public LastSeenParticles LastSeenParticles => (LastSeenParticles)Components[typeof(LastSeenParticles)];

        public readonly Dictionary<Type, EditorComponentBase> Components = new Dictionary<Type, EditorComponentBase>();

        public Editor()
        {
            EditorUI = new EditorUI(this);
            EditorUI.ActuallyClosed += EditorUI_Closed;

            LoadComponents();

            MyRenderProxy.SetGravityProvider(RenderGravityOverride);

            // TODO: reset vanilla particles on world unload
        }

        public void Dispose()
        {
            foreach(EditorComponentBase comp in Components.Values)
            {
                comp.Dispose();
            }

            MyRenderProxy.SetGravityProvider(MyGravityProviderSystem.CalculateTotalGravityInPoint);

            EditorUI?.Dispose();

            if(ShowEditor)
            {
                CloseEditor();
            }
        }

        void LoadComponents()
        {
            foreach(Type type in GetType().Assembly.GetTypes())
            {
                if(type.IsAbstract || !type.IsClass)
                    continue;

                if(typeof(EditorComponentBase).IsAssignableFrom(type))
                {
                    //MyLog.Default.WriteLine($"ParticleEditor: Added component '{type.Name}'");

                    if(Components.ContainsKey(type))
                    {
                        Log.Error($"Component '{type.Name}' is already added to components list!");
                        continue;
                    }

                    EditorComponentBase comp = (EditorComponentBase)Activator.CreateInstance(type, new[] { this });
                    Components.Add(type, comp);

                    //FieldInfo field = GetType().GetField(type.Name, BindingFlags.SetField | BindingFlags.Instance | BindingFlags.Public);
                    //if(field != null)
                    //{
                    //    field.SetValue(this, comp);
                    //}
                }
            }
        }

        Vector3 RenderGravityOverride(Vector3D point)
        {
            return EditorUI?.StatusUI?.RenderGravityOverride(point) ?? MyGravityProviderSystem.CalculateTotalGravityInPoint(point);
        }

        public void Update()
        {
            if(MySector.MainCamera == null || MyInput.Static == null || MySession.Static == null)
            {
                if(ShowEditor)
                    CloseEditor();

                return;
            }

            if(!MySession.Static.IsServer || MySession.Static.OnlineMode != MyOnlineModeEnum.OFFLINE)
            {
                if(!MySandboxGame.Static.IsCursorVisible && MyInput.Static.IsNewKeyPressed(MyKeys.F) && MyInput.Static.IsAnyShiftKeyPressed())
                    MyAPIGateway.Utilities.ShowNotification("Partile Editor only allowed in offline worlds", 3000, MyFontEnum.Red);

                if(ShowEditor)
                    CloseEditor();

                return;
            }

            if(!MySandboxGame.Static.IsCursorVisible && MyInput.Static.IsNewKeyPressed(MyKeys.F) && MyInput.Static.IsAnyShiftKeyPressed())
            {
                if(!ShowEditor)
                    OpenEditor();
                //else
                //    CloseEditor();
            }

            if(ShowEditor)
            {
                // prevent goodbot and other stuff from showing up...
                MyHud.Questlog.Visible = false;

                foreach(EditorComponentBase comp in Components.Values)
                {
                    comp.Update();
                }

                //EditorUI.Update();
            }
        }

        bool shownInvulWarn = false;

        void OpenEditor()
        {
            if(ShowEditor)
                return;

            OriginalHUDState = MyHud.HudState;

            if(!shownInvulWarn && MySession.Static.SurvivalMode && (MySession.Static.AdminSettings & AdminSettingsEnum.Invulnerable) == 0)
            {
                shownInvulWarn = true;
                Notifications.Show("You are not invulnerable, enable that in admin menu (Alt+F10)", 5, Color.Yellow);

                // no easy way to set it properly like admin menu does, with sync and all that.
                //Notifications.Show("Made you invulnerable to avoid unpleasantries. Turn off from admin menu (Alt+F10)");
                //MySession.Static.AdminSettings |= AdminSettingsEnum.Invulnerable;
            }

            EditorUI.Open();

            MyGuiSandbox.AddScreen(EditorUI);
            ShowEditor = true;
        }

        void EditorUI_Closed()
        {
            MyHud.SetHudState(OriginalHUDState);

            ShowEditor = false;
        }

        void CloseEditor()
        {
            if(!ShowEditor)
                return;

            ShowEditor = false;

            if(EditorUI != null)
            {
                EditorUI.Close();
                MyGuiSandbox.RemoveScreen(EditorUI);
            }
        }
    }
}
