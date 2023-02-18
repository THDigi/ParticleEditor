using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Digi.ParticleEditor.GameData;
using Sandbox.Graphics.GUI;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using VRageRender.Animations;

namespace Digi.ParticleEditor.UIControls
{
    public class UIAnimatedValueEditor : MyGuiScreenBase, IScreenAllowHotkeys
    {
        PropId PropId;
        object PropHost;
        PropertyData PropInfo;
        string PropFriendlyName;
        GenerationProperty OriginalData;

        public string Tooltip;
        public bool Is2D;
        public Action<GenerationProperty> SaveData;
        public Func<GenerationProperty> GetData;
        public Func<IMyConstProperty> GetProp;

        public bool UseValueAsKeyColor = false;

        VerticalControlsHost Host;
        VerticalControlsHost ScrollHost;
        MyGuiControlScrollablePanel ScrollablePanel;

        MyGuiControlContextMenu ContextMenu;
        bool FocusOnDropDown;

        GenerationProperty SavedDataOB;

        MyGuiControlButton _applyButton;
        bool _changesMade = false;
        bool ChangesMade
        {
            get => _changesMade;
            set
            {
                if(_applyButton != null)
                    _applyButton.ColorMask = value ? Color.Lime : Color.White;

                _changesMade = value;
            }
        }

        Dictionary<int, bool> SingleValueMode = new Dictionary<int, bool>();

        static bool Maximized = false;

        static readonly Vector2 WindowPosition = new Vector2(0.5f, 1f);

        public override string GetFriendlyName() => nameof(UIAnimatedValueEditor);

        public UIAnimatedValueEditor(object propHost, string propInternalName, string propFriendlyName, string tooltip, bool is2D)
            : base(WindowPosition, isTopMostScreen: false)
        {
            // TODO convert to a sub-UI of editorUI so that I can show status UI top-left... and also potentially leave right side shown? /shrug

            PropHost = propHost;
            PropId = new PropId(propHost, propInternalName);
            PropInfo = VersionSpecificInfo.GetPropertyInfo(PropId);
            if(PropInfo == null)
                PropInfo = VersionSpecificInfo.DefaultPropInfo;

            EditorUI.GetOriginalPropData(propHost, propInternalName, out OriginalData);

            PropFriendlyName = propFriendlyName;
            Tooltip = tooltip;
            Is2D = is2D;
            Align = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM;

            m_closeOnEsc = true;
            CanBeHidden = true;
            CanHideOthers = false;
            EnabledBackgroundFade = false;
            CloseButtonEnabled = false;

            ChangesMade = false;
            SavedDataOB = null;
        }

        public void FinishSetup()
        {
            LoadFrom(GetData.Invoke());
        }

        public override void RecreateControls(bool constructor)
        {
            Vector2 windowSize = new Vector2(1f, Maximized ? 1f : 0.25f);
            Size = windowSize;
            Host = new VerticalControlsHost(this, new Vector2(windowSize.X / -2f, -windowSize.Y), windowSize, drawBackground: true);

            if(ContextMenu != null)
            {
                ContextMenu.Deactivate();
                ContextMenu = null;
            }

            base.RecreateControls(constructor);
            Host.Reset();

            ContextMenu = new MyGuiControlContextMenu();
            ContextMenu.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM;
            ContextMenu.Deactivate();
            ContextMenu.ItemClicked += (c, args) =>
            {
                Action action = args.UserData as Action;
                action?.Invoke();
            };

            ContextMenu.OnDeactivated += () =>
            {
                //foreach(MyGuiControlBase c in ScrollHost.Panel.Controls)
                //{
                //    if(c is UITimeline)
                //    {
                //        c.Enabled = true;
                //    }
                //}
            };

            string title;
            if(Is2D)
                title = $"{PropFriendlyName} - 2D animated property";
            else
                title = $"{PropFriendlyName} - 1D animated property";


            //MyGuiControlButton xmlButton = Host.CreateButton("XML", "Open XML editor for this property only.", clicked: (b) =>
            //{
            //});
            //MyGuiControlButton xmlButton = Host.SideXmlButton(PropId, GetProp());
            //xmlButton.Size = new Vector2(0.05f, xmlButton.Size.Y);

            _applyButton = Host.CreateButton("Apply", "Applies changes without closing window.", clicked: (b) =>
            {
                ApplyChanges();
            });

            MyGuiControlButton resizeButton = Host.CreateButton(Maximized ? "Smaller" : "Maximize", "Toggle between a fullscreen window to a smaller one.", clicked: (b) =>
            {
                Maximized = !Maximized;
                RefreshUI();
            });

            MyGuiControlButton closeButton = Host.CreateButton("Close", "Close and ignore any changes (escape key also does this)", clicked: (b) =>
            {
                if(ChangesMade)
                {
                    EditorUI.PopupConfirmation("Changes made, apply them before closing?",
                        confirmed: () =>
                        {
                            ApplyChanges(refreshUI: false);
                            CloseScreen();
                        },
                        declined: () =>
                        {
                            CloseScreen();
                        });
                }
                else
                {
                    CloseScreen();
                }
            });

            _applyButton.Size = new Vector2(0.1f, _applyButton.Size.Y);
            resizeButton.Size = new Vector2(0.1f, resizeButton.Size.Y);
            closeButton.Size = new Vector2(0.1f, closeButton.Size.Y);

            //MyGuiControlLabel spacer = Host.CreateLabel("");
            //spacer.Size = new Vector2(0.02f, 0.001f);

            //Host.StackRight(xmlButton, spacer, applyButton, resizeButton, closeButton);

            Host.StackRight(_applyButton, resizeButton, closeButton);

            Host.UndoLastVerticalShift();

            Host.MoveY(Host.Padding.Y);

            MyGuiControlLabel labelTitle = Host.CreateLabel(title);
            labelTitle.SetToolTip(Tooltip);

            MyGuiControlButton helpButton = Host.CreateButton("Help", (Is2D ? VersionSpecificInfo.AnimatedProp2DHelp : VersionSpecificInfo.AnimatedProp1DHelp));

            MyGuiControlLabel labelMinKeys = Host.CreateLabel(Is2D ? $"Min keys: {PropInfo.RequiredKeys1D} vertical, {PropInfo.RequiredKeys2D} horizontal" : $"Min keys: {PropInfo.RequiredKeys1D} horizontal");
            labelMinKeys.SetToolTip("The minimum amount of keys on each axis that this property requires to not crash the game.\nThese values are enforced on apply but not enforced in XML editor.");

            Host.PositionControlsNoSize(labelTitle, helpButton, labelMinKeys);

            Vector2 scrollAreaSize = new Vector2(windowSize.X - EditorUI.EyeballedScrollbarWidth - Host.Padding.X * 2, 0);

            ScrollHost = new VerticalControlsHost(null, Vector2.Zero, scrollAreaSize, padding: new Vector2(0.005f, 0.002f));

            ScrollablePanel = Host.CreateScrollableArea(ScrollHost.Panel, new Vector2(windowSize.X, windowSize.Y - Host.CurrentHeight - (Host.Padding.Y * 2)));

            Host.Add(ScrollablePanel);
            Host.PositionAndFillWidth(ScrollablePanel);

            Controls.Add(ContextMenu);

            ChangesMade = ChangesMade; // update button's color when UI gets re-created
        }

