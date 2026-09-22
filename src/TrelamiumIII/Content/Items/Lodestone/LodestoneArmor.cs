using System.Collections.Generic;

namespace TrelamiumIII.Content.Lodestone;

[AutoloadEquip(EquipType.Legs)]
public class LodestoneLeggings : ModItem
{
    public override void SetDefaults()
    {
        Item.defense = 4;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 2);
        Item.width = Item.height = 34;
    }

    public override void UpdateEquip(Player player)
    {
        player.runAcceleration *= 1.1f;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ModContent.ItemType<LodestoneItem>(), 10)
        .AddIngredient(ModContent.ItemType<MagnetizedCoil>(), 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

[AutoloadEquip(EquipType.Body)]
public class LodestoneChestplate : ModItem
{
    public override void SetDefaults()
    {
        Item.defense = 5;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 2);
        Item.width = Item.height = 34;
    }

    public override void UpdateEquip(Player player)
    {
        player.endurance += 0.02f;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ModContent.ItemType<LodestoneItem>(), 12)
        .AddIngredient(ModContent.ItemType<MagnetizedCoil>(), 1)
        .AddIngredient(ItemID.Silk, 6)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

[AutoloadEquip(EquipType.Head)]
public class LodestoneHelmet : ModItem
{
    // Both the radius items get grabbed from and the radius the field is drawn at. One number
    // by design; see LodestoneFieldWarp.FieldRadius.
    const float FieldRadius = LodestoneFieldWarp.FieldRadius;

    public override void Load()
    {
        On_Main.DrawInfernoRings += DrawLodestoneField;
    }

    public override void Unload()
    {
        On_Main.DrawInfernoRings -= DrawLodestoneField;
    }

    private void DrawLodestoneField(On_Main.orig_DrawInfernoRings orig, Main self)
    {
        orig(self);

        // Fallback indicator for other players: LodestoneFieldWarp is a screen-space pass centred only on the local player.
        var activePlayers = new List<Player>();
        for (int i = 0; i < 255; i++)
        {
            var p = Main.player[i];
            if (i == Main.myPlayer) continue;

            if (p.active && !p.outOfRange && !p.dead && HasLodestoneSet(p))
                activePlayers.Add(p);
        }

        if (activePlayers.Count == 0) return;

        var shader = Assets.Shaders.ForceFieldShader.Asset.Value;
        if (shader == null) return;

        var sb = Main.spriteBatch;
        var tex = Assets.Textures.Noise.Perlin.Asset.Value;

        sb.End();

        sb.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                 SamplerState.LinearClamp, DepthStencilState.None,
                 RasterizerState.CullNone, shader,
                 Main.GameViewMatrix.TransformationMatrix);

        foreach (var player in activePlayers)
        {
            Vector2 screenPos = player.Center - Main.screenPosition;

            shader.Parameters["uPlayerPosition"]?.SetValue(screenPos);
            shader.Parameters["uScreenResolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            shader.Parameters["uTime"]?.SetValue(Main.GameUpdateCount * 0.015f);
            shader.Parameters["uFieldRadius"]?.SetValue(FieldRadius);
            shader.Parameters["uRippleFrequency"]?.SetValue(2f);
            shader.Parameters["uFalloffPower"]?.SetValue(2.5f);
            shader.Parameters["uPulseSpeed"]?.SetValue(1.5f);
            shader.Parameters["uPulseIntensity"]?.SetValue(0.15f);
            shader.Parameters["uBaseOpacity"]?.SetValue(0.8f);
            shader.Parameters["uColor"]?.SetValue(new Vector4(0.0f, 0.65f, 1.0f, 1f));
            shader.Parameters["uOpacity"]?.SetValue(2f);
            shader.CurrentTechnique.Passes[0].Apply();

            sb.Draw(tex,
                new Rectangle(0, 0, Main.screenWidth, Main.screenHeight),
                Color.White);
        }
        sb.End();

        sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                 Main.DefaultSamplerState, DepthStencilState.None,
                 Main.Rasterizer, null, Main.Transform);
    }

    internal static bool HasLodestoneSet(Player player)
    {
        return player.armor[0].type == ModContent.ItemType<LodestoneHelmet>()
            && player.armor[1].type == ModContent.ItemType<LodestoneChestplate>()
            && player.armor[2].type == ModContent.ItemType<LodestoneLeggings>();
    }

    public override void SetStaticDefaults()
    {
        ArmorIDs.Head.Sets.DrawFullHair[Item.headSlot] = true;
    }

    public override void SetDefaults()
    {
        Item.defense = 3;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 2);
        Item.width = Item.height = 34;
    }

    public override void UpdateEquip(Player player)
    {
        player.pickSpeed += 0.1f;
    }

    public override bool IsArmorSet(Item head, Item body, Item legs)
    {
        return body.type == ModContent.ItemType<LodestoneChestplate>()
            && legs.type == ModContent.ItemType<LodestoneLeggings>();
    }

    public override void UpdateArmorSet(Player player)
    {
        player.setBonus = "You emit a magnetic field that gravitates items towards you"
                        + "\nIncreases pickup range substantially";

        int grabbed = 0;

        for (int i = 0; i < Main.maxItems; i++)
        {
            var item = Main.item[i];
            if (!item.active || item.beingGrabbed || item.noGrabDelay != 0) continue;

            float distance = Vector2.Distance(player.Center, item.Center);
            if (distance > FieldRadius) continue;

            var movement = Vector2.Normalize(player.Center - item.Center);
            var speedFactor = 1f - distance / FieldRadius;
            item.velocity = movement * (3f * speedFactor);

            grabbed++;
        }

        // Lets the warp strain when the field is actually hauling something in.
        if (!Main.dedServ && player.whoAmI == Main.myPlayer)
            LodestoneFieldWarp.GrabbedItemCount = grabbed;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ModContent.ItemType<LodestoneItem>(), 8)
        .AddIngredient(ModContent.ItemType<MagnetizedCoil>(), 2)
        .AddIngredient(ItemID.CopperBar, 2)
        .AddTile(TileID.Anvils)
        .Register();
    }
}