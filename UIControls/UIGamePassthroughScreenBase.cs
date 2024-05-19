using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Input;
using VRageMath;

namespace Digi.ParticleEditor.UIControls
{
    /// <summary>
    /// A screen base that has an optional <see cref="ComputeGameControlPassThrough(RectangleF?, RectangleF?, bool)"/> which can be used to allow game control by holding RMB while not over some GUI.
    /// </summary>
    public class UIGamePassthroughScreenBase : MyGuiScreenBase
    {
        const MyMouseButtonsEnum ControlGameInput = MyMouseButtonsEnum.Right;
        bool HandlingOtherInputs = false;
        Vector2 RememberMouse = Vector2.Zero;

        public bool GamePassThrough { get; private set; }

        public override string GetFriendlyName() => GetType().Name;

        //protected UIGamePassthroughScreenBase(Vector4? backgroundColor = null, bool isTopMostScreen = false)
        //    : this(new Vector2(MyGuiManager.GetMaxMouseCoord().X - 0.16f, 0.5f), new Vector2(0.32f, 1f), backgroundColor ?? (0.85f * Color.Black.ToVector4()), isTopMostScreen)
        //{
        //    m_closeOnEsc = true;
        //    m_drawEvenWithoutFocus = true;
        //    m_isTopMostScreen = false;
        //    base.CanHaveFocus = false;
        //    m_isTopScreen = true;
        //}

        protected UIGamePassthroughScreenBase(Vector2 position, Vector2? size, Vector4? backgroundColor, bool isTopMostScreen)
            : base(position, backgroundColor, size, isTopMostScreen)
        {
        }

        /// <summary>
        /// Returns true if game control is given.
        /// </summary>
        protected bool ComputeGameControlPassThrough(bool receivedFocusInThisUpdate, RectangleF? guiArea1 = null, RectangleF? guiArea2 = null, RectangleF? guiArea3 = null)
        {
            if(HandlingOtherInputs) // prevent infinite loops
                return (GamePassThrough = true);

            if(!DrawMouseCursor)
            {
                if(!MyInput.Static.IsMousePressed(ControlGameInput))
                {
                    DrawMouseCursor = true;

                    // TODO: toggleable mouse pos saving?
                    MyInput.Static.SetMousePosition((int)RememberMouse.X, (int)RememberMouse.Y);
                }
            }

            bool newPressed = MyInput.Static.IsNewMousePressed(ControlGameInput);

            if(DrawMouseCursor)
            {
                Vector2 mousePos = MyInput.Static.GetMousePosition(); // in screen pixels

                if(newPressed)
                {
                    Vector2 mousePosGUI = MyGuiManager.MouseCursorPosition;

                    //if(guiArea1 != null)
                    //{
                    //    MyGuiManager.DrawRectangle(guiArea1.Value.Position, guiArea1.Value.Size, new Color(255, 0, 255) * 0.5f);
                    //    MyGuiManager.DrawRectangle(mousePosGUI, MyGuiManager.GetNormalizedSizeFromScreenSize(new Vector2(4, 4)), Color.Lime);
                    //}

                    if((guiArea1 == null || !guiArea1.Value.Contains(mousePosGUI))
                    && (guiArea2 == null || !guiArea2.Value.Contains(mousePosGUI))
                    && (guiArea3 == null || !guiArea3.Value.Contains(mousePosGUI)))
                    {
                        RememberMouse = mousePos;
                        DrawMouseCursor = false;
                    }
                }
            }

            if(!DrawMouseCursor)
            {
                if(newPressed) // ignore first frame of press to avoid triggering stuff in world like when aiming at conveyor ports.
                    return (GamePassThrough = true);

                try
                {
                    HandlingOtherInputs = true;

                    // allow game control
                    foreach(MyGuiScreenBase screen in MyScreenManager.Screens)
                    {
                        screen.HandleInput(receivedFocusInThisUpdate);
                    }
                }
                finally
                {
                    HandlingOtherInputs = false; // ensure this gets false
                }

                return (GamePassThrough = true);
            }

            return (GamePassThrough = false);
        }
    }
}
