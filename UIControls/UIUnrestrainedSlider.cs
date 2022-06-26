using System;
using Sandbox.Graphics.GUI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor.UIControls
{
    // TODO: make a fully custom control to avoid weirdness
    public class UIUnrestrainedSlider : MyGuiControlSlider
    {
        float _unrestrainedValue;
        public float UnrestraintedValue
        {
            get => _unrestrainedValue;
            set
            {
                IgnoreEvents = true;
                Value = value;
                _unrestrainedValue = value;
                UnrestrainedValueChanged?.Invoke(value);
                IgnoreEvents = false;
            }
        }

        bool IgnoreEvents = false;

        public event Action<float> UnrestrainedValueChanged;

        public UIUnrestrainedSlider(Vector2? position = null, float minValue = 0, float maxValue = 1, float width = 0.29F, float? defaultValue = null, Vector4? color = null, string labelText = null, int labelDecimalPlaces = 1, float labelScale = 0.8F, float labelSpaceWidth = 0, string labelFont = "White", string toolTip = null, MyGuiControlSliderStyleEnum visualStyle = MyGuiControlSliderStyleEnum.Default, MyGuiDrawAlignEnum originAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, bool intValue = false, bool showLabel = false)
            : base(position, minValue, maxValue, width, defaultValue, color, labelText, labelDecimalPlaces, labelScale, labelSpaceWidth, labelFont, toolTip, visualStyle, originAlign, intValue, showLabel)
        {
            _unrestrainedValue = Value;
        }

        protected override void OnValueChange()
        {
            if(IgnoreEvents)
                return;

            UnrestraintedValue = Value;
            base.OnValueChange();
        }

        public override MyGuiControlBase HandleInput()
        {
            if(IsMouseOver && DefaultValue.HasValue && MyInput.Static.IsNewSecondaryButtonPressed())
            {
                Value = DefaultValue.Value;
                UnrestraintedValue = DefaultValue.Value;
                return this;
            }

            return base.HandleInput();
        }
    }
}
