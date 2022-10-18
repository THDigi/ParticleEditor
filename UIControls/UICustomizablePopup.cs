using System;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor.UIControls
{
    public class UICustomizablePopup : MyGuiScreenBase
    {
        VerticalControlsHost Host;

        string CloseButtonTooltip;

        public Action<VerticalControlsHost> ControlGetter;

        static readonly Vector2 DefaultScreenPosition = new Vector2(0.5f, 0.95f);
        static readonly Vector2 DefaultScreenSize = new Vector2(0.5f, 0.2f);

        public override string GetFriendlyName() => nameof(UICustomizablePopup);

        public UICustomizablePopup(string closeButtonTooltip = null, Vector2? screenPosition = null, Vector2? screenSize = null)
            : base(screenPosition ?? DefaultScreenPosition, size: screenSize ?? DefaultScreenSize, isTopMostScreen: false)
        {
            CloseButtonTooltip = closeButtonTooltip;
            Align = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM;

            var size = screenSize ?? DefaultScreenSize;
            Host = new VerticalControlsHost(this, new Vector2(size.X / -2f, -size.Y), size, drawBackground: true);

            m_closeOnEsc = true;
            CanBeHidden = true;
            CanHideOthers = false;
            EnabledBackgroundFade = false;
            CloseButtonEnabled = false;
        }

        public void FinishSetup()
        {
            RecreateControls(true);
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            Host.Reset();

            MyGuiControlButton closeButton = Host.CreateButton("Close", CloseButtonTooltip, clicked: (b) =>
            {
                CloseScreen();
            });
            Host.StackRight(closeButton);

            ControlGetter?.Invoke(Host);
        }



        // TODO: allow RMB to free move and all that stuff

        //protected override void Canceling()
        //{
        //    base.Canceling();
        //}

        //public override bool CloseScreen(bool isUnloading = false)
        //{
        //    return base.CloseScreen(isUnloading);
        //}

        //public override bool Update(bool hasFocus)
        //{
        //    if(hasFocus)
        //    {
        //    }

        //    return base.Update(hasFocus);
        //}
    }
}