using System;
using System.Text;
using Sandbox;
using Sandbox.Graphics.GUI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor.UIControls
{
    // A modified copy of PB's MyGuiScreenEditor
    public class UILargeXMLEditorScreen : MyGuiScreenBase
    {
        string Title;
        string TextContents;

        public Action<ResultEnum> ClosedCallback;
        public Func<string, bool> SaveTextCallback;
        public Action<string> ValidateCallback;
        public Action HelpOriginalCallback;
        public Action HelpDefaultCallback;

        ResultEnum CloseResult = ResultEnum.CANCEL;

        MyGuiControlMultilineEditableText TextEditorControl;
        MyGuiControlLabel LineCounterLabel;

        public string GetText() => TextEditorControl.Text.ToString();

        public override string GetFriendlyName() => nameof(UILargeXMLEditorScreen);

        public UILargeXMLEditorScreen(string title, string text, Action<ResultEnum> closedCallback = null, Func<string, bool> saveTextCallback = null)
            : base(new Vector2(0.5f, 0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(1f, 0.9f), isTopMostScreen: false,
                  backgroundTransition: MySandboxGame.Config.UIBkOpacity, guiTransition: MySandboxGame.Config.UIOpacity)
        {
            Title = title;
            TextContents = text;

            ClosedCallback = closedCallback;
            SaveTextCallback = saveTextCallback;

            m_closeOnEsc = true;
            CanBeHidden = true;
            CanHideOthers = true;
            EnabledBackgroundFade = true;
            CloseButtonEnabled = true;

            //RecreateControls(constructor: true);
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);

            AddCaption(Title, captionOffset: new Vector2(0, -0.015f));

            Vector2 textEditorPos = new Vector2(0, 0);
            Vector2 textEditorSize = new Vector2(0.98f, 0.76f);

            // FIXME: code that has scrollbar being ctrl+X'd without scrollbar does not scroll at the top

            // HACK: buttons and whatever else need to be added after this otherwise it'll have the PB editor problem with inability to click those buttons (caused by IsAnyKeyPressed() always returning true)
            TextEditorControl = new MyGuiControlMultilineEditableText(textEditorPos, textEditorSize, Color.White, MyFontEnum.White, 0.8f,
                MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, null, drawScrollbarV: true, drawScrollbarH: true,
                textBoxAlign: MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
            TextEditorControl.IgnoreOffensiveText = true;
            TextEditorControl.TextPadding = new MyGuiBorderThickness(0.005f);
            TextEditorControl.Text = new StringBuilder(TextContents);
            Controls.Add(TextEditorControl);

            float y = 0.42f;

            LineCounterLabel = new MyGuiControlLabel(new Vector2(-0.48f, y), textScale: 0.8f, font: MyFontEnum.White);
            Elements.Add(LineCounterLabel);
            UpdateLineLabel();

            MyGuiControlButtonStyleEnum buttonStyle = MyGuiControlButtonStyleEnum.Default;

            MyGuiControlButton saveButton = new MyGuiControlButton(new Vector2(-0.184f, y), buttonStyle, MyGuiConstants.BACK_BUTTON_SIZE, null,
                MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
            saveButton.Text = "Save and close";
            saveButton.SetToolTip("Validates code, saves it and closes window.");
            saveButton.ButtonClicked += (c) =>
            {
                if(SaveTextCallback == null)
                    throw new Exception("No save callback...");

                if(!SaveTextCallback.Invoke(TextEditorControl.Text.ToString()))
                    return;

                CloseResult = ResultEnum.OK;
                CloseScreen();
            };
            Controls.Add(saveButton);

            if(ValidateCallback != null)
            {
                MyGuiControlButton validateButton = new MyGuiControlButton(new Vector2(-0.001f, y), buttonStyle, MyGuiConstants.BACK_BUTTON_SIZE, null,
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, textScale: 0.8f, textAlignment: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
                    highlightType: MyGuiControlHighlightType.WHEN_CURSOR_OVER);
                validateButton.Text = "Validate code";
                validateButton.SetToolTip("Checks the XML syntax and and schema for errors.");
                validateButton.ButtonClicked += (c) =>
                {
                    ValidateCallback?.Invoke(TextEditorControl.Text.ToString());
                };
                Controls.Add(validateButton);
            }

            if(HelpOriginalCallback != null)
            {
                MyGuiControlButton helpOriginalButton = new MyGuiControlButton(new Vector2(0.182f, y), buttonStyle, MyGuiConstants.BACK_BUTTON_SIZE, null,
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, textScale: 0.8f, textAlignment: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
                    highlightType: MyGuiControlHighlightType.WHEN_CURSOR_OVER);
                helpOriginalButton.Text = "See original";
                helpOriginalButton.SetToolTip("Shows the original XML for this property on this emitter, if it was an existing one.");
                helpOriginalButton.ButtonClicked += (c) =>
                {
                    HelpOriginalCallback?.Invoke();
                };
                Controls.Add(helpOriginalButton);
            }

            if(HelpDefaultCallback != null)
            {
                MyGuiControlButton helpDefaultButon = new MyGuiControlButton(new Vector2(0.365f, y), buttonStyle, MyGuiConstants.BACK_BUTTON_SIZE, null,
                    MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, textScale: 0.8f, textAlignment: MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
                    highlightType: MyGuiControlHighlightType.WHEN_CURSOR_OVER);
                helpDefaultButon.Text = "See default";
                helpDefaultButon.SetToolTip("Shows the default XML for this property in general.");
                helpDefaultButon.ButtonClicked += (c) =>
                {
                    HelpDefaultCallback?.Invoke();
                };
                Controls.Add(helpDefaultButon);
            }

            FocusedControl = TextEditorControl;

            if(MyVRage.Platform.ImeProcessor != null)
                MyVRage.Platform.ImeProcessor.RegisterActiveScreen(this);
        }

        protected override void Canceling()
        {
            base.Canceling();
            CloseResult = ResultEnum.CANCEL;
        }

        public override bool CloseScreen(bool isUnloading = false)
        {
            ClosedCallback?.Invoke(CloseResult);
            return base.CloseScreen(isUnloading);
        }

        public override bool Update(bool hasFocus)
        {
            if(hasFocus && TextEditorControl.CarriageMoved())
            {
                UpdateLineLabel();
            }

            return base.Update(hasFocus);
        }

        void UpdateLineLabel()
        {
            int lines = TextEditorControl.GetTotalNumLines();
            int currentLine = Math.Min(TextEditorControl.GetCurrentCarriageLine(), lines); // HACK: it can go past the total lines for some reason
            LineCounterLabel.Text = $"Line {currentLine} / {lines}";
        }
    }
}