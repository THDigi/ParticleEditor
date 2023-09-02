using System;
using System.IO;
using System.Windows.Forms;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Render.Particles;
using VRageMath;

namespace Digi.ParticleEditor
{
    public partial class EditorUI
    {
        enum TabEnum { Emitters, Lights, General, Debug }

        TabEnum Tab;

        public VerticalControlsHost ScrollHost;
        public MyGuiControlScrollablePanel ScrollablePanel;

        public readonly EditorLights EditorLights;
        public readonly EditorEmitters EditorEmitters;
        public readonly EditorGeneral EditorGeneral;
        public readonly EditorDebug EditorDebug;

        float LastScrollEmitters = 0f;
        float LastScrollLights = 0f;
        bool IgnoreScrollEvent;

        MyGuiControlButton.StyleDefinition ButtonStyle_TabSelected = new MyGuiControlButton.StyleDefinition
        {
            NormalTexture = MyGuiConstants.TEXTURE_RECTANGLE_BUTTON_ACTIVE_BORDER,
            HighlightTexture = MyGuiConstants.TEXTURE_RECTANGLE_BUTTON_FOCUS_BORDER,
            FocusTexture = MyGuiConstants.TEXTURE_RECTANGLE_BUTTON_ACTIVE_BORDER,
            ActiveTexture = MyGuiConstants.TEXTURE_RECTANGLE_BUTTON_ACTIVE_BORDER,
            NormalFont = "White",
            HighlightFont = "White",
            Padding = new MyGuiBorderThickness(5f / MyGuiConstants.GUI_OPTIMAL_SIZE.X, 5f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y),
            TextColorFocus = Color.White,
            BackgroundColor = Color.White,
        };

