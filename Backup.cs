using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.World;
using Sandbox.Graphics;
using Sandbox.ModAPI;
using VRage.FileSystem;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.ParticleEditor
{
    public class Backup : EditorComponentBase
    {
        public bool Enabled = true;

        bool _shouldBackup;
        public bool ShouldBackup
        {
            get => _shouldBackup;
            set
            {
                _shouldBackup = value;

                //if(value && ShouldBackupAt <= 0)
                //    ShouldBackupAt = Time + 3;
            }
        }

        double ShouldBackupAt = -1;

        bool ShowBackupIcon = false;
        Task BackupTask;
        double BackupFinsihedAt;

        const double BackupIconFade = 1;

        public const int BackupEveryTicks = 60 * 10;
        public static string BackupPath = Path.Combine(MyFileSystem.UserDataPath, @"ParticleEditor\Backups");
        public const string CrashedTokenFile = "LastWasCrash.token";

        ParticleHandler SelectedParticle => EditorUI.SelectedParticle;
        double Time => MySession.Static.ElapsedGameTime.TotalSeconds;

        bool CheckedAskFile = false;

        public Backup(Editor editor) : base(editor)
        {
            Editor.EditorUI.SelectedParticle.EditsMade += SelectedParticle_ChangesMade;

            Editor.EditorVisibleChanged += EditorVisibleChanged;

            InsertUnhandledExceptionHandler();
        }

        public override void Dispose()
        {
            AppDomain.CurrentDomain.UnhandledException -= UnhandledException;
        }

        void EditorVisibleChanged(bool visible)
        {
            if(visible)
            {
                CheckCrashRestoreBackup();
            }
            else
            {
                if(ShouldBackup)
                    BackupCurrentParticle();
            }
        }

        void CheckCrashRestoreBackup()
        {
            if(CheckedAskFile)
                return;

            CheckedAskFile = true;

            string askFile = Path.Combine(BackupPath, CrashedTokenFile);
            if(File.Exists(askFile))
            {
                File.Delete(askFile);

                EditorUI.PopupConfirmation("Game crashed last time, want to load a backup?", () =>
                {
                    Editor.EditorUI.LoadParticleDialog(BackupPath);
                });
            }
        }

        public override void Update()
        {
            if(MySession.Static.GameplayFrameCounter % BackupEveryTicks == 0)
            {
                // prevent backing up right after a change in case it crashes
                if(ShouldBackupAt <= 0 || Time >= ShouldBackupAt)
                    BackupCurrentParticle();
            }
        }

        void SelectedParticle_ChangesMade()
        {
            ShouldBackup = true;

            if(ShouldBackupAt <= 0)
                ShouldBackupAt = Time + 3;
        }

        public void BackupCurrentParticle()
        {
            try
            {
                ShouldBackup = false;

                if(!Enabled || SelectedParticle.Name == null || !SelectedParticle.HasChanges)
                    return;

                if(BackupTask == null || BackupTask.IsCompleted)
                {
                    string name = SelectedParticle.Name;
                    MyObjectBuilder_ParticleEffect particleOB = MyParticleEffectDataSerializer.SerializeToObjectBuilder(SelectedParticle.Data);

                    BackupTask = Task.Run(() => BackupParticle(name, particleOB));

                    ShowBackupIcon = true;
                }

                if(ShowBackupIcon || Time <= (BackupFinsihedAt + BackupIconFade))
                {
                    // TODO: needs minimum display time to not be just an unreadable flicker

                    //MyGuiManager.DrawSprite("", new Vector2(0.1f, 0.1f), Color.White, false, false);

                    Color color = Color.White;

                    string text = "Backing up...";
                    if(!ShowBackupIcon)
                        text = "Backed up!";

                    if(Time >= BackupFinsihedAt)
                    {
                        color = Color.Lime * (float)MathHelper.Clamp((Time - BackupFinsihedAt) / BackupIconFade, 0, 1);
                    }

                    MyGuiManager.DrawString("Debug", text, new Vector2(0.4f, 0.01f), 0.5f, color, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP, MyVideoSettingsManager.IsTripleHead());


                    MyRenderProxy.DebugDrawText2D(new Vector2(10, 10), "Backing up particle...", color, 0.5f, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void BackupParticle(string fileNameNoExtension, MyObjectBuilder_ParticleEffect particleOB)
        {
            try
            {
                string fileName = fileNameNoExtension;

                foreach(char c in Path.GetInvalidFileNameChars())
                {
                    fileName = fileName.Replace(c, '_');
                }

                fileName += ".sbc";

                Directory.CreateDirectory(BackupPath);

                string filePath = Path.Combine(BackupPath, fileName);
                string oldFile = Path.Combine(BackupPath, fileName + " (previous).sbc");

                if(File.Exists(filePath))
                {
                    if(File.Exists(oldFile))
                    {
                        File.Delete(oldFile);
                    }

                    File.Move(filePath, oldFile);
                }

                MyObjectBuilder_Definitions definitionsOB = new MyObjectBuilder_Definitions();
                definitionsOB.ParticleEffects = new MyObjectBuilder_ParticleEffect[] { particleOB };

                if(!EditorUI.SerializeToXML(filePath, definitionsOB))
                {
                    Notifications.Show($"Failed to backup particle {particleOB.Id.SubtypeName} ! Check SE log.", 5, Color.Red);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            BackupFinsihedAt = Time;
            ShowBackupIcon = false;
        }

        void InsertUnhandledExceptionHandler()
        {
            MethodInfo gameExceptionHandlerMethod = typeof(MyInitializer).GetMethod("UnhandledExceptionHandler", BindingFlags.NonPublic | BindingFlags.Static);
            if(gameExceptionHandlerMethod == null)
            {
                Log.Error("Couldn't find 'MyInitializer.UnhandledExceptionHandler()', backup-on-crash will not work!");
            }
            else
            {
                UnhandledExceptionEventHandler handlerDelegate = gameExceptionHandlerMethod.CreateDelegate<UnhandledExceptionEventHandler>(null);

                // butt-in to be first to get unhandled exceptions because the game's handler kills the process and doesn't allow anyone else to get the event, rood!
                AppDomain.CurrentDomain.UnhandledException -= handlerDelegate;
                AppDomain.CurrentDomain.UnhandledException += UnhandledException;
                AppDomain.CurrentDomain.UnhandledException += handlerDelegate;
            }
        }

        void UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string stack = e.ExceptionObject.ToString();

            MyParticleEffectData data = EditorUI?.SelectedParticle?.Data;
            if(data == null)
                return;

            // token to ask to restore backup on next load
            File.WriteAllText(Path.Combine(BackupPath, CrashedTokenFile), string.Empty);

            if(stack.Contains("Exception in render!")
            || stack.Contains("MyParticleGPUGenerationData"))
            {
                Log.Error("Crashed in render, if you remember what you changed on the particle right before this, report it on ParticleEditor's page!");
                return; // don't backup if particle caused crash =)
            }

            MyObjectBuilder_ParticleEffect particleOB = MyParticleEffectDataSerializer.SerializeToObjectBuilder(data);
            BackupParticle(data.Name + " (on crash)", particleOB);
        }
    }
}
