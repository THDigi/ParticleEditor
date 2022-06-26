using System;
using Digi.ParticleEditor.GameData;
using Digi.ParticleEditor.UIControls;
using Sandbox.Game.Screens;
using Sandbox.Graphics.GUI;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor
{
    public class EditorGeneral
    {
        readonly EditorUI EditorUI;
        VerticalControlsHost Host;

        ParticleHandler SelectedParticle => EditorUI.SelectedParticle;
        MyGuiControlScrollablePanel ScrollablePanel => EditorUI.ScrollablePanel;
        VerticalControlsHost ScrollHost => EditorUI.ScrollHost;

        public EditorGeneral(EditorUI editorUI)
        {
            EditorUI = editorUI;
        }

        public void RecreateControls(VerticalControlsHost host)
        {
            Host = host;

            UINumberBox SlideBox(PropId propId, float val, Action<float> set, bool inline = false)
            {
                float? defaultVal = (float?)EditorUI.GetGeneralPropertyOriginalOrDefault(propId);

                PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propId);
                string name = propInfo.GetName();
                string tooltip = propInfo.GetTooltip();
                float min = (propInfo.ValueRangeNum.Min == 0 ? 0 : float.MinValue);
                float max = float.MaxValue;
                int inputRound = propInfo.ValueRangeNum.InputRounding;
                int dragRound = propInfo.ValueRangeNum.Rounding;

                UINumberBox box = Host.CreateNumberBox(tooltip, val, defaultVal, min, max, inputRound, dragRound, (value) =>
                {
                    if(SelectedParticle.SpawnedEffect != null)
                    {
                        set?.Invoke(value);
                        SelectedParticle.Refresh();
                    }
                });

                if(!inline)
                {
                    MyGuiControlLabel label = Host.CreateLabel(name);
                    label.SetToolTip(tooltip);
                    //label.Size = new Vector2(0.12f, label.Size.Y);

                    Host.PositionControlsNoSize(box, label);
                    Host.MoveY(0.01f); // HACK: box is taller than its actual size
                }

                return box;
            }

            {
                PropId propId = new PropId(PropType.General, nameof(EditorUI.DefaultData.Name));

                string currentName = SelectedParticle.Data.Name;

                PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propId);
                string tooltip = $"{propInfo.GetTooltip()}\n\nClick to rename.";

                MyGuiControlButton button = Host.CreateButton($"Name: {currentName}", tooltip, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, clicked: (b) =>
                {
                    ValueGetScreenWithCaption namePrompt = new ValueGetScreenWithCaption($"Create particle", currentName, (newName) =>
                    {
                        if(string.IsNullOrWhiteSpace(newName))
                            return false;

                        MyParticleEffectData existingData;

                        if(MyParticleEffectsLibrary.Exists(newName))
                        {
                            if(MyParticleEffectsLibrary.Get(newName) == SelectedParticle.Data)
                            {
                                EditorUI.PopupConfirmation($"Entered the same name, do you wish to set ID to this name's hashcode ({newName.GetHashCode()}) ?", () =>
                                {
                                    if(MyParticleEffectsLibrary.GetById().TryGetValue(newName.GetHashCode(), out existingData))
                                    {
                                        Notifications.Show($"The hashcode of name '{newName}' used for the integer Id ({newName.GetHashCode()}) already exists, used by: '{existingData.Name}'", 5, Color.Red);
                                    }
                                    else
                                    {
                                        SelectedParticle.Data.SetID(newName.GetHashCode());
                                        SelectedParticle.HasChanges = true;

                                        Notifications.Show($"Succesfully set ID to {newName.GetHashCode()}");
                                        EditorUI.RefreshUI();
                                    }
                                }, focusNoButton: true);
                                return true;
                            }

                            Notifications.Show($"Cannot rename to '{newName}' because it already exists.", 5, Color.Red);
                            return false;
                        }

                        if(MyParticleEffectsLibrary.GetById().TryGetValue(newName.GetHashCode(), out existingData))
                        {
                            EditorUI.PopupInfo("ERROR", $"The hashcode of name '{newName}' used for the integer Id ({newName.GetHashCode()}) already exists, used by: '{existingData.Name}'", MyMessageBoxStyleEnum.Error);
                            return false;
                        }

                        SelectedParticle.Data.SetName(newName);
                        SelectedParticle.Data.SetID(newName.GetHashCode());
                        SelectedParticle.HasChanges = true;

                        Notifications.Show($"Succesfully renamed to '{newName}' and set new ID to {newName.GetHashCode()}");
                        EditorUI.RefreshUI();

                        return true;
                    });
                    MyGuiSandbox.AddScreen(namePrompt);
                });

                Host.PositionAndFillWidth(button);
            }

            {
                PropId propId = new PropId(PropType.General, nameof(EditorUI.DefaultData.ID));

                PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propId);
                string tooltip = propInfo.GetTooltip();

                MyGuiControlLabel label = Host.CreateLabel($"ID: {SelectedParticle.Data.ID}");
                label.SetToolTip(tooltip);

                Host.PositionAndFillWidth(label);
            }

            Host.InsertSeparator();

            {
                PropId propId = new PropId(PropType.General, nameof(SelectedParticle.Data.DistanceMax));
                SlideBox(propId, SelectedParticle.Data.DistanceMax, (v) => SelectedParticle.Data.DistanceMax = v);
            }

            {
                PropId propId = new PropId(PropType.General, nameof(SelectedParticle.Data.Loop));

                bool defaultValue = (bool)EditorUI.GetGeneralPropertyOriginalOrDefault(propId);

                PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propId);
                string name = propInfo.GetName();
                string tooltip = $"{propInfo.GetTooltip()}\nOriginal value: {defaultValue}";

                Host.InsertCheckbox(name, tooltip,
                    SelectedParticle.Data.Loop, (value) =>
                    {
                        if(SelectedParticle.SpawnedEffect != null)
                        {
                            SelectedParticle.Data.Loop = value;

                            if(value && SelectedParticle.SpawnedEffect.IsEmittingStopped)
                            {
                                SelectedParticle.Despawn();
                                SelectedParticle.Spawn(SelectedParticle.Name);
                            }
                            else
                            {
                                SelectedParticle.Refresh();
                            }
                        }
                    });
            }

            {
                PropId propId = new PropId(PropType.General, nameof(SelectedParticle.Data.DurationMin));
                SlideBox(propId, SelectedParticle.Data.DurationMin, (v) => SelectedParticle.Data.DurationMin = v);
            }

            {
                PropId propId = new PropId(PropType.General, nameof(SelectedParticle.Data.DurationMax));
                SlideBox(propId, SelectedParticle.Data.DurationMax, (v) => SelectedParticle.Data.DurationMax = v);
            }

            {
                PropId propId = new PropId(PropType.General, nameof(SelectedParticle.Data.Priority));
                SlideBox(propId, SelectedParticle.Data.Priority, (v) => SelectedParticle.Data.Priority = v);
            }

            {
                PropId propId = new PropId(PropType.General, nameof(SelectedParticle.Data.Length));
                SlideBox(propId, SelectedParticle.Data.Length, (v) => SelectedParticle.Data.Length = v);
            }

            //Host.PositionControlsNoSize(Host.CreateLabel("SBC values not used: LowRes, Version and Preload"));
        }
    }
}
