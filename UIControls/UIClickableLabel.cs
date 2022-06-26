using System;
using Sandbox.Graphics.GUI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor.UIControls
{
    public class UIClickableLabel : MyGuiControlLabel
    {
        public event Action Clicked;

        public UIClickableLabel(Vector2? position = null, Vector2? size = null, string text = null, Vector4? colorMask = null, float textScale = 0.8F, string font = "Blue", MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, bool isAutoEllipsisEnabled = false, float maxWidth = float.PositiveInfinity, bool isAutoScaleEnabled = false)
                         : base(position, size, text, colorMask, textScale, font, originAlign, isAutoEllipsisEnabled, maxWidth, isAutoScaleEnabled)
        {
        }

        public override MyGuiControlBase HandleInput()
        {
            if(IsMouseOver && MyInput.Static.IsNewPrimaryButtonPressed())
            {
                Clicked?.Invoke();
                return this;
            }

            return base.HandleInput();
        }
    }
}
