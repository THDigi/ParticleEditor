using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Digi.ParticleEditor.GameData;
using Digi.ParticleEditor.UIControls;
using Sandbox.Game.Screens;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender.Animations;

namespace Digi.ParticleEditor
{
    // TODO: multi-select support to edit the same property on multiple emitters/lights... needs quite some redesign

    public class EditorEmitters
    {
        public MyParticleGPUGenerationData SelectedEmitter { get; private set; }

        MyGuiControlListbox EmitterListBox;

        readonly EditorUI Editor;
        VerticalControlsHost Host;

        ParticleHandler SelectedParticle => Editor.SelectedParticle;
        MyGuiControlScrollablePanel ScrollablePanel => Editor.ScrollablePanel;
        VerticalControlsHost ScrollHost => Editor.ScrollHost;

        HashSet<IMyConstProperty> SkipProperties => Editor.SkipProperties;

        readonly CollapsibleSection SectionImage = new CollapsibleSection("Image");
        readonly CollapsibleSection SectionGeneration = new CollapsibleSection("Generation");
        readonly CollapsibleSection SectionMotion = new CollapsibleSection("Motion");
        readonly CollapsibleSection SectionLighting = new CollapsibleSection("Lighting");
        readonly CollapsibleSection SectionExternalForces = new CollapsibleSection("External Forces");
        readonly CollapsibleSection SectionCamera = new CollapsibleSection("Camera");
        readonly CollapsibleSection SectionOther = new CollapsibleSection("Other");

        public EditorEmitters(EditorUI editor)
        {
            Editor = editor;

            Editor.SelectedParticle.Changed += SelectedParticleChanged;
        }

        void SelectedParticleChanged(MyParticleEffectData oldParticle, MyParticleEffectData newParticle)
        {
            SelectedEmitter = null;
        }

        public void RecreateControls(VerticalControlsHost host)
        {
            Host = host;

            MyGuiControlButton buttonAdd = Host.CreateButton("Add", "Immediately adds an emitter with default values.\nDoes not use selection.", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                MyParticleGPUGenerationData emitter = new MyParticleGPUGenerationData();
                emitter.Start(SelectedParticle.Data); // adds properties and default values
                emitter.Name = $"New emitter {SelectedParticle.Data.GetGenerations().Count + 1}";

                const bool recreateParticle = false;
                SelectedParticle.Data.AddGeneration(emitter, recreateParticle);

                SelectedEmitter = emitter;

                if(Editor.DrawOnlySelected)
                {
                    Editor.RestoreEnabled();
                    Editor.RefreshShowOnlySelected();
                }

                RefreshEmitterList();
                EmitterListBox.ScrollToFirstSelection();
                SelectedParticle.Refresh();
            });

            MyGuiControlButton buttonClone = Host.CreateButton("Clone", "Immediately copies the selected emitter.\nRequires a single selection.", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                if(SelectedEmitter != null)
                {
                    MyParticleGPUGenerationData emitter = new MyParticleGPUGenerationData();
                    emitter.Start(SelectedParticle.Data); // adds properties and default values

                    ParticleGeneration ob = SelectedEmitter.SerializeToObjectBuilder();
                    emitter.DeserializeFromObjectBuilder(ob);

                    emitter.Name = $"Clone of {SelectedEmitter.Name}";

                    const bool recreateParticle = false;
                    SelectedParticle.Data.AddGeneration(emitter, recreateParticle);

                    SelectedEmitter = emitter;

                    if(Editor.DrawOnlySelected)
                    {
                        Editor.RestoreEnabled();
                        Editor.RefreshShowOnlySelected();
                    }

                    RefreshEmitterList();
                    EmitterListBox.ScrollToFirstSelection();
                    SelectedParticle.Refresh();
                }
                else if(EmitterListBox?.SelectedItems != null && EmitterListBox.SelectedItems.Count > 1)
                {
                    EditorUI.PopupInfo("Too many selections", "Select a single emitter to clone.", MyMessageBoxStyleEnum.Error);
                }
            });

