using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game.Screens;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Render.Particles;
using VRageMath;
using VRageRender.Animations;

namespace Digi.ParticleEditor
{
    public class EditorLights
    {
        public MyParticleLightData SelectedLight { get; private set; }

        MyGuiControlListbox LightListBox;

        readonly EditorUI Editor;
        VerticalControlsHost Host;
        ParticleHandler SelectedParticle => Editor.SelectedParticle;
        MyGuiControlScrollablePanel ScrollablePanel => Editor.ScrollablePanel;
        VerticalControlsHost ScrollHost => Editor.ScrollHost;

        HashSet<IMyConstProperty> SkipProperties => Editor.SkipProperties;

        public EditorLights(EditorUI editor)
        {
            Editor = editor;
            Editor.SelectedParticle.Changed += SelectedParticleChanged;
        }

        void SelectedParticleChanged(MyParticleEffectData oldParticle, MyParticleEffectData newParticle)
        {
            SelectedLight = null;
        }

        public void RecreateControls(VerticalControlsHost host)
        {
            Host = host;

            MyGuiControlButton buttonAdd = Host.CreateButton("Add", "Immediately adds a light with default values.\nDoes not use selection.", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                MyParticleLightData light = new MyParticleLightData();
                light.Start(SelectedParticle.Data); // adds properties and default values
                light.Name = $"New Light {SelectedParticle.Data.GetParticleLights().Count + 1}";

                const bool recreateParticle = false;
                SelectedParticle.Data.AddParticleLight(light, recreateParticle);

                SelectedLight = light;

                if(Editor.DrawOnlySelected)
                {
                    Editor.RestoreEnabled();
                    Editor.RefreshShowOnlySelected();
                }

                RefreshLightsList();
                LightListBox.ScrollToFirstSelection();
                SelectedParticle.Refresh();
            });

            MyGuiControlButton buttonClone = Host.CreateButton("Clone", "Immediately copies the selected light.\nRequires a single selection.", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                if(SelectedLight != null)
                {
                    MyParticleLightData light = new MyParticleLightData();
                    light.Start(SelectedParticle.Data); // adds properties and default values

                    ParticleLight ob = SelectedLight.SerializeToObjectBuilder();
                    light.DeserializeFromObjectBuilder(ob);

                    light.Name = $"Clone of {SelectedLight.Name}";

                    const bool recreateParticle = false;
                    SelectedParticle.Data.AddParticleLight(light, recreateParticle);

                    SelectedLight = light;

                    if(Editor.DrawOnlySelected)
                    {
                        Editor.RestoreEnabled();
                        Editor.RefreshShowOnlySelected();
                    }

                    RefreshLightsList();
                    LightListBox.ScrollToFirstSelection();
                    SelectedParticle.Refresh();
                }
                else if(LightListBox?.SelectedItems != null && LightListBox.SelectedItems.Count > 1)
                {
                    EditorUI.PopupInfo("Too many selections", "Select a single light to clone.", MyMessageBoxStyleEnum.Error);
                }
            });