        void ApplyChanges(bool refreshUI = true)
        {
            string defaultReason = "it can crash the game.";

            int keys1D = SavedDataOB.Keys.Count;
            if(keys1D < PropInfo.RequiredKeys1D)
            {
                Notifications.Show($"The vertical timeline has {keys1D} keys, should have at least {PropInfo.RequiredKeys1D}\nReason: {PropInfo.RequiredKeys1DReason ?? defaultReason}", 5, Color.Red);
                //EditorUI.PopupConfirmation($"The vertical timeline has {keys1D} keys, should have at least {PropInfo.RequiredKeys1D} or it will crash the game!\nApply anyway?", () => DoApply(), focusOnNo: true);
                return;
            }

            if(Is2D && keys1D > 0)
            {
                int keys2Dmin = SavedDataOB.Keys.Min(k => k.Value2D.Keys.Count);
                if(keys2Dmin < PropInfo.RequiredKeys2D)
                {
                    Notifications.Show($"One horizontal timeline has {keys2Dmin} keys, should have at least {PropInfo.RequiredKeys2D}\nReason: {PropInfo.RequiredKeys2DReason ?? defaultReason}", 5, Color.Red);
                    //EditorUI.PopupConfirmation($"One horizontal timeline has {keys2Dmin} keys, should have at least {PropInfo.RequiredKeys2D} or it will crash the game!\nApply anyway?", () => DoApply(), focusOnNo: true);
                    return;
                }
            }

            DoApply();

            void DoApply()
            {
                bool collidingTimes1D = false;
                bool collidingTimes2D = false;

                SavedDataOB.Keys.Sort((k1, k2) => k1.Time.CompareTo(k2.Time));

                HashSet<float> existingTimes = new HashSet<float>();

                foreach(AnimationKey key1D in SavedDataOB.Keys)
                {
                    if(!existingTimes.Add(key1D.Time))
                    {
                        collidingTimes1D = true;
                        break;
                    }
                }

                if(Is2D)
                {
                    foreach(AnimationKey key1D in SavedDataOB.Keys)
                    {
                        key1D.Value2D.Keys.Sort((k1, k2) => k1.Time.CompareTo(k2.Time));

                        if(!collidingTimes2D)
                        {
                            existingTimes.Clear();

                            foreach(AnimationKey key2D in key1D.Value2D.Keys)
                            {
                                if(!existingTimes.Add(key2D.Time))
                                {
                                    collidingTimes2D = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                if(collidingTimes1D || collidingTimes2D)
                {
                    if(collidingTimes1D && collidingTimes2D)
                    {
                        EditorUI.PopupConfirmation("WARNING: Time values for both timeline axis have duplicated values!\nThis will cause some to get deleted!\nDo you really wish to save?", DoSave, focusNoButton: true);
                    }
                    else
                    {
                        if(collidingTimes1D)
                            EditorUI.PopupConfirmation("WARNING: Duplicated time values for the vertical timeline!\nThe other key(s) will get deleted!\nDo you really wish to save?", DoSave, focusNoButton: true);
                        else
                            EditorUI.PopupConfirmation("WARNING: Duplicated time values for a horizontal timeline!\nThe other key(s) will get deleted!\nDo you really wish to save?", DoSave, focusNoButton: true);
                    }
                }
                else
                {
                    DoSave();
                }

                void DoSave()
                {
                    // TODO: manually count the keys without duplicates and use that for RequiredKeysND!

                    SaveData?.Invoke(SavedDataOB);

                    ChangesMade = false;

                    if(refreshUI)
                        LoadFrom(GetData.Invoke());
                }
            }
        }

        void RefreshUI() => LoadFrom(SavedDataOB);

        void LoadFrom(GenerationProperty propOB)
        {
            RecreateControls(true);

            if(propOB.AnimationType == PropertyAnimationType.Animated2D)
            {
                if(!Is2D)
                    throw new Exception($"Property '{propOB.Name}' is supposed to be 2D but got AnimationType={propOB.AnimationType}; Type={propOB.Type}");
            }
            else if(propOB.AnimationType == PropertyAnimationType.Animated)
            {
                if(Is2D)
                    throw new Exception($"Property '{propOB.Name}' is NOT supposed to be 2D but got AnimationType={propOB.AnimationType}; Type={propOB.Type}");
            }
            else
                throw new Exception($"Property '{propOB.Name}' has unsupported AnimationType={propOB.AnimationType}; Type={propOB.Type}");

            SavedDataOB = propOB;

            try
            {
                GenerateScrollContent();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void GenerateScrollContent()
        {
            for(int _idx = 0; _idx < SavedDataOB.Keys.Count; _idx++)
            {
                int index = _idx; // required for safe capture
                AnimationKey animKey = SavedDataOB.Keys[index];

                MyGuiControlButton deleteButton = ScrollHost.CreateButton("X", "Deletes this key (horizontal line)", clicked: (b) =>
                {
                    SavedDataOB.Keys.RemoveAt(index);
                    ChangesMade = true;
                    RefreshUI();
                });

                float time = SavedDataOB.Keys[index].Time;

                MyGuiControlLabel timeLabel = ScrollHost.CreateLabel("Time:");

                UINumberBox timeControl = ScrollHost.CreateNumberBox(
                    "Time at which the value(s) on the right will be used." +
                    "\nRanges from 0 to particle duration.",
                    time, 0, 0, float.MaxValue, inputRound: 6, dragRound: 2, changed: (value) =>
                    {
                        AnimationKey key = SavedDataOB.Keys[index];
                        key.Time = value;
                        SavedDataOB.Keys[index] = key;
                        ChangesMade = true;
                    });

                deleteButton.Size = new Vector2(EditorUI.ButtonSize.Y);
                timeControl.Size = new Vector2(ScrollHost.AvailableWidth * (5f / 50f), timeControl.Size.Y);

                bool show1Dedit = !Is2D;

                const int KeysToUse = 4;

                MyGuiControlBase editModeButton = null;

                if(Is2D)
                {
                    bool? singleValue = null;

                    {
                        if(SingleValueMode.TryGetValue(index, out bool mode))
                            singleValue = mode;
                    }

                    if(singleValue == null && animKey.Value2D.Keys.Count == KeysToUse)
                    {
                        switch(SavedDataOB.Type)
                        {
                            case "Vector4": singleValue = animKey.Value2D.Keys.All(k => k.ValueVector4 == animKey.Value2D.Keys[0].ValueVector4); break;
                            case "Vector3": singleValue = animKey.Value2D.Keys.All(k => k.ValueVector3 == animKey.Value2D.Keys[0].ValueVector3); break;
                            case "Float": singleValue = animKey.Value2D.Keys.All(k => k.ValueFloat == animKey.Value2D.Keys[0].ValueFloat); break;
                            default: singleValue = false; break;
                        }
                    }

                    string buttonTitle;
                    string buttonTooltip;
                    float buttonWidth;

                    if(singleValue == null || singleValue == false)
                    {
                        buttonWidth = 0.02f;
                        buttonTitle = "S";
                        buttonTooltip = "Switch to single value editor to edit one value that gets copied in 4 keys on the hidden timeline.";
                    }
                    else
                    {
                        buttonWidth = 0.08f;
                        buttonTitle = "Timeline";
                        buttonTooltip = "Switch back to a timeline editor which allows values over particle lifetime.";
                    }

                    editModeButton = ScrollHost.CreateButton(buttonTitle, buttonTooltip,
                        clicked: (b) =>
                        {
                            if(SingleValueMode.TryGetValue(index, out bool mode))
                                SingleValueMode[index] = !mode;
                            else
                                SingleValueMode[index] = (singleValue.HasValue ? !singleValue.Value : true);

                            RefreshUI();
                        });

                    editModeButton.Size = new Vector2(buttonWidth, editModeButton.Size.Y);

                    if(singleValue == null || singleValue == false)
                    {
                        UITimeline timeline = CreateTimeline(index, animKey.Value2D.Keys);
                        timeline.Size = new Vector2(ScrollHost.AvailableWidth * (39f / 50f), timeline.Size.Y);
                        ScrollHost.PositionControlsNoSize(deleteButton, timeLabel, timeControl, timeline, editModeButton);

                        timeControl.PositionY -= 0.005f; // HACK: fixing the box being too low compared to timeline
                    }
                    else
                    {
                        show1Dedit = true;
                    }
                }

                if(show1Dedit)
                {
                    MyGuiControlLabel valueLabel = ScrollHost.CreateLabel("Value: ");

                    int idx = index; // because it gets modified (set to 0 below) it needs to remain isolated from `index` which is used in other capture.
                    string tooltip = null;
                    List<AnimationKey> keysRef = SavedDataOB.Keys;
                    if(Is2D)
                    {
                        keysRef = SavedDataOB.Keys[idx].Value2D.Keys;

                        if(keysRef.Count == 0)
                        {
                            AnimationKey key = GetNewKey(0f, null);
                            for(int i = 0; i < 4; i++)
                            {
                                key.Time = (1 / 3f * i);
                                keysRef.Add(key);
                            }
                        }

                        idx = 0;
                        tooltip = "Reminder that this is originally a timeline (animated 2D), this single value editor will set all 4 keys to the same value.";
                    }

                    if(editModeButton == null)
                        editModeButton = ScrollHost.CreateLabel("");

                    switch(SavedDataOB.Type)
                    {
                        case "Vector4":
                        {
                            Vector4 val = (keysRef.Count > idx ? keysRef[idx].ValueVector4 : PropInfo.ValueRangeVector4.Default ?? Vector4.One);

                            Vector4? def = PropInfo.ValueRangeVector4.Default;
                            if(EditorUI.GetOriginalPropData(PropHost, PropId.Name, out GenerationProperty originalData))
                                def = OriginalData.ValueVector4;

                            int inputRound = PropInfo.ValueRangeVector4.InputRounding;
                            int dragRound = PropInfo.ValueRangeVector4.Rounding;

                            List<MyGuiControlBase> controls = new List<MyGuiControlBase>(16);

                            controls.Add(deleteButton);
                            controls.Add(timeLabel);
                            controls.Add(timeControl);
                            controls.Add(valueLabel);

                            if(UseValueAsKeyColor)
                            {
                                (MyGuiControlParent previewParent, Action<Vector4> updateColor) = ScrollHost.CreateColorPreview(val);

                                controls.Add(previewParent);

                                Vector4 min = Vector4.Zero;
                                Vector4 max = new Vector4(float.MaxValue);

                                for(int _d = 0; _d < 4; _d++)
                                {
                                    int dim = _d; // for reliable capture

                                    MyGuiControlLabel label = ScrollHost.CreateLabel(EditorUI.Vector4ColorNames[dim]);

                                    UINumberBox box = ScrollHost.CreateNumberBox(tooltip,
                                        val.GetDim(dim), (def == null ? (float?)null : def.Value.GetDim(dim)), min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                                        (value) =>
                                        {
                                            AnimationKey key = (keysRef.Count > idx ? keysRef[idx] : new AnimationKey());

                                            key.ValueVector4 = key.ValueVector4.GetChangedDim(dim, value);

                                            updateColor(key.ValueVector4);

                                            if(Is2D)
                                            {
                                                keysRef.Clear();
                                                for(int i = 0; i < KeysToUse; i++)
                                                {
                                                    keysRef.Add(key);
                                                    key.Time += 1f / (KeysToUse - 1);
                                                }
                                            }
                                            else
                                            {
                                                keysRef[idx] = key;
                                            }

                                            ChangesMade = true;
                                        });

                                    controls.Add(label);
                                    controls.Add(box);
                                }
                            }
                            else
                            {
                                Vector4 min = new Vector4(float.MinValue);
                                Vector4 max = new Vector4(float.MaxValue);

                                for(int _d = 0; _d < 4; _d++)
                                {
                                    int dim = _d; // for reliable capture

                                    MyGuiControlLabel label = ScrollHost.CreateLabel(EditorUI.Vector4AxisNames[dim]);

                                    UINumberBox box = ScrollHost.CreateNumberBox(tooltip,
                                        val.GetDim(dim), (def == null ? (float?)null : def.Value.GetDim(dim)), min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                                        (value) =>
                                        {
                                            AnimationKey key = (keysRef.Count > idx ? keysRef[idx] : new AnimationKey());

                                            key.ValueVector4 = key.ValueVector4.GetChangedDim(dim, value);

                                            if(Is2D)
                                            {
                                                keysRef.Clear();
                                                for(int i = 0; i < KeysToUse; i++)
                                                {
                                                    keysRef.Add(key);
                                                    key.Time += 1f / (KeysToUse - 1);
                                                }
                                            }
                                            else
                                            {
                                                keysRef[idx] = key;
                                            }

                                            ChangesMade = true;
                                        });

                                    controls.Add(label);
                                    controls.Add(box);
                                }
                            }

                            controls.Add(editModeButton);

                            ScrollHost.PositionControlsNoSize(controls.ToArray());
                            break;
                        }

                        case "Vector3":
                        {
                            Vector3 val = (keysRef.Count > idx ? keysRef[idx].ValueVector3 : PropInfo.ValueRangeVector3.Default ?? Vector3.One);

                            Vector3 min = Vector3.MinValue;
                            Vector3 max = Vector3.MaxValue;

                            Vector3? def = PropInfo.ValueRangeVector3.Default;
                            if(EditorUI.GetOriginalPropData(PropHost, PropId.Name, out GenerationProperty originalData))
                                def = OriginalData.ValueVector3;

                            int inputRound = PropInfo.ValueRangeVector3.InputRounding;
                            int dragRound = PropInfo.ValueRangeVector3.Rounding;

                            List<MyGuiControlBase> controls = new List<MyGuiControlBase>(16);
                            controls.Add(deleteButton);
                            controls.Add(timeLabel);
                            controls.Add(timeControl);
                            controls.Add(valueLabel);

                            for(int _d = 0; _d < 3; _d++)
                            {
                                int dim = _d; // for reliable capture

                                MyGuiControlLabel label = ScrollHost.CreateLabel(EditorUI.Vector3AxisNames[dim]);

                                UINumberBox box = ScrollHost.CreateNumberBox(tooltip,
                                    val.GetDim(dim), (def == null ? (float?)null : def.Value.GetDim(dim)), min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                                    (value) =>
                                    {
                                        AnimationKey key = (keysRef.Count > idx ? keysRef[idx] : new AnimationKey());
                                        Vector3 vec = key.ValueVector3;

                                        vec.SetDim(dim, value);
                                        key.ValueVector3 = vec;

                                        if(Is2D)
                                        {
                                            keysRef.Clear();
                                            for(int i = 0; i < KeysToUse; i++)
                                            {
                                                keysRef.Add(key);
                                                key.Time += 1f / (KeysToUse - 1);
                                            }
                                        }
                                        else
                                        {
                                            keysRef[idx] = key;
                                        }

                                        ChangesMade = true;
                                    });

                                controls.Add(label);
                                controls.Add(box);
                            }

                            controls.Add(editModeButton);

                            ScrollHost.PositionControlsNoSize(controls.ToArray());
                            break;
                        }

                        case "Float":
                        {
                            float? def = PropInfo.ValueRangeNum.Default;
                            if(EditorUI.GetOriginalPropData(PropHost, PropId.Name, out GenerationProperty originalData))
                                def = OriginalData.ValueFloat;

                            float val = (keysRef.Count > idx ? keysRef[idx].ValueFloat : PropInfo.ValueRangeNum.Default ?? 0f);

                            float min = (PropInfo.ValueRangeNum.Min == 0 ? 0 : float.MinValue);
                            float max = float.MaxValue;

                            int inputRound = PropInfo.ValueRangeNum.InputRounding;
                            int dragRound = PropInfo.ValueRangeNum.Rounding;

                            UINumberBox box = ScrollHost.CreateNumberBox(tooltip,
                                val, def, min, max, inputRound, dragRound,
                                (value) =>
                                {
                                    AnimationKey key = (keysRef.Count > idx ? keysRef[idx] : new AnimationKey());
                                    key.ValueFloat = value;

                                    if(Is2D)
                                    {
                                        keysRef.Clear();
                                        for(int i = 0; i < KeysToUse; i++)
                                        {
                                            keysRef.Add(key);
                                            key.Time += 1f / (KeysToUse - 1);
                                        }
                                    }
                                    else
                                    {
                                        keysRef[idx] = key;
                                    }

                                    ChangesMade = true;
                                });

                            ScrollHost.PositionControlsNoSize(deleteButton, timeLabel, timeControl, valueLabel, box, editModeButton);
                            break;
                        }

                        default:
                        {
                            ScrollHost.PositionControlsNoSize(deleteButton, timeLabel, timeControl, valueLabel, ScrollHost.CreateLabel($"Unsupported value type to edit as animated: {SavedDataOB.Type}"), editModeButton);
                            break;
                        }
                    }

                    // HACK: fixing buttons being too high
                    deleteButton.PositionY += 0.005f;
                    editModeButton.PositionY += 0.005f;
                }
            }

            MyGuiControlButton addKeyButton = ScrollHost.CreateButton("Add key",
                clicked: (b) =>
                {
                    List<AnimationKey> keysRef = new List<AnimationKey>();

                    float time = 0f;
                    if(SavedDataOB.Keys.Count > 0)
                        time = SavedDataOB.Keys.Max(k => k.Time) + 5f;

                    SavedDataOB.Keys.Add(new AnimationKey()
                    {
                        Time = time,
                        ValueType = SavedDataOB.Type,
                        Value2D = new Generation2DProperty()
                        {
                            Keys = keysRef,
                        },
                    });

                    ChangesMade = true;
                    RefreshUI();
                    ScrollablePanel.SetVerticalScrollbarValue(99999); // scroll to bottom
                });
            addKeyButton.Size = new Vector2(0.2f, addKeyButton.Size.Y);
            ScrollHost.MoveY(addKeyButton.Size.Y / 3f);
            ScrollHost.PositionControlsNoSize(addKeyButton);

            MyGuiControlParent content = ScrollHost.Panel;
            content.Size = new Vector2(content.Size.X, ScrollHost.CurrentPosition.Y);

            // offset controls up by half content's Y size
            foreach(MyGuiControlBase control in content.Controls)
            {
                control.PositionY -= ScrollHost.CurrentPosition.Y / 2f;
            }

            ScrollablePanel.RefreshInternals();
        }

        AnimationKey GetNewKey(float time, object value)
        {
            AnimationKey key = new AnimationKey();
            key.Time = time;

            switch(SavedDataOB.Type)
            {
                case "Vector4":
                {
                    if(value is Vector4)
                        key.ValueVector4 = (Vector4)value;
                    else
                        key.ValueVector4 = PropInfo.ValueRangeVector4.Default ?? (UseValueAsKeyColor ? new Vector4(1, 0, 0.5f, 1) : Vector4.One);
                    break;
                }
                case "Vector3":
                {
                    if(value is Vector3)
                        key.ValueVector3 = (Vector3)value;
                    else
                        key.ValueVector3 = PropInfo.ValueRangeVector3.Default ?? Vector3.Zero;
                    break;
                }
                case "Float":
                {
                    if(value is float)
                        key.ValueFloat = (float)value;
                    else
                        key.ValueFloat = PropInfo.ValueRangeNum.Default ?? 1f;
                    break;
                }
            }

            return key;
        }

        UITimeline CreateTimeline(int index2D, List<AnimationKey> keysRef)
        {
            UITimeline timeline = new UITimeline(Host.CurrentPosition);

            timeline.GetKeys = () => keysRef.Select(a => a.Time);

            timeline.AddKey = (time, value) =>
            {
                keysRef.Add(GetNewKey(time, value));
                ChangesMade = true;
            };

            timeline.RemoveKey = (idx) =>
            {
                keysRef.RemoveAt(idx);
                ChangesMade = true;
            };

            timeline.SetKey = (idx, time) =>
            {
                AnimationKey key = keysRef[idx];
                key.Time = time;
                keysRef[idx] = key;

                ChangesMade = true;
            };

            timeline.GetKeyValue = (idx) =>
            {
                if(keysRef.Count <= idx || idx < 0)
                {
                    Log.Error($"keys desync? keysRef ({keysRef.Count}): {string.Join(" / ", keysRef)}");
                    return null;
                }

                switch(SavedDataOB.Type)
                {
                    case "Vector4": return keysRef[idx].ValueVector4;
                    case "Vector3": return keysRef[idx].ValueVector3;
                    case "Float": return keysRef[idx].ValueFloat;
                }
                return null;
            };

            timeline.GetInterpolatedValue = (aimedTime, idxLeft, idxRight) =>
            {
                AnimationKey keyLeft = keysRef[idxLeft];
                AnimationKey keyRight = keysRef[idxRight];

                // ratio between the 2 keys where aimedTime is located
                float ratio = (aimedTime - keyLeft.Time) / (keyRight.Time - keyLeft.Time);

                object value = null;
                switch(SavedDataOB.Type)
                {
                    case "Vector4": value = Vector4.Lerp(keyLeft.ValueVector4, keyRight.ValueVector4, ratio); break;
                    case "Vector3": value = Vector3.Lerp(keyLeft.ValueVector3, keyRight.ValueVector3, ratio); break;
                    case "Float": value = MathHelper.Lerp(keyLeft.ValueFloat, keyRight.ValueFloat, ratio); break;
                    default: Log.Error($"Unsupported value type for interpolation: {SavedDataOB.Type}"); break;
                }
                return value;
            };

            timeline.EditKeyValueGUI = (idx) =>
            {
                string closeButtonTooltip = "Changes are stored into memory which you can then apply in the timeline editor";

                switch(SavedDataOB.Type)
                {
                    case "Vector4":
                    {
                        UICustomizablePopup screen = new UICustomizablePopup(closeButtonTooltip);

                        screen.ControlGetter = (host) =>
                        {
                            Vector4 v = keysRef[idx].ValueVector4;

                            ValueInfo<Vector4> valueRange = PropInfo.ValueRangeVector4;

                            Vector4? def = valueRange.Default;
                            Vector4 min = (valueRange.Min == Vector4.Zero ? Vector4.Zero : new Vector4(float.MinValue));
                            Vector4 max = new Vector4(float.MaxValue);
                            if(valueRange.LimitNumberBox)
                            {
                                min = valueRange.Min;
                                max = valueRange.Max;
                            }

                            int dragRound = valueRange.Rounding;
                            int inputRound = valueRange.InputRounding;

                            if(UseValueAsKeyColor)
                            {
                                host.UndoLastVerticalShift();

                                (MyGuiControlParent previewParent, Action<Vector4> updateColor) = host.CreateColorPreview(v, size: new Vector2(0.3f, 0.025f));

                                host.PositionControlsNoSize(previewParent);

                                MyGuiControlBase[] controls = new MyGuiControlBase[4 * 2];

                                for(int i = 0; i < 4; i++)
                                {
                                    int dim = i; // required for reliable capture

                                    MyGuiControlLabel dimLabel = host.CreateLabel(EditorUI.Vector4ColorNames[dim]);

                                    UINumberBox box = host.CreateNumberBox(null,
                                        v.GetDim(dim), (def == null ? (float?)null : def.Value.GetDim(dim)),
                                        min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                                        (value) =>
                                        {
                                            AnimationKey key = keysRef[idx];
                                            Vector4 vec = key.ValueVector4;
                                            vec.SetDim(dim, value);
                                            key.ValueVector4 = vec;
                                            keysRef[idx] = key;

                                            updateColor(vec);

                                            ChangesMade = true;
                                        });

                                    float boxWidth = 0.085f - dimLabel.Size.X - host.ControlSpacing;
                                    box.Size = new Vector2(boxWidth, box.Size.Y);

                                    controls[i * 2] = dimLabel;
                                    controls[i * 2 + 1] = box;
                                }

                                host.PositionControlsNoSize(controls);

                                /*
                                host.InsertMultiSlider(SavedDataOB.Name, VersionSpecificInfo.ColorSlidersTooltip,
                                    EditorUI.Vector4ColorNames,
                                    new float[] { 0, 0, 0, 0 },
                                    new float[] { 1, 1, 1, 1 },
                                    new float[] { v.X, v.Y, v.Z, v.W },
                                    null,
                                    2, (dim, value) =>
                                    {
                                        AnimationKey key = keysRef[idx];
                                        Vector4 vec = key.ValueVector4;

                                        switch(dim)
                                        {
                                            case 0: vec = new Vector4(value, vec.Y, vec.Z, vec.W); break;
                                            case 1: vec = new Vector4(vec.X, value, vec.Z, vec.W); break;
                                            case 2: vec = new Vector4(vec.X, vec.Y, value, vec.W); break;
                                            case 3: vec = new Vector4(vec.X, vec.Y, vec.Z, value); break;
                                        }

                                        key.ValueVector4 = vec;
                                        keysRef[idx] = key;

                                        updateColor(vec);

                                        ChangesMade = true;
                                    });
                                */
                            }
                            else
                            {
                                MyGuiControlBase[] controls = new MyGuiControlBase[4 * 2];

                                for(int i = 0; i < 4; i++)
                                {
                                    int dim = i; // required for reliable capture

                                    MyGuiControlLabel dimLabel = host.CreateLabel(EditorUI.Vector4AxisNames[dim]);

                                    UINumberBox box = host.CreateNumberBox(null,
                                        v.GetDim(dim), (def == null ? (float?)null : def.Value.GetDim(dim)),
                                        min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                                        (value) =>
                                        {
                                            AnimationKey key = keysRef[idx];
                                            Vector4 vec = key.ValueVector4;
                                            vec.SetDim(dim, value);
                                            key.ValueVector4 = vec;
                                            keysRef[idx] = key;

                                            ChangesMade = true;
                                        });

                                    float boxWidth = 0.085f - dimLabel.Size.X - host.ControlSpacing;
                                    box.Size = new Vector2(boxWidth, box.Size.Y);

                                    controls[i * 2] = dimLabel;
                                    controls[i * 2 + 1] = box;
                                }

                                host.PositionControlsNoSize(controls);

                                /*
                                host.InsertMultiSlider(SavedDataOB.Name, null,
                                    EditorUI.Vector4AxisNames,
                                    new float[] { min.X, min.Y, min.Z, min.W },
                                    new float[] { max.X, max.Y, max.Z, max.W },
                                    new float[] { v.X, v.Y, v.Z, v.W },
                                    (def == null ? null : new float[] { def.Value.X, def.Value.Y, def.Value.Z, def.Value.W }),
                                    2, (dim, value) =>
                                    {
                                        AnimationKey key = keysRef[idx];
                                        Vector4 vec = key.ValueVector4;

                                        switch(dim)
                                        {
                                            case 0: vec = new Vector4(value, vec.Y, vec.Z, vec.W); break;
                                            case 1: vec = new Vector4(vec.X, value, vec.Z, vec.W); break;
                                            case 2: vec = new Vector4(vec.X, vec.Y, value, vec.W); break;
                                            case 3: vec = new Vector4(vec.X, vec.Y, vec.Z, value); break;
                                        }

                                        key.ValueVector4 = vec;
                                        keysRef[idx] = key;

                                        ChangesMade = true;
                                    });
                                */
                            }
                        };

                        screen.FinishSetup();

                        MyGuiSandbox.AddScreen(screen);
                        break;
                    }

                    case "Vector3":
                    {
                        UICustomizablePopup screen = new UICustomizablePopup(closeButtonTooltip);

                        screen.ControlGetter = (host) =>
                        {
                            Vector3 v = keysRef[idx].ValueVector3;

                            ValueInfo<Vector3> valueRange = PropInfo.ValueRangeVector3;

                            Vector3? def = valueRange.Default;
                            Vector3 min = (valueRange.Min == Vector3.Zero ? Vector3.Zero : Vector3.MaxValue);
                            Vector3 max = Vector3.MaxValue;
                            if(valueRange.LimitNumberBox)
                            {
                                min = valueRange.Min;
                                max = valueRange.Max;
                            }

                            int dragRound = valueRange.Rounding;
                            int inputRound = valueRange.InputRounding;

                            MyGuiControlBase[] controls = new MyGuiControlBase[3 * 2];

                            for(int i = 0; i < 3; i++)
                            {
                                int dim = i; // required for reliable capture

                                MyGuiControlLabel dimLabel = host.CreateLabel(EditorUI.Vector3AxisNames[dim]);

                                UINumberBox box = host.CreateNumberBox(null,
                                    v.GetDim(dim), (def == null ? (float?)null : def.Value.GetDim(dim)),
                                    min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                                    (value) =>
                                    {
                                        AnimationKey key = keysRef[idx];
                                        Vector4 vec = key.ValueVector4;
                                        vec.SetDim(dim, value);
                                        key.ValueVector4 = vec;
                                        keysRef[idx] = key;

                                        ChangesMade = true;
                                    });

                                float boxWidth = 0.1f - dimLabel.Size.X - host.ControlSpacing;
                                box.Size = new Vector2(boxWidth, box.Size.Y);

                                controls[i * 2] = dimLabel;
                                controls[i * 2 + 1] = box;
                            }

                            host.PositionControlsNoSize(controls);

                            /*
                            host.InsertMultiSlider(SavedDataOB.Name, null,
                                EditorUI.Vector3AxisNames,
                                new float[] { min.X, min.Y, min.Z, },
                                new float[] { max.X, max.Y, max.Z },
                                new float[] { v.X, v.Y, v.Z },
                                (def == null ? null : new float[] { def.Value.X, def.Value.Y, def.Value.Z }),
                                2, (dim, value) =>
                            {
                                AnimationKey key = keysRef[idx];
                                Vector3 vec = key.ValueVector3;
                                vec.SetDim(dim, value);
                                key.ValueVector3 = vec;
                                keysRef[idx] = key;

                                ChangesMade = true;
                            }, EditorUI.Vector3AxisTooltips);
                            */
                        };

                        screen.FinishSetup();

                        MyGuiSandbox.AddScreen(screen);
                        break;
                    }

                    case "Float":
                    {
                        UICustomizablePopup screen = new UICustomizablePopup(closeButtonTooltip);

                        screen.ControlGetter = (host) =>
                        {
                            float val = keysRef[idx].ValueFloat;

                            ValueInfo<float> valueRange = PropInfo.ValueRangeNum;

                            float? def = valueRange.Default;
                            float min = (valueRange.Min == 0 ? 0 : float.MinValue);
                            float max = float.MaxValue;
                            if(valueRange.LimitNumberBox)
                            {
                                min = valueRange.Min;
                                max = valueRange.Max;
                            }

                            int inputRound = valueRange.InputRounding;
                            int dragRound = valueRange.Rounding;

                            MyGuiControlLabel dimLabel = host.CreateLabel(SavedDataOB.Name);

                            UINumberBox box = host.CreateNumberBox(null,
                                val, def, min, max, inputRound, dragRound,
                                (value) =>
                                {
                                    AnimationKey key = keysRef[idx];
                                    key.ValueFloat = value;
                                    keysRef[idx] = key;

                                    ChangesMade = true;
                                });

                            host.PositionControlsNoSize(box, dimLabel);

                            /*
                            host.InsertSlider(SavedDataOB.Name, null, PropInfo.ValueRangeNum.Min, PropInfo.ValueRangeNum.Max, keysRef[idx].ValueFloat, PropInfo.ValueRangeNum.Default, 2, (value) =>
                            {
                                AnimationKey key = keysRef[idx];
                                key.ValueFloat = value;
                                keysRef[idx] = key;

                                ChangesMade = true;
                            });
                            */
                        };

                        screen.FinishSetup();

                        MyGuiSandbox.AddScreen(screen);
                        break;
                    }

                    default:
                    {
                        Notifications.Show($"Unsupported value type to edit as animated: {SavedDataOB.Type}", 5, Color.Red);
                        break;
                    }
                }

                ChangesMade = true;
            };

            if(UseValueAsKeyColor)
            {
                timeline.CustomKeyColor = (idx, time) =>
                {
                    if(SavedDataOB == null || keysRef == null || keysRef.Count <= idx)
                        return Vector4.One;

                    switch(SavedDataOB.Type)
                    {
                        case "Vector4":
                        {
                            return EditorUI.ColorForPreview(keysRef[idx].ValueVector4);
                        }
                        //case "Vector3":
                        //{
                        //    return new Vector4(keysRef[idx].ValueVector3, 1f);
                        //}
                        case "Float":
                        {
                            float value = keysRef[idx].ValueFloat;

                            if(value <= 1f)
                                return Color.White * value;
                            else if(value < 10f)
                                return Color.Lerp(Color.White, Color.Yellow, value / 10);
                            else if(value < 50f)
                                return Color.Lerp(Color.Yellow, Color.Red, value / 50);
                            else
                                return Color.Lerp(Color.Red, Color.Blue, value);
                        }
                        default: return Color.White;
                    }
                };
            }

            timeline.GetKeyTooltip = (idx) =>
            {
                if(keysRef.Count <= idx || idx < 0)
                    return null;

                switch(SavedDataOB.Type)
                {
                    case "Vector3":
                    {
                        Vector3 val = keysRef[idx].ValueVector3;
                        return $"Value: {val.X:0.#####}  {val.Y:0.#####}  {val.Z:0.#####}";
                    }
                    case "Vector4":
                    {
                        Vector4 val = keysRef[idx].ValueVector4;
                        return $"Value: {val.X:0.#####}  {val.Y:0.#####}  {val.Z:0.#####}  {val.W:0.#####}";
                    }

                    case "Float": return $"Value: {keysRef[idx].ValueFloat:0.#####}";

                    default: return null;
                }
            };

            timeline.GetValueType = () =>
            {
                switch(SavedDataOB.Type)
                {
                    case "Vector3": return typeof(Vector3);
                    case "Vector4": return typeof(Vector4);
                    case "Float": return typeof(float);
                    default: return null;
                }
            };

            timeline.FinishSetup();
            ScrollHost.Add(timeline);
            return timeline;
        }

        protected override void Canceling()
        {
            if(ChangesMade)
            {
                EditorUI.PopupConfirmation("Changes made, apply them before closing?",
                    confirmed: () =>
                    {
                        ApplyChanges(refreshUI: false);
                    });
            }

            base.Canceling();
        }

        public override bool CloseScreen(bool isUnloading = false)
        {
            return base.CloseScreen(isUnloading);
        }

        UITimeline MouseOverTimeline()
        {
            if(ScrollHost?.Panel == null)
                return null;

            foreach(MyGuiControlBase c in ScrollHost.Panel.Controls)
            {
                if(c is UITimeline timeline && timeline.IsMouseOver)
                {
                    return timeline;
                }
            }

            return null;
        }

        public override void HandleInput(bool receivedFocusInThisUpdate)
        {
            // TODO: allow RMB to free move and all that stuff

            base.HandleInput(receivedFocusInThisUpdate);

            if(FocusOnDropDown)
                FocusedControl = ContextMenu;

            if(MyInput.Static.IsAnyNewMousePressed() && MouseOverTimeline() == null)
            {
                FocusOnDropDown = false;
                ContextMenu.Deactivate();
            }
            else if(MyInput.Static.IsNewRightMousePressed())
            {
                UITimeline timeline = MouseOverTimeline();
                if(timeline != null)
                {
                    ContextMenu.CreateNewContextMenu();

                    MyGuiControlListbox listbox = (MyGuiControlListbox)ContextMenu.GetInnerList();

                    // HACK: contextmenu's Add() ignores the tooltip input!
                    void AddItem(string text, string tooltip = null, object userData = null)
                    {
                        MyGuiControlListbox.Item item = new MyGuiControlListbox.Item(new StringBuilder(text), tooltip, null, userData);
                        listbox.Add(item);
                        listbox.VisibleRowsCount = Math.Min(20, listbox.Items.Count);
                    }

                    // FIXME: these tooltips don't work...

                    listbox.ItemMouseOver += (c) =>
                    {
                        //if(listbox.MouseOverItem != null)
                        //{
                        listbox.ShowToolTip();
                        //}
                    };

                    //foreach(MyGuiControlBase c in ScrollHost.Panel.Controls)
                    //{
                    //    if(c is UITimeline)
                    //    {
                    //        c.Enabled = false;
                    //    }
                    //}

                    UITimeline.Key capturedKey = timeline.AimedKey; // required to be properly passed on as its current value

                    if(capturedKey != null)
                    {
                        AddItem("Edit value  (E or Ctrl+Click)",
                            userData: new Action(() => timeline.ContextMenu_Edit(capturedKey)));

                        AddItem("Copy value  (C) ",
                            "Copies value to clipboard" +
                            "\nClipboard in this case is localized to this editor, not using OS's clipboard.",
                            userData: new Action(() => timeline.ContextMenu_Copy(capturedKey)));

                        AddItem("Paste over  (V)",
                            userData: new Action(() => timeline.ContextMenu_PasteReplace(capturedKey)));

                        AddItem("Delete  (D)",
                            userData: new Action(() => timeline.ContextMenu_Delete(capturedKey)));
                    }
                    else
                    {
                        AddItem("Add key  (A)",
                            "Creates a new key at the aimed position",
                            userData: new Action(timeline.ContextMenu_Add));

                        AddItem("Add interpolated key  (i)",
                            "Creates a new key with value interpolated between closest left and right keys.",
                            userData: new Action(timeline.ContextMenu_AddInterpolated));

                        AddItem("Paste new   (V)",
                            $"Creates a new key with the value from clipboard: {UITimeline.Clipboard ?? "(empty)"}" +
                            $"\nClipboard in this case is localized to this editor, not using OS's clipboard.",
                            userData: new Action(timeline.ContextMenu_AddPaste));
                    }

                    ContextMenu.ItemList_UseSimpleItemListMouseOverCheck = true;
                    ContextMenu.Activate();
                    FocusOnDropDown = true;
                }
            }
        }
    }
}
