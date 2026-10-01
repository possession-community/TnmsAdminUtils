using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Utils.Entity;
using TnmsAdminUtils.Modules.UiInteractions;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class TeamName(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "teamname";
    public override string CommandDescription => "Sets a team's display name.";

    private const string Permission = "tnms.adminutil.management.ingame.command.teamname";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override void OnRegistered()
        => ((TnmsAdminUtils)Plugin).AdminMenu.Registry.Register(AdminMenuEntry.Create(CommandName, Permission).Usage("TeamName.Notification.Usage").Choice("AdminMenu.Step.Team", new AdminMenuChoice("ct", "AdminMenu.Choice.Team.Ct"), new AdminMenuChoice("t", "AdminMenu.Choice.Team.T")).Text("AdminMenu.Step.Name"));

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator(Permission, true))
        .Add(new ArgumentCountValidator(2, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case ArgumentCountValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "TeamName.Notification.Usage"));
                break;
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
        }

        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        string teamArg = commandInfo.GetArg(1).ToLower();

        CStrikeTeam team = teamArg switch
        {
            "ct" or "3" => CStrikeTeam.CT,
            "t" or "2" => CStrikeTeam.TE,
            _ => CStrikeTeam.Spectator
        };

        if (team == CStrikeTeam.Spectator)
        {
            PrintMessageToServerOrPlayerChat(client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(client, "Team.Notification.InvalidTeam"));
            return;
        }

        string newName = string.Join(" ", commandInfo.ArgString.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..]);
        CsTeamUtil.SetTeamName(team, newName);

        string executor = PlayerUtil.GetPlayerName(client);
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} set {teamArg} team name to {newName}");

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;

            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "TeamName.Broadcast.TeamNameSet", executor, teamArg.ToUpper(), newName));
        }
    }
}