        void Controls_EditParticle()
        {
            MyGuiControlLabel tabsLabel = Host.CreateLabel("Tabs:");

            MyGuiControlButton buttonEmitters = Host.CreateButton("Emitters", "Show list of emitters", clicked: (c) =>
            {
                Tab = TabEnum.Emitters;
                RefreshShowOnlySelected();
                RefreshUI();
            });

            MyGuiControlButton buttonLights = Host.CreateButton("Lights", "Show list of lights", clicked: (c) =>
            {
                Tab = TabEnum.Lights;
                RefreshShowOnlySelected();
                RefreshUI();
            });

            MyGuiControlButton buttonGeneral = Host.CreateButton("General", "Edit general properties of the particle", clicked: (c) =>
            {
                Tab = TabEnum.General;
                RefreshUI();
            });

            MyGuiControlButton buttonDebug = Host.CreateButton("Debug", "Global settings for debugging/experimenting", clicked: (c) =>
            {
                Tab = TabEnum.Debug;
                RefreshUI();
            });

            //float availableWidth = tabsLabel.Size.X - (Host.ControlSpacing * 4) - (Host.Padding.X * 2);
            //float tabButtonWidth = availableWidth / 4f;

            //buttonEmitters.Size = new Vector2(tabButtonWidth, buttonEmitters.Size.Y);
            //buttonLights.Size = new Vector2(tabButtonWidth, buttonLights.Size.Y);
            //buttonGeneral.Size = new Vector2(tabButtonWidth, buttonGeneral.Size.Y);
            //buttonDebug.Size = new Vector2(tabButtonWidth, buttonDebug.Size.Y);

            //Host.PositionControlsNoSize(tabsLabel, buttonEmitters, buttonLights, buttonGeneral, buttonDebug);

            Host.PositionControls(tabsLabel, buttonEmitters, buttonLights, buttonGeneral, buttonDebug);
            Host.InsertSeparator();


            Vector2 size = new Vector2(Host.PanelSize.X - Host.Padding.X * 2 - EyeballedScrollbarWidth, 0f);
            ScrollHost = new VerticalControlsHost(null, Vector2.Zero, size);

            ScrollablePanel = Host.CreateScrollableArea(ScrollHost.Panel, new Vector2(ScreenSize.X - Host.Padding.X * 2, 0.62f));

            IgnoreScrollEvent = true;
            ScrollablePanel.PanelScrolled += (panel) =>
            {
                if(IgnoreScrollEvent)
                    return;

                if(Tab == TabEnum.Emitters)
                    LastScrollEmitters = panel.ScrollbarVPosition;
                else if(Tab == TabEnum.Lights)
                    LastScrollLights = panel.ScrollbarVPosition;
            };

            MyGuiControlButton highlightButton = null;

            if(Tab == TabEnum.Emitters)
            {
                highlightButton = buttonEmitters;

                EditorEmitters.RecreateControls(Host);

                ScrollablePanel.SetVerticalScrollbarValue(LastScrollEmitters);
            }
            else if(Tab == TabEnum.Lights)
            {
                highlightButton = buttonLights;

                EditorLights.RecreateControls(Host);

                ScrollablePanel.SetVerticalScrollbarValue(LastScrollLights);
            }
            else if(Tab == TabEnum.General)
            {
                highlightButton = buttonGeneral;

                EditorGeneral.RecreateControls(Host);
            }
            else if(Tab == TabEnum.Debug)
            {
                highlightButton = buttonDebug;

                EditorDebug.RecreateControls(Host);
            }

            if(highlightButton != null)
            {
                highlightButton.CustomStyle = ButtonStyle_TabSelected;
                FocusedControl = highlightButton;
            }

            IgnoreScrollEvent = false;

            Host.SetPosition(new Vector2(Host.CurrentPosition.X, (ScreenSize.Y / 2) - ButtonSize.Y - (Host.Padding.Y * 2)));

            MyGuiControlButton buttonSaveToFile = Host.CreateButton("Save to file", "Saves the particle to a .sbc file in the folder that you pick.");
            buttonSaveToFile.ButtonClicked += (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                buttonSaveToFile.Enabled = false;

                try
                {
                    string fileName = SelectedParticle.Name;

                    foreach(char c in Path.GetInvalidFileNameChars())
                    {
                        fileName = fileName.Replace(c, '_');
                    }

                    FileDialog<SaveFileDialog>("Save a particle effect", null, FileDialogFilterSBC, (filePath) =>
                    {
                        try
                        {
                            MyObjectBuilder_ParticleEffect particleOB = MyParticleEffectDataSerializer.SerializeToObjectBuilder(SelectedParticle.Data);
                            MyObjectBuilder_Definitions definitionsOB = new MyObjectBuilder_Definitions();
                            definitionsOB.ParticleEffects = new MyObjectBuilder_ParticleEffect[] { particleOB };

                            if(EditorUI.SerializeToXML(filePath, definitionsOB))
                            {
                                Notifications.Show($"Succesfully saved {SelectedParticle.Name} to:\n{filePath}", 5);
                            }
                        }
                        catch(Exception e)
                        {
                            Log.Error(e);
                        }
                    }, preFilledFileName: fileName);
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
                finally
                {
                    buttonSaveToFile.Enabled = true;
                }
            };

            MyGuiControlButton buttonHelp = Host.CreateButton("Help/hotkeys",
                "Hold RMB while not on any UI to have normal game control." +
                "\nF to restart particle playback." +
                "\nR to toggle parenting particle to character." +
                "\nAlso various hotkeys in individual controls like in sliders, input boxes, timelines, etc, see their tooltips." +
                "\n");

            MyGuiControlButton buttonResetAll = Host.CreateButton("Full Reset",
                "Resets the entire particle to its original state, if it was a loaded particle.",
                clicked: (b) =>
                {
                    if(SelectedParticle == null)
                        return;

                    if(SelectedParticle.OriginalData == null)
                    {
                        Notifications.Show("No original data found, this is likely a new particle.", 5, Color.Red);
                        return;
                    }

                    EditorUI.PopupConfirmation("Are you sure you want to reset this entire effect back to its original state?", () =>
                    {
                        MyParticleEffectData data = new MyParticleEffectData();
                        data.Start(SelectedParticle.Data.ID, SelectedParticle.Data.Name);
                        MyParticleEffectDataSerializer.DeserializeFromObjectBuilder(data, SelectedParticle.OriginalData);

                        MyParticleEffectsLibrary.Remove(data.Name);
                        MyParticleEffectsLibrary.Add(data);

                        SelectedParticle.Despawn();
                        SelectedParticle.Spawn(data.Name, true);

                        SelectedParticle.HasChanges = false;

                        Notifications.Show("Particle reset to original state.", 5);
                        RefreshUI();
                    }, focusNoButton: true);
                });

            Host.PositionControls(buttonHelp, buttonResetAll, buttonSaveToFile);

            // TODO: revert entire particle to OriginalParticleData
        }
    }
}
