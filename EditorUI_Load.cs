using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Sandbox.Game.Screens;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Render.Particles;
using VRageMath;
using VRageRender.Animations;

namespace Digi.ParticleEditor
{
    public partial class EditorUI // to allow use of the protected methods
    {
        int CreatedParticles = 1;
        MyGuiControlListbox ParticlesListbox;
        ValueGetScreenWithCaption NamePrompt;
        bool ShowParticlePicker = true;

        string SearchQuery = null;
        CheckStateEnum FilterLoop = CheckStateEnum.Unchecked;
        bool FilterSeen = false;

        bool Controls_LoadParticle()
        {
            MyGuiControlLabel labelTitle = Host.CreateLabel("Particle Editor");
            labelTitle.SetToolTip(@"Some notes about the behavior of this tool:

It sees all particles loaded into the game, including mod-added ones (but it cannot identify the mod unfortunately).

Changes to particles are only in memory and will not reset when changing between particles.
Therefore you need to save your changes to an SBC file (button at the bottom right) if you wish to keep them.

Also, particles of the same name share the same data so changes will affect it when spawned by blocks or whatever else.");

            if(!ShowParticlePicker && SelectedParticle.Name != null)
            {
                MyGuiControlButton buttonChangeParticle = Host.CreateButton("Change Particle", "Goes back to the particle browser without unloading the particle nor losing any changes.", clicked: (b) =>
                {
                    ShowParticlePicker = true;
                    RefreshUI();
                });

                Host.PositionControls(labelTitle, Host.CreateLabel(""), buttonChangeParticle);

                Host.PositionAndFillWidth(Host.CreateLabel(SelectedParticle.Name));

                return true; // show the rest of controls
            }
            else
            {
                MyGuiControlButton buttonCreateParticle = Host.CreateButton("New particle",
                    "Creates a new particle effect with the name you'll be asked for." +
                    "\nRemember that all particle changes, including new ones, are only in-memory. You need to export them to .sbc to keep them.",
                    clicked: (b) =>
                    {
                        string name = $"New Particle {CreatedParticles}";

                        NamePrompt = new ValueGetScreenWithCaption($"Create particle", name, (text) =>
                        {
                            return CreateParticle(name);
                        });
                        MyGuiSandbox.AddScreen(NamePrompt);
                    });

                MyGuiControlButton buttonLoadFromFile = Host.CreateButton("Load from file",
                    "Opens file explorer to pick a .sbc file to load particles from." +
                    "\nWill load all particles from that file and ask if you wish to overwrite them, if necessary." +
                    "\nRemember that all particle changes, including loaded ones, are only in-memory. You need to export them to .sbc to keep them.",
                    clicked: (b) => LoadParticleDialog());

                Host.PositionControls(labelTitle, buttonCreateParticle, buttonLoadFromFile);

                MyGuiControlButton buttonBack = Host.CreateButton($"< Back to {SelectedParticle.Name ?? "particle"}", "", clicked: (button) =>
                {
                    ShowParticlePicker = false;
                    RefreshUI();
                });
                buttonBack.ColorMask = (SelectedParticle.Name != null ? Color.Lime : Color.Gray);
                buttonBack.Enabled = (SelectedParticle.Name != null);
                Host.PositionAndFillWidth(buttonBack);

                MyGuiControlSearchBox searchBar = new MyGuiControlSearchBox();
                searchBar.Size = new Vector2(Host.PanelSize.X, searchBar.Size.Y * 2);
                searchBar.SearchText = SearchQuery;
                searchBar.OnTextChanged += (text) =>
                {
                    SearchQuery = text;
                    RefreshParticlesList();
                };

                // FIXME: doesn't "glow" like after clicking on it...
                FocusedControl = searchBar.TextBox;

                Host.Add(searchBar);
                Host.PositionAndFillWidth(searchBar);

                // HACK fix this control being weird
                searchBar.Position = new Vector2(searchBar.Position.X + (searchBar.Size.X / 2f), searchBar.Position.Y + (searchBar.Size.Y / 2f));

                MyGuiControlParent cbFilterLoop = Host.Insert3StateCheckbox("Loop", "Filter in/out looping particles.\nEmpty = don't filter.", FilterLoop, (state) =>
                {
                    FilterLoop = state;
                    RefreshParticlesList();
                });
                Host.UndoLastVerticalShift();

                MyGuiControlParent cbFilterLastSeen = Host.InsertCheckbox("Last Seen", "Show only last seen particles in game world.", FilterSeen, (state) =>
                {
                    FilterSeen = state;
                    RefreshParticlesList();
                });
                Host.UndoLastVerticalShift();
                Host.PositionControlsNoSize(cbFilterLoop, cbFilterLastSeen);

                ParticlesListbox = Host.CreateListBox(36, Host.PanelSize.X * 0.95f);
                Host.PositionAndFillWidth(ParticlesListbox, ParticlesListbox.TextScale);

                ParticlesListbox.ItemDoubleClicked += (c) => LoadSelected();

                RefreshParticlesList();

                MyGuiControlButton buttonLoadSelected = Host.CreateButton("Load selected",
                    null,
                    clicked: (b) => LoadSelected());

                MyGuiControlButton buttonCreateFromSelected = Host.CreateButton("Duplicate selection",
                    "Creates a new particle with the data from the selected particle.",
                    clicked: (b) =>
                    {
                        if(ParticlesListbox.SelectedItems.Count != 1)
                            return;

                        string copyFrom = (string)ParticlesListbox.SelectedItems[0].UserData;
                        string name = $"Copy of {copyFrom}";

                        NamePrompt = new ValueGetScreenWithCaption($"Duplicate particle", name, (text) =>
                        {
                            return CreateParticle(name, copyFrom);
                        });
                        MyGuiSandbox.AddScreen(NamePrompt);
                    });

                buttonLoadSelected.Enabled = false;
                buttonCreateFromSelected.Enabled = false;

                ParticlesListbox.ItemsSelected += (c) =>
                {
                    buttonLoadSelected.Enabled = true;
                    buttonCreateFromSelected.Enabled = true;

                    // gets too wide
                    //string copyFrom = (string)ParticlesListbox.SelectedItems[0].UserData;
                    //buttonLoadSelected.Text = $"Load {copyFrom}";
                };

                Host.PositionControls(buttonLoadSelected, buttonCreateFromSelected);

                return false; // hide the rest of controls
            }
        }

        void LoadSelected()
        {
            if(ParticlesListbox.SelectedItems.Count != 1)
                return;

            string subtypeId = (string)ParticlesListbox.SelectedItems[0].UserData;
            LoadParticle(subtypeId);
        }

        void UpdateLoadParticleUI()
        {
            // update every second to see new particles pop up and such
            if(FilterSeen && MySession.Static.GameplayFrameCounter % 60 == 0)
            {
                RefreshParticlesList();
            }
        }

        void RefreshParticlesList()
        {
            ParticlesListbox.ClearItems();
            ParticlesListbox.SelectedItems.Clear();

            List<(MyParticleEffectData, int)> showParticles = new List<(MyParticleEffectData, int)>();

            int tick = MySession.Static.GameplayFrameCounter;
            int minTickSeen = tick - (60 * 5); // at most 5 seconds ago

            foreach(KeyValuePair<string, MyParticleEffectData> kv in MyParticleEffectsLibrary.GetByName())
            {
                MyParticleEffectData data = kv.Value;
                int order = 0;

                if(FilterSeen)
                {
                    int lastSeenTick = Editor.LastSeenParticles.Recent.GetValueOrDefault(data.Name, -1);
                    if(lastSeenTick < 0 || lastSeenTick < minTickSeen)
                        continue;

                    order = lastSeenTick;
                }

                if(FilterLoop == CheckStateEnum.Checked && !data.Loop)
                    continue;

                if(FilterLoop == CheckStateEnum.Indeterminate && data.Loop)
                    continue;

                if(SearchQuery != null)
                {
                    if(data.Name.IndexOf(SearchQuery, StringComparison.OrdinalIgnoreCase) == -1)
                        continue;
                }

                showParticles.Add((data, order));
            }

            if(FilterSeen)
            {
                showParticles.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            }
            else
            {
                showParticles.Sort((a, b) => a.Item1.Name.CompareTo(b.Item1.Name));
            }

            foreach((MyParticleEffectData, int) kv in showParticles)
            {
                MyParticleEffectData data = kv.Item1;
                int tickSeen = kv.Item2;

                StringBuilder label = new StringBuilder(data.Name);

                if(data.Loop)
                    label.Append(" (loop)");

                if(FilterSeen)
                {
                    label.Append("   Last seen ").Append(TimeSpan.FromSeconds((tick - tickSeen) / 60.0).TotalSeconds.ToString("0")).Append("s ago");
                }

                //if(data.DurationMin > 0 || data.DurationMax > 0)
                //{
                //    label.Append(" (");

                //    if(data.DurationMax > data.DurationMin)
                //        label.Append(data.DurationMin).Append("s to ").Append(data.DurationMax).Append("s");
                //    else
                //        label.Append(data.DurationMin).Append("s");

                //    label.Append(")");
                //}

                string tooltip = $@"Integer Id: {data.ID}
Loop: {data.Loop}
Duration: {(data.DurationMin < data.DurationMax ? $"{data.DurationMin}s to {data.DurationMax}s" : data.DurationMin.ToString())}
Emitters: {data.GetGenerations().Count}
Lights: {data.GetParticleLights().Count}
Priority: {data.Priority}
Tag: {data.Tag}";

                MyGuiControlListbox.Item item = new MyGuiControlListbox.Item(label, tooltip, userData: data.Name);

                ParticlesListbox.Add(item);
            }
        }

        bool CreateParticle(string name, string cloneFromName = null)
        {
            if(MyParticleEffectsLibrary.Exists(name))
            {
                PopupInfoAction("ERROR", $"Particle name '{name}' already exists", "Select that particle", "Pick a different name", (result) =>
                {
                    if(result == MyGuiScreenMessageBox.ResultEnum.YES)
                    {
                        if(NamePrompt != null)
                            MyGuiSandbox.RemoveScreen(NamePrompt);

                        LoadParticle(name);
                    }
                }, MyMessageBoxStyleEnum.Error);
                return false;
            }

            if(cloneFromName != null && !MyParticleEffectsLibrary.Exists(cloneFromName))
            {
                PopupInfo("Error", $"Couldn't find '{cloneFromName}' to clone from!", MyMessageBoxStyleEnum.Error);
                return false;
            }

            // TODO: need a better way to deal with the particle ID

            if(MyParticleEffectsLibrary.GetById().TryGetValue(name.GetHashCode(), out MyParticleEffectData existingData))
            {
                PopupInfo("ERROR", $"The hashcode of name '{name}' used for the integer Id ({name.GetHashCode()}) already exists, used by: '{existingData.Name}'", MyMessageBoxStyleEnum.Error);
                return false;
            }

            MyParticleEffectData data = new MyParticleEffectData();
            data.Start(name.GetHashCode(), name);

            if(cloneFromName != null)
            {
                MyParticleEffectData cloneFromData = MyParticleEffectsLibrary.Get(cloneFromName);
                MyObjectBuilder_ParticleEffect dataOB = MyParticleEffectDataSerializer.SerializeToObjectBuilder(cloneFromData);
                MyParticleEffectDataSerializer.DeserializeFromObjectBuilder(data, dataOB);

                data.ID = name.GetHashCode();
                data.Name = name;
            }

            MyParticleEffectsLibrary.Add(data);
            CreatedParticles++;

            if(SelectedParticle.SpawnedEffect != null)
            {
                if(Editor.Backup.ShouldBackup)
                {
                    Editor.Backup.BackupCurrentParticle();
                }

                if(DrawOnlySelected)
                {
                    RestoreEnabled();
                    DrawOnlySelected = false;
                }

                SelectedParticle.Despawn();
            }

            SelectedParticle.ParentDistance = 2f;
            SelectedParticle.Spawn(data.Name);
            SelectedParticle.HasChanges = false;

            ShowParticlePicker = false;
            Editor.Backup.ShouldBackup = false;

            RefreshUI();
            return true;
        }

        public void LoadParticleDialog(string overrideFolder = null)
        {
            // TODO: allow multiple files? for backup load too...

            FileDialog<OpenFileDialog>("Load particle(s)", overrideFolder, FileDialogFilterSBC, (filePath) =>
            {
                try
                {
                    string text = File.ReadAllText(filePath);
                    if(string.IsNullOrWhiteSpace(text))
                    {
                        PopupInfo("Error", "File contents is empty", MyMessageBoxStyleEnum.Error);
                        return;
                    }

                    if(text.Contains("<ParticleEffects>"))
                    {
                        MyObjectBuilder_Definitions definitionsOB = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_Definitions>(text);
                        if(definitionsOB == null)
                            throw new Exception("It got deserialized as Definitions but returned null.");

                        if(definitionsOB.ParticleEffects.Length == 0)
                        {
                            PopupInfo("Error", "Definitions does not contain any particle effects.", MyMessageBoxStyleEnum.Error);
                            return;
                        }

                        if(definitionsOB.ParticleEffects.Length > 1)
                        {
                            // TODO: a 3rd option to pick one of the particles
                            PopupInfoAction("Multiple particles", $"This file contains multiple {definitionsOB.ParticleEffects.Length} particles!", "Load and overwrite all", "Cancel", (result) =>
                            {
                                if(result == MyGuiScreenMessageBox.ResultEnum.YES)
                                {
                                    foreach(MyObjectBuilder_ParticleEffect ob in definitionsOB.ParticleEffects)
                                    {
                                        AddOrReplaceParticleOB(ob);
                                    }
                                }
                            }, MyMessageBoxStyleEnum.Info);
                        }
                        else
                        {
                            LoadParticleFromOB(definitionsOB.ParticleEffects[0]);
                        }
                    }
                    else
                    {
                        MyObjectBuilder_ParticleEffect particleOB = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_ParticleEffect>(text);
                        if(particleOB == null)
                            throw new Exception("It got deserialized as ParticleEffect but returned null.");

                        LoadParticleFromOB(particleOB);
                    }
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            });

            void LoadParticleFromOB(MyObjectBuilder_ParticleEffect particleOB)
            {
                string name = particleOB.Id.SubtypeName;

                if(MyParticleEffectsLibrary.Exists(name))
                {
                    PopupInfoAction("ERROR", $"Particle name '{name}' already exists", "Overwrite", "Cancel", (result) =>
                    {
                        if(result == MyGuiScreenMessageBox.ResultEnum.YES)
                        {
                            AddOrReplaceParticleOB(particleOB);
                        }
                    }, MyMessageBoxStyleEnum.Error);
                    return;
                }
                else
                {
                    AddOrReplaceParticleOB(particleOB);
                }
            }

            void AddOrReplaceParticleOB(MyObjectBuilder_ParticleEffect particleOB)
            {
                string name = particleOB.Id.SubtypeName;

                bool existed = MyParticleEffectsLibrary.Exists(name);

                if(existed)
                    MyParticleEffectsLibrary.Remove(name);

                if(MyParticleEffectsLibrary.GetById().TryGetValue(name.GetHashCode(), out MyParticleEffectData existingData))
                {
                    PopupInfo("ERROR", $"The hashcode of name '{name}' used for the integer Id already exists, used by: '{existingData.Name}'", MyMessageBoxStyleEnum.Error);
                    return;
                }

                if(SelectedParticle != null)
                {
                    if(Editor.Backup.ShouldBackup)
                    {
                        Editor.Backup.BackupCurrentParticle();
                    }

                    if(DrawOnlySelected)
                    {
                        RestoreEnabled();
                        DrawOnlySelected = false;
                    }

                    SelectedParticle.Despawn();
                }

                MyParticleEffectData data = new MyParticleEffectData();
                data.Start(name.GetHashCode(), name);
                MyParticleEffectDataSerializer.DeserializeFromObjectBuilder(data, particleOB);
                MyParticleEffectsLibrary.Add(data);

                SelectedParticle.Spawn(data.Name);
                SelectedParticle.ParentDistance = Math.Max(2f, EstimateParticleTotalRadius(data) * 0.6f);
                SelectedParticle.HasChanges = false;

                CheckForZeroViewDistance(data);

                ShowParticlePicker = false;
                Editor.Backup.ShouldBackup = false;

                RefreshUI();
            }
        }

        void LoadParticle(string name)
        {
            if(SelectedParticle.SpawnedEffect != null)
            {
                if(Editor.Backup.ShouldBackup)
                {
                    Editor.Backup.BackupCurrentParticle();
                }

                if(DrawOnlySelected)
                {
                    RestoreEnabled();
                    DrawOnlySelected = false;
                }

                SelectedParticle.Despawn();
            }

            SelectedParticle.Spawn(name, true);

            if(SelectedParticle.SpawnedEffect == null)
                return;

            if(!OriginalParticleData.ContainsKey(name))
            {
                OriginalParticleData.Add(name, MyParticleEffectDataSerializer.SerializeToObjectBuilder(SelectedParticle.Data));
            }

            SelectedParticle.HasChanges = false;
            SelectedParticle.ParentDistance = Math.Max(2f, EstimateParticleTotalRadius(SelectedParticle.Data) * 0.6f);
            SelectedParticle.OriginalData = OriginalParticleData.GetValueOrDefault(name); // HACK: fixing original data not being there in first load because it gets assigned in Spawn()

            CheckForZeroViewDistance(SelectedParticle.Data);

            ShowParticlePicker = false;
            Editor.Backup.ShouldBackup = false;

            RefreshUI();
        }

        static void CheckForZeroViewDistance(MyParticleEffectData data)
        {
            if(data.DistanceMax <= 0)
            {
                data.DistanceMax = DefaultData.DistanceMax;
                Notifications.Show($"View distance was 0 or negative. Was reset to default: {data.DistanceMax}.", 5, Color.Yellow);
            }
        }

        static float EstimateParticleTotalRadius(MyParticleEffectData data)
        {
            BoundingSphere sphere = new BoundingSphere(Vector3.Zero, 0f);

            foreach(MyParticleGPUGenerationData emitter in data.GetGenerations())
            {
                float highestRadius = 0f;
                float radiusVar = Math.Max(0, emitter.RadiusVar.GetValue());

                for(float time = 0f; time <= 1f; time += 0.1f)
                {
                    CollectKeys(time, emitter.Radius, out MyAnimatedPropertyFloat keys);

                    float radius;
                    keys.GetKey(0, out _, out radius);
                    highestRadius = Math.Max(highestRadius, radius + radiusVar);
                    keys.GetKey(1, out _, out radius);
                    highestRadius = Math.Max(highestRadius, radius + radiusVar);
                    keys.GetKey(2, out _, out radius);
                    highestRadius = Math.Max(highestRadius, radius + radiusVar);
                    keys.GetKey(3, out _, out radius);
                    highestRadius = Math.Max(highestRadius, radius + radiusVar);
                }

                float highestEmitterSize = 0f;

                for(float time = 0f; time <= 1f; time += 0.1f)
                {
                    emitter.EmitterSize.GetInterpolatedValue(time, out Vector3 vec);
                    highestEmitterSize = Math.Max(highestEmitterSize, vec.AbsMax());
                }

                float highestVelocity = 0f;

                for(float time = 0f; time <= 1f; time += 0.1f)
                {
                    emitter.Velocity.GetInterpolatedValue(time, out float vel);
                    emitter.VelocityVar.GetInterpolatedValue(time, out float velVar);
                    highestVelocity = Math.Max(highestVelocity, vel + Math.Max(0, velVar));
                }

                float maxLife = emitter.Life.GetValue() + Math.Max(0, emitter.LifeVar.GetValue());

                float size = highestEmitterSize + highestRadius + (highestVelocity * maxLife);
                BoundingSphere emitterSphere = new BoundingSphere(emitter.Offset.GetValue(), size);
                sphere.Include(emitterSphere);
            }

            return sphere.Radius;
        }

        static void CollectKeys(float time, MyAnimatedProperty2DFloat prop, out MyAnimatedPropertyFloat keys)
        {
            if(prop.GetKeysCount() == 1)
            {
                prop.GetKey(0, out float _, out keys);
                return;
            }

            _tempFloatProperty.ClearKeys();
            prop.GetInterpolatedKeys(time, 1f, _tempFloatProperty);
            keys = _tempFloatProperty;
        }

        static readonly MyAnimatedPropertyFloat _tempFloatProperty = new MyAnimatedPropertyFloat();
    }
}
