using System;
using System.Reflection;
using Sandbox.Game.GUI;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage;
using VRage.Audio;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor.UIControls
{
    public class UINumberBox : MyGuiControlTextbox
    {
        public float Min;
        public float Max;
        public int InputRound;
        public int DragRound;
        public float? DefaultValue;

        public double DragValueMultiplier = 1;

        public event Action<float> NumberChanged;

        bool Dragging = false;
        Vector2 DragInitialMousePos;
        double DragInitialValue;
        double DragAmount;

        Action<MyGuiControlTextbox, MyRectangle2D> TextAreaRelativeSetter;

        const MyMouseButtonsEnum MouseButton = MyMouseButtonsEnum.Right; // adjust tooltip too if changing this

        public UINumberBox(Vector2? position = null, float initialValue = 0, float min = float.MinValue, float max = float.MaxValue, int inputRound = 6, int dragRound = 2, float? defaultValue = null, string tooltip = null)
             : base(position, Math.Round(initialValue, inputRound).ToString(), maxLength: 64, type: MyGuiControlTextboxType.Normal)
        {
            OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP;
            Size = new Vector2(0.12f, Size.Y);

            const float HeightPx = 36f; // 48f;
            const float EdgeWidthPx = 2f; // 8f;

            MyGuiCompositeTexture texture = new MyGuiCompositeTexture()
            {
                LeftTop = new MyGuiSizedTexture
                {
                    Texture = "Textures\\GUI\\Controls\\textbox_left.dds",
                    SizePx = new Vector2(EdgeWidthPx, HeightPx)
                },
                CenterTop = new MyGuiSizedTexture
                {
                    Texture = "Textures\\GUI\\Controls\\textbox_center.dds",
                    SizePx = new Vector2(4f, HeightPx)
                },
                RightTop = new MyGuiSizedTexture
                {
                    Texture = "Textures\\GUI\\Controls\\textbox_right.dds",
                    SizePx = new Vector2(EdgeWidthPx, HeightPx)
                },
            };

            MyGuiCompositeTexture textureHighlight = new MyGuiCompositeTexture()
            {
                LeftTop = new MyGuiSizedTexture
                {
                    Texture = "Textures\\GUI\\Controls\\textbox_left_highlight.dds",
                    SizePx = new Vector2(EdgeWidthPx, HeightPx)
                },
                CenterTop = new MyGuiSizedTexture
                {
                    Texture = "Textures\\GUI\\Controls\\textbox_center_highlight.dds",
                    SizePx = new Vector2(4f, HeightPx)
                },
                RightTop = new MyGuiSizedTexture
                {
                    Texture = "Textures\\GUI\\Controls\\textbox_right_highlight.dds",
                    SizePx = new Vector2(EdgeWidthPx, HeightPx)
                },
            };

            ApplyStyle(new StyleDefinition()
            {
                //NormalTexture = MyGuiConstants.TEXTURE_TEXTBOX,
                //HighlightTexture = MyGuiConstants.TEXTURE_TEXTBOX_HIGHLIGHT,
                //FocusTexture = MyGuiConstants.TEXTURE_TEXTBOX_FOCUS,
                NormalTexture = texture,
                HighlightTexture = textureHighlight,
                FocusTexture = textureHighlight,
                NormalFont = "White",
                HighlightFont = "White",
                TextColor = Color.White,
                TextColorFocus = Color.White,
                TextColorHighlight = Color.White,
            });
            TextScale = 0.75f;

            Min = min;
            Max = max;
            InputRound = inputRound;
            DragRound = dragRound;
            DefaultValue = defaultValue;

            if(DragRound == 1)
                DragValueMultiplier = 10;
            else if(DragRound == 0)
                DragValueMultiplier = 100;

            string help = $"Hold RMB and drag horizontally to adjust.\nWhile dragging, hold Ctrl to round to {DragRound / 2}\nPress C to clear the text box.";

            if(DefaultValue.HasValue)
                help = $"Default value: {DefaultValue.Value} (press D to reset to this)\n\n{help}";

            if(tooltip != null)
                SetToolTip($"{tooltip}\n\n{help}");
            else
                SetToolTip(help);

            MoveCarriageToEnd();

            TextChanged += OnTextChanged;

            HackProperSizing();
        }

        void HackProperSizing()
        {
            try
            {
                FieldInfo field = typeof(MyGuiControlTextbox).GetField("m_textAreaRelative", BindingFlags.SetField | BindingFlags.Instance | BindingFlags.NonPublic);

                if(field == null)
                    Log.Error($"Cannot find field 'm_textAreaRelative' in {nameof(MyGuiControlTextbox)}");
                else
                    TextAreaRelativeSetter = field.CreateSetter<MyGuiControlTextbox, MyRectangle2D>();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void OnTextChanged(MyGuiControlTextbox _)
        {
            bool isEmpty = string.IsNullOrWhiteSpace(Text);
            float value = 0;

            if(isEmpty || float.TryParse(Text, out value))
            {
                ColorMask = Color.White;

                if(isEmpty)
                    value = 0;

                //value = MathHelper.Clamp(value, Min, Max);
                value = (float)Math.Round(value, InputRound);

                NumberChanged?.Invoke(value);
            }
            else
            {
                ColorMask = Color.Red;
            }
        }

        public override MyGuiControlBase HandleInput()
        {
            // TODO fix the delay after releasing C and it allowing input...
            if(IsMouseOver && MyInput.Static.IsKeyPress(MyKeys.C) && !MyInput.Static.IsAnyCtrlKeyPressed())
            {
                Text = "";
                return this;
            }

            if(DefaultValue.HasValue && IsMouseOver && MyInput.Static.IsKeyPress(MyKeys.D)) // && !MyInput.Static.IsAnyCtrlKeyPressed())
            {
                Text = DefaultValue.Value.ToString();
                return this;
            }

            bool dragStartedThisUpdate = false;

            if(IsMouseOver && MyInput.Static.IsNewMousePressed(MouseButton))
            {
                Dragging = true;
                dragStartedThisUpdate = true;
            }

            if(Dragging)
            {
                if(MyInput.Static.IsMousePressed(MouseButton))
                {
                    if(string.IsNullOrWhiteSpace(Text))
                    {
                        Text = (Min > 0 ? Min.ToString() : "0");
                    }

                    if(double.TryParse(Text, out double value))
                    {
                        Vector2 mousePos = MyVRage.Platform.Input.MousePosition;
                        Vector2 mouseArea = MyVRage.Platform.Input.MouseAreaSize;

                        if(dragStartedThisUpdate)
                        {
                            DragAmount = 0;
                            DragInitialValue = value;
                            DragInitialMousePos = mousePos;
                        }

                        MyVRage.Platform.Input.MousePosition = DragInitialMousePos;

                        float diff = (mousePos.X - DragInitialMousePos.X);

                        if(MyInput.Static.IsKeyPress(MyKeys.R))
                        {
                            DragAmount = 0;
                            diff = 0;
                            value = DragInitialValue;
                        }

                        if(Math.Abs(diff) > 0f)
                        {
                            DragAmount += (diff / mouseArea.X) * 3f * DragValueMultiplier;
                            DragAmount = MathHelper.Clamp(DragInitialValue + DragAmount, Min, Max) - DragInitialValue;

                            value = DragInitialValue + DragAmount;

                            if(MyInput.Static.IsAnyCtrlKeyPressed())
                                value = Math.Round(value, DragRound / 2);
                            else
                                value = Math.Round(value, DragRound);
                        }

                        Text = value.ToString();

                        MoveCarriageToEnd();
                    }
                    else
                    {
                        if(dragStartedThisUpdate)
                        {
                            MyGuiAudio.PlaySound(MyGuiSounds.HudUnable);
                        }
                    }

                    return this;
                }
                else
                {
                    Dragging = false;
                }
            }

            // auto-focus on hover, auto-unfocus on unhover (which also prevents input reading).
            if(IsMouseOver)
            {
                if(MyScreenManager.FocusedControl != this)
                {
                    MyGuiScreenBase screen = MyScreenManager.GetScreenWithFocus();
                    if(screen != null)
                        screen.FocusedControl = this;
                }
            }
            else
            {
                if(MyScreenManager.FocusedControl == this)
                {
                    MyGuiScreenBase screen = MyScreenManager.GetScreenWithFocus();
                    if(screen != null)
                        screen.FocusedControl = null;
                }
            }

            return base.HandleInput();
        }

        public override void Draw(float transitionAlpha, float backgroundTransitionAlpha)
        {
            Vector2 padding = new Vector2(0.0075f, 0.003f);
            MyRectangle2D textAreaRelative = new MyRectangle2D(padding, base.Size - (2f * padding));

            // HACK: fixing huge padding
            if(TextAreaRelativeSetter != null)
            {
                TextAreaRelativeSetter.Invoke(this, textAreaRelative);
            }

            base.Draw(transitionAlpha, backgroundTransitionAlpha);

            if(TextAreaRelativeSetter != null)
            {
                if(string.IsNullOrWhiteSpace(Text))
                {
                    // this will not modify it in TextAreaRelativeSetter, and that is good.
                    textAreaRelative.LeftTop += GetPositionAbsoluteTopLeft();
                    //RectangleF normalizedRectangle = new RectangleF(textAreaRelative.LeftTop, new Vector2(textAreaRelative.Size.X, textAreaRelative.Size.Y * 2f));
                    //using(MyGuiManager.UsingScissorRectangle(ref normalizedRectangle))
                    {
                        Vector2 normalizedCoord = new Vector2(textAreaRelative.LeftTop.X, textAreaRelative.LeftTop.Y);
                        MyGuiManager.DrawString(TextFont, "0", normalizedCoord, TextScaleWithLanguage, ApplyColorMaskModifiers(Color.DimGray, Enabled, transitionAlpha));
                    }
                }
            }


            // TODO: some visual cue for dragging... or visual buttons to click?

            //if(IsMouseOver && MyInput.Static.IsMousePressed(MouseButton))
            //{
            //    Vector2 mousePos = MyGuiManager.MouseCursorPosition;
            //}


            //Vector2 fromPx = MyGuiManager.GetHudPixelCoordFromNormalizedCoord(MousePosAtClick);
            //Vector2 toPx = MyGuiManager.GetHudPixelCoordFromNormalizedCoord(mousePos);

            //MyRenderProxy.DebugDrawLine2D(fromPx, toPx, Color.Lime, Color.Red);
        }
    }
}
