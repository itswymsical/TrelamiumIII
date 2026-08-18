using TrelamiumIII.Core.Dialogue;
using System.Collections.Generic;
using Terraria.UI;

namespace TrelamiumIII.Common.Systems
{
    /// <summary>
    ///   The main system for the TrelamiumIII. This system is responsible for managing the mod's state and handling events.
    /// </summary>
    public class SubstrateMainSystem : ModSystem
    {
        public static SubstrateMainSystem Instance => ModContent.GetInstance<SubstrateMainSystem>();

        //public static LocalizedText ExperienceText { get; private set; }

        //public override void Load()
        //{
        //    var category = "UI";
        //    ExperienceText ??= Mod.GetLocalization($"{category}.ExperienceTracker");

        //}

        public override void UpdateUI(GameTime gameTime)
        {
            base.UpdateUI(gameTime);

            DialogueManager.Instance.Update();
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            var resourceBarIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Resource Bars"));

            if (resourceBarIndex != -1)
            {
                layers.Insert(resourceBarIndex, new LegacyGameInterfaceLayer(
                    "Reverie: Dialogue Manager",
                    delegate
                    {
                        var anchor = new Vector2(Main.screenWidth / 2f, Main.screenHeight);
                        DialogueManager.Instance.Draw(Main.spriteBatch, anchor);
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }
}
