using System;
using System.Collections.Generic;
using Digi.ParticleEditor.GameData;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Render.Particles;
using VRageMath;

namespace Digi.ParticleEditor
{
    public class ParticleHandler
    {
        public class Multipliers
        {
            public float Scale { get; set; } = 1f;
            public float Life { get; set; } = 1f;
            public float Fade { get; set; } = 1f;
            public float Birth { get; set; } = 1f;
            public float Radius { get; set; } = 1f;
            public float Velocity { get; set; } = 1f;
            public float SoftParticle { get; set; } = 1f;
            public float ColorIntensity { get; set; } = 1f;
            public float CameraSoftRadius { get; set; } = 1f;
            public Vector4 Color { get; set; } = Vector4.One;

            readonly ParticleHandler Handler;

            public Multipliers(ParticleHandler handler)
            {
                Handler = handler;
            }

            public void ApplyToParticle()
            {
                Handler.SpawnedEffect.UserBirthMultiplier = Birth;
                Handler.SpawnedEffect.UserRadiusMultiplier = Radius;
                Handler.SpawnedEffect.UserVelocityMultiplier = Velocity;
                Handler.SpawnedEffect.UserColorIntensityMultiplier = ColorIntensity;
                Handler.SpawnedEffect.UserLifeMultiplier = Life;
                Handler.SpawnedEffect.CameraSoftRadiusMultiplier = CameraSoftRadius;
                Handler.SpawnedEffect.SoftParticleDistanceScaleMultiplier = SoftParticle;
                Handler.SpawnedEffect.UserColorMultiplier = Color;
                Handler.SpawnedEffect.UserScale = Scale;
                Handler.SpawnedEffect.UserFadeMultiplier = Fade;
            }

            public void ResetAll()
            {
                Scale = 1f;
                Life = 1f;
                Fade = 1f;
                Birth = 1f;
                Radius = 1f;
                Velocity = 1f;
                SoftParticle = 1f;
                ColorIntensity = 1f;
                CameraSoftRadius = 1f;
                Color = Vector4.One;
            }
        }

        public MyParticleEffect SpawnedEffect { get; private set; }
        public MyParticleEffectData Data { get; private set; }
        public string Name => Data?.Name;

        public MyObjectBuilder_ParticleEffect OriginalData { get; set; }

        public readonly Multipliers UserMultipliers;

        /// <summary>
        /// Loaded a different particle.
        /// </summary>
        public event ChangedDel Changed;
        public delegate void ChangedDel(MyParticleEffectData oldParticle, MyParticleEffectData newParticle);

        bool _hasChanges;
        public bool HasChanges
        {
            get => _hasChanges;
            set
            {
                if(value && !_hasChanges)
                    EditsMade?.Invoke();

                _hasChanges = value;
            }
        }

        /// <summary>
        /// When HasChanges is first set to true.
        /// </summary>
        public event Action EditsMade;

        public bool Paused { get; set; }

        bool _useParent = true;
        public bool UseParent
        {
            get => _useParent;
            set
            {
                _useParent = value;

                if(!value)
                    LastWorldMatrix = null;

                if(Name != null)
                    Spawn(Name);
            }
        }

        float _particlePositionDistance;
        public float ParentDistance
        {
            get => _particlePositionDistance;
            set
            {
                _particlePositionDistance = value;
                RefreshPositionOnly();
            }
        }
        Vector3 ParticlePositionOffset => new Vector3(-ParentDistance / 4f, 1.5f, -ParentDistance);

        MyEntity LastParent;
        MatrixD? LastWorldMatrix;

        public ParticleHandler()
        {
            UserMultipliers = new Multipliers(this);
        }

        public void Update()
        {
            if(DoRefresh && NoRefreshUntilTick <= MySession.Static.GameplayFrameCounter)
            {
                DoRefresh = false;
                Refresh();
            }

            if(SpawnedEffect != null)
            {
                MyCharacter character = MySession.Static?.LocalCharacter;

                // re-parent particle if got a new character
                if(UseParent && character != null && LastParent != character)
                {
                    Spawn(Name);
                }
            }
        }

        const int RefreshMaxFPS = 10;
        int NoRefreshUntilTick = 0;
        bool DoRefresh = false;

