using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Digi.ParticleEditor.GameData;
using Digi.ParticleEditor.UIControls;
using Sandbox;
using Sandbox.Game.Gui;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender.Animations;

namespace Digi.ParticleEditor
{
    public partial class EditorUI
    {
        public static string[] Vector2AxisNames = { "X", "Y" };

        public static string[] Vector3AxisNames = { "X", "Y", "Z" };
        public static string[] Vector3AxisTooltips =
        {
            "X axis: positive values go towards right, negative towards left.",
            "Y axis: positive values go upwards, negative downwards.",
            "Z axis: positive values go towards back, negative values go forward.",
        };

        public static string[] Vector4AxisNames = { "X", "Y", "Z", "W" };
        //public static string[] Vector4AxisTooltips =
        //{
        //    Vector3AxisTooltips[0],
        //    Vector3AxisTooltips[1],
        //    Vector3AxisTooltips[2],
        //    "W axis: unknown",
        //};

        public static string[] Vector4ColorNames = { "R", "G", "B", "A" };

        public static double RoundTo5(double value)
        {
            return Math.Round(value / 5.0) * 5;
        }

        public static Vector4 ColorForPreview(Vector4 color)
        {
            // "normalize" color to 0 to 1
            float max = Math.Max(Math.Max(color.X, color.Y), Math.Max(color.Z, color.W));
            if(max > 1)
            {
                color.X /= max;
                color.Y /= max;
                color.Z /= max;
            }

            color.W = Math.Max(color.W, 0.05f);
            return color;
        }

