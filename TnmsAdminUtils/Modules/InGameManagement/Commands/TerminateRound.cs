using System.Globalization;
using Sharp.Shared.Enums;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using TnmsPluginFoundation.Extensions.Client;
using TnmsPluginFoundation.Models.Command;
using TnmsPluginFoundation.Models.Command.Validators;
using TnmsPluginFoundation.Models.Command.Validators.RangedValidators;
using TnmsPluginFoundation.Utils.Entity;

namespace TnmsAdminUtils.Modules.InGameManagement.Commands;

public class TerminateRound(IServiceProvider provider) : TnmsAbstractCommandBase(provider)
{
    public override string CommandName => "terminateround";
    public override List<string> CommandAliases => ["endround"];
    public override string CommandDescription => "Terminates the current round immediately or specified seconds.";

    public override TnmsCommandRegistrationType CommandRegistrationType =>
        TnmsCommandRegistrationType.Client | TnmsCommandRegistrationType.Server;

    protected override ICommandValidator? GetValidator() => new CompositeValidator()
        .Add(new PermissionValidator("tnms.adminutil.management.ingame.command.terminateround", true))
        .Add(new RangedArgumentValidator<float>(0.0f, 30.0f, 1, 0.0f, true));

    protected override ValidationFailureResult OnValidationFailed(ValidationFailureContext context)
    {
        switch (context.Validator)
        {
            case PermissionValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.NotEnoughPermissions"));
                break;
            case IRangedArgumentValidator rangedArgumentValidator:
                PrintMessageToServerOrPlayerChat(context.Client, ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(context.Client, "Common.ValidationFailure.ArgumentIsMustBeInRange", rangedArgumentValidator.ArgumentIndex, rangedArgumentValidator.GetRangeDescription()));
                break;
        }
        
        return ValidationFailureResult.SilentAbort();
    }

    protected override void ExecuteCommand(IGameClient? client, StringCommand commandInfo, ValidatedArguments? validatedArguments)
    {
        RoundEndReason reason = RoundEndReason.RoundDraw;
        float delaySeconds = validatedArguments!.GetArgument<float>(1);
        if (commandInfo.ArgCount >= 2)
        {
            try
            {
                reason = (RoundEndReason)uint.Parse(commandInfo.GetArg(2));
            }
            catch (Exception)
            {
                // Ignored
            }
        }


        string executor = PlayerUtil.GetPlayerName(client);
        Plugin.TnmsLogger.LogAdminAction(client, $"Admin {executor} terminated round in {delaySeconds} seconds with reason {reason.ToString()}");
        

        foreach (var gameClient in SharedSystem.GetModSharp().GetIServer().GetGameClients(true, true))
        {
            if (gameClient.IsFakeClient || gameClient.IsHltv)
                continue;
            
            gameClient.GetPlayerController()?
                .PrintToChat(
                    ((TnmsAdminUtils)Plugin).LocalizeWithPluginPrefix(gameClient, "TerminateRound.Broadcast.RoundTerminated", executor, delaySeconds, reason.ToString()));
        }
        
        GameRulesUtil.TerminateRound(delaySeconds, reason, true);
    }
}