            MyGuiControlButton buttonRename = Host.CreateButton("Rename", "Prompts to rename the selected emitter(s).", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                if(SelectedEmitter != null)
                {
                    ValueGetScreenWithCaption textPopup = new ValueGetScreenWithCaption($"Rename emitter {SelectedEmitter.Name}", SelectedEmitter.Name, (newName) =>
                    {
                        if(SelectedEmitter != null)
                        {
                            SelectedEmitter.Name = newName;
                            RefreshEmitterList();
                        }
                        return true;
                    });
                    MyGuiSandbox.AddScreen(textPopup);
                }
                else if(EmitterListBox?.SelectedItems != null && EmitterListBox.SelectedItems.Count > 1)
                {
                    ValueGetScreenWithCaption textPopup = new ValueGetScreenWithCaption($"Rename {EmitterListBox.SelectedItems.Count} emitters, numbers will be suffixed.", "Emitter", (newName) =>
                    {
                        if(EmitterListBox?.SelectedItems != null && EmitterListBox.SelectedItems.Count > 1)
                        {
                            int num = 1;

                            foreach(MyGuiControlListbox.Item item in EmitterListBox.SelectedItems)
                            {
                                var emitter = item.UserData as MyParticleGPUGenerationData;
                                if(emitter != null)
                                {
                                    emitter.Name = $"{newName} #{num}";
                                    num++;
                                }
                            }

                            RefreshEmitterList();
                        }
                        return true;
                    });
                    MyGuiSandbox.AddScreen(textPopup);
                }
            });

            MyGuiControlButton buttonDelete = Host.CreateButton("Delete", "Prompts to delete the selected emitter(s).", clicked: (b) =>
            {
                if(SelectedParticle.SpawnedEffect == null)
                    return;

                if(SelectedEmitter != null)
                {
                    EditorUI.PopupConfirmation($"Delete '{SelectedEmitter.Name}' emitter?", () =>
                    {
                        if(SelectedEmitter != null)
                        {
                            SelectedParticle.Data.RemoveGeneration(SelectedEmitter);
                            Editor.StoredEnabled.Remove(SelectedEmitter);

                            CallAfterRemove();
                        }
                    });
                }
                else if(EmitterListBox?.SelectedItems != null && EmitterListBox.SelectedItems.Count > 1)
                {
                    EditorUI.PopupConfirmation($"Delete the {EmitterListBox.SelectedItems.Count} selected emitters?", () =>
                    {
                        if(EmitterListBox?.SelectedItems != null)
                        {
                            foreach(MyGuiControlListbox.Item item in EmitterListBox.SelectedItems)
                            {
                                var emitter = item.UserData as MyParticleGPUGenerationData;
                                if(emitter != null)
                                {
                                    SelectedParticle.Data.RemoveGeneration(emitter);
                                    Editor.StoredEnabled.Remove(emitter);
                                }
                            }

                            CallAfterRemove();
                        }
                    });
                }

                void CallAfterRemove()
                {
                    SelectedEmitter = null;

                    RefreshEmitterList();
                    SelectedParticle.Refresh();
                    Editor.RefreshShowOnlySelected();
                }
            });

            Editor.ButtonResetVis = Host.CreateButton("ResetVis", "If this is a loaded particle, this resets visibility of emitters back to their original state (Enabled state)." +
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

                foreach(MyParticleGPUGenerationData emitter in SelectedParticle.Data.GetGenerations())
                {
                    ParticleGeneration emitterOB = originalDataOB.ParticleGenerations.Find(e => e.Name == emitter.Name);
                    if(emitterOB == null)
                    {
                        unaffected.Add(emitter.Name);
                        continue;
                    }

                    GenerationProperty enabledProp = emitterOB.Properties.Find(p => p.Name == nameof(emitter.Enabled));
                    if(enabledProp == null)
                        emitter.Enabled.SetValue(true);
                    else
                        emitter.Enabled.SetValue(enabledProp.ValueBool);
                }

                if(unaffected.Count > 0)
                {
                    EditorUI.PopupInfo("Warning", $"Newer emitters were unaffected: {string.Join(", ", unaffected)}");
                }

                RefreshEmitterList();
            });

            if(Editor.DrawOnlySelected)
                Editor.ButtonResetVis.Enabled = false;

            MyGuiControlCheckbox cbDrawSelected = Host.CreateCheckboxNoLabel(Editor.DrawOnlySelected, "Render only selected thing from the current tab and hides all from the other tab.", (cb) =>
            {
                Editor.DrawOnlySelected = cb.IsChecked;
                Editor.RefreshShowOnlySelected();
                RefreshEmitterList();
                SelectedParticle.Refresh(edits: false);
            });

            Host.PositionControlsNoSize(buttonAdd, buttonClone, buttonRename, buttonDelete, Editor.ButtonResetVis, cbDrawSelected);

            EmitterListBox = Host.CreateListBox(6, Host.PanelSize.X * 0.95f, multiSelect: true);
            Host.PositionAndFillWidth(EmitterListBox, EmitterListBox.TextScale);

            EmitterListBox.ItemsSelected += (control) =>
            {
                if(control.SelectedItems.Count == 0)
                    return;

                if(control.SelectedItems.Count > 1)
                    SelectedEmitter = null;
                else
                    SelectedEmitter = control.SelectedItems[0]?.UserData as MyParticleGPUGenerationData;

                if(SelectedEmitter != null)
                    Editor.RefreshShowOnlySelected();

                RefreshEmitterProperties(SelectedEmitter);
            };

            EmitterListBox.ItemDoubleClicked += (control) =>
            {
                if(Editor.DrawOnlySelected || control.SelectedItems.Count == 0)
                    return;

                SelectedEmitter = control.SelectedItems[0]?.UserData as MyParticleGPUGenerationData;

                if(SelectedEmitter != null)
                {
                    SelectedEmitter.Enabled.SetValue(!SelectedEmitter.Enabled.GetValue());

                    RefreshEmitterList();
                    SelectedParticle.Refresh();
                }
            };

            Host.InsertSeparator();

            MyGuiControlLabel emitterPropLabel = Host.CreateLabel("Selected emitter's properties:");
            Editor.ButtonFullReset = Host.CreateButton("Reset Properties",
                "Resets all properties of the selected emitter to the original state." +
                "\nIf this is a new emitter or a new particle effect, this will do nothing.",
                clicked: (b) =>
                {
                    if(SelectedParticle.SpawnedEffect == null || SelectedEmitter == null)
                        return;

                    MyObjectBuilder_ParticleEffect originalDataOB = Editor.OriginalParticleData.GetValueOrDefault(SelectedParticle.Name);
                    if(originalDataOB == null)
                    {
                        Notifications.Show("No original data found, this is likely a new particle.", 5, Color.Red);
                        return;
                    }

                    ParticleGeneration emitterOB = originalDataOB.ParticleGenerations.Find(e => e.Name == SelectedEmitter.Name);

                    if(emitterOB == null)
                    {
                        Notifications.Show("Emitter original data not found, this is likely a new emitter.", 5, Color.Red);
                        return;
                    }

                    EditorUI.PopupConfirmation($"This will reset properties of emitter '{SelectedEmitter.Name}'.\nProceed?", () =>
                    {
                        SelectedEmitter.InitDefault();
                        SelectedEmitter.DeserializeFromObjectBuilder(emitterOB);

                        RefreshEmitterList();
                        SelectedParticle.Refresh();

                        Notifications.Show($"Reset emitter '{SelectedEmitter.Name}' to original state.", 5);
                    }, focusNoButton: true);
                });

            Host.PositionControls(emitterPropLabel, Host.CreateLabel(""), Editor.ButtonFullReset);

            ScrollablePanel.Position = Host.CurrentPosition;
            Host.Add(ScrollablePanel);
            Host.PositionAndFillWidth(ScrollablePanel);

            RefreshEmitterList();
        }

        public void RefreshEmitterList(bool scrollToSelection = true)
        {
            if(SelectedParticle.SpawnedEffect == null || EmitterListBox == null)
                return;

            EmitterListBox.ClearItems();
            EmitterListBox.SelectedItems.Clear();

            MyObjectBuilder_ParticleEffect originalOB = Editor.OriginalParticleData.GetValueOrDefault(SelectedParticle.Name);

            string tooltip = (Editor.DrawOnlySelected ? "" : "Double-click to toggle Enabled.");

            if(!SelectedParticle.Data.GetGenerations().Contains(SelectedEmitter))
            {
                SelectedEmitter = null;
            }

            if(SelectedParticle.Data.GetGenerations().Count == 0)
            {
                MyGuiControlListbox.Item listItem = new MyGuiControlListbox.Item(new StringBuilder().Append(' ', 30).Append("Add an emitter to begin"));
                listItem.ColorMask = Color.Lime;
                EmitterListBox.Add(listItem);
            }
            else
            {
                foreach(MyParticleGPUGenerationData emitter in SelectedParticle.Data.GetGenerations())
                {
                    // automatically select first emitter if nothing is selected
                    if(SelectedEmitter == null)
                        SelectedEmitter = emitter;

                    // turned off because too crowded
                    //StringBuilder sb = new StringBuilder(128);
                    //sb.Append(emitter.Name);
                    //sb.Append("    - mat: ");
                    //sb.Append(emitter.Material?.GetValue()?.Id.String);
                    //int frames = emitter.ArrayModulo.GetValue();
                    //if(frames > 0)
                    //    sb.Append(", ").Append(frames).Append(" frames");

                    MyGuiControlListbox.Item listItem = new MyGuiControlListbox.Item(new StringBuilder(emitter.Name), tooltip, userData: emitter);

                    if(Editor.DrawOnlySelected)
                    {
                        listItem.ColorMask = emitter.Enabled ? Color.White : Color.Gray;
                        listItem.FontOverride = MyFontEnum.Debug;
                    }
                    else
                    {
                        listItem.ColorMask = Color.White;
                        listItem.FontOverride = emitter.Enabled ? MyFontEnum.Debug : MyFontEnum.Red;
                    }

                    EmitterListBox.Add(listItem);

                    if(emitter == SelectedEmitter)
                    {
                        EmitterListBox.SelectedItems.Add(listItem);
                    }
                }
            }

            RefreshEmitterProperties(SelectedEmitter);
            Editor.RefreshButtonsStatus();

            if(scrollToSelection)
                EmitterListBox.ScrollToFirstSelection();
        }

        public void RefreshEmitterPropertiesUI()
        {
            RefreshEmitterProperties(SelectedEmitter);
        }

        void RefreshEmitterProperties(MyParticleGPUGenerationData emitter)
        {
            try
            {
                ScrollHost.Reset();
                ScrollHost.SkipProperties = SkipProperties;
                SkipProperties.Clear();

                if(EmitterListBox?.SelectedItems != null && EmitterListBox.SelectedItems.Count > 1)
                {
                    // to support editing props of multiple emitters it would need a few things:
                    // - some nice way of showing all values or some way to show that value is different for other selections
                    // - have to fix GUI refresh on expand/contract sections to maintain multi-select

                    ScrollHost.PositionAndFillWidth(ScrollHost.CreateLabel("Multiple emitters selected."));
                    EditorUI.FinalizeScrollable(ScrollablePanel, ScrollHost.Panel, ScrollHost);
                    return;
                }

                if(emitter == null)
                    return;

                IMyConstProperty[] properties = (IMyConstProperty[])emitter.GetProperties();

                SkipProperties.Add(emitter.Enabled);

                using(CollapsibleSection section = ScrollHost.CollapsibleSectionStart(SectionImage))
                {
                    {
                        if(!ScrollHost.SectionHidesControl())
                        {
                            VRageRender.MyTransparentMaterial mat = emitter.Material.GetValue();
                            string label;
                            int frames = emitter.ArrayModulo.GetValue();
                            if(frames == 0)
                                frames = 1; // HACK: 0 is also supported and works as 1 frame, because of how they use this in bitwise math

                            if(VersionSpecificInfo.IsMaterialSupported(mat))
                                label = $"Material: {mat.Id.String}  (using {frames} {(frames == 1 ? "frame" : "frames")})";
                            else
                                label = $"Material: {mat.Id.String}  (unsupported!)";

                            MyGuiControlButton button = ScrollHost.CreateButton(label, "Open material and UV/animated atlas configurator.", MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, clicked: (b) =>
                            {
                                UITextureAligner screen = new UITextureAligner(emitter);
                                screen.Closed += (s, isUnloading) =>
                                {
                                    Editor.RefreshUI();
                                    SelectedParticle.Refresh();
                                };
                                MyGuiSandbox.AddScreen(screen);
                            });

                            ScrollHost.PositionAndFillWidth(button);
                        }

                        SkipProperties.Add(emitter.Material);
                        SkipProperties.Add(emitter.ArraySize);
                        SkipProperties.Add(emitter.ArrayOffset);
                        SkipProperties.Add(emitter.ArrayModulo);
                    }

                    ScrollHost.PropNumberBox(emitter, emitter.AnimationFrameTime);

                    ScrollHost.PropAnimated(emitter, emitter.Color, colorKeys: true);

                    ScrollHost.PropAnimated(emitter, emitter.ColorIntensity, colorKeys: true);

                    ScrollHost.PropSlider(emitter, emitter.HueVar);

                    ScrollHost.PropAnimated(emitter, emitter.Emissivity, colorKeys: true);

                    ScrollHost.PropCheckbox(emitter, emitter.Streaks);

                    ScrollHost.PropNumberBox(emitter, emitter.StreakMultiplier);

                    ScrollHost.PropAnimated(emitter, emitter.Radius);

                    ScrollHost.PropNumberBox(emitter, emitter.RadiusVar);

                    ScrollHost.PropAnimated(emitter, emitter.Thickness);
                }

                ScrollHost.InsertSeparator();

                using(CollapsibleSection section = ScrollHost.CollapsibleSectionStart(SectionGeneration))
                {
                    ScrollHost.PropAnimated(emitter, emitter.ParticlesPerFrame);

                    ScrollHost.PropAnimated(emitter, emitter.ParticlesPerSecond);

                    ScrollHost.PropAnimated(emitter, emitter.DirectionConeVar);

                    ScrollHost.PropAnimated(emitter, emitter.DirectionInnerCone); // TODO: a way to show a calculated value? like in this case show DirectionInnerCone*DirectionConeVar as degrees.

                    ScrollHost.PropAnimated(emitter, emitter.EmitterSize);

                    ScrollHost.PropAnimated(emitter, emitter.EmitterSizeMin);

                    ScrollHost.PropNumberBoxVector3(emitter, emitter.Offset);

                    ScrollHost.PropNumberBoxVector3(emitter, emitter.Direction);

                    // HACK because emitter.RotationReference returns only the value
                    MyConstPropertyEnum prop = (MyConstPropertyEnum)properties[(int)MyGPUGenerationPropertiesEnum.RotationReference];
                    ScrollHost.PropComboBox(emitter, prop);

                    ScrollHost.PropNumberBoxVector3(emitter, emitter.Angle);

                    ScrollHost.PropNumberBoxVector3(emitter, emitter.AngleVar);

                    ScrollHost.PropNumberBox(emitter, emitter.Life);

                    ScrollHost.PropNumberBox(emitter, emitter.LifeVar);
                }

                ScrollHost.InsertSeparator();

                using(CollapsibleSection section = ScrollHost.CollapsibleSectionStart(SectionMotion))
                {
                    ScrollHost.PropAnimated(emitter, emitter.Velocity);

                    ScrollHost.PropAnimated(emitter, emitter.VelocityVar);

                    ScrollHost.PropNumberBoxVector3(emitter, emitter.Acceleration);

                    ScrollHost.PropAnimated(emitter, emitter.AccelerationFactor);

                    ScrollHost.PropCheckbox(emitter, emitter.RotationEnabled);

                    ScrollHost.PropNumberBox(emitter, emitter.RotationVelocity);

                    ScrollHost.PropNumberBox(emitter, emitter.RotationVelocityVar);
                }

                ScrollHost.InsertSeparator();

                using(CollapsibleSection section = ScrollHost.CollapsibleSectionStart(SectionLighting))
                {
                    ScrollHost.PropCheckbox(emitter, emitter.Light);

                    ScrollHost.PropCheckbox(emitter, emitter.VolumetricLight);

                    ScrollHost.PropNumberBox(emitter, emitter.AmbientFactor);

                    ScrollHost.PropNumberBox(emitter, emitter.ShadowAlphaMultiplier);

                    ScrollHost.PropNumberBox(emitter, emitter.SoftParticleDistanceScale);

                    ScrollHost.PropNumberBox(emitter, emitter.OITWeightFactor);
                }

                ScrollHost.InsertSeparator();

                using(CollapsibleSection section = ScrollHost.CollapsibleSectionStart(SectionCamera))
                {
                    ScrollHost.PropNumberBox(emitter, emitter.CameraBias);

                    ScrollHost.PropNumberBox(emitter, emitter.DistanceScalingFactor);

                    ScrollHost.PropNumberBox(emitter, emitter.CameraSoftRadius);

                    ScrollHost.PropCheckbox(emitter, emitter.UseAlphaAnisotropy);
                }

                ScrollHost.InsertSeparator();

                using(CollapsibleSection section = ScrollHost.CollapsibleSectionStart(SectionExternalForces))
                {
                    ScrollHost.PropCheckbox(emitter, emitter.Collide);

                    ScrollHost.PropNumberBox(emitter, emitter.Bounciness);

                    ScrollHost.PropNumberBox(emitter, emitter.RotationVelocityCollisionMultiplier);

                    ScrollHost.PropSliderInt(emitter, emitter.CollisionCountToKill);

                    ScrollHost.PropCheckbox(emitter, emitter.SleepState);

                    ScrollHost.PropNumberBox(emitter, emitter.Gravity);

                    ScrollHost.PropCheckbox(emitter, emitter.MotionInterpolation);

                    ScrollHost.PropNumberBox(emitter, emitter.MotionInheritance);
                }

                ScrollHost.InsertSeparator();

                using(CollapsibleSection section = ScrollHost.CollapsibleSectionStart(SectionOther))
                {
                    ScrollHost.PropSlider(emitter, emitter.TargetCoverage);

                    {
                        MyConstPropertyBool prop = (MyConstPropertyBool)properties[(int)MyGPUGenerationPropertiesEnum.UseEmissivityChannel];
                        if(SkipProperties.Add(prop)) // condition reason: in case keen decides to remove the id and it shifts to something else
                        {
                            ScrollHost.PropCheckbox(emitter, prop);
                        }
                    }

                    bool unknownPropsSeparator = true;

                    // find any new properties that might exist
                    foreach(IMyConstProperty prop in emitter.GetProperties().OrderBy(p => p.Name))
                    {
                        if(SkipProperties.Contains(prop))
                            continue;

                        if(unknownPropsSeparator)
                        {
                            unknownPropsSeparator = false;
                            ScrollHost.InsertSeparator();
                        }

                        ScrollHost.PropEditXML(emitter, prop);
                    }
                }

                EditorUI.FinalizeScrollable(ScrollablePanel, ScrollHost.Panel, ScrollHost, () => emitter == null || !emitter.Enabled);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