        public void Refresh(bool edits = true)
        {
            int tick = MySession.Static.GameplayFrameCounter;

            if(NoRefreshUntilTick > tick)
            {
                DoRefresh = true; // if multiple changes are done in quick succession, do them after the tick timeout expires regardless of this method being called
                return;
            }

            if(Name != null)
            {
                NoRefreshUntilTick = tick + (60 / RefreshMaxFPS);
                DoRefresh = false;

                // HACK: this will cause the particle instance to be fed back to the pool, I need to actually respawn it to avoid that
                //Particle.Data.SetDirty();
                Spawn(Name, false);

                //if(Paused)
                //{
                //    Particle.Play();
                //    Particle.Pause();
                //}

                if(edits)
                {
                    HasChanges = true;
                    EditsMade?.Invoke();
                }
            }
        }

        public void RefreshPositionOnly()
        {
            MyCharacter character = MySession.Static?.LocalCharacter;
            if(!UseParent || SpawnedEffect == null || character == null)
                return;

            SpawnedEffect.WorldMatrix = MatrixD.CreateTranslation(ParticlePositionOffset);
        }

        public void Despawn()
        {
            NoRefreshUntilTick = 0;

            if(SpawnedEffect == null)
                return;

            SpawnedEffect.Data?.SetDirty(); // Data goes null when effect is finished playing (non-loop)

            SpawnedEffect.OnDelete -= EffectOnDelete;

            VersionSpecificInfo.RemoveParticle(SpawnedEffect);
            SpawnedEffect = null;
        }

        public void Spawn(string subtypeId, bool firstSpawn = false)
        {
            MyParticleEffectData particleData = MyParticleEffectsLibrary.Get(subtypeId);
            if(particleData == null)
            {
                EditorUI.PopupInfo("ERROR", $"Particle '{subtypeId}' doesn't exist.", MyMessageBoxStyleEnum.Error);
                return;
            }

            MyCharacter character = MySession.Static?.LocalCharacter;
            if(character == null)
            {
                EditorUI.PopupInfo("ERROR", "You need a character", MyMessageBoxStyleEnum.Error);
                return;
            }

            if(particleData.Name != Name)
            {
                Changed?.Invoke(Data, particleData);
            }

            Despawn();

            Data = particleData;
            LastParent = character;

            // this is a local matrix when particle is parented
            MatrixD matrix = MatrixD.CreateTranslation(ParticlePositionOffset);
            Vector3D cameraPos = MySector.MainCamera.Position; // HACK: TryCreateParticleEffect() will not spawn particle if this position is further than MaxDistance if particle is also not looped.

            bool enabled = particleData.Enabled;
            if(!enabled)
            {
                if(firstSpawn)
                    Notifications.Show($"NOTE: '{subtypeId}' has Enabled=false. Was temporarily set it to true to spawn for edit.", 6, Color.Yellow);
                particleData.Enabled = true;
            }

            if(!LastWorldMatrix.HasValue)
                LastWorldMatrix = character.WorldMatrix;

            uint parent;
            if(UseParent)
            {
                parent = character.Render.GetRenderObjectID();
            }
            else
            {
                parent = VersionSpecificInfo.NoParentId;
                matrix *= LastWorldMatrix.Value;
            }

            if(!particleData.Loop && particleData.DistanceMax > 0 && Vector3D.DistanceSquared(character.WorldMatrix.Translation, cameraPos) > (particleData.DistanceMax * particleData.DistanceMax))
            {
                if(firstSpawn)
                {
                    Notifications.Show($"NOTE: '{subtypeId}' has {particleData.DistanceMax:0.##}m max view distance and does not loop.", 10, Color.Yellow);
                    Notifications.Show("Which is too far from camera right now, but it spawns anyway because this is an editor.", 10, Color.White);
                }
            }

            MyParticleEffect particle;
            MyParticlesManager.TryCreateParticleEffect(subtypeId, ref matrix, ref cameraPos, parent, out particle);

            // needed? consequences?
            //particle.Autodelete = false;

            SpawnedEffect = particle;
            Paused = false;
            HasChanges = false;
            OriginalData = EditorUI.Instance.OriginalParticleData.GetValueOrDefault(subtypeId);

            particleData.Enabled = enabled;

            UserMultipliers.ApplyToParticle();

            if(SpawnedEffect == null)
            {
                EditorUI.PopupInfo("ERROR", $"Couldn't spawn particle '{subtypeId}'.\n" +
                                            $"TryCreateParticleEffect() simply refused to do it.\n" +
                                            $"ExistsInLib={(particleData != null)}; Enabled={(particleData?.Enabled.ToString() ?? "N/A")}", MyMessageBoxStyleEnum.Error);
            }

            SpawnedEffect.OnDelete += EffectOnDelete;
        }

        void EffectOnDelete(MyParticleEffect _)
        {
        }
    }
}
