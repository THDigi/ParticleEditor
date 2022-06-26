using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Digi.ParticleEditor.GameData;
using Digi.ParticleEditor.UIControls;
using Sandbox;
using Sandbox.Game;
using Sandbox.Game.Gui;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender.Animations;

namespace Digi.ParticleEditor
{
    public class VerticalControlsHost
    {
        readonly IMyGuiControlsParent Screen;
        public readonly MyGuiControlParent Panel;

        //public IMyGuiControlsParent Parent;
        public Vector2 PanelSize { get; private set; }
        public Vector2 CurrentPosition { get; private set; }
        public Vector2 StartingPosition { get; private set; }
        public Vector2 Padding { get; private set; }
        public float ControlSpacing = 0.005f;

        CollapsibleSection Section;
        float Indent = 0f;

        public float AvailableWidth => PanelSize.X - (Padding.X * 2) - Indent;

        public float CurrentHeight => CurrentPosition.Y - StartingPosition.Y;

        float LastYMove = 0;

        static readonly bool DebugBorders = false;
        static readonly Vector4 DebugHostColor = Color.Red;
        static readonly Vector4 DebugLabelColor = Color.Lime;
        static readonly Vector4 DebugCheckboxColor = Color.Red;
        static readonly Vector4 DebugParentColor = Color.HotPink;

        public HashSet<IMyConstProperty> SkipProperties = null;

        public VerticalControlsHost(IMyGuiControlsParent parent, Vector2 pos, Vector2 size, Vector2? padding = null, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, bool drawBackground = false)
        {
            Screen = parent;

            Padding = padding ?? new Vector2(0.01f, 0.005f);

            Panel = new MyGuiControlParent(pos, size);
            Panel.OriginAlign = originAlign;

            if(drawBackground)
            {
                Panel.BackgroundTexture = new MyGuiCompositeTexture(@"Textures\GUI\Blank.dds");
                Panel.ColorMask = EditorUI.TransitionAlpha(EditorUI.ColorBg, MySandboxGame.Config.UIBkOpacity);
            }

            Panel.BorderColor = DebugHostColor;
            Panel.BorderEnabled = DebugBorders;
            Panel.BorderSize = 3;

            PanelSize = size;
            StartingPosition = size / -2;
            CurrentPosition = StartingPosition;

            Reset();
        }

        public void Reset()
        {
            Panel.Controls.Clear();
            CurrentPosition = StartingPosition + new Vector2(0, Padding.Y);

            if(Screen != null && !Screen.Controls.Contains(Panel))
                Screen.Controls.Add(Panel);
        }

        public void Add(MyGuiControlBase control)
        {
            Panel.Controls.Add(control);
        }

        public void SetPosition(Vector2 pos)
        {
            CurrentPosition = pos;
            LastYMove = 0;
        }

        public void ResizeY()
        {
            // can't do this because StartingPosition is size-based... ?

            float newHeight = (CurrentPosition.Y - StartingPosition.Y) + Padding.Y * 2;

            float diffHeight = (Panel.Size.Y - newHeight) / 2f; // HACK: why /2 works?

            Panel.Size = new Vector2(Panel.Size.X, newHeight);

            foreach(MyGuiControlBase control in Panel.Controls)
            {
                control.PositionY += diffHeight;
            }
        }

        public void MoveY(float add)
        {
            LastYMove = add;
            CurrentPosition = new Vector2(CurrentPosition.X, CurrentPosition.Y + add);
        }

        public void UndoLastVerticalShift()
        {
            CurrentPosition = new Vector2(CurrentPosition.X, CurrentPosition.Y - LastYMove);
            //LastYMove = 0;
        }

        public void PositionAndFillWidth(MyGuiControlBase control, float heightScale = 1f)
        {
            control.Position = new Vector2(CurrentPosition.X + Padding.X + Indent, CurrentPosition.Y + Padding.Y);

            if(!(control is MyGuiControlCheckbox))
                control.Size = new Vector2(AvailableWidth, control.Size.Y * heightScale);

            MoveY(control.Size.Y + Padding.Y);
        }

        //public void PositionControls(MyGuiControlBase a, MyGuiControlBase b) => PositionControls(new[] { a, b });
        //public void PositionControls(MyGuiControlBase a, MyGuiControlBase b, MyGuiControlBase c) => PositionControls(new[] { a, b, c });
        //public void PositionControls(MyGuiControlBase a, MyGuiControlBase b, MyGuiControlBase c, MyGuiControlBase d) => PositionControls(new[] { a, b, c, d });
        //public void PositionControls(MyGuiControlBase a, MyGuiControlBase b, MyGuiControlBase c, MyGuiControlBase d, MyGuiControlBase e) => PositionControls(new[] { a, b, c, d, e });
        public void PositionControls(params MyGuiControlBase[] controls)
        {
            float controlWidth = AvailableWidth / (float)controls.Length - 0.001f * (float)controls.Length;
            float offsetX = controlWidth + 0.001f * (float)controls.Length;

            float tallestControl = controls.Max(c => c.Size.Y);

            //float controlWidth = ((PanelSize.X - (Padding.X * 2)) / controls.Length) - (ControlSpacing * controls.Length);
            //float offsetX = controlWidth + ControlSpacing * controls.Length;

            float x = CurrentPosition.X + Padding.X + Indent;
            float y = CurrentPosition.Y + Padding.Y;

            for(int i = 0; i < controls.Length; i++)
            {
                MyGuiControlBase control = controls[i];

                if(!(control is MyGuiControlCheckbox))
                    control.Size = new Vector2(controlWidth, control.Size.Y);

                control.Position = new Vector2(x + (offsetX * i), y + (tallestControl - control.Size.Y) / 2f);
            }

            MoveY(tallestControl + Padding.Y);
        }

        //public void PositionControlsNoSize(MyGuiControlBase a) => PositionControlsNoSize(new[] { a });
        //public void PositionControlsNoSize(MyGuiControlBase a, MyGuiControlBase b) => PositionControlsNoSize(new[] { a, b });
        //public void PositionControlsNoSize(MyGuiControlBase a, MyGuiControlBase b, MyGuiControlBase c) => PositionControlsNoSize(new[] { a, b, c });
        //public void PositionControlsNoSize(MyGuiControlBase a, MyGuiControlBase b, MyGuiControlBase c, MyGuiControlBase d) => PositionControlsNoSize(new[] { a, b, c, d });
        //public void PositionControlsNoSize(MyGuiControlBase a, MyGuiControlBase b, MyGuiControlBase c, MyGuiControlBase d, MyGuiControlBase e) => PositionControlsNoSize(new[] { a, b, c, d, e });
        public void PositionControlsNoSize(params MyGuiControlBase[] controls)
        {
            float tallestControl = controls.Max(c => c.Size.Y);
            float x = CurrentPosition.X + Padding.X + Indent;
            float y = CurrentPosition.Y + Padding.Y;

            for(int i = 0; i < controls.Length; i++)
            {
                MyGuiControlBase control = controls[i];
                control.Position = new Vector2(x, y + (tallestControl - control.Size.Y) / 2f);

                x += control.Size.X + ControlSpacing;
            }

            MoveY(tallestControl + Padding.Y);
        }

