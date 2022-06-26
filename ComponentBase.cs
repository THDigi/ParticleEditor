namespace Digi.ParticleEditor
{
    /// <summary>
    /// Simply create a class extending this and it will be instanced by Editor.
    /// </summary>
    public abstract class EditorComponentBase
    {
        protected readonly Editor Editor;
        protected EditorUI EditorUI => Editor.EditorUI;

        public EditorComponentBase(Editor editor)
        {
            Editor = editor;
        }

        public abstract void Dispose();

        public abstract void Update();
    }
}
