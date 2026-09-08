
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TrelamiumIII.Content.Hairs;

/// <summary>
/// since its the guide hair and pre-colored, when setting hair color just use the default gray
/// </summary>
public class GuideHair : ModHair
{
	public override Gender RandomizedCharacterCreationGender => Gender.Female;

	public override bool AvailableDuringCharacterCreation => false;

	public override void SetStaticDefaults() {
		HairID.Sets.DrawBackHair[Type] = true;
	}
}
