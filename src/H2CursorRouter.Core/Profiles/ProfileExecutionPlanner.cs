namespace H2CursorRouter.Core.Profiles;

public static class ProfileExecutionPlanner
{
    public static bool ShouldApplyCursorLayout(ExecutionProfile profile, bool? h2AckOk)
    {
        if (string.IsNullOrWhiteSpace(profile.CursorLayoutId))
        {
            return false;
        }

        if (profile.H2Preset is null)
        {
            return true;
        }

        return !profile.RequireH2AckBeforeCursorLayout || h2AckOk == true;
    }
}
