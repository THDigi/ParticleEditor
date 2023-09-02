using System;
using System.Linq;
using Digi.ParticleEditor.GameData;
using Sandbox.Graphics;
using Sandbox.Graphics.GUI;
using VRage.Render.Particles;
using VRage.Utils;
using VRageMath;
using VRageRender;
using VRageRender.Animations;

namespace Digi.ParticleEditor.UIControls
{
    // TODO: redesign with a "Close & save" button and draggable number boxes
    public class UITextureAligner : MyGuiScreenBase, IScreenAllowHotkeys
    {
        MyParticleGPUGenerationData Emitter;

        VerticalControlsHost Host;
        MyGuiControlImage ImageControl;
        MyGuiControlCombobox MaterialComboBox;
        Vector2 TextureSize;

        static readonly Vector2 ScreenSize = new Vector2(1f, 0.95f);
        static readonly Vector2 ScreenPosition = new Vector2(0.5f, 0.5f);

        public override string GetFriendlyName() => nameof(UITextureAligner);

        public UITextureAligner(MyParticleGPUGenerationData emitter)
            : base(ScreenPosition, MyGuiConstants.SCREEN_BACKGROUND_COLOR, ScreenSize, false, null, backgroundTransition: 1f, guiTransition: 1f)
        {
            Emitter = emitter;
            Align = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER;
            Host = new VerticalControlsHost(this, ScreenSize / -2f, ScreenSize);

            m_closeOnEsc = true;
            CanBeHidden = true;
            CanHideOthers = false;
            EnabledBackgroundFade = false;
            CloseButtonEnabled = true;

            RecreateControls(true);
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            Host.Reset();

            MyGuiControlLabel comboLabel = Host.CreateLabel(Emitter.Material.Name);
            comboLabel.SetToolTip(Emitter.Material.Description);

            MaterialComboBox = Host.CreateComboBox();
            MaterialComboBox.SetToolTip(Emitter.Material.Description + VersionSpecificInfo.MaterialComboBoxTooltipAddition);

            float labelWidth = Host.PanelSize.X * 0.2f;

            foreach(MyTransparentMaterial mat in MyTransparentMaterials.Materials)
            {
                if(VersionSpecificInfo.IsMaterialSupported(mat))
                {
                    MaterialComboBox.AddItem(mat.Id.Id, $"{mat.Id.String} {(mat == EditorUI.DefaultEmitter.Material.GetValue() ? "(default)" : "")}", sort: false);
                }
            }

            MaterialComboBox.AddItem(0, "--- Unsupported materials: ---", sort: false);

            foreach(MyTransparentMaterial mat in MyTransparentMaterials.Materials.OrderBy(m => m.Id.String))
            {
                if(!VersionSpecificInfo.IsMaterialSupported(mat))
                {
                    MaterialComboBox.AddItem(mat.Id.Id, $"{mat.Id.String}", sort: false);
                }
            }

            MyTransparentMaterial selectedMaterial = Emitter.Material.GetValue();
            if(selectedMaterial != null)
                MaterialComboBox.SelectItemByKey(selectedMaterial.Id.Id, false);

            MaterialComboBox.ItemSelected += () =>
            {
                int key = (int)MaterialComboBox.GetSelectedKey();
                if(key == 0)
                    return;

                MyStringId id = new MyStringId();
                id.m_id = key;

                MyTransparentMaterial material;
                if(MyTransparentMaterials.TryGetMaterial(id, out material))
                {
                    Emitter.Material.SetValue(material);

                    RecreateControls(false);
                    //RefreshParticle();
                }
                else
                {
                    Notifications.Show($"Error attempting to set material to '{id.String}', not found in transparent materials...", 10, Color.Red);
                }
            };

            comboLabel.Size = new Vector2(Host.AvailableWidth * (2f / 10f), comboLabel.Size.Y);
            MaterialComboBox.Size = new Vector2(Host.AvailableWidth * (7f / 10f), MaterialComboBox.Size.Y);
            Host.PositionControlsNoSize(comboLabel, MaterialComboBox);

            if(selectedMaterial == null || selectedMaterial.TextureType != MyTransparentMaterialTextureType.FileTexture || selectedMaterial.Texture == null)
            {
                Host.PositionAndFillWidth(Host.CreateLabel("ERROR: Invalid material (not FileTexture or no null texture)"));
                return;
            }

            string texturePath = selectedMaterial.Texture;

            TextureSize = MyRenderProxy.GetTextureSize(texturePath);

            //var label = EditorUI.Host.CreateLabel($"{Path.GetFileName(texturePath)}      {(int)TextureSize.X} x {(int)TextureSize.Y}");
            //Host.PositionControl(comboLabel);

            ImageControl = new MyGuiControlImage(backgroundTexture: texturePath);
            ImageControl.BorderEnabled = true;
            ImageControl.BorderColor = MyGuiConstants.THEMED_GUI_LINE_COLOR;

            // FIXME: a way to make this always square? Size is relative to parent...
            const float ImageScale = 0.65f;
            ImageControl.Size = new Vector2(ImageScale);

            MyGuiControlParent imageParent = new MyGuiControlParent();
            imageParent.BackgroundTexture = new MyGuiCompositeTexture(@"Textures\GUI\Blank.dds");
            imageParent.ColorMask = new Color(55, 55, 55);
            imageParent.Size = ImageControl.Size;
            imageParent.Position = Host.CurrentPosition + Host.Padding + ImageControl.Size / 2;

            imageParent.Controls.Add(ImageControl);

            Host.Add(imageParent);
            Host.MoveY(ImageControl.Size.Y + Host.Padding.Y);

            {
                Vector3 v = Emitter.ArraySize.GetValue();

                MyParticleGPUGenerationData propHost = Emitter;
                MyConstPropertyVector3 prop = Emitter.ArraySize;

                Vector3 def = Vector3.Zero;
                if(EditorUI.GetOriginalPropData(propHost, prop.Name, out GenerationProperty propOB))
                    def = propOB.ValueVector3;

                PropertyData propInfo = VersionSpecificInfo.GetPropertyInfo(propHost, prop);
                string name = PropertyData.GetName(propInfo, prop);
                string tooltip = PropertyData.GetTooltip(propInfo, prop);
                Vector3 min = propInfo.ValueRangeVector3.Min;
                Vector3 max = propInfo.ValueRangeVector3.Max;
                int round = propInfo.ValueRangeVector3.Rounding;

                Host.InsertMultiSlider(name, tooltip,
                    EditorUI.Vector2AxisNames,
                    new float[] { min.X, min.Y },
                    new float[] { max.X, max.Y },
                    new float[] { v.X, v.Y, },
                    new float[] { def.X, def.Y },
                    round, (dim, value) =>
                    {
                        Vector3 vec = Emitter.ArraySize.GetValue();
                        vec.SetDim(dim, (float)Math.Round(value, round));
                        Emitter.ArraySize.SetValue(vec);
                    });
            }

            Host.PropSliderInt(Emitter, Emitter.ArrayOffset);

            Host.PropSliderInt(Emitter, Emitter.ArrayModulo);
        }

