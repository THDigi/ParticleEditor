using System;
using System.Reflection;
using Digi.ParticleEditor.GameData;
using Digi.ParticleEditor.UIControls;
using Sandbox;
using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
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

        bool Restore_PostProcessing;
        MyRenderQualityEnum? Restore_ShaderQuality;

        public EditorDebug(EditorUI editorUI)
        {
            EditorUI = editorUI;
            FindParticlesMultiplierProperty();

            MySession.AfterLoading += SessionAfterLoading;
            MySession.OnUnloaded += SessionUnloading;

            if(MySession.Static != null)
            {
                SessionAfterLoading();
            }
        }

        public void Dispose()
        {
            MySession.AfterLoading -= SessionAfterLoading;
            MySession.OnUnloaded -= SessionUnloading;

            RestoreConfigSettings();
        }

        void SessionAfterLoading()
        {
            GetConfigSettings();
        }

        void SessionUnloading()
        {
            RestoreConfigSettings();
        }

        void GetConfigSettings()
        {
            Restore_PostProcessing = MySandboxGame.Config.PostProcessingEnabled;
            Restore_ShaderQuality = MySandboxGame.Config.ShaderQuality;

            MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
            graphicsSettings.PostProcessingEnabled = Restore_PostProcessing;
            MyVideoSettingsManager.Apply(graphicsSettings);
        }

        void RestoreConfigSettings()
        {
            MySandboxGame.Config.PostProcessingEnabled = Restore_PostProcessing;
            MySandboxGame.Config.ShaderQuality = Restore_ShaderQuality;
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

        public void RecreateControls(VerticalControlsHost host)
        {
            Host = host;

            //Host.PositionAndFillWidth(Host.CreateLabel("Debug"));

            MyGuiControlLabel shaderQualityLabel = Host.CreateLabel("Temporarily change particle quality:");
            shaderQualityLabel.SetToolTip("To preview how the particle looks at various graphics settings.\nThis setting is affected by Shader Quality in the game's graphics UI.");
            Host.PositionAndFillWidth(shaderQualityLabel);

            string tooltipExtra = "This does not save to config.";

            // NOTE: ParticleQuality does not get saved to config, it gets set from VoxelShaderQuality instead.

            MyGuiControlButton buttonLow = Host.CreateButton("Low", tooltipExtra, clicked: (button) =>
            {
                MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
                graphicsSettings.PerformanceSettings.RenderSettings.ParticleQuality = MyRenderQualityEnum.LOW;
                MyVideoSettingsManager.Apply(graphicsSettings);
            });

            MyGuiControlButton buttonMed = Host.CreateButton("Medium", tooltipExtra, clicked: (button) =>
            {
                MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
                graphicsSettings.PerformanceSettings.RenderSettings.ParticleQuality = MyRenderQualityEnum.NORMAL;
                MyVideoSettingsManager.Apply(graphicsSettings);
            });

            MyGuiControlButton buttonHigh = Host.CreateButton("High", tooltipExtra, clicked: (button) =>
            {
                MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
                graphicsSettings.PerformanceSettings.RenderSettings.ParticleQuality = MyRenderQualityEnum.HIGH;
                MyVideoSettingsManager.Apply(graphicsSettings);
            });

            MyGuiControlButton buttonExtreme = Host.CreateButton("Extreme", tooltipExtra, clicked: (button) =>
            {
                MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
                graphicsSettings.PerformanceSettings.RenderSettings.ParticleQuality = MyRenderQualityEnum.EXTREME;
                MyVideoSettingsManager.Apply(graphicsSettings);
            });

            Host.PositionControls(buttonLow, buttonMed, buttonHigh, buttonExtreme);

            ParticleMulLabel = Host.CreateLabel("Particle emitter multiplier: (unknown)");
            ParticleMulLabel.SetToolTip("Affects how many particles are spawned by every particle effect's emitter." +
                                        "\nAffected by graphics options' Shader Quality (which you can temporarily change with the buttons above)." +
                                        "\n\nThe unknown value means this plugin was not able to read it because SE code got changed." +
                                        "\nYou can still try the buttons above and visually inspect if particles are being reduced.");
            Host.PositionAndFillWidth(ParticleMulLabel);



            Host.InsertSeparator();

            Host.InsertCheckbox("Render post processing", "Temporarily toggle post processing, gets reset to config value on world unload.",
                MyVideoSettingsManager.CurrentGraphicsSettings.PostProcessingEnabled, (value) =>
                {
                    MyGraphicsSettings graphicsSettings = MyVideoSettingsManager.CurrentGraphicsSettings;
                    graphicsSettings.PostProcessingEnabled = value;
                    MyVideoSettingsManager.Apply(graphicsSettings);
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
