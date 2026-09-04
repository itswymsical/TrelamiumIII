namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     The job board's own strings, resolved from <c>Localization/Jobs</c>.
/// </summary>
/// <remarks>
///     Looked up per call rather than cached in statics: this type could be touched before the
///     localisation files have loaded, and the lookup itself is a dictionary hit.
/// </remarks>
internal static class JobBoardText
{
    private static string Get(string key) => TrelamiumIII.Instance.GetLocalization($"Jobs.UI.{key}").Value;

    public static string ListHeader(int count)
        => TrelamiumIII.Instance.GetLocalization("Jobs.UI.ListHeader").Format(count);

    public static string From(string provider)
        => TrelamiumIII.Instance.GetLocalization("Jobs.UI.From").Format(provider);

    public static string Rewards => Get("Rewards");
    public static string NoneAvailable => Get("NoneAvailable");

    public static string TakeJob => Get("TakeJob");
    public static string Close => Get("Close");

    public static string ConfirmPrompt => Get("ConfirmPrompt");
    public static string ConfirmAccept => Get("ConfirmAccept");
    public static string ConfirmBack => Get("ConfirmBack");
}
