using System;
using System.Reflection;
using Digi.ParticleEditor.GameData;
using Digi.ParticleEditor.UIControls;
using Sandbox;
using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.ParticleEditor
{
    public class EditorDebug
    {
        readonly EditorUI EditorUI;
        VerticalControlsHost Host;

        ParticleHandler SelectedParticle => EditorUI.SelectedParticle;

        MyGuiControlLabel ParticleMulLabel;

        public float? ParticleEmitterMultiplier => _particleEmitterMultiplier?.Invoke();
        Func<float> _particleEmitterMultiplier;

        public EditorDebug(EditorUI editorUI)
        {
            EditorUI = editorUI;
            FindParticlesMultiplierProperty();

            MySession.OnUnloaded += SessionUnloading;
        }

        public void Dispose()
        {
            MySession.OnUnloaded -= SessionUnloading;
        }

        void SessionUnloading()
        {
            MyRenderQualityEnum? quality = MySandboxGame.Config?.ShaderQuality;
            if(quality != null && MyVideoSettingsManager.CurrentGraphicsSettings.PerformanceSettings.RenderSettings.ParticleQuality != quality.Value)
            {
                SetParticleQuality(quality.Value);
                MyLog.Default.WriteLine($"ParticleEditor: Set particle quality back to {quality.Value}");
            }
        }

        void FindParticlesMultiplierProperty()
        {
            Type type = typeof(MyDX11Render).Assembly.GetType("VRageRender.MyGPUEmitters");
            PropertyInfo prop = type?.GetProperty("ParticleCountMultiplier", BindingFlags.Static | BindingFlags.Public);
            if(prop != null)
            {
                MethodInfo[] methods = prop.GetAccessors(false);
                _particleEmitterMultiplier = methods[0].CreateDelegate<Func<float>>(null);
            }
        }

        void SetParticleQuality(MyRenderQualityEnum quality)
        {
            MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
            graphicsSettings.PerformanceSettings.RenderSettings.ParticleQuality = quality;
            MyVideoSettingsManager.Apply(graphicsSettings);

            // shader qualtiy in config is being set by voxel shading quality so this is no concern of being changed permanently.
            // it does however persist for the duration of the game so needs to be reset on world unload.
            // it also gets reset if graphics options menu is opened and OK is pressed.
        }

        public void RecreateControls(VerticalControlsHost host)
        {
            Host = host;

            //Host.PositionAndFillWidth(Host.CreateLabel("Debug"));

            MyGuiControlLabel shaderQualityLabel = Host.CreateLabel("Temporarily change global particle quality:");
            shaderQualityLabel.SetToolTip("This particle quality setting is hidden and affected by game's Shader Quality." +
                "\nThe below buttons' changes will only last until world unload and do not get saved to game config." +
                "\nAlso gets reset if you go to graphics options UI and click OK.");
            Host.PositionAndFillWidth(shaderQualityLabel);

            string tooltipExtra = null;

            MyGuiControlButton buttonLow = Host.CreateButton("Low", tooltipExtra, clicked: (button) =>
            {
                SetParticleQuality(MyRenderQualityEnum.LOW);
            });

            MyGuiControlButton buttonMed = Host.CreateButton("Medium", tooltipExtra, clicked: (button) =>
            {
                SetParticleQuality(MyRenderQualityEnum.NORMAL);
            });

            MyGuiControlButton buttonHigh = Host.CreateButton("High", tooltipExtra, clicked: (button) =>
            {
                SetParticleQuality(MyRenderQualityEnum.HIGH);
            });

            MyGuiControlButton buttonExtreme = Host.CreateButton("Extreme", tooltipExtra, clicked: (button) =>
            {
                SetParticleQuality(MyRenderQualityEnum.EXTREME);
            });

            Host.PositionControls(buttonLow, buttonMed, buttonHigh, buttonExtreme);

            ParticleMulLabel = Host.CreateLabel("Particle emitter multiplier: (unknown)");
            ParticleMulLabel.SetToolTip("This is what ultimately affects all particles' spawn-rate." +
                                        "\nThis gets set by Particle Quality (buttons above) which is in turn set by game's Shader Quality in graphics options UI." +
                                        "\n\nIf it shows up as '(unknown)' it means this plugin was not able to read it because SE code got changed." +
                                        "\nYou can still try the buttons above and visually inspect if particles are being reduced.");
            Host.PositionAndFillWidth(ParticleMulLabel);



            Host.InsertSeparator();

            Host.InsertCheckbox("Post-processing", "This is a shortcut for the post processing toggle from graphics options UI.\nNOTE: gets saved to game config, remember to undo it.",
                MyVideoSettingsManager.CurrentGraphicsSettings.PostProcessingEnabled, (value) =>
                {
                    MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
                    graphicsSettings.PostProcessingEnabled = value;
                    MyVideoSettingsManager.Apply(graphicsSettings);

                    // the above setting gets read and written to config at unknown times... might as well make it official:

                    MySandboxGame.Config.PostProcessingEnabled = value;
                    MySandboxGame.Config.Save();
                });



            Host.InsertSeparator();

            {
                MyGuiControlLabel label = Host.CreateLabel("Simulate user multipliers");
                label.SetToolTip("These do not get saved in the particle definition." +
                                 "\nVarious things that spawn particles can choose to use multipliers, this is where you can simulate them.");
                Host.PositionAndFillWidth(label);
            }

            void UserMultiplierSlider(string propName, Func<float> getter, Action<float> setter)
            {
                PropId propId = new PropId(PropType.UserMultiplier, propName);
                PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propId);

                ValueInfo<float> valueRange = propInfo.ValueRangeNum;

                float defaultValue = valueRange.Default ?? 1f;
                float min = (valueRange.Min == 0 ? 0 : float.MinValue);
                float max = float.MaxValue;
                if(valueRange.LimitNumberBox)
                {
                    min = valueRange.Min;
                    max = valueRange.Max;
                }

                MyGuiControlLabel label = Host.CreateLabel(propInfo.GetName());
                UINumberBox box = Host.CreateNumberBox(propInfo.GetTooltip(),
                    getter.Invoke(), defaultValue: defaultValue, min: min, max: max, inputRound: 6, dragRound: 1,
                    changed: (value) =>
                    {
                        setter.Invoke(value);
                        SelectedParticle.UserMultipliers.ApplyToParticle();
                    });
                Host.PositionControlsNoSize(box, label);
                Host.MoveY(0.01f);
            }

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.UserScale),
                () => SelectedParticle.UserMultipliers.Scale,
                (value) => SelectedParticle.UserMultipliers.Scale = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.UserLifeMultiplier),
                () => SelectedParticle.UserMultipliers.Life,
                (value) => SelectedParticle.UserMultipliers.Life = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.UserFadeMultiplier),
                () => SelectedParticle.UserMultipliers.Fade,
                (value) => SelectedParticle.UserMultipliers.Fade = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.UserBirthMultiplier),
                () => SelectedParticle.UserMultipliers.Birth,
                (value) => SelectedParticle.UserMultipliers.Birth = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.UserRadiusMultiplier),
                () => SelectedParticle.UserMultipliers.Radius,
                (value) => SelectedParticle.UserMultipliers.Radius = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.UserVelocityMultiplier),
                () => SelectedParticle.UserMultipliers.Velocity,
                (value) => SelectedParticle.UserMultipliers.Velocity = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.SoftParticleDistanceScaleMultiplier),
                () => SelectedParticle.UserMultipliers.SoftParticle,
                (value) => SelectedParticle.UserMultipliers.SoftParticle = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.CameraSoftRadiusMultiplier),
                () => SelectedParticle.UserMultipliers.CameraSoftRadius,
                (value) => SelectedParticle.UserMultipliers.CameraSoftRadius = value);

            UserMultiplierSlider(nameof(SelectedParticle.SpawnedEffect.UserColorIntensityMultiplier),
                () => SelectedParticle.UserMultipliers.ColorIntensity,
                (value) => SelectedParticle.UserMultipliers.ColorIntensity = value);

            {
                PropId propId = new PropId(PropType.UserMultiplier, nameof(SelectedParticle.SpawnedEffect.UserColorMultiplier));
                PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propId);

                string name = propInfo.GetName();
                string tooltip = propInfo.GetTooltip();

                ValueInfo<Vector4> valueRange = propInfo.ValueRangeVector4;
                Vector4 def = valueRange.Default ?? Vector4.One;
                Vector4 min = (valueRange.Min == Vector4.Zero ? Vector4.Zero : new Vector4(float.MinValue));
                Vector4 max = new Vector4(float.MaxValue);
                if(valueRange.LimitNumberBox)
                {
                    min = valueRange.Min;
                    max = valueRange.Max;
                }

                int dragRound = valueRange.Rounding;
                int inputRound = valueRange.InputRounding;

                Vector4 val = SelectedParticle.UserMultipliers.Color;

                MyGuiControlLabel titleLabel = Host.CreateLabel(name);
                titleLabel.SetToolTip(tooltip);
                Host.PositionControlsNoSize(titleLabel);

                MyGuiControlBase[] controls = new MyGuiControlBase[4 * 2];

                for(int i = 0; i < 4; i++)
                {
                    int dim = i; // required for reliable capture

                    MyGuiControlLabel dimLabel = Host.CreateLabel(EditorUI.Vector4AxisNames[dim]);

                    UINumberBox box = Host.CreateNumberBox(tooltip,
                        val.GetDim(dim), def.GetDim(dim),
                        min.GetDim(dim), max.GetDim(dim), inputRound, dragRound,
                        (value) =>
                        {
                            Vector4 vec = SelectedParticle.UserMultipliers.Color;
                            vec.SetDim(dim, value);
                            SelectedParticle.UserMultipliers.Color = vec;
                            SelectedParticle.UserMultipliers.ApplyToParticle();
                        });

                    float boxWidth = 0.085f - dimLabel.Size.X - Host.ControlSpacing;
                    box.Size = new Vector2(boxWidth, box.Size.Y);

                    controls[i * 2] = dimLabel;
                    controls[i * 2 + 1] = box;
                }

                Host.PositionControlsNoSize(controls);

                Host.MoveY(0.01f); // HACK: box is taller than its actual size
            }



            Host.InsertSeparator();

            Host.InsertCheckbox("Periodically backup",
                                $"Backs up changed particles every {(Backup.BackupEveryTicks / 60)}s to:\n'{Backup.BackupPath}\\*.sbc'.\nDoes not get saved in any config.\nAlso, regardless of this setting, if the game crashes this plugin will backup your current particle.",
                EditorUI.Editor.Backup.Enabled, (value) =>
                {
                    EditorUI.Editor.Backup.Enabled = value;
                });
        }

        public void Update()
        {
            if(MySession.Static.GameplayFrameCounter % 10 == 0)
            {
                float? mul = ParticleEmitterMultiplier;
                if(mul.HasValue)
                    ParticleMulLabel.Text = $"Particle emitter multiplier:  {mul.Value:0.#####}";
            }
        }
    }
}
