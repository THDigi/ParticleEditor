using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.World;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Collections;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.ParticleEditor
{
    public class Notifications : EditorComponentBase
    {
        struct Message
        {
            public readonly string Text;
            public readonly Vector2 TextSize;
            public readonly Color Color;
            public readonly double ExpiresAt;
            public readonly bool Fade;

            public Message(string text, Color color, double expiresAt, bool fade)
            {
                Text = text;
                TextSize = MyGuiManager.MeasureString(Font, Text, TextScale);
                Color = color;
                ExpiresAt = expiresAt;
                Fade = fade;
            }
        }

        static Notifications Instance;

        const string Font = "Debug";
        const double FadeOutTime = 0.5;
        static float TextScale => MyGuiSandbox.GetDefaultTextScaleWithLanguage() * 1.2f;

        readonly Vector2 Position = new Vector2(0.01f, 0.4f);
        readonly MyConcurrentList<Message> Messages = new MyConcurrentList<Message>();

        public Notifications(Editor editor) : base(editor)
        {
            Instance = this;

            MySession.OnUnloaded += SessionUnloaded;
        }

        public override void Dispose()
        {
            Instance = null;

            MySession.OnUnloaded -= SessionUnloaded;
        }

        void SessionUnloaded()
        {
            Messages.Clear();
        }

        public override void Update()
        {
            int totalMessages = Messages.Count;
            if(totalMessages == 0)
                return;

            Vector2 position = Position;
            bool isTrippleHead = MyVideoSettingsManager.IsTripleHead();
            float scale = TextScale;
            int limit = 5;

            double time = MySession.Static.ElapsedGameTime.TotalSeconds;

            for(int i = (totalMessages - 1); i >= 0; i--)
            {
                Message msg = Messages[i];
                Color color = msg.Color;

                if((time + FadeOutTime) >= msg.ExpiresAt)
                {
                    if(time >= msg.ExpiresAt)
                    {
                        Messages.RemoveAt(i);
                        continue;
                    }

                    if(msg.Fade)
                    {
                        color *= (float)MathHelper.Clamp((msg.ExpiresAt - time) / FadeOutTime, 0, 1);
                    }
                }

                //position.X = Position.X - msg.TextSize.X / 2f;

                //MyGuiManager.DrawString(Font, line.Text, position, scale, color, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, isTrippleHead);

                // draws always on top of HUD
                // TODO: a bit crooked...
                MyRenderProxy.DebugDrawText2D(MyGuiManager.GetHudPixelCoordFromNormalizedCoord(position), msg.Text, color, scale, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);

                position.Y += msg.TextSize.Y;

                if(--limit <= 0)
                    break;
            }
        }

        /// <summary>
        /// Thread-safe
        /// </summary>
        public static void Show(string message, double liveSeconds = 2, Color? color = null)
        {
            if(Instance == null)
                return;

            double expireAt = MySession.Static.ElapsedGameTime.TotalSeconds + liveSeconds;

            if(!color.HasValue)
                color = Color.White;

            bool fade = liveSeconds > (FadeOutTime + 0.5);

            Instance.Messages.Add(new Message(message, color.Value, expireAt, fade));
        }
    }
}
