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

        static readonly Vector2 ScreenPosition = new Vector2(0.5f, 0.95f);
        static readonly Vector2 ScreenSize = new Vector2(0.5f, 0.2f);

        public override string GetFriendlyName() => nameof(UICustomizablePopup);

        public UICustomizablePopup(string closeButtonTooltip = null)
            : base(ScreenPosition, size: ScreenSize, isTopMostScreen: false)
        {
            CloseButtonTooltip = closeButtonTooltip;
            Align = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM;
            Host = new VerticalControlsHost(this, new Vector2(ScreenSize.X / -2f, -ScreenSize.Y), ScreenSize, drawBackground: true);

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