        public override bool Draw()
        {
            bool ret = base.Draw();

            if(ImageControl != null && !MaterialComboBox.IsOpen)
            {
                Vector2 topLeft = ImageControl.GetPositionAbsoluteTopLeft();

                Vector3 arraySize3D = Emitter.ArraySize.GetValue();
                Vector2 gridSize = new Vector2(arraySize3D.X, arraySize3D.Y);

                Vector2 imageSize = ImageControl.Size;

                Vector2 cellSize = imageSize / gridSize; // individual cell size in GUI space

                int offset = Emitter.ArrayOffset.GetValue();
                int cells = Emitter.ArrayModulo.GetValue();

                // HACK: because of how these 2 values are used by game code (with bitwise math that I don't understand), you can have modulo be set to 0 and offset be +1 and it'll work.
                if(cells == 0)
                {
                    cells = 1;
                    offset -= 1;
                }

                // from \Shaders\Transparent\GPUParticles\Globals.hlsl
                //   uvw.x = float(imgOffset % dimX) / dimX + uvOffset.x / dimX;
                //   uvw.y = float(imgOffset / dimX) / dimY + uvOffset.y / dimY;

                for(int idx = offset; idx < offset + cells; idx++)
                {
                    float x = (int)(idx % gridSize.X) * cellSize.X;
                    float y = (int)(idx / gridSize.X) * cellSize.Y;

                    Vector2 pos = topLeft + new Vector2(x, y);

                    int border = 1;
                    Color color = Color.Lime;
                    if(x < 0 || y < 0 || y >= imageSize.X || y >= imageSize.Y)
                    {
                        color = Color.Red;
                        border = 5;
                    }

                    MyGuiManager.DrawBorders(pos, cellSize, color * m_transitionAlpha, border);
                }
            }

            return ret;
        }

        bool IScreenAllowHotkeys.IsAllowed(Vector2 mousePosition)
        {
            // mouse over GUI, ignore hotkeys
            if(Host.Panel.Rectangle.Contains(mousePosition))
                return false;

            return true;
        }
    }
}