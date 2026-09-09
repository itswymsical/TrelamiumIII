namespace TrelamiumIII.Utilities;

public static class RarityColorUtil
{
    // pulled from vanilla
    public static Color GetPopupRarityColor(int itemRarity)
    {
        //IL_0013: Unknown result type (might be due to invalid IL or missing references)
        //IL_002c: Unknown result type (might be due to invalid IL or missing references)
        //IL_0045: Unknown result type (might be due to invalid IL or missing references)
        //IL_005e: Unknown result type (might be due to invalid IL or missing references)
        //IL_0077: Unknown result type (might be due to invalid IL or missing references)
        //IL_008d: Unknown result type (might be due to invalid IL or missing references)
        //IL_00a6: Unknown result type (might be due to invalid IL or missing references)
        //IL_00bf: Unknown result type (might be due to invalid IL or missing references)
        //IL_00d5: Unknown result type (might be due to invalid IL or missing references)
        //IL_00eb: Unknown result type (might be due to invalid IL or missing references)
        //IL_0101: Unknown result type (might be due to invalid IL or missing references)
        //IL_0115: Unknown result type (might be due to invalid IL or missing references)
        //IL_012c: Unknown result type (might be due to invalid IL or missing references)
        //IL_0143: Unknown result type (might be due to invalid IL or missing references)
        //IL_013d: Unknown result type (might be due to invalid IL or missing references)
        if (itemRarity == 1)
        {
            return new Color(150, 150, 255);
        }
        if (itemRarity == 2)
        {
            return new Color(150, 255, 150);
        }
        if (itemRarity == 3)
        {
            return new Color(255, 200, 150);
        }
        if (itemRarity == 4)
        {
            return new Color(255, 150, 150);
        }
        if (itemRarity == 5)
        {
            return new Color(255, 150, 255);
        }
        if (itemRarity == -11)
        {
            return new Color(255, 175, 0);
        }
        if (itemRarity == -1)
        {
            return new Color(130, 130, 130);
        }
        if (itemRarity == 6)
        {
            return new Color(210, 160, 255);
        }
        if (itemRarity == 7)
        {
            return new Color(150, 255, 10);
        }
        if (itemRarity == 8)
        {
            return new Color(255, 255, 10);
        }
        if (itemRarity == 9)
        {
            return new Color(5, 200, 255);
        }
        if (itemRarity == 10)
        {
            return new Color(255, 40, 100);
        }
        if (itemRarity == 11)
        {
            return new Color(180, 40, 255);
        }
        if (itemRarity >= 12)
        {
            return RarityLoader.GetRarity(itemRarity).RarityColor;
        }
        return Color.White;
    }
}