        public void StackRight(params MyGuiControlBase[] controls)
        {
            float tallestControl = controls.Max(c => c.Size.Y);

            float x = CurrentPosition.X + Padding.X + AvailableWidth;
            float y = CurrentPosition.Y + Padding.Y;

            for(int i = (controls.Length - 1); i >= 0; i--)
            {
                MyGuiControlBase control = controls[i];

                x -= control.Size.X;

                control.Position = new Vector2(x, y + (tallestControl - control.Size.Y) / 2f);

                x -= ControlSpacing;
            }

            MoveY(tallestControl + Padding.Y);
        }

        public MyGuiControlScrollablePanel CreateScrollableArea(MyGuiControlParent content, Vector2 size)
        {
            MyGuiControlScrollablePanel scrollable = new MyGuiControlScrollablePanel(content)
            {
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                Position = CurrentPosition,
                Size = size,

                DrawScrollBarSeparator = true,
                ScrollbarVEnabled = true,
                ScrolledAreaPadding = new MyGuiBorderThickness(0, 0, 0, 0.005f),

                BorderEnabled = true,
                BorderColor = MyGuiConstants.THEMED_GUI_LINE_COLOR,
            };

            scrollable.BackgroundTexture = MyGuiConstants.TEXTURE_RECTANGLE_DARK;
            scrollable.ColorMask = EditorUI.TransitionAlpha(Color.White, MySandboxGame.Config.UIBkOpacity);


            // TODO: fix this narrowing scrollbar attempt...
#if false
            try
            {
                const float Divider = 4f;

                MyGuiCompositeTexture thumb = new MyGuiCompositeTexture()
                {
                    LeftTop = new MyGuiSizedTexture
                    {
                        Texture = "Textures\\GUI\\Controls\\scrollbar_v_thumb_top.dds",
                        SizePx = new Vector2(46f / Divider, 46f)
                    },
                    LeftCenter = new MyGuiSizedTexture
                    {
                        Texture = "Textures\\GUI\\Controls\\scrollbar_v_thumb_center.dds",
                        SizePx = new Vector2(46f / Divider, 4f)
                    },
                    LeftBottom = new MyGuiSizedTexture
                    {
                        Texture = "Textures\\GUI\\Controls\\scrollbar_v_thumb_bottom.dds",
                        SizePx = new Vector2(46f / Divider, 23f)
                    },
                };

                MyGuiCompositeTexture thumbHighlight = new MyGuiCompositeTexture()
                {
                    LeftTop = new MyGuiSizedTexture
                    {
                        Texture = "Textures\\GUI\\Controls\\scrollbar_v_thumb_top_highlight.dds",
                        SizePx = new Vector2(46f / Divider, 46f)
                    },
                    LeftCenter = new MyGuiSizedTexture
                    {
                        Texture = "Textures\\GUI\\Controls\\scrollbar_v_thumb_center_highlight.dds",
                        SizePx = new Vector2(46f / Divider, 4f)
                    },
                    LeftBottom = new MyGuiSizedTexture
                    {
                        Texture = "Textures\\GUI\\Controls\\scrollbar_v_thumb_bottom_highlight.dds",
                        SizePx = new Vector2(46f / Divider, 23f)
                    },
                };

                var scrollbarV = (MyVScrollbar)typeof(MyGuiControlScrollablePanel)?.GetField("m_scrollbarV", BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ScrollablePanel);
                typeof(MyScrollbar).GetField("m_normalTexture", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(scrollbarV, thumb);
                typeof(MyScrollbar).GetField("m_highlightTexture", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(scrollbarV, thumbHighlight);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
#endif

            return scrollable;
        }

        public void InsertSeparator(float widthScale = 1f)
        {
            MoveY(Padding.Y);
            MyGuiControlSeparatorList separatorList = new MyGuiControlSeparatorList();
            separatorList.Position = CurrentPosition;
            separatorList.Size = new Vector2(PanelSize.X * widthScale, 0.001f);
            separatorList.AddHorizontal(Vector2.Zero, PanelSize.X * widthScale);
            Add(separatorList);
            MoveY(Padding.Y);
            //return separatorList;
        }

        public MyGuiControlCombobox CreateComboBox()
        {
            MyGuiControlCombobox comboBox = new MyGuiControlCombobox(new Vector2(Padding.X + Indent, CurrentPosition.Y), EditorUI.ButtonSize, originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            comboBox.Enabled = true;
            Add(comboBox);
            return comboBox;
        }

        public MyGuiControlLabel CreateLabel(string text, MyGuiDrawAlignEnum align = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
        {
            MyGuiControlLabel label = new MyGuiControlLabel(CurrentPosition + new Vector2(Padding.X + Indent, 0), EditorUI.ItemSize, text, null, 0.8f, "Debug", align);
            label.ColorMask = Color.White;
            Add(label);
            return label;
        }

        public MyGuiControlButton CreateButton(string text, string tooltip = null, MyGuiDrawAlignEnum textAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, Action<MyGuiControlButton> clicked = null, float buttonScale = 0.6f)
        {
            const MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;

            MyGuiControlButton button = new MyGuiControlButton(new Vector2(Padding.X + Indent, CurrentPosition.Y),
                MyGuiControlButtonStyleEnum.Rectangular, EditorUI.ButtonSize, Color.Yellow.ToVector4(), originAlign,
                tooltip, new StringBuilder(text), buttonScale, textAlign, MyGuiControlHighlightType.WHEN_CURSOR_OVER, clicked);

            Add(button);
            return button;
        }

        public MyGuiControlListbox CreateListBox(int rows, float width, bool multiSelect = false)
        {
            MyGuiControlListbox listbox = new MyGuiControlListbox(CurrentPosition, MyGuiControlListboxStyleEnum.Blueprints);
            listbox.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            listbox.MultiSelect = multiSelect;
            listbox.Enabled = true;
            listbox.ItemSize = new Vector2(width * 0.9f, EditorUI.ItemSize.Y);
            listbox.TextScale = 0.6f;
            listbox.VisibleRowsCount = rows;
            Add(listbox);
            listbox.Size = new Vector2(width, listbox.ItemSize.Y * rows);
            return listbox;
        }

        public MyGuiControlCheckbox CreateCheckboxNoLabel(bool isChecked, string tooltip, Action<MyGuiControlCheckbox> checkedChanged)
        {
            MyGuiControlCheckbox checkbox = new MyGuiControlCheckbox(null, null, tooltip, isChecked, MyGuiControlCheckboxStyleEnum.Debug, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            checkbox.IsCheckedChanged = checkedChanged;
            Add(checkbox);
            return checkbox;
        }

        public MyGuiControlParent InsertCheckbox(string title, string tooltip, bool value, Action<bool> valueSet)
        {
            //MyGuiControlCheckbox checkbox = new MyGuiControlCheckbox(new Vector2(ITEM_HORIZONTAL_PADDING, 0), null, tooltip, value, MyGuiControlCheckboxStyleEnum.Debug, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            MyGuiControlCheckbox checkbox = new MyGuiControlCheckbox();
            checkbox.IsChecked = value;
            checkbox.SetToolTip(tooltip);
            checkbox.Position = new Vector2(0, 0);
            checkbox.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER;
            checkbox.VisualStyle = MyGuiControlCheckboxStyleEnum.Debug;
            checkbox.BorderEnabled = DebugBorders;
            checkbox.BorderColor = DebugCheckboxColor;
            checkbox.Refresh();

            if(valueSet != null)
                checkbox.IsCheckedChanged = (c) => valueSet.Invoke(c.IsChecked);

            return CheckboxParent(checkbox, title, tooltip);
        }

        public MyGuiControlParent Insert3StateCheckbox(string title, string tooltip, CheckStateEnum value, Action<CheckStateEnum> valueSet)
        {
            MyGuiControlIndeterminateCheckbox checkbox = new MyGuiControlIndeterminateCheckbox();
            checkbox.State = value;
            checkbox.SetToolTip(tooltip);
            checkbox.Position = new Vector2(Padding.X + Indent, 0);
            checkbox.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER;
            checkbox.BorderEnabled = DebugBorders;
            checkbox.BorderColor = DebugCheckboxColor;
            checkbox.VisualStyle = MyGuiControlIndeterminateCheckboxStyleEnum.Debug;
            checkbox.ApplyStyle(MyGuiControlIndeterminateCheckbox.GetVisualStyle(checkbox.VisualStyle));

            if(valueSet != null)
                checkbox.IsCheckedChanged = (c) => valueSet.Invoke(c.State);

            return CheckboxParent(checkbox, title, tooltip);
        }

        MyGuiControlParent CheckboxParent(MyGuiControlBase checkbox, string title, string tooltip)
        {
            UIClickableLabel label = new UIClickableLabel(new Vector2(checkbox.Position.X + checkbox.Size.X + ControlSpacing, 0), null, title, null, 0.8f, "Debug",
                MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER);
            label.ColorMask = Color.White;
            label.SetTooltip(tooltip);
            label.BorderColor = DebugLabelColor;
            label.BorderEnabled = DebugBorders;
            // TODO: maybe make this work somehow
            //label.Clicked += () =>
            //{
            //    checkbox.IsMouseOver = true;
            //    checkbox.HandleInput();
            //};

            // TODO: remove padding from controls, have it be in CurrentPosition instead

            float width = (Padding.X * 2) + checkbox.Size.X + ControlSpacing + label.Size.X;
            float height = Math.Max(checkbox.Size.Y, label.Size.Y) + Padding.Y;

            MyGuiControlParent parent = new MyGuiControlParent()
            {
                Position = CurrentPosition + new Vector2(Padding.X + Indent, 0),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                Size = new Vector2(width, height),
                Controls = { checkbox, label },
                BorderEnabled = DebugBorders,
                BorderColor = DebugParentColor,
            };

            foreach(MyGuiControlBase c in parent.Controls)
            {
                c.PositionX -= parent.Size.X / 2;
            }

            parent.EnabledChanged = (_) =>
            {
                foreach(MyGuiControlBase c in parent.Controls)
                {
                    c.Enabled = parent.Enabled;
                }
            };

            Add(parent);
            MoveY(height);
            return parent;
        }

        public void InsertMultiSlider(string label, string tooltip, string[] names, float[] min, float[] max,
                                      float[] currentValues, float[] defaultValues = null, int round = 3,
                                      Action<int, float> valueChanged = null, string[] tooltips = null, float namesWidthRatio = 0.18f)
        {
            if(names.Length != min.Length
            || names.Length != max.Length
            || (tooltips != null && names.Length != tooltips.Length)
            || (defaultValues != null && names.Length != defaultValues.Length))
                throw new ArgumentException("all input arrays must be the same length!");


            StringBuilder tooltipSB = new StringBuilder(256);

            if(tooltip != null)
                tooltipSB.Append(tooltip).Append('\n');

            if(defaultValues != null)
                tooltipSB.Append("\nOriginal value: ").Append(string.Join(", ", defaultValues)).Append(" (Press RMB to reset to this)");

            tooltipSB.Append("\nCtrl+Click to enter custom values and beyond the slider limits.");


            float width = AvailableWidth;
            float x = CurrentPosition.X + Padding.X + Indent;
            float y = CurrentPosition.Y;

            MyGuiControlLabel nameLabel = new MyGuiControlLabel
            {
                Position = new Vector2(x, y),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                Text = label,
                Font = "Debug",
                ColorMask = Color.White,
            };
            nameLabel.SetTooltip(tooltip);

            Add(nameLabel);

            y += nameLabel.Size.Y + Padding.Y;

            for(int i = 0; i < names.Length; i++)
            {
                int dim = i; // required like this to be captured without issues

                MyGuiControlLabel prefixLabel = new MyGuiControlLabel
                {
                    OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                    Text = $"{names[dim]}: {Math.Round(currentValues[dim], round)}",
                    ColorMask = Color.White,
                };
                prefixLabel.SetTooltip(tooltip);

                UIUnrestrainedSlider slider = new UIUnrestrainedSlider(null, min[dim], max[dim], defaultValue: (defaultValues == null ? (float?)null : defaultValues[dim]));
                slider.SetToolTip(tooltipSB.ToString());
                slider.Value = currentValues[dim];
                slider.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
                slider.UnrestrainedValueChanged += (value) =>
                {
                    value = (float)Math.Round(value, round);
                    slider.Value = value;
                    prefixLabel.Text = $"{names[dim]}: {value}";
                    valueChanged?.Invoke(dim, value);
                };
                slider.SliderSetValueManual = (c) =>
                {
                    MyGuiScreenDialogAmount dialog = new MyGuiScreenDialogAmount(float.MinValue, float.MaxValue, defaultAmount: slider.UnrestraintedValue,
                        caption: MyStringId.GetOrCompute($"Edit '{label}'." +
                                                         (defaultValues != null ? $" Original value: {defaultValues[dim]:0.#####}" : "") +
                                                         "\nNOTE: this prompt does not enforce any limits."),
                        minMaxDecimalDigits: 3,
                        parseAsInteger: false,
                        backgroundTransition: MySandboxGame.Config.UIBkOpacity,
                        guiTransition: MySandboxGame.Config.UIOpacity);

                    //dialog.Closed += Dialog_Closed;
                    dialog.OnConfirmed += (value) =>
                    {
                        slider.UnrestraintedValue = value;
                        prefixLabel.Text = $"{names[dim]}: {value.ToString()}";
                    };
                    MyGuiSandbox.AddScreen(dialog);
                    return true;
                };

                prefixLabel.Size = new Vector2(width * namesWidthRatio, prefixLabel.Size.Y);
                slider.Size = new Vector2(width * (1f - namesWidthRatio), slider.Size.Y);
                slider.DebugScale = 0.5f;

                prefixLabel.Position = new Vector2(x, y);
                slider.Position = new Vector2(x + prefixLabel.Size.X, y);

                Add(prefixLabel);
                Add(slider);

                y += slider.Size.Y + Padding.Y;
            }

            MoveY(y - CurrentPosition.Y);
        }

        public void InsertSlider(string label, string tooltip, float min, float max, float currentValue, float? defaultValue, int round, Action<float> valueChanged, Func<float, string> valueWriter = null)
        {
            float width = AvailableWidth;
            float x = CurrentPosition.X + Padding.X + Indent;
            float y = CurrentPosition.Y;


            StringBuilder tooltipSB = new StringBuilder(256);

            if(tooltip != null)
                tooltipSB.Append(tooltip).Append('\n');

            if(defaultValue != null)
                tooltipSB.Append("\nOriginal value: ").Append(defaultValue.Value.ToString("N5")).Append(" (Press RMB to reset to this)");

            tooltipSB.Append("\nCtrl+Click to enter custom values and beyond the slider limits.");


            MyGuiControlLabel nameLabel = new MyGuiControlLabel
            {
                Position = new Vector2(x, y),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
                Text = label,
                Font = "Debug",
                ColorMask = Color.White,
            };
            nameLabel.SetTooltip(tooltip);

            if(valueWriter == null)
                valueWriter = (v) => Math.Round(v, round).ToString();

            MyGuiControlLabel valueLabel = new MyGuiControlLabel
            {
                Position = new Vector2(x + width, y),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP,
                Text = valueWriter.Invoke(currentValue),
                ColorMask = Color.White,
            };

            y += nameLabel.Size.Y + Padding.Y;

            UIUnrestrainedSlider slider = new UIUnrestrainedSlider(new Vector2(x, y), min, max, defaultValue: defaultValue);
            slider.Size = new Vector2(width, 1f);
            slider.Value = currentValue;
            slider.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            slider.UnrestrainedValueChanged += (value) =>
            {
                value = (float)Math.Round(value, round);
                slider.Value = value;
                valueLabel.Text = valueWriter.Invoke(value);
                valueChanged?.Invoke(value);
            };
            slider.SliderSetValueManual = (c) =>
            {
                MyGuiScreenDialogAmount dialog = new MyGuiScreenDialogAmount(float.MinValue, float.MaxValue, defaultAmount: slider.UnrestraintedValue,
                    caption: MyStringId.GetOrCompute($"Edit '{label}'." +
                                                    (defaultValue != null ? $" Original value: {defaultValue.Value:0.#####}" : "") +
                                                    "\nNOTE: this prompt does not enforce any limits."),
                    minMaxDecimalDigits: 3, parseAsInteger: false, backgroundTransition: MySandboxGame.Config.UIBkOpacity, guiTransition: MySandboxGame.Config.UIOpacity);
                dialog.OnConfirmed += (value) =>
                {
                    slider.UnrestraintedValue = value;
                    valueLabel.Text = valueWriter.Invoke(value);
                };
                MyGuiSandbox.AddScreen(dialog);
                return true;
            };

            slider.DebugScale = 0.8f;
            slider.SetTooltip(tooltipSB.ToString());

            Add(slider);
            Add(nameLabel);
            Add(valueLabel);

            MoveY(nameLabel.Size.Y + Padding.Y + slider.Size.Y + Padding.Y);
        }

        public UINumberBox CreateNumberBox(string tooltip, float initialValue, float? defaultValue, float min, float max, int inputRound, int dragRound, Action<float> changed)
        {
            UINumberBox box = new UINumberBox(CurrentPosition, initialValue, min, max, inputRound: inputRound, dragRound: dragRound, defaultValue, tooltip);
            box.NumberChanged += changed;
            Add(box);
            return box;
        }

        public (MyGuiControlParent, Action<Vector4>) CreateColorPreview(Vector4 color, Vector2? size = null, string tooltip = "Color preview")
        {
            MyGuiControlImage colorPreview = new MyGuiControlImage(size: size ?? new Vector2(0.035f, 0.035f), backgroundTexture: @"Textures\GUI\Blank.dds", originAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            colorPreview.ColorMask = EditorUI.ColorForPreview(color);
            colorPreview.Position = colorPreview.Size / -2;
            colorPreview.SetToolTip(tooltip);

            MyGuiControlParent parent = new MyGuiControlParent();
            parent.OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            parent.Size = colorPreview.Size;
            parent.BorderColor = MyGuiConstants.THEMED_GUI_LINE_COLOR;
            parent.BorderEnabled = true;
            parent.BorderSize = 1;
            parent.Controls.Add(colorPreview);

            Add(parent);
            return (parent, (v) => colorPreview.ColorMask = EditorUI.ColorForPreview(v));
        }

        public MyGuiControlButton PropEditXML(object propHost, IMyConstProperty prop, string name = null, string tooltip = null, float buttonScale = 0.6f, bool callPosition = true)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return null;

            // HACK xD
            if(propHost is PropId propId)
            {
                if(propId.Type == PropType.Emitter)
                    propHost = EditorUI.Instance.EditorEmitters.SelectedEmitter;
                else if(propId.Type == PropType.Light)
                    propHost = EditorUI.Instance.EditorLights.SelectedLight;
            }

            if(propHost is MyParticleGPUGenerationData emitter)
            {
                MyParticleGPUGenerationData selectedEmitter = EditorUI.Instance.EditorEmitters.SelectedEmitter;
                if(selectedEmitter != emitter)
                    Log.Error($"Selected emitter '{selectedEmitter?.Name ?? "null"}' is not the passed emitter '{emitter?.Name ?? "null"}' ?!");

                return XMLButton(prop, name, tooltip, buttonScale,
                    saved: () =>
                    {
                        EditorUI.Instance.EditorEmitters.RefreshEmitterPropertiesUI();
                    },
                    helpOriginal: () =>
                    {
                        string originalXML = null;

                        MyObjectBuilder_ParticleEffect originalOB = EditorUI.Instance.OriginalParticleData.GetValueOrDefault(EditorUI.Instance.SelectedParticle.Name);
                        if(originalOB != null)
                        {
                            ParticleGeneration emitterOB = originalOB.ParticleGenerations.Find(e => e.Name == emitter.Name);
                            if(emitterOB != null)
                            {
                                GenerationProperty originalPropOB = emitterOB.Properties.Find(p => p.Name == prop.Name);
                                if(originalPropOB != null)
                                {
                                    originalXML = EditorUI.CleanupXML(MyAPIGateway.Utilities.SerializeToXML(originalPropOB));
                                }
                            }
                        }

                        if(originalXML != null)
                        {
                            MyGuiScreenTextPanel gui = new MyGuiScreenTextPanel("Original XML", "", "", originalXML, editable: true, okButtonCaption: "Close\n(changes do not matter)");
                            MyScreenManager.AddScreen(gui);
                        }
                        else
                        {
                            EditorUI.PopupInfo("Original XML", "No original data found.");
                        }
                    },
                    helpDefault: () =>
                    {
                        IMyConstProperty defaultProp = EditorUI.DefaultEmitter.GetProperties().FirstOrDefault(p => p.Name == prop.Name);
                        if(defaultProp != null)
                        {
                            string defaultXML = EditorUI.CleanupXML(MyAPIGateway.Utilities.SerializeToXML(defaultProp.SerializeToObjectBuilder()));
                            MyGuiScreenTextPanel gui = new MyGuiScreenTextPanel("Original XML", "", "", defaultXML, editable: true, okButtonCaption: "Close\n(changes do not matter)");
                            MyScreenManager.AddScreen(gui);
                        }
                    });
            }

            if(propHost is MyParticleLightData light)
            {
                MyParticleLightData selectedLight = EditorUI.Instance.EditorLights.SelectedLight;
                if(selectedLight != light)
                    Log.Error($"Selected light '{selectedLight?.Name ?? "null"}' is not the passed light '{light?.Name ?? "null"}' ?!");

                return XMLButton(prop, name, tooltip, buttonScale,
                    saved: () =>
                    {
                        EditorUI.Instance.EditorLights.RefreshPropertiesUI();
                    },
                    helpOriginal: () =>
                    {
                        string originalXML = null;

                        MyObjectBuilder_ParticleEffect originalOB = EditorUI.Instance.OriginalParticleData.GetValueOrDefault(EditorUI.Instance.SelectedParticle.Name);
                        if(originalOB != null)
                        {
                            ParticleLight lightOB = originalOB.ParticleLights.Find(e => e.Name == light.Name);
                            if(lightOB != null)
                            {
                                GenerationProperty originalPropOB = lightOB.Properties.Find(p => p.Name == prop.Name);
                                if(originalPropOB != null)
                                {
                                    originalXML = EditorUI.CleanupXML(MyAPIGateway.Utilities.SerializeToXML(originalPropOB));
                                }
                            }
                        }

                        if(originalXML != null)
                        {
                            MyGuiScreenTextPanel gui = new MyGuiScreenTextPanel("Original XML", "", "", originalXML, editable: true, okButtonCaption: "Close\n(changes do not matter)");
                            MyScreenManager.AddScreen(gui);
                        }
                        else
                        {
                            EditorUI.PopupInfo("Original XML", "No original data found.");
                        }
                    },
                    helpDefault: () =>
                    {
                        IMyConstProperty defaultProp = EditorUI.DefaultLight.GetProperties().FirstOrDefault(p => p.Name == prop.Name);
                        if(defaultProp != null)
                        {
                            string defaultXML = EditorUI.CleanupXML(MyAPIGateway.Utilities.SerializeToXML(defaultProp.SerializeToObjectBuilder()));
                            MyGuiScreenTextPanel gui = new MyGuiScreenTextPanel("Original XML", "", "", defaultXML, editable: true, okButtonCaption: "Close\n(changes do not matter)");
                            MyScreenManager.AddScreen(gui);
                        }
                    });
            }

            throw new ArgumentException($"Unsupported propHost type: {propHost?.GetType()}");
        }

        MyGuiControlButton XMLButton(IMyConstProperty prop, string name = null, string tooltip = null, float buttonScale = 0.6f,
                                            Action saved = null,
                                            Action helpOriginal = null,
                                            Action helpDefault = null)
        {
            MyGuiControlButton button = CreateXMLEditButton(name ?? $"{prop.Name}: {prop.GetValue()}", tooltip ?? prop.Description + $"\nXML Property name: \"{prop.Name}\"",
            getXml: () =>
            {
                GenerationProperty propOB = prop.SerializeToObjectBuilder();
                return EditorUI.CleanupXML(MyAPIGateway.Utilities.SerializeToXML(propOB));
            },
            submitted: (text, validationOnly) =>
            {
                GenerationProperty propEditedOB = null;
                try
                {
                    propEditedOB = MyAPIGateway.Utilities.SerializeFromXML<GenerationProperty>(text);
                }
                catch(Exception e)
                {
                    MyScreenManager.AddScreen(new MyGuiScreenEditorError($"Error deserializing XML:\n{e.ToString()}"));
                    return false;
                }

                if(propEditedOB.Name != prop.Name)
                {
                    EditorUI.PopupInfo("Error", "Name differs, probably pasted a different property's XML?", MyMessageBoxStyleEnum.Error);
                    return false;
                }

                if(propEditedOB.Type != prop.ValueType
                    || (propEditedOB.AnimationType == PropertyAnimationType.Const && prop.Animated)
                    || (propEditedOB.AnimationType == PropertyAnimationType.Animated && (!prop.Animated || prop.Is2D))
                    || (propEditedOB.AnimationType == PropertyAnimationType.Animated2D && (!prop.Animated || !prop.Is2D))
                    )
                {
                    EditorUI.PopupInfo("Error", "Type differs, probably pasted a different property's XML?", MyMessageBoxStyleEnum.Error);
                    return false;
                }

                if(validationOnly)
                {
                    MyGuiSandbox.AddScreen(MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Info, MyMessageBoxButtonsType.OK, messageCaption: new StringBuilder("XML validation result"), messageText: new StringBuilder("Code is valid!")));
                }
                else
                {
                    try
                    {
                        // HACK: required to avoid keys piling up
                        if(prop is IMyAnimatedProperty animProp)
                            animProp.ClearKeys();

                        prop.DeserializeFromObjectBuilder(propEditedOB);

                        EditorUI.Instance.SelectedParticle.Refresh();

                        Notifications.Show($"Succesfully edited XML for '{prop.Name}'");

                        saved?.Invoke();
                    }
                    catch(Exception e)
                    {
                        MyScreenManager.AddScreen(new MyGuiScreenEditorError($"Error applying deserialized data:\n{e.ToString()}"));
                        return false;
                    }
                }

                return true;
            },
            helpOriginal: helpOriginal,
            helpDefault: helpDefault,
            buttonScale: buttonScale);
            return button;
        }

        MyGuiControlButton CreateXMLEditButton(string title, string tooltip, Func<string> getXml, Func<string, bool, bool> submitted, Action helpOriginal = null, Action helpDefault = null, float buttonScale = 0.6f)
        {
            MyGuiControlButton button = CreateButton(title, tooltip, textAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, buttonScale: buttonScale);
            button.ButtonClicked += (c) =>
            {
                string xml = getXml.Invoke();
                xml = EditorUI.CleanupXML(xml);

                UILargeXMLEditorScreen editor = new UILargeXMLEditorScreen("Isolated XML Editor", xml);
                editor.SaveTextCallback = (text) =>
                {
                    if(string.IsNullOrWhiteSpace(text))
                    {
                        EditorUI.PopupInfo("No code", "Cannot submit nothing!", MyMessageBoxStyleEnum.Error);
                        return false;
                    }

                    return submitted.Invoke(text, false);
                };
                editor.ClosedCallback = (result) =>
                {
                    if(result != VRage.Game.ModAPI.ResultEnum.OK)
                    {
                        string newText = editor.GetText();

                        if(string.IsNullOrWhiteSpace(newText))
                            return;

                        newText = newText.Replace("\r", "");

                        if(newText != xml)
                        {
                            MyGuiScreenMessageBox popup = MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Info, MyMessageBoxButtonsType.YES_NO, messageCaption: new StringBuilder("Code Changed"), messageText: new StringBuilder("Save changes?"), okButtonText: null, cancelButtonText: null, yesButtonText: null, noButtonText: null, callback: null, timeoutInMiliseconds: 0, focusedResult: MyGuiScreenMessageBox.ResultEnum.YES, canHideOthers: false);
                            popup.ResultCallback = (pressed) =>
                            {
                                if(pressed == MyGuiScreenMessageBox.ResultEnum.YES)
                                    submitted.Invoke(newText, false);
                            };
                            MyScreenManager.AddScreen(popup);
                        }
                    }
                };
                editor.ValidateCallback = (text) => submitted.Invoke(text, true);
                editor.HelpOriginalCallback = helpOriginal;
                editor.HelpDefaultCallback = helpDefault;
                editor.RecreateControls(false);
                MyScreenManager.AddScreen(editor);
            };

            return button;
        }

        MyGuiControlButton SideXmlButton(object propHost, IMyConstProperty prop)
        {
            MyGuiControlButton xmlButton = PropEditXML(propHost, prop, "XML", $"Open XML editor window for '{prop.Name}' property only, allows for more experimentation and/or copy pasting.", 0.3f, callPosition: false);
            xmlButton.TextAlignment = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER;
            return xmlButton;
        }

        public void PropSlider(object propHost, MyConstPropertyFloat prop)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            float defaultValue = EditorUI.GetDefaultPropData(propHost, prop).ValueFloat;
            if(EditorUI.GetOriginalPropData(propHost, prop.Name, out GenerationProperty propOB))
                defaultValue = propOB.ValueFloat;

            PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
            string name = PropertyData.GetName(propInfo, prop);
            string tooltip = PropertyData.GetTooltip(propInfo, prop);
            float min = propInfo.ValueRangeNum.Min;
            float max = propInfo.ValueRangeNum.Max;
            int round = propInfo.ValueRangeNum.Rounding;

            InsertSlider(name, tooltip, min, max, prop.GetValue(), defaultValue, round, (val) =>
            {
                prop.SetValue(val);
                EditorUI.Instance.SelectedParticle.Refresh();
            });
        }

        public void PropNumberBox(object propHost, MyConstPropertyFloat prop)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            float? defaultValue = EditorUI.GetDefaultPropData(propHost, prop).ValueFloat;
            if(EditorUI.GetOriginalPropData(propHost, prop.Name, out GenerationProperty propOB))
                defaultValue = propOB.ValueFloat;

            PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
            string name = PropertyData.GetName(propInfo, prop);
            string tooltip = PropertyData.GetTooltip(propInfo, prop);

            ValueInfo<float> valueRange = propInfo.ValueRangeNum;
            float min = (valueRange.Min == 0 ? 0 : float.MinValue);
            float max = float.MaxValue;
            if(valueRange.LimitNumberBox)
            {
                min = valueRange.Min;
                max = valueRange.Max;
            }

            int dragRound = valueRange.Rounding;
            int inputRound = valueRange.InputRounding;

            UINumberBox box = CreateNumberBox(tooltip, prop.GetValue(), defaultValue, min, max, inputRound, dragRound, (value) =>
            {
                prop.SetValue(value);
                EditorUI.Instance.SelectedParticle.Refresh();
            });

            box.Size = new Vector2(0.06f, box.Size.Y);

            MyGuiControlLabel label = CreateLabel(name);
            label.SetToolTip(tooltip);

            PositionControlsNoSize(box, label);
            MoveY(0.01f); // HACK: box is taller than its actual size
        }

        public void PropSliderInt(object propHost, MyConstPropertyInt prop)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            int defaultValue = EditorUI.GetDefaultPropData(propHost, prop).ValueInt;
            if(EditorUI.GetOriginalPropData(propHost, prop.Name, out GenerationProperty propOB))
                defaultValue = propOB.ValueInt;

            PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
            string name = PropertyData.GetName(propInfo, prop);
            string tooltip = PropertyData.GetTooltip(propInfo, prop);
            float min = propInfo.ValueRangeNum.Min;
            float max = propInfo.ValueRangeNum.Max;

            InsertSlider(name, tooltip, min, max, prop.GetValue(), defaultValue, 0, (val) =>
            {
                prop.SetValue((int)Math.Round(val));
                EditorUI.Instance.SelectedParticle.Refresh();
            });
        }

        public void PropSliderVector3(object propHost, MyConstPropertyVector3 prop)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            Vector3? def = EditorUI.GetDefaultPropData(propHost, prop).ValueVector3;
            if(EditorUI.GetOriginalPropData(propHost, prop.Name, out GenerationProperty propOB))
                def = propOB.ValueVector3;

            PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
            string name = PropertyData.GetName(propInfo, prop);
            string tooltip = PropertyData.GetTooltip(propInfo, prop);
            Vector3 min = propInfo.ValueRangeVector3.Min;
            Vector3 max = propInfo.ValueRangeVector3.Max;
            int round = propInfo.ValueRangeVector3.Rounding;

            Vector3 val = prop.GetValue();

            MyGuiControlButton xmlButton = SideXmlButton(propHost, prop);
            xmlButton.Size = new Vector2(AvailableWidth * (1f / 10f), xmlButton.Size.Y);
            xmlButton.Position = CurrentPosition + new Vector2(PanelSize.X - xmlButton.Size.X - Padding.X, 0f);

            InsertMultiSlider(name, tooltip,
                EditorUI.Vector3AxisNames,
                new float[] { min.X, min.Y, min.Z },
                new float[] { max.X, max.Y, max.Z },
                new float[] { val.X, val.Y, val.Z },
                (def == null ? null : new float[] { def.Value.X, def.Value.Y, def.Value.Z }),
                round, (dim, value) =>
                {
                    Vector3 vec = prop.GetValue();
                    vec.SetDim(dim, value);
                    prop.SetValue(vec);
                    EditorUI.Instance.SelectedParticle.Refresh();
                });
        }

