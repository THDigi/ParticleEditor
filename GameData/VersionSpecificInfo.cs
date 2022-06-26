using System;
using System.Collections.Generic;
using System.IO;
using VRage.Game;
using VRage.Render.Particles;
using VRageMath;
using VRageRender;
using VRageRender.Animations;

namespace Digi.ParticleEditor.GameData
{
    // Last checked on SE v201
    public static class VersionSpecificInfo
    {
        public const uint NoParentId = uint.MaxValue;

        // from MyDefinitionManager.CreateTransparentMaterials()
        public const string MaterialComboBoxTooltipAddition = "\n\nNOTE: The game has some strict restrictions on what materials work for particles:" +
                                                              "\n- Material's texture file name must start with 'Atlas_', case sensitive;" +
                                                              "\n- Material's texture must be exactly 8192 x 8192 pixels;" +
                                                              "\n- Material must have TextureType set to FileTexture (or undeclared as it is default).";

        public const string ColorSlidersTooltip = "Values between 0 and 1 for normal ranges, can go past 1 to cause bloom.";

        // HACK: MyDefinitionManager.CreateTransparentMaterials() does this check before giving texture to renderer (MyGPUEmitters)
        // HACK: also game somewhere requires these textures to be exactly 8192x8192 otherwise they're pink
        public static bool IsMaterialSupported(MyTransparentMaterial mat)
        {
            if(mat.TextureType == MyTransparentMaterialTextureType.FileTexture && !string.IsNullOrEmpty(mat.Texture) && Path.GetFileNameWithoutExtension(mat.Texture).StartsWith("Atlas_"))
            {
                Vector2I size = (Vector2I)MyRenderProxy.GetTextureSize(mat.Texture);
                if(size == new Vector2I(8192, 8192))
                    return true;
            }

            return false;
        }

        public const string AnimatedProp1DHelp = "Animated properties (1D) are over the effect's total elapsed time." +
                                                 "\nMost properties can function with just one vertical key.";

        public const string AnimatedProp2DHelp = "2D animated properties are verticaly over the effect's total elapsed time and horizontally over individual particle's life." +
                                                 "\nTime on the horizontal axis is always 0 to 1 as the ratio for the particle's total life." +
                                                 "\nMost need at least 2 keys on the horizontal line to properly get data, 3 to get interpolation and 4 is pretty much maximum.";

        public static PropertyData DefaultPropInfo = new PropertyData(); // NOTE: don't edit these here, edit'em in the class declaration!

