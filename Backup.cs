using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Graphics;
using VRage.FileSystem;
using VRage.Game;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.ParticleEditor
{
    public class Backup : EditorComponentBase
    {
        public bool Enable = true;

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

        DateTime? ShouldBackupAt;

        bool ShowMessage = false;
        Task BackupTask;
        DateTime? MessageHideAt;
        DateTime Time => DateTime.Now;

        public static string BackupPath = Path.Combine(MyFileSystem.UserDataPath, @"ParticleEditor\Backups");
        public const string CrashedTokenFile = "LastWasCrash.token";

        ParticleHandler SelectedParticle => EditorUI.SelectedParticle;

        bool CheckedAskFile = false;

        public Backup(Editor editor) : base(editor)
        {
            AlwaysUpdate = true;

            Editor.EditorUI.SelectedParticle.EditsMade += SelectedParticle_ChangesMade;

            Editor.EditorVisibleChanged += EditorVisibleChanged;

            InsertUnhandledExceptionHandler();
        }

        public override void Dispose()
        {
            AppDomain.CurrentDomain.UnhandledException -= UnhandledException;
        }

        public override void Update()
        {
            if(Enable && ShouldBackupAt != null && Time >= ShouldBackupAt)
            {
                ShouldBackupAt = null;
                BackupCurrentParticle();
            }

            if(ShowMessage)
            {
                if(Editor.ShowEditor)
                {
                    bool inProgress = MessageHideAt == null;
                    string text = inProgress ? "Backing up..." : "Backed up!";
                    Color color = inProgress ? Color.Gray : Color.White; // inProgress ? Color.Yellow : Color.Lime;

                    MyRenderProxy.DebugDrawText2D(MyGuiManager.GetHudPixelCoordFromNormalizedCoord(new Vector2(0.5f, 0.01f)), text, color, 0.5f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                }

                if(MessageHideAt != null && Time >= MessageHideAt.Value)
                {
                    MessageHideAt = null;
                    ShowMessage = false;
                }
            }
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

        void SelectedParticle_ChangesMade()
        {
            ShouldBackup = true;

            // back up a bit later in case the change causes a crash
            if(ShouldBackupAt == null)
                ShouldBackupAt = Time + TimeSpan.FromSeconds(5);
        }

        public void BackupCurrentParticle()
        {
            try
            {
                ShouldBackup = false;

                if(SelectedParticle.Name == null || !SelectedParticle.HasChanges)
                    return;

                if(BackupTask == null || BackupTask.IsCompleted)
                {
                    ShowMessage = true;

                    string name = SelectedParticle.Name;
                    MyObjectBuilder_ParticleEffect particleOB = MyParticleEffectDataSerializer.SerializeToObjectBuilder(SelectedParticle.Data);

                    BackupTask = Task.Run(() => BackupParticle(name, particleOB));
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
                    Notifications.Show($"Failed to backup particle '{particleOB.Id.SubtypeName}'! Check SE log.", 5, Color.Red);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            MessageHideAt = Time + TimeSpan.FromSeconds(1);
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