        public void PropNumberBoxVector3(object propHost, MyConstPropertyVector3 prop, bool enforceLimits = false)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            Vector3? def = EditorUI.GetDefaultPropData(propHost, prop).ValueVector3;
            if(EditorUI.GetOriginalPropData(propHost, prop.Name, out GenerationProperty propOB))
                def = propOB.ValueVector3;

            PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
            string name = PropertyData.GetName(propInfo, prop);
            string tooltip = PropertyData.GetTooltip(propInfo, prop);

            ValueInfo<Vector3> valueRange = propInfo.ValueRangeVector3;
            Vector3 min = (valueRange.Min == Vector3.Zero ? Vector3.Zero : Vector3.MinValue);
            Vector3 max = Vector3.MaxValue;
            if(valueRange.LimitNumberBox)
            {
                min = valueRange.Min;
                max = valueRange.Max;
            }

            int dragRound = valueRange.Rounding;
            int inputRound = valueRange.InputRounding;

            Vector3 val = prop.GetValue();

            MyGuiControlLabel titleLabel = CreateLabel(name);
            titleLabel.SetToolTip(tooltip);
            PositionControlsNoSize(titleLabel);

            titleLabel.ColorMask = (valueRange.InvalidValue.HasValue && val == valueRange.InvalidValue.Value ? Color.Red : Color.White);

