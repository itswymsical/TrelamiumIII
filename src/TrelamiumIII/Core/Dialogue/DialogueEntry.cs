namespace TrelamiumIII.Core.Dialogue;

abstract class DialogueEntry : ILoadable
{
    protected abstract void Register();

    void ILoadable.Load(Mod mod)
    {
       Register();
    }

    void ILoadable.Unload() { }
}
