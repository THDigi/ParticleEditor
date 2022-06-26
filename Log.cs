using System;
using Sandbox.Game.Gui;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Digi.ParticleEditor
{
    public static class Log
    {
        public static string Name = "(UnknownPlugin)";

        public static MyHudNotification ErrorNotify;

        public static void Error(Exception e)
        {
            MyLog.Default.WriteLine($"{Name} ERROR: {e}");
            Notify($"{Name} ERROR: {e.Message}");
        }

        public static void Error(string error)
        {
            MyLog.Default.WriteLine($"{Name} ERROR: {error}");
            Notify($"{Name} ERROR: {error}");
        }

        static void Notify(string msg)
        {
            if(EditorUI.Instance?.Editor?.ShowEditor ?? false)
            {
                Notifications.Show(msg, 5, Color.Red);
            }
            else
            {
                if(MyHud.Notifications == null)
                    return;

                if(ErrorNotify == null)
                    ErrorNotify = new MyHudNotification(MyCommonTexts.CustomText, 5000, MyFontEnum.Red);

                MyHud.Notifications.Remove(ErrorNotify);
                ErrorNotify.SetTextFormatArguments(msg);
                MyHud.Notifications.Add(ErrorNotify);
            }
        }
    }
}
