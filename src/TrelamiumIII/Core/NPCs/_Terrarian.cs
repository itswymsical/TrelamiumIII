using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.NPCs.Actors;
using TrelamiumIII.Core.NPCs.Controllers;
using TrelamiumIII.Core.NPCs.Trading;
using TrelamiumIII.Utilities.Extensions;
using System.Collections.Generic;
using System.Linq;
using Terraria.Chat;
using Terraria.Localization;
using Terraria.ModLoader.IO;

namespace TrelamiumIII.Core.NPCs;

/// <summary>
/// This class acts as the "wandering World NPC" template.
/// </summary>
public abstract class Villager : WorldNPC
{
    public override bool CanDespawn => true;

    protected abstract Color[] HairPalette { get; }
    protected abstract Color[] EyePalette { get; }
    protected abstract Color[] SkinPallete { get; }
    protected abstract int[] MaleHairPool { get; }
    protected abstract int[] FemaleHairPool { get; }


    protected abstract string[] MaleNames { get; }

    protected abstract string[] FemaleNames { get; }

    public override List<string> SetNPCNameList() 
        => Male ? [.. MaleNames] : [.. FemaleNames];
    
    public virtual bool UsesTradingSystem { get; set;  } = true;
    private static T Pick<T>(T[] source) => source[Main.rand.Next(source.Length)];

    public sealed override void SetDefaults()
    {
        base.SetDefaults();
        if (UsesTradingSystem)
        {
            InitializeTrades();
            SetTradeInventory();
        }
    }

    public override string GetChat() => Main.rand.Next(11) switch
    {
        0 => "Hi... not sure I know you. Did you need something?",
        1 => "Unless you've got gear I'm interested in, I don't care buddy.",
        2 => "I'm always looking for a good deal. Care to trade?",
        3 => $"Oh, hi! Nice to meet you, {Main.LocalPlayer.name}.",
        4 => "People say I talk a lot. But I don't really think that can be true. This one time the Guide was just blabbering on about recipes and finding materials, a real long lecture I tell ya. I mean, dude... if you're the Guide then you should be the GUIDE. But to be fair, I guess he was basically doing that...",
        5 => "Huh? oh, hey.",
        6 => "Dude, there's this guy around here who won't shut up.",
        7 => "Am I even real? Or just, a serial array of binary with no will or agency...",
        8 => "You gotta stay safe out here, some of these Terrarians are absolute nutjobs.",
        9 => "You there, Traveler! Do you have any spare wood?",
        10 => "Trades! Trades! Trades! It may just be your lucky day...",
        _ => "Hey, what's your deal?",
    };

    protected override void SetAppearanceDefaults()
    {
        Male = Main.rand.NextBool();
        ClothingStyle = RollBodyVariant(Male);
        SkinColor = Pick(SkinPallete);
        EyeColor = Pick(EyePalette);

        HairType = Male
            ? MaleHairPool[Main.rand.Next(MaleHairPool.Length)]
            : FemaleHairPool[Main.rand.Next(FemaleHairPool.Length)];
        HairColor = Pick(HairPalette);

        NPC.HitSound = Male ? SoundID.PlayerHit : SoundID.FemaleHit;
        NPC.DeathSound = SoundID.PlayerKilled;
    }

    private static int RollBodyVariant(bool male) => male
    ? Pick([
        (int)PlayerClothingStyleID.MaleDefault,
        (int)PlayerClothingStyleID.MaleShirt,
        (int)PlayerClothingStyleID.MaleShirt2,
        (int)PlayerClothingStyleID.MaleLabcoat,
    ])
    : Pick([
        (int)PlayerClothingStyleID.FemaleDefault,
        (int)PlayerClothingStyleID.FemaleShirt,
        (int)PlayerClothingStyleID.FemaleShirt2,
        (int)PlayerClothingStyleID.FemaleLabcoat,
    ]);

    public override void SetChatButtons(ref string button, ref string button2)
    {
        if (!UsesTradingSystem)
            return;

        if (Trades is { Count: > 0 })
            button = "Trade";
    }

    public override void OnChatButtonClicked(bool firstButton, ref string shop)
    {
        if (!UsesTradingSystem)
            return;

        if (firstButton && Trades is { Count: > 0 })
            OpenTradeUI();
    }
    public override void SaveData(TagCompound tag)
    {
        base.SaveData(tag);

        var tradeTags = new List<TagCompound>();
        foreach (var trade in Trades)
        {
            var offerTag = new TagCompound { ["output"] = trade.Output.ItemId };
            trade.Save(offerTag);
            tradeTags.Add(offerTag);
        }
        tag["trades"] = tradeTags;
    }

