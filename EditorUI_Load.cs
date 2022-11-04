using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Digi.ParticleEditor.UIControls;
using Sandbox.Game.Screens;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
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
            MyGuiControlLabel labelTitle = Host.CreateLabel($"Particle Editor v{ParticleEditorPlugin.Version.ToString()}");
            labelTitle.SetToolTip(@"Some notes about the behavior of this tool:

It sees all particles loaded into the game, including mod-added ones (but it cannot identify the mod unfortunately).

Changes to particles are only in memory and will not reset when changing between particles.
Therefore you need to save your changes to an SBC file (button at the bottom right) if you wish to keep them.

Also, particles of the same name share the same data so changes will affect it when spawned by blocks or whatever else.");

            if(!ShowParticlePicker && SelectedParticle.Name != null)
            {
                MyGuiControlButton buttonChangeParticle = Host.CreateButton("Change Particle",
                    "Shows the particle browser." +
                    "\nChanges to this particle remain in memory regardless, do not forget to export if you wish to keep them.", clicked: (b) =>
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
                            return CreateParticle(text);
                        });
                        MyGuiSandbox.AddScreen(NamePrompt);
                    });

                MyGuiControlButton buttonLoadFromFile = Host.CreateButton("Load from SBC",
                    "Opens file explorer to pick one or more .sbc file(s) to load particles from." +
                    "\nWill prompt for override and for picking the particles to load if multiple are found." +
                    "\nRemember that all particle changes, including loaded ones, are only in-memory. You need to export them to .sbc to keep them.",
                    clicked: (b) => LoadParticleDialog());

                Host.PositionControls(labelTitle, Host.CreateLabel(""), buttonCreateParticle, buttonLoadFromFile);

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

                (MyGuiControlParent cbFilterLoop, _, _) = Host.Insert3StateCheckbox("Loop", "Filter in/out looping particles.\nEmpty = don't filter.", FilterLoop, (state) =>
                {
                    FilterLoop = state;
                    RefreshParticlesList();
                });
                Host.UndoLastVerticalShift();

                (MyGuiControlParent cbFilterLastSeen, _, _) = Host.InsertCheckbox("Last Seen", "Show only last seen particles in game world.", FilterSeen, (state) =>
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
                    "You can also doubleclick a particle in the list to cause this action.",
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
            int minTickSeen = tick - (60 * 60); // at most 1 minute ago

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

        class LoadParticleInfo
        {
            public readonly string FileName;
            public readonly MyObjectBuilder_ParticleEffect OB;
            public bool Collides = false;

            public LoadParticleInfo(string fileName, MyObjectBuilder_ParticleEffect ob)
            {
                FileName = fileName;
                OB = ob;
            }
        }

        public void LoadParticleDialog(string overrideFolder = null)
        {
            MyParticleEffectData lastLoadedData = null;
            int loadedCount = 0;

            OpenMultipleFilesDialog("Load particle(s)", overrideFolder, FileDialogFilterSBC, (filePaths) =>
            {
                try
                {
                    List<LoadParticleInfo> foundParticles = new List<LoadParticleInfo>();

                    foreach(string filePath in filePaths)
                    {
                        string text = File.ReadAllText(filePath);
                        if(string.IsNullOrWhiteSpace(text))
                        {
                            Log.Error($"File is empty: '{filePath}'");
                            continue;
                        }

                        string fileName = Path.GetFileName(filePath);

                        if(text.Contains("<ParticleEffects>"))
                        {
                            MyObjectBuilder_Definitions definitionsOB;
                            if(!MyObjectBuilderSerializer.DeserializeXML<MyObjectBuilder_Definitions>(filePath, out definitionsOB) || definitionsOB == null)
                            {
                                Log.Error($"'{fileName}': Couldn't deserialize, see SE log maybe there's something.");
                                continue;
                            }

                            if(definitionsOB.ParticleEffects.Length == 0)
                            {
                                Log.Error($"'{fileName}': Does not contain any valid particle effects.");
                                continue;
                            }

                            foreach(MyObjectBuilder_ParticleEffect particleOB in definitionsOB.ParticleEffects)
                            {
                                foundParticles.Add(new LoadParticleInfo(fileName, particleOB));
                            }
                        }
                        else
                        {
                            MyObjectBuilder_ParticleEffect particleOB = MyAPIGateway.Utilities.SerializeFromXML<MyObjectBuilder_ParticleEffect>(text);

                            if(particleOB == null)
                                throw new Exception("It got deserialized as ParticleEffect but returned null.");

                            foundParticles.Add(new LoadParticleInfo(filePath, particleOB));
                        }
                    }

                    if(foundParticles.Count <= 0)
                    {
                        Notifications.Show("Found 0 particle effects.", 5, Color.Yellow);
                        return;
                    }
                    else if(foundParticles.Count == 1)
                    {
                        LoadSingleParticleFromOB(foundParticles[0].OB);
                    }
                    else
                    {
                        foundParticles.Sort((a, b) => a.OB.Id.SubtypeName.CompareTo(b.OB.Id.SubtypeName));

                        // check duplicates which are always next to eachother because of the above sorting
                        for(int i = 1; i < foundParticles.Count; i++)
                        {
                            LoadParticleInfo piA = foundParticles[i - 1];
                            LoadParticleInfo piB = foundParticles[i];
                            if(piA.OB.Id.SubtypeName == piB.OB.Id.SubtypeName)
                            {
                                piA.Collides = true;
                                piB.Collides = true;
                            }
                        }

                        UICustomizablePopup screen = new UICustomizablePopup(closeButtonTooltip: "Cancels loading.", screenSize: new Vector2(0.5f, 0.8f));

                        HashSet<string> selectedParticles = new HashSet<string>();
                        List<MyGuiControlCheckbox> checkboxes = new List<MyGuiControlCheckbox>(foundParticles.Count);

                        screen.ControlGetter = (innerHost) =>
                        {
                            const string CollidingNamesTitle = "Same particle declared multiple times!";
                            const string CollidingNamesText = "At least one particle name is used by multiple definitions (the yellow colored ones)." +
                                                              "\nRecommended to check only one of them and use 'Load only selected'.";

                            var loadOverride = innerHost.CreateButton("Load all + override", "Loads all found particles and overrides if they match any existing ones.",
                                clicked: (b) =>
                                {
                                    if(foundParticles.Any(pi => pi.Collides))
                                    {
                                        PopupInfoAction(CollidingNamesTitle, CollidingNamesText,
                                            "Continue, load whichever", "Go back", (result) =>
                                            {
                                                if(result == MyGuiScreenMessageBox.ResultEnum.YES)
                                                    DoTheThing();
                                            });
                                    }
                                    else
                                    {
                                        DoTheThing();
                                    }

                                    void DoTheThing()
                                    {
                                        try
                                        {
                                            foreach(LoadParticleInfo pi in foundParticles)
                                            {
                                                AddOrReplaceParticleOB(pi.OB);
                                            }

                                            FinalizeLoading();
                                        }
                                        catch(Exception e)
                                        {
                                            Log.Error(e);
                                        }

                                        screen.CloseScreen();
                                    }
                                });

                            var loadNew = innerHost.CreateButton("Load only new", "Loads only particles that don't already exist in memory (no override).",
                                clicked: (b) =>
                                {
                                    if(foundParticles.Any(pi => pi.Collides))
                                    {
                                        PopupInfoAction(CollidingNamesTitle, CollidingNamesText,
                                            "Continue, load whichever", "Go back", (result) =>
                                            {
                                                if(result == MyGuiScreenMessageBox.ResultEnum.YES)
                                                    DoTheThing();
                                            });
                                    }
                                    else
                                    {
                                        DoTheThing();
                                    }

                                    void DoTheThing()
                                    {
                                        try
                                        {
                                            foreach(LoadParticleInfo pi in foundParticles)
                                            {
                                                if(!MyParticleEffectsLibrary.Exists(pi.OB.Id.SubtypeName))
                                                    AddOrReplaceParticleOB(pi.OB);
                                            }

                                            FinalizeLoading();
                                        }
                                        catch(Exception e)
                                        {
                                            Log.Error(e);
                                        }

                                        screen.CloseScreen();
                                    }
                                });

                            innerHost.PositionControls(loadOverride, loadNew);

                            innerHost.InsertSeparator();

                            var invertSelection = innerHost.CreateButton("Invert selection", "Uncheck all checked and check all unchecked.",
                            clicked: (b) =>
                            {
                                HashSet<string> inverted = new HashSet<string>();

                                foreach(LoadParticleInfo pi in foundParticles)
                                {
                                    if(!selectedParticles.Contains(pi.OB.Id.SubtypeName))
                                        inverted.Add(pi.OB.Id.SubtypeName);
                                }

                                selectedParticles = inverted;

                                foreach(MyGuiControlCheckbox cb in checkboxes)
                                {
                                    cb.IsChecked = selectedParticles.Contains((string)cb.UserData);
                                }
                            });

                            var loadSelected = innerHost.CreateButton("Load only selected", "Loads only the below selected particles, and overrides if any already exist.",
                                clicked: (b) =>
                                {
                                    if(selectedParticles.Count == 0)
                                    {
                                        PopupInfoAction("Nothing selected", "No particles were selected...",
                                            "Yes, load nothing", "Go back", (result) =>
                                            {
                                                if(result == MyGuiScreenMessageBox.ResultEnum.YES)
                                                    screen.CloseScreen();
                                            });
                                    }
                                    else
                                    {
                                        try
                                        {
                                            foreach(LoadParticleInfo pi in foundParticles)
                                            {
                                                if(selectedParticles.Contains(pi.OB.Id.SubtypeName))
                                                {
                                                    AddOrReplaceParticleOB(pi.OB);
                                                }
                                            }

                                            FinalizeLoading();
                                        }
                                        catch(Exception e)
                                        {
                                            Log.Error(e);
                                        }

                                        screen.CloseScreen();
                                    }
                                });

                            innerHost.PositionControls(invertSelection, loadSelected);

                            Vector2 size = new Vector2(innerHost.PanelSize.X - innerHost.Padding.X * 2 - EyeballedScrollbarWidth, 0f);
                            var scrollHost = new VerticalControlsHost(null, Vector2.Zero, size);
                            var scrollPanel = innerHost.CreateScrollableArea(scrollHost.Panel, new Vector2(innerHost.PanelSize.X - innerHost.Padding.X * 2, 0.62f));

                            innerHost.Add(scrollPanel);
                            innerHost.PositionAndFillWidth(scrollPanel);

                            scrollHost.Reset();

                            foreach(LoadParticleInfo pi in foundParticles)
                            {
                                MyObjectBuilder_ParticleEffect particleOB = pi.OB;
                                string name = particleOB.Id.SubtypeName;
                                bool exists = MyParticleEffectsLibrary.Exists(name);

                                (_, MyGuiControlCheckbox cb, MyGuiControlLabel label) = scrollHost.InsertCheckbox($"{name}{(exists ? " (exists)" : "")}", $"From file: {pi.FileName}", false, (v) =>
                                {
                                    if(v)
                                        selectedParticles.Add(name);
                                    else
                                        selectedParticles.Remove(name);
                                });

                                cb.UserData = name;
                                checkboxes.Add(cb);

                                if(pi.Collides)
                                    label.ColorMask = Color.Yellow;
                                else if(exists)
                                    label.ColorMask = Color.Gray;
                            }

                            EditorUI.FinalizeScrollable(scrollPanel, scrollHost.Panel, scrollHost);
                        };

                        screen.FinishSetup();

                        MyGuiSandbox.AddScreen(screen);
                    }
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            });

            void LoadSingleParticleFromOB(MyObjectBuilder_ParticleEffect particleOB)
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

                FinalizeLoading();
            }

            void FinalizeLoading()
            {
                if(lastLoadedData != null)
                {
                    SelectedParticle.Spawn(lastLoadedData.Name);
                    SelectedParticle.ParentDistance = Math.Max(2f, EstimateParticleTotalRadius(lastLoadedData) * 0.6f);
                    SelectedParticle.HasChanges = false;

                    CheckForZeroViewDistance(lastLoadedData);

                    ShowParticlePicker = false;
                    Editor.Backup.ShouldBackup = false;
                }

                if(loadedCount > 0)
                {
                    Notifications.Show($"Loaded {loadedCount} particle effects. Selected: {lastLoadedData.Name}", 5);
                }
                else
                {
                    Notifications.Show("Loaded 0 particle effects.", 5, Color.Yellow);
                }

                RefreshUI();
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

                lastLoadedData = data;
                loadedCount++;
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
                Notifications.Show($"Particle '{data.Name}' had ViewDistance 0 or negative. Was reset to default: {data.DistanceMax}.", 5, Color.Yellow);
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
