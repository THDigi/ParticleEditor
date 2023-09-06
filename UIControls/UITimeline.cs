using System;
using System.Collections.Generic;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor.UIControls
{
    public class UITimeline : MyGuiControlBase
    {
        public class Key
        {
            public float Position;

            public Key(float time)
            {
                Position = time;
            }
        }

        List<Key> Keys = new List<Key>();

        public Func<IEnumerable<float>> GetKeys;
        public Action<float, object> AddKey;
        public Action<int> RemoveKey;
        public Action<int, float> SetKey;
        public Action<int> EditKeyValueGUI;
        public Func<int, object> GetKeyValue;
        public Func<float, int, int, object> GetInterpolatedValue;
        public Func<int, float, Vector4> CustomKeyColor;
        public Func<int, string> GetKeyTooltip;
        public Func<Type> GetValueType;

        string GeneralTooltip;

        public Key AimedKey { get; private set; }
        Key MovingKey;
        float DragOffset;
        float MouseOnTimeline;

        const float InsideOffset = 0.01f; // MyGuiConstants.SLIDER_INSIDE_OFFSET_X;
        float GetSliderStart() => GetPositionAbsoluteTopLeft().X + InsideOffset;
        float GetSliderEnd() => GetPositionAbsoluteTopLeft().X + (Size.X - InsideOffset);

        public static object Clipboard { get; private set; }
        // TODO: use system clipboard?
        //{
        //    get
        //    {
        //        string text = MyVRage.Platform.System.Clipboard;
        //
        //        if(float.TryParse(text, out float num))
        //            return num;
        //
        //        // how the heck do I parse vector3 or 4 to be compatible with all sorts of things people might copy xD
        //
        //        return null;
        //    }
        //    set
        //    {
        //        if(value is float)
        //            MyVRage.Platform.System.Clipboard = "";
        //    }
        //}

        const float AccuracyToSelectKey = 0.02f;

        #region Style stuff
        public enum StyleEnum
        {
            Default = 0,
        }

        public class StyleDefinition
        {
            public MyGuiCompositeTexture RailTexture;
            public MyGuiHighlightTexture KeyTexture;
        }

        StyleEnum _visualStyle;
        public StyleEnum VisualStyle
        {
            get => _visualStyle;
            set
            {
                _visualStyle = value;
                RefreshVisualStyle();
            }
        }

        public void ApplyStyle(StyleDefinition style)
        {
            if(style != null)
            {
                Style = style;
                RefreshInternals();
            }
        }

        public float OverallScale { get; set; } = 1f;

        StyleDefinition Style;
        MyGuiCompositeTexture TextureRail;

        static StyleDefinition[] Styles;

        public static StyleDefinition GetVisualStyle(StyleEnum style) => Styles[(int)style];

        static UITimeline()
        {
            Styles = new StyleDefinition[MyUtils.GetMaxValueFromEnum<StyleEnum>() + 1];

            Styles[(int)StyleEnum.Default] = new StyleDefinition
            {
                RailTexture = MyGuiConstants.TEXTURE_SLIDER_RAIL,
                KeyTexture = new MyGuiHighlightTexture
                {
                    Normal = @"Textures\GUI\blank.dds", // it's white so we can color it nicely
                    SizePx = new Vector2(16f, 38f)
                },
            };
        }
        #endregion

        public UITimeline(Vector2? position = null, string tooltip = null, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP)
            : base(position, null, null, null, null, true, true, MyGuiControlHighlightType.WHEN_CURSOR_OVER, originAlign)
        {
            VisualStyle = StyleEnum.Default;

            //string controlHints = "A = add key, D = delete key\nE or Ctrl+Click = edit value\nC = copy value, V = add with value from memory\nI = add interpolated value between 2 keys";
            string controlHints = "Rightclick for options and hotkeys.";
            GeneralTooltip = string.IsNullOrEmpty(tooltip) ? controlHints : tooltip + "\n\n" + controlHints;
            SetToolTip(GeneralTooltip);

            RefreshInternals();
        }

        /// <summary>
        /// Call after assigning all callbacks
        /// </summary>
        public void FinishSetup()
        {
            if(GetKeys == null)
                throw new ArgumentNullException("GetKeys");

            Keys.Clear();

            foreach(float time in GetKeys.Invoke())
            {
                Keys.Add(new Key(time));
            }
        }

        public override void OnRemoving()
        {
            FinishMovingKey();
            MovingKey = null;
            base.OnRemoving();
        }

        void FinishMovingKey()
        {
            if(MovingKey == null)
                return;

            int index = Keys.IndexOf(MovingKey);
            if(index == -1)
                throw new Exception("MovingKey not in Keys list?!?");

            SetKey?.Invoke(index, MovingKey.Position);
        }

        public void ContextMenu_Add()
        {
            if(AddKey == null)
                return;

            float time = MathHelper.Clamp(MouseOnTimeline, 0, 1);
            Key createdKey = new Key(time);
            Keys.Add(createdKey);

            AddKey?.Invoke(createdKey.Position, null);
        }

        public void ContextMenu_AddInterpolated()
        {
            if(AddKey == null || GetInterpolatedValue == null)
                return;

            float time = MathHelper.Clamp(MouseOnTimeline, 0, 1);
            Key closestLeft = null;
            Key closestRight = null;

            if(Keys.Count >= 2)
            {
                foreach(Key key in Keys)
                {
                    if(key.Position < time)
                    {
                        if(closestLeft == null || closestLeft.Position < key.Position)
                            closestLeft = key;
                    }
                    else
                    {
                        if(closestRight == null || closestRight.Position > key.Position)
                            closestRight = key;
                    }
                }
            }

            if(closestLeft == null || closestRight == null)
            {
                Notifications.Show("Aim between 2 existing keys to add one with interpolated value.", 3, Color.Red);
            }
            else
            {
                object value = GetInterpolatedValue?.Invoke(time, Keys.IndexOf(closestLeft), Keys.IndexOf(closestRight));
                if(value != null)
                {
                    Key createdKey = new Key(time);
                    Keys.Add(createdKey);
                    AddKey?.Invoke(createdKey.Position, value);
                }
            }
        }

        public void ContextMenu_AddPaste()
        {
            if(Clipboard == null)
            {
                Notifications.Show("Clipboard is empty, nothing to paste.", 3, Color.Red);
                return;
            }

            if(AddKey == null)
                return;

            if(GetValueType == null || Clipboard.GetType() != GetValueType.Invoke())
            {
                Notifications.Show($"Cannot paste a '{Clipboard.GetType()}' type onto timeline's '{(GetValueType?.Invoke()?.ToString() ?? "(unknown)")}' type.", 3, Color.Red);
                return;
            }

            float time = MathHelper.Clamp(MouseOnTimeline, 0, 1);
            Key createdKey = new Key(time);
            Keys.Add(createdKey);

            AddKey?.Invoke(createdKey.Position, Clipboard);
        }

        public void ContextMenu_Copy(Key aimKey)
        {
            if(aimKey == null)
                return;

            int index = Keys.IndexOf(aimKey);
            if(index == -1)
                throw new Exception("aimKey not in Keys list?!?");

            Clipboard = GetKeyValue.Invoke(index);

            if(Clipboard == null)
            {
                Log.Error($"Couldn't copy! Given index: {index}; Keys ({Keys.Count}): {string.Join(" / ", Keys)}");
                return;
            }

            Notifications.Show($"Copied key #{index + 1}'s value: {Clipboard}");
        }

        public void ContextMenu_Edit(Key aimKey)
        {
            if(aimKey == null)
                return;

            int index = Keys.IndexOf(aimKey);
            if(index == -1)
                throw new Exception("aimKey not in Keys list?!?");

            AimedKey = null; // it is correct, this one needs to be null, not aimKey
            MovingKey = null;
            EditKeyValueGUI?.Invoke(index);
        }

        public void ContextMenu_PasteReplace(Key aimKey)
        {
            if(aimKey == null || AddKey == null || RemoveKey == null)
                return;

            if(Clipboard == null)
            {
                Notifications.Show("Clipboard is empty, nothing to paste.", 3, Color.Red);
                return;
            }

            if(GetValueType == null || Clipboard.GetType() != GetValueType.Invoke())
            {
                Notifications.Show($"Cannot paste a '{Clipboard.GetType()}' type onto timeline's '{(GetValueType?.Invoke()?.ToString() ?? "(unknown)")}' type.", 3, Color.Red);
                return;
            }

            int index = Keys.IndexOf(aimKey);
            if(index == -1)
                throw new Exception("aimKey not in Keys list?!?");

            float time = Keys[index].Position;

            Keys.RemoveAt(index);
            RemoveKey?.Invoke(index);

            Key createdKey = new Key(time);
            Keys.Add(createdKey);
            AddKey?.Invoke(createdKey.Position, Clipboard);

            Notifications.Show($"Replaced value for key #{index + 1} with: {Clipboard}");
        }

        public void ContextMenu_Delete(Key aimKey)
        {
            if(aimKey == null)
                return;

            int index = Keys.IndexOf(aimKey);
            if(index == -1)
                throw new Exception("aimKey not in Keys list?!?");

            Keys.RemoveAt(index);
            AimedKey = null; // it is correct, this one needs to be null, not aimKey
            MovingKey = null;
            RemoveKey?.Invoke(index);
        }

        public override MyGuiControlBase HandleInput()
        {
            MyGuiControlBase ret = base.HandleInput();
            if(ret != null)
                return ret;

            try
            {
                if(!Enabled)
                    return null;

                float start = GetSliderStart();
                float end = GetSliderEnd();
                MouseOnTimeline = (MyGuiManager.MouseCursorPosition.X - start) / (end - start); // NOTE: can go below 0 and beyond 1

                bool wasAimed = (AimedKey != null);

                AimedKey = null;

                if(MovingKey != null && MyInput.Static.IsNewLeftMouseReleased())
                {
                    FinishMovingKey();
                    MovingKey = null;
                }

                if(IsMouseOver)
                {
                    AimedKey = GetAimedKey(MouseOnTimeline);

                    if(AimedKey != null && MovingKey == null && MyInput.Static.IsLeftMousePressed() && !MyInput.Static.IsAnyCtrlKeyPressed())
                    {
                        MovingKey = AimedKey;
                        DragOffset = (MovingKey.Position - MouseOnTimeline);
                    }

                    if(MyInput.Static.IsNewKeyPressed(MyKeys.I))
                    {
                        ContextMenu_AddInterpolated();
                    }

                    if(MyInput.Static.IsNewKeyPressed(MyKeys.A))
                    {
                        ContextMenu_Add();
                    }

                    if(MyInput.Static.IsNewKeyPressed(MyKeys.V))
                    {
                        if(AimedKey != null && MovingKey == null)
                            ContextMenu_PasteReplace(AimedKey);
                        else
                            ContextMenu_AddPaste();
                    }

                    if(AimedKey != null && MovingKey == null)
                    {
                        if(MyInput.Static.IsNewKeyPressed(MyKeys.E) || (MyInput.Static.IsNewLeftMousePressed() && MyInput.Static.IsAnyCtrlKeyPressed()))
                        {
                            ContextMenu_Edit(AimedKey);
                        }

                        if(MyInput.Static.IsNewKeyPressed(MyKeys.C))
                        {
                            ContextMenu_Copy(AimedKey);
                        }

                        if(MyInput.Static.IsNewKeyPressed(MyKeys.D))
                        {
                            ContextMenu_Delete(AimedKey);
                        }
                    }
                }

                {
                    bool useGeneral = true;
                    Key keyForTooltip = MovingKey ?? AimedKey;

                    if(keyForTooltip != null)
                    {
                        int index = Keys.IndexOf(keyForTooltip);
                        if(index != -1)
                        {
                            string text = $"At particle lifetime: {Math.Round(keyForTooltip.Position * 100, 2)}%";

                            if(GetKeyTooltip != null)
                                text += "\n" + GetKeyTooltip.Invoke(index);

                            if(GeneralTooltip != null)
                                text += "\n" + GeneralTooltip;

                            if(MyInput.Static.IsLeftMousePressed())
                                text += "\n\n(Hold ctrl to round to integer percentages)"; // TODO: add this on various other sliders

                            SetToolTip(text);
                            useGeneral = false;
                        }
                    }

                    if(useGeneral)
                        SetToolTip(GeneralTooltip);
                }

                if(MovingKey != null)
                {
                    // finishes editing on drag release
                    MovingKey.Position = MathHelper.Clamp(MouseOnTimeline + DragOffset, 0, 1);

                    if(MyInput.Static.IsAnyCtrlKeyPressed())
                        MovingKey.Position = (float)Math.Round(MovingKey.Position, 2);

                    ret = this;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            return ret;
        }

        Key GetAimedKey(float mousePos)
        {
            Key ClosestKey = null;
            float ClosestDist = float.MaxValue;

            foreach(Key key in Keys)
            {
                float distance = Math.Abs(key.Position - mousePos);

                if(distance <= AccuracyToSelectKey && distance < ClosestDist)
                {
                    ClosestKey = key;
                    ClosestDist = distance;
                }
            }

            return ClosestKey;
        }

        public override void Draw(float transitionAlpha, float backgroundTransitionAlpha)
        {
            base.Draw(transitionAlpha, backgroundTransitionAlpha);

            Vector2 posTopLeft = GetPositionAbsoluteTopLeft();

            Color color = ApplyColorMaskModifiers(ColorMask, Enabled, transitionAlpha);

            TextureRail.Draw(posTopLeft, Size, color, OverallScale);

            float start = GetSliderStart();
            float end = GetSliderEnd();

            for(int i = 0; i < Keys.Count; i++)
            {
                Key key = Keys[i];
                float x = MathHelper.Lerp(start, end, key.Position);
                float y = posTopLeft.Y + Size.Y / 2f;

                Vector4 borderColor = MyGuiConstants.THEMED_GUI_LINE_COLOR;
                if(MovingKey == key)
                    borderColor = new Vector4(0f, 0.5f, 1f, 1f);
                else if(MovingKey == null && AimedKey == key)
                    borderColor = new Vector4(0f, 0.5f, 0f, 1f);

                Vector2 posCenter = new Vector2(x, y);
                Vector2 size = Style.KeyTexture.SizeGui * OverallScale;

                Vector4 innerColor = EditorUI.ColorBg;
                if(CustomKeyColor != null)
                    innerColor = CustomKeyColor.Invoke(i, key.Position);

                MyGuiManager.DrawSpriteBatch(Style.KeyTexture.Normal, posCenter, size * 0.9f, ApplyColorMaskModifiers(innerColor, true, transitionAlpha),
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);

                MyGuiManager.DrawBorders(posCenter - (size / 2), size, ApplyColorMaskModifiers(borderColor, true, transitionAlpha), 2);
            }

            if(MovingKey != null)
            {
                Vector2 mousePos = MyGuiManager.MouseCursorPosition;

                const string font = "Debug";
                string text = $"{Math.Round(MovingKey.Position * 100, 2)}%";
                float textScale = 0.8f * OverallScale;
                Vector2 textSize = MyGuiManager.MeasureString(font, text, textScale);

                Vector2 pos = new Vector2(MathHelper.Lerp(start, end, MovingKey.Position) - (textSize.X / 2), posTopLeft.Y);

                Vector2 padding = new Vector2(0.01f);
                MyGuiManager.DrawRectangle(pos - (padding / 2), textSize + padding, EditorUI.ColorBgDarker);
                MyGuiManager.DrawString(font, text, pos, textScale, drawAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            }
        }

        protected override void OnSizeChanged()
        {
            base.OnSizeChanged();
            RefreshInternals();
        }

        protected override void OnHasHighlightChanged()
        {
            base.OnHasHighlightChanged();
            RefreshInternals();
        }

        public override void OnFocusChanged(bool focus)
        {
            base.OnFocusChanged(focus);
            RefreshInternals();
        }

        private void RefreshVisualStyle()
        {
            Style = GetVisualStyle(VisualStyle);
            RefreshInternals();
        }

        void RefreshInternals()
        {
            if(Style == null)
                Style = Styles[0];

            TextureRail = Style.RailTexture;

            // both of these also set Size
            MinSize = TextureRail.MinSizeGui * OverallScale;
            MaxSize = TextureRail.MaxSizeGui * OverallScale;
        }
    }
}