namespace TrelamiumIII.Core.Dialogue;

/// <summary>
/// abstract class for defining and registering dialogue line effects, autoloaded.
/// </summary>
abstract class ModDialogue : ILoadable
{
    protected abstract void Register();

    void ILoadable.Load(Mod mod)
    {
       Register();
    }

    void ILoadable.Unload() { }
}