        static Dictionary<PropId, PropertyData> PropertyInfo = new Dictionary<PropId, PropertyData>()
        {
            [new PropId(MyGPUGenerationPropertiesEnum.Enabled)] = new PropertyData(), // bool

            [new PropId(MyGPUGenerationPropertiesEnum.Material)] = new PropertyData() // string/MyTransparentMaterial
            {
            },

            [new PropId(MyGPUGenerationPropertiesEnum.ArraySize)] = new PropertyData() // Vector3
            {
                NameOverride = $"Divide texture into cells ({EditorUI.DefaultEmitter.ArraySize.Name})",
                TooltipAddition = "This is normally a Vector3 but only X and Y are used.",
                ValueRangeVector3 = new ValueInfo<Vector3>(new Vector3(1), new Vector3(256), round: 0),
            },
            [new PropId(MyGPUGenerationPropertiesEnum.ArrayOffset)] = new PropertyData() // int
            {
                NameOverride = $"Start from cell ({EditorUI.DefaultEmitter.ArrayOffset.Name})",
                ValueRangeNum = new ValueInfo<float>(0, 1024, round: 0),
            },
            [new PropId(MyGPUGenerationPropertiesEnum.ArrayModulo)] = new PropertyData() // int
            {
                NameOverride = $"Amount of cells to use  ({EditorUI.DefaultEmitter.ArrayModulo.Name})",
                ValueRangeNum = new ValueInfo<float>(1, 256, round: 0),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.AnimationFrameTime)] = new PropertyData() // float
            {
                TooltipOverride = "Used only if using animated/atlas/array texture feature." +
                                  "\nThis controls framerate, time between each frame, lower means it cycles faster.",
                ValueRangeNum = new ValueInfo<float>(0.016f, float.MaxValue, round: 3, limitNumberBox: true),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Color)] = new PropertyData() // animated 2D Vector4
            {
                TooltipAddition = "Can be higher than 1 to have bloom.",
                ValueRangeVector4 = new ValueInfo<Vector4>(Vector4.Zero, new Vector4(float.MaxValue), new Vector4(1f, 0f, 0.5f, 1f), limitNumberBox: true),
                RequiredKeys1D = 1,
                RequiredKeys2D = 1,
            },
            [new PropId(MyGPUGenerationPropertiesEnum.ColorIntensity)] = new PropertyData() // animated 2D float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 100f, defaultValue: 0f),
                RequiredKeys1D = 1,
                RequiredKeys2D = 1,
            },

            [new PropId(MyGPUGenerationPropertiesEnum.HueVar)] = new PropertyData() // float
            {
                NameOverride = $"Color Hue variance ({EditorUI.DefaultEmitter.HueVar.Name})",
                TooltipOverride = "Randomly varies hue, higher = further hue from the original color (1 being the full 360deg)." +
                                  "\nCan only be between 0 and 1, game code caps it.",
                ValueRangeNum = new ValueInfo<float>(0, 1, round: 3),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Emissivity)] = new PropertyData() // animated 2D float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 1000f, defaultValue: 0f),
                RequiredKeys1D = 1,
                RequiredKeys2D = 4,
                RequiredKeys2DTooltip = "Requires 4 keys because otherwise it resets to 4 keys with 0 value on deserialization.",

                // HACK: emitter.DeserializeFromObjectBuilder() does something special for Emissivity:
                /*
                    MyAnimatedPropertyFloat myAnimatedPropertyFloat = new MyAnimatedPropertyFloat();
	                Emissivity.GetInterpolatedKeys(0f, 1f, myAnimatedPropertyFloat);
	                if (myAnimatedPropertyFloat.GetKeysCount() < 4)
	                {
		                MyAnimatedPropertyFloat myAnimatedPropertyFloat2 = new MyAnimatedPropertyFloat();
		                myAnimatedPropertyFloat2.AddKey(0f, 0f);
		                myAnimatedPropertyFloat2.AddKey(0.33f, 0f);
		                myAnimatedPropertyFloat2.AddKey(0.66f, 0f);
		                myAnimatedPropertyFloat2.AddKey(1f, 0f);
		                Emissivity.AddKey(0f, myAnimatedPropertyFloat2);
	                }
                 */
            },

            [new PropId(MyGPUGenerationPropertiesEnum.ParticlesPerFrame)] = new PropertyData() // animated 1D float
            {
                TooltipAddition = "It's particles over effect lifetime (Duration from General tab)." +
                                  "\nDoes not seem to be affected by graphics quality.",
                ValueRangeNum = new ValueInfo<float>(0, int.MaxValue, round: 0),
                RequiredKeys1D = 0,
            },
            [new PropId(MyGPUGenerationPropertiesEnum.ParticlesPerSecond)] = new PropertyData() // animated 1D float
            {
                TooltipAddition = "Particles per second regardless of effect's total duration." +
                                  "\nAffected by graphics quality, see Debug tab for simulating different settings.",
                ValueRangeNum = new ValueInfo<float>(0, int.MaxValue, round: 0),
                RequiredKeys1D = 0,
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Offset)] = new PropertyData() // Vector3
            {
                ValueRangeVector3 = new ValueInfo<Vector3>(new Vector3(-30f), new Vector3(30f), round: 1),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Direction)] = new PropertyData() // Vector3
            {
                // HACK: buggy math, see \Content\Shaders\Transparent\GPUParticles\Emit.hlsl where Direction is being read
                TooltipAddition = "It is internally normalized to a unit vector." +
                                  "\nNOTE: It has buggy math for Z axis, if using only Z axis then it's fine, but if you have the other axis too then it will flip Z axis.",
                ValueRangeVector3 = new ValueInfo<Vector3>(new Vector3(-1f), new Vector3(1f), round: 2, limitNumberBox: true, invalidValue: Vector3.Zero),
                // TODO: any other prop easily validated with an invalid value?
            },

            [new PropId(MyGPUGenerationPropertiesEnum.DirectionConeVar)] = new PropertyData() // animated 1D float
            {
                NameOverride = "Direction cone",
                ValueRangeNum = new ValueInfo<float>(-360f, 360f), // TODO: are negative values useful here?
                RequiredKeys1D = 0,
            },
            [new PropId(MyGPUGenerationPropertiesEnum.DirectionInnerCone)] = new PropertyData() // animated 1D float
            {
                ValueRangeNum = new ValueInfo<float>(-360f, 360f), // TODO: are negative values useful here?
                RequiredKeys1D = 0,
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Acceleration)] = new PropertyData() // Vector3
            {
                ValueRangeVector3 = new ValueInfo<Vector3>(new Vector3(-100f), new Vector3(100f), round: 3),
            },
            [new PropId(MyGPUGenerationPropertiesEnum.AccelerationFactor)] = new PropertyData() // animated 2D float
            {
                ValueRangeNum = new ValueInfo<float>(-10f, 10f, defaultValue: 0),
                RequiredKeys1D = 1,
                RequiredKeys2D = 1,
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Velocity)] = new PropertyData() // animated 1D float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 100f, defaultValue: 0f, round: 1),
                RequiredKeys1D = 0,
            },
            [new PropId(MyGPUGenerationPropertiesEnum.VelocityVar)] = new PropertyData() // animated 1D float
            {
                NameOverride = "Velocity Variance",
                TooltipOverride = "Random multiplier applied to velocity, the given value goes both positive and negative." +
                                  "\nFormula used: (Velocity + Random(-1,1) * VelocityVariance) * UserScale * UserVelocityMultiplier",
                ValueRangeNum = new ValueInfo<float>(0f, 100f, defaultValue: 0f, round: 1),
                RequiredKeys1D = 0,
            },

            [new PropId(MyGPUGenerationPropertiesEnum.EmitterSize)] = new PropertyData() // animated 1D Vector3
            {
                ValueRangeVector3 = new ValueInfo<Vector3>(new Vector3(0), new Vector3(1000000)),
            },
            [new PropId(MyGPUGenerationPropertiesEnum.EmitterSizeMin)] = new PropertyData() // animated 1D float
            {
                ValueRangeNum = new ValueInfo<float>(0, 1000000),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.RotationReference)] = new PropertyData() // enum
            {
                TooltipAddition = "The 'Local and camera' value does nothing different than 'Local' in SE v201.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Angle)] = new PropertyData() // Vector3
            {
                TooltipAddition = "Value in degrees. Requires 'Rotation Reference' to be Local.",
                ValueRangeVector3 = new ValueInfo<Vector3>(new Vector3(-180), new Vector3(180), round: 0),
            },
            [new PropId(MyGPUGenerationPropertiesEnum.AngleVar)] = new PropertyData() // Vector3
            {
                NameOverride = "Angle variance",
                TooltipAddition = "Value in degrees. Requires 'Rotation Reference' to be Local.",
                ValueRangeVector3 = new ValueInfo<Vector3>(new Vector3(0), new Vector3(180), round: 0),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Radius)] = new PropertyData() // animated 2D float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 10f),
                RequiredKeys1D = 1,
                RequiredKeys2D = 1,
            },
            [new PropId(MyGPUGenerationPropertiesEnum.RadiusVar)] = new PropertyData() // float
            {
                NameOverride = "Radius Variance",
                ValueRangeNum = new ValueInfo<float>(0f, 2f),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Thickness)] = new PropertyData() // animated 2D float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 10f),
                RequiredKeys1D = 1,
                RequiredKeys2D = 1,
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Life)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue, limitNumberBox: true, invalidValue: 0),
            },
            [new PropId(MyGPUGenerationPropertiesEnum.LifeVar)] = new PropertyData() // float
            {
                NameOverride = "Life Variance",
                ValueRangeNum = new ValueInfo<float>(0f, 30f),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Light)] = new PropertyData() // bool
            {
                NameOverride = "Lit by the Sun",
                TooltipOverride = "Particles get lit by sunlight. Artificial lights or particle lights have no effect.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.VolumetricLight)] = new PropertyData() // bool
            {
                NameOverride = "Volumetric Lighting",
                TooltipAddition = "Shadow casting only works on particles.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.AmbientFactor)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(-10f, 10f), // TODO: unknown actual range
            },

            [new PropId(MyGPUGenerationPropertiesEnum.ShadowAlphaMultiplier)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(-10f, 10f), // TODO: unknown actual range
            },

            [new PropId(MyGPUGenerationPropertiesEnum.OITWeightFactor)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(-10f, 10f), // TODO: unknown actual range
                TooltipFixup = EditorUI.DefaultEmitter.OITWeightFactor.Description.Replace(", default", ".\nDefault"), // a single long sentence, splitting it up
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Streaks)] = new PropertyData() // bool
            {
                NameOverride = $"Stretch with Velocity ({EditorUI.DefaultEmitter.Streaks.Name})",
                TooltipOverride = "Stretches the particle billboard by its velocity." +
                                  "\nDoes not take external motion into account." +
                                  "\nRequires 'Rotation Reference' to be Camera.",
            },
            [new PropId(MyGPUGenerationPropertiesEnum.StreakMultiplier)] = new PropertyData() // float
            {
                TooltipOverride = "Only works if 'Stretch with Velocity' is enabled." +
                                  "\nFormula used: Velocity * StreakMultiplier / 2",
                ValueRangeNum = new ValueInfo<float>(-10f, 10f),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.CameraBias)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(-10f, 10f),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.DistanceScalingFactor)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 1f),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.CameraSoftRadius)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 50f),
                TooltipAddition = "Large particles can be partially sliced by this.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.SoftParticleDistanceScale)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 50f),
                TooltipOverride = "Distance/depth of fading/fogging when particle intersects geometry." +
                                  "\n0 will make it intersect/clip sharply, like a normal solid geometric face." +
                                  "\nVery high values will effectively render the particle always under anything." +
                                  "\nNOTE: High color values, intensity or emissivity affects this setting's ability to fade them out.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.UseAlphaAnisotropy)] = new PropertyData() // bool
            {
                NameOverride = "Transparent from the sides (Alpha Anisotropy)",
                TooltipOverride = "Makes particles transparent when you see sprite/billboard from the sides" +
                                  "\nOnly seems to work if 'Rotation reference' is set to Local.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Collide)] = new PropertyData() // bool
            {
                NameOverride = "Screenspace Collisions",
                TooltipOverride = "Enables particles to collide and bounce off environment." +
                                  "\nDoes not use physics engine, instead it uses screen depth buffer and can be very inaccurate.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Bounciness)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(-3f, 3f, round: 3),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.RotationVelocityCollisionMultiplier)] = new PropertyData() // float
            {
                TooltipAddition = "Setting this to 0 can cause the particle to be invisible.",
                ValueRangeNum = new ValueInfo<float>(-30f, 30f),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.CollisionCountToKill)] = new PropertyData() // int
            {
                ValueRangeNum = new ValueInfo<float>(0, 3, round: 0),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.SleepState)] = new PropertyData() // bool
            {
                NameOverride = "Sleep State",
                TooltipAddition = $"Seems to do the same thing as '{EditorUI.DefaultEmitter.CollisionCountToKill.Name}' being set to 0.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.Gravity)] = new PropertyData() // float
            {
                NameOverride = "Gravity Influence",
                TooltipOverride = "Nearby total gravity (natural and artificial) is multiplied by this value and added to the particle's velocity.",
                ValueRangeNum = new ValueInfo<float>(-3f, 3f),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.MotionInterpolation)] = new PropertyData() // bool
            {
                TooltipOverride = "Interpolates particle emitter positions between update ticks to form a more continuous line when particle's parent moves at high speeds." +
                                  "\nAlso enables Motion Inheritance slider to work.",
            },
            [new PropId(MyGPUGenerationPropertiesEnum.MotionInheritance)] = new PropertyData() // float
            {
                TooltipAddition = "Value 0 disables, 1 will perfectly ignore linear motion (not angular motion).",
                ValueRangeNum = new ValueInfo<float>(-2, 2, round: 3),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.RotationEnabled)] = new PropertyData() // bool
            {
                NameOverride = $"Spawn with random roll ({EditorUI.DefaultEmitter.RotationEnabled.Name})",
                TooltipOverride = "Makes particles spawn with a random roll angle." +
                                  "\nCould also have other unknown effets.",
            },

            [new PropId(MyGPUGenerationPropertiesEnum.RotationVelocity)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(-60f, 60f, defaultValue: 1f, round: 2),
                TooltipAddition = "Value in degrees per tick (60 ticks per second).",
            },
            [new PropId(MyGPUGenerationPropertiesEnum.RotationVelocityVar)] = new PropertyData() // float
            {
                NameOverride = "Rotation velocity variance",
                ValueRangeNum = new ValueInfo<float>(0f, 60f, round: 2),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.TargetCoverage)] = new PropertyData() // float
            {
                TooltipAddition = "Does not seem to be used in SE v201.",
                ValueRangeNum = new ValueInfo<float>(-10f, 10f, round: 2),
            },

            [new PropId(MyGPUGenerationPropertiesEnum.UseEmissivityChannel)] = new PropertyData() // bool
            {
                TooltipAddition = "Does not seem to be used in SE v201.",
            },

            // -------------------------------------------------------------------------------------------------------------------------------

            [new PropId(MyLightPropertiesEnum.Enabled)] = new PropertyData(), // bool

            [new PropId(MyLightPropertiesEnum.Color)] = new PropertyData() // animated 1D Vector4
            {
                RequiredKeys1D = 0,
            },
            [new PropId(MyLightPropertiesEnum.ColorVar)] = new PropertyData() // animated 1D float
            {
                NameOverride = "Color Variance",
                RequiredKeys1D = 0,
            },

            [new PropId(MyLightPropertiesEnum.Falloff)] = new PropertyData() // float
            {
                ValueRangeNum = new ValueInfo<float>(0f, 10f),
            },

            [new PropId(MyLightPropertiesEnum.GravityDisplacement)] = new PropertyData() // float
            {
                TooltipAddition = "This only works if effect is not parented.",
                ValueRangeNum = new ValueInfo<float>(-2f, 2f),
            },

            [new PropId(MyLightPropertiesEnum.Intensity)] = new PropertyData() // animated 1D float
            {
                RequiredKeys1D = 0,
            },
            [new PropId(MyLightPropertiesEnum.IntensityVar)] = new PropertyData() // animated 1D float
            {
                NameOverride = "Intensity Variance",
                RequiredKeys1D = 0,
            },

            [new PropId(MyLightPropertiesEnum.Position)] = new PropertyData() // animated 1D Vector3
            {
                RequiredKeys1D = 0,
            },
            [new PropId(MyLightPropertiesEnum.PositionVar)] = new PropertyData() // animated 1D Vector3
            {
                NameOverride = "Position Variance",
                RequiredKeys1D = 0,
            },

            [new PropId(MyLightPropertiesEnum.Range)] = new PropertyData() // animated 1D float
            {
                RequiredKeys1D = 0,
            },
            [new PropId(MyLightPropertiesEnum.RangeVar)] = new PropertyData() // animated 1D float
            {
                NameOverride = "Range Variance",
                RequiredKeys1D = 0,
            },

            [new PropId(MyLightPropertiesEnum.VarianceTimeout)] = new PropertyData() // float
            {
                TooltipOverride = "Time between variance properties randomizing again.",
                ValueRangeNum = new ValueInfo<float>(0f, 2f),
            },

            // -------------------------------------------------------------------------------------------------------------------------------

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.Enabled))] = new PropertyData(), // bool

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.ID))] = new PropertyData() // int
            {
                TooltipFixup = EditorUI.GetDescriptionAttrib(typeof(MyParticleEffectData), nameof(EditorUI.DefaultData.ID)),
                TooltipAddition = "Cannot change it directly in this editor. To keep things simpler it is set to the name's hashcode automatically.",
            },

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.Name))] = new PropertyData() // string
            {
                TooltipFixup = EditorUI.GetDescriptionAttrib(typeof(MyParticleEffectData), nameof(EditorUI.DefaultData.Name)),
            },

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.DistanceMax))] = new PropertyData() // float
            {
                NameFallback = "Max view distance",
                TooltipFixup = EditorUI.GetDescriptionAttrib(typeof(MyParticleEffectData), nameof(EditorUI.DefaultData.DistanceMax)),
                ValueRangeNum = new ValueInfo<float>(0, float.MaxValue),
            },

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.DurationMin))] = new PropertyData() // float
            {
                NameFallback = "Min duration",
                TooltipFixup = EditorUI.GetDescriptionAttrib(typeof(MyParticleEffectData), nameof(EditorUI.DefaultData.DurationMin)),
                TooltipAddition = "Duration will be random on every playback between min and max, unless max is smaller than min in which case the min value is used.",
                ValueRangeNum = new ValueInfo<float>(0, float.MaxValue),
            },

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.DurationMax))] = new PropertyData() // float
            {
                NameFallback = "Max duration",
                TooltipFixup = EditorUI.GetDescriptionAttrib(typeof(MyParticleEffectData), nameof(EditorUI.DefaultData.DurationMax)),
                ValueRangeNum = new ValueInfo<float>(0, float.MaxValue),
            },

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.Loop))] = new PropertyData() // bool
            {
                NameFallback = "Loop",
                TooltipFixup = EditorUI.GetDescriptionAttrib(typeof(MyParticleEffectData), nameof(EditorUI.DefaultData.Loop)),
            },

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.Priority))] = new PropertyData() // float
            {
                NameFallback = "Priority",
                TooltipOverride = "Determines the importance of rendering this particle (lower means more important)." +
                                  "\nThis only comes into effect if there's more than 1024 emitters in world, to determine which ones to keep rendering." +
                                  "\nIt sorts them incrementally by distance to camera multiplied by Priority, then uses the first 1024 of that sorted list to render." +
                                  "\nNOTE: it's stored squared in a float field, should not be less than -1e19 or more than 1e19.",
                ValueRangeNum = new ValueInfo<float>(-1e19f, 1e19f),
            },

            [new PropId(PropType.General, nameof(EditorUI.DefaultData.Length))] = new PropertyData() // float
            {
                NameFallback = "Length",
                TooltipFixup = EditorUI.GetDescriptionAttrib(typeof(MyParticleEffectData), nameof(EditorUI.DefaultData.Length)),
                TooltipAddition = "Seems unused inside the game, it's likely for KSH's own particle editor.",
                ValueRangeNum = new ValueInfo<float>(float.MinValue, float.MaxValue),
            },

            // -------------------------------------------------------------------------------------------------------------------------------

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserScale))] = new PropertyData() // float
            {
                NameFallback = "Scale Multiplier",
                TooltipFixup = "Affects emitter size, offset, radius, velocity, camera bias.",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserLifeMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Life Multiplier",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserFadeMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Fade Multiplier",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserBirthMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Birth Multiplier",
                TooltipFixup = "Affects emitter particle count, but only 'particles per second' ones.",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserRadiusMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Radius Multiplier",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserVelocityMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Velocity Multiplier",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.SoftParticleDistanceScaleMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Soft Particle Distance Multiplier",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserColorIntensityMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Color Intensity Multiplier",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.CameraSoftRadiusMultiplier))] = new PropertyData() // float
            {
                NameFallback = "Camera Soft Radius Multiplier",
                ValueRangeNum = new ValueInfo<float>(0f, float.MaxValue),
            },

            [new PropId(PropType.UserMultiplier, nameof(EditorUI.DefaultEffect.UserColorMultiplier))] = new PropertyData() // Vector4
            {
                NameFallback = "Color Multiplier",
                ValueRangeVector4 = new ValueInfo<Vector4>(new Vector4(float.MinValue), new Vector4(float.MaxValue), defaultValue: Vector4.One),
            },
        };

        /// <summary>
        /// Gets specific property info or default one, never returns null.
        /// </summary>
        public static PropertyData GetPropertyInfo(object propHost, IMyConstProperty prop)
        {
            if(propHost == null) throw new ArgumentNullException("host");
            if(prop == null) throw new ArgumentNullException("prop");
            return PropertyInfo.GetValueOrDefault(new PropId(propHost, prop.Name), DefaultPropInfo);
        }

        /// <summary>
        /// Gets specific property info or default one, never returns null.
        /// </summary>
        public static PropertyData GetPropertyInfo(PropId propId) => PropertyInfo.GetValueOrDefault(propId, DefaultPropInfo);

        public static void RemoveParticle(MyParticleEffect particle)
        {
            if(particle == null)
                return;

            //particle.Stop();
            MyParticlesManager.RemoveParticleEffect(particle);
        }

        static VersionSpecificInfo()
        {
#if false
            foreach(IMyConstProperty prop in EditorUI.DefaultEmitter.GetProperties())
            {
                var propId = new PropId(PropType.Emitter, prop.Name);
                if(!PropertyInfo.ContainsKey(propId))
                {
                    MyAPIGateway.Utilities.ShowNotification($"[debug] Emnitter property '{prop.Name}' is not declared in PropertyInfo!", 5000);
                }
            }

            foreach(IMyConstProperty prop in EditorUI.DefaultLight.GetProperties())
            {
                var propId = new PropId(PropType.Light, prop.Name);
                if(!PropertyInfo.ContainsKey(propId))
                {
                    MyAPIGateway.Utilities.ShowNotification($"[debug] Light property '{prop.Name}' is not declared in PropertyInfo!", 5000);
                }
            }
#endif
        }
    }

    public class PropertyData
    {
        public string NameOverride;
        public string NameFallback;

        public string TooltipOverride;
        public string TooltipFixup;
        public string TooltipFallback;
        public string TooltipAddition;

        /// <summary>
        /// Keys on first dimmension required to not crash the game.
        /// Only for animated 1D & 2D properties.
        /// </summary>
        public int RequiredKeys1D = 1;
        public string RequiredKeys1DTooltip;

        /// <summary>
        /// Keys on 2nd dimmension required to not crash the game.
        /// Only for animated 2D properties.
        /// </summary>
        public int RequiredKeys2D = 1;
        public string RequiredKeys2DTooltip;

        public ValueInfo<float> ValueRangeNum = new ValueInfo<float>(-1f, 1f);
        public ValueInfo<Vector3> ValueRangeVector3 = new ValueInfo<Vector3>(-Vector3.One, Vector3.One);
        public ValueInfo<Vector4> ValueRangeVector4 = new ValueInfo<Vector4>(-Vector4.One, Vector4.One);

        public string GetTooltip()
        {
            string tooltip = TooltipFixup;

            if(!string.IsNullOrEmpty(TooltipOverride))
                tooltip = $"(Overwritten description)\n{TooltipOverride}";

            if(string.IsNullOrEmpty(tooltip))
            {
                if(!string.IsNullOrEmpty(TooltipFallback))
                    tooltip = $"(Fallback description)\n{TooltipFallback}";
                else
                    tooltip = "(No description declared in SE code or in this plugin's code for this property)";
            }

            if(!string.IsNullOrEmpty(TooltipAddition))
                tooltip += $"\nExtra info: {TooltipAddition}";

            return tooltip;
        }

        public static string GetTooltip(PropertyData propInfo, IMyConstProperty prop)
        {
            if(prop == null) throw new ArgumentNullException("prop");

            string tooltip = prop.Description;

            if(propInfo != null)
            {
                if(!string.IsNullOrEmpty(propInfo.TooltipFixup))
                    tooltip = propInfo.TooltipFixup;

                if(!string.IsNullOrEmpty(propInfo.TooltipOverride))
                    tooltip = $"(Overwritten description)\n{propInfo.TooltipOverride}";

                if(string.IsNullOrEmpty(tooltip))
                {
                    if(!string.IsNullOrEmpty(propInfo.TooltipFallback))
                        tooltip = $"(Fallback description)\n{propInfo.TooltipFallback}";
                    else
                        tooltip = "(No description declared in SE code or in this plugin's code for this property)";
                }

                if(!string.IsNullOrEmpty(propInfo.TooltipAddition))
                    tooltip += $"\nExtra info: {propInfo.TooltipAddition}";
            }

            return tooltip;
        }

        public string GetName()
        {
            return NameOverride ?? NameFallback;
        }

        public static string GetName(PropertyData propInfo, IMyConstProperty prop)
        {
            if(propInfo == null)
                return prop.Name;

            string name = propInfo.NameOverride ?? prop.Name;

            if(string.IsNullOrEmpty(name))
                name = propInfo.NameFallback ?? "(Unknown)";

            return name;
        }
    }

    public struct ValueInfo<T> where T : struct
    {
        public readonly T Min;
        public readonly T Max;
        public readonly T? Default; // TODO: get rid of this in favor of getting default from property/field?
        public readonly int Rounding;
        public readonly int InputRounding;
        public readonly bool LimitNumberBox;
        public readonly T? InvalidValue;

        public ValueInfo(T min, T max, T? defaultValue = null, int round = 2, int inputRound = 6, bool limitNumberBox = false, T? invalidValue = null)
        {
            Min = min;
            Max = max;
            Default = defaultValue;
            Rounding = round;
            InputRounding = inputRound;
            LimitNumberBox = limitNumberBox;
            InvalidValue = invalidValue;
        }
    }

    public enum PropType { Emitter, Light, General, UserMultiplier }

    public struct PropId
    {
        public readonly PropType Type;
        public readonly string Name;

        public PropId(object parentObj, string propName)
        {
            if(parentObj == null) throw new ArgumentNullException("parentObj");

            if(parentObj is MyParticleGPUGenerationData)
                Type = PropType.Emitter;
            else if(parentObj is MyParticleLightData)
                Type = PropType.Light;
            else
                throw new Exception($"parentObj is not a supported type: {parentObj.GetType()}");

            Name = propName ?? throw new ArgumentNullException("propName");
        }

        public PropId(PropType type, string propName)
        {
            Type = type;
            Name = propName ?? throw new ArgumentNullException("propName");
        }

        public PropId(PropType type, IMyConstProperty prop)
        {
            Type = type;
            Name = prop?.Name ?? throw new ArgumentNullException("prop");
        }

        public PropId(MyGPUGenerationPropertiesEnum propEnum)
        {
            Type = PropType.Emitter;

            IMyConstProperty[] properties = (IMyConstProperty[])EditorUI.DefaultEmitter.GetProperties();
            Name = properties[(int)propEnum].Name;
        }

        public PropId(MyLightPropertiesEnum propEnum)
        {
            Type = PropType.Light;
            IMyConstProperty[] properties = (IMyConstProperty[])EditorUI.DefaultLight.GetProperties();
            Name = properties[(int)propEnum].Name;
        }
    }
}