    public override void LoadData(TagCompound tag)
    {
        base.LoadData(tag);

        if (!tag.ContainsKey("trades")) return;

        Trades.Clear();

        var byOutput = GetStockPool().ToDictionary(s => s.Offer.Output.ItemId);

        foreach (var entry in tag.GetList<TagCompound>("trades"))
        {
            int outputId = entry.GetInt("output");
            if (!byOutput.TryGetValue(outputId, out var stock)) continue;

            Trades.Add(stock.Offer);
            stock.Offer.Load(entry);
        }
    }

    #region Trading Mechanic

    public List<TradeOffer> Trades { get; private set; }

    /// <summary>
    /// Creates a new trade offer, registers it, and returns it for
    /// config. Call from <see cref="SetTrades"/>.
    /// </summary>
    public TradeOffer AddTrade()
    {
        var offer = new TradeOffer();
        Trades.Add(offer);
        return offer;
    }

    /// <summary>
    /// Returns offers that are currently visible to the player. Depleted or
    /// condition-gated offers can still render in the UI as greyed out; this
    /// helper excludes them.
    /// </summary>
    public IEnumerable<TradeOffer> GetAvailableOffers()
        => Trades.Where(t => t.IsUnlocked && t.InStock(this));

    private void InitializeTrades()
    {
        Trades = new List<TradeOffer>();
    }

    /// <summary>
    /// Returns the total stack of the given item across all CarriedInventory slots.
    /// </summary>
    public int GetCarriedItemCount(int itemId)
    {
        int total = 0;
        for (int i = 0; i < CarriedInventorySize; i++)
        {
            if (CarriedInventory[i].type == itemId)
                total += CarriedInventory[i].stack;
        }
        return total;
    }

    public virtual void OpenTradeUI()
    {
        TradeUI.Open(this);
    }


    /// <summary>Pairs an inventory entry with the trade that vends it.</summary>
    protected readonly record struct StockEntry(Action AddInventory, TradeOffer Offer);

    protected virtual List<StockEntry> GetStockPool() => [];
    protected static TradeOffer Trade() => new();

    private void SetTradeInventory()
    {
        var stock = GetStockPool();
        int picks = Main.rand.Next(2, 4);

        for (int i = 0; i < picks && stock.Count > 0; i++)
        {
            int idx = Main.rand.Next(stock.Count);
            var entry = stock[idx];

            entry.AddInventory();
            Trades.Add(entry.Offer);

            stock.RemoveAt(idx);
        }

        DrawWithoutReplacement(SetWorldInventory(), Main.rand.Next(1, 3));
    }

    protected static void DrawWithoutReplacement(List<Action> pool, int count)
    {
        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = Main.rand.Next(pool.Count);
            pool[idx]();
            pool.RemoveAt(idx);
        }
    }
    #endregion

    public override bool ModifyDeathMessage(ref NetworkText customText, ref Color color) 
        => false;
    
    public override void OnKill()
    {
        if (Main.rand.NextFloat() < 0.125f)
        {
            DropInventory(percentage: Main.rand.Next(25, 51));
        }

        DummyPlayer.headVelocity.Y = (float)Main.rand.Next(-40, -10) * 0.1f;
        DummyPlayer.bodyVelocity.Y = (float)Main.rand.Next(-40, -10) * 0.1f;
        DummyPlayer.legVelocity.Y = (float)Main.rand.Next(-40, -10) * 0.1f;
        DummyPlayer.headVelocity.X = (float)Main.rand.Next(-20, 21) * 0.1f + (float)(2 * DummyPlayer.direction);
        DummyPlayer.bodyVelocity.X = (float)Main.rand.Next(-20, 21) * 0.1f + (float)(2 * DummyPlayer.direction);
        DummyPlayer.legVelocity.X = (float)Main.rand.Next(-20, 21) * 0.1f + (float)(2 * DummyPlayer.direction);

        for (int i = 0; i < 100; i++)
        {
            int blood = Dust.NewDust(NPC.position, DummyPlayer.width, DummyPlayer.height, DustID.Blood, 2 * DummyPlayer.direction, -2f);
            Main.dust[blood].velocity *= 1f;
            Main.dust[blood].scale *= 1f;
            Main.dust[blood].fadeIn *= 0;
        }
        NetworkText deathText = NetworkText.FromLiteral($"{NPC.GivenName} was slain...");
        ChatHelper.BroadcastChatMessage(deathText, new Color(225, 25, 25));
        
        DummyPlayer.DropTombstone(0, deathText, DummyPlayer.direction);
    }
}