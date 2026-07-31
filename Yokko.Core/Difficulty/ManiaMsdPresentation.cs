using System.Globalization;

namespace Yokko.Core.Difficulty;

public static class ManiaMsdPresentation
{
    public static string FormatValue(ManiaMsdResult result) =>
        result.IsSuccess
            ? result.Value!.Value.ToString(
                "0.00",
                CultureInfo.InvariantCulture)
            : "--";

    public static string Qualifier(ManiaMsdResult result) =>
        result.IsSuccess
            ? $"ETTERNA MSD · "
              + ShortSkillsetName(
                  result.Skillsets!.DominantSkillset)
            : "ETTERNA MSD";

    public static string ShortSkillsetName(
        EtternaMsdSkillset skillset) => skillset switch
    {
        EtternaMsdSkillset.Overall => "OVERALL",
        EtternaMsdSkillset.Stream => "STREAM",
        EtternaMsdSkillset.Jumpstream => "JUMPSTREAM",
        EtternaMsdSkillset.Handstream => "HANDSTREAM",
        EtternaMsdSkillset.Stamina => "STAMINA",
        EtternaMsdSkillset.JackSpeed => "JACK",
        EtternaMsdSkillset.Chordjack => "CHORDJACK",
        EtternaMsdSkillset.Technical => "TECH",
        _ => throw new ArgumentOutOfRangeException(nameof(skillset)),
    };
}

