namespace Digi.ParticleEditor
{
    /// <summary>
    /// Simply create a class extending this and it will be instanced by Editor.
    /// </summary>
    public abstract class EditorComponentBase
    {
        protected readonly Editor Editor;
        protected EditorUI EditorUI => Editor.EditorUI;

        /// <summary>
        /// Makes component update even if editor is closed.
        /// It will still not update if editor is unable to be opened anyway (MP games for example).
        /// </summary>
        public bool AlwaysUpdate { get; set; } = false;

        public EditorComponentBase(Editor editor)
        {
            Editor = editor;
        }

        public abstract void Dispose();

        public abstract void Update();
    }
}