            MyGuiControlButton buttonRename = Host.CreateButton("Rename", "Prompts to rename the selected light(s).", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                if(SelectedLight != null)
                {
                    ValueGetScreenWithCaption textPopup = new ValueGetScreenWithCaption($"Rename light {SelectedLight.Name}", SelectedLight.Name, (text) =>
                    {
                        if(SelectedLight != null)
                        {
                            SelectedLight.Name = text;
                            RefreshLightsList();
                        }
                        return true;
                    });
                    MyGuiSandbox.AddScreen(textPopup);
                }
                else if(LightListBox?.SelectedItems != null && LightListBox.SelectedItems.Count > 1)
                {
                    ValueGetScreenWithCaption textPopup = new ValueGetScreenWithCaption($"Rename {LightListBox.SelectedItems.Count} lights, numbers will be suffixed.", "Light", (newName) =>
                    {
                        if(LightListBox?.SelectedItems != null && LightListBox.SelectedItems.Count > 1)
                        {
                            int num = 1;

                            foreach(MyGuiControlListbox.Item item in LightListBox.SelectedItems)
                            {
                                var emitter = item.UserData as MyParticleLightData;
                                if(emitter != null)
                                {
                                    emitter.Name = $"{newName} #{num}";
                                    num++;
                                }
                            }

                            RefreshLightsList();
                        }
                        return true;
                    });
                    MyGuiSandbox.AddScreen(textPopup);
                }
            });

            MyGuiControlButton buttonDelete = Host.CreateButton("Delete", "Prompts to delete the selected light(s).", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                if(SelectedLight != null)
                {
                    EditorUI.PopupConfirmation($"Delete '{SelectedLight.Name}' light?", () =>
                    {
                        if(SelectedLight != null)
                        {
                            SelectedParticle.Data.RemoveParticleLight(SelectedLight);
                            Editor.StoredEnabled.Remove(SelectedLight);

                            CallAfterRemove();
                        }
                    });
                }
                else if(LightListBox?.SelectedItems != null && LightListBox.SelectedItems.Count > 1)
                {
                    EditorUI.PopupConfirmation($"Delete the {LightListBox.SelectedItems.Count} selected lights?", () =>
                    {
                        if(LightListBox?.SelectedItems != null)
                        {
                            foreach(MyGuiControlListbox.Item item in LightListBox.SelectedItems)
                            {
                                var light = item.UserData as MyParticleLightData;
                                if(light != null)
                                {
                                    SelectedParticle.Data.RemoveParticleLight(light);
                                    Editor.StoredEnabled.Remove(light);
                                }
                            }

                            CallAfterRemove();
                        }
                    });
                }

                void CallAfterRemove()
                {
                    SelectedLight = null;

                    RefreshLightsList();
                    SelectedParticle.Refresh();

                    Editor.RefreshShowOnlySelected();
                }
            });

            Editor.ButtonResetVis = Host.CreateButton("ResetVis", "If this is a loaded particle, this resets visibility of all lights back to their original state (Enabled state)." +
                "\nDoes not use selection.", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                MyObjectBuilder_ParticleEffect originalDataOB = Editor.OriginalParticleData.GetValueOrDefault(SelectedParticle.Name);
                if(originalDataOB == null)
                {
                    EditorUI.PopupInfo("Warning", "This is a newly created particle, it has no original data to reset to.");
                    return;
                }

                List<string> unaffected = new List<string>();

                foreach(MyParticleLightData light in SelectedParticle.Data.GetParticleLights())
                {
                    ParticleLight lightOB = originalDataOB.ParticleLights.Find(e => e.Name == light.Name);
                    if(lightOB == null)
                    {
                        unaffected.Add(light.Name);
                        continue;
                    }

                    GenerationProperty enabledProp = lightOB.Properties.Find(p => p.Name == nameof(light.Enabled));
                    if(enabledProp == null)
                        light.Enabled.SetValue(true);
                    else
                        light.Enabled.SetValue(enabledProp.ValueBool);
                }

                if(unaffected.Count > 0)
                {
                    EditorUI.PopupInfo("Warning", $"Newer lights were unaffected: {string.Join(", ", unaffected)}");
                }

                RefreshLightsList();
            });

            if(Editor.DrawOnlySelected)
                Editor.ButtonResetVis.Enabled = false;

            MyGuiControlCheckbox cbDrawSelected = Host.CreateCheckboxNoLabel(Editor.DrawOnlySelected, "Render only selected thing from the current tab and hides all from the other tab.", (cb) =>
            {
                Editor.DrawOnlySelected = cb.IsChecked;
                Editor.RefreshShowOnlySelected();
                RefreshLightsList();
                SelectedParticle.Refresh();
            });

            Host.PositionControlsNoSize(buttonAdd, buttonClone, buttonRename, buttonDelete, Editor.ButtonResetVis, cbDrawSelected);

            LightListBox = Host.CreateListBox(6, Host.PanelSize.X * 0.95f, multiSelect: true);
            Host.PositionAndFillWidth(LightListBox, LightListBox.TextScale);

            LightListBox.ItemsSelected += (control) =>
            {
                if(control.SelectedItems.Count == 0)
                    return;

                if(control.SelectedItems.Count > 1)
                    SelectedLight = null;
                else
                    SelectedLight = control.SelectedItems[0]?.UserData as MyParticleLightData;

                if(SelectedLight != null)
                    Editor.RefreshShowOnlySelected();

                RefreshLightProperties(SelectedLight);
            };

            LightListBox.ItemDoubleClicked += (control) =>
            {
                if(Editor.DrawOnlySelected || control.SelectedItems.Count == 0)
                    return;

                SelectedLight = control.SelectedItems[0]?.UserData as MyParticleLightData;

                if(SelectedLight != null)
                {
                    SelectedLight.Enabled.SetValue(!SelectedLight.Enabled.GetValue());

                    RefreshLightsList();
                    SelectedParticle.Refresh();
                }
            };

            Host.InsertSeparator();

            MyGuiControlLabel lightPropLabel = Host.CreateLabel("Selected light's properties:");
            Editor.ButtonFullReset = Host.CreateButton("Reset Properties",
                "Resets all properties of the selected light to the original state." +
                "\nIf this is a new light or a new particle effect, this will do nothing.",
                clicked: (b) =>
                {
                    if(SelectedParticle.SpawnedEffect == null || SelectedLight == null)
                        return;

                    MyObjectBuilder_ParticleEffect originalDataOB = Editor.OriginalParticleData.GetValueOrDefault(SelectedParticle.Name);
                    if(originalDataOB == null)
                    {
                        Notifications.Show("No original data found, this is likely a new particle.", 5, Color.Red);
                        return;
                    }

                    ParticleLight lightOB = originalDataOB.ParticleLights.Find(e => e.Name == SelectedLight.Name);

                    if(lightOB == null)
                    {
                        Notifications.Show("Light original data not found, this is likely a new light.", 5, Color.Red);
                        return;
                    }

                    EditorUI.PopupConfirmation($"This will reset properties of light '{SelectedLight.Name}'.\nProceed?", () =>
                    {
                        SelectedLight.InitDefault();
                        SelectedLight.DeserializeFromObjectBuilder(lightOB);

                        RefreshLightsList();
                        SelectedParticle.Refresh();

                        Notifications.Show($"Reset light '{SelectedLight.Name}' to original state.", 5);
                    }, focusNoButton: true);
                });

            Host.PositionControls(lightPropLabel, Host.CreateLabel(""), Editor.ButtonFullReset);

            ScrollablePanel.Position = Host.CurrentPosition;
            Host.Add(ScrollablePanel);
            Host.PositionAndFillWidth(ScrollablePanel);

            RefreshLightsList();
            LightListBox.ScrollToFirstSelection();
        }

        public void RefreshLightsList()
        {
            if(SelectedParticle.SpawnedEffect == null || LightListBox == null)
                return;

            LightListBox.ClearItems();
            LightListBox.SelectedItems.Clear();

            string tooltip = (Editor.DrawOnlySelected ? "" : "Double-click to toggle Enabled.");

            if(!SelectedParticle.Data.GetParticleLights().Contains(SelectedLight))
            {
                SelectedLight = null;
            }

            if(SelectedParticle.Data.GetParticleLights().Count == 0)
            {
                MyGuiControlListbox.Item listItem = new MyGuiControlListbox.Item(new StringBuilder().Append(' ', 30).Append("(No lights)"));
                listItem.ColorMask = Color.Gray;
                LightListBox.Add(listItem);
            }
            else
            {
                foreach(MyParticleLightData light in SelectedParticle.Data.GetParticleLights())
                {
                    // automatically select first if nothing is selected
                    if(SelectedLight == null)
                        SelectedLight = light;

                    MyGuiControlListbox.Item listItem = new MyGuiControlListbox.Item(new StringBuilder(light.Name), tooltip, userData: light);

                    if(Editor.DrawOnlySelected)
                    {
                        listItem.ColorMask = light.Enabled ? Color.White : Color.Gray;
                        listItem.FontOverride = MyFontEnum.Debug;
                    }
                    else
                    {
                        listItem.ColorMask = Color.White;
                        listItem.FontOverride = light.Enabled ? MyFontEnum.Debug : MyFontEnum.Red;
                    }

                    LightListBox.Add(listItem);

                    if(light == SelectedLight)
                    {
                        LightListBox.SelectedItems.Add(listItem);
                    }
                }
            }

            RefreshLightProperties(SelectedLight);
            Editor.RefreshButtonsStatus();
        }

        public void RefreshPropertiesUI()
        {
            RefreshLightProperties(SelectedLight);
        }

        void RefreshLightProperties(MyParticleLightData light)
        {
            try
            {
                ScrollHost.Reset();
                ScrollHost.SkipProperties = SkipProperties;
                SkipProperties.Clear();

                if(LightListBox?.SelectedItems != null && LightListBox.SelectedItems.Count > 1)
                {
                    ScrollHost.PositionAndFillWidth(ScrollHost.CreateLabel("Multiple lights selected."));
                    EditorUI.FinalizeScrollable(ScrollablePanel, ScrollHost.Panel, ScrollHost);
                    return;
                }

                if(light == null)
                    return;

                SkipProperties.Add(light.Enabled);

                ScrollHost.PropAnimated(light, light.Color, colorKeys: true);

                ScrollHost.PropAnimated(light, light.ColorVar);

                ScrollHost.PropAnimated(light, light.Intensity);

                ScrollHost.PropAnimated(light, light.IntensityVar);

                ScrollHost.PropAnimated(light, light.Range);

                ScrollHost.PropAnimated(light, light.RangeVar);

                ScrollHost.PropNumberBox(light, light.Falloff);

                ScrollHost.PropAnimated(light, light.Position);

                ScrollHost.PropAnimated(light, light.PositionVar);

                ScrollHost.InsertSeparator();

                ScrollHost.PropNumberBox(light, light.VarianceTimeout);

                ScrollHost.PropNumberBox(light, light.GravityDisplacement);


                bool unknownPropsSeparator = true;

                // find any new properties that might exist
                foreach(IMyConstProperty prop in light.GetProperties().OrderBy(p => p.Name))
                {
                    if(SkipProperties.Contains(prop))
                        continue;

                    if(unknownPropsSeparator)
                    {
                        unknownPropsSeparator = false;
                        ScrollHost.InsertSeparator();
                    }

                    ScrollHost.PropEditXML(light, prop);
                }

                EditorUI.FinalizeScrollable(ScrollablePanel, ScrollHost.Panel, ScrollHost, () => light == null || !light.Enabled);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