            MyGuiControlBase[] controls = new MyGuiControlBase[3 * 2];

            for(int i = 0; i < 3; i++)
            {
                int dim = i; // required for reliable capture

                MyGuiControlLabel dimLabel = CreateLabel(EditorUI.Vector3AxisNames[dim]);

                UINumberBox box = CreateNumberBox(tooltip,
                    val.GetDim(dim), (def == null ? (float?)null : def.Value.GetDim(dim)),
                    min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                    (value) =>
                    {
                        Vector3 vec = prop.GetValue();
                        vec.SetDim(dim, value);
                        prop.SetValue(vec);
                        EditorUI.Instance.SelectedParticle.Refresh();

                        titleLabel.ColorMask = (valueRange.InvalidValue.HasValue && vec == valueRange.InvalidValue.Value ? Color.Red : Color.White);
                    });

                float boxWidth = 0.1f - dimLabel.Size.X - ControlSpacing;
                box.Size = new Vector2(boxWidth, box.Size.Y);

                controls[i * 2] = dimLabel;
                controls[i * 2 + 1] = box;
            }

            PositionControlsNoSize(controls);

            MoveY(0.01f); // HACK: box is taller than its actual size
        }

        public void PropCheckbox(object propHost, MyConstPropertyBool prop)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            bool def = EditorUI.GetDefaultPropData(propHost, prop).ValueBool;
            if(EditorUI.GetOriginalPropData(propHost, prop.Name, out GenerationProperty propOB))
                def = propOB.ValueBool;

            PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
            string name = PropertyData.GetName(propInfo, prop);
            string tooltip = PropertyData.GetTooltip(propInfo, prop);

            tooltip = $"{tooltip}\n\nOriginal value: {def}";

            InsertCheckbox(name, tooltip, prop.GetValue(), (value) =>
            {
                prop.SetValue(value);
                EditorUI.Instance.SelectedParticle.Refresh();
            });
        }

        public void PropAnimated(object propHost, IMyAnimatedProperty prop, bool colorKeys = false)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
            string name = PropertyData.GetName(propInfo, prop);
            string tooltip = PropertyData.GetTooltip(propInfo, prop);

            MyGuiControlButton xmlButton = SideXmlButton(propHost, prop);

            string label = name; // the button label that contains value too

            float highestTime = 0;
            for(int i = 0; i < prop.GetKeysCount(); i++)
            {
                prop.GetKey(i, out float time, out _);
                highestTime = Math.Max(time, highestTime);
            }

            if(prop.Is2D)
            {
                string moreDimmensions = "";
                //if(prop.GetKeysCount() > 1)
                //    moreDimmensions = $" ({prop.GetKeysCount()} vertical keys)";

                if(prop is MyAnimatedProperty2DVector4 propVec4)
                {
                    Vector4 startValue = propVec4.GetInterpolatedValue<Vector4>(0f, 0f);
                    Vector4 endValue = propVec4.GetInterpolatedValue<Vector4>(highestTime, 1f);
                    int round = propInfo.ValueRangeVector4.Rounding;

                    if(startValue == endValue)
                        label = $"{label}: [{startValue.FormatVector(round)}] {moreDimmensions}";
                    else
                        label = $"{label}: [{startValue.FormatVector(round)}] to [{endValue.FormatVector(round)}] {moreDimmensions}";
                }

                if(prop is MyAnimatedProperty2DVector3 propVec3)
                {
                    Vector3 startValue = propVec3.GetInterpolatedValue<Vector3>(0f, 0f);
                    Vector3 endValue = propVec3.GetInterpolatedValue<Vector3>(highestTime, 1f);
                    int round = propInfo.ValueRangeVector3.Rounding;

                    if(startValue == endValue)
                        label = $"{label}: [{startValue.FormatVector(round)}] {moreDimmensions}";
                    else
                        label = $"{label}: [{startValue.FormatVector(round)}] to [{endValue.FormatVector(round)}] {moreDimmensions}";
                }

                if(prop is MyAnimatedProperty2DFloat propFloat)
                {
                    float startValue = propFloat.GetInterpolatedValue<float>(0f, 0f);
                    float endValue = propFloat.GetInterpolatedValue<float>(highestTime, 1f);
                    int round = propInfo.ValueRangeNum.Rounding;

                    if(startValue == endValue)
                        label = $"{label}: {Math.Round(startValue, round)} {moreDimmensions}";
                    else
                        label = $"{label}: {Math.Round(startValue, round)} to {Math.Round(endValue, round)} {moreDimmensions}";
                }
            }
            else
            {
                if(prop is MyAnimatedPropertyVector4 propVec4)
                {
                    propVec4.GetInterpolatedValue(0f, out Vector4 startValue);
                    propVec4.GetInterpolatedValue(highestTime, out Vector4 endValue);
                    int round = propInfo.ValueRangeVector4.Rounding;

                    if(startValue == endValue)
                        label = $"{label}: [{startValue.FormatVector(round)}]";
                    else
                        label = $"{label}: [{startValue.FormatVector(round)}] to [{endValue.FormatVector(round)}]";
                }

                if(prop is MyAnimatedPropertyVector3 propVec3)
                {
                    propVec3.GetInterpolatedValue(0f, out Vector3 startValue);
                    propVec3.GetInterpolatedValue(highestTime, out Vector3 endValue);
                    int round = propInfo.ValueRangeVector3.Rounding;

                    if(startValue == endValue)
                        label = $"{label}: [{startValue.FormatVector(round)}]";
                    else
                        label = $"{label}: [{startValue.FormatVector(round)}] to [{endValue.FormatVector(round)}]";
                }

                if(prop is MyAnimatedPropertyFloat propFloat)
                {
                    propFloat.GetInterpolatedValue(0f, out float startValue);
                    propFloat.GetInterpolatedValue(highestTime, out float endValue);
                    int round = propInfo.ValueRangeNum.Rounding;

                    if(startValue == endValue)
                        label = $"{label}: {Math.Round(startValue, 2)}";
                    else
                        label = $"{label}: {Math.Round(startValue, 2)} to {Math.Round(endValue, 2)}";
                }
            }

            MyGuiControlButton editorButton = CreateButton(label, $"{tooltip}\n\n(Opens animated property editor)", MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, clicked: (b) =>
            {
                UIAnimatedValueEditor editor = new UIAnimatedValueEditor(propHost, prop.Name, name, tooltip, prop.Is2D);
                editor.UseValueAsKeyColor = colorKeys;

                editor.GetData = () =>
                {
                    return prop.SerializeToObjectBuilder();
                };

                editor.SaveData = (propOB) =>
                {
                    prop.ClearKeys(); // HACK: required to avoid keys piling up
                    prop.DeserializeFromObjectBuilder(propOB);
                    Notifications.Show($"Succesfully edited '{name}'", 3);

                    EditorUI.Instance.SelectedParticle.Refresh();
                    EditorUI.Instance.RefreshUI();
                };

                editor.GetProp = () => prop;

                editor.FinishSetup();

                MyGuiSandbox.AddScreen(editor);
            });

            editorButton.Size = new Vector2(AvailableWidth * (9f / 10f), editorButton.Size.Y);
            xmlButton.Size = new Vector2(AvailableWidth * (1f / 10f), xmlButton.Size.Y);

            PositionControlsNoSize(editorButton, xmlButton);
        }

        public void PropComboBox(object propHost, MyConstPropertyEnum prop)
        {
            SkipProperties?.Add(prop);

            if(SectionHidesControl())
                return;

            List<string> names = prop.GetEnumStrings();
            int[] values = (int[])Enum.GetValues(prop.GetEnumType());

            MyGuiControlLabel label = CreateLabel(prop.Name);
            label.SetToolTip(prop.Description);

            MyGuiControlCombobox combo = CreateComboBox();
            combo.SetToolTip(prop.Description);

            PositionControls(label, combo);

            int defaultVal = prop.GetDefaultValue();
            for(int i = 0; i < names.Count; i++)
            {
                int value = values[i];
                string name = names[i];
                combo.AddItem(value, (value == defaultVal ? name + " (default)" : name), sort: false);
            }

            combo.SelectItemByKey(prop.GetValue(), false);

            combo.ItemSelected += () =>
            {
                prop.SetValue((int)combo.GetSelectedKey());
                EditorUI.Instance.SelectedParticle.Refresh();
            };
        }

        const float VerticalSeparatorOffsetY = 0.005f;

        public CollapsibleSection CollapsibleSectionStart(CollapsibleSection section)
        {
            Section = section;
            section.Host = this;
            section.VerticalSeparatorStart = CurrentPosition + new Vector2(0, VerticalSeparatorOffsetY);

            MyGuiControlButton button = CreateButton("    " + section.Name, "Click to expand/collapse", MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, (b) =>
            {
                section.ContentsVisible = !section.ContentsVisible;
                EditorUI.Instance.RefreshUI();
            });
            button.Size = new Vector2(AvailableWidth * 0.4f, button.Size.Y);
            PositionControlsNoSize(button);

            Indent = 0.02f;

            return section;
        }

        internal void CollapsibleSectionEnd(CollapsibleSection section)
        {
            //if(section.ContentsVisible)
            {
                Vector4 barColor = new Vector4(0.2f, 0.4f, 0.3f, 0.5f);

                if(section.ContentsVisible)
                    barColor = new Vector4(0.2f, 0.5f, 0.7f, 0.6f);

                MyGuiControlSeparatorList vertSeparator = new MyGuiControlSeparatorList();
                vertSeparator.Size = new Vector2(Indent, Math.Abs(CurrentPosition.Y - section.VerticalSeparatorStart.Y - VerticalSeparatorOffsetY));

                // positioning it centered vertically to avoid it vanishing on scroll
                vertSeparator.Position = section.VerticalSeparatorStart + new Vector2(Padding.X, vertSeparator.Size.Y / 2);
                vertSeparator.AddVertical(new Vector2(0, vertSeparator.Size.Y / -2), vertSeparator.Size.Y, Indent - Padding.X, barColor);

                Add(vertSeparator);
            }

            Indent = 0;
            Section = null;
        }

        public bool SectionHidesControl()
        {
            if(Section != null)
            {
                if(!Section.ContentsVisible)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class CollapsibleSection : IDisposable
    {
        public bool ContentsVisible = false;
        public VerticalControlsHost Host;

        public readonly string Name;

        public Vector2 VerticalSeparatorStart;

        public CollapsibleSection(string name)
        {
            Name = name;
        }

        public void Dispose()
        {
            Host.CollapsibleSectionEnd(this);
        }
    }
}