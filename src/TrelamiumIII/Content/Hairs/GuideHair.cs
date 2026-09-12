
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TrelamiumIII.Content.Hairs;

/// <summary>Pre-coloured, so use the default gray when setting hair colour.</summary>
public class GuideHair : ModHair
{
	public override Gender RandomizedCharacterCreationGender => Gender.Female;

	public override bool AvailableDuringCharacterCreation => false;

	public override void SetStaticDefaults() {
		HairID.Sets.DrawBackHair[Type] = true;
	}
}