        public static string CleanupXML(string xml)
        {
            // remove some pointless text complexity
            xml = xml.Replace("\r", "");
            xml = xml.Replace("<?xml version=\"1.0\" encoding=\"utf-16\"?>\n", "");
            xml = xml.Replace("xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"", "");
            return xml;
        }

        public static bool SerializeToXML(string filePath, MyObjectBuilder_Base ob)
        {
            try
            {
                //string xml = MyAPIGateway.Utilities.SerializeToXML(particleOB);
                //xml = xml.Replace(" encoding=\"utf-16\"", "");
                //File.WriteAllText(filePath, xml);

                return MyObjectBuilderSerializer.SerializeXML(filePath, false, ob);
            }
            catch(Exception e)
            {
                Log.Error(e);
                return false;
            }
        }

        public static bool CanReadInputs()
        {
            MyGuiScreenBase focusScreen = MyScreenManager.GetScreenWithFocus();
            if(focusScreen == null)
                return false;

            if(focusScreen is IScreenAllowHotkeys || focusScreen is MyGuiScreenGamePlay)
                return true;

            return false;

            //if(focusScreen is ValueGetScreenWithCaption
            //|| focusScreen is MyGuiScreenDialogAmount
            //|| focusScreen is MyGuiScreenDialogText)
            //    return false;

            //return true;
        }

        public static Color TransitionAlpha(Vector4 color, float transition)
        {
            Vector4 vector = color;
            vector.W *= transition;
            return new Color(vector);
        }

        public static MyGuiScreenMessageBox PopupInfo(string title, string text, MyMessageBoxStyleEnum type = MyMessageBoxStyleEnum.Info)
        {
            MyGuiScreenMessageBox box = MyGuiSandbox.CreateMessageBox(type, MyMessageBoxButtonsType.OK, new StringBuilder(text), new StringBuilder(title));
            MyGuiSandbox.AddScreen(box);
            return box;
        }

        public static MyGuiScreenMessageBox PopupConfirmation(string text, Action confirmed, Action declined = null, bool focusNoButton = false)
        {
            MyGuiScreenMessageBox box = MyGuiSandbox.CreateMessageBox(MyMessageBoxStyleEnum.Info, MyMessageBoxButtonsType.YES_NO, new StringBuilder(text), new StringBuilder("Confirmation"),
                                                                      yesButtonText: MyStringId.GetOrCompute("Yes"), noButtonText: MyStringId.GetOrCompute("No"),
                                                                      callback: (result) =>
                                                                      {
                                                                          if(result == MyGuiScreenMessageBox.ResultEnum.YES)
                                                                              confirmed?.Invoke();
                                                                          else
                                                                              declined?.Invoke();
                                                                      }, focusedResult: focusNoButton ? MyGuiScreenMessageBox.ResultEnum.NO : MyGuiScreenMessageBox.ResultEnum.YES);
            MyGuiSandbox.AddScreen(box);
            return box;
        }

        public static MyGuiScreenMessageBox PopupInfoAction(string title, string text, string yesButton = "Yes", string noButton = "No", Action<MyGuiScreenMessageBox.ResultEnum> callback = null, MyMessageBoxStyleEnum type = MyMessageBoxStyleEnum.Info)
        {
            MyGuiScreenMessageBox box = MyGuiSandbox.CreateMessageBox(type, MyMessageBoxButtonsType.YES_NO, new StringBuilder(text), new StringBuilder(title),
                                                                      yesButtonText: MyStringId.GetOrCompute(yesButton), noButtonText: MyStringId.GetOrCompute(noButton),
                                                                      callback: callback);
            MyGuiSandbox.AddScreen(box);
            return box;
        }

        public static string GetDescriptionAttrib(Type type, string memberName)
        {
            MemberInfo[] members = type.GetMember(memberName);
            if(members == null || members.Length <= 0)
                return null;

            object[] attribs = members[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            if(attribs == null || attribs.Length <= 0)
                return null;

            DescriptionAttribute attrib = attribs[0] as DescriptionAttribute;
            if(attrib == null)
                return null;

            return attrib.Description;
        }

        public static void FileDialog<TDialog>(string title, string directory, string filter, Action<string> callback) where TDialog : FileDialog, new()
        {
            Thread thread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    using(TDialog dialog = new TDialog())
                    {
                        if(directory != null && Directory.Exists(directory))
                            dialog.InitialDirectory = directory;

                        dialog.Title = title;
                        dialog.Filter = filter;
                        dialog.RestoreDirectory = true;
                        dialog.AddExtension = true;
                        dialog.AutoUpgradeEnabled = true;

                        Form GetMainForm()
                        {
                            if(Application.OpenForms.Count > 0)
                                return Application.OpenForms[0];
                            else
                                return new Form { TopMost = true };
                        }

                        if(dialog.ShowDialog(GetMainForm()) == DialogResult.OK)
                        {
                            MySandboxGame.Static.Invoke(() => callback(dialog.FileName), "ParticleEditorPlugin-FileDialog");
                        }
                    }
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        public static void FinalizeScrollable(MyGuiControlScrollablePanel panel, MyGuiControlParent content, VerticalControlsHost host, Func<bool> grayOutCondition)
        {
            bool grayedControls = grayOutCondition?.Invoke() ?? false;

            if(grayedControls)
                panel.SetToolTip("This is turned off, double click it in the list to enable it.");
            else
                panel.SetToolTip("");

            content.Size = new Vector2(content.Size.X, host.CurrentPosition.Y);

            // offset controls up by half content's Y size
            foreach(MyGuiControlBase control in content.Controls)
            {
                control.PositionY -= host.CurrentPosition.Y / 2f;

                if(grayedControls)
                {
                    control.Enabled = false;
                }
            }

            panel.RefreshInternals();
        }

        public static object GetGeneralPropertyOriginalOrDefault(PropId propId)
        {
            if(Instance.SelectedParticle == null)
                return null;

            if(propId.Type == PropType.General)
            {
                MyObjectBuilder_ParticleEffect originalData = EditorUI.Instance.SelectedParticle?.OriginalData;
                MyObjectBuilder_ParticleEffect defaultData = MyParticleEffectDataSerializer.SerializeToObjectBuilder(EditorUI.DefaultData);

                return GetFromOB(originalData) ?? GetFromOB(defaultData);
            }
            else
            {
                Log.Error($"Unknown PropType='{propId.Type}' for property '{propId.Name}'");
            }

            return null;

            object GetFromOB(MyObjectBuilder_ParticleEffect ob)
            {
                if(ob != null)
                {
                    FieldInfo field = ob.GetType().GetField(propId.Name, BindingFlags.GetField | BindingFlags.Instance | BindingFlags.Public);

                    if(field != null)
                        return field.GetValue(ob);
                    else
                        Log.Error($"Cannot find field with name '{propId.Name}' on type '{ob.GetType().Name}'");
                }

                return null;
            }
        }

        public static GenerationProperty GetDefaultPropData(object propHost, IMyConstProperty prop)
        {
            if(propHost == null) throw new ArgumentNullException("propHost");
            if(prop == null) throw new ArgumentNullException("prop");

            if(propHost is MyParticleGPUGenerationData emitter)
            {
                foreach(IMyConstProperty p in DefaultEmitter.GetProperties())
                {
                    if(p.Name == prop.Name)
                        return p.SerializeToObjectBuilder();
                }
            }
            else if(propHost is MyParticleLightData light)
            {
                foreach(IMyConstProperty p in DefaultLight.GetProperties())
                {
                    if(p.Name == prop.Name)
                        return p.SerializeToObjectBuilder();
                }
            }
            else
            {
                throw new Exception($"Unknown propHost type: {propHost.GetType().Name}");
            }

            throw new Exception($"Couldn't find property '{prop.Name}' for '{propHost.GetType().Name}'");
        }

        public static bool GetOriginalPropData(object propHost, string propName, out GenerationProperty propOB)
        {
            if(propHost == null) throw new ArgumentNullException("propHost");
            if(propName == null) throw new ArgumentNullException("propName");

            MyObjectBuilder_ParticleEffect originalData = EditorUI.Instance.SelectedParticle?.OriginalData;

            propOB = default;

            if(originalData == null)
                return false;

            if(propHost is MyParticleGPUGenerationData emitter)
            {
                ParticleGeneration emitterOB = originalData.ParticleGenerations.Find(e => e.Name == emitter.Name);
                if(emitterOB != null)
                {
                    propOB = emitterOB.Properties.Find(p => p.Name == propName);
                    return true;
                }
            }
            else if(propHost is MyParticleLightData light)
            {
                ParticleLight lightOB = originalData.ParticleLights.Find(e => e.Name == light.Name);
                if(lightOB != null)
                {
                    propOB = lightOB.Properties.Find(p => p.Name == propName);
                    return true;
                }
            }
            else
            {
                throw new Exception($"Unknown propHost type: {propHost.GetType().Name}");
            }

            return false;
        }
    }
}
