namespace TrelamiumIII.Core.Jobs;

/// <summary>
/// Represents a reward for completing a Job, which can be either a vanilla item or a cross-mod item.
/// </summary>
public class JobReward
{
    private Item _resolvedItem;
    private readonly string _modItemIdentifier;
    private readonly int _vanillaItemType;
    private readonly int _stack;
    private bool _isResolved;

    // Vanilla item
    public JobReward(int itemType, int stack = 1)
    {
        _vanillaItemType = itemType;
        _stack = stack;
        _isResolved = false;
        _resolvedItem = new Item(itemType, stack);
    }

    /// <summary>
    /// Creates a Job reward for a cross-mod item.
    /// </summary>
    /// <param name="modItemIdentifier"></param>
    /// <param name="stack"></param>
    /// 
    /// <remarks>Cross-mod item (format: "ModName/ItemName")</remarks>
    public JobReward(string modItemIdentifier, int stack = 1)
    {
        _modItemIdentifier = modItemIdentifier;
        _stack = stack;
        _isResolved = false;
    }
    /// <summary>
    /// called to get item IDs and/or textures post-initialization. The item will be resolved lazily when GetItem() is called.
    /// Vanilla sometimes doesn't load ItemIDs during init, and that obv is the same case for modded items.
    /// </summary>
    /// <returns></returns>
    public Item GetItem() 
    {
        if (!_isResolved && _modItemIdentifier != null)
        {
            TryResolve();
        }
        return _resolvedItem;
    }

    private void TryResolve()
    {
        var parts = _modItemIdentifier.Split('/');
        if (parts.Length != 2)
        {
            ModContent.GetInstance<TrelamiumIII>().Logger.Error($"Invalid mod item identifier: {_modItemIdentifier}");
            _isResolved = true;
            return;
        }

        var modName = parts[0];
        var itemName = parts[1];

        if (ModLoader.TryGetMod(modName, out Mod mod))
        {
            if (mod.TryFind<ModItem>(itemName, out var item))
            {
                _resolvedItem = new Item(item.Type, _stack);
                _isResolved = true;
                ModContent.GetInstance<TrelamiumIII>().Logger.Debug($"Resolved {_modItemIdentifier} to item type {item.Type}");
                return;
            }
        }

        ModContent.GetInstance<TrelamiumIII>().Logger.Warn($"Could not resolve mod item: {_modItemIdentifier}");
        _isResolved = true;
    }

    public bool IsResolved => _isResolved;
    public bool IsValid => _resolvedItem != null && _resolvedItem.type > ItemID.None;
}